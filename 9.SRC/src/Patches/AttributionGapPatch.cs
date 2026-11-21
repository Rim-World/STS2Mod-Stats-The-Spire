using System;
using System.Collections.Generic;
using System.Threading;
using CommunityStats.Collection;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CommunityStats.Patches;

// ═══════════════════════════════════════════════════════════
// 1.6.0 attribution gap hooks (021 audit §8 / §9 / §16.2).
//
// Every hook in this file is observation only: prefixes/postfixes
// read game state, write CombatTracker/ContributionMap records, and
// never alter a return value, an argument or game state.
//
// The gaps covered here are the ones that can be closed without
// touching CombatTracker/ContributionMap:
//   • BeltBuckle.BeforeCombatStart / AfterPotionDiscarded relic context
//   • SelfFormingClayPower.AfterBlockCleared power context
//   • RuinedHelmet.TryModifyPowerAmountReceived extra Strength → relic
//   • SneckoSkull.ModifyPowerAmountGivenAdditive extra Poison → relic layer
//   • UnsettlingLamp.ModifyPowerAmountGivenMultiplicative doubled debuff → relic layer
//   • TungstenRod / BeatingRemnant ModifyHpLostAfterOsty → MitigatedByBuff
//   • BrilliantScarf cost hooks and Bookmark EnergyCost.AddUntilPlayed → cost tag
//
// Power-amount hooks are split into "capture at modify time" and
// "commit after the amount was applied". The commit point is a
// Hook.AfterModifyingPowerAmountGiven/Received prefix, which runs
// after PowerModel.SetAmount, so the relic record is appended after
// the originating card's record and nothing is counted twice.
//
// Known limitation: SetAmount records the post-modifier amount against
// the card, so the relic entry added here sits on top of that record as
// an extra weight/layer. Both consumers distribute the real bonus/tick
// across the recorded sources, so totals stay exact, but per-source
// proportions are approximate until CombatTracker.OnPowerApplied can
// receive the pre-modifier amount (reported to the parent task).
// ═══════════════════════════════════════════════════════════

internal enum PowerExtraKind
{
    None = 0,
    RuinedHelmetStrength = 1,
    SneckoSkullPoison = 2,
    UnsettlingLampDebuff = 3,
}

internal sealed class PendingPowerExtra
{
    // Key by model id, not object identity: the power passed to the
    // TryModifyPowerAmount* hook may be the canonical model while the
    // AfterModifyingPowerAmount* hook receives the mutable instance.
    public string PowerId = "";
    public PowerExtraKind Kind;
    public int Amount;
    public long ExpiresAtMs;
}

/// <summary>
/// Async-local capture of a power-amount modification that must only be
/// recorded after the game applied it. Preview calls (PowerVar.PreviewValue)
/// also run the modify hooks but never reach the after hooks, so the TTL and
/// the canonical-model guard keep previews out of the ledger.
/// </summary>
internal static class PendingPowerExtraStore
{
    private const long TtlMs = 10_000;

    private static readonly AsyncLocal<List<PendingPowerExtra>?> Pending = new();

    public static void Capture(PowerModel? power, PowerExtraKind kind, int amount)
    {
        if (power == null || kind == PowerExtraKind.None || amount <= 0) return;

        string powerId = power.Id.Entry;
        if (string.IsNullOrEmpty(powerId)) return;

        var list = Pending.Value ??= new List<PendingPowerExtra>();
        long now = Environment.TickCount64;
        list.RemoveAll(e => now > e.ExpiresAtMs);

        foreach (var entry in list)
        {
            if (!string.Equals(entry.PowerId, powerId, StringComparison.Ordinal)
                || entry.Kind != kind) continue;
            entry.Amount += amount;
            entry.ExpiresAtMs = now + TtlMs;
            return;
        }

        list.Add(new PendingPowerExtra
        {
            PowerId = powerId,
            Kind = kind,
            Amount = amount,
            ExpiresAtMs = now + TtlMs,
        });
    }

    public static bool HasPending
    {
        get
        {
            var list = Pending.Value;
            return list != null && list.Count > 0;
        }
    }

