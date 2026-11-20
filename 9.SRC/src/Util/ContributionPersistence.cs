using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityStats.Collection;
using CommunityStats.Config;
using MegaCrit.Sts2.Core.Runs;

namespace CommunityStats.Util;

/// <summary>
/// Serializes per-combat and per-run contribution data to disk so the
/// Run History detail screen can load and replay them later.
///
/// New layout (1.5.0):
///   {DataDir}/contributions/{StartTime}_{seed}/
///     meta.json
///     combat_{floor}_{sequence}.json
///     summary.json
///     live.json
///     shop_purchases.json
///     shop_offers.json
///
/// Legacy layout (1.4.4 and earlier) is read-only:
///   {DataDir}/contributions/{seed}_{floor}.json
///   {DataDir}/contributions/{seed}_summary.json
///   {DataDir}/contributions/{seed}_live.json
///
/// Files older than 90 days are pruned at mod init.
/// </summary>
public static class ContributionPersistence
{
    private const int RetentionDays = 90;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    private static RunIdentity? _activeIdentity;
    private static int _activeFloor;

    public static void SetActiveRun(RunIdentity identity)
    {
        if (identity != null && identity.IsValid)
            _activeIdentity = identity;
    }

    /// <summary>
    /// Compatibility entry point for legacy callers. New code must use
    /// <see cref="SetActiveRun"/> so contributions get a per-run directory.
    /// </summary>
    public static void SetActiveSeed(string seed) => SetActiveRun(new RunIdentity(0, seed));

    public static void SetActiveFloor(int floor) => _activeFloor = floor;

    public static void ClearRunContext()
    {
        _activeIdentity = null;
        _activeFloor = 0;
    }

