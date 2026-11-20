using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityStats.Patches;
using CommunityStats.UI;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace StatsTheSpire.MapFixProbe;

/// <summary>
/// Test-only probe for the map hover panel cleanup. It waits until the game
/// opens the map screen, attaches a panel to one map point, registers it in
/// MapPointPatch's private table, then closes the map and checks that the
/// panel was hidden and queued for deletion.
/// </summary>
[ModInitializer("Initialize")]
public static class MapFixProbe
{
    private static Harmony? _harmony;
    private static bool _started;

    public static void Initialize()
    {
        _harmony = new Harmony("test.statsthespire.mapfix");
        var target = AccessTools.Method(typeof(NMapScreen), nameof(NMapScreen.Open));
        var postfix = new HarmonyMethod(AccessTools.Method(typeof(MapFixProbe), nameof(AfterMapOpen)));
        _harmony.Patch(target, postfix: postfix);
        GD.Print("[MapFixProbe] initialized");
    }

    public static void AfterMapOpen(NMapScreen __instance)
    {
        if (_started) return;
        _started = true;
        Callable.From(() => Run(__instance)).CallDeferred();
    }

    private static void Run(NMapScreen screen)
    {
        InfoModPanel? panel = null;
        NMapPoint? point = null;
        try
        {
            var points = screen.GetNodeOrNull<Control>("TheMap/Points");
            if (points == null) { Fail("TheMap/Points not found"); return; }

            point = points.GetChildren().OfType<NMapPoint>()
                .FirstOrDefault(p => p.Point?.PointType == MapPointType.Shop)
                ?? points.GetChildren().OfType<NMapPoint>().FirstOrDefault();
            if (point == null) { Fail("no NMapPoint child"); return; }

            panel = InfoModPanel.Create("MapFixProbe", "test");
            point.AddChild(panel);
            point.SetMeta("sts_hover_info_panel", true);

            var field = typeof(MapPointPatch).GetField("_panels",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (field?.GetValue(null) is not ConditionalWeakTable<NMapPoint, InfoModPanel> table)
            {
                Fail("_panels table not found");
                return;
            }
            table.Remove(point);
            table.AddOrUpdate(point, panel);

            GD.Print($"[MapFixProbe] before close: valid={GodotObject.IsInstanceValid(panel)} visible={panel.Visible} parent={panel.GetParent() == point}");
            screen.Close(false);
            GD.Print($"[MapFixProbe] after close: valid={GodotObject.IsInstanceValid(panel)} visible={panel.Visible} queued={panel.IsQueuedForDeletion()} parent={panel.GetParent() == point}");
            if (!panel.Visible && (panel.IsQueuedForDeletion() || !GodotObject.IsInstanceValid(panel)))
                GD.Print("[MapFixProbe] PASS");
            else
                GD.Print("[MapFixProbe] FAIL panel still visible");
        }
        catch (Exception ex)
        {
            Fail(ex.ToString());
        }
        finally
        {
            if (panel != null && GodotObject.IsInstanceValid(panel) && !panel.IsQueuedForDeletion())
                panel.QueueFree();
            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Quit();
        }
    }

    private static void Fail(string message)
    {
        GD.Print($"[MapFixProbe] FAIL {message}");
    }
}
