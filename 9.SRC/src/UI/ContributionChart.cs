using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace CommunityStats.UI;

/// <summary>
/// Horizontal bar chart showing per-source (card/relic/potion) contributions.
/// Sections: Damage, Defense, Card Draw, Energy Gained, Healing summary.
/// Supports sub-bars for generated/transformed cards displayed under their origin.
/// </summary>
public partial class ContributionChart : VBoxContainer
{
    // ── Palette (cohesive cool-tone deck with warm accents) ─────
    private static readonly Color CardBarColor    = new(0.36f, 0.58f, 0.95f); // sky blue
    private static readonly Color RelicBarColor   = new(0.95f, 0.78f, 0.30f); // saturated gold
    private static readonly Color PotionBarColor  = new(0.25f, 0.85f, 0.65f); // teal
    private static readonly Color PetBarColor     = new(0.55f, 0.85f, 0.55f); // light green (Osty/Wuppy)
    private static readonly Color PetWasteBarColor = new(0.75f, 0.28f, 0.28f); // dark red (wasted pet HP)
    private static readonly Color AttrBarColor    = new(0.55f, 0.78f, 1.00f); // pale blue
    private static readonly Color ModifierBarColor= new(0.74f, 0.55f, 0.95f); // lavender
    private static readonly Color MitigateBarColor= new(0.40f, 0.82f, 0.55f); // emerald
    private static readonly Color StrReduceBarColor = new(0.95f, 0.55f, 0.25f); // orange
    private static readonly Color StarBarColor    = new(1.00f, 0.86f, 0.35f); // bright gold
    private static readonly Color SelfDmgBarColor = new(0.92f, 0.30f, 0.25f); // red
    private static readonly Color HealBarColor    = new(0.30f, 0.92f, 0.40f); // bright green
    private static readonly Color UpgradeBarColor = new(0.95f, 0.65f, 0.20f); // amber
    private static readonly Color SubBarColor     = new(0.42f, 0.62f, 0.82f, 0.65f); // dimmed blue
    private static readonly Color HeaderColor     = new("#EFC851"); // gold accent (default, overridden per section)
    // Per-section header colors
    private static readonly Color DamageHeaderColor  = new("#FF4444"); // red
    private static readonly Color DefenseHeaderColor = new("#44CC44"); // green
    private static readonly Color DrawHeaderColor    = new("#4488FF"); // blue
    private static readonly Color EnergyHeaderColor  = new("#FF8C00"); // orange
    private static readonly Color StarsHeaderColor   = new("#FFD700"); // gold
    private static readonly Color SectionTextColor= new("#FFF6E2"); // cream
    private static readonly Color PlayCountColor  = new(0.62f, 0.62f, 0.72f);
    private static readonly Color RowAlt          = new(1f, 1f, 1f, 0.025f);
    private static readonly Color BarTrackColor   = new(1f, 1f, 1f, 0.06f);
    private static readonly Color BarBorderColor  = new(0f, 0f, 0f, 0.35f);

    private const int MaxBarsPerSection = 10;
    private const float BarHeight = 22f;
    // Shrunk from 540 → 380 to fit the narrower 570-wide panel.
    private const float MaxBarWidth = 380f;
    private const int BarCornerRadius = 4;
    private static int _rowCounter; // alternating background rows

    public enum Category { Damage, Defense, Draw, Energy, Stars, Healing }

    /// <summary>
    /// Controls how a single row renders for entries that carry both a
    /// positive defensive contribution AND a cost (self-damage or wasted pet
    /// HP). In <see cref="Normal"/> mode the row can mix values; when split,
    /// the positive row uses <see cref="PositiveOnly"/>, followed by
    /// <see cref="NegativeOnly"/> (self-damage) or
    /// <see cref="NegativePetWasted"/> (pet-waste).
    /// </summary>
    private enum RowMode { Normal, PositiveOnly, NegativeOnly, NegativePetWasted }

    // Shared expand/collapse state across chart rebuilds, keyed by parent source id.
    // Default: expanded.
    private static readonly HashSet<string> _collapsedParents = new();

