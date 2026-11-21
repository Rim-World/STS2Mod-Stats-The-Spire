using CommunityStats.Collection;
using CommunityStats.UI;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

using CombatStateType = MegaCrit.Sts2.Core.Combat.ICombatState;

namespace CommunityStats.Patches;

[HarmonyPatch]
public static class CombatLifecyclePatch
{
    // A combat result is saved once and displayed once. On defeat the game
    // finalizes the run (RunManager.OnEnded -> CreateRunHistoryEntry) before
    // CombatManager.CombatEnded, so the flush must happen at the LoseCombat
    // signal; the display still happens at CombatEnded.
    private static bool _combatResultSaved;
    private static bool _combatResultShown;

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
    [HarmonyPostfix]
    public static void AfterSetUpCombat(CombatManager __instance, CombatStateType state)
    {
        _combatResultSaved = false;
        _combatResultShown = false;

        // Subscribe once per CombatManager instance. Both victory and defeat
        // fire CombatEnded exactly once; victory runs AfterCombatVictory
        // (Burning Blood / Black Blood / Meat on the Bone) first, so the flush
        // in the event handler includes those heals. CombatRoom.OnCombatEnded
        // only fires on victory and would miss the defeat path entirely.
        SubscribeCombatEnded(__instance);

        Safe.Run(() =>
        {
            var encounter = state?.Encounter;
            var encounterId = encounter?.Id.Entry ?? "unknown";
            var rawType = encounter?.RoomType.ToString().ToLowerInvariant() ?? "normal";
            // Server enum is strict: normal|elite|boss only. Map Monster→normal
            // and clamp anything unexpected (Event combat, modded room types,
            // etc.) to "normal" so the run doesn't 422 on upload.
            var encounterType = rawType switch
            {
                "monster" => "normal",
                "elite" => "elite",
                "boss" => "boss",
                _ => "normal"
            };
            var floor = RunDataCollector.CurrentFloor;

            // Clean up any leaked intent panels from the previous combat.
            IntentHoverPatch.ForceHideAll();

            CombatTracker.Instance.OnCombatStart(encounterId, encounterType, floor);
            ContributionPersistence.SetActiveFloor(floor);
            Safe.Info($"Combat started: {encounterId} ({encounterType}) on floor {floor}");

            // Round 9 round 6: retry the intent-metadata eager bake here. At
            // mod-init time ModelDb._contentById is empty, so Initialize() just
            // logs "deferred". By the time combat starts, ModelDb.Init has run
            // and every monster is reachable — idempotent via `_eagerBakeDone`.
            MonsterIntentMetadata.Initialize();
        });
    }

    /// <summary>
    /// Increment the turn counter so CombatTracker.TurnCount tracks the
    /// number of player turns that have started in the current combat.
    /// Without this, _turnCount stayed at 0 for the whole combat and the
    /// "每回合平均伤害" readout fell back to "total damage / max(1, 0)" =
    /// total damage. Harmony PREFIX on the async Hook.AfterPlayerTurnStart
    /// runs synchronously before the state machine body, which is the
    /// earliest point we know a new player turn is starting — one call
    /// per turn, per combat. Safe.Run guards against any exception
    /// interrupting the game's own turn-start hook dispatch.
    /// </summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
    [HarmonyPrefix]
    public static void BeforePlayerTurnStartHook()
    {
        Safe.Run(() => CombatTracker.Instance.OnTurnStart());
    }

