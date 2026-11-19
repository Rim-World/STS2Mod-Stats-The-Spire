using System.Text.Json;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace StatsTheSpire.CompatProbe;

/// <summary>
/// Test-only probe for 1.5.1:
/// 1. A legacy seed-only shop purchase file must be copied into the current
///    StartTime+seed directory during finalization, and the old file must stay.
/// 2. Corrupt mod_prefs.json and settings.json must log a warning and fall
///    back to defaults. The probe restores both files in finally.
/// </summary>
[ModInitializer("Initialize")]
public static class CompatProbe
{
    private const string Seed = "codexlegacy151";
    private const long StartTime = 1795113500;
    private static int _frames;
    private static bool _probed;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            Safe.Warn("[CompatProbe] no SceneTree");
            return;
        }

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (!_probed && _frames >= 300)
            {
                _probed = true;
                Safe.Run(() => Run(tree));
            }
            if (_frames >= 600)
                tree.Quit();
        };
    }

    private static void Run(SceneTree tree)
    {
        ProbeLegacyShopCopy();
        ProbeCorruptSettings();
        tree.Quit();
    }

    private static void ProbeLegacyShopCopy()
    {
        var identity = new RunIdentity(StartTime, Seed);
        var legacyPath = RunStorage.LegacyShopPurchasesPath(Seed);
        var runDir = RunStorage.DirectoryFor(identity);
        var newPath = RunStorage.ShopPurchasesPath(identity);

        try
        {
            Directory.CreateDirectory(ModConfig.ContributionsDir);
            if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true);
            if (File.Exists(legacyPath)) File.Delete(legacyPath);

            var purchases = new List<LocalShopPurchase>
            {
                new() { ItemId = "TEST_CARD_A", ItemType = "card", Cost = 1, Floor = 2 },
                new() { ItemId = "TEST_CARD_B", ItemType = "card", Cost = 2, Floor = 3 },
            };
            File.WriteAllText(legacyPath, JsonSerializer.Serialize(purchases));

            RunDataCollector.ResumeRun(identity);
            int loaded = RunDataCollector.GetShopPurchases().Count;
            GD.Print($"[CompatProbe] legacy-load count={loaded} oldExists={File.Exists(legacyPath)} newExists={File.Exists(newPath)}");

            var serializableRun = new SerializableRun
            {
                StartTime = StartTime,
                SerializableRng = new SerializableRunRngSet { Seed = Seed },
            };
            RunDataCollector.OnMetricsUpload(serializableRun, isVictory: false, localPlayerId: 0);

            int copied = 0;
            if (File.Exists(newPath))
            {
                var saved = JsonSerializer.Deserialize<List<LocalShopPurchase>>(File.ReadAllText(newPath));
                copied = saved?.Count ?? 0;
            }

            GD.Print($"[CompatProbe] legacy-copy newExists={File.Exists(newPath)} copied={copied} oldExists={File.Exists(legacyPath)}");
            if (loaded != 2 || copied != 2 || !File.Exists(legacyPath))
                GD.Print("[CompatProbe] legacy-copy FAILED");
            else
                GD.Print("[CompatProbe] legacy-copy PASS");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[CompatProbe] legacy-copy exception: {ex}");
        }
        finally
        {
            try { if (File.Exists(legacyPath)) File.Delete(legacyPath); } catch { }
            try { if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true); } catch { }
        }
    }

    private static void ProbeCorruptSettings()
    {
        var prefsPath = ModConfig.PrefsPath;
        var settingsPath = ModConfig.SettingsPath;
        byte[]? originalPrefs = File.Exists(prefsPath) ? File.ReadAllBytes(prefsPath) : null;
        byte[]? originalSettings = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;

        try
        {
            Directory.CreateDirectory(ModConfig.DataDir);

            File.WriteAllText(prefsPath, "{ broken");
            ModConfig.LoadOverrides();
            GD.Print("[CompatProbe] corrupt-prefs load returned");

            File.WriteAllText(settingsPath, "{ broken");
            var filter = FilterSettings.Load();
            GD.Print($"[CompatProbe] corrupt-settings fallback charMode={filter.CharacterFilterMode} minAsc={filter.MinAscension?.ToString() ?? "null"}");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[CompatProbe] corrupt-settings exception: {ex}");
        }
        finally
        {
            RestoreFile(prefsPath, originalPrefs);
            RestoreFile(settingsPath, originalSettings);
            ModConfig.LoadOverrides();
            GD.Print("[CompatProbe] settings restored");
        }
    }

    private static void RestoreFile(string path, byte[]? content)
    {
        try
        {
            if (content != null)
                File.WriteAllBytes(path, content);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Safe.Warn($"[CompatProbe] restore {Path.GetFileName(path)} failed: {ex.Message}");
        }
    }
}