    /// <summary>
    /// Sum of all non-negative defense contributions for a single source —
    /// the value we want to show on a "positive row" when self-damage is
    /// split onto its own row.
    /// </summary>
    private static int PositiveDefense(ContributionAccum a)
        => a.EffectiveBlock + a.PetAbsorbed + a.ModifierBlock + a.MitigatedByDebuff
         + a.MitigatedByBuff + a.MitigatedByStrReduction + a.UpgradeBlock;

    private static bool IsNegativeRow(RowMode mode)
        => mode == RowMode.NegativeOnly || mode == RowMode.NegativePetWasted;

    public static ContributionChart Create(
        IReadOnlyDictionary<string, ContributionAccum> data,
        string title,
        bool isRunLevel = false)
    {
        var chart = new ContributionChart();
        chart.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        chart.AddThemeConstantOverride("separation", 4);

        // Title
        var titleLabel = new Label { Text = title };
        titleLabel.AddThemeColorOverride("font_color", HeaderColor);
        titleLabel.AddThemeFontSizeOverride("font_size", 16);
        chart.AddChild(titleLabel);

        chart.AddSeparator();
        chart.BuildSection(L.Get("chart.damage"), data, Category.Damage, DamageHeaderColor);
        chart.AddSeparator();
        chart.BuildSection(L.Get("chart.defense"), data, Category.Defense, DefenseHeaderColor);
        chart.AddSeparator();
        chart.BuildSection(L.Get("chart.draw"), data, Category.Draw, DrawHeaderColor);
        chart.AddSeparator();
        chart.BuildSection(L.Get("chart.energy"), data, Category.Energy, EnergyHeaderColor);
        chart.AddSeparator();
        chart.BuildSection(L.Get("chart.stars"), data, Category.Stars, StarsHeaderColor);

        // Healing section: only show in run-level summary
        if (isRunLevel)
        {
            chart.AddSeparator();
            chart.BuildSection(L.Get("chart.healing"), data, Category.Healing, HealBarColor);
        }

        return chart;
    }

