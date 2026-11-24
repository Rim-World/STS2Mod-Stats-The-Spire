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

namespace StatsTheSpire.ButtonProbe;

/// <summary>
/// Test-only probe. It exercises the same button factory used by
/// RunHistoryPatch.InjectButton, checks 390x75 / font 33 / (30,210), then
/// opens a real history popup through the shared OpenPopup path.
/// </summary>
[ModInitializer("Initialize")]
public static class ButtonProbe
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
                GD.Print($"[ButtonProbe] FATAL {ex}");
                tree.Quit(1);
            }
        };
    }

    private static async Task RunAsync(SceneTree tree)
    {
        bool pass = true;
        var root = tree.Root;
        Control? host = null;

        try
        {
            tree.Root.Size = new Vector2I(1920, 1080);

            var factory = typeof(RunHistoryPatch).GetMethod(
                "CreateEntryButton", BindingFlags.Static | BindingFlags.NonPublic);
            if (factory == null)
            {
                GD.Print("[ButtonProbe] factory FAIL");
                pass = false;
                return;
            }

            host = new Control
            {
                Name = "ButtonProbeHost",
                Size = new Vector2(1920, 1080),
            };
            root.AddChild(host);

            var button = (Button)factory.Invoke(null, null)!;
            host.AddChild(button);
            await Frames(tree, 2);

            var size = button.Size;
            var pos = button.GlobalPosition;
            int font = button.GetThemeFontSize("font_size");
            bool sizeOk = Mathf.Abs(size.X - 390f) <= 0.5f && Mathf.Abs(size.Y - 75f) <= 0.5f;
            bool posOk = Mathf.Abs(pos.X - 30f) <= 0.5f && Mathf.Abs(pos.Y - 210f) <= 0.5f;
            bool fontOk = font == 33;
            bool textOk = !string.IsNullOrWhiteSpace(button.Text) && button.Text.Contains("📊", StringComparison.Ordinal);
            GD.Print(
                $"[ButtonProbe] button pos={pos} size={size} font={font} text={button.Text} " +
                $"min={button.CustomMinimumSize} sizeOk={sizeOk} posOk={posOk} fontOk={fontOk} textOk={textOk}");
            pass &= sizeOk && posOk && fontOk && textOk;

            var history = LoadHistory();
            GD.Print($"[ButtonProbe] history seed={history.Seed} start={history.StartTime} floors={history.MapPointHistory?.Sum(a => a?.Count ?? 0) ?? 0}");
            var open = typeof(RunHistoryPatch).GetMethod(
                "OpenPopup", BindingFlags.Static | BindingFlags.NonPublic);
            if (open == null)
            {
                GD.Print("[ButtonProbe] open-method FAIL");
                pass = false;
                return;
            }

            open.Invoke(null, new object[] { history });
            await Frames(tree, 2);
            var popup = root.GetNodeOrNull("ModRunHistoryStatsPopup");
            var right = root.GetNodeOrNull<Control>("StatsTheSpireContribution");
            bool popupOk = popup != null;
            bool rightOk = right != null && right.Visible;
            GD.Print($"[ButtonProbe] click popup={popupOk} right={rightOk}");
            pass &= popupOk && rightOk;
        }
        catch (Exception ex)
        {
            GD.Print($"[ButtonProbe] FAIL {ex}");
            pass = false;
        }
        finally
        {
            try { RunHistoryPatch.CloseOpenPopup(); } catch { }
            try { ContributionPanel.Hide(); } catch { }
            if (host != null && GodotObject.IsInstanceValid(host)) host.QueueFree();
            GD.Print($"[ButtonProbe] RESULT {(pass ? "PASS" : "FAIL")}");
            tree.Quit(pass ? 0 : 1);
        }
    }

    private static RunHistory LoadHistory()
    {
        var file = FindHistoryFile("1795349355.run");
        if (file == null)
        {
            return new RunHistory { Seed = "PROBE", StartTime = 1, Win = true };
        }
        var loaded = JsonSerializationUtility.FromJson<RunHistory>(File.ReadAllText(file));
        return loaded.Success && loaded.SaveData != null
            ? loaded.SaveData
            : new RunHistory { Seed = "PROBE", StartTime = 1, Win = true };
    }

    private static string? FindHistoryFile(string fileName)
    {
        try
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "SlayTheSpire2");
            if (!Directory.Exists(path)) return null;
            return Directory.EnumerateFiles(path, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static async Task Frames(SceneTree tree, int count)
    {
        for (int i = 0; i < count; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
