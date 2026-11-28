using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityStats.Collection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay.Handlers.Rooms;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace StatsTheSpire.EarringProbe;

/// <summary>
/// Directed probe for WhisperingEarring's first-turn auto-play root.
/// The relic is added during the Play phase, after the natural first-turn
/// hook has passed. The real hook is then called once with a deterministic
/// hand. CardCmd.AutoPlay is blocked outside the probe to keep the test clean.
/// </summary>
[ModInitializer("Initialize")]
public static class EarringProbe
{
    private static bool _subscribed;
    private static bool _started;
    private static bool _blockAutoPlay = true;
    private static int _pass;
    private static int _fail;

    private static readonly AsyncLocal<bool> _probeAutoPlayAL = new();
    private static bool _probeAutoPlay
    {
        get => _probeAutoPlayAL.Value;
        set => _probeAutoPlayAL.Value = value;
    }

    public static void Initialize()
    {
        GD.Print("[EarringProbe] initialized");
        try
        {
            var harmony = new Harmony("test.earring_probe_1120");
            var releaseTarget = AccessTools.Method(typeof(NGame), "IsReleaseGame");
            if (releaseTarget != null)
            {
                harmony.Patch(releaseTarget,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(EarringProbe), nameof(ForceReleaseFalse))));
                GD.Print("[EarringProbe] IsReleaseGame gate patched");
            }

