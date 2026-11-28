using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityStats.Collection;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay.Handlers.Rooms;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace StatsTheSpire.AutoSourceProbe;

/// <summary>
/// Directed probe for the 1.13.1 auto-play source contexts.
/// Uses real game hooks where available; all other auto-play is gated.
/// </summary>
[ModInitializer("Initialize")]
public static class AutoSourceProbe
{
    private static bool _subscribed;
    private static bool _started;
    private static bool _finished;
    private static bool _blockAutoPlay = true;
    private static int _pass;
    private static int _fail;
    private static int _skip;

    private static readonly AsyncLocal<bool> _probeAutoPlayAL = new();
    private static bool _probeAutoPlay
    {
        get => _probeAutoPlayAL.Value;
        set => _probeAutoPlayAL.Value = value;
    }

    public static void Initialize()
    {
        GD.Print("[AutoSourceProbe] initialized");
        try
        {
            var harmony = new Harmony("test.autosource_probe_1131");
            var releaseTarget = AccessTools.Method(typeof(NGame), "IsReleaseGame");
            if (releaseTarget != null)
            {
                harmony.Patch(releaseTarget,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(AutoSourceProbe), nameof(ForceReleaseFalse))));
                GD.Print("[AutoSourceProbe] IsReleaseGame gate patched");
            }

