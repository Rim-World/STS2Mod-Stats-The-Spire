using System.Linq;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;

namespace CommunityStats.UI;

/// <summary>
/// Compact corner indicator showing current potion drop odds.
/// Hovering expands an InfoModPanel with cumulative N-combat odds. PRD 3.9.
/// </summary>
public partial class PotionOddsIndicator : Control
{
    private static readonly Color GoldColor = new("#EFC851");
    private static readonly Color CreamColor = new("#FFF6E2");

    private Label _percentLabel = null!;
    private InfoModPanel? _hoverPanel;
    private float _currentOdds;
    private bool _forced;

    public static PotionOddsIndicator Create()
    {
        var node = new PotionOddsIndicator
        {
            Name = "StatsTheSpirePotionOdds",
            MouseFilter = MouseFilterEnum.Stop,
            // Round 9 round 2: enlarged to match native top-bar buttons
            // (~56×56 icon + 18 px text + spacing → ~120×64 total).
            CustomMinimumSize = new Vector2(120, 64),
        };
        node.BuildUi();
        return node;
    }

    private void BuildUi()
    {
        // Round 9: HBox layout — icon on the left, percentage label to the
        // right (user feedback: previously percent was below the icon).
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        hbox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddChild(hbox);

        // Round 9: Use AttackPotion icon (was Distilled Chaos).
        Control iconNode;
        var icon = LoadNativeTexture(
            "atlases/potion_atlas.sprites/attack_potion.tres",
            "atlases/potion_atlas.sprites/attack_potion.png");
        if (icon != null)
        {
            iconNode = new TextureRect
            {
                Texture = icon,
                // Round 9 round 2: 40 → 56 to match native button size.
                CustomMinimumSize = new Vector2(56, 56),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        }
        else
        {
            var lbl = new Label { Text = "🧪" };
            lbl.AddThemeFontSizeOverride("font_size", 36);
            lbl.AddThemeColorOverride("font_color", GoldColor);
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            iconNode = lbl;
        }
        hbox.AddChild(iconNode);

        _percentLabel = new Label { Text = "—" };
        // Round 9 round 2: 12 → 18 to match native top-bar text scale.
        _percentLabel.AddThemeFontSizeOverride("font_size", 18);
        _percentLabel.AddThemeColorOverride("font_color", CreamColor);
        _percentLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _percentLabel.VerticalAlignment = VerticalAlignment.Center;
        _percentLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        hbox.AddChild(_percentLabel);

        MouseEntered += ShowHoverPanel;
        MouseExited += HideHoverPanel;
    }

    private static Texture2D? LoadNativeTexture(params string[] candidates)
    {
        foreach (var path in candidates)
        {
            try
            {
                var resolved = MegaCrit.Sts2.Core.Helpers.ImageHelper.GetImagePath(path);
                if (Godot.ResourceLoader.Exists(resolved))
                    return Godot.ResourceLoader.Load<Texture2D>(resolved, null, ResourceLoader.CacheMode.Reuse);
            }
            catch { }
        }
        // Final fallback: try resources cached by PreloadManager.
        try
        {
            // Round 9: AttackPotion (was DISTILLED_CHAOS).
            var attack = MegaCrit.Sts2.Core.Models.ModelDb.AllPotions
                .FirstOrDefault(p => p.Id.Entry == "ATTACK_POTION");
            return attack?.Image;
        }
        catch { return null; }
    }

    public void UpdateOdds(float currentValue, bool forced = false)
    {
        // Keep the raw value. A negative pity value is meaningful: the game
        // does not clamp it, and an elite bonus can bring it above zero.
        _currentOdds = currentValue;
        _forced = forced;
        float shown = forced ? 1f : Mathf.Clamp(currentValue, 0f, 1f);
        // PRD AC-15: percentages displayed with 1 decimal place.
        _percentLabel.Text = (shown * 100f).ToString("F1") + "%";
    }

    private void ShowHoverPanel()
    {
        if (_hoverPanel != null) return;
        if (RunContextInfo.IsBlitz()) return;

        _hoverPanel = InfoModPanel.Create(L.Get("potion.title"), L.Get("potion.subtitle"));
        _hoverPanel.AddSeparator();

        // PotionRewardOdds.Roll computes current + eliteBonus * 0.5.
        // The addition uses the raw value, so -10% + 12.5% = 2.5%.
        float eliteOdds = _forced ? 1f : Mathf.Clamp(_currentOdds + 0.125f, 0f, 1f);
        _hoverPanel.AddRow(L.Get("potion.elite"),
            (eliteOdds * 100f).ToString("F1") + "%");

        AddChild(_hoverPanel);
        _hoverPanel.ZIndex = 500;
        _hoverPanel.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y + 4f);
    }

    private void HideHoverPanel()
    {
        if (_hoverPanel == null) return;
        _hoverPanel.QueueFree();
        _hoverPanel = null;
    }
}
