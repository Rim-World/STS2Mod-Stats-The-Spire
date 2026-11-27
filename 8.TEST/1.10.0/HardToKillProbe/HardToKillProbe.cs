using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityStats.Collection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.ValueProps;

namespace StatsTheSpire.HardToKillProbe;

/// <summary>
/// Deterministic combat probe for the v0.114 HardToKill change.
/// Adds a slotless BigDummy to the first combat, applies HardToKill 9,
/// keeps AutoSlay from playing cards, and measures the real Stats tracker
/// after four controlled attacks.
/// </summary>
[ModInitializer("Initialize")]
public static class HardToKillProbe
{
    private static bool _subscribed;
    private static bool _started;
    private static bool _blockAutoPlay;
    private static int _pass;
    private static int _fail;

    [ThreadStatic] private static bool _probeAutoPlay;

    public static void Initialize()
    {
        GD.Print("[HardToKillProbe] initialized");
        try
        {
            var harmony = new Harmony("test.hardtokill_probe_1100");
            var releaseTarget = AccessTools.Method(typeof(NGame), "IsReleaseGame");
            if (releaseTarget != null)
            {
                harmony.Patch(releaseTarget,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(HardToKillProbe), nameof(ForceReleaseFalse))));
                GD.Print("[HardToKillProbe] IsReleaseGame gate patched");
            }
            harmony.PatchAll(typeof(HardToKillProbe).Assembly);
            GD.Print("[HardToKillProbe] Harmony patched");
        }
        catch (Exception ex)
        {
            GD.Print($"[HardToKillProbe] Harmony failed: {ex}");
        }

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.Print("[HardToKillProbe] no SceneTree");
            return;
        }

        tree.ProcessFrame += TrySubscribe;
    }

    public static bool ForceReleaseFalse(bool __result) => false;

    private static void TrySubscribe()
    {
        if (_subscribed) return;
        var cm = CombatManager.Instance;
        if (cm == null) return;
        cm.CombatBegan += OnCombatBegan;
        _subscribed = true;
        GD.Print("[HardToKillProbe] CombatBegan subscribed");
    }

    private static void OnCombatBegan(CombatState state)
    {
        if (_started) return;
        _started = true;
        _blockAutoPlay = true;
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

            var exo = await CreatureCmd.Add<BigDummy>(state);
            await CreatureCmd.SetMaxAndCurrentHp(exo, 200m);
            await PowerCmd.Apply<HardToKillPower>(ctx, exo, 9m, exo, null);
            var hard = exo.GetPower<HardToKillPower>();
            GD.Print($"[HardToKillProbe] dummy added hp={exo.CurrentHp} hard={hard?.Amount} block={exo.Block}");

            if (hard == null || hard.Amount != 9m)
            {
                Fail("hard-to-kill", $"amount={hard?.Amount}");
            }
            else
            {
                Pass("hard-to-kill", "amount=9");
            }

            await RunCase(state, me, exo, ctx, "single-big", "BLUDGEON", 0, 200, 9);
            await RunCase(state, me, exo, ctx, "multi-hit", "TWIN_STRIKE", 0, 200, 10);
            await RunCase(state, me, exo, ctx, "single-big-block5", "BLUDGEON", 5, 200, 14);
            await RunCase(state, me, exo, ctx, "single-big-kill5", "BLUDGEON", 0, 5, 5);

            var untracked = CombatTracker.Instance.GetCurrentCombatData().Values
                .Where(a => a.SourceId == "UNTRACKED")
                .Sum(a => a.TotalDamage);
            GD.Print($"[HardToKillProbe] untrackedDamage={untracked}");
            if (untracked == 0)
                Pass("no-untracked", "0");
            else
                Fail("no-untracked", $"untracked={untracked}");
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

    private static async Task RunCase(
        CombatState state,
        Player me,
        Creature exo,
        PlayerChoiceContext ctx,
        string label,
        string cardId,
        int block,
        int hp,
        int expectedTotal)
    {
        try
        {
            await CreatureCmd.SetMaxAndCurrentHp(exo, 200m);
            await CreatureCmd.LoseBlock(ctx, exo, exo.Block, null);
            if (block > 0)
                await CreatureCmd.GainBlock(exo, block, ValueProp.Unpowered, null);
            await CreatureCmd.SetMaxAndCurrentHp(exo, hp);
            me.PlayerCombatState!.GainEnergy(99m);

            var canonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == cardId);
            if (canonical == null)
            {
                Fail(label, $"card missing: {cardId}");
                return;
            }

            var card = state.CreateCard(canonical, me);
            await CardPileCmd.AddGeneratedCardsToCombat(new List<CardModel> { card }, PileType.Hand, me);

            int hpBefore = exo.CurrentHp;
            int blockBefore = exo.Block;
            int trackerBefore = CombatTracker.Instance.TotalDamageDealt;
            int sourceBefore = SourceTotal(cardId);

            _probeAutoPlay = true;
            try
            {
                await CardCmd.AutoPlay(ctx, card, exo, AutoPlayType.Default, skipCardPileVisuals: true);
            }
            finally
            {
                _probeAutoPlay = false;
            }

            await Task.Delay(100);

            int hpAfter = exo.CurrentHp;
            int blockAfter = exo.Block;
            int actual = (hpBefore - hpAfter) + (blockBefore - blockAfter);
            int trackerDelta = CombatTracker.Instance.TotalDamageDealt - trackerBefore;
            int sourceDelta = SourceTotal(cardId) - sourceBefore;

            GD.Print(
                $"[HardToKillProbe] CASE {label} card={cardId} hp={hpBefore}->{hpAfter} " +
                $"block={blockBefore}->{blockAfter} actual={actual} tracker={trackerDelta} " +
                $"source={sourceDelta} expected={expectedTotal}");

            if (actual == expectedTotal && trackerDelta == expectedTotal && sourceDelta == expectedTotal)
                Pass(label, $"actual={actual} tracker={trackerDelta} source={sourceDelta}");
            else
                Fail(label, $"actual={actual} tracker={trackerDelta} source={sourceDelta} expected={expectedTotal}");
        }
        catch (Exception ex)
        {
            Fail(label, ex.ToString());
        }
    }

    private static int SourceTotal(string sourceId) =>
        CombatTracker.Instance.GetCurrentCombatData().Values
            .Where(a => a.SourceId == sourceId)
            .Sum(a => a.TotalDamage);

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
        GD.Print($"[HardToKillProbe] PASS {name}: {detail}");
    }

    private static void Fail(string name, string detail)
    {
        _fail++;
        GD.Print($"[HardToKillProbe] FAIL {name}: {detail}");
    }

    private static void Finish()
    {
        GD.Print($"[HardToKillProbe] RESULT {(_fail == 0 ? "PASS" : "FAIL")} pass={_pass} fail={_fail}");
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Quit(_fail == 0 ? 0 : 1);
        }
        catch
        {
        }
    }

    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
    private static class AutoPlayGate
    {
        private static bool Prefix(ref Task __result)
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