    public static RunIdentity? GetActiveIdentity()
    {
        if (_activeIdentity is { IsValid: true }) return _activeIdentity;

        try
        {
            var state = RunManager.Instance?.DebugOnlyGetState();
            var seed = state?.Rng?.StringSeed;
            if (string.IsNullOrEmpty(seed)) return null;

            var identity = RunIdentity.FromManager(RunManager.Instance, seed);
            if (identity.IsValid)
            {
                _activeIdentity = identity;
                return identity;
            }
        }
        catch (Exception ex)
        {
            Safe.Warn($"ContributionPersistence: GetActiveIdentity failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Resolve the seed of the active run, or null if no run is active.
    /// Kept for callers that only need the display seed.
    /// </summary>
    public static string? GetActiveSeed()
    {
        var identity = GetActiveIdentity();
        if (identity is { IsValid: true }) return identity.Seed;

        try
        {
            var state = RunManager.Instance?.DebugOnlyGetState();
            return state?.Rng?.StringSeed;
        }
        catch (Exception ex)
        {
            Safe.Warn($"ContributionPersistence: GetActiveSeed failed: {ex.Message}");
            return null;
        }
    }

    // ── Public API ──────────────────────────────────────────

    /// <summary>
    /// Persist a single combat snapshot. Called from CombatLifecyclePatch.AfterCombatEnded.
    /// </summary>
    public static void SaveCombat(int floor, IReadOnlyDictionary<string, ContributionAccum>? data)
    {
        if (data == null || data.Count == 0) return;
        var identity = GetActiveIdentity();
        if (identity == null || !identity.IsValid) return;

        int effectiveFloor = floor > 0 ? floor : _activeFloor;
        Safe.Run(() =>
        {
            RunStorage.EnsureDirectory(identity);
            int sequence = RunStorage.NextCombatSequence(identity, effectiveFloor);
            var path = RunStorage.CombatPath(identity, effectiveFloor, sequence);
            var dto = new ContributionDoc
            {
                StartTime = identity.StartTime,
                Seed = identity.Seed,
                Floor = effectiveFloor,
                Kind = "combat",
                SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Sources = data.Values.Select(ToDto).ToList(),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOpts));
        });
    }

    /// <summary>
    /// Persist the full-run aggregate. Called from RunLifecyclePatch / RunDataCollector.OnMetricsUpload.
    /// </summary>
    public static void SaveRunSummary(RunIdentity identity, IReadOnlyDictionary<string, ContributionAccum>? data)
    {
        if (data == null || data.Count == 0 || identity == null || !identity.IsValid) return;

        Safe.Run(() =>
        {
            RunStorage.EnsureDirectory(identity);
            var path = RunStorage.SummaryPath(identity);
            var dto = new ContributionDoc
            {
                StartTime = identity.StartTime,
                Seed = identity.Seed,
                Kind = "summary",
                SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Sources = data.Values.Select(ToDto).ToList(),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOpts));
        });
    }

    /// <summary>
    /// Load the run summary for historical replay. New per-run data is tried
    /// first. If no new directory exists, the old seed-keyed files are read
    /// as a legacy fallback. Legacy data is never rewritten.
    /// </summary>
    public static IReadOnlyDictionary<string, ContributionAccum>? LoadRunSummary(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return null;

        if (identity.HasStartTime)
        {
            var direct = TryLoadDoc(RunStorage.SummaryPath(identity));
            if (direct != null && direct.Count > 0) return direct;

            var assembled = AssembleFromCombats(identity);
            if (assembled != null && assembled.Count > 0) return assembled;

            if (RunStorage.HasNewDirectory(identity))
                return null;
        }

        return LoadLegacyRunSummary(identity.Seed);
    }

    /// <summary>
    /// Compatibility overload for callers that only have a seed. It can only
    /// read the legacy flat layout.
    /// </summary>
    public static IReadOnlyDictionary<string, ContributionAccum>? LoadRunSummary(string seed)
        => LoadRunSummary(new RunIdentity(0, seed));

    private static IReadOnlyDictionary<string, ContributionAccum>? LoadLegacyRunSummary(string seed)
    {
        if (string.IsNullOrEmpty(seed)) return null;

        var direct = TryLoadDoc(RunStorage.LegacySummaryPath(seed));
        if (direct != null && direct.Count > 0)
        {
            Safe.Info($"[ContributionPersistence] using legacy summary for seed={seed}");
            return direct;
        }

        var assembled = AssembleLegacyFromCombats(seed);
        if (assembled != null && assembled.Count > 0)
            Safe.Info($"[ContributionPersistence] using legacy combat merge for seed={seed}");
        return assembled;
    }

    private static IReadOnlyDictionary<string, ContributionAccum>? TryLoadDoc(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<ContributionDoc>(json, JsonOpts);
            return dto?.Sources != null && dto.Sources.Count > 0
                ? ToDictionaryMerge(dto.Sources)
                : null;
        }
        catch (Exception ex)
        {
            Safe.Warn($"ContributionPersistence: load {Path.GetFileName(path)} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Re-hydrate in-memory run totals from every per-combat file for one
    /// concrete run. Called at upload time so the payload always has the
    /// authoritative per-combat record even when in-memory state was wiped.
    /// </summary>
    public static void AssembleAndHydrateRunTotals(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return;
        var merged = AssembleFromCombats(identity);
        if (merged == null || merged.Count == 0) return;
        RunContributionAggregator.Instance.HydrateRunTotals(merged);
        Safe.Info($"ContributionPersistence: hydrated run totals from {merged.Count} disk sources ({identity})");
    }

    /// <summary>
    /// Walk every combat file for one concrete run and merge into a single
    /// dictionary. New layout scans only the run directory; legacy identity
    /// scans the old flat seed pattern.
    /// </summary>
    public static IReadOnlyDictionary<string, ContributionAccum>? AssembleFromCombats(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return null;
        if (!identity.HasStartTime) return AssembleLegacyFromCombats(identity.Seed);

        var dir = RunStorage.DirectoryFor(identity);
        if (!Directory.Exists(dir)) return null;

        var merged = new Dictionary<string, ContributionAccum>();
        int files = 0;
        foreach (var path in Directory.EnumerateFiles(dir, "combat_*.json"))
        {
            var combat = TryLoadDoc(path);
            if (combat == null) continue;
            files++;
            foreach (var (key, accum) in combat)
            {
                if (merged.TryGetValue(key, out var existing))
                    existing.MergeFrom(accum);
                else
                    merged[key] = Clone(accum);
            }
        }
        return files > 0 ? merged : null;
    }

    private static IReadOnlyDictionary<string, ContributionAccum>? AssembleLegacyFromCombats(string seed)
    {
        var dir = ModConfig.ContributionsDir;
        if (!Directory.Exists(dir) || string.IsNullOrEmpty(seed)) return null;

        var pattern = RunStorage.LegacyCombatPattern(seed);
        var merged = new Dictionary<string, ContributionAccum>();
        int files = 0;
        foreach (var path in Directory.EnumerateFiles(dir, pattern))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("_summary", StringComparison.Ordinal)) continue;
            if (name.EndsWith("_live", StringComparison.Ordinal)) continue;
            if (name.EndsWith("_shop_offers", StringComparison.Ordinal)) continue;
            if (name.EndsWith("_shop_purchases", StringComparison.Ordinal)) continue;

            var combat = TryLoadDoc(path);
            if (combat == null) continue;
            files++;
            foreach (var (key, accum) in combat)
            {
                if (merged.TryGetValue(key, out var existing))
                    existing.MergeFrom(accum);
                else
                    merged[key] = Clone(accum);
            }
        }
        return files > 0 ? merged : null;
    }

    /// <summary>
    /// Deserialize a list of SourceDto into a dictionary. The in-memory key
    /// is sourceId + originId (the same composite used by CombatTracker),
    /// so a deck-native card and a generated copy with the same SourceId do
    /// not merge into one inflated entry.
    /// </summary>
    private static Dictionary<string, ContributionAccum> ToDictionaryMerge(List<SourceDto> sources)
    {
        var result = new Dictionary<string, ContributionAccum>();
        foreach (var s in sources)
        {
            var key = MakeSourceKey(s.SourceId, s.OriginSourceId);
            if (result.TryGetValue(key, out var existing))
                existing.MergeFrom(FromDto(s));
            else
                result[key] = FromDto(s);
        }
        return result;
    }

    private static string MakeSourceKey(string sourceId, string? originSourceId)
        => string.IsNullOrEmpty(originSourceId) ? sourceId : sourceId + "\u0001" + originSourceId;

    private static ContributionAccum Clone(ContributionAccum src)
    {
        var dst = new ContributionAccum
        {
            SourceId = src.SourceId,
            SourceType = src.SourceType,
        };
        dst.MergeFrom(src);
        return dst;
    }

    // ── Live mid-run state ──────────────────────────────────

    /// <summary>
    /// Persist the in-progress combat tracker state so a save+quit mid-run
    /// doesn't lose 本场战斗 / 本局汇总 data. Called from CombatTracker
    /// after every card play and from CombatLifecyclePatch on combat end.
    /// </summary>
    public static void SaveLiveState(LiveContributionSnapshot snapshot)
    {
        if (snapshot == null) return;
        var identity = GetActiveIdentity();
        if (identity == null || !identity.IsValid) return;

        Safe.Run(() =>
        {
            RunStorage.EnsureDirectory(identity);
            var path = RunStorage.LivePath(identity);
            var dto = new LiveContributionDoc
            {
                StartTime = identity.StartTime,
                Seed = identity.Seed,
                EncounterId = snapshot.EncounterId,
                EncounterType = snapshot.EncounterType,
                Floor = snapshot.Floor,
                TurnCount = snapshot.TurnCount,
                TotalDamageDealt = snapshot.TotalDamageDealt,
                DamageTakenByPlayer = snapshot.DamageTakenByPlayer,
                CombatInProgress = snapshot.CombatInProgress,
                SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CurrentCombat = snapshot.CurrentCombat?.Values.Select(ToDto).ToList() ?? new(),
                RunTotals = snapshot.RunTotals?.Values.Select(ToDto).ToList() ?? new(),
                Encounters = snapshot.Encounters?.Select(ToEncounterDto).ToList() ?? new(),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOpts));
            Godot.GD.Print($"[StatsTheSpire] SaveLiveState: wrote to {path}, runTotalEntries={dto.RunTotals.Count}, encounters={dto.Encounters.Count}");
        });
    }

