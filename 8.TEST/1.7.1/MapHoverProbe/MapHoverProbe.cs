using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityStats.Config;
using CommunityStats.UI;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace StatsTheSpire.MapHoverProbe;

/// <summary>
/// Test-only probe for the 1.7.1 map hover cleanup. It uses the real viewport
/// mouse position and input queue, so it exercises the same mouse-enter and
/// mouse-exit path as a player.
/// </summary>
[ModInitializer("Initialize")]
public static class MapHoverProbe
{
    private static Harmony? _harmony;
    private static bool _started;
    private static NMapScreen? _screen;
    private static int _pass;
    private static int _fail;

    public static void Initialize()
    {
        GD.Print("[MapHoverProbe] initialized");
        _harmony = new Harmony("test.map_hover_probe_171");
        try
        {
            var gate = AccessTools.Method(typeof(NGame), "IsReleaseGame");
            if (gate != null)
            {
                _harmony.Patch(gate,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(MapHoverProbe), nameof(ForceReleaseFalse))));
                GD.Print("[MapHoverProbe] IsReleaseGame gate patched");
            }

            var open = AccessTools.Method(typeof(NMapScreen), nameof(NMapScreen.Open));
            if (open == null)
            {
                Fail("NMapScreen.Open not found");
                return;
            }
            _harmony.Patch(open,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(MapHoverProbe), nameof(AfterMapOpen))));
            GD.Print("[MapHoverProbe] NMapScreen.Open patched");
        }
        catch (Exception ex)
        {
            Fail($"harmony: {ex}");
        }
    }

    public static void ForceReleaseFalse(ref bool __result)
    {
        __result = false;
    }

    public static void AfterMapOpen(NMapScreen __instance)
    {
        if (_started) return;
        _started = true;
        _screen = __instance;
        GD.Print("[MapHoverProbe] map opened");
        Callable.From(() => Run(__instance)).CallDeferred();
    }

    private static async void Run(NMapScreen screen)
    {
        try
        {
            await Frames(2);
            NControllerManager.Instance?.ForceMouseMode();
            await Frames(2);
            var points = screen.GetNodeOrNull<Control>("TheMap/Points");
            if (points == null)
            {
                Fail("TheMap/Points not found");
                Finish();
                return;
            }

            var unknown = points.GetChildren().OfType<NMapPoint>()
                .Where(p => p.Point?.PointType == MapPointType.Unknown
                    && p.State != MapPointState.Traveled)
                .Take(2)
                .ToList();
            GD.Print($"[MapHoverProbe] unknown points={unknown.Count} toggle={ModConfig.Toggles.UnknownRoomOdds}");
            if (unknown.Count < 2)
            {
                Fail($"need 2 unknown points, got {unknown.Count}");
                Finish();
                return;
            }

            var first = PointCenter(unknown[0]);
            var second = PointCenter(unknown[1]);
            var empty = FindEmptyPosition(screen, points);
            DebugPoint("first", unknown[0]);
            DebugPoint("second", unknown[1]);

            await MoveTo(screen, first, "first");
            Check("first hover panel count", VisiblePanels(points), 1);
            DebugPanel("first-after", unknown[0]);

            // The first act view starts at the bottom of the map. Shift the
            // map down so the second unknown point is visible, then hover it.
            await ShiftMap(screen, 320f);
            second = PointCenter(unknown[1]);
            DebugPoint("second-visible", unknown[1]);
            await MoveTo(screen, second, "second");
            Check("second hover panel count", VisiblePanels(points), 1);
            Check("first panel removed", PanelsOn(unknown[0]), 0);

            empty = FindEmptyPosition(screen, points);
            await MoveTo(screen, empty, "empty");
            Check("after leave panel count", VisiblePanels(points), 0);
            DebugPanel("after-empty", unknown[1]);

            Finish();
        }
        catch (Exception ex)
        {
            Fail($"run: {ex}");
            Finish();
        }
    }

    private static async Task ShiftMap(NMapScreen screen, float dy)
    {
        var map = screen.GetNodeOrNull<Control>("TheMap");
        if (map == null) throw new InvalidOperationException("TheMap not found");
        var field = typeof(NMapScreen).GetField("_targetDragPos", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException("_targetDragPos not found");
        var current = (Vector2)field.GetValue(screen)!;
        var next = current + new Vector2(0f, dy);
        field.SetValue(screen, next);
        map.Position = next;
        await Frames(3);
    }

    private static async Task MoveTo(NMapScreen screen, Vector2 position, string label)
    {
        var viewport = screen.GetViewport();
        var windowPosition = viewport.GetScreenTransform() * position;
        viewport.WarpMouse(windowPosition);
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = windowPosition,
            GlobalPosition = windowPosition,
            Relative = Vector2.Zero,
        });
        await Frames(3);

        var actual = viewport.GetMousePosition();
        GD.Print($"[MapHoverProbe] {label}: target={position} window={windowPosition} actual={actual} viewportSize={viewport.GetVisibleRect().Size} nav={NControllerManager.Instance?.IsUsingDirectionalNavigation} panels={VisiblePanels(screen.GetNode<Control>("TheMap/Points"))}");
    }

    private static async Task Frames(int count)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) throw new InvalidOperationException("SceneTree missing");
        for (int i = 0; i < count; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void DebugPoint(string label, NMapPoint point)
    {
        var rect = point.GetGlobalRect();
        var panels = point.GetChildren().OfType<InfoModPanel>().ToList();
        GD.Print($"[MapHoverProbe] debug {label}: focus={point.HasFocus()} nav={NControllerManager.Instance?.IsUsingDirectionalNavigation} rect={rect} mouse={point.GetGlobalMousePosition()} panels={panels.Count} children=[{string.Join(",", panels.SelectMany(p => p.GetChildren().Select(c => c.Name)))}] visible=[{string.Join(",", panels.Select(p => p.Visible))}]");
    }

    private static void DebugPanel(string label, NMapPoint point)
    {
        var panel = point.GetChildren().OfType<InfoModPanel>().FirstOrDefault();
        if (panel == null)
        {
            GD.Print($"[MapHoverProbe] debug {label}: no panel");
            return;
        }
        var children = panel.GetChildren().Cast<Node>().ToList();
        GD.Print($"[MapHoverProbe] debug {label}: visible={panel.Visible} queued={panel.IsQueuedForDeletion()} panelMouse={panel.MouseFilter} panelChildren=[{string.Join(",", children.Select(c => $"{c.Name}:{c.GetType().Name}:proc={c.IsProcessing()}:can={c.CanProcess()}:mode={c.ProcessMode}"))}] mouse={point.GetGlobalMousePosition()} rect={point.GetGlobalRect()}");
    }

    private static Vector2 PointCenter(NMapPoint point)
    {
        var rect = point.GetGlobalRect();
        return rect.Position + rect.Size * 0.5f;
    }

    private static Vector2 FindEmptyPosition(NMapScreen screen, Control points)
    {
        var size = screen.GetViewportRect().Size;
        var rects = points.GetChildren().OfType<NMapPoint>()
            .Select(p => p.GetGlobalRect())
            .Where(r => r.Size.X > 0f && r.Size.Y > 0f)
            .ToList();

        var candidates = new List<Vector2>
        {
            new(80f, 80f),
            new(80f, size.Y - 80f),
            new(size.X - 80f, 80f),
            new(size.X - 80f, size.Y - 80f),
            new(size.X * 0.5f, size.Y - 80f),
        };

        foreach (var candidate in candidates)
            if (!rects.Any(r => r.HasPoint(candidate)))
                return candidate;

        throw new InvalidOperationException("no empty map position found");
    }

    private static int VisiblePanels(Control points)
        => points.GetChildren().OfType<NMapPoint>()
            .Sum(p => p.GetChildren().OfType<InfoModPanel>().Count(x => x.Visible));

    private static int PanelsOn(NMapPoint point)
        => point.GetChildren().OfType<InfoModPanel>().Count(x => x.Visible);

    private static void Check(string name, int actual, int expected)
    {
        if (actual == expected)
        {
            _pass++;
            GD.Print($"[MapHoverProbe] PASS {name}: {actual}");
        }
        else
        {
            _fail++;
            GD.Print($"[MapHoverProbe] FAIL {name}: expected {expected}, got {actual}");
        }
    }

    private static void Fail(string message)
    {
        _fail++;
        GD.Print($"[MapHoverProbe] FAIL {message}");
    }

    private static void Finish()
    {
        GD.Print($"[MapHoverProbe] summary pass={_pass} fail={_fail}");
        GD.Print(_fail == 0 ? "[MapHoverProbe] PASS" : "[MapHoverProbe] FAIL");
        var tree = Engine.GetMainLoop() as SceneTree;
        tree?.Quit();
    }
}
