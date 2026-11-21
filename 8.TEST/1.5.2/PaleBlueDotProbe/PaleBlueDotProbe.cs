using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityStats.Collection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Powers;

namespace StatsTheSpire.PaleBlueDotProbe;

/// <summary>
/// Test-only probe: apply PaleBlueDotPower directly at the first combat,
/// record its logical card source, then verify that the next-turn draw is
/// attributed to PALE_BLUE_DOT exactly once and not to the fifth attack.
///
/// Set PALE_BLUE_AMOUNT=2 to exercise the upgraded amount. The probe quits
/// itself and prints PASS/FAIL lines.
/// </summary>
[ModInitializer("Initialize")]
public static class PaleBlueDotProbe
{
    private static int _frames;
    private static bool _applied;
    private static bool _sawDraw;
    private static int _drawFrames;
    private static int _firstDrawn;
    private static string _firstSource = "";
    private static string _otherDrawSource = "";
    private static string _mapSource = "";

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (!_applied)
            {
                TryApply();
                return;
            }

            TryReadDraw();
            if (_sawDraw && ++_drawFrames > 60)
                Finish(tree, pass: _firstDrawn == ExpectedAmount
                    && _firstSource == "PALE_BLUE_DOT"
                    && _mapSource == "PALE_BLUE_DOT"
                    && _otherDrawSource.Length == 0);
            else if (!_sawDraw && _frames > 3600)
                Finish(tree, pass: false);
        };
    }

    private static int ExpectedAmount
    {
        get
        {
            var raw = System.Environment.GetEnvironmentVariable("PALE_BLUE_AMOUNT");
            return int.TryParse(raw, out var value) && value > 0 ? value : 1;
        }
    }

    private static void TryApply()
    {
        var state = CombatManager.Instance?.DebugOnlyGetState();
        if (state == null || !CombatManager.Instance.IsInProgress) return;
        var me = LocalContext.GetMe(state);
        if (me?.Creature == null) return;

        _applied = true;
        int amount = ExpectedAmount;
        ContributionMap.Instance.RecordPowerSource(
            "PALE_BLUE_DOT_POWER", "PALE_BLUE_DOT", "card", amount);
        GD.Print($"[PaleBlueDotProbe] applying amount={amount} combat={state.Encounter?.Id.Entry ?? "?"}");
        _ = ApplyAsync(me.Creature, amount);
    }

    private static async Task ApplyAsync(MegaCrit.Sts2.Core.Entities.Creatures.Creature target, int amount)
    {
        try
        {
            await PowerCmd.Apply<PaleBlueDotPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                amount,
                target,
                null,
                silent: true);
            GD.Print("[PaleBlueDotProbe] power applied");
        }
        catch (Exception ex)
        {
            GD.Print($"[PaleBlueDotProbe] apply FAIL {ex}");
        }
    }

    private static void TryReadDraw()
    {
        try
        {
            var data = CombatTracker.Instance.GetCurrentCombatData();
            foreach (var (key, accum) in data)
            {
                if (accum.CardsDrawn <= 0) continue;
                if (accum.SourceId != "PALE_BLUE_DOT" && accum.SourceId != "PALE_BLUE_DOT_POWER")
                {
                    _otherDrawSource = accum.SourceId;
                    continue;
                }
                if (_sawDraw && accum.CardsDrawn == _firstDrawn && accum.SourceId == _firstSource) continue;

                if (!_sawDraw)
                {
                    _firstDrawn = accum.CardsDrawn;
                    _firstSource = accum.SourceId;
                    _mapSource = ContributionMap.Instance.GetPowerSource("DRAW_CARDS_NEXT_TURN_POWER")?.SourceId ?? "";
                    _sawDraw = true;
                    _drawFrames = 0;
                }
                GD.Print($"[PaleBlueDotProbe] draw source={accum.SourceId} count={accum.CardsDrawn} first={_sawDraw} key={key}");
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[PaleBlueDotProbe] read FAIL {ex.Message}");
        }
    }

    private static void Finish(SceneTree tree, bool pass)
    {
        GD.Print($"[PaleBlueDotProbe] RESULT {(pass ? "PASS" : "FAIL")} expected={ExpectedAmount} firstDrawn={_firstDrawn} source={_firstSource} mapSource={_mapSource} otherSource={_otherDrawSource}");
        tree.Quit();
    }
}
