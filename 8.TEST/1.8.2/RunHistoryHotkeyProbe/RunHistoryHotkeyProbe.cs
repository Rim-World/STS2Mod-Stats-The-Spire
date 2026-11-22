using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityStats.Patches;
using CommunityStats.UI;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace StatsTheSpire.HotkeyProbe;

/// <summary>
/// Test-only probe. Opens the real run-history dual window, invokes the exact
/// contribution-panel hotkey callback, and checks that replay state is
/// untouched. After closing the dual window, it verifies the live F8 path.
/// </summary>
[ModInitializer("Initialize")]
public static class RunHistoryHotkeyProbe
{
    private const string ReplayMeta = "sts_replay_window";
    private static int _frames;
    private static bool _ran;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_ran || _frames < 240) return;
            _ran = true;
            try
            {
                _ = RunAsync(tree);
            }
            catch (Exception ex)
            {
                GD.Print($"[HotkeyProbe] FATAL {ex}");
                tree.Quit(1);
            }
        };
    }

    private static async Task RunAsync(SceneTree tree)
    {
        var root = tree.Root;
        bool pass = true;

        try
        {
            tree.Root.Size = new Vector2I(1920, 1080);
            var history = LoadHistory();
            GD.Print($"[HotkeyProbe] seed={history.Seed} start={history.StartTime} " +
                     $"floors={history.MapPointHistory?.Sum(a => a?.Count ?? 0) ?? 0}");

            var open = typeof(RunHistoryPatch).GetMethod(
                "OpenPopup", BindingFlags.Static | BindingFlags.NonPublic);
            if (open == null)
            {
                GD.Print("[HotkeyProbe] open-method FAIL");
                pass = false;
                return;
            }

            open.Invoke(null, new object[] { history });
            await Frames(tree, 2);

            var popup = root.GetNodeOrNull("ModRunHistoryStatsPopup");
            var left = root.GetNodeOrNull<Control>("ModRunHistoryStatsPopup/PopupPanel");
            var right = root.GetNodeOrNull<Control>("StatsTheSpireContribution");
            bool opened = popup != null && left != null && right != null && right.Visible;
            bool metaBefore = ContributionPanel.Instance.HasMeta(ReplayMeta);
            var sizeBefore = right?.Size ?? Vector2.Zero;
            var posBefore = right?.GlobalPosition ?? Vector2.Zero;
            GD.Print($"[HotkeyProbe] opened={opened} meta={metaBefore} " +
                     $"size={sizeBefore} pos={posBefore}");
            pass &= opened && metaBefore;

            // This is the exact callback registered by HotkeyRegistration.
            ContributionPanel.Toggle();
            await Frames(tree, 2);

            bool popupAfter = root.GetNodeOrNull("ModRunHistoryStatsPopup") != null;
            bool leftAfter = left != null && left.Visible;
            bool rightAfter = right != null && right.Visible;
            bool metaAfter = ContributionPanel.Instance.HasMeta(ReplayMeta);
            var sizeAfter = right?.Size ?? Vector2.Zero;
            var posAfter = right?.GlobalPosition ?? Vector2.Zero;
            bool geometrySame = sizeAfter.DistanceTo(sizeBefore) < 0.01f
                             && posAfter.DistanceTo(posBefore) < 0.01f;
            GD.Print($"[HotkeyProbe] replayToggle popup={popupAfter} left={leftAfter} " +
                     $"right={rightAfter} meta={metaAfter} geometrySame={geometrySame}");
            pass &= popupAfter && leftAfter && rightAfter && metaAfter && geometrySame;

            // The right close button must still close both windows.
            var rightClose = right == null ? null : FindButtonNode(right, "✕");
            if (rightClose == null)
            {
                GD.Print("[HotkeyProbe] rightClose missing");
                pass = false;
            }
            else
            {
                rightClose.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(tree, 2);
                bool popupGone = root.GetNodeOrNull("ModRunHistoryStatsPopup") == null;
                bool rightGone = right != null && !right.Visible;
                bool metaCleared = !ContributionPanel.Instance.HasMeta(ReplayMeta);
                GD.Print($"[HotkeyProbe] rightClose popupGone={popupGone} " +
                         $"rightGone={rightGone} metaCleared={metaCleared}");
                pass &= popupGone && rightGone && metaCleared;
            }

            // Outside replay, the same callback must still toggle the live panel.
            ContributionPanel.Toggle();
            await Frames(tree, 2);
            bool liveOpened = ContributionPanel.IsVisible
                           && !ContributionPanel.Instance.HasMeta(ReplayMeta);
            GD.Print($"[HotkeyProbe] liveOpen visible={liveOpened}");
            pass &= liveOpened;

            ContributionPanel.Toggle();
            await Frames(tree, 1);
            bool liveClosed = !ContributionPanel.IsVisible
                           && !ContributionPanel.Instance.HasMeta(ReplayMeta);
            GD.Print($"[HotkeyProbe] liveClose hidden={liveClosed}");
            pass &= liveClosed;
        }
        catch (Exception ex)
        {
            GD.Print($"[HotkeyProbe] FAIL {ex}");
            pass = false;
        }
        finally
        {
            try { RunHistoryPatch.CloseOpenPopup(); } catch { }
            try { ContributionPanel.Hide(); } catch { }
            GD.Print($"[HotkeyProbe] RESULT {(pass ? "PASS" : "FAIL")}");
            tree.Quit(pass ? 0 : 1);
        }
    }

    private static RunHistory LoadHistory()
    {
        var file = FindHistoryFile("1795349355.run");
        if (file != null)
        {
            var loaded = JsonSerializationUtility.FromJson<RunHistory>(File.ReadAllText(file));
            if (loaded.Success && loaded.SaveData != null)
            {
                GD.Print($"[HotkeyProbe] historySource={file}");
                return loaded.SaveData;
            }
        }

        GD.Print("[HotkeyProbe] historySource=synthetic");
        return new RunHistory
        {
            Seed = "HOTKEY_PROBE_182",
            StartTime = 1795349355,
            Win = true,
            Ascension = 10,
        };
    }

    private static async Task Frames(SceneTree tree, int count)
    {
        for (int i = 0; i < count; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static string? FindHistoryFile(string fileName)
    {
        try
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var root = Path.Combine(appData, "SlayTheSpire2");
            if (!Directory.Exists(root)) return null;
            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static Button? FindButtonNode(Node node, string text)
    {
        if (node is Button button && button.Text.Contains(text, StringComparison.Ordinal))
            return button;
        foreach (var child in node.GetChildren())
        {
            var found = FindButtonNode(child, text);
            if (found != null) return found;
        }
        return null;
    }
}