    /// <summary>
    /// Load the live state for the given run. If the new run directory does
    /// not exist, a legacy seed-keyed snapshot is read as a fallback.
    /// </summary>
    public static LiveContributionSnapshot? LoadLiveState(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return null;

        var path = RunStorage.LivePath(identity);
        if (!File.Exists(path) && identity.HasStartTime && !RunStorage.HasNewDirectory(identity))
            path = RunStorage.LegacyLivePath(identity.Seed);

        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<LiveContributionDoc>(json, JsonOpts);
            if (dto == null) return null;
            var snap = new LiveContributionSnapshot
            {
                EncounterId = dto.EncounterId ?? "",
                EncounterType = dto.EncounterType ?? "",
                Floor = dto.Floor,
                TurnCount = dto.TurnCount,
                TotalDamageDealt = dto.TotalDamageDealt,
                DamageTakenByPlayer = dto.DamageTakenByPlayer,
                CombatInProgress = dto.CombatInProgress,
            };
            if (dto.CurrentCombat != null && dto.CurrentCombat.Count > 0)
                snap.CurrentCombat = ToDictionaryMerge(dto.CurrentCombat);
            if (dto.RunTotals != null && dto.RunTotals.Count > 0)
                snap.RunTotals = ToDictionaryMerge(dto.RunTotals);
            if (dto.Encounters != null && dto.Encounters.Count > 0)
                snap.Encounters = dto.Encounters.Select(FromEncounterDto).ToList();
            return snap;
        }
        catch (Exception ex)
        {
            Safe.Warn($"ContributionPersistence: LoadLiveState failed for {identity}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Delete the live snapshot for a finished run.</summary>
    public static void DeleteLiveState(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return;
        try
        {
            var path = RunStorage.LivePath(identity);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    /// <summary>
    /// Delete contribution files older than 90 days. New run directories are
    /// removed as a unit; legacy flat files are removed by file timestamp.
    /// </summary>
    public static void PruneOldFiles()
    {
        Safe.Run(() =>
        {
            var dir = ModConfig.ContributionsDir;
            if (!Directory.Exists(dir)) return;
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            int removedFiles = 0;
            int removedDirs = 0;

            foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                    {
                        File.Delete(path);
                        removedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    Safe.Warn($"ContributionPersistence: failed to prune {path}: {ex.Message}");
                }
            }

            foreach (var runDir in Directory.EnumerateDirectories(dir))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(runDir) < cutoff)
                    {
                        Directory.Delete(runDir, recursive: true);
                        removedDirs++;
                    }
                }
                catch (Exception ex)
                {
                    Safe.Warn($"ContributionPersistence: failed to prune {runDir}: {ex.Message}");
                }
            }

            if (removedFiles > 0 || removedDirs > 0)
                Safe.Info($"Contribution prune removed {removedFiles} expired files and {removedDirs} run directories.");
        });
    }