    public static int Take(PowerModel? power, PowerExtraKind kind)
    {
        if (power == null) return 0;

        var list = Pending.Value;
        if (list == null || list.Count == 0) return 0;

        string powerId = power.Id.Entry;
        if (string.IsNullOrEmpty(powerId)) return 0;

        long now = Environment.TickCount64;
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (!string.Equals(entry.PowerId, powerId, StringComparison.Ordinal)
                || entry.Kind != kind) continue;
            list.RemoveAt(i);
            return now > entry.ExpiresAtMs ? 0 : entry.Amount;
        }
        return 0;
    }
}

// ═══════════════════════════════════════════════════════════
// R3: BeltBuckle — the combat-start and potion-discarded branches
// apply Dexterity without a relic context, so the +2 stacks were
// recorded against the resting/current source instead of the relic.
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class BeltBuckleContextGapPatch
{
    private const string RelicId = "BELT_BUCKLE";

    private static bool SetContext(BeltBuckle relic)
    {
        bool set = false;
        Safe.Run(() =>
        {
            if (!CombatHistoryPatch.IsLocalPlayer(relic.Owner)) return;
            CombatTracker.Instance.SetActiveRelic(RelicId);
            set = true;
        });
        return set;
    }

    private static void ClearContext(bool set)
    {
        if (!set) return;
        Safe.Run(() =>
        {
            if (CombatTracker.Instance.ActiveRelicId == RelicId)
                CombatTracker.Instance.ClearActiveRelic();
        });
    }

    [HarmonyPatch(typeof(BeltBuckle), nameof(BeltBuckle.BeforeCombatStart))]
    [HarmonyPrefix]
    public static void BeforeBeforeCombatStart(BeltBuckle __instance, out bool __state)
        => __state = SetContext(__instance);

    [HarmonyPatch(typeof(BeltBuckle), nameof(BeltBuckle.BeforeCombatStart))]
    [HarmonyPostfix]
    public static void AfterBeforeCombatStart(bool __state) => ClearContext(__state);

    [HarmonyPatch(typeof(BeltBuckle), nameof(BeltBuckle.AfterPotionDiscarded))]
    [HarmonyPrefix]
    public static void BeforeAfterPotionDiscarded(BeltBuckle __instance, out bool __state)
        => __state = SetContext(__instance);

    [HarmonyPatch(typeof(BeltBuckle), nameof(BeltBuckle.AfterPotionDiscarded))]
    [HarmonyPostfix]
    public static void AfterAfterPotionDiscarded(bool __state) => ClearContext(__state);
}

// ═══════════════════════════════════════════════════════════
// R5: RuinedHelmet doubles one Strength gain per combat. SetAmount
// records the doubled total against the card, so the relic needs its
// own entry in the STRENGTH_POWER source table. The extra is captured
// before the apply and committed after SetAmount, which keeps the
// relic entry behind the card entry (the distribution's last-source
// remainder then lands on the relic).
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class RuinedHelmetExtraStrengthPatch
{
    [HarmonyPatch(typeof(RuinedHelmet), nameof(RuinedHelmet.TryModifyPowerAmountReceived))]
    [HarmonyPostfix]
    public static void AfterTryModifyPowerAmountReceived(RuinedHelmet __instance, PowerModel canonicalPower,
        decimal amount, bool __result)
    {
        Safe.Run(() =>
        {
            if (!__result) return;
            if (amount <= 0m) return;
            if (canonicalPower is not StrengthPower) return;
            // Skip the canonical model used by PowerVar.PreviewValue.
            if (canonicalPower.IsCanonical) return;
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;

            // TryModifyPowerAmountReceived only doubles, so the extra
            // equals the incoming amount.
            PendingPowerExtraStore.Capture(canonicalPower, PowerExtraKind.RuinedHelmetStrength, (int)amount);
        });
    }
}

