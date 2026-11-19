using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace CommunityStats.Patches;

/// <summary>
/// Patches RunManager to trigger data preload at run start and upload at run end.
/// Also hooks ModManager.OnMetricsUpload for run data submission.
/// </summary>
[HarmonyPatch]
public static class RunLifecyclePatch
{
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
    [HarmonyPostfix]
    public static void OnRunStartSP(RunManager __instance, RunState state)
    {
        Safe.Run(() =>
        {
            Safe.Info("[DIAG:RunLifecycle] SetUpNewSingleplayer Postfix fired");
            var identity = RunIdentity.FromManager(__instance, state?.Rng?.StringSeed);
            RunDataCollector.BeginNewRun(identity);
            Safe.Info($"[RunLifecycle] new singleplayer run: {identity}");
            // PRD §3.9 / §3.17 round 9: top-bar indicators get built once
            // per run (lifetime mirrors NTopBar's). Defer one frame so
            // NRun.GlobalUi.TopBar is fully laid out by the time we attach.
            CommunityStats.Patches.CombatUiOverlayPatch.OnRunStarted();
        });
    }

    /// <summary>
    /// Round 9 fix: when the user clicks "Continue" from the main menu the
    /// game routes through `SetUpSavedSingleplayer`, NOT `SetUpNewSingleplayer`.
    /// Without this hook the run-start path never fires for resumed runs and
    /// every per-run init (live-state hydrate, top-bar indicators) is skipped.
    /// Crash dump analysis showed the second crash was in the resume path
    /// because `CombatUiOverlayPatch.OnRunStarted` was never called →
    /// `_potion / _cardDrop` stayed null → `AfterSetUpCombat` fallback tried
    /// to lazy-create them mid-combat-init and AV'd inside the Godot binding.
    /// </summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
    [HarmonyPostfix]
    public static void OnRunResumeSP(RunManager __instance, RunState state, SerializableRun save)
    {
        Safe.Run(() =>
        {
            Safe.Info("[DIAG:RunLifecycle] SetUpSavedSingleplayer Postfix fired");
            // SetUpSavedSingleplayer is async and sets _startTime only after
            // its first await. The postfix runs before that, so read StartTime
            // from the save argument instead of RunManager._startTime.
            var identity = new RunIdentity(
                save?.StartTime ?? 0,
                save?.SerializableRng?.Seed ?? state?.Rng?.StringSeed ?? "");
            RunDataCollector.ResumeRun(identity);
            TryHydrateLiveState(identity);
            Safe.Info($"[RunLifecycle] resumed singleplayer run: {identity}");
            CommunityStats.Patches.CombatUiOverlayPatch.OnRunStarted();
        });
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
    [HarmonyPostfix]
    public static void OnRunResumeMP(RunManager __instance, RunState state, LoadRunLobby lobby)
    {
        Safe.Run(() =>
        {
            Safe.Info("[DIAG:RunLifecycle] SetUpSavedMultiplayer Postfix fired");
            var save = lobby?.Run;
            var identity = new RunIdentity(
                save?.StartTime ?? 0,
                save?.SerializableRng?.Seed ?? state?.Rng?.StringSeed ?? "");
            RunDataCollector.ResumeRun(identity);
            TryHydrateLiveState(identity);
            Safe.Info($"[RunLifecycle] resumed multiplayer run: {identity}");
            CommunityStats.Patches.CombatUiOverlayPatch.OnRunStarted();
        });
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
    [HarmonyPostfix]
    public static void OnRunStartMP(RunManager __instance, RunState state)
    {
        Safe.Run(() =>
        {
            var identity = RunIdentity.FromManager(__instance, state?.Rng?.StringSeed);
            RunDataCollector.BeginNewRun(identity);
            Safe.Info($"[RunLifecycle] new multiplayer run: {identity}");
            CommunityStats.Patches.CombatUiOverlayPatch.OnRunStarted();
        });
    }

    /// <summary>
    /// Round 8 §3.6.1: when a run resumes after save+quit, check for a
    /// `live.json` snapshot in the same StartTime+seed directory and
    /// rehydrate the CombatTracker / RunContributionAggregator from it.
    /// </summary>
    private static void TryHydrateLiveState(RunIdentity identity)
    {
        try
        {
            if (!identity.IsValid) return;
            var snap = Util.ContributionPersistence.LoadLiveState(identity);
            if (snap == null) return;
            CombatTracker.Instance.HydrateFromLiveSnapshot(snap);
            Safe.Info($"[RunLifecycle] hydrated live state for {identity} (combatInProgress={snap.CombatInProgress}, runTotalEntries={snap.RunTotals?.Count ?? 0})");
        }
        catch (System.Exception ex)
        {
            Safe.Warn($"TryHydrateLiveState failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Register the OnMetricsUpload hook. Called once at mod init.
    /// </summary>
    public static void RegisterMetricsHook()
    {
        ModManager.OnMetricsUpload += (run, isVictory, localPlayerId) =>
        {
            Safe.Run(() => RunDataCollector.OnMetricsUpload(run, isVictory, localPlayerId));
        };
    }

    /// <summary>
    /// Round 9 round 36: hook the authoritative run-history-write path.
    /// `OnMetricsUpload` is **skipped** by the game when the run was abandoned
    /// (see MetricUtilities.UploadRunMetricsInternal), so we'd never invalidate
    /// our cache for abandoned runs even though the .run file IS written.
    /// `RunHistoryUtilities.CreateRunHistoryEntry` runs for ALL run-end paths
    /// (death / victory / abandon-from-main-menu) and is the function that
    /// actually calls `SaveManager.SaveRunHistory`, so postfixing it gives
    /// us a guaranteed signal to refresh local bundles.
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Runs.RunHistoryUtilities),
        nameof(MegaCrit.Sts2.Core.Runs.RunHistoryUtilities.CreateRunHistoryEntry))]
    [HarmonyPostfix]
    public static void AfterCreateRunHistoryEntry(
        MegaCrit.Sts2.Core.Saves.SerializableRun run, bool victory, bool isAbandoned)
    {
        Safe.Run(() =>
        {
            Safe.Info($"[RunLifecycle] CreateRunHistoryEntry postfix: victory={victory}, abandoned={isAbandoned}");

            // CreateRunHistoryEntry runs for every run-end path. On v0.111.1
            // ModManager.OnMetricsUpload may not fire at all, so finalize here
            // for victory, defeat, and abandoned runs. RunDataCollector
            // ignores duplicate finalization on v0.113.1.
            Safe.Info($"[RunLifecycle] finalizing run via CreateRunHistoryEntry (abandoned={isAbandoned})");
            Collection.RunDataCollector.OnMetricsUpload(run, victory, 0);

            // Drop cached snapshot + bundles, then kick off a forced reload
            // so the next time the player opens the card library / relic
            // collection / career stats screen, fresh data is ready.
            Collection.RunHistoryAnalyzer.Instance.InvalidateAll();
            Safe.RunAsync(async () =>
            {
                try
                {
                    await Collection.RunHistoryAnalyzer.Instance.LoadAllAsync(null, force: true);
                }
                catch (System.Exception ex)
                {
                    Safe.Warn($"[RunLifecycle] post-run reload failed: {ex.Message}");
                }
            });
        });
    }
}
