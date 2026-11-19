using System;
using CommunityStats.Config;
using CommunityStats.Patches;
using CommunityStats.UI;
using CommunityStats.Util;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace CommunityStats.Settings;

/// <summary>
/// Registers the mod settings page in RitsuLib. Values are read from and
/// written to the existing <see cref="ModConfig"/> + mod_prefs.json store;
/// no new settings data file is introduced.
/// </summary>
public static class RitsuSettingsPage
{
    public const string ModId = "sts2_statsthespire_local";
    private const string PageId = "settings";
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;

        try
        {
            RitsuLibFramework.RegisterModSettings(ModId, page =>
            {
                page.WithTitle(Text("settings.title"))
                    .WithDescription(Text("settings.page_description"))
                    .WithModDisplayName(ModSettingsText.Literal("Stats the Spire"))
                    .WithSortOrder(0);

                page.AddSection("general", section =>
                {
                    section.WithTitle(Text("settings.section_general"));

                    section.AddChoice(
                        "language",
                        Text("settings.language"),
                        Binding(
                            "settings.language",
                            () => ModConfig.Language,
                            value =>
                            {
                                ModConfig.Language = value == "EN" ? "EN" : "CN";
                                ApplySettingsChanged();
                            }),
                        [
                            new ModSettingsChoiceOption<string>("CN", ModSettingsText.Literal("中文")),
                            new ModSettingsChoiceOption<string>("EN", ModSettingsText.Literal("English")),
                        ],
                        presentation: ModSettingsChoicePresentation.Dropdown);

                    section.AddSlider(
                        "intent_panel_scale",
                        Text("settings.intent_panel_scale"),
                        Binding(
                            "settings.intent_panel_scale",
                            () => (double)ModConfig.IntentPanelScale,
                            value =>
                            {
                                ModConfig.IntentPanelScale = Math.Clamp((float)value, 0.5f, 1.25f);
                                ApplySettingsChanged();
                            }),
                        0.5,
                        1.25,
                        0.05,
                        value => $"{value:F2}×");

                    section.AddKeyBinding(
                        "contribution_panel_hotkey",
                        Text("settings.contribution_hotkey"),
                        Binding(
                            "settings.contribution_panel_hotkey",
                            () => ModConfig.ContributionPanelHotkey,
                            value =>
                            {
                                if (HotkeyRegistration.TryRebind(value, out var normalized))
                                    ModConfig.ContributionPanelHotkey = normalized;
                                else
                                    Safe.Warn($"[RitsuSettings] rejected invalid hotkey '{value}'");
                            }),
                        allowModifierCombos: true,
                        allowModifierOnly: false,
                        distinguishModifierSides: false,
                        Text("settings.contribution_hotkey_desc"));
                });

                page.AddSection("toggles", section =>
                {
                    section.WithTitle(Text("settings.toggles_title"));
                    foreach (var (key, labelKey) in FeatureToggles.ToggleDefinitions)
                    {
                        section.AddToggle(
                            $"feature_{key}",
                            Text(labelKey),
                            Binding(
                                $"feature_toggles.{key}",
                                () => ModConfig.Toggles.GetByName(key),
                                value =>
                                {
                                    ModConfig.Toggles.SetByName(key, value);
                                    ApplySettingsChanged();
                                }));
                    }
                });
            }, PageId);

            _registered = true;
            Safe.Info("[RitsuSettings] registered settings page");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[RitsuSettings] failed to register settings page: {ex}");
        }
    }

    private static ModSettingsCallbackValueBinding<T> Binding<T>(
        string dataKey,
        Func<T> read,
        Action<T> write)
        => new(ModId, dataKey, SaveScope.Global, read, write, SaveAndApply);

    private static void SaveAndApply()
    {
        ModConfig.SaveSettings();
        ApplySettingsChanged();
    }

    private static void ApplySettingsChanged()
    {
        Safe.Run(() =>
        {
            L.Current = ModConfig.Language == "EN" ? L.Lang.EN : L.Lang.CN;
            CombatUiOverlayPatch.RefreshVisibility();
            IntentHoverPatch.ForceHideAll();
            ContributionPanel.RefreshText();
        });
    }

    private static ModSettingsText Text(string key)
        => ModSettingsText.Dynamic(() => L.Get(key));
}
