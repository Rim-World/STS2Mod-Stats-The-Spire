using System.Collections.Generic;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Runs;

namespace CommunityStats.Patches;

/// <summary>
/// Round 9 round 51 (rev 2): button + root-parented overlay.
///
/// 1.8.0: clicking 本局统计 now opens two windows side by side. The left
/// window is the single-run statistics popup; the right window is the
/// contribution replay panel. The old 查看贡献图表 button is gone.
///
/// Both windows are root-parented. The history overlay owns the dim layer
/// and the left window. CloseOpenPopup() also hides the replay window.
/// </summary>
[HarmonyPatch]
public static class RunHistoryPatch
{
    private const string ButtonMeta  = "sts_run_hist_button";
    private const string PopupName   = "ModRunHistoryStatsPopup";
    private static bool _replayWindowOpen;

    [HarmonyPatch(typeof(NRunHistory), "SelectPlayer")]
    [HarmonyPostfix]
    public static void AfterSelectPlayer(NRunHistory __instance)
    {
        // Round 9 round 51 (rev 3): free any leftover popup before re-injecting
        // the button. The overlay is parented to GetTree().Root so it survives
        // across scene transitions — if the user opened it once and navigated
        // away without closing, the dim layer was still blocking input on
        // the next visit. SelectPlayer fires on every screen entry and on
        // player switches, so freeing here is the right cleanup point.
        Safe.Run(() => CloseOpenPopup());
        Safe.Run(() => InjectButton(__instance));
    }

    /// <summary>
    /// The popup is parented to the scene root, so it survives the run-history
    /// screen being hidden. Close it when the submenu leaves, otherwise the
    /// full-screen dim layer blocks the main menu on the next visit.
    /// </summary>
    [HarmonyPatch(typeof(NRunHistory), "OnSubmenuHidden")]
    [HarmonyPostfix]
    public static void AfterSubmenuHidden()
    {
        Safe.Run(() => CloseOpenPopup());
    }

    // Round 9 round 51 (rev 4): removed the _ExitTree postfix — NRunHistory
    // does not override _ExitTree, so the Harmony patch attribute couldn't
    // resolve the method and broke ALL patches in this class (including
    // SelectPlayer), which is why the button stopped appearing.
    // The CloseOpenPopup at the start of AfterSelectPlayer is enough cleanup:
    // any leftover overlay from a previous visit gets freed when the user
    // re-enters the run history screen.

    private static void InjectButton(NRunHistory screen)
    {
        RunHistory? history;
        Control? screenContents;
        try
        {
            history = Traverse.Create(screen).Field("_history").GetValue<RunHistory>();
            screenContents = Traverse.Create(screen).Field("_screenContents").GetValue<Control>();
        }
        catch (System.Exception ex)
        {
            Safe.Warn($"RunHistoryPatch: failed to read NRunHistory fields: {ex.Message}");
            return;
        }
        if (history == null || screenContents == null) return;

        // Diagnostic: dump the root child list so we can see exactly what's
        // alive when the user enters the run history screen.
        try
        {
            var tree = (Godot.Engine.GetMainLoop() as SceneTree);
            var root = tree?.Root;
            if (root != null)
            {
                int n = root.GetChildCount();
                Safe.Info($"[RunHistoryPatch] InjectButton: root has {n} children");
                for (int i = 0; i < n; i++)
                {
                    var c = root.GetChild(i);
                    Safe.Info($"  root[{i}] = {c.GetType().Name} \"{c.Name}\" Visible={(c is Control ctrl ? ctrl.Visible.ToString() : "n/a")}");
                }
            }
            Safe.Info($"[RunHistoryPatch] _screenContents={screenContents.GetType().Name} pos={screenContents.Position} size={screenContents.Size}");
        }
        catch (System.Exception ex) { Safe.Warn($"diagnostic dump failed: {ex.Message}"); }

        // Round 9 round 51 (rev 5): button now lives on `screen` (NRunHistory)
        // not `screenContents` (a MarginContainer). MarginContainer is a
        // LAYOUT container — it ignores child anchors and stretches its lone
        // child to fill the entire content area. That's why the previous
        // version made the button balloon to ~1920×1080 with its default
        // background filling the screen as a dim "overlay".
        var button = FindChildByMeta(screen, ButtonMeta) as Button;
        if (button == null)
        {
            button = new Button
            {
                Name = "ModRunHistoryStatsButton",
                Text = "📊  " + L.Get("run_hist_btn"),
                MouseFilter = Control.MouseFilterEnum.Stop,
                // Round 9 round 51 (rev 3): no keyboard focus — prevent any
                // accidental Space/Enter activation while navigating the
                // surrounding native widgets.
                FocusMode = Control.FocusModeEnum.None,
            };
            button.SetMeta(ButtonMeta, true);
            button.AddThemeFontSizeOverride("font_size", 22);

            // Round 9 round 52: moved from top-right to below the player
            // icon container (top-left area). Anchors top-left with fixed
            // offsets that match the layout of the native player icon row.
            button.AnchorLeft   = 0.0f;
            button.AnchorRight  = 0.0f;
            button.AnchorTop    = 0.0f;
            button.AnchorBottom = 0.0f;
            button.OffsetLeft   = 30f;
            button.OffsetRight  = 290f;
            button.OffsetTop    = 210f;
            button.OffsetBottom = 260f;

            screen.AddChild(button);
        }

        // Drop any previous Pressed handler so we don't stack across SelectPlayer.
        try
        {
            var connections = button.GetSignalConnectionList(Button.SignalName.Pressed);
            foreach (var conn in connections)
            {
                var callable = conn["callable"].AsCallable();
                button.Disconnect(Button.SignalName.Pressed, callable);
            }
        }
        catch { }

        var capturedHistory = history;
        button.Pressed += () => Safe.Run(() => OpenPopup(capturedHistory));
    }

