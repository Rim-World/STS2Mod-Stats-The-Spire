using System;
using System.Collections.Generic;

namespace CommunityStats.Util;

/// <summary>Rarity that a card reward slot can show.</summary>
public enum CardRewardRarity
{
    Common,
    Uncommon,
    Rare,
}

/// <summary>Allowed rarities after the card pool is filtered.</summary>
[Flags]
public enum CardRewardRarityMask
{
    None = 0,
    Common = 1,
    Uncommon = 2,
    Rare = 4,
    All = Common | Uncommon | Rare,
}

/// <summary>How one card slot in a reward group rolls its rarity.</summary>
public enum CardRewardRollMode
{
    /// <summary>Use the shared pity offset and advance it after the roll.</summary>
    Offset,

    /// <summary>Use the base table only; do not read or advance the pity offset.</summary>
    Base,

    /// <summary>Always rare; used by Boss rewards and Big Game Hunter.</summary>
    FixedRare,
}

/// <summary>
/// One card slot. The calculator is pure and does not read game state.
/// </summary>
public readonly record struct CardRewardRollProfile(
    CardRewardRollMode Mode,
    double BaseRare = 0,
    double BaseUncommon = 0,
    double Growth = 0)
{
    public static CardRewardRollProfile OffsetRoll(double baseRare, double baseUncommon, double growth)
        => new(CardRewardRollMode.Offset, baseRare, baseUncommon, growth);

    public static CardRewardRollProfile BaseRoll(double baseRare, double baseUncommon)
        => new(CardRewardRollMode.Base, baseRare, baseUncommon);

    public static CardRewardRollProfile FixedRare()
        => new(CardRewardRollMode.FixedRare);
}

/// <summary>
/// Exact rare-count distribution for one base card reward group.
/// The result is indexed by the number of rare cards in the group.
/// </summary>
public sealed class CardRewardGroupOddsResult
{
    public int CardCount { get; }
    public IReadOnlyList<double> RareCountProbabilities { get; }
    public double AtLeastOneRare { get; }

    internal CardRewardGroupOddsResult(int cardCount, double[] rareCountProbabilities)
    {
        CardCount = cardCount;
        RareCountProbabilities = rareCountProbabilities;
        AtLeastOneRare = 1.0 - rareCountProbabilities[0];
    }
}

/// <summary>
/// Simulates the game's sequential rarity roll without using RNG.
/// Rarity outcomes are enumerated as a probability tree. The shared pity
/// offset advances after each offset-based card, including when the rolled
/// rarity is remapped because the pool lacks that rarity.
/// </summary>
public static class CardRewardGroupOdds
{
    private const double SumTolerance = 1e-9;

    /// <summary>
    /// Calculate the rare-count distribution for the given slots.
    /// </summary>
    /// <param name="profiles">Card slots in generated order.</param>
    /// <param name="initialOffset">Shared pity offset before the first card.</param>
    /// <param name="allowed">Rarities present in the card pool.</param>
    /// <param name="rareOffsetReset">Offset written when a raw rare is rolled.</param>
    /// <param name="maxOffset">Upper bound for the pity offset.</param>
    public static CardRewardGroupOddsResult Calculate(
        IReadOnlyList<CardRewardRollProfile> profiles,
        double initialOffset,
        CardRewardRarityMask allowed,
        double rareOffsetReset,
        double maxOffset)
    {
        if (profiles == null)
            throw new ArgumentNullException(nameof(profiles));
        if (allowed == CardRewardRarityMask.None)
            throw new ArgumentException("At least one rarity must be allowed.", nameof(allowed));
        if (!IsFinite(initialOffset) || !IsFinite(rareOffsetReset) || !IsFinite(maxOffset))
            throw new ArgumentOutOfRangeException(nameof(initialOffset), "Offset values must be finite.");
        if (maxOffset < rareOffsetReset)
            throw new ArgumentOutOfRangeException(nameof(maxOffset), "maxOffset must be at least rareOffsetReset.");

        foreach (var profile in profiles)
            ValidateProfile(profile);

        var states = new List<RollState> { new(initialOffset, 0, 1.0) };

        foreach (var profile in profiles)
        {
            var next = new List<RollState>(states.Count * 3);
            foreach (var state in states)
            {
                AddOutcome(next, state, profile, profile.Mode == CardRewardRollMode.Offset
                    ? SplitOffsetOdds(profile.BaseRare, profile.BaseUncommon, state.Offset)
                    : SplitBaseOdds(profile.BaseRare, profile.BaseUncommon),
                    allowed, rareOffsetReset, maxOffset);
            }
            states = next;
        }

        var distribution = new double[profiles.Count + 1];
        double total = 0.0;
        foreach (var state in states)
        {
            if (state.RareCount < 0 || state.RareCount >= distribution.Length)
                throw new InvalidOperationException("Invalid rare-count state.");
            if (!IsFinite(state.Probability) || state.Probability < 0.0)
                throw new InvalidOperationException("Invalid probability state.");
            distribution[state.RareCount] += state.Probability;
            total += state.Probability;
        }

        for (int i = 0; i < distribution.Length; i++)
        {
            if (distribution[i] < 0.0 && distribution[i] > -SumTolerance)
                distribution[i] = 0.0;
            if (!IsFinite(distribution[i]) || distribution[i] < 0.0)
                throw new InvalidOperationException("Invalid rarity distribution.");
        }

        if (!IsFinite(total) || Math.Abs(total - 1.0) > SumTolerance)
            throw new InvalidOperationException($"Rarity distribution does not sum to 1: {total:R}");

        return new CardRewardGroupOddsResult(profiles.Count, distribution);
    }