            var autoPlayMethod = AccessTools.Method(typeof(CardCmd), nameof(CardCmd.AutoPlay));
            if (autoPlayMethod != null)
            {
                harmony.Patch(autoPlayMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoPlayGate), nameof(AutoPlayGate.Prefix))));
                GD.Print("[AutoSourceProbe] CardCmd.AutoPlay gate patched");
            }

            var historyHook = AccessTools.Method(typeof(HistoryCourse), "AfterAutoPrePlayPhaseEntered");
            if (historyHook != null)
            {
                harmony.Patch(historyHook,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoSourceProbe), nameof(LogHistoryHook))));
            }

            var autoSlayCombat = AccessTools.Method(typeof(CombatRoomHandler), "PlayCombatAsync",
                new[] { typeof(MegaCrit.Sts2.Core.Random.Rng), typeof(System.Threading.CancellationToken) });
            if (autoSlayCombat != null)
            {
                harmony.Patch(autoSlayCombat,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoSourceProbe), nameof(SkipAutoSlayCombat))));
                GD.Print("[AutoSourceProbe] AutoSlay combat loop blocked");
            }
            else
            {
                var v111Handler = AccessTools.Method(typeof(CombatRoomHandler), "HandleAsync",
                    new[] { typeof(MegaCrit.Sts2.Core.Random.Rng), typeof(System.Threading.CancellationToken) });
                if (v111Handler != null)
                {
                    harmony.Patch(v111Handler,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoSourceProbe), nameof(BlockAutoSlayHandler))));
                    GD.Print("[AutoSourceProbe] AutoSlay combat handler blocked (v111)");
                }
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[AutoSourceProbe] Harmony failed: {ex}");
        }

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.Print("[AutoSourceProbe] no SceneTree");
            return;
        }
        tree.ProcessFrame += TrySubscribe;
    }

    public static bool ForceReleaseFalse(bool __result) => false;

    public static void LogHistoryHook(HistoryCourse __instance, Player player)
    {
        try
        {
            var me = LocalContext.GetMe(player.Creature.CombatState.Players);
            GD.Print($"[AutoSourceProbe] HISTORY_HOOK owner={__instance.Owner?.NetId} " +
                $"player={player.NetId} turn={player.PlayerCombatState?.TurnNumber} " +
                $"phase={player.PlayerCombatState?.Phase} " +
                $"inRelics={me?.Relics.Any(r => r.Id.Entry == "HISTORY_COURSE")}");
        }
        catch (Exception ex)
        {
            GD.Print($"[AutoSourceProbe] HISTORY_HOOK log failed {ex.Message}");
        }
    }

    public static bool SkipAutoSlayCombat(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }

    public static bool BlockAutoSlayHandler(ref Task __result, CancellationToken ct)
    {
        __result = WaitForProbe(ct);
        return false;
    }

    private static async Task WaitForProbe(CancellationToken ct)
    {
        try
        {
            while (!_finished && !ct.IsCancellationRequested)
                await Task.Delay(50, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void TrySubscribe()
    {
        if (_subscribed) return;
        var cm = CombatManager.Instance;
        if (cm == null) return;
        cm.CombatBegan += OnCombatBegan;
        _subscribed = true;
        GD.Print("[AutoSourceProbe] CombatBegan subscribed");
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

            VerifyLostLuteBinding();

            var hand = PileType.Hand.GetPile(me);
            if (hand.Cards.Count > 0)
                await CardPileCmd.RemoveFromCombat(hand.Cards.ToList(), skipVisuals: true);

            await RunHellraiser(state, me, ctx);
            await RunLostLute(state, me, ctx);
            await RunLostLuteRealSource(state, me, ctx);
            await RunFool(state, me, ctx);
            await RunCascade(state, me, ctx);
            await RunHowl(state, me, ctx);
            await RunPotion(state, me, ctx);
            await RunImbued(state, me, ctx);
            await RunHistoryCourse(state, me, ctx);

            LogRows("final");
            CombatTracker.Instance.NotifyCombatDataUpdated();
            await VerifyPersistence();
            CombatTracker.Instance.FlushLiveState();
        }
        catch (Exception ex)
        {
            Fail("fatal", ex.ToString());
        }
        finally
        {
            _finished = true;
            GD.Print($"[AutoSourceProbe] RESULT {(_fail == 0 ? "PASS" : "FAIL")} " +
                $"pass={_pass} fail={_fail} skip={_skip}");
            Finish();
        }
    }

    private static async Task RunHellraiser(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var power = await ApplyPower(state, me, ctx, "HELLRAISER_POWER", "HELLRAISER");
            if (power == null)
            {
                Skip("hellraiser", "power or source card not present");
                return;
            }

            var strikeCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "STRIKE_IRONCLAD");
            if (strikeCanonical == null)
            {
                Fail("hellraiser", "STRIKE_IRONCLAD not found");
                power.RemoveInternal();
                return;
            }

            var strike = state.CreateCard(strikeCanonical, me);
            await AddToPile(strike, PileType.Draw, me);

            var rootBefore = FindRoot("HELLRAISER");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            int beforeDamage = rootBefore?.TotalDamage ?? 0;
            _probeAutoPlay = true;
            try
            {
                await CardPileCmd.Draw(ctx, me);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("HELLRAISER");
            var strikeRow = FindRoot("STRIKE_IRONCLAD");
            var powerRow = FindRoot("HELLRAISER_POWER");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            int strikeTimes = strikeRow?.TimesPlayed ?? 0;
            GD.Print($"[AutoSourceProbe] CASE hellraiser root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"auto={root?.AutoPlayedCount ?? 0} autoDelta={autoDelta} damageDelta={damageDelta} " +
                $"times={root?.TimesPlayed ?? 0} strikeTimes={strikeTimes} strikeAuto={strikeRow?.AutoPlayedCount ?? 0} " +
                $"powerRow={(powerRow != null)} source={ContributionMap.Instance.GetPowerSource("HELLRAISER_POWER")?.SourceId ?? "null"}");

            if (root == null || root.SourceType != "card")
                Fail("hellraiser", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 1)
                Fail("hellraiser", $"autoDelta={autoDelta} expected=1");
            else if (damageDelta != 6)
                Fail("hellraiser", $"damageDelta={damageDelta} expected=6");
            else if (root.TimesPlayed != 0)
                Fail("hellraiser", $"rootTimes={root.TimesPlayed}");
            else if (strikeTimes != 0 || (strikeRow?.AutoPlayedCount ?? 0) != 0)
                Fail("hellraiser", $"strikeTimes={strikeTimes} strikeAuto={strikeRow?.AutoPlayedCount ?? 0}");
            else if (powerRow != null)
                Fail("hellraiser", "HELLRAISER_POWER row exists");
            else
                Pass("hellraiser", $"autoDelta=1 damageDelta=6 times=0");

            power.RemoveInternal();
        }
        catch (Exception ex)
        {
            Fail("hellraiser", ex.ToString());
        }
    }

    private static async Task RunLostLute(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var power = await ApplyPower(state, me, ctx, "LOST_LUTE_POWER", "LOST_LUTE");
            if (power == null)
            {
                Skip("lost-lute", "power or source card not present");
                return;
            }

            var choCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "CHO_CHORUS");
            var bludgeonCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "BLUDGEON");
            if (choCanonical == null || bludgeonCanonical == null)
            {
                Fail("lost-lute", $"CHO_CHORUS={choCanonical != null} BLUDGEON={bludgeonCanonical != null}");
                power.RemoveInternal();
                return;
            }

            var cho = state.CreateCard(choCanonical, me);
            var bludgeon = state.CreateCard(bludgeonCanonical, me);
            await AddToPile(cho, PileType.Hand, me);
            await AddToPile(bludgeon, PileType.Draw, me);

            var rootBefore = FindRoot("LOST_LUTE");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            int beforeDamage = rootBefore?.TotalDamage ?? 0;
            _probeAutoPlay = true;
            try
            {
                await CardCmd.AutoPlay(ctx, cho, null);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("LOST_LUTE");
            var bludgeonRow = FindRoot("BLUDGEON");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            GD.Print($"[AutoSourceProbe] CASE lost-lute root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"auto={root?.AutoPlayedCount ?? 0} autoDelta={autoDelta} damageDelta={damageDelta} " +
                $"times={root?.TimesPlayed ?? 0} bludgeonAuto={bludgeonRow?.AutoPlayedCount ?? 0} " +
                $"source={ContributionMap.Instance.GetPowerSource("LOST_LUTE_POWER")?.SourceId ?? "null"}");

            if (root == null || root.SourceType != "card")
                Fail("lost-lute", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 1)
                Fail("lost-lute", $"autoDelta={autoDelta} expected=1");
            else if (damageDelta != 32)
                Fail("lost-lute", $"damageDelta={damageDelta} expected=32");
            else if (root.TimesPlayed != 0)
                Fail("lost-lute", $"rootTimes={root.TimesPlayed}");
            else if ((bludgeonRow?.AutoPlayedCount ?? 0) != 0 || (bludgeonRow?.TimesPlayed ?? 0) != 0)
                Fail("lost-lute", $"bludgeonAuto={bludgeonRow?.AutoPlayedCount ?? 0} times={bludgeonRow?.TimesPlayed ?? 0}");
            else
                Pass("lost-lute", $"autoDelta=1 damageDelta=32 times=0");

            power.RemoveInternal();
        }
        catch (Exception ex)
        {
            Fail("lost-lute", ex.ToString());
        }
    }

    private static async Task RunLostLuteRealSource(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        const string testRelic = "TEST_RELIC_SOURCE";
        try
        {
            await ResetEnemies(state);
            var power = await ApplyPower(state, me, ctx, "LOST_LUTE_POWER", "LOST_LUTE", testRelic);
            if (power == null)
            {
                Skip("lost-lute-real-source", "power or source card not present");
                return;
            }

            var wahCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "WAH_BLAST");
            var bludgeonCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "BLUDGEON");
            if (wahCanonical == null || bludgeonCanonical == null)
            {
                Fail("lost-lute-real-source",
                    $"WAH_BLAST={wahCanonical != null} BLUDGEON={bludgeonCanonical != null}");
                power.RemoveInternal();
                return;
            }

            var wah = state.CreateCard(wahCanonical, me);
            var bludgeon = state.CreateCard(bludgeonCanonical, me);
            await AddToPile(wah, PileType.Hand, me);
            await AddToPile(bludgeon, PileType.Draw, me);

            var rootBefore = FindRoot(testRelic);
            var lostBefore = FindRoot("LOST_LUTE");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            int beforeDamage = rootBefore?.TotalDamage ?? 0;
            int lostAutoBefore = lostBefore?.AutoPlayedCount ?? 0;

            _probeAutoPlay = true;
            try
            {
                await CardCmd.AutoPlay(ctx, wah, null);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot(testRelic);
            var lost = FindRoot("LOST_LUTE");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            int lostDelta = (lost?.AutoPlayedCount ?? 0) - lostAutoBefore;
            GD.Print($"[AutoSourceProbe] CASE lost-lute-real-source root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} damageDelta={damageDelta} lostDelta={lostDelta} " +
                $"source={ContributionMap.Instance.GetPowerSource("LOST_LUTE_POWER")?.SourceId ?? "null"}");

            if (root == null || root.SourceType != "relic")
                Fail("lost-lute-real-source", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 1)
                Fail("lost-lute-real-source", $"autoDelta={autoDelta} expected=1");
            else if (damageDelta != 32)
                Fail("lost-lute-real-source", $"damageDelta={damageDelta} expected=32");
            else if (root.TimesPlayed != 0)
                Fail("lost-lute-real-source", $"rootTimes={root.TimesPlayed}");
            else if (lostDelta != 0)
                Fail("lost-lute-real-source", $"lostDelta={lostDelta} expected=0");
            else
                Pass("lost-lute-real-source", $"root=TEST_RELIC_SOURCE autoDelta=1 damageDelta=32");

            power.RemoveInternal();
        }
        catch (Exception ex)
        {
            Fail("lost-lute-real-source", ex.ToString());
        }
    }

    private static async Task RunFool(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var foolCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "THE_FOOL");
            var defendCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "DEFEND_SILENT");
            if (foolCanonical == null || defendCanonical == null)
            {
                Skip("fool-onplay", $"THE_FOOL={foolCanonical != null} DEFEND_SILENT={defendCanonical != null}");
                return;
            }

            var fool = state.CreateCard(foolCanonical, me);
            var defend1 = state.CreateCard(defendCanonical, me);
            var defend2 = state.CreateCard(defendCanonical, me);
            await AddToPile(fool, PileType.Hand, me);
            await AddToPile(defend1, PileType.Hand, me);
            await AddToPile(defend2, PileType.Hand, me);

            var foolBefore = FindRoot("THE_FOOL");
            var defendBefore = FindRoot("DEFEND_SILENT");
            int beforeAuto = foolBefore?.AutoPlayedCount ?? 0;
            int beforeTimes = foolBefore?.TimesPlayed ?? 0;
            int beforeDefendAuto = defendBefore?.AutoPlayedCount ?? 0;
            int beforeDefendTimes = defendBefore?.TimesPlayed ?? 0;
            _probeAutoPlay = true;
            try
            {
                await PlayManual(ctx, fool, null);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("THE_FOOL");
            var defendRow = FindRoot("DEFEND_SILENT");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int timesDelta = (root?.TimesPlayed ?? 0) - beforeTimes;
            int defendDelta = (defendRow?.AutoPlayedCount ?? 0) - beforeDefendAuto;
            int defendTimesDelta = (defendRow?.TimesPlayed ?? 0) - beforeDefendTimes;
            GD.Print($"[AutoSourceProbe] CASE fool-onplay root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} timesDelta={timesDelta} defendAutoDelta={defendDelta} " +
                $"defendTimesDelta={defendTimesDelta}");

            if (root == null || root.SourceType != "card")
                Fail("fool-onplay", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 2)
                Fail("fool-onplay", $"autoDelta={autoDelta} expected=2");
            else if (timesDelta != 1)
                Fail("fool-onplay", $"timesDelta={timesDelta} expected=1");
            else if (defendDelta != 0 || defendTimesDelta != 0)
                Fail("fool-onplay", $"defendAutoDelta={defendDelta} defendTimesDelta={defendTimesDelta}");
            else
                Pass("fool-onplay", $"autoDelta=2 timesDelta=1 defendDelta=0");
        }
        catch (Exception ex)
        {
            Fail("fool-onplay", ex.ToString());
        }
    }

    private static async Task RunCascade(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var cascadeCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "CASCADE");
            var defendCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "DEFEND_SILENT");
            if (cascadeCanonical == null || defendCanonical == null)
            {
                Skip("cascade-onplay",
                    $"CASCADE={cascadeCanonical != null} DEFEND_SILENT={defendCanonical != null}");
                return;
            }

            var cascade = state.CreateCard(cascadeCanonical, me);
            var defend1 = state.CreateCard(defendCanonical, me);
            var defend2 = state.CreateCard(defendCanonical, me);
            await AddToPile(cascade, PileType.Hand, me);
            await AddToPile(defend1, PileType.Draw, me);
            await AddToPile(defend2, PileType.Draw, me);
            cascade.EnergyCost.CapturedXValue = 2;

            var rootBefore = FindRoot("CASCADE");
            var defendBefore = FindRoot("DEFEND_SILENT");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            int beforeTimes = rootBefore?.TimesPlayed ?? 0;
            int beforeDefendAuto = defendBefore?.AutoPlayedCount ?? 0;
            int beforeDefendTimes = defendBefore?.TimesPlayed ?? 0;

            _probeAutoPlay = true;
            try
            {
                await PlayManual(ctx, cascade, null);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("CASCADE");
            var defendRow = FindRoot("DEFEND_SILENT");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int timesDelta = (root?.TimesPlayed ?? 0) - beforeTimes;
            int defendDelta = (defendRow?.AutoPlayedCount ?? 0) - beforeDefendAuto;
            int defendTimesDelta = (defendRow?.TimesPlayed ?? 0) - beforeDefendTimes;
            GD.Print($"[AutoSourceProbe] CASE cascade-onplay root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} timesDelta={timesDelta} defendAutoDelta={defendDelta} " +
                $"defendTimesDelta={defendTimesDelta}");

            if (root == null || root.SourceType != "card")
                Fail("cascade-onplay", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 2)
                Fail("cascade-onplay", $"autoDelta={autoDelta} expected=2");
            else if (timesDelta != 1)
                Fail("cascade-onplay", $"timesDelta={timesDelta} expected=1");
            else if (defendDelta != 0 || defendTimesDelta != 0)
                Fail("cascade-onplay", $"defendAutoDelta={defendDelta} defendTimesDelta={defendTimesDelta}");
            else
                Pass("cascade-onplay", $"autoDelta=2 timesDelta=1 defendDelta=0");
        }
        catch (Exception ex)
        {
            Fail("cascade-onplay", ex.ToString());
        }
    }

    private static async Task RunHowl(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var howlCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "HOWL_FROM_BEYOND");
            if (howlCanonical == null)
            {
                Skip("howl", "HOWL_FROM_BEYOND not present");
                return;
            }

            var howl = state.CreateCard(howlCanonical, me);
            await AddToPile(howl, PileType.Exhaust, me);
            var method = typeof(HowlFromBeyond).GetMethod(
                "AfterAutoPostPlayPhaseEntered",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(PlayerChoiceContext), typeof(Player) },
                null);
            if (method == null)
            {
                Fail("howl", "hook method missing");
                return;
            }

            var before = FindRoot("HOWL_FROM_BEYOND");
            int beforeAuto = before?.AutoPlayedCount ?? 0;
            int beforeDamage = before?.TotalDamage ?? 0;
            int enemyCount = state.HittableEnemies.Count(e => e.IsAlive);
            int expectedDamage = enemyCount * 18;
            _probeAutoPlay = true;
            try
            {
                await (Task)method.Invoke(howl, new object[] { ctx, me })!;
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("HOWL_FROM_BEYOND");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            GD.Print($"[AutoSourceProbe] CASE howl root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} damageDelta={damageDelta} expected={expectedDamage} " +
                $"enemies={enemyCount} times={root?.TimesPlayed ?? 0}");

            if (root == null || root.SourceType != "card")
                Fail("howl", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 1)
                Fail("howl", $"autoDelta={autoDelta} expected=1");
            else if (damageDelta != expectedDamage)
                Fail("howl", $"damageDelta={damageDelta} expected={expectedDamage}");
            else if (root.TimesPlayed != 0)
                Fail("howl", $"rootTimes={root.TimesPlayed}");
            else
                Pass("howl", $"autoDelta=1 damageDelta={damageDelta} times=0");
        }
        catch (Exception ex)
        {
            Fail("howl", ex.ToString());
        }
    }

    private static async Task RunPotion(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var potionCanonical = ModelDb.AllPotions.FirstOrDefault(p => p.Id.Entry == "DISTILLED_CHAOS");
            if (potionCanonical == null)
            {
                Skip("potion", "DISTILLED_CHAOS not present");
                return;
            }
            if (!me.HasOpenPotionSlots)
            {
                Skip("potion", "no open potion slot");
                return;
            }

            var strikeCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "STRIKE_SILENT");
            if (strikeCanonical == null)
            {
                Fail("potion", "STRIKE_SILENT not found");
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                var strike = state.CreateCard(strikeCanonical, me);
                await AddToPile(strike, PileType.Draw, me);
            }

            var potion = potionCanonical.ToMutable();
            var procured = await PotionCmd.TryToProcure(potion, me);
            if (!procured.success)
            {
                Fail("potion", "TryToProcure failed");
                return;
            }

            var before = FindRoot("DISTILLED_CHAOS");
            int beforeAuto = before?.AutoPlayedCount ?? 0;
            int beforeDamage = before?.TotalDamage ?? 0;
            _probeAutoPlay = true;
            try
            {
                await potion.OnUseWrapper(ctx, me.Creature);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("DISTILLED_CHAOS");
            var strikeRow = FindRoot("STRIKE_SILENT");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            GD.Print($"[AutoSourceProbe] CASE potion root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} auto={root?.AutoPlayedCount ?? 0} damageDelta={damageDelta} " +
                $"times={root?.TimesPlayed ?? 0} strikeAuto={strikeRow?.AutoPlayedCount ?? 0} " +
                $"strikeTimes={strikeRow?.TimesPlayed ?? 0}");

            if (root == null || root.SourceType != "potion")
                Fail("potion", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 3)
                Fail("potion", $"autoDelta={autoDelta} expected=3");
            else if (damageDelta != 18)
                Fail("potion", $"damageDelta={damageDelta} expected=18");
            else if (root.TimesPlayed != 0)
                Fail("potion", $"rootTimes={root.TimesPlayed}");
            else if ((strikeRow?.AutoPlayedCount ?? 0) != 0 || (strikeRow?.TimesPlayed ?? 0) != 0)
                Fail("potion", $"strikeAuto={strikeRow?.AutoPlayedCount ?? 0} strikeTimes={strikeRow?.TimesPlayed ?? 0}");
            else
                Pass("potion", $"autoDelta=3 damageDelta=18 times=0");
        }
        catch (Exception ex)
        {
            Fail("potion", ex.ToString());
        }
    }

    private static async Task RunImbued(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var strikeCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "STRIKE_SILENT");
            if (strikeCanonical == null)
            {
                Skip("imbued", "STRIKE_SILENT not present");
                return;
            }

            var strike = state.CreateCard(strikeCanonical, me);
            await AddToPile(strike, PileType.Hand, me);
            var enchantment = ModelDb.Enchantment<Imbued>().ToMutable();
            strike.EnchantInternal(enchantment, 1m);
            var rootBefore = FindRoot("STRIKE_SILENT");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            int beforeDamage = rootBefore?.TotalDamage ?? 0;
            _probeAutoPlay = true;
            try
            {
                await strike.Enchantment!.AfterAutoPrePlayPhaseEntered(ctx, me);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("STRIKE_SILENT");
            var imbuedRow = FindRoot("IMBUED");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int damageDelta = (root?.TotalDamage ?? 0) - beforeDamage;
            GD.Print($"[AutoSourceProbe] CASE imbued root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} damageDelta={damageDelta} times={root?.TimesPlayed ?? 0} " +
                $"imbuedRow={(imbuedRow != null)}");

            if (root == null || root.SourceType != "card")
                Fail("imbued", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 1)
                Fail("imbued", $"autoDelta={autoDelta} expected=1");
            else if (damageDelta != 6)
                Fail("imbued", $"damageDelta={damageDelta} expected=6");
            else if (root.TimesPlayed != 0)
                Fail("imbued", $"rootTimes={root.TimesPlayed}");
            else if (imbuedRow != null)
                Fail("imbued", "IMBUED row exists");
            else
                Pass("imbued", $"autoDelta=1 damageDelta=6 card-root");
        }
        catch (Exception ex)
        {
            Fail("imbued", ex.ToString());
        }
    }

    private static async Task RunHistoryCourse(CombatState state, Player me, PlayerChoiceContext ctx)
    {
        try
        {
            await ResetEnemies(state);
            var bludgeonCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "BLUDGEON");
            if (bludgeonCanonical == null)
            {
                Skip("history-course", "BLUDGEON not present");
                return;
            }

            var bludgeon = state.CreateCard(bludgeonCanonical, me);
            await AddToPile(bludgeon, PileType.Hand, me);
            var target = state.HittableEnemies.FirstOrDefault(e => e.IsAlive);
            _probeAutoPlay = true;
            try
            {
                await CardCmd.AutoPlay(ctx, bludgeon, target);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var relic = await RelicCmd.Obtain<HistoryCourse>(me);
            GD.Print($"[AutoSourceProbe] relic added type={relic.GetType().Name} id={relic.Id.Entry} " +
                $"inRelics={me.Relics.Any(r => r.Id.Entry == "HISTORY_COURSE")}");
            var entries = CombatManager.Instance.History.CardPlaysFinished.ToList();
            GD.Print($"[AutoSourceProbe] HISTORY_AFTER_SEED entries={entries.Count} " +
                string.Join(" | ", entries.TakeLast(5).Select(e =>
                    $"{e.CardPlay.Card.Id.Entry}:{e.CardPlay.Card.Type}:last={e.HappenedLastPlayerTurn(me)}:dupe={e.CardPlay.Card.IsDupe}")));
            var beforeHistory = FindRoot("HISTORY_COURSE");
            var beforeBludgeon = FindRoot("BLUDGEON");
            int beforeHistoryAuto = beforeHistory?.AutoPlayedCount ?? 0;
            int beforeBludgeonAuto = beforeBludgeon?.AutoPlayedCount ?? 0;
            int turnBefore = me.PlayerCombatState!.TurnNumber;

            _probeAutoPlay = true;
            bool previousBlock = _blockAutoPlay;
            _blockAutoPlay = false;
            try
            {
                PlayerCmd.EndTurn(me, canBackOut: false);
                bool nextTurn = await WaitUntil(
                    () => me.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                        && me.PlayerCombatState.TurnNumber > turnBefore,
                    30000);
                if (!nextTurn)
                {
                    Fail("history-course", $"next turn not reached: turn={me.PlayerCombatState?.TurnNumber} " +
                        $"phase={me.PlayerCombatState?.Phase}");
                    return;
                }
            }
            finally
            {
                _probeAutoPlay = false;
                _blockAutoPlay = previousBlock;
            }

            await Task.Delay(200);
            var afterEntries = CombatManager.Instance.History.CardPlaysFinished.ToList();
            GD.Print($"[AutoSourceProbe] HISTORY_AFTER_TURN entries={afterEntries.Count} " +
                string.Join(" | ", afterEntries.TakeLast(5).Select(e =>
                    $"{e.CardPlay.Card.Id.Entry}:{e.CardPlay.Card.Type}:last={e.HappenedLastPlayerTurn(me)}:dupe={e.CardPlay.Card.IsDupe}")));
            var root = FindRoot("HISTORY_COURSE");
            var bludgeonRow = FindRoot("BLUDGEON");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeHistoryAuto;
            int bludgeonDelta = (bludgeonRow?.AutoPlayedCount ?? 0) - beforeBludgeonAuto;
            GD.Print($"[AutoSourceProbe] CASE history-course root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"auto={root?.AutoPlayedCount ?? 0} autoDelta={autoDelta} " +
                $"direct={root?.DirectDamage ?? 0} times={root?.TimesPlayed ?? 0} " +
                $"bludgeonAutoDelta={bludgeonDelta} turn={me.PlayerCombatState?.TurnNumber}");

            if (root == null || root.SourceType != "relic")
                Fail("history-course", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta != 1)
                Fail("history-course", $"autoDelta={autoDelta} expected=1");
            else if ((root?.DirectDamage ?? 0) != 32)
                Fail("history-course", $"direct={root?.DirectDamage ?? 0} expected=32");
            else if (root.TimesPlayed != 0)
                Fail("history-course", $"rootTimes={root.TimesPlayed}");
            else if (bludgeonDelta != 0)
                Fail("history-course", $"bludgeonAutoDelta={bludgeonDelta} expected=0");
            else if ((me.PlayerCombatState?.TurnNumber ?? 0) != 2)
                Fail("history-course", $"turn={me.PlayerCombatState?.TurnNumber} expected=2");
            else
                Pass("history-course", $"autoDelta=1 direct=32 times=0 turn=2");
        }
        catch (Exception ex)
        {
            Fail("history-course", ex.ToString());
        }
    }

    private static void VerifyLostLuteBinding()
    {
        try
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.Powers.LostLutePower");
            if (type == null)
            {
                Skip("lost-lute-binding", "LostLutePower not present");
                return;
            }
            var method = AccessTools.Method(type, "AfterCardPlayed");
            if (method == null)
            {
                Fail("lost-lute-binding", "AfterCardPlayed not found");
                return;
            }
            var info = Harmony.GetPatchInfo(method);
            const string owner = "com.communitystats.sts2";
            int prefixes = info?.Prefixes?.Count(p => p.owner == owner) ?? 0;
            int postfixes = info?.Postfixes?.Count(p => p.owner == owner) ?? 0;
            GD.Print($"[AutoSourceProbe] CASE lost-lute-binding prefixes={prefixes} postfixes={postfixes}");
            if (prefixes == 1 && postfixes == 1)
                Pass("lost-lute-binding", "one production prefix/postfix");
            else
                Fail("lost-lute-binding", $"prefixes={prefixes} postfixes={postfixes} expected=1/1");
        }
        catch (Exception ex)
        {
            Fail("lost-lute-binding", ex.ToString());
        }
    }

    private static async Task PlayManual(PlayerChoiceContext ctx, CardModel card, Creature? target)
    {
        int energyValue = card.EnergyCost.CostsX
            ? card.EnergyCost.CapturedXValue
            : Math.Max(0, card.EnergyCost.GetAmountToSpend());
        var resources = new ResourceInfo
        {
            EnergySpent = energyValue,
            EnergyValue = energyValue,
            StarsSpent = 0,
            StarValue = Math.Max(0, card.GetStarCostWithModifiers()),
        };
        await card.OnPlayWrapper(ctx, target, isAutoPlay: false, resources, skipCardPileVisuals: true);
    }

    private static Task VerifyPersistence()
    {
        try
        {
            var identity = ContributionPersistence.GetActiveIdentity();
            if (identity == null || !identity.IsValid)
            {
                Fail("persistence", "active run identity missing");
                return Task.CompletedTask;
            }

            CombatTracker.Instance.FlushLiveState();
            var path = RunStorage.LivePath(identity);
            if (!File.Exists(path))
            {
                Fail("persistence", $"live file missing: {path}");
                return Task.CompletedTask;
            }

            var currentRows = CombatTracker.Instance.GetCurrentCombatData().Values;
            var target = currentRows.FirstOrDefault(a => a.SourceId == "THE_FOOL" && a.OriginSourceId == null)
                ?? currentRows.FirstOrDefault(a => a.SourceId == "HISTORY_COURSE" && a.OriginSourceId == null);
            if (target == null)
            {
                Fail("persistence", "no tracked persistence target row");
                return Task.CompletedTask;
            }
            string targetId = target.SourceId;
            int expectedAuto = target.AutoPlayedCount;
            int expectedTimes = target.TimesPlayed;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var jsonRow = FindJsonRow(doc.RootElement, targetId);
            int jsonAuto = -1;
            int jsonTimes = -1;
            if (jsonRow is { } parsed)
            {
                jsonAuto = ReadInt(parsed, "AutoPlayedCount");
                jsonTimes = ReadInt(parsed, "TimesPlayed");
            }

            var loaded = ContributionPersistence.LoadLiveState(identity);
            var loadedRow = loaded?.CurrentCombat?.Values
                .FirstOrDefault(a => a.SourceId == targetId && a.OriginSourceId == null);
            int loadedAuto = loadedRow?.AutoPlayedCount ?? -1;
            int loadedTimes = loadedRow?.TimesPlayed ?? -1;

            GD.Print($"[AutoSourceProbe] CASE persistence id={targetId} jsonAuto={jsonAuto} jsonTimes={jsonTimes} " +
                $"loadedAuto={loadedAuto} loadedTimes={loadedTimes} expected={expectedAuto}/{expectedTimes} path={path}");

            if (jsonAuto != expectedAuto || jsonTimes != expectedTimes)
                Fail("persistence", $"json {targetId} auto={jsonAuto} times={jsonTimes} expected={expectedAuto}/{expectedTimes}");
            else if (loadedAuto != expectedAuto || loadedTimes != expectedTimes)
                Fail("persistence", $"loaded {targetId} auto={loadedAuto} times={loadedTimes} expected={expectedAuto}/{expectedTimes}");
            else
                Pass("persistence", $"{targetId} json and reload match auto={expectedAuto} times={expectedTimes}");
        }
        catch (Exception ex)
        {
            Fail("persistence", ex.ToString());
        }
        return Task.CompletedTask;
    }

    private static int ReadInt(JsonElement row, string propertyName)
    {
        return row.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static JsonElement? FindJsonRow(JsonElement root, string sourceId)
    {
        if (!root.TryGetProperty("CurrentCombat", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.TryGetProperty("SourceId", out var id) && id.GetString() == sourceId)
                return row;
        }
        return null;
    }

    private static async Task<PowerModel?> ApplyPower(
        CombatState state, Player me, PlayerChoiceContext ctx, string powerId, string sourceCardId,
        string? relicSource = null)
    {
        var canonicalPower = ModelDb.AllPowers.FirstOrDefault(p => p.Id.Entry == powerId);
        if (canonicalPower == null)
        {
            GD.Print($"[AutoSourceProbe] SKIP {powerId}: power not present");
            return null;
        }
        var sourceCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == sourceCardId);
        if (sourceCanonical == null)
        {
            GD.Print($"[AutoSourceProbe] SKIP {powerId}: source card {sourceCardId} not present");
            return null;
        }
        var sourceCard = state.CreateCard(sourceCanonical, me);
        var applyModel = ModelDb.Contains(canonicalPower.GetType())
            ? canonicalPower.ToMutable()
            : canonicalPower;
        var previousContext = CombatTracker.Instance.CaptureAttributionContext();
        if (relicSource != null)
            CombatTracker.Instance.SetActiveRelic(relicSource);
        try
        {
            await PowerCmd.Apply(ctx, applyModel, me.Creature, 1m, me.Creature, sourceCard, silent: true);
        }
        finally
        {
            CombatTracker.Instance.RestoreAttributionContext(previousContext);
        }
        var applied = me.Creature.GetPower(canonicalPower.Id);
        GD.Print($"[AutoSourceProbe] power applied {powerId} instance={(applied != null)} " +
            $"source={ContributionMap.Instance.GetPowerSource(powerId)?.SourceId ?? "null"} " +
            $"relicContext={relicSource ?? "null"}");
        return applied;
    }

    private static async Task AddToPile(CardModel card, PileType pile, Player me)
    {
        await CardPileCmd.AddGeneratedCardsToCombat(
            new List<CardModel> { card }, pile, me, CardPilePosition.Top);
    }

    private static async Task ResetEnemies(CombatState state)
    {
        foreach (var enemy in state.HittableEnemies)
            await CreatureCmd.SetMaxAndCurrentHp(enemy, 400m);
    }

    private static ContributionAccum? FindRoot(string sourceId) =>
        CombatTracker.Instance.GetCurrentCombatData().Values
            .FirstOrDefault(a => a.SourceId == sourceId && a.OriginSourceId == null);

    private static void LogRows(string label)
    {
        foreach (var row in CombatTracker.Instance.GetCurrentCombatData().Values
                     .Where(a => a.TimesPlayed > 0 || a.AutoPlayedCount > 0
                         || a.TotalDamage > 0 || a.EffectiveBlock > 0 || a.OriginSourceId != null)
                     .OrderBy(a => a.SourceId))
        {
            GD.Print($"[AutoSourceProbe] ROW {label} id={row.SourceId} type={row.SourceType} " +
                $"times={row.TimesPlayed} auto={row.AutoPlayedCount} d={row.DirectDamage} " +
                $"m={row.ModifierDamage} a={row.AttributedDamage} block={row.EffectiveBlock} " +
                $"origin={row.OriginSourceId ?? "-"}");
        }
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
        GD.Print($"[AutoSourceProbe] PASS {name}: {detail}");
    }

    private static void Fail(string name, string detail)
    {
        _fail++;
        GD.Print($"[AutoSourceProbe] FAIL {name}: {detail}");
    }

    private static void Skip(string name, string detail)
    {
        _skip++;
        GD.Print($"[AutoSourceProbe] SKIP {name}: {detail}");
    }

    private static void Finish()
    {
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
