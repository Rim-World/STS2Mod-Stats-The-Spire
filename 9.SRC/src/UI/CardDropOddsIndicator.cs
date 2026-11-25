using System;
using System.Collections.Generic;
using System.Linq;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CommunityStats.UI;

/// <summary>
/// Compact corner indicator for the next base card reward. Hovering expands
/// an InfoModPanel with the exact rare-count distribution for the next
/// regular or elite reward group. PRD RQ-031.
/// </summary>
public partial class CardDropOddsIndicator : Control
{
    private static readonly Color GoldColor = new("#EFC851");
    private static readonly Color AquaColor = new(0.16f, 0.92f, 0.75f);
    private static readonly Color CreamColor = new("#FFF6E2");

    private static readonly Color HeaderColor = new(0.62f, 0.62f, 0.72f);
    private const float NameColumnWidth = 100f;
    private const float ValueColumnWidth = 82f;

    // These two values are private inside CardRarityOdds. Keep the copies
    // together here; the pure calculator receives them as arguments.
    private const double RareOffsetReset = -0.05;
    private const double MaxRareOffset = 0.4;

    private InfoModPanel? _hoverPanel;
    private float _currentOffset = -0.05f;

    // STS2 base rates come from CardRarityOdds, because the Scarcity
    // ascension (A7+) changes both regular and elite values.

    public static CardDropOddsIndicator Create()
    {
        var node = new CardDropOddsIndicator
        {
            Name = "StatsTheSpireCardDropOdds",
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(72, 72),
        };
        node.BuildUi();
        return node;
    }

    private void BuildUi()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        vbox.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(vbox);