    /// <summary>
    /// Predicts whether LastingCandy adds a fourth card to the next combat
    /// card reward. Rewards are populated before BeforeCombatRewardOffered
    /// increments CombatRewardsSeen, so the fourth card is added when the
    /// current count is odd.
    /// </summary>
    public static bool WillLastingCandyAddFourthCard(
        int combatRewardsSeen, bool hasLastingCandy, bool hasPowerCard)
    {
        if (!hasLastingCandy || !hasPowerCard || combatRewardsSeen < 0)
            return false;
        return (combatRewardsSeen & 1) == 1;
    }

    private static void AddOutcome(
        List<RollState> next,
        RollState state,
        CardRewardRollProfile profile,
        (double Rare, double Uncommon, double Common) odds,
        CardRewardRarityMask allowed,
        double rareOffsetReset,
        double maxOffset)
    {
        if (profile.Mode == CardRewardRollMode.FixedRare)
        {
            AddMapped(next, state, CardRewardRarity.Rare, 1.0, allowed,
                state.Offset, profile.Growth, rareOffsetReset, maxOffset, updateOffset: false);
            return;
        }

        AddMapped(next, state, CardRewardRarity.Rare, odds.Rare, allowed,
            state.Offset, profile.Growth, rareOffsetReset, maxOffset, updateOffset: profile.Mode == CardRewardRollMode.Offset);
        AddMapped(next, state, CardRewardRarity.Uncommon, odds.Uncommon, allowed,
            state.Offset, profile.Growth, rareOffsetReset, maxOffset, updateOffset: profile.Mode == CardRewardRollMode.Offset);
        AddMapped(next, state, CardRewardRarity.Common, odds.Common, allowed,
            state.Offset, profile.Growth, rareOffsetReset, maxOffset, updateOffset: profile.Mode == CardRewardRollMode.Offset);
    }

    private static void AddMapped(
        List<RollState> next,
        RollState state,
        CardRewardRarity raw,
        double probability,
        CardRewardRarityMask allowed,
        double offset,
        double growth,
        double rareOffsetReset,
        double maxOffset,
        bool updateOffset)
    {
        if (probability <= 0.0)
            return;
        if (!TryMap(raw, allowed, out var selected))
            return;

        double nextOffset = offset;
        if (updateOffset)
        {
            nextOffset = raw == CardRewardRarity.Rare
                ? rareOffsetReset
                : Math.Min(offset + growth, maxOffset);
        }

        int rareCount = state.RareCount + (selected == CardRewardRarity.Rare ? 1 : 0);
        next.Add(new RollState(nextOffset, rareCount, state.Probability * probability));
    }

    private static (double Rare, double Uncommon, double Common) SplitOffsetOdds(
        double baseRare, double baseUncommon, double offset)
        => Split(baseRare, baseUncommon, offset);

    private static (double Rare, double Uncommon, double Common) SplitBaseOdds(
        double baseRare, double baseUncommon)
        => Split(baseRare, baseUncommon, 0.0);

    private static (double Rare, double Uncommon, double Common) Split(
        double baseRare, double baseUncommon, double offset)
    {
        double rareThreshold = baseRare + offset;
        double rare = Clamp01(rareThreshold);
        double uncommon = Math.Min(Clamp01(baseUncommon + Math.Min(0.0, rareThreshold)), 1.0 - rare);
        double common = Clamp01(1.0 - rare - uncommon);
        return (rare, uncommon, common);
    }

    private static void ValidateProfile(CardRewardRollProfile profile)
    {
        switch (profile.Mode)
        {
            case CardRewardRollMode.FixedRare:
                return;
            case CardRewardRollMode.Offset:
            case CardRewardRollMode.Base:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), "Unknown roll mode.");
        }

        if (!IsFinite(profile.BaseRare) || profile.BaseRare < 0.0 || profile.BaseRare > 1.0)
            throw new ArgumentOutOfRangeException(nameof(profile), "BaseRare must be in [0, 1].");
        if (!IsFinite(profile.BaseUncommon) || profile.BaseUncommon < 0.0 || profile.BaseUncommon > 1.0)
            throw new ArgumentOutOfRangeException(nameof(profile), "BaseUncommon must be in [0, 1].");
        if (!IsFinite(profile.Growth) || profile.Growth < 0.0)
            throw new ArgumentOutOfRangeException(nameof(profile), "Growth must be finite and non-negative.");
    }

    private static bool TryMap(
        CardRewardRarity raw,
        CardRewardRarityMask allowed,
        out CardRewardRarity selected)
    {
        var candidate = raw;
        for (int i = 0; i < 3; i++)
        {
            if (IsAllowed(candidate, allowed))
            {
                selected = candidate;
                return true;
            }
            candidate = NextRarity(candidate);
        }

        selected = default;
        return false;
    }

    private static bool IsAllowed(CardRewardRarity rarity, CardRewardRarityMask allowed)
        => rarity switch
        {
            CardRewardRarity.Common => (allowed & CardRewardRarityMask.Common) != 0,
            CardRewardRarity.Uncommon => (allowed & CardRewardRarityMask.Uncommon) != 0,
            CardRewardRarity.Rare => (allowed & CardRewardRarityMask.Rare) != 0,
            _ => false,
        };

    private static CardRewardRarity NextRarity(CardRewardRarity rarity)
        => rarity switch
        {
            CardRewardRarity.Common => CardRewardRarity.Uncommon,
            CardRewardRarity.Uncommon => CardRewardRarity.Rare,
            CardRewardRarity.Rare => CardRewardRarity.Common,
            _ => rarity,
        };

    private static double Clamp01(double value)
        => Math.Clamp(value, 0.0, 1.0);

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private readonly record struct RollState(double Offset, int RareCount, double Probability);
}