            var autoPlayMethod = AccessTools.Method(typeof(CardCmd), nameof(CardCmd.AutoPlay));
            if (autoPlayMethod != null)
            {
                harmony.Patch(autoPlayMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoPlayGate), nameof(AutoPlayGate.Prefix))));
                GD.Print("[EarringProbe] CardCmd.AutoPlay gate patched");
            }
            else
            {
                GD.Print("[EarringProbe] CardCmd.AutoPlay gate NOT found");
            }

            // AutoSlayer would play the hand and end the turn while the probe
            // is preparing its deterministic case. Let it start the run, but
            // skip its combat loop.
            var autoSlayCombat = AccessTools.Method(typeof(CombatRoomHandler), "PlayCombatAsync",
                new[] { typeof(MegaCrit.Sts2.Core.Random.Rng), typeof(CancellationToken) });
            if (autoSlayCombat != null)
            {
                harmony.Patch(autoSlayCombat,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(EarringProbe), nameof(SkipAutoSlayCombat))));
                GD.Print("[EarringProbe] AutoSlay combat loop blocked");
            }
            else
            {
                GD.Print("[EarringProbe] AutoSlay combat loop NOT found");
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[EarringProbe] Harmony failed: {ex}");
        }

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.Print("[EarringProbe] no SceneTree");
            return;
        }

        tree.ProcessFrame += TrySubscribe;
    }

    public static bool ForceReleaseFalse(bool __result) => false;

    public static bool SkipAutoSlayCombat(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }

    private static void TrySubscribe()
    {
        if (_subscribed) return;
        var cm = CombatManager.Instance;
        if (cm == null) return;
        cm.CombatBegan += OnCombatBegan;
        _subscribed = true;
        GD.Print("[EarringProbe] CombatBegan subscribed");
    }

    private static void OnCombatBegan(CombatState state)
    {
        if (_started) return;
        _started = true;
        TaskHelper.RunSafely(RunLive(state));
    }

    private static async Task RunLive(CombatState state)
    {
        try
        {
            var me = LocalContext.GetMe(state.Players);
            if (me == null)
            {
                Fail("player", "LocalContext.GetMe returned null");
                Finish();
                return;
            }

            if (!await WaitUntil(() => me.PlayerCombatState?.Phase == PlayerTurnPhase.Play, 30000))
            {
                Fail("play-phase", $"phase={me.PlayerCombatState?.Phase}");
                Finish();
                return;
            }

            me.PlayerCombatState!.GainEnergy(99m);
            var ctx = new ThrowingPlayerChoiceContext();

            var attackCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "BLUDGEON");
            var defendCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "DEFEND_SILENT");
            if (attackCanonical == null || defendCanonical == null)
            {
                Fail("cards", $"BLUDGEON={attackCanonical != null} DEFEND_SILENT={defendCanonical != null}");
                Finish();
                return;
            }

            // Remove the natural hand so only the deterministic cards are
            // available. Do this before obtaining the relic: removing cards
            // can fire unrelated relic hooks.
            var hand = PileType.Hand.GetPile(me);
            var oldHand = hand.Cards.ToList();
            if (oldHand.Count > 0)
                await CardPileCmd.RemoveFromCombat(oldHand, skipVisuals: true);

            var attack = state.CreateCard(attackCanonical, me);
            var defend = state.CreateCard(defendCanonical, me);
            await CardPileCmd.AddGeneratedCardsToCombat(
                new List<CardModel> { attack, defend }, PileType.Hand, me, CardPilePosition.Top);

            // Add the relic while the first-turn auto-pre-play hook has already
            // passed. The call below is the real hook, not a synthetic tracker call.
            var earring = await RelicCmd.Obtain<WhisperingEarring>(me);
            GD.Print($"[EarringProbe] relic added type={earring.GetType().Name} id={earring.Id.Entry}");

            int hpBefore = EffectiveEnemyHp(state);
            foreach (var enemy in state.HittableEnemies)
                await CreatureCmd.SetMaxAndCurrentHp(enemy, 300m);
            hpBefore = EffectiveEnemyHp(state);
            _probeAutoPlay = true;
            try
            {
                await earring.AfterAutoPrePlayPhaseEnteredLate(ctx, me);
            }
            finally
            {
                _probeAutoPlay = false;
            }

            await Task.Delay(200);
            int hpAfter = EffectiveEnemyHp(state);
            int actualDamage = Math.Max(0, hpBefore - hpAfter);

            var root = FindRoot("WHISPERING_EARRING");
            var attackRow = FindRoot("BLUDGEON");
            var defendRow = FindRoot("DEFEND_SILENT");

            GD.Print(
                $"[EarringProbe] CASE earring root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"auto={root?.AutoPlayedCount ?? 0} times={root?.TimesPlayed ?? 0} " +
                $"direct={root?.DirectDamage ?? 0} block={root?.EffectiveBlock ?? 0} " +
                $"energy={root?.EnergyGained ?? 0} actualDamage={actualDamage} " +
                $"attackAuto={attackRow?.AutoPlayedCount ?? 0} defendAuto={defendRow?.AutoPlayedCount ?? 0}");

            foreach (var row in CombatTracker.Instance.GetCurrentCombatData().Values
                         .Where(a => a.TimesPlayed > 0 || a.AutoPlayedCount > 0
                             || a.TotalDamage > 0 || a.EffectiveBlock > 0)
                         .OrderBy(a => a.SourceId))
            {
                GD.Print($"[EarringProbe] ROW id={row.SourceId} type={row.SourceType} " +
                    $"times={row.TimesPlayed} auto={row.AutoPlayedCount} " +
                    $"d={row.DirectDamage} m={row.ModifierDamage} a={row.AttributedDamage} " +
                    $"block={row.EffectiveBlock} energy={row.EnergyGained} " +
                    $"origin={row.OriginSourceId ?? "-"}");
            }

            if (root == null)
            {
                Fail("root-missing", "WHISPERING_EARRING row not found");
            }
            else if (root.SourceType != "relic")
            {
                Fail("root-type", $"type={root.SourceType}");
            }
            else if (root.AutoPlayedCount != 2)
            {
                Fail("auto-count", $"auto={root.AutoPlayedCount} expected=2");
            }
            else if (root.TimesPlayed != 0)
            {
                Fail("manual-count", $"times={root.TimesPlayed} expected=0");
            }
            else if (root.DirectDamage <= 0)
            {
                Fail("damage", $"direct={root.DirectDamage} actual={actualDamage}");
            }
            else if (root.DirectDamage != actualDamage)
            {
                Fail("damage-match", $"root={root.DirectDamage} actual={actualDamage}");
            }
            else if (attackRow != null && attackRow.AutoPlayedCount > 0)
            {
                Fail("attack-row", $"auto={attackRow.AutoPlayedCount}");
            }
            else if (defendRow != null && defendRow.AutoPlayedCount > 0)
            {
                Fail("defend-row", $"auto={defendRow.AutoPlayedCount}");
            }
            else
            {
                // EffectiveBlock counts block that actually absorbed damage.
                // End the turn so the enemy attack consumes Defend's block.
                int turnBeforeEnd = me.PlayerCombatState!.TurnNumber;
                PlayerCmd.EndTurn(me, canBackOut: false);
                bool reachedNextTurn = await WaitUntil(
                    () => me.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                        && me.PlayerCombatState.TurnNumber > turnBeforeEnd,
                    30000);

                var afterTurn = FindRoot("WHISPERING_EARRING");
                GD.Print(
                    $"[EarringProbe] AFTER_TURN next={reachedNextTurn} " +
                    $"turn={me.PlayerCombatState?.TurnNumber} phase={me.PlayerCombatState?.Phase} " +
                    $"auto={afterTurn?.AutoPlayedCount ?? 0} block={afterTurn?.EffectiveBlock ?? 0} " +
                    $"energy={afterTurn?.EnergyGained ?? 0} times={afterTurn?.TimesPlayed ?? 0}");

                if (!reachedNextTurn)
                {
                    Fail("next-turn", $"phase={me.PlayerCombatState?.Phase} turn={me.PlayerCombatState?.TurnNumber}");
                }
                else if (afterTurn == null || afterTurn.EffectiveBlock <= 0)
                {
                    Fail("block", $"block={afterTurn?.EffectiveBlock ?? 0}");
                }
                else if (afterTurn.EnergyGained != 1)
                {
                    Fail("energy", $"energy={afterTurn.EnergyGained} expected=1");
                }
                else if (afterTurn.AutoPlayedCount != 2 || afterTurn.TimesPlayed != 0)
                {
                    Fail("after-turn-counts",
                        $"auto={afterTurn.AutoPlayedCount} times={afterTurn.TimesPlayed}");
                }
                else
                {
                    Pass("earring-root",
                        $"auto=2 damage={root.DirectDamage} block={afterTurn.EffectiveBlock} " +
                        $"times=0 energy=1 actual={actualDamage}");
                }
            }
        }
        catch (Exception ex)
        {
            Fail("fatal", ex.ToString());
        }
        finally
        {
            Finish();
        }
    }

    private static int EffectiveEnemyHp(CombatState state) =>
        state.HittableEnemies.Where(e => e.IsAlive)
            .Sum(e => Math.Max(0, e.CurrentHp) + Math.Max(0, e.Block));

    private static ContributionAccum? FindRoot(string sourceId) =>
        CombatTracker.Instance.GetCurrentCombatData().Values
            .FirstOrDefault(a => a.SourceId == sourceId && a.OriginSourceId == null);

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(50);
        return condition();
    }

    private static void Pass(string name, string detail)
    {
        _pass++;
        GD.Print($"[EarringProbe] PASS {name}: {detail}");
    }

    private static void Fail(string name, string detail)
    {
        _fail++;
        GD.Print($"[EarringProbe] FAIL {name}: {detail}");
    }

    private static void Finish()
    {
        GD.Print($"[EarringProbe] RESULT {(_fail == 0 ? "PASS" : "FAIL")} pass={_pass} fail={_fail}");
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Quit(_fail == 0 ? 0 : 1);
        }
        catch
        {
        }
    }

    private static class AutoPlayGate
    {
        public static bool Prefix(ref Task __result)
        {
            if (_blockAutoPlay && !_probeAutoPlay)
            {
                __result = Task.CompletedTask;
                return false;
            }
            return true;
        }
    }
}
