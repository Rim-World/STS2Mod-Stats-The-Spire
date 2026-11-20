using System;
using System.Collections.Generic;
using System.Reflection;
using CommunityStats.Collection;
using CommunityStats.Patches;
using CommunityStats.UI;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.UiLifecycleProbe;

/// <summary>
/// Test-only probe for the root-level popup cleanup, Esc input catcher, and
/// replay-data clearing. It does not require a run.
/// </summary>
[ModInitializer("Initialize")]
public static class UiLifecycleProbe
{
    private static int _frames;
    private static bool _ran;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_ran || _frames < 300) return;
            _ran = true;
            Safe.Run(() => Run(tree));
        };
    }

    private static void Run(SceneTree tree)
    {
        bool pass = ProbePopupClose(tree);
        pass &= ProbeEscapeControl(tree);
        pass &= ProbeReplayClear(tree);
        GD.Print($"[UiLifecycleProbe] RESULT {(pass ? "PASS" : "FAIL")}");
        tree.Quit();
    }

    private static bool ProbePopupClose(SceneTree tree)
    {
        Control? fake = null;
        try
        {
            fake = new Control { Name = "ModRunHistoryStatsPopup" };
            tree.Root.AddChild(fake);
            bool present = tree.Root.GetNodeOrNull("ModRunHistoryStatsPopup") != null;
            bool closed = RunHistoryPatch.CloseOpenPopup();
            bool gone = tree.Root.GetNodeOrNull("ModRunHistoryStatsPopup") == null;
            bool pass = present && closed && gone;
            GD.Print($"[UiLifecycleProbe] popup present={present} closed={closed} gone={gone} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[UiLifecycleProbe] popup FAIL {ex}");
            return false;
        }
        finally
        {
            if (fake != null && GodotObject.IsInstanceValid(fake) && !fake.IsQueuedForDeletion())
                fake.QueueFree();
        }
    }

    private static bool ProbeEscapeControl(SceneTree tree)
    {
        EscapeCloseControl? control = null;
        try
        {
            bool fired = false;
            control = new EscapeCloseControl { Name = "EscProbe" };
            control.EscapePressed = () => fired = true;
            tree.Root.AddChild(control);
            control._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            bool pass = fired;
            GD.Print($"[UiLifecycleProbe] esc fired={fired} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[UiLifecycleProbe] esc FAIL {ex}");
            return false;
        }
        finally
        {
            if (control != null && GodotObject.IsInstanceValid(control) && !control.IsQueuedForDeletion())
                control.QueueFree();
        }
    }

    private static bool ProbeReplayClear(SceneTree tree)
    {
        ContributionPanel? panel = null;
        try
        {
            panel = ContributionPanel.Instance;
            if (panel.GetParent() == null) tree.Root.AddChild(panel);
            var replay = new Dictionary<string, ContributionAccum>
            {
                ["TEST_CARD"] = new ContributionAccum { SourceId = "TEST_CARD", SourceType = "card", DirectDamage = 7 },
            };
            ContributionPanel.ShowRunReplay(replay);
            bool visible = panel.Visible;
            ContributionPanel.Toggle();
            bool hidden = !panel.Visible;
            var field = typeof(ContributionPanel).GetField("_replayRunData", BindingFlags.Static | BindingFlags.NonPublic);
            bool cleared = field?.GetValue(null) == null;
            bool pass = visible && hidden && cleared;
            GD.Print($"[UiLifecycleProbe] replay visible={visible} hidden={hidden} cleared={cleared} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[UiLifecycleProbe] replay FAIL {ex}");
            return false;
        }
        finally
        {
            if (panel != null && GodotObject.IsInstanceValid(panel) && !panel.IsQueuedForDeletion())
                panel.QueueFree();
        }
    }
}