// ═══════════════════════════════════════════════════════════
// R4: SneckoSkull adds +1 Poison in ModifyPowerAmountGivenAdditive.
// The extra stack is recorded as a relic debuff layer so the poison
// tick split sees the relic as its own source (the card layer already
// carries the full applied amount; this file does not add another
// card-side record).
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class SneckoSkullExtraPoisonPatch
{
    [HarmonyPatch(typeof(SneckoSkull), nameof(SneckoSkull.ModifyPowerAmountGivenAdditive))]
    [HarmonyPostfix]
    public static void AfterModifyPowerAmountGivenAdditive(SneckoSkull __instance, PowerModel power,
        Creature giver, decimal amount, Creature? target, decimal __result)
    {
        Safe.Run(() =>
        {
            if (__result <= 0m) return;
            if (amount <= 0m) return;
            if (power is not PoisonPower) return;
            if (giver != __instance.Owner?.Creature) return;
            if (power.IsCanonical) return;
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;

            PendingPowerExtraStore.Capture(power, PowerExtraKind.SneckoSkullPoison, (int)__result);
        });
    }
}

// ═══════════════════════════════════════════════════════════
// R6: UnsettlingLamp doubles the first enemy debuff applied by a card.
// The doubled portion is recorded as a relic debuff layer; the game's
// applied amount (and the card's own record) stays untouched.
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class UnsettlingLampDoubleDebuffPatch
{
    [HarmonyPatch(typeof(UnsettlingLamp), nameof(UnsettlingLamp.ModifyPowerAmountGivenMultiplicative))]
    [HarmonyPostfix]
    public static void AfterModifyPowerAmountGivenMultiplicative(UnsettlingLamp __instance, PowerModel power,
        Creature giver, decimal amount, Creature? target, decimal __result)
    {
        Safe.Run(() =>
        {
            if (__result <= 1m) return;
            if (amount <= 0m) return;
            if (giver != __instance.Owner?.Creature) return;
            if (power.IsCanonical) return;
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;

            int extra = (int)(amount * (__result - 1m));
            PendingPowerExtraStore.Capture(power, PowerExtraKind.UnsettlingLampDebuff, extra);
        });
    }
}

// ═══════════════════════════════════════════════════════════
// Shared commit point for the three relic power-amount modifiers.
// Runs after PowerModel.SetAmount in both PowerCmd.Apply and
// PowerCmd.ModifyAmount, so the originating card's record exists
// first and the relic is appended as a separate source.
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class PowerAmountRelicCommitPatch
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterModifyingPowerAmountGiven))]
    [HarmonyPrefix]
    public static void BeforeAfterModifyingPowerAmountGiven(PowerModel modifiedPower)
    {
        Safe.Run(() =>
        {
            if (!PendingPowerExtraStore.HasPending) return;

            var powerId = modifiedPower?.Id.Entry;
            if (string.IsNullOrEmpty(powerId)) return;

            CommitDebuffLayer(modifiedPower, powerId!, PowerExtraKind.SneckoSkullPoison, "SNECKO_SKULL");
            CommitDebuffLayer(modifiedPower, powerId!, PowerExtraKind.UnsettlingLampDebuff, "UNSETTLING_LAMP");
        });
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterModifyingPowerAmountReceived))]
    [HarmonyPrefix]
    public static void BeforeAfterModifyingPowerAmountReceived(PowerModel modifiedPower)
    {
        Safe.Run(() =>
        {
            if (!PendingPowerExtraStore.HasPending) return;

            var powerId = modifiedPower?.Id.Entry;
            if (string.IsNullOrEmpty(powerId)) return;
            CommitStrength(modifiedPower, powerId!);
        });
    }

    private static void CommitStrength(PowerModel? power, string powerId)
    {
        if (power == null) return;
        int extra = PendingPowerExtraStore.Take(power, PowerExtraKind.RuinedHelmetStrength);
        if (extra <= 0) return;

        // Only split an amount that already has a recorded source. When the
        // base application carried no context (e.g. a combat-start relic
        // application the mod does not patch), adding the relic entry alone
        // would claim the whole Strength bonus — stay UNTRACKED instead.
        var existing = ContributionMap.Instance.GetPowerSources(powerId);
        if (existing == null || existing.Count == 0) return;

        ContributionMap.Instance.RecordPowerSource(powerId, "RUINED_HELMET", "relic", extra);
        ContributionMap.Instance.RecordPlayerBuffSource(powerId, "RUINED_HELMET", "relic", extra);
    }

    private static void CommitDebuffLayer(PowerModel? power, string powerId, PowerExtraKind kind,
        string relicId)
    {
        if (power == null) return;
        int extra = PendingPowerExtraStore.Take(power, kind);
        if (extra <= 0) return;

        var owner = power.Owner;
        if (owner == null) return;
        int creatureHash = owner.GetHashCode();

        // Same rule as CommitStrength: without an existing applier source the
        // relic layer would own 100% of the debuff tick/stack. Skip instead.
        if (ContributionMap.Instance.GetDebuffSource(creatureHash, powerId) == null) return;

        ContributionMap.Instance.RecordDebuffLayer(
            creatureHash, powerId, relicId, "relic", extra);

        // The card layer written by SetAmount already carries the full
        // modified amount, so the layer total is now `extra` above the
        // power's real stack count. For the three stack-ticked powers the
        // FIFO table is calibrated against the real amount (every tick
        // decrements one layer); remove the same `extra` from the FIFO head
        // so the totals stay aligned and the relic layer represents the
        // extra duration instead of leaving a residual layer behind.
        if (_fifoStackPowers.Contains(powerId))
        {
            for (int i = 0; i < extra; i++)
                ContributionMap.Instance.DecrementDebuffLayers(creatureHash, powerId);
        }
    }

    /// <summary>
    /// Powers whose debuff layers are decremented by the game's per-stack
    /// tick-down path (see DebuffDurationPatch.AfterSetAmount).
    /// </summary>
    private static readonly HashSet<string> _fifoStackPowers = new(StringComparer.Ordinal)
    {
        "POISON_POWER",
        "VULNERABLE_POWER",
        "WEAK_POWER",
    };
}

