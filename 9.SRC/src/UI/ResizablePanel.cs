using CommunityStats.Config;
using CommunityStats.Util;
using Godot;

namespace CommunityStats.UI;

/// <summary>
/// Adds a bottom-right drag handle to a panel and persists its size.
/// The handle is a small Label; dragging it changes panel.Size, clamped to
/// the viewport and the supplied minimum size.
/// </summary>
public static class ResizablePanel
{
    private static bool _resizing;
    private static Vector2 _startMouse;
    private static Vector2 _startSize;
    private static Control? _target;

    public static void Attach(Control panel, Control handle, Vector2 minimumSize)
    {
        handle.GuiInput += input => Safe.Run(() => HandleInput(panel, handle, input, minimumSize));
    }

    private static void HandleInput(Control panel, Control handle, InputEvent input, Vector2 minimumSize)
    {
        // Run-history replay windows are positioned by RunHistoryPatch.
        if (panel.HasMeta(ContributionPanel.ReplayWindowMeta)) return;

        if (input is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            if (button.Pressed)
            {
                _resizing = true;
                _target = panel;
                _startMouse = button.GlobalPosition;
                _startSize = panel.Size;
                panel.MoveToFront();
            }
            else if (_resizing && _target == panel)
            {
                _resizing = false;
                _target = null;
                ModConfig.PanelWidth = panel.Size.X;
                ModConfig.PanelHeight = panel.Size.Y;
                ModConfig.SaveSettings();
            }
            handle.AcceptEvent();
            return;
        }

        if (input is InputEventMouseMotion motion && _resizing && _target == panel)
        {
            var viewport = panel.GetViewportRect().Size;
            var maxSize = new Vector2(
                Mathf.Max(minimumSize.X, viewport.X - panel.GlobalPosition.X),
                Mathf.Max(minimumSize.Y, viewport.Y - panel.GlobalPosition.Y));
            var desired = _startSize + (motion.GlobalPosition - _startMouse);
            panel.Size = new Vector2(
                Mathf.Clamp(desired.X, minimumSize.X, maxSize.X),
                Mathf.Clamp(desired.Y, minimumSize.Y, maxSize.Y));
            handle.AcceptEvent();
        }
    }

    public static void RestoreSize(Control panel, Vector2 fallback, Vector2 minimumSize)
    {
        var size = ModConfig.PanelWidth.HasValue && ModConfig.PanelHeight.HasValue
            ? new Vector2(ModConfig.PanelWidth.Value, ModConfig.PanelHeight.Value)
            : fallback;

        var viewport = panel.GetViewportRect().Size;
        panel.Size = new Vector2(
            Mathf.Clamp(size.X, minimumSize.X, Mathf.Max(minimumSize.X, viewport.X)),
            Mathf.Clamp(size.Y, minimumSize.Y, Mathf.Max(minimumSize.Y, viewport.Y)));
    }
}
