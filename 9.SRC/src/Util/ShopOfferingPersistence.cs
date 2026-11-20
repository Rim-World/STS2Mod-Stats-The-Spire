using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CommunityStats.Collection;

namespace CommunityStats.Util;

/// <summary>
/// Persist the in-memory shop-card-offerings list for one concrete run.
/// Survives save+quit+resume and no longer mixes runs that share a seed.
/// </summary>
public static class ShopOfferingPersistence
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };

    public static void Save(List<LocalShopCardOffering> offerings)
    {
        var identity = ContributionPersistence.GetActiveIdentity();
        if (identity == null || !identity.IsValid) return;

        Safe.Run(() =>
        {
            RunStorage.EnsureDirectory(identity);
            var path = RunStorage.ShopOffersPath(identity);
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(offerings, JsonOpts));
        });
    }

    public static List<LocalShopCardOffering>? Load(RunIdentity? identity = null)
    {
        identity ??= ContributionPersistence.GetActiveIdentity();
        if (identity == null || !identity.IsValid) return null;

        var path = RunStorage.ShopOffersPath(identity);
        if (!File.Exists(path) && identity.HasStartTime && !RunStorage.HasNewDirectory(identity))
            path = RunStorage.LegacyShopOffersPath(identity.Seed);

        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<LocalShopCardOffering>>(json, JsonOpts);
        }
        catch (System.Exception ex)
        {
            Safe.Warn($"ShopOfferingPersistence: load failed: {ex.Message}");
            AtomicFile.Quarantine(path, ex);
            return null;
        }
    }

    public static void Delete(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return;
        try
        {
            var path = RunStorage.ShopOffersPath(identity);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
