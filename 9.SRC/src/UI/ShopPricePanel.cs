using System;
using System.Linq;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CommunityStats.UI;

/// <summary>
/// Hover panel showing shop price ranges (PRD §3.16, manual feedback round 4).
///
/// Layout matches the sts1 InfoMod reference: a 4-column table where the
/// header row carries rarity columns (Common / Uncommon / Rare) and each
/// content row is a category (Relics / Cards / Potions). When the player
/// owns MembershipCard or TheCourier the displayed prices are *already*
/// discounted (per Q3c — show only the post-discount value, not both).
///
/// Card / relic / potion prices are the game's documented shop pool bands.
/// Removal cost is NOT hardcoded — we ask the game to compute it via a
/// throwaway <see cref="MerchantCardRemovalEntry"/> so the value tracks
/// Inflation ascension, future balance tweaks, and <c>CardShopRemovalsUsed</c>
/// automatically.
/// </summary>
public static class ShopPricePanel
{
    private static readonly Color GoldColor   = new("#EFC851");
    private static readonly Color AquaColor   = new(0.16f, 0.92f, 0.75f);
    private static readonly Color CreamColor  = new("#FFF6E2");
    private static readonly Color GrayColor   = new(0.62f, 0.62f, 0.72f);
    private static readonly Color RedColor    = new(0.94f, 0.42f, 0.42f);
    private static readonly Color BlueColor   = new(0.36f, 0.66f, 0.98f);
    private static readonly Color GreenColor  = new(0.45f, 0.88f, 0.50f);
    private static readonly Color DimColor    = new(0.45f, 0.45f, 0.50f);

    private const int LabelSize  = 13;
    private const int HeaderSize = 12;
    private const int ValueSize  = 12;

    public static InfoModPanel Create(Player? player = null, int cardRemovalsUsed = 0)
    {
        var panel = InfoModPanel.Create(L.Get("shop.title"), L.Get("shop.subtitle"));
        panel.AddSeparator();

        // Compute the multiplier from the player's active discount relics.
        decimal multiplier = ComputeDiscountMultiplier(player);
        AppendDiscountSummary(panel, player, multiplier);

        int removalCost = ComputeRemovalCost(player, cardRemovalsUsed, multiplier);
        int playerGold = TryGetGold(player);
        var removalColor = (playerGold >= 0 && playerGold < removalCost) ? DimColor : RedColor;
        panel.AddRow(string.Format(L.Get("shop.removal"), removalCost), "", removalColor, removalColor);

        panel.AddSeparator();

        // Build the table grid: header row + 3 category rows × 3 rarity columns.
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 4);
        panel.AddCustom(grid);

        // Header row
        AddCell(grid, "", GrayColor, HeaderSize);
        AddCell(grid, L.Get("shop.col_common"), GrayColor, HeaderSize);
        AddCell(grid, L.Get("shop.col_uncommon"), GrayColor, HeaderSize);
        AddCell(grid, L.Get("shop.col_rare"), GrayColor, HeaderSize);

        // Relics: 175 / 225 / 275, ±15% (v0.103.2 RelicModel.MerchantCost)
        AddCategoryRow(grid, L.Get("shop.relics"), GoldColor, multiplier,
            new[] { 175, 225, 275 }, 0.15f, playerGold);

        // Shop-rarity relics: 200, ±15%. Keep the three normal rarity
        // columns and append one extra row; the price occupies the first
        // value cell, with the remaining rarity cells left blank.
        int shopLow = DiscountedRangeLow(200, 0.15f, multiplier);
        var shopRelic = FormatRange(200, 0.15f, multiplier);
        var shopColor = (playerGold >= 0 && playerGold < shopLow) ? DimColor : GoldColor;
        AddCell(grid, L.Get("shop.relic_shop"), GoldColor, LabelSize);
        AddCell(grid, shopRelic, shopColor, ValueSize);
        AddCell(grid, "", GrayColor, ValueSize);
        AddCell(grid, "", GrayColor, ValueSize);

        // Cards: 50 / 75 / 150, ±5%
        AddCategoryRow(grid, L.Get("shop.cards"), BlueColor, multiplier,
            new[] { 50, 75, 150 }, 0.05f, playerGold);

        // Potions: 50 / 75 / 100, ±5%
        AddCategoryRow(grid, L.Get("shop.potions"), GreenColor, multiplier,
            new[] { 50, 75, 100 }, 0.05f, playerGold);

        panel.AddSeparator();
        panel.AddLabel(L.Get("shop.colorless_note"), GrayColor);

