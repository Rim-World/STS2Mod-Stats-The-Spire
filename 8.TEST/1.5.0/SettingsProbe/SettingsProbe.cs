using System.Linq;
using CommunityStats.Config;
using CommunityStats.Settings;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib.RuntimeInput;
using STS2RitsuLib.Settings;

namespace StatsTheSpire.SettingsProbe;

/// <summary>
/// Test-only probe: verifies the RitsuLib page registration, main-menu host,
/// and the contribution-panel hotkey. It does not modify production code.
/// </summary>
[ModInitializer("Initialize")]
public static class SettingsProbe
{
    private const string ModId = "sts2_statsthespire_local";
    private const string PageId = "settings";
    private const string HotkeyId = "sts2_statsthespire_local.toggle_contribution_panel";

    private static int _frames;
    private static bool _probed;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += OnProcessFrame;
    }

    private static void OnProcessFrame()
    {
        _frames++;
        if (!_probed && _frames >= 600)
        {
            _probed = true;
            Safe.Run(Probe);
        }

        if (_frames >= 900)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Quit();
        }
    }

    private static void Probe()
    {
        var pages = ModSettingsRegistry.GetPages()
            .Where(p => string.Equals(p.ModId, ModId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        GD.Print($"[SettingsProbe] pages={pages.Count}");
        foreach (var page in pages)
        {
            int entries = page.Sections.Sum(s => s.Entries.Count);
            GD.Print($"[SettingsProbe] page id={page.Id} title={page.Title?.Resolve()} sections={page.Sections.Count} entries={entries} visibleOnHosts={page.VisibleOnHostSurfaces}");
        }

        var allEntries = pages.SelectMany(p => p.Sections).SelectMany(s => s.Entries).ToList();
        ProbeBindings(allEntries);

        var result = ModSettingsNavigator.RequestOpenByIds(ModId, PageId, null, null);
        GD.Print($"[SettingsProbe] open-main-menu success={result.Success} code={result.Code} deferred={result.IsDeferred} message={result.Message}");

        var hotkey = RuntimeHotkeyService.GetRegisteredHotkeys()
            .FirstOrDefault(h => string.Equals(h.Id, HotkeyId, StringComparison.Ordinal));
        GD.Print(hotkey == null
            ? $"[SettingsProbe] hotkey MISSING id={HotkeyId}"
            : $"[SettingsProbe] hotkey id={hotkey.Id} binding={hotkey.CurrentBinding} textSuppressed={hotkey.SuppressWhenTextInputFocused} consoleSuppressed={hotkey.SuppressWhenDevConsoleVisible}");

        bool f9 = HotkeyRegistration.TryRebind("F9", out var normalizedF9);
        GD.Print($"[SettingsProbe] rebind-F9 ok={f9} normalized={normalizedF9}");
        bool f8 = HotkeyRegistration.TryRebind("F8", out var normalizedF8);
        GD.Print($"[SettingsProbe] rebind-F8 ok={f8} normalized={normalizedF8}");
        bool invalid = HotkeyRegistration.TryRebind("not-a-key", out var invalidNormalized);
        GD.Print($"[SettingsProbe] reject-invalid ok={invalid} normalized={invalidNormalized}");
    }

    private static void ProbeBindings(List<ModSettingsEntryDefinition> entries)
    {
        var language = entries.OfType<ChoiceModSettingsEntryDefinition<string>>()
            .FirstOrDefault(e => e.Id == "language");
        if (language != null)
        {
            string original = language.Binding.Read();
            language.Binding.Write("EN");
            language.Binding.Save();
            GD.Print($"[SettingsProbe] binding language write=EN config={ModConfig.Language} current={L.Current}");
            language.Binding.Write(original);
            language.Binding.Save();
        }

        var scale = entries.OfType<SliderModSettingsEntryDefinition>()
            .FirstOrDefault(e => e.Id == "intent_panel_scale");
        if (scale != null)
        {
            double original = scale.Binding.Read();
            scale.Binding.Write(1.0);
            scale.Binding.Save();
            GD.Print($"[SettingsProbe] binding scale write=1.0 config={ModConfig.IntentPanelScale:F2}");
            scale.Binding.Write(original);
            scale.Binding.Save();
        }

        var toggle = entries.OfType<ToggleModSettingsEntryDefinition>()
            .FirstOrDefault(e => e.Id == "feature_PotionOdds");
        if (toggle != null)
        {
            bool original = toggle.Binding.Read();
            toggle.Binding.Write(!original);
            toggle.Binding.Save();
            GD.Print($"[SettingsProbe] binding toggle write={!original} config={ModConfig.Toggles.PotionOdds}");
            toggle.Binding.Write(original);
            toggle.Binding.Save();
        }
    }
}
