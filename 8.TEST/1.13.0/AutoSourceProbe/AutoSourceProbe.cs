using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace StatsTheSpire.AutoSourceProbe;

/// <summary>
/// Directed probe for the 1.13.0 auto-play source contexts.
/// Uses real game hooks where available; all other auto-play is gated.
/// </summary>
[ModInitializer("Initialize")]
public static class AutoSourceProbe
{
    private static bool _subscribed;
    private static bool _started;
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
            var harmony = new Harmony("test.autosource_probe_1130");
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

            var hand = PileType.Hand.GetPile(me);
            if (hand.Cards.Count > 0)
                await CardPileCmd.RemoveFromCombat(hand.Cards.ToList(), skipVisuals: true);

            await RunHellraiser(state, me, ctx);
            await RunLostLute(state, me, ctx);
            await RunFool(state, me, ctx);
            await RunHowl(state, me, ctx);
            await RunPotion(state, me, ctx);
            await RunImbued(state, me, ctx);
            await RunHistoryCourse(state, me, ctx);

            LogRows("final");
            CombatTracker.Instance.NotifyCombatDataUpdated();
            CombatTracker.Instance.FlushLiveState();
        }
        catch (Exception ex)
        {
            Fail("fatal", ex.ToString());
        }
        finally
        {
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
            GD.Print($"[AutoSourceProbe] CASE hellraiser root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"auto={root?.AutoPlayedCount ?? 0} autoDelta={autoDelta} damageDelta={damageDelta} " +
                $"strikeAuto={strikeRow?.AutoPlayedCount ?? 0} powerRow={(powerRow != null)} " +
                $"source={ContributionMap.Instance.GetPowerSource("HELLRAISER_POWER")?.SourceId ?? "null"}");

            if (root == null || root.SourceType != "card")
                Fail("hellraiser", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 1)
                Fail("hellraiser", $"autoDelta={autoDelta}");
            else if (damageDelta <= 0)
                Fail("hellraiser", $"damageDelta={damageDelta}");
            else if ((strikeRow?.AutoPlayedCount ?? 0) > 0)
                Fail("hellraiser", $"strikeAuto={strikeRow?.AutoPlayedCount}");
            else if (powerRow != null)
                Fail("hellraiser", "HELLRAISER_POWER row exists");
            else
                Pass("hellraiser", $"autoDelta={autoDelta} damageDelta={damageDelta}");

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
            var defendCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "DEFEND_SILENT");
            if (choCanonical == null || defendCanonical == null)
            {
                Fail("lost-lute", $"CHO_CHORUS={choCanonical != null} DEFEND_SILENT={defendCanonical != null}");
                power.RemoveInternal();
                return;
            }

            var cho = state.CreateCard(choCanonical, me);
            var defend = state.CreateCard(defendCanonical, me);
            await AddToPile(cho, PileType.Hand, me);
            await AddToPile(defend, PileType.Draw, me);

            var rootBefore = FindRoot("LOST_LUTE");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
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
            var defendRow = FindRoot("DEFEND_SILENT");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            GD.Print($"[AutoSourceProbe] CASE lost-lute root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"auto={root?.AutoPlayedCount ?? 0} autoDelta={autoDelta} defendAuto={defendRow?.AutoPlayedCount ?? 0} " +
                $"source={ContributionMap.Instance.GetPowerSource("LOST_LUTE_POWER")?.SourceId ?? "null"}");

            if (root == null || root.SourceType != "card")
                Fail("lost-lute", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 1)
                Fail("lost-lute", $"autoDelta={autoDelta}");
            else
                Pass("lost-lute", $"autoDelta={autoDelta}");

            power.RemoveInternal();
        }
        catch (Exception ex)
        {
            Fail("lost-lute", ex.ToString());
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
            var defend = state.CreateCard(defendCanonical, me);
            await AddToPile(fool, PileType.Hand, me);
            await AddToPile(defend, PileType.Hand, me);

            var foolBefore = FindRoot("THE_FOOL");
            var defendBefore = FindRoot("DEFEND_SILENT");
            int beforeAuto = foolBefore?.AutoPlayedCount ?? 0;
            int beforeDefendAuto = defendBefore?.AutoPlayedCount ?? 0;
            _probeAutoPlay = true;
            try
            {
                await CardCmd.AutoPlay(ctx, fool, null);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("THE_FOOL");
            var defendRow = FindRoot("DEFEND_SILENT");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            int defendDelta = (defendRow?.AutoPlayedCount ?? 0) - beforeDefendAuto;
            GD.Print($"[AutoSourceProbe] CASE fool-onplay root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} defendAutoDelta={defendDelta}");

            if (root == null || root.SourceType != "card")
                Fail("fool-onplay", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 1)
                Fail("fool-onplay", $"autoDelta={autoDelta}");
            else if (defendDelta > 0)
                Fail("fool-onplay", $"defendAutoDelta={defendDelta}");
            else
                Pass("fool-onplay", $"autoDelta={autoDelta} defendAutoDelta={defendDelta}");
        }
        catch (Exception ex)
        {
            Fail("fool-onplay", ex.ToString());
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
            GD.Print($"[AutoSourceProbe] CASE howl root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} direct={root?.DirectDamage ?? 0}");

            if (root == null || root.SourceType != "card")
                Fail("howl", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 1)
                Fail("howl", $"autoDelta={autoDelta}");
            else if ((root?.DirectDamage ?? 0) <= 0)
                Fail("howl", $"direct={root?.DirectDamage ?? 0}");
            else
                Pass("howl", $"autoDelta={autoDelta} direct={root.DirectDamage}");
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

            var defendCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "DEFEND_SILENT");
            if (defendCanonical == null)
            {
                Fail("potion", "DEFEND_SILENT not found");
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                var defend = state.CreateCard(defendCanonical, me);
                await AddToPile(defend, PileType.Draw, me);
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
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            GD.Print($"[AutoSourceProbe] CASE potion root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} auto={root?.AutoPlayedCount ?? 0}");

            if (root == null || root.SourceType != "potion")
                Fail("potion", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 3)
                Fail("potion", $"autoDelta={autoDelta} expected>=3");
            else
                Pass("potion", $"autoDelta={autoDelta} type=potion");
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
            var defendCanonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == "DEFEND_SILENT");
            if (defendCanonical == null)
            {
                Skip("imbued", "DEFEND_SILENT not present");
                return;
            }

            var defend = state.CreateCard(defendCanonical, me);
            await AddToPile(defend, PileType.Hand, me);
            var enchantment = ModelDb.Enchantment<Imbued>().ToMutable();
            defend.EnchantInternal(enchantment, 1m);
            var rootBefore = FindRoot("DEFEND_SILENT");
            int beforeAuto = rootBefore?.AutoPlayedCount ?? 0;
            _probeAutoPlay = true;
            try
            {
                await defend.Enchantment!.AfterAutoPrePlayPhaseEntered(ctx, me);
            }
            finally
            {
                _probeAutoPlay = false;
            }
            await Task.Delay(150);

            var root = FindRoot("DEFEND_SILENT");
            var imbuedRow = FindRoot("IMBUED");
            int autoDelta = (root?.AutoPlayedCount ?? 0) - beforeAuto;
            GD.Print($"[AutoSourceProbe] CASE imbued root={(root != null)} type={root?.SourceType ?? "-"} " +
                $"autoDelta={autoDelta} imbuedRow={(imbuedRow != null)}");

            if (root == null || root.SourceType != "card")
                Fail("imbued", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 1)
                Fail("imbued", $"autoDelta={autoDelta}");
            else if (imbuedRow != null)
                Fail("imbued", "IMBUED row exists");
            else
                Pass("imbued", $"autoDelta={autoDelta} card-root");
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
                $"direct={root?.DirectDamage ?? 0} bludgeonAutoDelta={bludgeonDelta} " +
                $"turn={me.PlayerCombatState?.TurnNumber}");

            if (root == null || root.SourceType != "relic")
                Fail("history-course", $"root={root?.SourceId ?? "null"} type={root?.SourceType ?? "null"}");
            else if (autoDelta < 1)
                Fail("history-course", $"autoDelta={autoDelta}");
            else if ((root?.DirectDamage ?? 0) <= 0)
                Fail("history-course", $"direct={root?.DirectDamage ?? 0}");
            else if (bludgeonDelta > 0)
                Fail("history-course", $"bludgeonAutoDelta={bludgeonDelta}");
            else
                Pass("history-course", $"autoDelta={autoDelta} direct={root.DirectDamage}");
        }
        catch (Exception ex)
        {
            Fail("history-course", ex.ToString());
        }
    }

    private static async Task<PowerModel?> ApplyPower(
        CombatState state, Player me, PlayerChoiceContext ctx, string powerId, string sourceCardId)
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
        await PowerCmd.Apply(ctx, applyModel, me.Creature, 1m, me.Creature, sourceCard, silent: true);
        var applied = me.Creature.GetPower(canonicalPower.Id);
        GD.Print($"[AutoSourceProbe] power applied {powerId} instance={(applied != null)} " +
            $"source={ContributionMap.Instance.GetPowerSource(powerId)?.SourceId ?? "null"}");
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