// ═══════════════════════════════════════════════════════════
// R7: TungstenRod (−1 HP loss per hit) and BeatingRemnant (20 HP
// loss cap per turn) reduce HP loss after Osty. The actual damage is
// untouched; the prevented amount is credited to the relic as
// mitigation (MitigatedByBuff), matching Intangible/Buffer/HardenedShell.
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class HpLossRelicMitigationPatch
{
    [HarmonyPatch(typeof(TungstenRod), nameof(TungstenRod.ModifyHpLostAfterOsty))]
    [HarmonyPrefix]
    public static void BeforeTungstenRodModify(Creature target, decimal amount, out decimal __state)
        => __state = amount;

    [HarmonyPatch(typeof(TungstenRod), nameof(TungstenRod.ModifyHpLostAfterOsty))]
    [HarmonyPostfix]
    public static void AfterTungstenRodModify(TungstenRod __instance, Creature target, decimal __state,
        decimal __result)
    {
        Safe.Run(() =>
        {
            if (!CombatManager.Instance.IsInProgress) return;
            if (!target.IsPlayer) return;
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;
            if (__result < 0m || __result >= __state) return;

            int prevented = (int)(__state - __result);
            CombatTracker.Instance.OnRelicIncomingDamageMitigation("TUNGSTEN_ROD", prevented);
        });
    }

    [HarmonyPatch(typeof(BeatingRemnant), nameof(BeatingRemnant.ModifyHpLostAfterOsty))]
    [HarmonyPrefix]
    public static void BeforeBeatingRemnantModify(Creature target, decimal amount, out decimal __state)
        => __state = amount;

    [HarmonyPatch(typeof(BeatingRemnant), nameof(BeatingRemnant.ModifyHpLostAfterOsty))]
    [HarmonyPostfix]
    public static void AfterBeatingRemnantModify(BeatingRemnant __instance, Creature target,
        decimal __state, decimal __result)
    {
        Safe.Run(() =>
        {
            if (!CombatManager.Instance.IsInProgress) return;
            if (!target.IsPlayer) return;
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;
            if (__result < 0m || __result >= __state) return;

            int prevented = (int)(__state - __result);
            CombatTracker.Instance.OnRelicIncomingDamageMitigation("BEATING_REMNANT", prevented);
        });
    }
}

