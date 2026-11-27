using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace StatsTheSpire.AutoPlayProbe;

/// <summary>
/// Deterministic probe for auto-play root-source rollup.
/// Simulates the source card play, applies the real power, invokes the real
/// power hook, and checks the tracker rows. The game's own AutoSlayer is
/// blocked from playing cards so it cannot pollute the measurements.
/// </summary>
[ModInitializer("Initialize")]
public static class AutoPlayProbe
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
        GD.Print("[AutoPlayProbe] initialized");
        try
        {
            var harmony = new Harmony("test.autoplay_probe_1110");
            var releaseTarget = AccessTools.Method(typeof(NGame), "IsReleaseGame");
            if (releaseTarget != null)
            {
                harmony.Patch(releaseTarget,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(AutoPlayProbe), nameof(ForceReleaseFalse))));
                GD.Print("[AutoPlayProbe] IsReleaseGame gate patched");
            }
            var autoPlayMethod = AccessTools.Method(typeof(CardCmd), nameof(CardCmd.AutoPlay));
            if (autoPlayMethod != null)
            {
                harmony.Patch(autoPlayMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoPlayGate), nameof(AutoPlayGate.Prefix))));
                GD.Print("[AutoPlayProbe] CardCmd.AutoPlay gate patched explicitly");
            }
            else
            {
                GD.Print("[AutoPlayProbe] CardCmd.AutoPlay gate NOT found");
            }
            GD.Print("[AutoPlayProbe] Harmony patched");
        }
        catch (Exception ex)
        {
            GD.Print($"[AutoPlayProbe] Harmony failed: {ex}");
        }

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.Print("[AutoPlayProbe] no SceneTree");
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
        GD.Print("[AutoPlayProbe] CombatBegan subscribed");
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

            await RunCase(state, me, ctx, "mayhem", "MAYHEM_POWER", "MAYHEM",
                "AfterAutoPrePlayPhaseEntered", PileType.Draw, "BLUDGEON");
            await RunCase(state, me, ctx, "stampede", "STAMPEDE_POWER", "STAMPEDE",
                "AfterAutoPostPlayPhaseEntered", PileType.Hand, "BLUDGEON");
            await RunCase(state, me, ctx, "simpleton", "SIMPLETON_POWER", "SIMPLEON",
                "AfterAutoPostPlayPhaseEntered", PileType.Hand, "BLUDGEON");

            RunStackTests();
            CombatTracker.Instance.NotifyCombatDataUpdated();
            CombatTracker.Instance.FlushLiveState();
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
        PlayerChoiceContext ctx,
        string label,
        string powerId,
        string expectedSourceId,
        string hookName,
        PileType cardPile,
        string autoCardId)
    {
        try
        {
            await ResetEnemies(state);

            var powerModel = ModelDb.AllPowers.FirstOrDefault(p => p.Id.Entry == powerId);
            if (powerModel == null)
            {
                // Some Fool content is present as a type but is not exposed
                // through ModelDb.AllPowers on every game build. Construct the
                // real power type directly so the hook patch still runs.
                var powerType = AccessTools.TypeByName(
                    "MegaCrit.Sts2.Core.Models.Powers.SimpletonPower");
                if (powerType != null && powerId == "SIMPLETON_POWER")
                {
                    try
                    {
                        // The type may already be registered even when the
                        // cached AllPowers enumeration was built before it.
                        var modelId = ModelDb.GetId(powerType);
                        powerModel = ModelDb.GetByIdOrNull<PowerModel>(modelId);
                        if (powerModel == null)
                            powerModel = (PowerModel)Activator.CreateInstance(powerType)!;
                    }
                    catch (Exception ex) { GD.Print($"[AutoPlayProbe] power construct failed: {ex}"); }
                }
            }
            if (powerModel == null)
            {
                GD.Print($"[AutoPlayProbe] SKIP {label}: power {powerId} not present in this game version");
                return;
            }

            var sourceCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == expectedSourceId);
            var autoCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == autoCardId);
            if (autoCanonical == null)
            {
                Fail(label, $"auto card missing: {autoCardId}");
                return;
            }

            var sourceCard = sourceCanonical != null ? state.CreateCard(sourceCanonical, me) : null;
            var autoCard = state.CreateCard(autoCanonical, me);
            await CardPileCmd.AddGeneratedCardsToCombat(
                new List<CardModel> { autoCard }, cardPile, me, CardPilePosition.Top);

            // Simulate the source card's manual play so the power application
            // and later auto hook have the same source context as production.
            int sourceHash = sourceCard?.GetHashCode() ?? expectedSourceId.GetHashCode();
            CombatTracker.Instance.OnCardPlayStarted(expectedSourceId, sourceHash, false);
            try
            {
                var applyModel = ModelDb.Contains(powerModel.GetType()) ? powerModel.ToMutable() : powerModel;
                await PowerCmd.Apply(ctx, applyModel, me.Creature, 1m,
                    me.Creature, sourceCard, silent: true);
            }
            finally
            {
                CombatTracker.Instance.OnCardPlayFinished();
            }

            var power = me.Creature.GetPower(powerModel.Id);
            if (power == null)
            {
                Fail(label, "power instance missing after apply");
                return;
            }

            GD.Print($"[AutoPlayProbe] DEBUG {label} modelId={powerModel.Id.Entry} " +
                $"type={powerModel.GetType().Name} appliedId={power.Id.Entry} " +
                $"sourceByModel={ContributionMap.Instance.GetPowerSource(powerModel.Id.Entry)?.SourceId ?? "null"} " +
                $"sourceByApplied={ContributionMap.Instance.GetPowerSource(power.Id.Entry)?.SourceId ?? "null"} " +
                $"sourceByExpected={ContributionMap.Instance.GetPowerSource(powerId)?.SourceId ?? "null"}");

            var recordedSource = ContributionMap.Instance.GetPowerSource(powerId);
            if (recordedSource == null || recordedSource.SourceId != expectedSourceId)
            {
                Fail(label, $"power source={recordedSource?.SourceId ?? "null"} expected={expectedSourceId}");
                return;
            }

            var rootBefore = FindRoot(expectedSourceId);
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            int beforeDamage = rootBefore?.TotalDamage ?? 0;

            int effectiveHpBefore = EffectiveEnemyHp(state);
            _probeAutoPlay = true;
            try
            {
                var task = InvokeHook(power, hookName, ctx, me);
                if (task != null) await task;
            }
            finally
            {
                _probeAutoPlay = false;
            }

            await Task.Delay(150);

            int effectiveHpAfter = EffectiveEnemyHp(state);
            int actualDamage = Math.Max(0, effectiveHpBefore - effectiveHpAfter);
            var root = FindRoot(expectedSourceId);
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            var autoCardRow = FindRoot(autoCardId);
            var powerRow = FindRoot(powerId);

            GD.Print(
                $"[AutoPlayProbe] CASE {label} source={expectedSourceId} root={(root != null)} " +
                $"times={root?.TimesPlayed ?? 0} auto={root?.AutoPlayedCount ?? 0} autoDelta={autoDelta} " +
                $"damage={root?.TotalDamage ?? 0} damageDelta={damageDelta} actualDamage={actualDamage} " +
                $"autoCardRow={(autoCardRow != null)} powerRow={(powerRow != null)} " +
                $"rootD={root?.DirectDamage ?? 0} rootM={root?.ModifierDamage ?? 0} rootA={root?.AttributedDamage ?? 0}");

            foreach (var row in CombatTracker.Instance.GetCurrentCombatData().Values
                         .Where(a => a.TimesPlayed > 0 || a.AutoPlayedCount > 0
                             || a.TotalDamage > 0 || a.OriginSourceId != null)
                         .OrderBy(a => a.SourceId))
            {
                GD.Print($"[AutoPlayProbe] ROW {label} id={row.SourceId} type={row.SourceType} " +
                    $"times={row.TimesPlayed} auto={row.AutoPlayedCount} d={row.DirectDamage} " +
                    $"m={row.ModifierDamage} a={row.AttributedDamage} u={row.UpgradeDamage} " +
                    $"origin={row.OriginSourceId ?? "-"}");
            }

            if (root == null)
            {
                Fail(label, "root row missing");
            }
            else if (autoDelta <= 0)
            {
                Fail(label, $"autoDelta={autoDelta}");
            }
            else if (root.TimesPlayed <= 0)
            {
                Fail(label, $"TimesPlayed={root.TimesPlayed}");
            }
            else if (powerRow != null)
            {
                Fail(label, $"power row should be mapped away: {powerId}");
            }
            else
            {
                Pass(label, $"autoDelta={autoDelta} damageDelta={damageDelta} actual={actualDamage}");
            }

            // Stale rows from this case must not affect the next one.
            power.RemoveInternal();
        }
        catch (Exception ex)
        {
            Fail(label, ex.ToString());
        }
    }

    private static void RunStackTests()
    {
        CombatTracker.Instance.ForceResetAllContext();

        // Nested auto: two auto frames inherit the first root.
        CombatTracker.Instance.OnCardPlayStarted("TEST_ROOT", 0x5101, true);
        CombatTracker.Instance.OnCardPlayStarted("TEST_AUTO1", 0x5102, true);
        ContributionMap.Instance.LastDamageModifiers.Add(
            new ContributionMap.ModifierContribution("STRENGTH_POWER", "power", 4));
        CombatTracker.Instance.OnDamageDealt(12, 0, "TEST_AUTO1", false, 7001, 7002);
        CombatTracker.Instance.OnCardPlayFinished();
        CombatTracker.Instance.OnCardPlayFinished();

        // Manual card nested inside an auto chain: it must not inherit the root.
        CombatTracker.Instance.OnCardPlayStarted("TEST_ROOT2", 0x5103, true);
        CombatTracker.Instance.OnCardPlayStarted("TEST_MANUAL", 0x5104, false);
        CombatTracker.Instance.OnDamageDealt(5, 0, "TEST_MANUAL", false, 7003, 7004);
        CombatTracker.Instance.OnCardPlayFinished();
        CombatTracker.Instance.OnCardPlayFinished();

        var root = FindRoot("TEST_ROOT");
        var autoChild = FindRoot("TEST_AUTO1");
        var root2 = FindRoot("TEST_ROOT2");
        var manual = FindRoot("TEST_MANUAL");

        if (root == null || root.AutoPlayedCount != 2 || root.TimesPlayed != 0
            || root.DirectDamage != 8 || root.ModifierDamage != 4 || autoChild != null)
        {
            Fail("stack-nested", $"rootAuto={root?.AutoPlayedCount} rootTimes={root?.TimesPlayed} " +
                $"rootDirect={root?.DirectDamage} rootMod={root?.ModifierDamage} autoChild={autoChild != null}");
        }
        else
        {
            Pass("stack-nested", "rootAuto=2 direct=8 modifier=4 childRow=False");
        }

        if (root2 == null || root2.AutoPlayedCount != 1 || root2.DirectDamage != 0
            || manual == null || manual.TimesPlayed != 1 || manual.DirectDamage != 5)
        {
            Fail("stack-manual", $"rootAuto={root2?.AutoPlayedCount} rootDirect={root2?.DirectDamage} " +
                $"manualTimes={manual?.TimesPlayed} manualDirect={manual?.DirectDamage}");
        }
        else
        {
            Pass("stack-manual", "rootAuto=1 rootDirect=0 manualTimes=1 manualDirect=5");
        }
    }

    private static async Task ResetEnemies(CombatState state)
    {
        foreach (var enemy in state.HittableEnemies)
        {
            await CreatureCmd.SetMaxAndCurrentHp(enemy, 300m);
        }
    }

    private static int EffectiveEnemyHp(CombatState state) =>
        state.HittableEnemies.Where(e => e.IsAlive)
            .Sum(e => Math.Max(0, e.CurrentHp) + Math.Max(0, e.Block));

    private static ContributionAccum? FindRoot(string sourceId) =>
        CombatTracker.Instance.GetCurrentCombatData().Values
            .FirstOrDefault(a => a.SourceId == sourceId && a.OriginSourceId == null);

    private static Task? InvokeHook(PowerModel power, string methodName,
        PlayerChoiceContext ctx, Player me)
    {
        var method = power.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(PlayerChoiceContext), typeof(Player) },
            null);
        if (method == null)
        {
            throw new MissingMethodException(power.GetType().FullName, methodName);
        }
        return method.Invoke(power, new object[] { ctx, me }) as Task;
    }

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
        GD.Print($"[AutoPlayProbe] PASS {name}: {detail}");
    }

    private static void Fail(string name, string detail)
    {
        _fail++;
        GD.Print($"[AutoPlayProbe] FAIL {name}: {detail}");
    }

    private static void Finish()
    {
        GD.Print($"[AutoPlayProbe] RESULT {(_fail == 0 ? "PASS" : "FAIL")} pass={_pass} fail={_fail}");
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
