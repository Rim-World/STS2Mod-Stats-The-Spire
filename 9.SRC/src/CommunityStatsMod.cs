using CommunityStats.Config;
using CommunityStats.Patches;
using CommunityStats.Settings;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace CommunityStats;

/// <summary>
/// Stats the Spire (Local Edition) mod entry point.
/// Initializes Harmony patches, loads user settings, and attaches UI panels
/// to the scene tree. No network — all data is local.
/// </summary>
[ModInitializerAttribute("Initialize")]
public static class CommunityStatsMod
{
    private static Harmony? _harmony;

    public static void Initialize()
    {
        Safe.Info($"Stats the Spire (Local) v{ModConfig.ModVersion} initializing...");

        // Load saved settings (feature toggles, etc.) from disk.
        ModConfig.LoadOverrides();

        // Register the RitsuLib settings page before the user can open it.
        RitsuSettingsPage.Register();

        // Ensure data directories exist. A read-only AppData must not prevent
        // the game from starting; individual writes report their own errors.
        if (!ModConfig.EnsureDirectories())
            Safe.Warn("[Startup] data directory unavailable; running without disk cache");

        // Old contribution files are never deleted automatically. Users may
        // keep years of run history; cleanup is an explicit manual action.

        // Pre-warm the in-memory career-stats cache from disk so the very first
        // 百科大全 → 角色数据 page open shows data instantly.
        Safe.Run(() => Collection.RunHistoryAnalyzer.Instance.GetCached(null));

        // Kick off a background full LoadAllAsync so the per-card / per-relic
        // bundles (LocalCards / LocalRelics) get populated.
        Safe.RunAsync(async () =>
        {
            try { await Collection.RunHistoryAnalyzer.Instance.LoadAllAsync(null); }
            catch (Exception ex) { Safe.Warn($"Startup LoadAllAsync failed: {ex.Message}"); }
        });

        // Load saved filter settings
        Safe.Run(() =>
        {
            ModConfig.CurrentFilter = FilterSettings.Load() ?? new FilterSettings();
        });

        // Apply Harmony patches — patch each class individually so one failure
        // doesn't prevent the rest from loading.
        _harmony = new Harmony("com.communitystats.sts2");
        var assembly = typeof(CommunityStatsMod).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            try
            {
                var processor = _harmony.CreateClassProcessor(type);
                processor.Patch();
            }
            catch (Exception ex)
            {
                Safe.Warn($"[Harmony] Failed to patch {type.Name}: {ex}");
            }
        }

        // Apply manual hook context patches. Each block is isolated so one
        // failed target does not prevent the remaining attribution hooks,
        // metric hook, or hotkey installation.
        TryInstall("relic hooks", () => RelicHookContextPatcher.PatchAll(_harmony));
        TryInstall("power hooks", () => PowerHookContextPatcher.PatchAll(_harmony));
        TryInstall("orb turn-end", () => OrbPassivePatch.PatchOrbTurnEndTriggers(_harmony));
        TryInstall("orb evoke", () => OrbEvokePatch.PatchOrbEvokeMethods(_harmony));
        TryInstall("killing blow", () => KillingBlowPatcher.PatchAll(_harmony));
        TryInstall("damage guard", () => RealDamageGuardPatch.Patch(_harmony));
        TryInstall("card generation", () => CardGenerationOriginPatch.Patch(_harmony));
        TryInstall("fool content hooks", () => FoolContentPatch.PatchAll(_harmony));

        // Harmony is now installed. The intent cache model lookup can safely
        // run and can be retried from CombatLifecyclePatch if ModelDb is late.
        Safe.Run(() => Util.MonsterIntentMetadata.Initialize());
        PatchInventory.Log(_harmony);

        // Register ModManager.OnMetricsUpload hook for run contribution saving
        RunLifecyclePatch.RegisterMetricsHook();

        // Register hotkeys and attach UI panels to scene tree
        Safe.Run(() => RegisterHotkeys());

        Safe.Info("Stats the Spire initialized successfully");
    }

    private static void TryInstall(string name, Action action)
    {
        try { action(); }
        catch (Exception ex) { Safe.Warn($"[Harmony] {name} install failed: {ex.Message}"); }
    }

    private static void RegisterHotkeys()
    {
        // Wait for the scene tree, then register the RitsuLib runtime hotkey
        // and attach the UI panels to the root.
        Safe.RunAsync(async () =>
        {
            while (Engine.GetMainLoop() is not SceneTree sceneTree || sceneTree.Root == null)
            {
                await Task.Delay(100);
            }

            var tree = (SceneTree)Engine.GetMainLoop();
            var root = tree.Root;
            root.CallDeferred(Node.MethodName.AddChild, ContributionPanel.Instance);

            // Attach top-bar potion / card-drop indicators.
            CommunityStats.Patches.CombatUiOverlayPatch.Attach();

            // RitsuLib owns hotkey routing and text-input suppression.
            await HotkeyRegistration.InitializeAsync();
        });
    }
}
