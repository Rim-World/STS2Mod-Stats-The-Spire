using System.Text.Json;
using CommunityStats.Config;
using MegaCrit.Sts2.Core.Saves;

namespace CommunityStats.Util;

/// <summary>
/// Startup retention for contribution snapshots. The policy is opt-out:
/// 90-day cleanup is enabled by default, and the RitsuLib settings page can
/// disable it. The current single-player and multiplayer save files are read
/// before pruning so an old but still playable run is never deleted.
/// </summary>
public static class ContributionRetention
{
    public const int RetentionDays = 90;
    public const string SettingKey = "auto_cleanup";

    private static bool _startupScheduled;

    public readonly record struct CurrentRunSaveState(
        bool ProfileReady,
        bool HasCurrentSave,
        bool ParseFailed,
        IReadOnlyCollection<long> StartTimes,
        IReadOnlyCollection<string> Seeds);

    public readonly record struct PruneResult(
        int DeletedFiles,
        int DeletedDirectories,
        int SkippedProtected,
        int SkippedReparsePoints);

    /// <summary>
    /// Schedule the one startup prune. Profile initialization happens after
    /// mod initialization in NGame.GameStartup, so wait for it before reading
    /// current_run.save. If the profile is still unavailable, skip the prune
    /// instead of risking deletion of the active run.
    /// </summary>
    public static void ScheduleStartupPrune()
    {
        if (_startupScheduled) return;
        _startupScheduled = true;

        Safe.RunAsync(async () =>
        {
            if (!ModConfig.AutoCleanupEnabled)
            {
                Safe.Info("[Retention] auto-cleanup disabled by setting; no files removed.");
                return;
            }

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!IsProfileReady() && DateTime.UtcNow < deadline)
                await Task.Delay(100);

            if (!IsProfileReady())
            {
                Safe.Warn("[Retention] SaveManager profile not initialized; cleanup skipped to protect the current save.");
                return;
            }

            var current = ReadCurrentRunRefs();
            if (current.ParseFailed)
            {
                Safe.Warn("[Retention] current save could not be parsed; cleanup skipped to protect the current save.");
                return;
            }

            var result = Prune(
                ModConfig.ContributionsDir,
                DateTime.UtcNow.AddDays(-RetentionDays),
                current.StartTimes,
                current.Seeds,
                enabled: true);

            Safe.Info(
                $"[Retention] auto-cleanup enabled; currentSaves={current.StartTimes.Count}; " +
                $"removedFiles={result.DeletedFiles}; removedDirs={result.DeletedDirectories}; " +
                $"skippedProtected={result.SkippedProtected}; skippedReparse={result.SkippedReparsePoints}.");
        });
    }

    /// <summary>
    /// Read the current single-player and multiplayer save files. A missing
    /// current save is normal (for example, after a finished run). A corrupt
    /// save is not normal; callers must not prune when ParseFailed is true.
    /// </summary>
    public static CurrentRunSaveState ReadCurrentRunRefs()
    {
        var startTimes = new List<long>();
        var seeds = new List<string>();
        bool hasSave = false;
        bool parseFailed = false;

        try
        {
            if (!SaveManager.Instance.IsProfileInitialized)
                return new CurrentRunSaveState(false, false, false, startTimes, seeds);

            var savesDir = SaveManager.Instance.GetProfileScopedPath("saves");
            ReadSaveFiles(savesDir, startTimes, seeds, ref hasSave, ref parseFailed);
        }
        catch (Exception ex)
        {
            Safe.Warn($"[Retention] failed to read current save: {ex.Message}");
            parseFailed = true;
        }

        return new CurrentRunSaveState(true, hasSave, parseFailed, startTimes, seeds);
    }

    /// <summary>
    /// Read current save files from an explicit saves directory. Used by the
    /// probe to exercise the same path and parse logic with a temporary file.
    /// </summary>
    public static CurrentRunSaveState ReadCurrentRunRefs(string savesDir)
    {
        var startTimes = new List<long>();
        var seeds = new List<string>();
        bool hasSave = false;
        bool parseFailed = false;
        ReadSaveFiles(savesDir, startTimes, seeds, ref hasSave, ref parseFailed);
        return new CurrentRunSaveState(true, hasSave, parseFailed, startTimes, seeds);
    }

    private static void ReadSaveFiles(
        string savesDir,
        List<long> startTimes,
        List<string> seeds,
        ref bool hasSave,
        ref bool parseFailed)
    {
        foreach (var path in CandidateSavePaths(savesDir))
        {
            if (!File.Exists(path)) continue;
            hasSave = true;
            if (!TryReadSaveFile(path, out long startTime, out string seed))
            {
                parseFailed = true;
                continue;
            }

            if (startTime > 0) startTimes.Add(startTime);
            if (!string.IsNullOrWhiteSpace(seed)) seeds.Add(seed);
        }
    }

    /// <summary>
    /// Parse the fields needed to protect one current save. The game stores
    /// start_time at the root and the content seed at rng.seed.
    /// </summary>
    public static bool TryReadSaveFile(string path, out long startTime, out string seed)
    {
        startTime = 0;
        seed = "";

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (!TryGetLong(root, "start_time", out startTime) &&
                !TryGetLong(root, "StartTime", out startTime))
                return false;
            if (startTime <= 0) return false;

            if (root.TryGetProperty("rng", out var rng) &&
                rng.ValueKind == JsonValueKind.Object &&
                rng.TryGetProperty("seed", out var seedElement) &&
                seedElement.ValueKind == JsonValueKind.String)
                seed = seedElement.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(seed) &&
                root.TryGetProperty("seed", out var rootSeed) &&
                rootSeed.ValueKind == JsonValueKind.String)
                seed = rootSeed.GetString() ?? "";

            return true;
        }
        catch (Exception ex)
        {
            Safe.Warn($"[Retention] failed to parse current save {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Remove expired top-level legacy files and run directories. This method
    /// is public so a deterministic probe can exercise the real policy without
    /// touching the player's data directory.
    /// </summary>
    public static PruneResult Prune(
        string contributionsDir,
        DateTime cutoffUtc,
        IReadOnlyCollection<long>? protectedStartTimes,
        IReadOnlyCollection<string>? protectedSeeds,
        bool enabled)
    {
        if (!enabled)
            return new PruneResult(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(contributionsDir) || !Directory.Exists(contributionsDir))
            return new PruneResult(0, 0, 0, 0);

        var cutoff = cutoffUtc.Kind == DateTimeKind.Utc
            ? cutoffUtc
            : cutoffUtc.ToUniversalTime();
        var protectedStarts = new HashSet<long>(protectedStartTimes ?? Array.Empty<long>());
        var protectedSeedNames = new HashSet<string>(
            (protectedSeeds ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(RunStorage.Sanitize),
            StringComparer.OrdinalIgnoreCase);

        int deletedFiles = 0;
        int deletedDirectories = 0;
        int skippedProtected = 0;
        int skippedReparse = 0;

        foreach (var path in Directory.EnumerateFiles(contributionsDir, "*.json"))
        {
            try
            {
                if (IsReparsePoint(path))
                {
                    skippedReparse++;
                    continue;
                }

                if (IsProtectedFile(path, protectedStarts, protectedSeedNames))
                {
                    skippedProtected++;
                    continue;
                }

                if (File.GetLastWriteTimeUtc(path) >= cutoff) continue;
                File.Delete(path);
                deletedFiles++;
            }
            catch (Exception ex)
            {
                Safe.Warn($"[Retention] failed to prune file {path}: {ex.Message}");
            }
        }

        foreach (var path in Directory.EnumerateDirectories(contributionsDir))
        {
            try
            {
                if (IsReparsePoint(path))
                {
                    skippedReparse++;
                    continue;
                }

                if (IsProtectedDirectory(path, protectedStarts))
                {
                    skippedProtected++;
                    continue;
                }

                if (Directory.GetLastWriteTimeUtc(path) >= cutoff) continue;
                Directory.Delete(path, recursive: true);
                deletedDirectories++;
            }
            catch (Exception ex)
            {
                Safe.Warn($"[Retention] failed to prune directory {path}: {ex.Message}");
            }
        }

        return new PruneResult(deletedFiles, deletedDirectories, skippedProtected, skippedReparse);
    }

    private static IEnumerable<string> CandidateSavePaths(string savesDir)
    {
        yield return Path.Combine(savesDir, "current_run.save");
        yield return Path.Combine(savesDir, "current_run.save.backup");
        yield return Path.Combine(savesDir, "current_run_mp.save");
        yield return Path.Combine(savesDir, "current_run_mp.save.backup");
    }

    private static bool IsProfileReady()
    {
        try { return SaveManager.Instance.IsProfileInitialized; }
        catch { return false; }
    }

    private static bool TryGetLong(JsonElement root, string name, out long value)
    {
        value = 0;
        return root.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt64(out value);
    }

    private static bool IsProtectedFile(
        string path,
        HashSet<long> protectedStarts,
        HashSet<string> protectedSeeds)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        foreach (var seed in protectedSeeds)
        {
            if (stem.Equals(seed, StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith(seed + "_", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var start in protectedStarts)
        {
            if (stem.Equals(start.ToString(), StringComparison.Ordinal) ||
                stem.StartsWith(start + "_", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsProtectedDirectory(string path, HashSet<long> protectedStarts)
    {
        var name = Path.GetFileName(path);
        int separator = name.IndexOf('_');
        if (separator <= 0) return false;
        return long.TryParse(name[..separator], out long start) &&
               protectedStarts.Contains(start);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }
}
