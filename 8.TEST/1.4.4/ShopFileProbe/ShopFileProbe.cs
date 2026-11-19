using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.ShopFileProbe;

/// <summary>
/// Test-only probe: create a shop-purchase sidecar next to one combat
/// contribution, then call AssembleFromCombats. The old build logs a warning
/// for the sidecar; 1.4.4 must skip it and merge the combat file.
/// </summary>
[ModInitializer("Initialize")]
public static class ShopFileProbe
{
    private const string Seed = "codextest_shop";
    private static int _frames;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            Safe.Warn("[ShopFileProbe] no SceneTree");
            return;
        }

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_frames == 300) Run(tree);
        };
    }

    private static void Run(SceneTree tree)
    {
        var dir = ModConfig.ContributionsDir;
        var combatPath = Path.Combine(dir, Seed + "_1.json");
        var shopPath = Path.Combine(dir, Seed + "_shop_purchases.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(combatPath,
                "{\"Seed\":\"codextest_shop\",\"Floor\":1,\"Kind\":\"combat\"," +
                "\"SavedAtUnix\":0,\"Sources\":[{\"SourceId\":\"TEST_CARD\"," +
                "\"SourceType\":\"card\",\"TimesPlayed\":1,\"DirectDamage\":5}]}");
            File.WriteAllText(shopPath,
                "[{\"ItemId\":\"TEST_CARD\",\"ItemType\":\"card\",\"Cost\":1,\"Floor\":1}]");

            var merged = ContributionPersistence.AssembleFromCombats(Seed);
            Safe.Info($"[ShopFileProbe] sources={merged?.Count ?? -1}");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[ShopFileProbe] failed: {ex}");
        }
        finally
        {
            try { File.Delete(combatPath); } catch { }
            try { File.Delete(shopPath); } catch { }
            tree.Quit();
        }
    }
}
