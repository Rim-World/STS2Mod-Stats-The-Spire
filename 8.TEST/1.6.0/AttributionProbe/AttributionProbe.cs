using System;
using System.IO;
using System.Linq;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.AttributionProbe;

/// <summary>
/// Deterministic 1.6.0 probe for the pure tracker fixes that do not need a
/// live encounter: first-heal placement, forge-log consumption, upgrade-block
/// split, and pending-draw clearing. Test-only code; never shipped.
/// </summary>
[ModInitializer("Initialize")]
public static class AttributionProbe
{
    private const long StartTime = 1795336001L;
    private const string Seed = "codex160attrs";
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
        var identity = new RunIdentity(StartTime, Seed);
        var runDir = RunStorage.DirectoryFor(identity);
        bool all = false;
        try
        {
            Directory.CreateDirectory(ModConfig.DataDir);
            if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true);
            ContributionPersistence.SetActiveRun(identity);
            CombatTracker.Instance.ForceResetAllContext();
            RunContributionAggregator.Instance.Reset();

            all = ProbeFirstHeal() & ProbeForgeConsume() & ProbeUpgradeBlockSplit() & ProbePendingDrawClear() & ProbeContextRestore() & ProbeCostSavings() & ProbeActualHandDraw() & ProbeRestHealSplit() & ProbeMultiSourceBlock() & ProbeNestedCardContext();
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] EXCEPTION {ex}");
        }
        finally
        {
            try { ContributionPersistence.ClearRunContext(); } catch { }
            try { if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true); } catch { }
            try { CombatTracker.Instance.ForceResetAllContext(); } catch { }
            try { RunContributionAggregator.Instance.Reset(); } catch { }
        }

        GD.Print($"[AttributionProbe] RESULT {(all ? "PASS" : "FAIL")}");
        tree.Quit();
    }

    private static bool ProbeFirstHeal()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_FIRST_HEAL", "normal", 1);
            tracker.OnHealingReceived(5, "PROBE_FALLBACK", "probe");

            var current = tracker.GetCurrentCombatData();
            int currentHeal = current.Values.Where(v => v.SourceId == "PROBE_FALLBACK").Sum(v => v.HpHealed);
            bool pass = currentHeal == 5;
            GD.Print($"[AttributionProbe] first-heal current={currentHeal} expect=5 {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] first-heal FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeForgeConsume()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_FORGE", "normal", 1);
            tracker.OnForge("PROBE_FORGE_SOURCE", "card", 4);
            tracker.FlushForgeSubBars();

            var first = tracker.GetCurrentCombatData();
            int firstDamage = first
                .Where(kv => kv.Key.StartsWith("FORGE:PROBE_FORGE_SOURCE", StringComparison.Ordinal))
                .Sum(kv => kv.Value.DirectDamage);

            tracker.FlushForgeSubBars();
            var second = tracker.GetCurrentCombatData();
            int secondDamage = second
                .Where(kv => kv.Key.StartsWith("FORGE:PROBE_FORGE_SOURCE", StringComparison.Ordinal))
                .Sum(kv => kv.Value.DirectDamage);

            bool pass = firstDamage == 4 && secondDamage == 4;
            GD.Print($"[AttributionProbe] forge-consume first={firstDamage} second={secondDamage} expect=4/4 {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] forge-consume FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeUpgradeBlockSplit()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_UPGRADE_BLOCK", "normal", 1);

            int cardHash = 0x7A11;
            ContributionMap.Instance.RecordUpgradeDelta(cardHash, 0, 3, "PROBE_UPGRADER", "card");
            tracker.OnCardPlayStarted("PROBE_BLOCK_CARD", cardHash);
            tracker.OnBlockGained(8, null);
            tracker.OnDamageDealt(8, 8, null, true, 0x7A12, 0);

            var data = tracker.GetCurrentCombatData();
            int cardBlock = data.TryGetValue("PROBE_BLOCK_CARD", out var card) ? card.EffectiveBlock : 0;
            int upgradeBlock = data.TryGetValue("PROBE_UPGRADER", out var upg) ? upg.UpgradeBlock : 0;
            bool pass = cardBlock == 5 && upgradeBlock == 3;
            GD.Print($"[AttributionProbe] upgrade-block card={cardBlock} upgrade={upgradeBlock} expect=5/3 {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] upgrade-block FAIL {ex}");
            return false;
        }
    }


    private static bool ProbeContextRestore()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_CONTEXT_RESTORE", "normal", 1);

            tracker.SetActiveRelic("OUTER_RELIC");
            tracker.SetActivePowerSourceManual("OUTER_POWER", "OUTER_SOURCE", "power");
            var outer = tracker.CaptureAttributionContext();

            tracker.SetActiveRelic("INNER_RELIC");
            tracker.SetActivePowerSourceManual("INNER_POWER", "INNER_SOURCE", "power");
            tracker.RestoreAttributionContext(outer);

            bool pass = tracker.ActiveRelicId == "OUTER_RELIC"
                && tracker.ActivePowerSourceId == "OUTER_SOURCE"
                && tracker.ActivePowerSourceType == "power";
            GD.Print($"[AttributionProbe] context-restore relic={tracker.ActiveRelicId} power={tracker.ActivePowerSourceId} expect=OUTER_RELIC/OUTER_SOURCE {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] context-restore FAIL {ex}");
            return false;
        }
    }



    private static bool ProbeActualHandDraw()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_HAND_DRAW", "normal", 1);
            ContributionMap.Instance.RecordHandDrawBonus("PROBE_DRAW_SOURCE", "power", 3);
            for (int i = 0; i < 8; i++) tracker.OnCardDrawn(true);

            var data = tracker.GetCurrentCombatData();
            int full = data.TryGetValue("PROBE_DRAW_SOURCE", out var a) ? a.CardsDrawn : 0;

            // Simulate a hand-limit cut: 3 planned, only one actual card
            // beyond the base five.
            tracker.OnCombatStart("PROBE_HAND_LIMIT", "normal", 1);
            ContributionMap.Instance.RecordHandDrawBonus("PROBE_DRAW_SOURCE", "power", 3);
            for (int i = 0; i < 6; i++) tracker.OnCardDrawn(true);
            data = tracker.GetCurrentCombatData();
            int limited = data.TryGetValue("PROBE_DRAW_SOURCE", out var b) ? b.CardsDrawn : 0;

            // Negative modifiers reduce the net pool: +3 -2 => 1.
            tracker.OnCombatStart("PROBE_HAND_NET", "normal", 1);
            ContributionMap.Instance.RecordHandDrawBonus("PROBE_DRAW_SOURCE", "power", 3);
            ContributionMap.Instance.RecordHandDrawBonus("PROBE_DRAW_PENALTY", "power", -2);
            for (int i = 0; i < 8; i++) tracker.OnCardDrawn(true);
            data = tracker.GetCurrentCombatData();
            int net = data.TryGetValue("PROBE_DRAW_SOURCE", out var c) ? c.CardsDrawn : 0;
            bool pass = full == 3 && limited == 1 && net == 1;
            GD.Print($"[AttributionProbe] actual-hand-draw full={full} limited={limited} net={net} expect=3/1/1 {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] actual-hand-draw FAIL {ex}");
            return false;
        }
    }




    private static bool ProbeNestedCardContext()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_NESTED_CARD", "normal", 1);
            tracker.OnCardPlayStarted("OUTER_CARD", 0x7A41);
            tracker.OnCardPlayStarted("INNER_CARD", 0x7A42);
            bool inner = tracker.ActiveCardId == "INNER_CARD";
            tracker.OnCardPlayFinished();
            bool outer = tracker.ActiveCardId == "OUTER_CARD";
            tracker.OnCardPlayFinished();
            bool cleared = tracker.ActiveCardId == null;
            bool pass = inner && outer && cleared;
            GD.Print($"[AttributionProbe] nested-card inner={inner} outer={outer} cleared={cleared} {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] nested-card FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeMultiSourceBlock()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_MULTI_BLOCK", "normal", 1);
            ContributionMap.Instance.RecordPowerSource("PROBE_PLATING", "PROBE_SRC_A", "card", 2);
            ContributionMap.Instance.RecordPowerSource("PROBE_PLATING", "PROBE_SRC_B", "card", 1);
            tracker.SetActivePowerSource("PROBE_PLATING");
            tracker.OnBlockGained(9, null);
            tracker.OnDamageDealt(9, 9, null, true, 0x7A31, 0);

            var data = tracker.GetCurrentCombatData();
            int a = data.TryGetValue("PROBE_SRC_A", out var ac) ? ac.EffectiveBlock : 0;
            int b = data.TryGetValue("PROBE_SRC_B", out var bc) ? bc.EffectiveBlock : 0;
            bool pass = a == 6 && b == 3;
            GD.Print($"[AttributionProbe] multi-source-block a={a} b={b} expect=6/3 {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] multi-source-block FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeRestHealSplit()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.OnCombatEnd();
            tracker.ForceResetAllContext();
            RunContributionAggregator.Instance.Reset();

            tracker.SetRestHealBonus(15, "REGAL_PILLOW", "relic");
            tracker.OnHealingReceived(50, "REST_SITE", "rest");

            var totals = RunContributionAggregator.Instance.RunTotals;
            int rest = totals.TryGetValue("REST_SITE", out var r) ? r.HpHealed : 0;
            int pillow = totals.TryGetValue("REGAL_PILLOW", out var p) ? p.HpHealed : 0;
            bool pass = rest == 35 && pillow == 15 && rest + pillow == 50;
            GD.Print($"[AttributionProbe] rest-heal-split rest={rest} pillow={pillow} total={rest + pillow} expect=35/15/50 {(pass ? "PASS" : "FAIL")}");
            RunContributionAggregator.Instance.Reset();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] rest-heal-split FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeCostSavings()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_COST", "normal", 1);

            int normalHash = 0x7A21;
            ContributionMap.Instance.TagCostReductionSource(normalHash, "PROBE_RELIC", "relic");
            tracker.AttributeCostSavings(normalHash, 2, 0, 0, 0, false, false);

            int autoHash = 0x7A22;
            ContributionMap.Instance.TagCostReductionSource(autoHash, "PROBE_RELIC", "relic");
            tracker.AttributeCostSavings(autoHash, 2, 0, 0, 0, false, true);

            var data = tracker.GetCurrentCombatData();
            int saved = data.TryGetValue("PROBE_RELIC", out var accum) ? accum.EnergyGained : 0;
            bool pass = saved == 2;
            GD.Print($"[AttributionProbe] cost-savings saved={saved} expect=2(autoPlay=0) {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] cost-savings FAIL {ex}");
            return false;
        }
    }

    private static bool ProbePendingDrawClear()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            tracker.ForceResetAllContext();
            tracker.OnCombatStart("PROBE_PENDING_DRAW", "normal", 1);
            tracker.SetPendingDrawSource("PROBE_LEAK", "relic");
            tracker.OnTurnStart();
            tracker.OnCardDrawn(false);

            var data = tracker.GetCurrentCombatData();
            bool leaked = data.TryGetValue("PROBE_LEAK", out var leak) && leak.CardsDrawn > 0;
            bool pass = !leaked;
            GD.Print($"[AttributionProbe] pending-draw leaked={leaked} expect=False {(pass ? "PASS" : "FAIL")}");
            tracker.OnCombatEnd();
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[AttributionProbe] pending-draw FAIL {ex}");
            return false;
        }
    }
}
