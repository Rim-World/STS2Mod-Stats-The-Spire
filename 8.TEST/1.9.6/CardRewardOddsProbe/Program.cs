using System;
using System.Collections.Generic;
using CommunityStats.Util;

namespace StatsTheSpire.CardRewardOddsProbe;

internal static class Program
{
    private const double Reset = -0.05;
    private const double Cap = 0.4;
    private static int _failures;

    private static int Main()
    {
        Console.WriteLine("[CardRewardOddsProbe] start");

        CheckDistribution(
            "A0 regular 3 offset -0.05",
            CalcOffsetGroup(3, 0.03, 0.37, 0.01, -0.05),
            new[] { 1.0, 0.0, 0.0, 0.0 });

        CheckDistribution(
            "A7 regular 3 offset 0",
            CalcOffsetGroup(3, 0.0149, 0.37, 0.005, 0.0),
            new[] { 0.941455646901, 0.058544353099, 0.0, 0.0 });

        CheckDistribution(
            "A0 elite 3 offset +0.2",
            CalcOffsetGroup(3, 0.1, 0.4, 0.01, 0.2),
            new[] { 0.32844, 0.62861, 0.0422, 0.00075 });

        CheckDistribution(
            "A0 candy 4 offset -0.05",
            CalcCandy4(0.03, 0.37, 0.01, -0.05),
            new[] { 0.97, 0.03, 0.0, 0.0, 0.0 });

        CheckDistribution(
            "BGH 3 fixed rare",
            CalcFixed(3),
            new[] { 0.0, 0.0, 0.0, 1.0 });

        CheckDistribution(
            "BGH candy 4: all fixed rare",
            CalcFixed(4),
            new[] { 0.0, 0.0, 0.0, 0.0, 1.0 });

        CheckDistribution(
            "Boss 4 fixed rare",
            CalcFixed(4),
            new[] { 0.0, 0.0, 0.0, 0.0, 1.0 });

        CheckDistribution(
            "No rare in pool: 3 offset cards",
            CardRewardGroupOdds.Calculate(
                OffsetProfiles(3, 0.03, 0.37, 0.01),
                -0.05,
                CardRewardRarityMask.Common | CardRewardRarityMask.Uncommon,
                Reset,
                Cap),
            new[] { 1.0, 0.0, 0.0, 0.0 });

        CheckDistribution(
            "Rare reset and non-rare cap: 2 offset cards from +0.4",
            CalcOffsetGroup(2, 0.03, 0.37, 0.01, 0.4),
            new[] { 0.3249, 0.6751, 0.0 });

        CheckCandyPrediction("candy count 0", 0, true, true, false);
        CheckCandyPrediction("candy count 1", 1, true, true, true);
        CheckCandyPrediction("candy count 2", 2, true, true, false);
        CheckCandyPrediction("no candy", 0, false, true, false);
        CheckCandyPrediction("no power", 0, true, false, false);

        CheckThrows("empty allowed mask",
            () => CardRewardGroupOdds.Calculate(
                OffsetProfiles(3, 0.03, 0.37, 0.01), -0.05, CardRewardRarityMask.None, Reset, Cap));

        CheckThrows("invalid base rare",
            () => CardRewardGroupOdds.Calculate(
                new[] { CardRewardRollProfile.OffsetRoll(1.1, 0.2, 0.01) },
                -0.05, CardRewardRarityMask.All, Reset, Cap));

        Console.WriteLine($"[CardRewardOddsProbe] RESULT {(_failures == 0 ? "PASS" : "FAIL")} failures={_failures}");
        return _failures == 0 ? 0 : 1;
    }

    private static CardRewardGroupOddsResult CalcOffsetGroup(
        int count, double rare, double uncommon, double growth, double offset)
        => CardRewardGroupOdds.Calculate(
            OffsetProfiles(count, rare, uncommon, growth),
            offset,
            CardRewardRarityMask.All,
            Reset,
            Cap);

    private static CardRewardGroupOddsResult CalcCandy4(
        double rare, double uncommon, double growth, double offset)
        => CardRewardGroupOdds.Calculate(
            CandyProfiles(rare, uncommon, growth),
            offset,
            CardRewardRarityMask.All,
            Reset,
            Cap);

    private static CardRewardGroupOddsResult CalcFixed(int count)
    {
        var profiles = new List<CardRewardRollProfile>(count);
        for (int i = 0; i < count; i++)
            profiles.Add(CardRewardRollProfile.FixedRare());
        return CardRewardGroupOdds.Calculate(
            profiles, -0.05, CardRewardRarityMask.All, Reset, Cap);
    }

    private static List<CardRewardRollProfile> OffsetProfiles(
        int count, double rare, double uncommon, double growth)
    {
        var profiles = new List<CardRewardRollProfile>(count);
        for (int i = 0; i < count; i++)
            profiles.Add(CardRewardRollProfile.OffsetRoll(rare, uncommon, growth));
        return profiles;
    }

    private static List<CardRewardRollProfile> CandyProfiles(
        double rare, double uncommon, double growth)
    {
        var profiles = new List<CardRewardRollProfile>(4)
        {
            CardRewardRollProfile.OffsetRoll(rare, uncommon, growth),
            CardRewardRollProfile.OffsetRoll(rare, uncommon, growth),
            CardRewardRollProfile.OffsetRoll(rare, uncommon, growth),
            CardRewardRollProfile.BaseRoll(rare, uncommon),
        };
        return profiles;
    }

    private static void CheckDistribution(
        string name, CardRewardGroupOddsResult result, double[] expected)
    {
        bool ok = result.CardCount == expected.Length - 1;
        double sum = 0.0;
        for (int i = 0; i < expected.Length; i++)
        {
            double actual = i < result.RareCountProbabilities.Count
                ? result.RareCountProbabilities[i]
                : double.NaN;
            sum += actual;
            if (Math.Abs(actual - expected[i]) > 1e-9)
                ok = false;
        }
        if (Math.Abs(sum - 1.0) > 1e-9) ok = false;
        if (Math.Abs(result.AtLeastOneRare - (1.0 - result.RareCountProbabilities[0])) > 1e-9)
            ok = false;

        Console.WriteLine(
            $"[CardRewardOddsProbe] {name}: [{string.Join(", ", result.RareCountProbabilities)}] " +
            $"atLeast={result.AtLeastOneRare:F12} {(ok ? "PASS" : "FAIL")}");
        if (!ok)
        {
            _failures++;
            Console.WriteLine($"[CardRewardOddsProbe] expected [{string.Join(", ", expected)}]");
        }
    }

    private static void CheckCandyPrediction(
        string name, int seen, bool hasCandy, bool hasPower, bool expected)
    {
        bool actual = CardRewardGroupOdds.WillLastingCandyAddFourthCard(seen, hasCandy, hasPower);
        bool ok = actual == expected;
        Console.WriteLine($"[CardRewardOddsProbe] {name}: {actual} {(ok ? "PASS" : "FAIL")}");
        if (!ok) _failures++;
    }

    private static void CheckThrows(string name, Action action)
    {
        bool ok = false;
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            ok = true;
        }
        catch (Exception)
        {
            ok = false;
        }
        Console.WriteLine($"[CardRewardOddsProbe] {name}: {(ok ? "PASS" : "FAIL")}");
        if (!ok) _failures++;
    }
}
