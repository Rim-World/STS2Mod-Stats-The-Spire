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

namespace StatsTheSpire.LayoutProbe;

/// <summary>
/// Test-only probe. It calls the private RunHistoryPatch.OpenPopup with a
/// synthetic RunHistory and a real persisted run-summary identity. It checks
/// the side-by-side geometry, the removed entry button, and close behavior.
/// </summary>
[ModInitializer("Initialize")]
public static class RunHistoryLayoutProbe
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
            if (_ran || _frames < 240) return;
            _ran = true;
            try
            {
                _ = RunAsync(tree);
            }
            catch (Exception ex)
            {
                GD.Print($"[LayoutProbe] FATAL {ex}");
                tree.Quit(1);
            }
        };
    }

    private static async Task RunAsync(SceneTree tree)
    {
        var root = tree.Root;
        Node? popup = null;
        Control? left = null;
        Control? right = null;
        bool pass = true;

        try
        {
            // The headless display defaults to a square 1920x1920 viewport.
            // Use the real 16:9 game resolution so ratio checks match play.
            tree.Root.Size = new Vector2I(1920, 1080);

            var history = new RunHistory
            {
                Seed = "XSY4EYPRTTJW",
                StartTime = 1795349355,
                Win = true,
                Ascension = 10,
            };
            string historySource = "synthetic";
            try
            {
                var file = FindHistoryFile("1795349355.run");
                if (file != null)
                {
                    var loaded = JsonSerializationUtility.FromJson<RunHistory>(
                        File.ReadAllText(file));
                    if (loaded.Success && loaded.SaveData != null)
                    {
                        history = loaded.SaveData;
                        historySource = file;
                    }
                }
            }
            catch (Exception ex)
            {
                GD.Print($"[LayoutProbe] history-load fallback: {ex.Message}");
            }
            GD.Print($"[LayoutProbe] historySource={historySource} floors={history.MapPointHistory?.Sum(a => a?.Count ?? 0) ?? 0}");

            var open = typeof(RunHistoryPatch).GetMethod(
                "OpenPopup", BindingFlags.Static | BindingFlags.NonPublic);
            if (open == null)
            {
                GD.Print("[LayoutProbe] open-method FAIL");
                pass = false;
                return;
            }

            open.Invoke(null, new object[] { history });
            await Frames(tree, 2);
            popup = root.GetNodeOrNull("ModRunHistoryStatsPopup");
            left = root.GetNodeOrNull<Control>("ModRunHistoryStatsPopup/PopupPanel");
            right = root.GetNodeOrNull<Control>("StatsTheSpireContribution");

            bool popupOk = popup != null && left != null;
            bool rightOk = right != null && right.Visible;
            GD.Print($"[LayoutProbe] popup={popupOk} right={rightOk}");

            if (popupOk && rightOk)
            {
                var viewport = root.GetVisibleRect().Size;
                float gap = right!.GlobalPosition.X - (left!.GlobalPosition.X + left.Size.X);
                bool sideBySide = gap > 4f;
                bool topAligned = Mathf.Abs(left.GlobalPosition.Y - right.GlobalPosition.Y) <= 2f;
                bool sameHeight = Mathf.Abs(left.Size.Y - right.Size.Y) <= 2f;
                bool sameWidth = Mathf.Abs(left.Size.X - right.Size.X) <= 2f;
                float leftMargin = left.GlobalPosition.X;
                float rightMargin = viewport.X - (right.GlobalPosition.X + right.Size.X);
                bool symmetric = Mathf.Abs(leftMargin - rightMargin) <= 2f;
                float totalW = right.GlobalPosition.X + right.Size.X - left.GlobalPosition.X;
                float widthRatio = totalW / viewport.X;
                float heightRatio = left.Size.Y / viewport.Y;
                bool widthOk = widthRatio >= 0.65f && widthRatio <= 0.76f;
                bool heightOk = heightRatio >= 0.70f && heightRatio <= 0.88f;
                bool noButton = !FindButton(left, "查看贡献图表")
                             && !FindButton(right, "查看贡献图表");
                int titleCount = CountLabels(popup!, "本局统计");
                bool singleTitle = titleCount == 1;
                bool leftHasClose = FindButtonNode(left, "✕") != null;
                var rightClose = FindButtonNode(right, "✕");
                var rightHelp = FindButtonNode(right, "?");
                bool rightButtons = rightClose != null && rightHelp != null;
                bool buttonsSized = rightClose != null && rightHelp != null
                    && rightClose.CustomMinimumSize.X >= 40f
                    && rightClose.CustomMinimumSize.Y >= 40f
                    && rightHelp.CustomMinimumSize.X >= 40f
                    && rightHelp.CustomMinimumSize.Y >= 40f;
                bool buttonsStyled = rightClose != null && rightHelp != null
                    && rightClose.GetThemeStylebox("normal") is StyleBoxFlat closeStyle
                    && closeStyle.BorderWidthLeft >= 2
                    && rightHelp.GetThemeStylebox("normal") is StyleBoxFlat helpStyle
                    && helpStyle.BorderWidthLeft >= 2
                    && rightClose.GetThemeFontSize("font_size") >= 18;
                bool rightOnTop = right.GetIndex() > popup!.GetIndex();
                var leftScroll = FindFirst<ScrollContainer>(left);
                bool leftScrolls = leftScroll?.GetVScrollBar().Visible ?? false;
                float scrollMax = (float)(leftScroll?.GetVScrollBar().MaxValue ?? 0.0);
                float scrollPage = (float)(leftScroll?.GetVScrollBar().Page ?? 0.0);

                GD.Print(
                    $"[LayoutProbe] viewport={viewport} leftPos={left.GlobalPosition} leftSize={left.Size} " +
                    $"rightPos={right.GlobalPosition} rightSize={right.Size} gap={gap:F1} " +
                    $"margins={leftMargin:F1}/{rightMargin:F1}");
                GD.Print(
                    $"[LayoutProbe] sideBySide={sideBySide} topAligned={topAligned} sameHeight={sameHeight} " +
                    $"sameWidth={sameWidth} symmetric={symmetric} widthRatio={widthRatio:F3} " +
                    $"heightRatio={heightRatio:F3} noButton={noButton} titleCount={titleCount}");
                GD.Print(
                    $"[LayoutProbe] leftHasClose={leftHasClose} rightButtons={rightButtons} " +
                    $"buttonsSized={buttonsSized} buttonsStyled={buttonsStyled} rightOnTop={rightOnTop} " +
                    $"leftScrolls={leftScrolls} scrollMax={scrollMax:F1} scrollPage={scrollPage:F1}");

                pass &= sideBySide && topAligned && sameHeight && sameWidth && symmetric
                     && widthOk && heightOk && noButton && singleTitle && !leftHasClose
                     && rightButtons && buttonsSized && buttonsStyled
                     && rightOnTop && !leftScrolls;

                // The right close button must close both windows.
                if (rightClose == null)
                {
                    GD.Print("[LayoutProbe] rightClose button missing");
                    pass = false;
                }
                else
                {
                    rightClose.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(tree, 2);
                    bool rightClosed = !right!.Visible;
                    bool closePopupGone = root.GetNodeOrNull("ModRunHistoryStatsPopup") == null;
                    GD.Print($"[LayoutProbe] rightClose rightClosed={rightClosed} popupGone={closePopupGone}");
                    pass &= rightClosed && closePopupGone;
                }

                // Reopen, then close through the shared popup cleanup path.
                open.Invoke(null, new object[] { history });
                bool closed = RunHistoryPatch.CloseOpenPopup();
                bool popupGone = root.GetNodeOrNull("ModRunHistoryStatsPopup") == null;
                bool rightGone = !ContributionPanel.IsVisible;
                bool metaCleared = !ContributionPanel.Instance.HasMeta("sts_replay_window");
                bool anchorRestored = Mathf.Abs(ContributionPanel.Instance.AnchorTop - 0.25f) < 0.001f;
                bool liveHeightRestored = ContributionPanel.Instance.Size.Y < viewport.Y * 0.65f;
                GD.Print(
                    $"[LayoutProbe] close closed={closed} popupGone={popupGone} rightHidden={rightGone} " +
                    $"metaCleared={metaCleared} anchorRestored={anchorRestored} liveH={ContributionPanel.Instance.Size.Y:F1}");
                pass &= closed && popupGone && rightGone && metaCleared && anchorRestored && liveHeightRestored;

                // A run with no persisted summary must still open the right
                // window with its empty-state message.
                var emptyHistory = new RunHistory
                {
                    Seed = "NO_SUMMARY_PROBE_180",
                    StartTime = 1,
                };
                open.Invoke(null, new object[] { emptyHistory });
                await Frames(tree, 2);
                var emptyRight = root.GetNodeOrNull<Control>("StatsTheSpireContribution");
                bool emptyRightVisible = emptyRight != null && emptyRight.Visible;
                GD.Print($"[LayoutProbe] emptyReplay visible={emptyRightVisible}");
                pass &= emptyRightVisible;
            }
            else
            {
                pass = false;
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[LayoutProbe] FAIL {ex}");
            pass = false;
        }
        finally
        {
            SafeClose(root);
            GD.Print($"[LayoutProbe] RESULT {(pass ? "PASS" : "FAIL")}");
            tree.Quit(pass ? 0 : 1);
        }
    }

    private static void SafeClose(Node root)
    {
        try { RunHistoryPatch.CloseOpenPopup(); } catch { }
        var popup = root.GetNodeOrNull("ModRunHistoryStatsPopup");
        if (popup != null && GodotObject.IsInstanceValid(popup))
        {
            popup.QueueFree();
        }
        try { ContributionPanel.Hide(); } catch { }
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

    private static bool FindButton(Node node, string text)
    {
        if (node is Button button && button.Text.Contains(text, StringComparison.Ordinal))
            return true;
        foreach (var child in node.GetChildren())
            if (FindButton(child, text)) return true;
        return false;
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

    private static T? FindFirst<T>(Node node) where T : class
    {
        if (node is T match) return match;
        foreach (var child in node.GetChildren())
        {
            var found = FindFirst<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static int CountLabels(Node node, string text)
    {
        int count = node is Label label && label.Text == text ? 1 : 0;
        foreach (var child in node.GetChildren())
            count += CountLabels(child, text);
        return count;
    }
}