    /// <summary>
    /// Free any open run-history-stats popup. Called by both the popup's own
    /// close handlers and by RunHistoryStatsSection before it opens the
    /// contribution panel (so the contribution panel pops onto a clean
    /// background instead of fighting for z-order with us).
    /// </summary>
    public static bool CloseOpenPopup()
    {
        if (_replayWindowOpen)
        {
            _replayWindowOpen = false;
            if (ContributionPanel.IsVisible) ContributionPanel.Hide();
        }

        var tree = (Godot.Engine.GetMainLoop() as SceneTree);
        var root = tree?.Root;
        if (root == null) return false;
        int freed = 0;
        for (int i = root.GetChildCount() - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            // StringName / string equality is unreliable across Godot
            // marshalling — compare via ToString() instead.
            if (c.Name.ToString() == PopupName)
            {
                if (c is Control ctrl) ctrl.Visible = false;
                root.RemoveChild(c);
                c.QueueFree();
                freed++;
            }
        }
        if (freed > 0) Safe.Info($"[RunHistoryPatch] CloseOpenPopup freed {freed} overlay(s)");
        return freed > 0;
    }

    private static void OpenPopup(RunHistory history)
    {
        Safe.Info("[RunHistoryPatch] OpenPopup called (button click)");
        var tree = (Godot.Engine.GetMainLoop() as SceneTree);
        var root = tree?.Root;
        if (root == null) return;

        // Free any previous popup so we never stack two.
        CloseOpenPopup();

        var viewport = root.GetVisibleRect().Size;
        ComputeWindowRects(viewport, out var leftRect, out var rightRect);

        // Top-level Control overlay parented to root. Must explicitly fill
        // the viewport via anchors since root has no layout enforcement.
        var overlay = new Control
        {
            Name = PopupName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // Dim background — clicking it closes both windows.
        var dim = new ColorRect
        {
            Name = "Dim",
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(dim);
        dim.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                CloseOpenPopup();
            }
        };

        // Left window: single-run statistics.
        var panel = new PanelContainer
        {
            Name = "PopupPanel",
            CustomMinimumSize = new Vector2(420, 480),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.11f, 0.98f),
            BorderColor = new Color(0.55f, 0.70f, 0.95f, 0.7f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        panel.AddThemeStyleboxOverride("panel", bgStyle);
        panel.AnchorLeft = 0f; panel.AnchorRight = 0f;
        panel.AnchorTop = 0f; panel.AnchorBottom = 0f;
        panel.OffsetLeft = leftRect.Position.X;
        panel.OffsetRight = leftRect.Position.X + leftRect.Size.X;
        panel.OffsetTop = leftRect.Position.Y;
        panel.OffsetBottom = leftRect.Position.Y + leftRect.Size.Y;
        overlay.AddChild(panel);

        var body = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);

        // Title bar with close button.
        var titleRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        titleRow.AddThemeConstantOverride("separation", 8);
        var titleLbl = new Label { Text = L.Get("run_hist_popup_title") };
        titleLbl.AddThemeFontSizeOverride("font_size", 28);
        titleLbl.AddThemeColorOverride("font_color", new Color("#EFC851"));
        titleLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(titleLbl);

        var closeBtn = new Button
        {
            Text = "✕",
            CustomMinimumSize = new Vector2(40, 40),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        closeBtn.AddThemeFontSizeOverride("font_size", 22);
        closeBtn.Pressed += () => CloseOpenPopup();
        titleRow.AddChild(closeBtn);
        body.AddChild(titleRow);

        // Scrollable content area.
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        body.AddChild(scroll);

        var section = RunHistoryStatsSection.Create(history);
        section.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        section.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        scroll.AddChild(section);

        // Esc must close only this popup, not pop the run-history screen.
        // A focusless Control with GuiInput cannot receive keys, so use a
        // raw-input node and consume the event there.
        var escHandler = new EscapeCloseControl
        {
            Name = "EscCloseHandler",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        escHandler.EscapePressed = () => CloseOpenPopup();
        overlay.AddChild(escHandler);

        root.AddChild(overlay);

        // Right window: contribution replay. It is root-level, so open it
        // after the overlay and move it to the front. CloseOpenPopup() owns
        // its lifetime.
        var identity = RunIdentity.FromHistory(history);
        var summary = ContributionPersistence.LoadRunSummary(identity)
            ?? new Dictionary<string, ContributionAccum>();
        int totalTurns = CountCombatTurns(history);
        ContributionPanel.ShowRunReplay(
            summary, totalTurns, rightRect.Position, rightRect.Size);
        _replayWindowOpen = true;

        Safe.Info(
            $"[RunHistoryPatch] layout viewport={viewport} left={leftRect} right={rightRect} replay={_replayWindowOpen}");
    }

    /// <summary>
    /// Compute the two side-by-side window rectangles. The base layout is
    /// 570x900 + 16 gap + 660x900; both axes scale to keep a 48 px viewport
    /// margin, and the pair is centered. The windows never fill the screen.
    /// </summary>
    private static void ComputeWindowRects(
        Vector2 viewport,
        out Rect2 left,
        out Rect2 right)
    {
        const float baseLeft = 570f;
        const float baseRight = 660f;
        const float baseGap = 16f;
        const float baseHeight = 900f;

        float availableW = Mathf.Max(640f, viewport.X - 48f);
        float availableH = Mathf.Max(420f, viewport.Y - 48f);
        float widthScale = Mathf.Min(1f, availableW / (baseLeft + baseRight + baseGap));
        float heightScale = Mathf.Min(1f, availableH / baseHeight);

        float leftW = baseLeft * widthScale;
        float rightW = baseRight * widthScale;
        float gap = baseGap * widthScale;
        float height = baseHeight * heightScale;
        float totalW = leftW + gap + rightW;
        float x = (viewport.X - totalW) / 2f;
        float y = (viewport.Y - height) / 2f;

        left = new Rect2(x, y, leftW, height);
        right = new Rect2(x + leftW + gap, y, rightW, height);
    }

    private static int CountCombatTurns(RunHistory? history)
    {
        if (history?.MapPointHistory == null) return 0;
        int total = 0;
        foreach (var act in history.MapPointHistory)
        {
            if (act == null) continue;
            foreach (var floor in act)
            {
                if (floor?.Rooms == null) continue;
                foreach (var room in floor.Rooms)
                {
                    if (room != null && room.TurnsTaken > 0)
                        total += room.TurnsTaken;
                }
            }
        }
        return total;
    }

    private static Node? FindChildByMeta(Node parent, string meta)
    {
        for (int i = 0; i < parent.GetChildCount(); i++)
        {
            var c = parent.GetChild(i);
            if (c.HasMeta(meta)) return c;
        }
        return null;
    }
}
