using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CommunityStats.Collection;
using CommunityStats.Config;

namespace CommunityStats.Util;

/// <summary>
/// Persist the in-memory shop purchase list for one concrete run.
/// New runs use the per-run directory; legacy seed-keyed files are read-only
/// fallback when no new directory exists.
/// </summary>
public static class ShopPurchasePersistence
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };

    public static void Save(List<LocalShopPurchase> purchases)
    {
        var identity = ContributionPersistence.GetActiveIdentity();
        if (identity == null || !identity.IsValid) return;

        Safe.Run(() =>
        {
            RunStorage.EnsureDirectory(identity);
            var path = RunStorage.ShopPurchasesPath(identity);
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(purchases, JsonOpts));
        });
    }

    public static List<LocalShopPurchase>? Load(RunIdentity? identity = null)
        => LoadWithSource(identity, out _);

    /// <summary>
    /// Load purchases for one run and report whether the data came from the
    /// legacy seed-keyed file. The source flag lets the run finalizer copy
    /// legacy purchases into the current RunKey without moving the old file.
    /// </summary>
    public static List<LocalShopPurchase>? LoadWithSource(RunIdentity? identity, out bool fromLegacy)
    {
        fromLegacy = false;
        identity ??= ContributionPersistence.GetActiveIdentity();
        if (identity == null || !identity.IsValid) return null;

        var path = RunStorage.ShopPurchasesPath(identity);
        if (!File.Exists(path) && identity.HasStartTime && !RunStorage.HasNewDirectory(identity))
        {
            path = RunStorage.LegacyShopPurchasesPath(identity.Seed);
            fromLegacy = true;
        }

        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<LocalShopPurchase>>(json, JsonOpts);
        }
        catch (System.Exception ex)
        {
            Safe.Warn($"ShopPurchasePersistence: load failed: {ex.Message}");
            AtomicFile.Quarantine(path, ex);
            return null;
        }
    }

    public static void Delete(RunIdentity identity)
    {
        if (identity == null || !identity.IsValid) return;
        try
        {
            var path = RunStorage.ShopPurchasesPath(identity);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
