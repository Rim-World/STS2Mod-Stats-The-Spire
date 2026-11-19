using System.Text;
using System.Text.Json;
using CommunityStats.Collection;
using CommunityStats.Config;

namespace CommunityStats.Util;

/// <summary>
/// Resolves the on-disk layout for one run. New data lives in
/// <c>contributions/{StartTime}_{seed}/</c>. Legacy data remains flat in
/// <c>contributions/{seed}_*.json</c> and is only read, never rewritten.
/// </summary>
public static class RunStorage
{
    public const int SchemaVersion = 1;

    public static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";

        var invalid = Path.GetInvalidFileNameChars();
        var buf = new StringBuilder(value.Length);
        foreach (var c in value)
            buf.Append(Array.IndexOf(invalid, c) >= 0 || c is '*' or '?' ? '_' : c);

        var result = buf.ToString().Trim();
        if (result.Length == 0) return "unknown";
        return result.Length <= 80 ? result : result[..80];
    }

    public static string DirectoryFor(RunIdentity identity)
        => identity.HasStartTime
            ? Path.Combine(ModConfig.ContributionsDir, identity.RunKey)
            : ModConfig.ContributionsDir;

    public static bool HasNewDirectory(RunIdentity identity)
        => identity.HasStartTime && Directory.Exists(DirectoryFor(identity));

    public static string EnsureDirectory(RunIdentity identity)
    {
        var dir = DirectoryFor(identity);
        Directory.CreateDirectory(dir);
        if (identity.HasStartTime) WriteMetaIfMissing(identity, dir);
        return dir;
    }

    public static string MetaPath(RunIdentity identity)
        => Path.Combine(DirectoryFor(identity), "meta.json");

    public static string CombatPath(RunIdentity identity, int floor, int sequence)
    {
        if (!identity.HasStartTime)
            return sequence <= 1 ? LegacyCombatPath(identity.Seed, floor) : LegacyCombatSequencePath(identity.Seed, floor, sequence);
        return Path.Combine(DirectoryFor(identity), $"combat_{floor}_{sequence}.json");
    }

    public static string SummaryPath(RunIdentity identity)
        => identity.HasStartTime
            ? Path.Combine(DirectoryFor(identity), "summary.json")
            : LegacySummaryPath(identity.Seed);

    public static string LivePath(RunIdentity identity)
        => identity.HasStartTime
            ? Path.Combine(DirectoryFor(identity), "live.json")
            : LegacyLivePath(identity.Seed);

    public static string ShopPurchasesPath(RunIdentity identity)
        => identity.HasStartTime
            ? Path.Combine(DirectoryFor(identity), "shop_purchases.json")
            : LegacyShopPurchasesPath(identity.Seed);

    public static string ShopOffersPath(RunIdentity identity)
        => identity.HasStartTime
            ? Path.Combine(DirectoryFor(identity), "shop_offers.json")
            : LegacyShopOffersPath(identity.Seed);

    public static int NextCombatSequence(RunIdentity identity, int floor)
    {
        var dir = DirectoryFor(identity);
        for (int sequence = 1; sequence < 10000; sequence++)
        {
            if (!File.Exists(CombatPath(identity, floor, sequence)))
                return sequence;
        }
        return 10000;
    }

    // ── Legacy flat layout (read-only) ─────────────────────

    public static string LegacyCombatPath(string seed, int floor)
        => Path.Combine(ModConfig.ContributionsDir, $"{Sanitize(seed)}_{floor}.json");

    public static string LegacyCombatSequencePath(string seed, int floor, int sequence)
        => Path.Combine(ModConfig.ContributionsDir, $"{Sanitize(seed)}_{floor}_{sequence}.json");

    public static string LegacyCombatPattern(string seed)
        => $"{Sanitize(seed)}_*.json";

    public static string LegacySummaryPath(string seed)
        => Path.Combine(ModConfig.ContributionsDir, $"{Sanitize(seed)}_summary.json");

    public static string LegacyLivePath(string seed)
        => Path.Combine(ModConfig.ContributionsDir, $"{Sanitize(seed)}_live.json");

    public static string LegacyShopPurchasesPath(string seed)
        => Path.Combine(ModConfig.ContributionsDir, $"{Sanitize(seed)}_shop_purchases.json");

    public static string LegacyShopOffersPath(string seed)
        => Path.Combine(ModConfig.ContributionsDir, $"{Sanitize(seed)}_shop_offers.json");

    // ── Metadata ────────────────────────────────────────────

    private static void WriteMetaIfMissing(RunIdentity identity, string dir)
    {
        var path = Path.Combine(dir, "meta.json");
        if (File.Exists(path)) return;

        try
        {
            var meta = new RunMeta
            {
                SchemaVersion = SchemaVersion,
                StartTime = identity.StartTime,
                Seed = identity.Seed,
                GameVersion = VersionManager.GameVersion,
                ModVersion = ModConfig.ModVersion,
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Safe.Warn($"[RunStorage] failed to write meta.json: {ex.Message}");
        }
    }

    private sealed class RunMeta
    {
        public int SchemaVersion { get; set; }
        public long StartTime { get; set; }
        public string Seed { get; set; } = "";
        public string GameVersion { get; set; } = "";
        public string ModVersion { get; set; } = "";
        public long CreatedAtUnix { get; set; }
    }
}