    // ── DTO mapping ─────────────────────────────────────────

    private static SourceDto ToDto(ContributionAccum a) => new()
    {
        SourceId = a.SourceId,
        SourceType = a.SourceType,
        TimesPlayed = a.TimesPlayed,
        DirectDamage = a.DirectDamage,
        AttributedDamage = a.AttributedDamage,
        ModifierDamage = a.ModifierDamage,
        UpgradeDamage = a.UpgradeDamage,
        EffectiveBlock = a.EffectiveBlock,
        ModifierBlock = a.ModifierBlock,
        UpgradeBlock = a.UpgradeBlock,
        MitigatedByDebuff = a.MitigatedByDebuff,
        MitigatedByBuff = a.MitigatedByBuff,
        MitigatedByStrReduction = a.MitigatedByStrReduction,
        SelfDamage = a.SelfDamage,
        CardsDrawn = a.CardsDrawn,
        EnergyGained = a.EnergyGained,
        HpHealed = a.HpHealed,
        StarsContribution = a.StarsContribution,
        OriginSourceId = a.OriginSourceId,
    };

    private static ContributionAccum FromDto(SourceDto d) => new()
    {
        SourceId = d.SourceId,
        SourceType = d.SourceType,
        TimesPlayed = d.TimesPlayed,
        DirectDamage = d.DirectDamage,
        AttributedDamage = d.AttributedDamage,
        ModifierDamage = d.ModifierDamage,
        UpgradeDamage = d.UpgradeDamage,
        EffectiveBlock = d.EffectiveBlock,
        ModifierBlock = d.ModifierBlock,
        UpgradeBlock = d.UpgradeBlock,
        MitigatedByDebuff = d.MitigatedByDebuff,
        MitigatedByBuff = d.MitigatedByBuff,
        MitigatedByStrReduction = d.MitigatedByStrReduction,
        SelfDamage = d.SelfDamage,
        CardsDrawn = d.CardsDrawn,
        EnergyGained = d.EnergyGained,
        HpHealed = d.HpHealed,
        StarsContribution = d.StarsContribution,
        OriginSourceId = d.OriginSourceId,
    };

