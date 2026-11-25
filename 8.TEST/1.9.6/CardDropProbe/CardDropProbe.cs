using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityStats.Config;
using CommunityStats.UI;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.CardDropProbe;

/// <summary>
/// Test-only probe for the 1.9.5 card reward group panel. It constructs the
/// real indicator on the main thread, opens the real hover panel through its
/// private entry point, and checks the rendered labels.
/// </summary>
[ModInitializer("Initialize")]
public static class CardDropProbe
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
            if (_ran || _frames < 180) return;
            _ran = true;
            try
            {
                _ = RunAsync(tree);
            }
            catch (Exception ex)
            {
                GD.Print($"[CardDropProbe] FATAL {ex}");
                tree.Quit(1);
            }
        };
    }

    private static async Task RunAsync(SceneTree tree)
    {
        bool pass = true;
        CardDropOddsIndicator? indicator = null;
        try
        {
            indicator = CardDropOddsIndicator.Create();
            tree.Root.AddChild(indicator);
            await Frames(tree, 2);

            var show = typeof(CardDropOddsIndicator).GetMethod(
                "ShowHoverPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            var hide = typeof(CardDropOddsIndicator).GetMethod(
                "HideHoverPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            var panelField = typeof(CardDropOddsIndicator).GetField(
                "_hoverPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            pass &= show != null && hide != null && panelField != null;

            if (show != null && panelField != null)
            {
                show.Invoke(indicator, null);
                await Frames(tree, 1);
                var panel = panelField.GetValue(indicator) as Control;
                var labels = CollectLabels(panel);
                GD.Print($"[CardDropProbe] language={L.Current} labels={labels.Count}");
                foreach (var text in labels)
                    GD.Print($"[CardDropProbe] label={text}");

                string title = L.Get("carddrop.title");
                string subtitle3 = L.Get("carddrop.subtitle_3");
                string row0 = string.Format(L.Get("carddrop.rare_count"), 0);
                string row3 = string.Format(L.Get("carddrop.rare_count"), 3);
                string atLeast = L.Get("carddrop.at_least_one");
                string note = L.Get("carddrop.extra_groups_note");

                bool initialOk = panel != null
                    && labels.Contains(title)
                    && labels.Contains(subtitle3)
                    && labels.Contains(row0)
                    && labels.Contains(row3)
                    && labels.Contains(atLeast)
                    && labels.Contains(note)
                    && !labels.Contains("Single-card rarity odds")
                    && !labels.Contains("单张卡牌各稀有度出率");
                GD.Print($"[CardDropProbe] initialPanel={initialOk}");
                pass &= initialOk;

                indicator.UpdateOffset(0.4f);
                hide!.Invoke(indicator, null);
                show.Invoke(indicator, null);
                await Frames(tree, 1);
                var panel2 = panelField.GetValue(indicator) as Control;
                var labels2 = CollectLabels(panel2);
                bool refreshed = panel2 != null
                    && labels2.Contains(row0)
                    && labels2.Contains(atLeast)
                    && !labels2.SequenceEqual(labels);
                GD.Print($"[CardDropProbe] refreshedPanel={refreshed}");
                pass &= refreshed;
            }

            GD.Print($"[CardDropProbe] RESULT {(pass ? "PASS" : "FAIL")}");
        }
        catch (Exception ex)
        {
            GD.Print($"[CardDropProbe] FAIL {ex}");
            pass = false;
        }
        finally
        {
            try { indicator?.QueueFree(); } catch { }
            tree.Quit(pass ? 0 : 1);
        }
    }

    private static List<string> CollectLabels(Control? root)
    {
        var result = new List<string>();
        if (root == null) return result;
        Collect(root, result);
        return result;
    }

    private static void Collect(Node node, List<string> result)
    {
        if (node is Label label && !string.IsNullOrEmpty(label.Text))
            result.Add(label.Text);
        foreach (var child in node.GetChildren())
            Collect(child, result);
    }

    private static async Task Frames(SceneTree tree, int count)
    {
        for (int i = 0; i < count; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
