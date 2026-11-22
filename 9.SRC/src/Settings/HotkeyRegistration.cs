using System;
using System.Threading.Tasks;
using CommunityStats.Config;
using CommunityStats.UI;
using CommunityStats.Util;
using MegaCrit.Sts2.Core.Nodes;
using STS2RitsuLib.RuntimeInput;

namespace CommunityStats.Settings;

/// <summary>
/// Owns the single contribution-panel runtime hotkey. Registration is deferred
/// until the game node exists; setting changes made before that point are
/// persisted and applied by <see cref="InitializeAsync"/>.
/// </summary>
public static class HotkeyRegistration
{
    public const string HotkeyId = "sts2_statsthespire_local.toggle_contribution_panel";
    public const string DefaultBinding = "Ctrl+F8";

    private static IRuntimeHotkeyHandle? _handle;
    private static bool _initialized;

    public static async Task InitializeAsync()
    {
        if (_initialized) return;

        // RitsuLib needs NGame as the router parent. ModInitializer can run
        // before that node exists, so wait briefly instead of failing once.
        for (int attempt = 0; attempt < 40; attempt++)
        {
            if (TryRegisterConfigured()) return;
            await Task.Delay(250);
        }

        Safe.Warn("[HotkeyRegistration] failed to register contribution-panel hotkey after 10s");
    }

    private static bool TryRegisterConfigured()
    {
        if (NGame.Instance == null) return false;

        var binding = ModConfig.ContributionPanelHotkey;
        if (!RuntimeHotkeyService.TryNormalizeBinding(binding, out var normalized))
        {
            Safe.Warn($"[HotkeyRegistration] invalid configured binding '{binding}', using {DefaultBinding}");
            normalized = DefaultBinding;
            ModConfig.ContributionPanelHotkey = normalized;
        }

        try
        {
            _handle = RuntimeHotkeyService.Register(
                normalized,
                () => Safe.Run(ContributionPanel.Toggle),
                new RuntimeHotkeyOptions
                {
                    Id = HotkeyId,
                    DebugName = "StatsTheSpire.ContributionPanel",
                    DisplayName = RuntimeHotkeyText.Literal("Stats the Spire: contribution panel"),
                    Description = RuntimeHotkeyText.Literal("Toggle the contribution panel"),
                    MarkInputHandled = true,
                    SuppressWhenTextInputFocused = true,
                    SuppressWhenDevConsoleVisible = true,
                });
            _initialized = true;
            Safe.Info($"[HotkeyRegistration] registered {normalized} for {HotkeyId}");
            return true;
        }
        catch (Exception ex)
        {
            Safe.Warn($"[HotkeyRegistration] registration failed (binding={normalized}): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validate and apply a new binding. If registration is already live, the
    /// runtime handle is rebound immediately; otherwise the value is stored and
    /// used by the deferred registration.
    /// </summary>
    public static bool TryRebind(string bindingText, out string normalized)
    {
        normalized = "";
        if (!RuntimeHotkeyService.TryNormalizeBinding(bindingText, out normalized))
            return false;

        try
        {
            if (_handle is { IsRegistered: true })
            {
                if (!_handle.TryRebind(normalized, out var applied))
                    return false;
                normalized = applied;
            }

            ModConfig.ContributionPanelHotkey = normalized;
            return true;
        }
        catch (Exception ex)
        {
            Safe.Warn($"[HotkeyRegistration] rebind failed: {ex.Message}");
            return false;
        }
    }
}
