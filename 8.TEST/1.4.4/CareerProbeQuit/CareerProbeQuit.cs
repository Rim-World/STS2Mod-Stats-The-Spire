using CommunityStats.Collection;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.CareerProbeQuit;

/// <summary>
/// Test-only probe: read career stats after a run, log totals, then quit.
/// Used to verify the run just completed was added to history.
/// </summary>
[ModInitializer("Initialize")]
public static class CareerProbeQuit
{
    private static int _frames;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            Safe.Warn("[CareerProbeQuit] no SceneTree");
            return;
        }

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_frames == 600) _ = ProbeAsync(tree);
        };
    }

    private static async Task ProbeAsync(SceneTree tree)
    {
        try
        {
            var all = await RunHistoryAnalyzer.Instance.LoadAllAsync(null, force: true);
            var standard = await RunHistoryAnalyzer.Instance.LoadAllAsync(
                null, force: true, modeFilter: CareerRunModeFilter.Standard);
            var multiplayer = await RunHistoryAnalyzer.Instance.LoadAllAsync(
                null, force: true, modeFilter: CareerRunModeFilter.Multiplayer);
            var fool = await RunHistoryAnalyzer.Instance.LoadAllAsync("FOOL", force: true);

            Safe.Info($"[CareerProbeQuit] all total={all.TotalRuns} wins={all.Wins}");
            Safe.Info($"[CareerProbeQuit] standard total={standard.TotalRuns} wins={standard.Wins}");
            Safe.Info($"[CareerProbeQuit] multiplayer total={multiplayer.TotalRuns}");
            Safe.Info($"[CareerProbeQuit] fool total={fool.TotalRuns} wins={fool.Wins}");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[CareerProbeQuit] failed: {ex}");
        }
        finally
        {
            tree.Quit();
        }
    }
}
