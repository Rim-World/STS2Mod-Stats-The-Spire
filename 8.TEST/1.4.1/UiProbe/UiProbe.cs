using CommunityStats.Collection;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.UiProbe;

/// <summary>Test-only probe for contribution/settings panel construction.</summary>
[ModInitializer("Initialize")]
public static class UiProbe
{
    private static int _frames;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_frames == 600) Probe(tree);
        };
    }

    private static void Probe(SceneTree tree)
    {
        Safe.Run(() =>
        {
            var sample = new Dictionary<string, ContributionAccum>
            {
                ["STRIKE_SILENT"] = new ContributionAccum
                {
                    SourceId = "STRIKE_SILENT",
                    SourceType = "card",
                    TimesPlayed = 1,
                    DirectDamage = 10,
                },
            };

            var panel = ContributionPanel.Instance;
            if (panel.GetParent() == null) tree.Root.AddChild(panel);
            CommunityStats.Config.ModConfig.Toggles.ContributionPanel = true;
            ContributionPanel.ShowCombatResult(sample);
            Safe.Info($"[UiProbe] contribution visible={ContributionPanel.IsVisible} children={panel.GetChildCount()}");

            ContributionPanel.Hide();
            Safe.Info($"[UiProbe] contribution hidden={!ContributionPanel.IsVisible}");

            var filter = FilterPanel.Instance;
            if (filter.GetParent() == null) tree.Root.AddChild(filter);
            FilterPanel.Toggle();
            Safe.Info($"[UiProbe] filter visible={filter.Visible} children={filter.GetChildCount()}");
            filter.Visible = false;

            var career = CareerStatsSection.Create("FOOL");
            tree.Root.AddChild(career);
            var dropdowns = career.FindChildren("*", "OptionButton", true, false);
            int dropdownIndex = 0;
            foreach (var node in dropdowns)
            {
                if (node is not OptionButton option) continue;
                var items = new List<string>();
                for (int i = 0; i < option.ItemCount; i++)
                    items.Add($"{option.GetItemText(i)}={option.GetItemId(i)}");
                Safe.Info($"[UiProbe] career dropdown[{dropdownIndex}]={string.Join("|", items)}");
                dropdownIndex++;
            }
            Safe.Info($"[UiProbe] career fool section children={career.GetChildCount()}");
        });
    }
}