    // ── Wire format ─────────────────────────────────────────

    private sealed class ContributionDoc
    {
        public long StartTime { get; set; }
        public string Seed { get; set; } = "";
        public int? Floor { get; set; }
        public string Kind { get; set; } = "";
        public long SavedAtUnix { get; set; }
        public List<SourceDto> Sources { get; set; } = new();
    }

    /// <summary>Live mid-run snapshot — `live.json`. Round 8 §3.6.1.</summary>
    private sealed class LiveContributionDoc
    {
        public long StartTime { get; set; }
        public string Seed { get; set; } = "";
        public string? EncounterId { get; set; }
        public string? EncounterType { get; set; }
        public int Floor { get; set; }
        public int TurnCount { get; set; }
        public int TotalDamageDealt { get; set; }
        public int DamageTakenByPlayer { get; set; }
        public bool CombatInProgress { get; set; }
        public long SavedAtUnix { get; set; }
        public List<SourceDto> CurrentCombat { get; set; } = new();
        public List<SourceDto> RunTotals { get; set; } = new();
        public List<EncounterDto> Encounters { get; set; } = new();
    }

    private sealed class EncounterDto
    {
        public string EncounterId { get; set; } = "";
        public string EncounterType { get; set; } = "";
        public int Floor { get; set; }
        public int DamageTaken { get; set; }
        public int TurnsTaken { get; set; }
        public bool PlayerDied { get; set; }
    }

    private static EncounterDto ToEncounterDto(RunContributionAggregator.EncounterRecord e) => new()
    {
        EncounterId = e.EncounterId,
        EncounterType = e.EncounterType,
        Floor = e.Floor,
        DamageTaken = e.DamageTaken,
        TurnsTaken = e.TurnsTaken,
        PlayerDied = e.PlayerDied,
    };

    private static RunContributionAggregator.EncounterRecord FromEncounterDto(EncounterDto d) => new(
        d.EncounterId,
        d.EncounterType,
        d.Floor,
        d.DamageTaken,
        d.TurnsTaken,
        d.PlayerDied);

    private sealed class SourceDto
    {
        public string SourceId { get; set; } = "";
        public string SourceType { get; set; } = "card";
        public int TimesPlayed { get; set; }
        public int DirectDamage { get; set; }
        public int AttributedDamage { get; set; }
        public int ModifierDamage { get; set; }
        public int UpgradeDamage { get; set; }
        public int EffectiveBlock { get; set; }
        public int ModifierBlock { get; set; }
        public int UpgradeBlock { get; set; }
        public int MitigatedByDebuff { get; set; }
        public int MitigatedByBuff { get; set; }
        public int MitigatedByStrReduction { get; set; }
        public int SelfDamage { get; set; }
        public int CardsDrawn { get; set; }
        public int EnergyGained { get; set; }
        public int HpHealed { get; set; }
        public int StarsContribution { get; set; }
        public string? OriginSourceId { get; set; }
    }
}
