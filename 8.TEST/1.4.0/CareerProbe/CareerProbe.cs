using CommunityStats.Collection;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.CareerProbe;

/// <summary>
/// Test-only probe: after the profile is initialized, load career stats,
/// verify averages and mode filters, and instantiate the career UI.
/// </summary>
[ModInitializer("Initialize")]
public static class CareerProbe
{
    private static int _frames;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            Safe.Warn("[CareerProbe] no SceneTree");
            return;
        }

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_frames == 600)
            {
                Safe.Info("[CareerProbe] starting");
                _ = ProbeAsync();
            }
        };
    }

    private static async Task ProbeAsync()
    {
        try
        {
            var all = await RunHistoryAnalyzer.Instance.LoadAllAsync(null, force: true);
            Safe.Info($"[CareerProbe] all total={all.TotalRuns} wins={all.Wins} mode={all.ModeFilter} " +
                      $"avgTime={all.AverageRunTimeSeconds:F1} avgDeck={all.AverageDeckSize:F1} " +
                      $"avgRelics={all.AverageRelicCount:F1} atk={all.AverageAttackCount:F1} " +
                      $"skl={all.AverageSkillCount:F1} pwr={all.AveragePowerCount:F1} " +
                      $"common={all.AverageCommonCount:F1} uncommon={all.AverageUncommonCount:F1} rare={all.AverageRareCount:F1}");

            var standard = await RunHistoryAnalyzer.Instance.LoadAllAsync(null, force: true, modeFilter: CareerRunModeFilter.Standard);
            Safe.Info($"[CareerProbe] standard total={standard.TotalRuns} wins={standard.Wins} mode={standard.ModeFilter}");

            var multiplayer = await RunHistoryAnalyzer.Instance.LoadAllAsync(null, force: true, modeFilter: CareerRunModeFilter.Multiplayer);
            Safe.Info($"[CareerProbe] multiplayer total={multiplayer.TotalRuns} mode={multiplayer.ModeFilter}");

            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                var section = CareerStatsSection.Create(null);
                tree.Root.AddChild(section);
                Safe.Info($"[CareerProbe] section children={section.GetChildCount()}");
            }
        }
        catch (Exception ex)
        {
            Safe.Warn($"[CareerProbe] failed: {ex}");
        }
    }
}