    // OnCombatEnded is a sync void method on CombatRoom — Postfix works correctly.
    // CombatRoom.OnCombatEnded fires before AfterCombatVictory and never on a
    // loss, so it must not flush the combat. Only clean up hover panels here;
    // the save/display/flush lives in OnCombatManagerEnded below.
    [HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.OnCombatEnded))]
    [HarmonyPostfix]
    public static void AfterCombatEnded(CombatRoom __instance)
    {
        Safe.Run(() =>
        {
            // Clean up any lingering intent panels immediately when combat ends.
            // Monsters that died while the mouse was hovering over them won't
            // fire OnUnfocus, so the panel stays orphaned on the scene root.
            IntentHoverPatch.ForceHideAll();
        });
    }

    /// <summary>
    /// Event combats whose encounter sets ShouldGiveRewards=false (for example
    /// the Battleworn Dummy) call NCombatUi.OnCombatWon -> ProceedWithoutRewards
    /// immediately. That path exits and resets the combat room before
    /// EndCombatInternal reaches its CombatEnded invocation at the end of the
    /// method. Save and display at the exit boundary instead. The prefix runs
    /// before CombatManager.Reset, so the tracker still holds the live combat.
    /// Normal combats have already saved in OnCombatManagerEnded; the flags
    /// make this fallback idempotent.
    /// </summary>
    [HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.Exit))]
    [HarmonyPrefix]
    public static void BeforeCombatRoomExit(CombatRoom __instance)
    {
        Safe.Run(() =>
        {
            // A mid-combat exit is handled by RunManager.OnEnded / LoseCombat.
            // Save here only after EndCombatInternal has marked combat finished.
            if (CombatManager.Instance.IsInProgress) return;
            if (!CombatTracker.Instance.LiveCombatInProgress) return;
            if (_combatResultSaved) return;

            Safe.Info("[CombatLifecycle] combat room exit fallback");
            SaveCombatResultOnce();
            ShowCombatResultOnce();
        });
    }

    private static void SubscribeCombatEnded(CombatManager manager)
    {
        try
        {
            // -= before += makes repeated combat setups idempotent.
            manager.CombatEnded -= OnCombatManagerEnded;
            manager.CombatEnded += OnCombatManagerEnded;
        }
        catch (Exception ex)
        {
            Safe.Warn($"[CombatLifecycle] CombatEnded subscribe failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Single post-combat entry point for both victory and defeat.
    /// OnCombatEnd/save/display are all idempotent, so nested or duplicate
    /// signals cannot flush the same combat twice.
    /// </summary>
    private static void OnCombatManagerEnded(CombatRoom room)
    {
        Safe.Run(() =>
        {
            IntentHoverPatch.ForceHideAll();
            SaveCombatResultOnce();
            ShowCombatResultOnce();
        });
    }

    /// <summary>
    /// Flush the current combat into the aggregator and persist its per-combat
    /// snapshot. Called from CombatEnded, LoseCombat, RunManager.OnEnded, and
    /// the CombatRoom.Exit fallback for no-reward event combats.
    /// The run-end prefix is required on defeat: OnEnded calls
    /// CreateRunHistoryEntry (which finalizes run totals from disk) before the
    /// pending loss produces CombatEnded.
    /// </summary>
    private static void SaveCombatResultOnce()
    {
        if (_combatResultSaved) return;
        if (!CombatTracker.Instance.LiveCombatInProgress
            && CombatTracker.Instance.LastCombatData == null)
            return;

        _combatResultSaved = true;

        if (CombatTracker.Instance.LiveCombatInProgress)
            CombatTracker.Instance.OnCombatEnd();

        // Persist this combat snapshot for future Run History replay (PRD §3.12).
        var lastData = CombatTracker.Instance.LastCombatData;
        Safe.Info($"[CombatLifecycle] combat result floor={RunDataCollector.CurrentFloor} lastCombatData.Count={lastData?.Count ?? 0}");
        ContributionPersistence.SaveCombat(
            RunDataCollector.CurrentFloor,
            lastData);
    }

    private static void ShowCombatResultOnce()
    {
        if (_combatResultShown) return;
        if (!Config.ModConfig.Toggles.ContributionPanel) return;
        _combatResultShown = true;

        ContributionPanel.ShowCombatResult(CombatTracker.Instance.LastCombatData);
        Safe.Info("Combat ended, contribution panel shown");
    }

    /// <summary>
    /// Safety net for run-end paths that never reach CombatEnded (defeat calls
    /// OnEnded while the pending loss is still queued; abandoning mid-combat
    /// also ends the run without a combat-end event). Observer only.
    /// </summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
    [HarmonyPrefix]
    public static void BeforeRunEnded()
    {
        Safe.Run(SaveCombatResultOnce);
    }

    /// <summary>
    /// Defeat requests loss from LoseCombat; the death screen can render
    /// before CombatEnded, so hide the hover panel here as well and record
    /// the player death before the pending-loss path flushes the combat.
    /// The flush is also saved here because RunManager.OnEnded finalizes the
    /// run immediately after this call.
    /// </summary>
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.LoseCombat))]
    [HarmonyPostfix]
    public static void AfterLoseCombat()
    {
        Safe.Run(IntentHoverPatch.ForceHideAll);
        Safe.Run(() => CombatTracker.Instance.OnPlayerDied());
        Safe.Run(SaveCombatResultOnce);
    }

    /// <summary>
    /// Auto-close the contribution panel when the player clicks the Proceed button
    /// after collecting rewards. ProceedFromTerminalRewardsScreen is the async method
    /// called by NRewardsScreen.OnProceedButtonPressed — a Prefix runs synchronously
    /// before the method starts, which is the right time to hide the panel.
    /// </summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
    [HarmonyPrefix]
    public static void BeforeProceed()
    {
        Safe.Run(() => ContributionPanel.Hide());
    }
}