        return panel;
    }

    /// <summary>
    /// Walk the player's relics and combine all merchant-price discounts.
    /// MembershipCard yields ×0.5, TheCourier yields ×0.8 — both stack.
    /// Returns 1.0 when the player has neither (or when player is null).
    /// </summary>
    private static decimal ComputeDiscountMultiplier(Player? player)
    {
        if (player == null) return 1m;
        try
        {
            decimal m = 1m;
            foreach (var relic in player.Relics)
            {
                if (relic is MembershipCard) m *= 0.5m;
                else if (relic is TheCourier) m *= 0.8m;
            }
            return m;
        }
        catch { return 1m; }
    }

    private static void AppendDiscountSummary(InfoModPanel panel, Player? player, decimal multiplier)
    {
        if (player == null || multiplier >= 0.999m) return;
        try
        {
            var names = player.Relics
                .Where(r => r is MembershipCard || r is TheCourier)
                .Select(r => Util.NameLookup.Relic(r.Id.Entry))
                .ToList();
            if (names.Count == 0) return;
            panel.AddLabel(L.Get("shop.discount_active") + " " + string.Join(", ", names), GoldColor);
        }
        catch { /* swallow */ }
    }

    private static void AddCategoryRow(GridContainer grid, string label, Color labelColor,
        decimal multiplier, int[] basePrices, float jitter, int playerGold)
    {
        AddCell(grid, label, labelColor, LabelSize);
        for (int i = 0; i < basePrices.Length; i++)
        {
            int low = DiscountedRangeLow(basePrices[i], jitter, multiplier);
            // Gray out when the player can't even afford the cheapest variant.
            var cellColor = (playerGold >= 0 && playerGold < low) ? DimColor : labelColor;
            AddCell(grid, FormatRange(basePrices[i], jitter, multiplier), cellColor, ValueSize);
        }
    }

    /// <summary>
    /// The game rounds the base random merchant cost first, then applies
    /// relic discounts as decimal values and casts the result to int
    /// (truncation). Keep the same order.
    /// </summary>
    private static int DiscountedRangeLow(int basePrice, float jitter, decimal multiplier)
    {
        int raw = (int)Math.Round(basePrice * (1f - jitter));
        return (int)(raw * multiplier);
    }

    private static int DiscountedRangeHigh(int basePrice, float jitter, decimal multiplier)
    {
        int raw = (int)Math.Round(basePrice * (1f + jitter));
        return (int)(raw * multiplier);
    }

    private static string FormatRange(int basePrice, float jitter, decimal multiplier)
    {
        int low = DiscountedRangeLow(basePrice, jitter, multiplier);
        int high = DiscountedRangeHigh(basePrice, jitter, multiplier);
        return low == high ? low.ToString() : $"{low}-{high}";
    }

    /// <summary>
    /// Compute the card-removal cost from the game's live formula and apply
    /// our discount multiplier exactly once.
    ///
    /// v0.103.2 <see cref="MerchantCardRemovalEntry"/>:
    /// <code>
    /// private static int BaseCost =>
    ///     AscensionHelper.GetValueIfAscension(AscensionLevel.Inflation, 100, 75);
    /// public static int PriceIncrease =>
    ///     AscensionHelper.GetValueIfAscension(AscensionLevel.Inflation, 50, 25);
    /// public override void CalcCost() {
    ///     _cost = BaseCost + PriceIncrease * _player.ExtraFields.CardShopRemovalsUsed;
    /// }
    /// </code>
    ///
    /// We read BaseCost / PriceIncrease via Traverse so the panel automatically
    /// reflects Inflation (A15+) vs non-Inflation pricing.  Discount is then
    /// applied once — we never call the Cost getter, which would also apply
    /// ModifyMerchantPrice hooks and double-count the relic discount.</summary>
    private static int ComputeRemovalCost(Player? player, int cardRemovalsUsed, decimal multiplier)
    {
        _ = player;
        int baseCost = Traverse.Create(typeof(MerchantCardRemovalEntry))
            .Property("BaseCost").GetValue<int>();
        int priceIncrease = Traverse.Create(typeof(MerchantCardRemovalEntry))
            .Property("PriceIncrease").GetValue<int>();
        int rawCost = baseCost + priceIncrease * Math.Max(0, cardRemovalsUsed);
        return (int)(rawCost * multiplier);
    }

    private static int TryGetGold(Player? player)
    {
        if (player == null) return -1;
        try { return player.Gold; }
        catch { return -1; }
    }

    private static void AddCell(GridContainer grid, string text, Color color, int fontSize)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        grid.AddChild(lbl);
    }
}