        // Native reward-screen "add a card" icon.
        Control iconNode;
        var icon = LoadNativeTexture(
            "ui/reward_screen/reward_icon_card.png",
            "ui/reward_screen/reward_icon_card.tres");
        if (icon != null)
        {
            iconNode = new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(62, 62),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        }
        else
        {
            var lbl = new Label { Text = "🃏" };
            lbl.AddThemeFontSizeOverride("font_size", 28);
            lbl.AddThemeColorOverride("font_color", GoldColor);
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            iconNode = lbl;
        }
        vbox.AddChild(iconNode);

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
        return null;
    }

    /// <summary>
    /// Update using the current pity offset (CardRarityOdds.CurrentValue).
    /// </summary>
    public void UpdateOffset(float offset)
    {
        _currentOffset = offset;
    }

    private void ShowHoverPanel()
    {
        if (_hoverPanel != null) return;
        if (RunContextInfo.IsBlitz()) return;

        try
        {
            BuildHoverPanel();
        }
        catch (Exception ex)
        {
            try
            {
                GD.PrintErr($"[StatsTheSpire][CardDrop] probability unavailable: {ex.GetType().Name}: {ex.Message}");
            }
            catch { }

            try
            {
                _hoverPanel?.QueueFree();
            }
            catch { }
            _hoverPanel = null;

            try
            {
                _hoverPanel = InfoModPanel.Create(L.Get("carddrop.title"), L.Get("carddrop.unavailable"));
                _hoverPanel.AddSeparator();
                _hoverPanel.AddLabel(L.Get("carddrop.unavailable"), HeaderColor, 11);
                AttachHoverPanel();
            }
            catch
            {
                _hoverPanel = null;
            }
        }
    }

    private void BuildHoverPanel()
    {
        var state = RunContextInfo.CurrentState;
        var player = RunContextInfo.GetLocalPlayer(state);
        var roomType = ResolveRoomType(state);
        bool hasBigGameHunter = RunContextInfo.HasBigGameHunter(state);
        bool bigGameHunterElite = roomType == RoomType.Elite && hasBigGameHunter;
        int groupSize = ResolveGroupSize(player, requireRarePower: bigGameHunterElite);

        _hoverPanel = InfoModPanel.Create(
            L.Get("carddrop.title"),
            L.Get(groupSize == 4 ? "carddrop.subtitle_4" : "carddrop.subtitle_3"));
        _hoverPanel.AddSeparator();

        // Boss rewards use the fixed boss table. The fourth card from
        // LastingCandy also uses the boss table, so every card stays rare.
        if (roomType == RoomType.Boss)
        {
            _hoverPanel.AddRow(
                string.Format(L.Get("carddrop.boss_group"), groupSize),
                "100.0%", GoldColor, GoldColor);
            AddExtraGroupsNote();
            AttachHoverPanel();
            return;
        }

        double regularRareBase = CardRarityOdds.RegularRareOdds;
        double regularUncommonBase = CardRarityOdds.regularUncommonOdds;
        double eliteRareBase = CardRarityOdds.EliteRareOdds;
        double eliteUncommonBase = CardRarityOdds.eliteUncommonOdds;
        double growth = ResolveGrowth(player);
        var allowed = ResolveAllowedRarities(player);

        var regularProfiles = BuildOffsetProfiles(
            regularRareBase, regularUncommonBase, growth, groupSize);
        var regular = CardRewardGroupOdds.Calculate(
            regularProfiles, _currentOffset, allowed, RareOffsetReset, MaxRareOffset);

        CardRewardGroupOddsResult elite;
        if (hasBigGameHunter)
        {
            // BigGameHunter mutates the shared CardCreationOptions to
            // Uniform + rare filter. That mutation is still present when
            // LastingCandy creates its fourth card, so all four are rare.
            var eliteProfiles = BuildBigGameHunterProfiles(groupSize);
            elite = CardRewardGroupOdds.Calculate(
                eliteProfiles, _currentOffset, allowed, RareOffsetReset, MaxRareOffset);
        }
        else
        {
            var eliteProfiles = BuildOffsetProfiles(
                eliteRareBase, eliteUncommonBase, growth, groupSize);
            elite = CardRewardGroupOdds.Calculate(
                eliteProfiles, _currentOffset, allowed, RareOffsetReset, MaxRareOffset);
        }

        AddHeaderRow();
        for (int rareCount = 0; rareCount <= groupSize; rareCount++)
        {
            var color = rareCount == 0 ? CreamColor : GoldColor;
            AddTableRow(
                string.Format(L.Get("carddrop.rare_count"), rareCount),
                regular.RareCountProbabilities[rareCount],
                elite.RareCountProbabilities[rareCount],
                color);
        }
        AddTableRow(
            L.Get("carddrop.at_least_one"),
            regular.AtLeastOneRare,
            elite.AtLeastOneRare,
            GoldColor);

        AddExtraGroupsNote();
        AttachHoverPanel();
    }

    private static List<CardRewardRollProfile> BuildOffsetProfiles(
        double baseRare, double baseUncommon, double growth, int groupSize)
    {
        var profiles = new List<CardRewardRollProfile>(groupSize);
        for (int i = 0; i < 3; i++)
            profiles.Add(CardRewardRollProfile.OffsetRoll(baseRare, baseUncommon, growth));
        if (groupSize == 4)
            profiles.Add(CardRewardRollProfile.BaseRoll(baseRare, baseUncommon));
        return profiles;
    }

    private static List<CardRewardRollProfile> BuildBigGameHunterProfiles(int groupSize)
    {
        var profiles = new List<CardRewardRollProfile>(groupSize);
        for (int i = 0; i < groupSize; i++)
            profiles.Add(CardRewardRollProfile.FixedRare());
        return profiles;
    }

    private int ResolveGroupSize(Player? player, bool requireRarePower)
    {
        if (player == null) return 3;

        try
        {
            var candy = player.GetRelic<LastingCandy>();
            if (candy == null) return 3;
            bool hasPower = HasPowerCard(player, requireRarePower);
            return CardRewardGroupOdds.WillLastingCandyAddFourthCard(
                candy.CombatRewardsSeen, hasLastingCandy: true, hasPowerCard: hasPower)
                ? 4
                : 3;
        }
        catch
        {
            return 3;
        }
    }

    private static bool HasPowerCard(Player player, bool requireRare)
    {
        try
        {
            var options = CardCreationOptions.ForRoom(player, RoomType.Monster);
            return options.GetPossibleCards(player)
                .Any(c => c.Type == CardType.Power && (!requireRare || c.Rarity == CardRarity.Rare));
        }
        catch
        {
            return false;
        }
    }

    private static double ResolveGrowth(Player? player)
    {
        try
        {
            if (player?.PlayerOdds?.CardRarity != null)
                return player.PlayerOdds.CardRarity.RarityGrowth;
        }
        catch { }
        return 0.01;
    }

    private static CardRewardRarityMask ResolveAllowedRarities(Player? player)
    {
        if (player == null) return CardRewardRarityMask.All;

        try
        {
            var options = CardCreationOptions.ForRoom(player, RoomType.Monster);
            CardRewardRarityMask mask = CardRewardRarityMask.None;
            foreach (var card in options.GetPossibleCards(player))
            {
                switch (card.Rarity)
                {
                    case CardRarity.Common:
                        mask |= CardRewardRarityMask.Common;
                        break;
                    case CardRarity.Uncommon:
                        mask |= CardRewardRarityMask.Uncommon;
                        break;
                    case CardRarity.Rare:
                        mask |= CardRewardRarityMask.Rare;
                        break;
                }
            }
            return mask == CardRewardRarityMask.None ? CardRewardRarityMask.All : mask;
        }
        catch
        {
            return CardRewardRarityMask.All;
        }
    }

    private static RoomType? ResolveRoomType(IRunState? state)
    {
        try
        {
            return state?.CurrentRoom?.RoomType;
        }
        catch
        {
            return null;
        }
    }

    private void AddExtraGroupsNote()
    {
        _hoverPanel!.AddLabel(L.Get("carddrop.extra_groups_note"), HeaderColor, 11);
    }

    private void AttachHoverPanel()
    {
        AddChild(_hoverPanel!);
        _hoverPanel!.ZIndex = 500;
        _hoverPanel.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y + 4f);
    }

    private void AddHeaderRow()
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);

        var spacer = new Label { Text = "" };
        spacer.AddThemeFontSizeOverride("font_size", 12);
        spacer.CustomMinimumSize = new Vector2(NameColumnWidth, 0);
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(spacer);

        var reg = new Label { Text = L.Get("carddrop.col_regular") };
        reg.AddThemeFontSizeOverride("font_size", 12);
        reg.AddThemeColorOverride("font_color", HeaderColor);
        reg.CustomMinimumSize = new Vector2(ValueColumnWidth, 0);
        reg.HorizontalAlignment = HorizontalAlignment.Right;
        hbox.AddChild(reg);

        var eli = new Label { Text = L.Get("carddrop.col_elite") };
        eli.AddThemeFontSizeOverride("font_size", 12);
        eli.AddThemeColorOverride("font_color", HeaderColor);
        eli.CustomMinimumSize = new Vector2(ValueColumnWidth, 0);
        eli.HorizontalAlignment = HorizontalAlignment.Right;
        hbox.AddChild(eli);

        _hoverPanel!.AddCustom(hbox);
    }

    private void AddTableRow(string label, double regular, double elite, Color color)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);

        var name = new Label { Text = label };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", color);
        name.CustomMinimumSize = new Vector2(NameColumnWidth, 0);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(name);

        var reg = new Label { Text = FormatPercent(regular) };
        reg.AddThemeFontSizeOverride("font_size", 12);
        reg.AddThemeColorOverride("font_color", color);
        reg.CustomMinimumSize = new Vector2(ValueColumnWidth, 0);
        reg.HorizontalAlignment = HorizontalAlignment.Right;
        hbox.AddChild(reg);

        var eli = new Label { Text = FormatPercent(elite) };
        eli.AddThemeFontSizeOverride("font_size", 12);
        eli.AddThemeColorOverride("font_color", color);
        eli.CustomMinimumSize = new Vector2(ValueColumnWidth, 0);
        eli.HorizontalAlignment = HorizontalAlignment.Right;
        hbox.AddChild(eli);

        _hoverPanel!.AddCustom(hbox);
    }

    private static string FormatPercent(double value)
        => (value * 100.0).ToString("F1") + "%";

    private void HideHoverPanel()
    {
        if (_hoverPanel == null) return;
        _hoverPanel.QueueFree();
        _hoverPanel = null;
    }
}
