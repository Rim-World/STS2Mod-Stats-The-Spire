using CommunityStats.Collection;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace StatsTheSpire.HistoryProbe;

/// <summary>
/// Test-only probe: verifies that two histories with seed 61111 resolve to
/// distinct StartTime directories and that old seed 51111 data still loads
/// through the legacy fallback.
/// </summary>
[ModInitializer("Initialize")]
public static class HistoryProbe
{
    private static int _frames;
    private static bool _started;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += OnFrame;
    }

    private static void OnFrame()
    {
        _frames++;
        if (!_started && _frames >= 600)
        {
            _started = true;
            _ = ProbeAsync();
        }
    }

    private static async Task ProbeAsync()
    {
        try
        {
            var all = await RunHistoryAnalyzer.Instance.LoadAllAsync(null, force: true);
            GD.Print($"[HistoryProbe] career all total={all.TotalRuns} wins={all.Wins}");

            var names = SaveManager.Instance.GetAllRunHistoryNames();
            foreach (var name in names)
            {
                var result = SaveManager.Instance.LoadRunHistory(name);
                if (result == null || !result.Success || result.SaveData == null) continue;
                var history = result.SaveData;
                if (history.Seed != "61111" && history.Seed != "51111") continue;

                var stats = RunHistoryAnalyzer.Instance.BuildSingleRunStats(history);
                var identity = RunIdentity.FromHistory(history);
                var summary = ContributionPersistence.LoadRunSummary(identity);
                GD.Print($"[HistoryProbe] seed={history.Seed} start={history.StartTime} win={history.Win} floor={stats.FloorReached} sources={summary?.Count ?? 0} identity={identity.RunKey}");
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[HistoryProbe] failed: {ex}");
        }
        finally
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Quit();
        }
    }
}
