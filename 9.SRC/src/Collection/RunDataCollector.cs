using CommunityStats.Config;
using CommunityStats.Util;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace CommunityStats.Collection;

/// <summary>
/// Tracks local shop data during one run and handles run-end contribution
/// persistence. All server upload logic has been removed.
///
/// New runs and resumed runs are separate entry points:
///  - <see cref="BeginNewRun"/> clears memory and never loads old files.
///  - <see cref="ResumeRun"/> clears memory and loads only the current
///    StartTime+seed run directory.
/// </summary>
public static class RunDataCollector
{
    private static readonly List<LocalShopPurchase> _shopPurchases = new();
    private static readonly List<LocalShopCardOffering> _shopCardOfferings = new();
    private static RunIdentity? _activeIdentity;
    private static string? _finalizedRunKey;

    public static RunIdentity? ActiveIdentity => _activeIdentity ?? ContributionPersistence.GetActiveIdentity();

    /// <summary>
    /// Live floor count derived from the run state's MapPointHistory.
    /// </summary>
    public static int CurrentFloor
    {
        get
        {
            try { return RunManager.Instance?.DebugOnlyGetState()?.TotalFloor ?? 0; }
            catch { return 0; }
        }
    }

    // ── Recording methods (called by patches) ───────────────

    public static void RecordShopPurchase(string itemId, string itemType, int cost, int floor)
    {
        _shopPurchases.Add(new LocalShopPurchase
        {
            ItemId = itemId,
            ItemType = itemType,
            Cost = Math.Clamp(cost, 0, 9999),
            Floor = floor
        });
        ShopPurchasePersistence.Save(_shopPurchases);
    }

    /// <summary>Read-only view used by RunHistoryAnalyzer.BuildSingleRunStats.</summary>
    public static IReadOnlyList<LocalShopPurchase> GetShopPurchases() => _shopPurchases;

    public static void RecordShopCardOffering(string cardId, int floor)
    {
        _shopCardOfferings.Add(new LocalShopCardOffering
        {
            CardId = cardId,
            Floor = floor
        });
        ShopOfferingPersistence.Save(_shopCardOfferings);
    }

    // ── Deprecated recording methods (no-ops, kept for ABI compat) ──

    public static void RecordCardReward(List<(string cardId, int upgradeLevel)> offeredCards, string? pickedCardId, int floor) { }
    public static void RecordEventChoice(string eventId, int optionIndex, int totalOptions) { }
    public static void RecordCardRemoval(string cardId, string source, int floor) { }
    public static void RecordCardUpgrade(string cardId, string source) { }

    // ── Run lifecycle ───────────────────────────────────────

    /// <summary>
    /// Start a brand-new run. Clear all in-memory state and do not load any
    /// persisted shop/live data, even when the new seed equals an old seed.
    /// </summary>
    public static void BeginNewRun(RunIdentity identity)
    {
        PrepareRun(identity, createNewDirectory: true);
        if (identity is { IsValid: true })
            Safe.Info($"[RunDataCollector] BeginNewRun {identity}");
    }

    /// <summary>
    /// Resume an existing run. Clear memory first, then load only files from
    /// the same StartTime+seed directory. A missing new directory falls back
    /// to the legacy seed layout for saves created by 1.4.4 or earlier.
    /// </summary>
    public static void ResumeRun(RunIdentity identity)
    {
        PrepareRun(identity, createNewDirectory: false);
        if (identity == null || !identity.IsValid) return;

        var savedOffers = ShopOfferingPersistence.Load(identity);
        if (savedOffers != null) _shopCardOfferings.AddRange(savedOffers);

        var savedPurchases = ShopPurchasePersistence.Load(identity);
        if (savedPurchases != null) _shopPurchases.AddRange(savedPurchases);

        Safe.Info($"[RunDataCollector] ResumeRun {identity}: purchases={_shopPurchases.Count}, offers={_shopCardOfferings.Count}");
    }

    private static void PrepareRun(RunIdentity identity, bool createNewDirectory)
    {
        if (identity == null || !identity.IsValid)
        {
            Safe.Warn("[RunDataCollector] run lifecycle called without a valid run identity");
            return;
        }

        _activeIdentity = identity;
        _finalizedRunKey = null;
        ContributionPersistence.SetActiveRun(identity);
        ContributionPersistence.SetActiveFloor(0);
        _shopPurchases.Clear();
        _shopCardOfferings.Clear();
        RunContributionAggregator.Instance.Reset();

        // Create the per-run directory immediately for new runs. This marker
        // makes a later resume use this run's (possibly empty) files and never
        // fall back to legacy same-seed shop or live data.
        if (createNewDirectory && identity.HasStartTime)
            RunStorage.EnsureDirectory(identity);
    }

    /// <summary>
    /// Called from ModManager.OnMetricsUpload hook. Saves run-summary
    /// contributions for future Run History replay. No upload.
    /// </summary>
    public static void OnMetricsUpload(SerializableRun run, bool isVictory, ulong localPlayerId)
    {
        Safe.Run(() =>
        {
            if (run == null) return;

            var identity = RunIdentity.FromSerializableRun(run);
            if (!identity.HasStartTime)
            {
                var active = ActiveIdentity;
                if (active is { HasStartTime: true })
                    identity = active;
            }
            if (!identity.IsValid)
                identity = ActiveIdentity ?? new RunIdentity(0, "");

            if (!identity.IsValid)
            {
                Safe.Warn("[RunDataCollector] OnMetricsUpload skipped: no valid run identity");
                return;
            }

            if (string.Equals(_finalizedRunKey, identity.RunKey, StringComparison.Ordinal))
            {
                Safe.Info($"[RunDataCollector] OnMetricsUpload skipped duplicate finalization: {identity}");
                return;
            }

            ContributionPersistence.SetActiveRun(identity);

            // Merge disk data with in-memory totals for contribution persistence.
            var diskTotals = ContributionPersistence.AssembleFromCombats(identity);
            if (diskTotals != null && diskTotals.Count > 0)
                RunContributionAggregator.Instance.MergeMaxFrom(diskTotals);

            ContributionPersistence.SaveRunSummary(identity, RunContributionAggregator.Instance.RunTotals);
            ContributionPersistence.DeleteLiveState(identity);
            ShopOfferingPersistence.Delete(identity);

            // Invalidate cached career snapshot so the next stats screen
            // open re-aggregates the latest data.
            RunHistoryAnalyzer.Instance.InvalidateAll();
            ContributionPersistence.ClearRunContext();
            _finalizedRunKey = identity.RunKey;
            _activeIdentity = null;
            _shopPurchases.Clear();
            _shopCardOfferings.Clear();
        });
    }
}
