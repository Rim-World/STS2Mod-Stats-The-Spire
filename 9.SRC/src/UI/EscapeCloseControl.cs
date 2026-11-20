using System;
using Godot;

namespace CommunityStats.UI;

/// <summary>
/// Small root-level input catcher for the run-history stats popup.
/// A Control's GuiInput does not receive keyboard events unless it owns
/// focus, so the old inline Esc branch was dead code. This node listens to
/// raw input, closes the popup, and consumes the event before the game's
/// screen stack treats Esc as "leave run history".
/// </summary>
public partial class EscapeCloseControl : Control
{
    public Action? EscapePressed;

    public override void _Ready()
    {
        SetProcessInput(true);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key) return;
        if (!key.Pressed || key.Echo || key.Keycode != Key.Escape) return;

        EscapePressed?.Invoke();
        GetViewport()?.SetInputAsHandled();
    }
}
