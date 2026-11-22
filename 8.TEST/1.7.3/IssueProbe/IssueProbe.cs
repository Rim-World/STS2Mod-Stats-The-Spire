using System;
using System.Collections.Generic;
using System.Reflection;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.IssueProbe;

/// <summary>
/// Test-only probe for the 1.7.3 fixes: mixed EnergyNextTurnPower stacks,
/// ancient event names, and run-replay DPS. Never shipped.
/// </summary>
[ModInitializer("Initialize")]
public static class IssueProbe
{
    private static int _frames;
    private static bool _ran;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_ran || _frames < 60) return;
            _ran = true;
            Safe.Run(() => Run(tree));
        };
    }

    private static void Run(SceneTree tree)
    {
        int pass = 0;
        int total = 0;
        try
        {
            total++; if (ProbeEnergyLayers()) pass++;
            total++; if (ProbeEventNames("CN")) pass++;
            L.Current = L.Lang.EN;
            total++; if (ProbeEventNames("EN")) pass++;
            total++; if (ProbeReplayDps()) pass++;
        }
        catch (Exception ex)
        {
            GD.Print($"[IssueProbe] EXCEPTION {ex}");
        }
        finally
        {
            try { ContributionPanel.Hide(); } catch { }
            try { CombatTracker.Instance.ForceResetAllContext(); } catch { }
            try { RunContributionAggregator.Instance.Reset(); } catch { }
        }

        GD.Print($"[IssueProbe] RESULT {pass}/{total} PASS");
        tree.Quit(pass == total ? 0 : 1);
    }

    private static bool ProbeEnergyLayers()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_ENERGY_LAYERS", "normal", 1);

            tracker.OnCardPlayStarted("WAH_KI", 0x7101);
            tracker.OnPowerApplied("ENERGY_NEXT_TURN_POWER", 1, 0x7100, true, true);
            tracker.OnCardPlayFinished();

            tracker.OnCardPlayStarted("KI_REST", 0x7102);
            tracker.OnPowerApplied("ENERGY_NEXT_TURN_POWER", 5, 0x7100, true, true);
            tracker.OnCardPlayFinished();

            tracker.SetActivePowerSourceManual("ENERGY_NEXT_TURN_POWER", "WAH_KI", "card");
            tracker.OnEnergyGained(6);

            var data = tracker.GetCurrentCombatData();
            int wah = data.TryGetValue("WAH_KI", out var a) ? a.EnergyGained : 0;
            int rest = data.TryGetValue("KI_REST", out var b) ? b.EnergyGained : 0;
            bool pass = wah == 1 && rest == 5;
            GD.Print($"[IssueProbe] energy wah={wah} ki_rest={rest} expect=1/5 {(pass ? "PASS" : "FAIL")}");
            tracker.ForceResetAllContext();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[IssueProbe] energy FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeEventNames(string label)
    {
        try
        {
            string expectedVakuu;
            string expectedPael;
            if (L.Current == L.Lang.CN)
            {
                try
                {
                    expectedVakuu = new LocString("ancients", "VAKUU.title").GetFormattedText();
                    expectedPael = new LocString("ancients", "PAEL.title").GetFormattedText();
                }
                catch
                {
                    expectedVakuu = "瓦库";
                    expectedPael = "佩尔";
                }
            }
            else
            {
                expectedVakuu = "Vakuu";
                expectedPael = "Pael";
            }

            string vakuu = NameLookup.Event("VAKUU");
            string pael = NameLookup.Event("PAEL");
            bool pass = vakuu == expectedVakuu && pael == expectedPael
                && vakuu != "VAKUU" && pael != "PAEL";
            GD.Print($"[IssueProbe] names {label} VAKUU=[{vakuu}] expected=[{expectedVakuu}] PAEL=[{pael}] expected=[{expectedPael}] {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[IssueProbe] names {label} FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeReplayDps()
    {
        try
        {
            var summary = new Dictionary<string, ContributionAccum>
            {
                ["PROBE_DAMAGE"] = new ContributionAccum
                {
                    SourceId = "PROBE_DAMAGE",
                    SourceType = "card",
                    DirectDamage = 4118,
                },
            };
            ContributionPanel.ShowRunReplay(summary, 114);
            var field = typeof(ContributionPanel).GetField(
                "_dpsLabel", BindingFlags.NonPublic | BindingFlags.Instance);
            var label = field?.GetValue(ContributionPanel.Instance) as Label;
            string text = label?.Text ?? "";
            bool pass = text.Contains("36.1", StringComparison.Ordinal);
            GD.Print($"[IssueProbe] replay-dps text=[{text}] expect contains 36.1 {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[IssueProbe] replay-dps FAIL {ex}");
            return false;
        }
    }
}
