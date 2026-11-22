using System.Text.Json;
using CommunityStats.Util;
using MegaCrit.Sts2.Core.Logging;

namespace CommunityStats.Config;

/// <summary>
/// Central configuration for the local edition.
/// No server addresses, upload flags, or update settings.
/// </summary>
public static class ModConfig
{
    public const string ModVersion = "1.7.3";

    // User preferences
    public static string Language { get; set; } = "CN"; // "CN" | "EN"

    // Feature toggles
    public static FeatureToggles Toggles { get; set; } = new();

    // Panel position (null = default right side)
    public static float? PanelPositionX { get; set; }
    public static float? PanelPositionY { get; set; }

    // Resizable contribution panel (null = default size)
    public static float? PanelWidth { get; set; }
    public static float? PanelHeight { get; set; }

    // Intent state machine scale
    public static float IntentPanelScale { get; set; } = 0.8f;

    // Contribution panel hotkey (stable F-key token; RitsuLib parses it)
    public static string ContributionPanelHotkey { get; set; } = "F8";

    // "My Data" filter
    public static bool UseMyDataOnly { get; set; }

    // Cache
    public static int MemoryCacheTtlSeconds { get; set; } = 900;   // 15 min
    public static int DiskCacheTtlHours { get; set; } = 24;

    // Paths
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "sts2_community_stats");
    public static string CacheDir => Path.Combine(DataDir, "cache");
    public static string ContributionsDir => Path.Combine(DataDir, "contributions");
    public static string SettingsPath => Path.Combine(DataDir, "settings.json");

    // Active filter (mutable at runtime)
    public static FilterSettings CurrentFilter { get; set; } = new();

    // Config override file path (next to the mod DLL).
    // Named .cfg so the game engine doesn't scan it as a mod manifest.
    public static string ConfigPath
    {
        get
        {
            var asmLocation = typeof(ModConfig).Assembly.Location;
            if (!string.IsNullOrEmpty(asmLocation))
                return Path.Combine(Path.GetDirectoryName(asmLocation)!, "settings.cfg");
            return Path.Combine(DataDir, "settings.cfg");
        }
    }

    /// <summary>User-modifiable preferences saved to AppData (always writable).</summary>
    public static string PrefsPath => Path.Combine(DataDir, "mod_prefs.json");

    public static bool EnsureDirectories()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            Directory.CreateDirectory(ContributionsDir);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[StatsTheSpire][Config] data directory unavailable; running without disk cache: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load config overrides from disk. Reads shipped config first, then the
    /// user's mod_prefs.json from AppData (overrides take priority).
    /// </summary>
    public static void LoadOverrides()
    {
        ApplyConfigFile(ConfigPath);
        ApplyConfigFile(PrefsPath);
    }

    private static void ApplyConfigFile(string path)
    {
        bool warned = false;
        void WarnOnce(string scope, Exception ex)
        {
            if (warned) return;
            warned = true;
            var suffix = string.IsNullOrEmpty(scope) ? "" : $"（{scope}）";
            Log.Warn(
                $"[StatsTheSpire][Config] {Path.GetFileName(path)} 解析失败{suffix}，" +
                $"已读取项保留，未读取项使用默认值: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("language", out var lang))
                Language = lang.GetString() ?? Language;

            if (root.TryGetProperty("feature_toggles", out var toggles))
            {
                try
                {
                    Toggles = JsonSerializer.Deserialize<FeatureToggles>(toggles.GetRawText()) ?? new();
                }
                catch (Exception ex)
                {
                    Toggles = new();
                    WarnOnce("feature_toggles", ex);
                }
            }

            if (root.TryGetProperty("panel_position", out var pos))
            {
                if (pos.TryGetProperty("x", out var px)) PanelPositionX = px.GetSingle();
                if (pos.TryGetProperty("y", out var py)) PanelPositionY = py.GetSingle();
            }

            if (root.TryGetProperty("panel_size", out var size))
            {
                if (size.TryGetProperty("width", out var width))
                {
                    var value = width.GetSingle();
                    if (value > 0) PanelWidth = value;
                }
                if (size.TryGetProperty("height", out var height))
                {
                    var value = height.GetSingle();
                    if (value > 0) PanelHeight = value;
                }
            }

            if (root.TryGetProperty("intent_panel_scale", out var scale))
                IntentPanelScale = Math.Clamp(scale.GetSingle(), 0.5f, 1.25f);

            if (root.TryGetProperty("contribution_panel_hotkey", out var hotkey))
            {
                var text = hotkey.GetString();
                if (!string.IsNullOrWhiteSpace(text) &&
                    STS2RitsuLib.RuntimeInput.RuntimeHotkeyService.TryNormalizeBinding(text, out var normalized))
                    ContributionPanelHotkey = normalized;
            }

            if (root.TryGetProperty("use_my_data_only", out var myData))
                UseMyDataOnly = myData.GetBoolean();
        }
        catch (Exception ex)
        {
            WarnOnce("", ex);
        }
    }

    /// <summary>
    /// Save current settings to AppData. Uses PrefsPath so settings survive
    /// mod updates and directory permission issues.
    /// </summary>
    public static void SaveSettings()
    {
        try
        {
            var data = new Dictionary<string, object?>
            {
                ["language"] = Language,
                ["feature_toggles"] = Toggles,
                ["use_my_data_only"] = UseMyDataOnly,
                ["intent_panel_scale"] = IntentPanelScale,
                ["contribution_panel_hotkey"] = ContributionPanelHotkey,
            };

            if (PanelPositionX.HasValue && PanelPositionY.HasValue)
            {
                data["panel_position"] = new { x = PanelPositionX.Value, y = PanelPositionY.Value };
            }

            if (PanelWidth.HasValue && PanelHeight.HasValue)
            {
                data["panel_size"] = new { width = PanelWidth.Value, height = PanelHeight.Value };
            }

            EnsureDirectories();
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            AtomicFile.WriteAllText(PrefsPath, json);
        }
        catch { /* ignore write failures */ }
    }
}
