using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.Settings;

namespace StatsTheSpire.RetentionProbe;

/// <summary>
/// Test-only probe for the 1.9.4 retention policy. It uses a temporary
/// contributions directory and only creates a real current_run.save when
/// all four current-save candidates are absent; the file is deleted in
/// finally and the player's contributions directory is never touched.
/// </summary>
[ModInitializer("Initialize")]
public static class RetentionProbe
{
    private static int _frames;
    private static bool _ran;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_ran || _frames < 180) return;
            _ran = true;
            try
            {
                _ = RunAsync(tree);
            }
            catch (Exception ex)
            {
                GD.Print($"[RetentionProbe] FATAL {ex}");
                tree.Quit(1);
            }
        };
    }

    private static async Task RunAsync(SceneTree tree)
    {
        bool pass = true;
        string temp = Path.Combine(Path.GetTempPath(), "sts2-retention-probe-" + Guid.NewGuid().ToString("N"));

        try
        {
            for (int i = 0; i < 600 && !SaveManager.Instance.IsProfileInitialized; i++)
                await NextFrame(tree);

            GD.Print($"[RetentionProbe] profileReady={SaveManager.Instance.IsProfileInitialized} settingDefault={ModConfig.AutoCleanupEnabled}");
            pass &= ModConfig.AutoCleanupEnabled;

            string rawSaveDir = SaveManager.Instance.GetProfileScopedPath("saves");
            string resolvedSaveDir = ContributionRetention.ResolveFileSystemPath(rawSaveDir);
            bool savePathOk = !string.IsNullOrWhiteSpace(resolvedSaveDir) &&
                              !resolvedSaveDir.StartsWith("user://", StringComparison.OrdinalIgnoreCase) &&
                              Path.IsPathFullyQualified(resolvedSaveDir) &&
                              Directory.Exists(resolvedSaveDir);
            GD.Print($"[RetentionProbe] path raw={rawSaveDir} resolved={resolvedSaveDir} exists={Directory.Exists(resolvedSaveDir)} ok={savePathOk}");
            pass &= savePathOk;

            bool allProfilePathsOk = true;
            for (int profileId = 1; profileId <= 3; profileId++)
            {
                string raw = UserDataPathProvider.GetProfileScopedPath(profileId, "saves");
                string resolved = ContributionRetention.ResolveFileSystemPath(raw);
                bool ok = !string.IsNullOrWhiteSpace(resolved) &&
                          !resolved.StartsWith("user://", StringComparison.OrdinalIgnoreCase) &&
                          Path.IsPathFullyQualified(resolved);
                allProfilePathsOk &= ok;
                GD.Print($"[RetentionProbe] profile{profileId} pathOk={ok} exists={Directory.Exists(resolved)} resolved={resolved}");
            }
            pass &= allProfilePathsOk;

            var state = ContributionRetention.ReadCurrentRunRefs();
            GD.Print($"[RetentionProbe] current profileReady={state.ProfileReady} hasSave={state.HasCurrentSave} parseFailed={state.ParseFailed} starts={state.StartTimes.Count} seeds={state.Seeds.Count}");

            var pages = ModSettingsRegistry.GetPages()
                .Where(p => string.Equals(p.ModId, "sts2_statsthespire_local", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var toggle = pages.SelectMany(p => p.Sections).SelectMany(s => s.Entries)
                .OfType<ToggleModSettingsEntryDefinition>()
                .FirstOrDefault(e => e.Id == "auto_cleanup");
            bool settingsOk = toggle != null && toggle.Binding.Read() == ModConfig.AutoCleanupEnabled;
            GD.Print($"[RetentionProbe] settingsPages={pages.Count} autoCleanupToggle={toggle != null} binding={toggle?.Binding.Read()} config={ModConfig.AutoCleanupEnabled} ok={settingsOk}");
            pass &= settingsOk;

            if (toggle != null)
            {
                bool original = toggle.Binding.Read();
                toggle.Binding.Write(!original);
                toggle.Binding.Save();
                bool wrote = ModConfig.AutoCleanupEnabled == !original;
                toggle.Binding.Write(original);
                toggle.Binding.Save();
                bool restored = ModConfig.AutoCleanupEnabled == original;
                GD.Print($"[RetentionProbe] toggleWrite wrote={wrote} restored={restored} final={ModConfig.AutoCleanupEnabled}");
                pass &= wrote && restored;
            }

            Directory.CreateDirectory(temp);
            var cutoff = DateTime.UtcNow.AddDays(-ContributionRetention.RetentionDays);

            string oldDir = Path.Combine(temp, "1795000000_OLDSEED");
            string protectedDir = Path.Combine(temp, "1795000001_PROTECTED");
            string recentDir = Path.Combine(temp, "1795000002_RECENT");
            string unknownDir = Path.Combine(temp, "NOT_A_RUN");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(protectedDir);
            Directory.CreateDirectory(recentDir);
            Directory.CreateDirectory(unknownDir);
            File.WriteAllText(Path.Combine(oldDir, "combat_1_1.json"), "{}");
            File.WriteAllText(Path.Combine(protectedDir, "combat_2_1.json"), "{}");
            File.WriteAllText(Path.Combine(recentDir, "combat_3_1.json"), "{}");
            File.WriteAllText(Path.Combine(unknownDir, "keep.txt"), "keep");

            string oldLegacy = Path.Combine(temp, "OLDSEED_1.json");
            string protectedLegacy = Path.Combine(temp, "PROTECTED_SEED_1.json");
            string recentLegacy = Path.Combine(temp, "RECENT_1.json");
            File.WriteAllText(oldLegacy, "{}");
            File.WriteAllText(protectedLegacy, "{}");
            File.WriteAllText(recentLegacy, "{}");

            SetOld(oldDir, cutoff);
            SetOld(protectedDir, cutoff);
            SetOld(unknownDir, cutoff);
            SetOld(oldLegacy, cutoff);
            SetOld(protectedLegacy, cutoff);

            var result = ContributionRetention.Prune(
                temp,
                cutoff,
                new long[] { 1795000001 },
                new[] { "PROTECTED_SEED" },
                enabled: true);

            bool removedOldDir = !Directory.Exists(oldDir);
            bool keptProtectedDir = Directory.Exists(protectedDir);
            bool keptRecentDir = Directory.Exists(recentDir);
            bool keptUnknownDir = Directory.Exists(unknownDir);
            bool removedOldLegacy = !File.Exists(oldLegacy);
            bool keptProtectedLegacy = File.Exists(protectedLegacy);
            bool keptRecentLegacy = File.Exists(recentLegacy);
            bool countsOk = result.DeletedFiles == 1 && result.DeletedDirectories == 1 &&
                            result.SkippedProtected == 2;

            GD.Print($"[RetentionProbe] oldDirRemoved={removedOldDir} protectedDirKept={keptProtectedDir} recentDirKept={keptRecentDir} unknownDirKept={keptUnknownDir}");
            GD.Print($"[RetentionProbe] oldLegacyRemoved={removedOldLegacy} protectedLegacyKept={keptProtectedLegacy} recentLegacyKept={keptRecentLegacy}");
            GD.Print($"[RetentionProbe] counts files={result.DeletedFiles} dirs={result.DeletedDirectories} protected={result.SkippedProtected} reparse={result.SkippedReparsePoints}");
            pass &= removedOldDir && keptProtectedDir && keptRecentDir && keptUnknownDir &&
                    removedOldLegacy && keptProtectedLegacy && keptRecentLegacy && countsOk;

            string disabledFile = Path.Combine(temp, "DISABLED_1.json");
            File.WriteAllText(disabledFile, "{}");
            SetOld(disabledFile, cutoff);
            var disabled = ContributionRetention.Prune(
                temp,
                cutoff,
                new long[] { 1795000001 },
                new[] { "PROTECTED_SEED" },
                enabled: false);
            bool disabledKept = File.Exists(disabledFile) &&
                                disabled.DeletedFiles == 0 &&
                                disabled.DeletedDirectories == 0;
            GD.Print($"[RetentionProbe] disabledKept={disabledKept} files={disabled.DeletedFiles} dirs={disabled.DeletedDirectories}");
            pass &= disabledKept;

            string savePath = Path.Combine(temp, "current_run.save");
            File.WriteAllText(savePath, "{\"start_time\":1795000001,\"rng\":{\"seed\":\"PROTECTED_SEED\"}}");
            bool parsed = ContributionRetention.TryReadSaveFile(savePath, out long parsedStart, out string parsedSeed);
            bool parseOk = parsed && parsedStart == 1795000001 && parsedSeed == "PROTECTED_SEED";
            GD.Print($"[RetentionProbe] parse ok={parsed} start={parsedStart} seed={parsedSeed}");
            pass &= parseOk;

            string savesDir = Path.Combine(temp, "saves");
            Directory.CreateDirectory(savesDir);
            File.WriteAllText(Path.Combine(savesDir, "current_run.save"),
                "{\"start_time\":1795000003,\"rng\":{\"seed\":\"AUTO_PROTECT\"}}");
            File.WriteAllText(Path.Combine(savesDir, "current_run_mp.save"),
                "{\"start_time\":1795000004,\"rng\":{\"seed\":\"AUTO_MP\"}}");
            var refsMethod = typeof(ContributionRetention).GetMethod("ReadCurrentRunRefs", new[] { typeof(string) });
            var refs = (ContributionRetention.CurrentRunSaveState)refsMethod!.Invoke(null, new object[] { savesDir })!;
            bool refsOk = refs.ProfileReady && refs.HasCurrentSave && !refs.ParseFailed &&
                          refs.StartTimes.Count == 2 && refs.Seeds.Count == 2;
            GD.Print($"[RetentionProbe] refs hasSave={refs.HasCurrentSave} parseFailed={refs.ParseFailed} starts={refs.StartTimes.Count} seeds={refs.Seeds.Count} ok={refsOk}");
            pass &= refsOk;

            string savesDir2 = Path.Combine(temp, "saves2");
            Directory.CreateDirectory(savesDir2);
            File.WriteAllText(Path.Combine(savesDir2, "current_run.save"),
                "{\"start_time\":1795000005,\"rng\":{\"seed\":\"PROFILE_TWO\"}}");
            var merged = ContributionRetention.ReadCurrentRunRefs(new[] { savesDir, savesDir2 });
            bool mergedOk = merged.HasCurrentSave && !merged.ParseFailed &&
                            merged.StartTimes.Contains(1795000005) &&
                            merged.Seeds.Contains("PROFILE_TWO") &&
                            merged.StartTimes.Count == 3 && merged.Seeds.Count == 3;
            GD.Print($"[RetentionProbe] merged starts={merged.StartTimes.Count} seeds={merged.Seeds.Count} contains={merged.StartTimes.Contains(1795000005)} ok={mergedOk}");
            pass &= mergedOk;

            File.WriteAllText(Path.Combine(savesDir, "current_run.save.backup"), "not-json");
            var corruptRefs = (ContributionRetention.CurrentRunSaveState)refsMethod.Invoke(null, new object[] { savesDir })!;
            bool corruptOk = corruptRefs.HasCurrentSave && corruptRefs.ParseFailed;
            GD.Print($"[RetentionProbe] corrupt parseFailed={corruptRefs.ParseFailed} ok={corruptOk}");
            pass &= corruptOk;

            string realSave = Path.Combine(resolvedSaveDir, "current_run.save");
            string realBackup = realSave + ".backup";
            string realMpSave = Path.Combine(resolvedSaveDir, "current_run_mp.save");
            string realMpBackup = realMpSave + ".backup";
            bool canCreateRealSave = !File.Exists(realSave) &&
                                     !File.Exists(realBackup) &&
                                     !File.Exists(realMpSave) &&
                                     !File.Exists(realMpBackup);
            if (!canCreateRealSave)
            {
                GD.Print("[RetentionProbe] realPath skipped: a current save already exists");
            }
            else
            {
                bool createdRealSave = false;
                bool realOk = false;
                try
                {
                    // Mark before writing so a partial write is still removed.
                    createdRealSave = true;
                    File.WriteAllText(realSave, "{\"start_time\":1795000006,\"rng\":{\"seed\":\"REAL_PATH_SEED\"}}");

                    var realState = ContributionRetention.ReadCurrentRunRefs();
                    string realOldDir = Path.Combine(temp, "1795000006_REAL_PATH_SEED");
                    Directory.CreateDirectory(realOldDir);
                    File.WriteAllText(Path.Combine(realOldDir, "combat_1_1.json"), "{}");
                    SetOld(realOldDir, cutoff);

                    var realPrune = ContributionRetention.Prune(
                        temp,
                        cutoff,
                        realState.StartTimes,
                        realState.Seeds,
                        enabled: true);
                    bool protectedBySave = Directory.Exists(realOldDir);
                    realOk = realState.HasCurrentSave && !realState.ParseFailed &&
                             realState.StartTimes.Contains(1795000006) &&
                             realState.Seeds.Contains("REAL_PATH_SEED") &&
                             protectedBySave && realPrune.SkippedProtected >= 1;
                    GD.Print($"[RetentionProbe] realPath hasSave={realState.HasCurrentSave} parseFailed={realState.ParseFailed} starts={realState.StartTimes.Count} seeds={realState.Seeds.Count} protectedDir={protectedBySave} skipped={realPrune.SkippedProtected} ok={realOk}");
                }
                finally
                {
                    if (createdRealSave)
                    {
                        try { File.Delete(realSave); }
                        catch (Exception ex)
                        {
                            GD.Print($"[RetentionProbe] realPath cleanup failed: {ex.Message}");
                            realOk = false;
                        }
                    }
                }
                pass &= realOk;
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[RetentionProbe] FAIL {ex}");
            pass = false;
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { }
            GD.Print($"[RetentionProbe] RESULT {(pass ? "PASS" : "FAIL")}");
            tree.Quit(pass ? 0 : 1);
        }
    }

    private static void SetOld(string path, DateTime cutoff)
    {
        var old = cutoff.AddDays(-1);
        if (Directory.Exists(path)) Directory.SetLastWriteTimeUtc(path, old);
        else File.SetLastWriteTimeUtc(path, old);
    }

    private static async Task NextFrame(SceneTree tree)
    {
        var tcs = new TaskCompletionSource();
        void OnFrame()
        {
            tree.ProcessFrame -= OnFrame;
            tcs.TrySetResult();
        }
        tree.ProcessFrame += OnFrame;
        await tcs.Task;
    }
}