    private void BuildSection(string header, IReadOnlyDictionary<string, ContributionAccum> data, Category category, Color? sectionColor = null)
    {
        var color = sectionColor ?? HeaderColor;
        var headerWrap = new PanelContainer();
        var headerStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.12f, 0.18f, 0.55f),
            BorderColor = color,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
        };
        headerWrap.AddThemeStyleboxOverride("panel", headerStyle);
        var headerLabel = new Label { Text = header };
        headerLabel.AddThemeColorOverride("font_color", color);
        headerLabel.AddThemeFontSizeOverride("font_size", 13);
        headerWrap.AddChild(headerLabel);
        AddChild(headerWrap);
        _rowCounter = 0;

        // Separate top-level entries and sub-entries (cards with OriginSourceId)
        var topLevel = new List<(ContributionAccum accum, int value)>();
        var subEntries = new Dictionary<string, List<(ContributionAccum accum, int value)>>();

        foreach (var accum in data.Values)
        {
            int value = category switch
            {
                Category.Damage => accum.TotalDamage,
                Category.Defense => accum.TotalDefense,
                Category.Draw => accum.CardsDrawn,
                Category.Energy => accum.EnergyGained,
                Category.Stars => accum.StarsContribution,
                Category.Healing => accum.HpHealed,
                _ => 0
            };

            if (accum.OriginSourceId != null)
            {
                if (value <= 0 && !(category == Category.Defense
                    && (accum.SelfDamage > 0 || accum.PetWasted > 0))) continue;
                if (!subEntries.TryGetValue(accum.OriginSourceId, out var list))
                {
                    list = new List<(ContributionAccum, int)>();
                    subEntries[accum.OriginSourceId] = list;
                }
                list.Add((accum, value));
            }
            else
            {
                // Keep entries with value > 0, or entries that might be parents (value == 0 checked later)
                topLevel.Add((accum, value));
            }
        }

        // Include parent entries that have sub-entries even if own value is 0
        // Remove top-level entries with 0 value AND no sub-entries
        // For Defense: keep entries with SelfDamage or PetWasted (negative value)
        topLevel.RemoveAll(x => x.value <= 0
            && !subEntries.ContainsKey(x.accum.SourceId)
            && !(category == Category.Defense
                && (x.accum.SelfDamage > 0 || x.accum.PetWasted > 0)));

        // Also add sub-entry values to their parent's total for sorting (use abs for negative defense)
        var sorted = topLevel
            .OrderByDescending(x => Math.Abs(x.value) + (subEntries.TryGetValue(x.accum.SourceId, out var subs)
                ? subs.Sum(s => Math.Abs(s.value)) : 0))
            .Take(MaxBarsPerSection)
            .ToList();

        // Also check if there are orphaned sub-entries whose parent is not in topLevel
        // (shouldn't happen normally, but handle gracefully)
        var topLevelIds = new HashSet<string>(sorted.Select(x => x.accum.SourceId));
        foreach (var (parentId, subs) in subEntries)
        {
            if (!topLevelIds.Contains(parentId) && data.TryGetValue(parentId, out var parentAccum))
            {
                sorted.Add((parentAccum, 0));
                topLevelIds.Add(parentId);
            }
        }

        if (sorted.Count == 0)
        {
            var none = new Label { Text = L.Get("chart.none") };
            none.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            none.AddThemeFontSizeOverride("font_size", 12);
            AddChild(none);
            return;
        }

        var maxVal = sorted.Max(x =>
        {
            int own = category == Category.Defense
                ? Math.Max(
                    Math.Max(Math.Abs(x.value), PositiveDefense(x.accum)),
                    Math.Max(x.accum.SelfDamage, x.accum.PetWasted))
                : Math.Abs(x.value);
            int subsMax = subEntries.TryGetValue(x.accum.SourceId, out var s)
                ? s.Sum(sub => category == Category.Defense
                    ? Math.Max(
                        Math.Max(Math.Abs(sub.value), PositiveDefense(sub.accum)),
                        Math.Max(sub.accum.SelfDamage, sub.accum.PetWasted))
                    : Math.Abs(sub.value))
                : 0;
            return own + subsMax;
        });

        // PRD §4.5 round 6 fix: defense % was using `TotalDefense` which already
        // subtracts self-damage, so an Offering self-hit could push the
        // denominator below the sum of positive contributions and produce
        // 125%-style nonsense. Compute the percentage denominator from the sum
        // of *positive* per-source contributions only — self-damage still
        // displays as a red bar but doesn't pollute the percentages.
        int totalVal;
        if (category == Category.Defense)
        {
            int posSum = 0;
            foreach (var a in data.Values)
            {
                int v = a.EffectiveBlock + a.PetAbsorbed + a.ModifierBlock + a.MitigatedByDebuff
                      + a.MitigatedByBuff + a.MitigatedByStrReduction + a.UpgradeBlock;
                if (v > 0) posSum += v;
            }
            totalVal = posSum;
        }
        else
        {
            totalVal = data.Values.Sum(a => category switch
            {
                Category.Damage => a.TotalDamage,
                Category.Draw => a.CardsDrawn,
                Category.Energy => a.EnergyGained,
                Category.Stars => a.StarsContribution,
                Category.Healing => a.HpHealed,
                _ => 0
            });
        }

        foreach (var (accum, value) in sorted)
        {
            bool hasSubs = subEntries.TryGetValue(accum.SourceId, out var subs);
            bool isDefense = category == Category.Defense;
            // Defense needs PositiveDefense because TotalDefense already
            // subtracts costs. Other sections must use their own value; using
            // PositiveDefense there would drop every damage/draw/energy row.
            bool hasPositive = isDefense ? PositiveDefense(accum) > 0 : value > 0;

            // Split both negative costs onto their own rows. A card can carry
            // real block and a pet-kill cost at the same time (BoneShards,
            // Sacrifice); the positive bar must not absorb the negative value.
            bool splitSelfDamage = isDefense
                                && accum.SelfDamage > 0
                                && hasPositive;
            bool splitPetWasted = isDefense
                               && accum.PetWasted > 0
                               && hasPositive;
            bool splitCosts = splitSelfDamage || splitPetWasted;

            if (hasPositive)
            {
                BuildEntryRow(accum, splitCosts ? PositiveDefense(accum) : value, maxVal, totalVal,
                    category, false, hasSubs, splitCosts ? RowMode.PositiveOnly : RowMode.Normal);
            }
            else if (hasSubs)
            {
                // Keep a zero-value parent row so its sub-bars can still be
                // collapsed/expanded.
                BuildEntryRow(accum, 0, maxVal, totalVal, category, false, true, RowMode.Normal);
            }

            // Render sub-bars if this entry has children AND not collapsed
            if (hasSubs && !_collapsedParents.Contains(accum.SourceId))
            {
                foreach (var (subAccum, subValue) in subs!.OrderByDescending(x => x.value))
                {
                    bool subPositive = isDefense ? PositiveDefense(subAccum) > 0 : subValue > 0;
                    bool subSplitCosts = isDefense
                        && (subAccum.SelfDamage > 0 || subAccum.PetWasted > 0)
                        && subPositive;

                    if (subPositive)
                    {
                        BuildEntryRow(subAccum, subSplitCosts ? PositiveDefense(subAccum) : subValue,
                            maxVal, totalVal, category, true, false,
                            subSplitCosts ? RowMode.PositiveOnly : RowMode.Normal);
                    }
                    if (isDefense && subAccum.SelfDamage > 0)
                    {
                        BuildEntryRow(subAccum, -subAccum.SelfDamage, maxVal, totalVal,
                            category, true, false, RowMode.NegativeOnly,
                            includeSourceName: !subPositive);
                    }
                    if (isDefense && subAccum.PetWasted > 0)
                    {
                        BuildEntryRow(subAccum, -subAccum.PetWasted, maxVal, totalVal,
                            category, true, false, RowMode.NegativePetWasted);
                    }
                }
            }

            // Emit cost rows only in Defense. Damage/draw/energy/stars/healing
            // must not inherit self-damage or pet-waste rows.
            if (isDefense && accum.SelfDamage > 0)
            {
                BuildEntryRow(accum, -accum.SelfDamage, maxVal, totalVal,
                    category, false, false, RowMode.NegativeOnly,
                    includeSourceName: !hasPositive && !hasSubs);
            }
            if (isDefense && accum.PetWasted > 0)
            {
                BuildEntryRow(accum, -accum.PetWasted, maxVal, totalVal,
                    category, false, false, RowMode.NegativePetWasted);
            }
        }
    }

    private void BuildEntryRow(ContributionAccum accum, int value, int maxVal, int totalVal,
        Category category, bool isSub, bool hasSubs = false, RowMode rowMode = RowMode.Normal,
        bool includeSourceName = false)
    {
        var rowWrap = new PanelContainer();
        var rowStyle = new StyleBoxFlat
        {
            BgColor = (_rowCounter++ % 2 == 0) ? RowAlt : new Color(0, 0, 0, 0),
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
        };
        rowWrap.AddThemeStyleboxOverride("panel", rowStyle);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        rowWrap.AddChild(row);

        // Expand/collapse toggle for parents that have sub-entries
        if (!isSub && hasSubs)
        {
            var isCollapsed = _collapsedParents.Contains(accum.SourceId);
            var toggle = new Button
            {
                Text = isCollapsed ? "▶" : "▼",
                CustomMinimumSize = new Vector2(18, BarHeight),
                Flat = true
            };
            toggle.AddThemeFontSizeOverride("font_size", 10);
            var sourceId = accum.SourceId;
            toggle.Pressed += () =>
            {
                if (_collapsedParents.Contains(sourceId))
                    _collapsedParents.Remove(sourceId);
                else
                    _collapsedParents.Add(sourceId);
                // Request parent panel to refresh — fire event on tracker
                try { Collection.CombatTracker.Instance.NotifyCombatDataUpdated(); } catch { }
            };
            row.AddChild(toggle);
        }
        else if (!isSub)
        {
            // Spacer so columns align
            row.AddChild(new Control { CustomMinimumSize = new Vector2(18, BarHeight) });
        }

        // Source name
        string prefix;
        if (IsNegativeRow(rowMode))
        {
            // Dedicated cost row — indent like a sub-entry so it visually
            // hangs off the positive parent row.
            prefix = "  \u2514 ";
        }
        else
        {
            prefix = isSub ? "  \u2514 " : accum.SourceType switch
            {
                "relic" => "[R] ",
                "potion" => "[P] ",
                "pet" or "osty" => L.Get("source.pet_prefix"),
                "rest" or "merchant" or "floor_regen" => "",
                "event" => L.Get("source.event_prefix"),
                _ => ""
            };
        }
        var displayName = GetLocalizedName(accum.SourceId, accum.SourceType);
        if (rowMode == RowMode.NegativeOnly)
        {
            var label = L.Get("chart.self_damage_suffix");
            displayName = includeSourceName ? $"{label} {displayName}" : label;
        }
        else if (rowMode == RowMode.NegativePetWasted)
            displayName = $"{L.Get("chart.pet_wasted_suffix")} {displayName}";
        bool indentLike = isSub || IsNegativeRow(rowMode);
        var nameLabel = new Label
        {
            Text = $"{prefix}{displayName}",
            CustomMinimumSize = new Vector2(indentLike ? 100 : 110, BarHeight)
        };
        nameLabel.AddThemeColorOverride("font_color",
            rowMode == RowMode.NegativePetWasted ? PetWasteBarColor :
            rowMode == RowMode.NegativeOnly ? SelfDmgBarColor :
            isSub ? new Color(0.7f, 0.7f, 0.8f) : new Color(0.9f, 0.9f, 0.9f));
        nameLabel.AddThemeFontSizeOverride("font_size", indentLike ? 11 : 12);
        nameLabel.ClipText = true;
        row.AddChild(nameLabel);

        // Times played (for cards) — omit on the dedicated self-damage row
        // to avoid a misleading double count (the play count already showed
        // on the positive row).
        if (accum.SourceType == "card" && accum.TimesPlayed > 0 && !IsNegativeRow(rowMode))
        {
            var playLabel = new Label
            {
                Text = $"x{accum.TimesPlayed}",
                CustomMinimumSize = new Vector2(30, BarHeight)
            };
            playLabel.AddThemeColorOverride("font_color", PlayCountColor);
            playLabel.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(playLabel);
        }
        else
        {
            row.AddChild(new Control { CustomMinimumSize = new Vector2(30, BarHeight) });
        }

        // Bar container with a subtle track background so empty bars are visible.
        var barContainer = new Panel { CustomMinimumSize = new Vector2(MaxBarWidth, BarHeight) };
        var trackStyle = new StyleBoxFlat
        {
            BgColor = BarTrackColor,
            CornerRadiusTopLeft = BarCornerRadius,
            CornerRadiusTopRight = BarCornerRadius,
            CornerRadiusBottomLeft = BarCornerRadius,
            CornerRadiusBottomRight = BarCornerRadius,
        };
        barContainer.AddThemeStyleboxOverride("panel", trackStyle);
        barContainer.MouseFilter = Control.MouseFilterEnum.Ignore;

        if (category == Category.Damage)
            BuildDamageBar(barContainer, accum, maxVal, isSub);
        else if (category == Category.Defense)
            BuildDefenseBar(barContainer, accum, maxVal, isSub, rowMode);
        else if (category == Category.Stars)
            BuildStarBar(barContainer, accum, value, maxVal, isSub);
        else if (category == Category.Healing)
            BuildHealBar(barContainer, accum, value, maxVal, isSub);
        else
            BuildSimpleBar(barContainer, accum, value, maxVal, isSub);

        row.AddChild(barContainer);

        // Value + percentage (PRD AC-15: 1 decimal place)
        string valText;
        if (IsNegativeRow(rowMode))
        {
            // HP costs on their own rows: show the raw value, no percentage
            // (denominators are computed from positive contributions).
            valText = $"{value}";
        }
        else
        {
            var pct = totalVal > 0 ? (float)value / totalVal * 100 : 0;
            valText = value < 0 ? $"{value}" : $"{value}  ({pct:F1}%)";
        }
        var valLabel = new Label { Text = valText };
        var valColor = rowMode == RowMode.NegativePetWasted ? PetWasteBarColor
            : (rowMode == RowMode.NegativeOnly || value < 0) ? SelfDmgBarColor
            : isSub ? new Color(0.5f, 0.5f, 0.6f) : new Color(0.7f, 0.7f, 0.7f);
        valLabel.AddThemeColorOverride("font_color", valColor);
        valLabel.AddThemeFontSizeOverride("font_size", indentLike ? 11 : 12);
        row.AddChild(valLabel);

        AddChild(rowWrap);
    }

    private static Color GetSourceColor(string sourceType) => sourceType switch
    {
        "relic" => RelicBarColor,
        "potion" => PotionBarColor,
        "pet" or "osty" => PetBarColor,
        _ => CardBarColor
    };

    private static void BuildDamageBar(Control container, ContributionAccum accum, int maxVal, bool isSub)
    {
        var baseColor = isSub ? SubBarColor : GetSourceColor(accum.SourceType);
        float offset = 0;

        // Direct damage
        if (accum.DirectDamage > 0)
        {
            var w = maxVal > 0 ? (float)accum.DirectDamage / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, baseColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        // Attributed damage (poison, vuln bonus, etc.)
        if (accum.AttributedDamage > 0)
        {
            var w = maxVal > 0 ? (float)accum.AttributedDamage / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, AttrBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        // Modifier damage (Strength, etc.)
        if (accum.ModifierDamage > 0)
        {
            var w = maxVal > 0 ? (float)accum.ModifierDamage / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, ModifierBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        // Upgrade damage bonus
        if (accum.UpgradeDamage > 0)
        {
            var w = maxVal > 0 ? (float)accum.UpgradeDamage / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, UpgradeBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
        }

        if (accum.TotalDamage <= 0)
            container.AddChild(CreateBar(0, BarHeight, baseColor));
    }

    private static void BuildDefenseBar(Control container, ContributionAccum accum, int maxVal, bool isSub, RowMode rowMode = RowMode.Normal)
    {
        var baseColor = isSub ? SubBarColor : GetSourceColor(accum.SourceType);
        float offset = 0;

        // NegativeOnly: render just the self-damage bar, nothing else.
        if (rowMode == RowMode.NegativeOnly)
        {
            if (accum.SelfDamage > 0)
            {
                var w = maxVal > 0 ? (float)accum.SelfDamage / maxVal * MaxBarWidth : MaxBarWidth * 0.2f;
                var bar = CreateBar(w, BarHeight, SelfDmgBarColor);
                bar.Position = new Vector2(0, 0);
                container.AddChild(bar);
            }
            else
            {
                container.AddChild(CreateBar(0, BarHeight, SelfDmgBarColor));
            }
            return;
        }

        // NegativePetWasted: render just the wasted-pet-HP bar.
        if (rowMode == RowMode.NegativePetWasted)
        {
            if (accum.PetWasted > 0)
            {
                var w = maxVal > 0 ? (float)accum.PetWasted / maxVal * MaxBarWidth : MaxBarWidth * 0.2f;
                var bar = CreateBar(w, BarHeight, PetWasteBarColor);
                bar.Position = new Vector2(0, 0);
                container.AddChild(bar);
            }
            else
            {
                container.AddChild(CreateBar(0, BarHeight, PetWasteBarColor));
            }
            return;
        }

        if (accum.EffectiveBlock > 0)
        {
            var w = maxVal > 0 ? (float)accum.EffectiveBlock / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, baseColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        if (accum.PetAbsorbed > 0)
        {
            var w = maxVal > 0 ? (float)accum.PetAbsorbed / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, PetBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        if (accum.ModifierBlock > 0)
        {
            var w = maxVal > 0 ? (float)accum.ModifierBlock / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, ModifierBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        if (accum.MitigatedByDebuff > 0)
        {
            var w = maxVal > 0 ? (float)accum.MitigatedByDebuff / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, MitigateBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        if (accum.MitigatedByBuff > 0)
        {
            var w = maxVal > 0 ? (float)accum.MitigatedByBuff / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, AttrBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        if (accum.MitigatedByStrReduction > 0)
        {
            var w = maxVal > 0 ? (float)accum.MitigatedByStrReduction / maxVal * MaxBarWidth : 0;
            var bar = CreateBar(w, BarHeight, StrReduceBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
            offset += w;
        }

        // Self-damage: red bar trailing the positive bars. Only drawn in
        // Normal mode; when the caller requested PositiveOnly the dedicated
        // NegativeOnly row that follows carries the self-damage visual.
        if (rowMode == RowMode.Normal && accum.SelfDamage > 0)
        {
            var w = maxVal > 0 ? (float)accum.SelfDamage / maxVal * MaxBarWidth : MaxBarWidth * 0.2f;
            var bar = CreateBar(w, BarHeight, SelfDmgBarColor);
            bar.Position = new Vector2(offset, 0);
            container.AddChild(bar);
        }

        if (PositiveDefense(accum) <= 0 && accum.SelfDamage <= 0 && accum.PetWasted <= 0)
            container.AddChild(CreateBar(0, BarHeight, baseColor));
    }

    private static void BuildStarBar(Control container, ContributionAccum accum, int value, int maxVal, bool isSub)
    {
        var barWidth = maxVal > 0 ? (float)value / maxVal * MaxBarWidth : 0;
        var color = isSub ? SubBarColor : StarBarColor;
        container.AddChild(CreateBar(barWidth, BarHeight, color));
    }

    private static void BuildHealBar(Control container, ContributionAccum accum, int value, int maxVal, bool isSub)
    {
        var barWidth = maxVal > 0 ? (float)value / maxVal * MaxBarWidth : 0;
        var color = isSub ? SubBarColor : HealBarColor;
        container.AddChild(CreateBar(barWidth, BarHeight, color));
    }

    private static void BuildSimpleBar(Control container, ContributionAccum accum, int value, int maxVal, bool isSub)
    {
        var barWidth = maxVal > 0 ? (float)value / maxVal * MaxBarWidth : 0;
        var color = isSub ? SubBarColor : GetSourceColor(accum.SourceType);
        container.AddChild(CreateBar(barWidth, BarHeight, color));
    }

    private static Control CreateBar(float width, float height, Color color)
    {
        // Use a Panel with a StyleBoxFlat so each bar gets rounded corners
        // and a subtle dark border — much cleaner than raw ColorRect.
        var panel = new Panel
        {
            CustomMinimumSize = new Vector2(width, height),
            Size = new Vector2(width, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var style = new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = BarBorderColor,
            BorderWidthBottom = 1,
            BorderWidthTop = 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            CornerRadiusTopLeft = BarCornerRadius,
            CornerRadiusTopRight = BarCornerRadius,
            CornerRadiusBottomLeft = BarCornerRadius,
            CornerRadiusBottomRight = BarCornerRadius,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private void AddSeparator()
    {
        // Vertical breathing room between sections; HSeparator's default look
        // is too noisy for the polished panel — use an empty spacer instead.
        var sep = new Control { CustomMinimumSize = new Vector2(0, 6) };
        AddChild(sep);
    }

    /// <summary>
    /// Resolves an internal ID (e.g. STRIKE_IRONCLAD) to its localized display name
    /// using the game's LocString system.
    /// </summary>
    private static string GetLocalizedName(string id, string sourceType)
    {
        // Rest site
        if (sourceType == "rest")
            return L.Get("source.rest_site");

        // Merchant
        if (sourceType == "merchant")
            return L.Get("source.merchant");

        // Per-floor recovery
        if (sourceType == "floor_regen")
        {
            // Extract floor number from "FLOOR_X_REGEN"
            var parts = id.Split('_');
            var floor = parts.Length >= 2 ? parts[1] : "?";
            return string.Format(L.Get("source.floor_regen"), floor);
        }

        // Forge sub-bar (Sovereign Blade's forge mechanic). IDs look like
        // "FORGE:BASE", "FORGE:BULWARK", "FORGE:FURNACE", etc.
        if (id.StartsWith("FORGE:"))
        {
            var variant = id.Substring(6);
            var key = "source.forge_" + variant.ToLowerInvariant();
            var known = L.Get(key);
            if (!string.IsNullOrEmpty(known) && known != key)
                return known;
            // Unknown variant: best-effort — look up the source power's title
            // from the game's LocString table, then wrap in the prefix template.
            try
            {
                var locStr = new LocString("powers", variant + "_POWER.title");
                var powerName = locStr.GetFormattedText();
                if (!string.IsNullOrEmpty(powerName) && powerName != variant + "_POWER.title")
                    return string.Format(L.Get("source.forge_prefix"), powerName);
            }
            catch { /* fall through */ }
            return string.Format(L.Get("source.forge_prefix"), variant);
        }

        try
        {
            // NameLookup follows the game's current localization table.
            var name = sourceType switch
            {
                "relic" => Util.NameLookup.Relic(id),
                "potion" => Util.NameLookup.Potion(id),
                "event" => Util.NameLookup.Event(id),
                "pet" or "osty" => Util.NameLookup.Monster(id),
                _ => Util.NameLookup.Card(id)
            };
            if (!string.IsNullOrEmpty(name) && name != id)
                return name;
        }
        catch { /* fall through to raw ID */ }
        return id;
    }
}