// ═══════════════════════════════════════════════════════════
// R9: BrilliantScarf makes the 6th card free in the late combat cost
// hooks. Tagging the card at the hook (same pattern as the existing
// TagRelicReduction patches) lets AttributeCostSavings credit the
// energy/star saving to the relic at play time.
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class BrilliantScarfCostTagPatch
{
    private const string RelicId = "BRILLIANT_SCARF";

    private static void Tag(BrilliantScarf relic, CardModel? card, decimal originalCost,
        decimal modifiedCost, bool result)
    {
        if (!result || card == null) return;
        if (originalCost <= modifiedCost) return;
        if (!CombatHistoryPatch.IsLocalPlayer(relic.Owner)) return;
        if (!CombatHistoryPatch.IsLocalPlayer(card.Owner)) return;

        ContributionMap.Instance.TagCostReductionSource(
            card.GetHashCode(), RelicId, "relic");
    }

    [HarmonyPatch(typeof(BrilliantScarf), nameof(BrilliantScarf.TryModifyEnergyCostInCombatLate))]
    [HarmonyPostfix]
    public static void AfterTryModifyEnergyCost(BrilliantScarf __instance, CardModel card,
        decimal originalCost, decimal modifiedCost, bool __result)
    {
        Safe.Run(() => Tag(__instance, card, originalCost, modifiedCost, __result));
    }

    [HarmonyPatch(typeof(BrilliantScarf), nameof(BrilliantScarf.TryModifyStarCost))]
    [HarmonyPostfix]
    public static void AfterTryModifyStarCost(BrilliantScarf __instance, CardModel card,
        decimal originalCost, decimal modifiedCost, bool __result)
    {
        Safe.Run(() => Tag(__instance, card, originalCost, modifiedCost, __result));
    }
}

// ═══════════════════════════════════════════════════════════
// R9: Bookmark applies EnergyCost.AddUntilPlayed(-1) to the randomly
// chosen retained card. The relic context is set around AfterFlush
// and AddUntilPlayed identifies the exact card, so only that card is
// tagged (no hand-wide guessing).
// ═══════════════════════════════════════════════════════════

[HarmonyPatch]
public static class BookmarkCostTagPatch
{
    private const string RelicId = "BOOKMARK";

    [HarmonyPatch(typeof(Bookmark), nameof(Bookmark.AfterFlush))]
    [HarmonyPrefix]
    public static void BeforeAfterFlush(Bookmark __instance, out bool __state)
    {
        bool set = false;
        Safe.Run(() =>
        {
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;
            CombatTracker.Instance.SetActiveRelic(RelicId);
            set = true;
        });
        __state = set;
    }

    [HarmonyPatch(typeof(Bookmark), nameof(Bookmark.AfterFlush))]
    [HarmonyPostfix]
    public static void AfterAfterFlush(bool __state)
    {
        if (!__state) return;
        Safe.Run(() =>
        {
            if (CombatTracker.Instance.ActiveRelicId == RelicId)
                CombatTracker.Instance.ClearActiveRelic();
        });
    }

    [HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.AddUntilPlayed))]
    [HarmonyPostfix]
    public static void AfterAddUntilPlayed(CardEnergyCost __instance, int amount)
    {
        Safe.Run(() =>
        {
            if (amount >= 0) return;
            if (CombatTracker.Instance.ActiveRelicId != RelicId) return;

            CardModel? card;
            try
            {
                card = Traverse.Create(__instance).Field("_card").GetValue<CardModel>();
            }
            catch
            {
                return;
            }
            if (card == null) return;
            if (card.EnergyCost.Canonical <= 0) return;
            if (!CombatHistoryPatch.IsLocalPlayer(card.Owner)) return;

            ContributionMap.Instance.TagCostReductionSource(
                card.GetHashCode(), RelicId, "relic");
        });
    }
}
