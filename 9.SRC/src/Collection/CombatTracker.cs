using CommunityStats.Util;

namespace CommunityStats.Collection;

/// <summary>
/// Per-combat tracker. Records card plays, damage, defense (effective block + mitigation),
/// draws, energy, and attributes indirect contributions back to their source card/relic/potion.
/// Reset at combat start, flushed to RunContributionAggregator at combat end.
/// </summary>
public sealed class CombatTracker
{
    public static CombatTracker Instance { get; } = new();

    // sourceId → accumulated contributions
    private readonly Dictionary<string, ContributionAccum> _currentCombat = new();

    // ── P0 FIX: AsyncLocal-backed attribution context ───────────
    // Harmony Postfix on async hook methods fires at the first `await` inside the
    // method, not after completion. With plain fields this meant ClearActive* wiped
    // attribution BEFORE the real damage/block/draw commands that the hook produced
    // post-await. AsyncLocal<T> uses copy-on-write ExecutionContext flow: the value
    // set by the prefix is captured by the async state machine at suspend time, and
    // is preserved for the continuation regardless of what the (synchronous) postfix
    // does to the outer logical flow. This restores attribution for 20+ power-tick
    // and async relic hook tests (Panache, DemonForm, Juggernaut, CharonsAshes, etc.).
    private static readonly AsyncLocal<string?> _activeCardIdAL = new();
    // Fix C: origin of the currently-playing card instance (e.g. SKILL_POTION for a
    // potion-generated card). Captured at OnCardPlayStarted from GetCardOrigin(hash).
    // Used by GetOrCreate to compose per-origin bucket keys so same-name cards from
    // different sources don't collapse into a single bucket.
    private static readonly AsyncLocal<string?> _activeCardOriginAL = new();
    private static readonly AsyncLocal<string?> _activeRelicIdAL = new();
    private static readonly AsyncLocal<string?> _activePotionIdAL = new();
    private static readonly AsyncLocal<string?> _activePowerSourceIdAL = new();
    private static readonly AsyncLocal<string?> _activePowerSourceTypeAL = new();
    // 4.18 fix: also remember the power id itself (e.g. "POISON_POWER") so
    // OnDamageDealt's indirect-damage path can ask for ALL recorded sources
    // and split the tick proportionally. The single _activePowerSourceId
    // above is only the first source — without this companion field, a
    // multi-application Poison would credit the entire tick to whoever
    // applied it first.
    private static readonly AsyncLocal<string?> _activePowerIdAL = new();
    private static readonly AsyncLocal<string?> _pendingDrawSourceIdAL = new();
    private static readonly AsyncLocal<string?> _pendingDrawSourceTypeAL = new();
    // One-shot override for a power application whose normal active context
    // cannot survive an async wait. PaleBlueDot sets this before its delayed
    // DrawCardsNextTurnPower apply; OnPowerApplied consumes it.
    private static readonly AsyncLocal<(string PowerId, string SourceId, string SourceType, long ExpiresAtMs)?> _pendingPowerSourceAL = new();
    // P2-3: N-count pending draw (CentennialPuzzle draws N cards from a single hook).
    // Wrapped in a box so we can decrement across async boundaries without needing
    // to re-Set the AsyncLocal value (which would not flow back to the caller anyway).
    private static readonly AsyncLocal<System.Runtime.CompilerServices.StrongBox<int>?> _pendingDrawRemainingAL = new();
    // Round 14 v5 review §8: upgrade delta must be AsyncLocal too so that nested
    // card plays (EchoForm / Mayhem / Burst duplications) don't cross-contaminate
    // the pending upgrade fields of their parent context.
    private static readonly AsyncLocal<int> _pendingUpgradeDamageDeltaAL = new();
    private static readonly AsyncLocal<int> _pendingUpgradeBlockDeltaAL = new();
    private static readonly AsyncLocal<string?> _pendingUpgradeSourceIdAL = new();
    private static readonly AsyncLocal<string?> _pendingUpgradeSourceTypeAL = new();
    // Fix D: upgrader origin pending for the currently-playing card.
    private static readonly AsyncLocal<string?> _pendingUpgradeSourceOriginAL = new();

    // The card currently being played (set by CardPlayStarted, cleared by CardPlayFinished)
    private string? _activeCardId
    {
        get => _activeCardIdAL.Value;
        set => _activeCardIdAL.Value = value;
    }

    // Fix C: origin of the currently-playing card instance.
    private string? _activeCardOrigin
    {
        get => _activeCardOriginAL.Value;
        set => _activeCardOriginAL.Value = value;
    }
    public string? ActiveCardOrigin => _activeCardOrigin;

    // The relic currently executing a hook (set/cleared by relic hook tracking patch)
    private string? _activeRelicId
    {
        get => _activeRelicIdAL.Value;
        set => _activeRelicIdAL.Value = value;
    }

    // The potion currently being used (set by PotionContextPatch Prefix, cleared by Postfix)
    private string? _activePotionId
    {
        get => _activePotionIdAL.Value;
        set => _activePotionIdAL.Value = value;
    }

    // Public accessors for source tagging (NEW-1: local cost modifier patches need active context)
    // ActiveCardId already exposed above
    public string? ActivePotionId => _activePotionId;
    public string? ActiveRelicId => _activeRelicId;

    // The power whose hook is currently executing (for indirect effects like Rage, FlameBarrier)
    private string? _activePowerSourceId
    {
        get => _activePowerSourceIdAL.Value;
        set => _activePowerSourceIdAL.Value = value;
    }
    private string? _activePowerSourceType
    {
        get => _activePowerSourceTypeAL.Value;
        set => _activePowerSourceTypeAL.Value = value;
    }
    private string? _activePowerId
    {
        get => _activePowerIdAL.Value;
        set => _activePowerIdAL.Value = value;
    }

    // Track DamageResult objects already processed by DamageReceived,
    // so KillingBlowPatcher doesn't double-count them.
    private readonly HashSet<int> _processedDamageResults = new();

    // Pending upgrade delta for the currently playing card (per-hit split, per Review R3).
    // Round 14 v5 Fix 6: also carry the UPGRADE SOURCE so that UpgradeDamage/UpgradeBlock
    // credits the card that performed the upgrade (e.g. ARMAMENTS), not the triggering card
    // (e.g. STRIKE_IRONCLAD) being played.
    // Round 14 v5 review §8: AsyncLocal-backed so nested card plays don't cross-contaminate.
    private int _pendingUpgradeDamageDelta
    {
        get => _pendingUpgradeDamageDeltaAL.Value;
        set => _pendingUpgradeDamageDeltaAL.Value = value;
    }
    private int _pendingUpgradeBlockDelta
    {
        get => _pendingUpgradeBlockDeltaAL.Value;
        set => _pendingUpgradeBlockDeltaAL.Value = value;
    }
    private string? _pendingUpgradeSourceId
    {
        get => _pendingUpgradeSourceIdAL.Value;
        set => _pendingUpgradeSourceIdAL.Value = value;
    }
    private string? _pendingUpgradeSourceType
    {
        get => _pendingUpgradeSourceTypeAL.Value;
        set => _pendingUpgradeSourceTypeAL.Value = value;
    }
    private string? _pendingUpgradeSourceOrigin
    {
        get => _pendingUpgradeSourceOriginAL.Value;
        set => _pendingUpgradeSourceOriginAL.Value = value;
    }

    // H3: Deferred Osty death negative defense — only applied when enemy next attacks player
    private (string sourceId, string sourceType, int amount)? _pendingOstyDeathDefense;

    // R2: Regal Pillow's +15 rest-site heal is part of the same Heal event.
    // Split the recorded amount without adding a second full heal.
    private (string sourceId, string sourceType, int amount)? _pendingRestHealBonus;
    // Idempotency guard: a single Osty death can be observed by both the
    // single-creature and collection Kill overloads (and duplicated by nested
    // prefixes). Reset on combat start and whenever Osty is summoned again.
    private bool _ostyDeathHandled;

    // Forge tracking: each ForgeCmd.Forge call records (sourceId, sourceType, amount).
    // When SovereignBlade plays, these are written as sub-bar entries under SOVEREIGN_BLADE.
    private readonly List<(string sourceId, string sourceType, int amount)> _forgeLog = new();

    // Event fired after card play or potion use completes (for real-time UI refresh)
    public event Action? CombatDataUpdated;

    // Current encounter info
    private string _encounterId = "";
    private string _encounterType = "";
    private int _damageTakenByPlayer;
    private int _turnCount;
    private bool _playerDied;
    private int _floor;
    private int _totalDamageDealt;

    // Snapshot of the last combat (for UI display)
    private Dictionary<string, ContributionAccum>? _lastCombatData;
    private string _lastEncounterId = "";

    // Live-state save throttling. A card play can trigger several tracker
    // events; each one used to serialize the entire run snapshot. Keep one
    // dirty flag and one 1 s timer instead, and force a flush at combat end.
    private const double LiveSaveDebounceSeconds = 1.0;
    private bool _liveSaveDirty;
    private bool _liveSavePending;
    private bool _liveCombatInProgress;
    // Actual cards drawn by the hand-draw path this turn. The first five
    // are the base draw; only later cards consume pending hand-draw bonuses.
    private int _handDrawCount;
    // Nested card plays: save the outer card context and restore it when the
    // inner play finishes.
    private readonly AsyncLocal<Stack<(string? CardId, string? CardOrigin)>?> _cardContextStackAL = new();
    // A single combat can reach the flush path more than once (victory fires
    // CombatRoom.OnCombatEnded and later CombatManager.CombatEnded; defeat only
    // fires the latter). Flush and display exactly once, and keep the flag true
    // across the whole post-combat heal window so AfterCombatVictory healing
    // still lands on the combat that just ended.
    private bool _combatEndHandled;

    public IReadOnlyDictionary<string, ContributionAccum>? LastCombatData => _lastCombatData;
    public string LastEncounterId => _lastEncounterId;
    public string? CurrentEncounterId => string.IsNullOrEmpty(_encounterId) ? null : _encounterId;

    /// <summary>
    /// True from OnCombatStart until OnCombatEnd flushes the combat. Unlike
    /// CombatManager.IsInProgress this stays true during AfterCombatVictory,
    /// where post-combat relic heals are still part of the just-ended fight.
    /// </summary>
    public bool LiveCombatInProgress => _liveCombatInProgress;

    /// <summary>
    /// Snapshot of the non-card attribution contexts. A nested relic/power
    /// hook saves this before setting its own context and restores it in the
    /// postfix, so the outer hook's later effects keep their source.
    /// </summary>
    public readonly record struct AttributionContext(
        string? CardId,
        string? CardOrigin,
        string? RelicId,
        string? PotionId,
        string? PowerSourceId,
        string? PowerSourceType,
        string? PowerId);

    public AttributionContext CaptureAttributionContext() => new(
        _activeCardId,
        _activeCardOrigin,
        _activeRelicId,
        _activePotionId,
        _activePowerSourceId,
        _activePowerSourceType,
        _activePowerId);

    public void RestoreAttributionContext(AttributionContext context)
    {
        _activeCardId = context.CardId;
        _activeCardOrigin = context.CardOrigin;
        _activeRelicId = context.RelicId;
        _activePotionId = context.PotionId;
        _activePowerSourceId = context.PowerSourceId;
        _activePowerSourceType = context.PowerSourceType;
        _activePowerId = context.PowerId;
    }

    // Public DPS accessors (PRD 3.7)
    public int TurnCount => _turnCount;
    public int TotalDamageDealt => _totalDamageDealt;

    /// <summary>
    /// Fire CombatDataUpdated event for real-time UI refresh. Called from
    /// CombatHistoryPatch.AfterCardPlayFinished. Round 8: also persist a
    /// live snapshot to disk so save+quit doesn't lose 本场战斗 data.
    /// </summary>
    public void NotifyCombatDataUpdated()
    {
        // Coalesce live persistence. The actual snapshot is built at flush
        // time, so the file always contains the newest state and one busy
        // card-play sequence does not write dozens of JSON files.
        // Once a combat is flushed, a late UI refresh must not make the tracker
        // look in-combat again (that would misroute post-combat heals).
        if (!_combatEndHandled) _liveCombatInProgress = true;
        _liveSaveDirty = true;
        ScheduleLiveSave();

        try { CombatDataUpdated?.Invoke(); }
        catch (Exception ex) { Godot.GD.Print($"[StatsTheSpire] CombatDataUpdated error: {ex.Message}"); }
    }

    /// <summary>
    /// Queue one live-state write after a short debounce. Only one timer is
    /// active at a time; the timer reads the latest state when it fires.
    /// </summary>
    private void ScheduleLiveSave()
    {
        if (_liveSavePending) return;
        var tree = Godot.Engine.GetMainLoop() as Godot.SceneTree;
        if (tree == null)
        {
            FlushLiveState();
            return;
        }

        _liveSavePending = true;
        var timer = tree.CreateTimer(LiveSaveDebounceSeconds, true, false, false);
        timer.Timeout += () =>
        {
            _liveSavePending = false;
            if (_liveSaveDirty) FlushLiveState(_liveCombatInProgress);
        };
    }

    /// <summary>
    /// Force the pending live snapshot to disk. Called at combat end, before
    /// save+quit, and by tests that need deterministic persistence.
    /// </summary>
    public void FlushLiveState() => FlushLiveState(_liveCombatInProgress);

    private void FlushLiveState(bool combatInProgress)
    {
        if (!_liveSaveDirty) return;
        _liveSaveDirty = false;
        try
        {
            var snap = BuildLiveSnapshot(combatInProgress);
            Util.ContributionPersistence.SaveLiveState(snap);
        }
        catch (Exception ex)
        {
            _liveSaveDirty = true;
            Godot.GD.Print($"[StatsTheSpire] SaveLiveState error: {ex.Message}");
        }
    }

    /// <summary>
    /// Build a snapshot of the current tracker state for disk persistence.
    /// </summary>
    public LiveContributionSnapshot BuildLiveSnapshot(bool combatInProgress)
    {
        var agg = RunContributionAggregator.Instance;
        return new LiveContributionSnapshot
        {
            EncounterId = _encounterId ?? "",
            EncounterType = _encounterType ?? "",
            Floor = _floor,
            TurnCount = _turnCount,
            TotalDamageDealt = _totalDamageDealt,
            DamageTakenByPlayer = _damageTakenByPlayer,
            CombatInProgress = combatInProgress,
            CurrentCombat = combatInProgress
                ? new Dictionary<string, ContributionAccum>(_currentCombat)
                : null,
            RunTotals = new Dictionary<string, ContributionAccum>(agg.RunTotals),
            Encounters = new List<RunContributionAggregator.EncounterRecord>(agg.Encounters),
        };
    }

    /// <summary>
    /// Restore tracker state from a live snapshot loaded at mod boot.
    /// Called from CommunityStatsMod.Initialize when a `_live.json` file
    /// matches the active run's seed.
    /// </summary>
    public void HydrateFromLiveSnapshot(LiveContributionSnapshot snap)
    {
        if (snap == null) return;
        _encounterId = snap.EncounterId ?? "";
        _encounterType = snap.EncounterType ?? "";
        _floor = snap.Floor;
        _turnCount = snap.TurnCount;
        _totalDamageDealt = snap.TotalDamageDealt;
        _damageTakenByPlayer = snap.DamageTakenByPlayer;
        _liveCombatInProgress = snap.CombatInProgress;

        if (snap.CombatInProgress && snap.CurrentCombat != null)
        {
            _currentCombat.Clear();
            foreach (var (k, v) in snap.CurrentCombat) _currentCombat[k] = v;
        }
        else
        {
            // Combat was not in progress at save time → restore to LastCombatData
            // so the panel still shows the most recent fight.
            _lastCombatData = snap.CurrentCombat ?? new Dictionary<string, ContributionAccum>();
            _lastEncounterId = snap.EncounterId ?? "";
        }

        if (snap.RunTotals != null)
            RunContributionAggregator.Instance.HydrateRunTotals(snap.RunTotals);

        if (snap.Encounters != null && snap.Encounters.Count > 0)
            RunContributionAggregator.Instance.HydrateEncounters(snap.Encounters);
    }

    // ── Lifecycle ────────────────────────────────────────────

    public void OnCombatStart(string encounterId, string encounterType, int floor)
    {
        _currentCombat.Clear();
        _activeCardId = null;
        _activeRelicId = null;
        _activePotionId = null;
        _activePowerSourceId = null;
        _activePowerSourceType = null;
        _activePowerId = null;
        _encounterId = encounterId;
        _encounterType = encounterType;
        _damageTakenByPlayer = 0;
        _turnCount = 0;
        _totalDamageDealt = 0;
        _playerDied = false;
        _floor = floor;
        _pendingOstyDeathDefense = null;
        _pendingRestHealBonus = null;
        _ostyDeathHandled = false;
        _pendingUpgradeDamageDelta = 0;
        _pendingUpgradeBlockDelta = 0;
        _pendingUpgradeSourceId = null;
        _pendingUpgradeSourceType = null;
        _pendingUpgradeSourceOrigin = null;
        _processedDamageResults.Clear();
        _forgeLog.Clear();
        _handDrawCount = 0;
        _cardContextStackAL.Value = null;
        ContributionMap.Instance.ClearPendingHandDrawBonus();
        _liveSaveDirty = false;
        _liveCombatInProgress = true;
        _combatEndHandled = false;
        ClearPendingDrawSource();
        ClearPendingPowerSource();
        ContributionMap.Instance.Clear();
    }

    public void OnCombatEnd()
    {
        // Victory and defeat can both reach this method; only the first call
        // owns the snapshot, aggregator flush, and live-save teardown.
        if (_combatEndHandled) return;
        _combatEndHandled = true;

        // Snapshot for UI
        _lastCombatData = new Dictionary<string, ContributionAccum>(_currentCombat);
        _lastEncounterId = _encounterId;

        // Flush to run-level aggregator
        RunContributionAggregator.Instance.AddCombat(
            _encounterId, _encounterType, _floor,
            _damageTakenByPlayer, _turnCount, _playerDied,
            _currentCombat);

        _currentCombat.Clear();
        _handDrawCount = 0;
        ContributionMap.Instance.ClearPendingHandDrawBonus();
        ClearPendingDrawSource();
        ClearPendingPowerSource();
        ContributionMap.Instance.Clear();
        _liveCombatInProgress = false;
        _liveSaveDirty = true;
        FlushLiveState(combatInProgress: false);
    }

    // ── Damage dedup (for KillingBlowPatcher) ────────────────
    public void MarkDamageResultProcessed(int resultHash) => _processedDamageResults.Add(resultHash);
    public bool IsDamageResultProcessed(int resultHash) => _processedDamageResults.Contains(resultHash);

    // ── Exposed Context (for patches that need to read current state) ──

    public string? ActiveCardId => _activeCardId;
    public string? ActivePowerSourceId => _activePowerSourceId;
    public string? ActivePowerSourceType => _activePowerSourceType;
    public string? ActivePowerId => _activePowerId;

    // ── Card Play Tracking ──────────────────────────────────

    public void OnCardPlayStarted(string cardId, int cardHash)
    {
        var stack = _cardContextStackAL.Value ??= new Stack<(string?, string?)>();
        stack.Push((_activeCardId, _activeCardOrigin));
        _activeCardId = cardId;
        // Safety: clear stale potion context (shouldn't still be set, but guards against
        // edge cases where PotionUsed didn't fire, e.g. combat ended mid-potion)
        _activePotionId = null;
        // Fix C: resolve origin BEFORE GetOrCreate so the bucket key routes correctly
        // on first write. A deck-native BATTLE_TRANCE and a potion-generated BATTLE_TRANCE
        // now land in distinct buckets instead of colliding by plain cardId.
        var origin = ContributionMap.Instance.GetCardOrigin(cardHash);
        _activeCardOrigin = origin?.originId;
        var accum = GetOrCreate(cardId, "card");
        accum.TimesPlayed++;
        // Reset orb first-trigger flag so first orb trigger during this card play
        // goes to channeling source, subsequent triggers go to this card.
        ContributionMap.Instance.ResetOrbFirstTrigger();
        // Load upgrade delta for per-hit split (C2: activated upgrade tracking)
        var upgDelta = ContributionMap.Instance.GetUpgradeDelta(cardHash);
        _pendingUpgradeDamageDelta = upgDelta?.DamageDelta ?? 0;
        _pendingUpgradeBlockDelta = upgDelta?.BlockDelta ?? 0;
        _pendingUpgradeSourceId = upgDelta?.SourceId;
        _pendingUpgradeSourceType = upgDelta?.SourceType;
        _pendingUpgradeSourceOrigin = upgDelta?.UpgraderOrigin;
    }

    public void OnCardPlayFinished()
    {
        var stack = _cardContextStackAL.Value;
        if (stack != null && stack.Count > 0)
        {
            var (outerId, outerOrigin) = stack.Pop();
            _activeCardId = outerId;
            _activeCardOrigin = outerOrigin;
        }
        else
        {
            _activeCardId = null;
            _activeCardOrigin = null;
        }
        // Round 14 v5 review §9: defensive clear of pending upgrade state at end
        // of each card play. OnCardPlayStarted already overwrites these each time,
        // so this is strictly defense-in-depth for any future code path that might
        // query them between play events.
        _pendingUpgradeDamageDelta = 0;
        _pendingUpgradeBlockDelta = 0;
        _pendingUpgradeSourceId = null;
        _pendingUpgradeSourceType = null;
        _pendingUpgradeSourceOrigin = null;
    }

    // ── Relic Context Tracking ──────────────────────────────

    public void SetActiveRelic(string relicId) => _activeRelicId = relicId;
    public void ClearActiveRelic() => _activeRelicId = null;

    // ── Potion Context Tracking ─────────────────────────────

    public void SetActivePotion(string potionId) => _activePotionId = potionId;
    public void ClearActivePotion() => _activePotionId = null;

    // ── Power Source Context Tracking ───────────────────────
    // Set when a Power's hook method is executing (e.g., RagePower.AfterCardPlayed,
    // FlameBarrierPower.AfterDamageReceived). This lets indirect effects (damage/block
    // caused by the power) be attributed back to the card that originally applied the power.

    public void SetActivePowerSource(string powerId)
    {
        // 4.18 fix: cache the raw powerId in addition to the resolved single
        // source. OnDamageDealt's indirect path uses _activePowerId to ask
        // ContributionMap for ALL recorded sources of this power and split
        // the tick proportionally, restoring credit to every card that
        // applied it (Poison from a Strike + Necronomicon stack, etc.).
        _activePowerId = powerId;
        var source = ContributionMap.Instance.GetPowerSource(powerId);
        if (source != null)
        {
            _activePowerSourceId = source.SourceId;
            _activePowerSourceType = source.SourceType;
        }
        else
        {
            // An unresolved power must not inherit the previous power's
            // source; otherwise a hook on a power applied by an untracked
            // path credits whoever happened to fire last.
            _activePowerSourceId = null;
            _activePowerSourceType = null;
        }
    }

    /// <summary>
    /// Set the active power-source context for an enemy-side debuff hook
    /// (Strangle, Poison, Slowed, etc) where the resolved source comes from
    /// the per-enemy <c>_debuffLayers</c> table rather than the global
    /// <c>_powerSources</c> map. <see cref="SetActivePowerSource"/> can't
    /// see those entries because enemy debuffs don't pollute the global
    /// table (Fix 3.1). Callers pass the FIFO head source they fetched via
    /// <see cref="ContributionMap.GetDebuffHeadSource"/>.
    /// </summary>
    public void SetActivePowerSourceManual(string powerId, string sourceId, string sourceType)
    {
        _activePowerId = powerId;
        _activePowerSourceId = sourceId;
        _activePowerSourceType = sourceType;
    }

    public void ClearActivePowerSource()
    {
        _activePowerSourceId = null;
        _activePowerSourceType = null;
        _activePowerId = null;
    }

    /// <summary>Force-clear ALL context. Called between tests to prevent stale state.
    /// Round 14: also clears the ContributionMap lookup state (power sources, block pool,
    /// debuff layers, etc.) which previously leaked between tests and caused
    /// (a) off-by-one in multi-source modifier distribution (P1) and
    /// (b) Defend/Potion/Power zero-attribution from stale pool/source pollution (P3).
    /// Does NOT clear CombatTracker._currentCombat — tests use delta snapshots.</summary>
    public void ForceResetAllContext()
    {
        _activeCardId = null;
        _activeCardOrigin = null;
        _activePotionId = null;
        _activeRelicId = null;
        _activePowerSourceId = null;
        _activePowerSourceType = null;
        _activePowerId = null;
        _pendingDrawSourceId = null;
        _pendingDrawSourceType = null;
        _pendingDrawRemainingAL.Value = null;
        _handDrawCount = 0;
        _cardContextStackAL.Value = null;
        ContributionMap.Instance.ClearPendingHandDrawBonus();
        _pendingOstyDeathDefense = null;
        _ostyDeathHandled = false;
        _pendingUpgradeDamageDelta = 0;
        _pendingUpgradeBlockDelta = 0;
        _pendingUpgradeSourceId = null;
        _pendingUpgradeSourceType = null;
        _pendingUpgradeSourceOrigin = null;
        _processedDamageResults.Clear();
        _forgeLog.Clear();
        _liveSaveDirty = false;
        ClearPendingPowerSource();
        ContributionMap.Instance.Clear();
    }

    // ── Turn Tracking ───────────────────────────────────────

    public void OnTurnStart()
    {
        _turnCount++;
        // A trigger that never produced a power application must not survive
        // into the next turn and steal a different draw's attribution.
        ClearPendingPowerSource();
        // Pending draw sources are one-shot: a hook that did not actually draw
        // must not poison the next hand draw or the next non-hand draw.
        ClearPendingDrawSource();
        // The hand-draw window is complete; discard any unspent planned bonus
        // so a hand-limit truncation cannot credit a later draw.
        _handDrawCount = 0;
        ContributionMap.Instance.ClearPendingHandDrawBonus();
    }

    // ── Resolve Source ───────────────────────────────────────
    // Fix 3 (Round 14 v5): power source now has HIGHER priority than card.
    // Rationale: when a power hook fires DURING card play (e.g. FeelNoPain's
    // AfterCardExhausted triggered while playing TrueGrit, or Juggernaut's
    // AfterBlockGained triggered while playing Defend), the downstream
    // contribution (block/damage/draw) originates from the POWER, not the
    // card that triggered the hook. Without this swap, TRUE_GRIT would
    // capture FEEL_NO_PAIN's 3 block, DEFEND_IC would capture JUGGERNAUT's
    // 5 damage, STRIKE_IRONCLAD would capture ENVENOM's poison apply, etc.
    //
    // Safety:
    //   - Normal card play: only _activeCardId is set → card branch runs.
    //   - Turn-start/end power hook: _activeCardId is null → power branch
    //     runs (same as before).
    //   - Power hook during card play: both set → power wins (the fix).
    //   - Explicit cardSourceId arg: still highest priority (caller knows best).
    //   - Orb context: still above power for orb passive/evoke damage.

    private (string id, string type) ResolveSource(string? cardSourceId)
    {
        if (cardSourceId != null)
            return (cardSourceId, "card");
        // Orb context takes priority (orb passive/evoke damage always wins)
        var orbCtx = ContributionMap.Instance.ActiveOrbContext;
        if (orbCtx != null)
            return (orbCtx.Value.sourceId, orbCtx.Value.sourceType);
        // Round 14 v5 Fix 3.6: all non-card trigger contexts (power/relic/potion)
        // take priority over card. When a relic hook fires DURING card play
        // (e.g. CharonsAshes.AfterCardExhausted, Kusarigama.AfterCardPlayed,
        // Nunchaku.AfterCardPlayed, GamePiece.AfterCardPlayed, etc.) the
        // contribution originates from the RELIC, not the card that triggered
        // the hook. Same rationale as power > card (Fix 3): prefix/postfix
        // scoping guarantees non-card contexts are only set while their own
        // hook is actively producing an effect.
        if (_activePowerSourceId != null)
            return (_activePowerSourceId, _activePowerSourceType ?? "card");
        if (_activeRelicId != null)
            return (_activeRelicId, "relic");
        if (_activePotionId != null)
            return (_activePotionId, "potion");
        if (_activeCardId != null)
            return (_activeCardId, "card");
        // No context found
        Godot.GD.Print($"[CommunityStats] WARN ResolveSource: no active context, using UNTRACKED");
        return ("UNTRACKED", "untracked");
    }

    // ── Damage Dealt to Enemies ─────────────────────────────

    /// <summary>
    /// Called when any creature takes damage.
    /// For damage to enemies: attributes to card/relic source, with modifier bonuses split out.
    /// For damage to player: tracks for encounter stats + block attribution + str reduction mitigation.
    /// </summary>
    public void OnDamageDealt(int totalDamage, int blockedDamage, string? cardSourceId,
        bool isPlayerReceiver, int targetHash, int dealerHash,
        bool isOstyDealer = false, bool isOstyReceiver = false,
        bool skipEnemyDamageAttribution = false)
    {
        // Osty receiving damage = absorbing for player → defense contribution
        if (isOstyReceiver && !isPlayerReceiver)
        {
            int hpLost = totalDamage; // totalDamage on Osty = HP lost by Osty
            OnOstyAbsorbedDamage(hpLost);
            return;
        }

        // Osty dealing damage to enemies → sub-bar under OSTY parent
        if (isOstyDealer && !isPlayerReceiver && cardSourceId != null)
        {
            OnOstyDamageDealt(totalDamage, cardSourceId);
            return;
        }

        if (isPlayerReceiver)
        {
            // H3: Apply deferred Osty death negative defense on first enemy damage to player
            if (_pendingOstyDeathDefense != null && dealerHash != targetHash)
            {
                var (ostySourceId, ostySourceType, ostyAmount) = _pendingOstyDeathDefense.Value;
                GetOrCreate(ostySourceId, ostySourceType).EffectiveBlock -= ostyAmount;
                _pendingOstyDeathDefense = null;
            }

            _damageTakenByPlayer += totalDamage;

            // Self-damage: player dealing damage to themselves via a card (Bloodletting, Offering, etc.)
            // Record as negative defense contribution. Two detection paths:
            //   (1) explicit self-target (dealerHash == targetHash) — Bloodletting path
            //   (2) P2-6: no dealer + cardSource active + player is receiver — Offering path.
            //       CreatureCmd.Damage(this, owner.Creature, ..., this) with no dealer
            //       arg goes through DamageReceived with dealer=null (hash=0) but
            //       cardSource=Offering. Still a self-hit, still should count as SelfDamage.
            int unblockedSelf = totalDamage - blockedDamage;
            bool isExplicitSelfHit = dealerHash != 0 && dealerHash == targetHash;
            bool isCardSelfHit = dealerHash == 0 && cardSourceId != null;
            if ((isExplicitSelfHit || isCardSelfHit) && unblockedSelf > 0)
            {
                var (srcId, srcType) = ResolveSource(cardSourceId);
                GetOrCreate(srcId, srcType).SelfDamage += unblockedSelf;
            }

            // Attribute blocked damage to block sources via FIFO pool.
            // Round 14 v5: use detailed consume so modifier slices
            // (Dex/Focus/Footwork) credit ModifierBlock, not EffectiveBlock,
            // and only when the chunk is actually consumed — matching the
            // existing invariant for EffectiveBlock.
            if (blockedDamage > 0)
            {
                var slices = ContributionMap.Instance.ConsumeBlockDetailed(blockedDamage);
                foreach (var slice in slices)
                {
                    if (slice.IsModifier)
                        GetOrCreate(slice.SourceId, slice.SourceType).ModifierBlock += slice.Amount;
                    else
                        GetOrCreate(slice.SourceId, slice.SourceType).EffectiveBlock += slice.Amount;
                }
            }

            // Attribute enemy strength reduction as defense contribution.
            //
            // Correct cap per-hit is NOT `min(totalReduction, rawBase)` — that
            // undercounts when the enemy had positive Strength before our
            // reduction (e.g. buffed +2 Str, we apply Piercing Wail -8). In
            // that case rawBase alone is smaller than the damage we actually
            // prevented: the enemy would have dealt `base + Str_buff` per hit;
            // with our reduction it now deals `max(0, base + Str_buff - 8)`.
            //
            // Reconstruct: "Str that would exist without our recorded
            // reductions" = currentStr + totalReduction.  (currentStr already
            // reflects our reduction because it's captured at hit time.)
            // Hypothetical damage per hit without our reductions =
            //   max(0, base + currentStr + totalReduction).
            // Our reduction could only prevent up to that hypothetical amount,
            // AND only up to the raw totalReduction magnitude (if base + Str
            // > totalReduction, we still only prevented totalReduction).
            //
            // effective = min(totalReduction, hypothetical)
            if (dealerHash != 0)
            {
                var reductions = ContributionMap.Instance.GetStrReductions(dealerHash);
                if (reductions != null && reductions.Count > 0)
                {
                    int totalReduction = 0;
                    foreach (var r in reductions) totalReduction += r.Amount;
                    if (totalReduction > 0)
                    {
                        int enemyBase = ContributionMap.Instance.PendingEnemyBaseDamage;
                        int currentStr = ContributionMap.Instance.PendingEnemyCurrentStr;
                        int hypothetical = Math.Max(0, enemyBase + currentStr + totalReduction);
                        int effectiveReduction = Math.Min(totalReduction, hypothetical);
                        foreach (var entry in reductions)
                        {
                            int share = (int)Math.Round((float)entry.Amount / totalReduction * effectiveReduction);
                            if (share > 0)
                                GetOrCreate(entry.SourceId, entry.SourceType).MitigatedByStrReduction += share;
                        }
                    }
                }
            }
            return;
        }

        // Invincible-phase guard: some bosses (Doormaker phase 1 "DOOR",
        // Waterfall Giant's AboutToBlow self-destruct phase, etc.) set
        // Creature.ShowsInfiniteHp = true and pin their HP at 999999999 so
        // the player cannot shorten those phases by dealing damage. Damage
        // numbers still flow through CombatHistory.DamageReceived, but they
        // have no gameplay effect. Treating them as real contribution would
        // inflate TotalDamageDealt + per-source DirectDamage by thousands of
        // points for a single invincible turn. Skip all attribution below.
        if (skipEnemyDamageAttribution) return;

        // Damage dealt TO enemies — track DPS (PRD 3.7) and attribute to source
        _totalDamageDealt += totalDamage;

        // First, consume damage modifier context from Hook.ModifyDamageInternal patch
        var modifiers = ContributionMap.Instance.LastDamageModifiers;
        int modifierTotal = 0;
        if (modifiers.Count > 0)
        {
            foreach (var mod in modifiers)
            {
                if (mod.Amount > 0)
                {
                    modifierTotal += mod.Amount;
                }
            }
        }

        // PRD-04 §4.1 — multi-multiplicative modifier scaling.
        // When multiple independent multiplicative modifiers stack (e.g. Vulnerable ×1.5
        // and DoubleDamage ×2 on Strike 6 → total 18, raw modifier votes 6 + 9 = 15 > 12),
        // their per-modifier contribution sum can exceed (totalDamage - baseDamage).
        // We don't have baseDamage here, so enforce the weaker invariant
        //   sum(ModifierDamage) <= totalDamage
        // by proportionally scaling down each modifier's vote when overshoot is detected.
        // This preserves the constraint DirectDamage + ModifierDamage = TotalDamage and
        // never reports a negative directDamage.
        if (modifierTotal > totalDamage && modifierTotal > 0 && totalDamage >= 0)
        {
            float scale = (float)totalDamage / modifierTotal;
            int scaledTotal = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if (m.Amount <= 0) continue;
                int scaledAmount = (int)Math.Round(m.Amount * scale);
                modifiers[i] = new ContributionMap.ModifierContribution(m.SourceId, m.SourceType, scaledAmount);
                scaledTotal += scaledAmount;
            }
            modifierTotal = scaledTotal;
        }

        int directDamage = Math.Max(0, totalDamage - modifierTotal);

        // SeekingEdge split: non-primary targets get damage attributed to SeekingEdge source
        bool seekingRedirect = false;
        if (ContributionMap.Instance.IsSeekingEdgeActive
            && cardSourceId == "SOVEREIGN_BLADE"
            && targetHash != ContributionMap.Instance.SeekingEdgePrimaryTarget)
        {
            var seekingSource = ContributionMap.Instance.GetPowerSource("SEEKING_EDGE_POWER");
            if (seekingSource != null)
            {
                GetOrCreate(seekingSource.SourceId, seekingSource.SourceType).DirectDamage += directDamage;
                // Also redirect modifier damage to SeekingEdge source
                if (modifiers.Count > 0)
                {
                    foreach (var mod in modifiers)
                    {
                        if (mod.Amount > 0)
                            GetOrCreate(seekingSource.SourceId, seekingSource.SourceType).ModifierDamage += mod.Amount;
                    }
                }
                seekingRedirect = true;
            }
        }

        if (!seekingRedirect)
        {
            // Normal attribution
            if (modifiers.Count > 0)
            {
                foreach (var mod in modifiers)
                {
                    if (mod.Amount > 0)
                        GetOrCreate(mod.SourceId, mod.SourceType).ModifierDamage += mod.Amount;
                }
            }

            // Orb value contributions (Focus + relics like Infused Core).
            foreach (var c in ContributionMap.Instance.ConsumeOrbValueContribs())
            {
                int amount = Math.Min(c.amount, directDamage);
                if (amount > 0)
                {
                    GetOrCreate(c.sourceId, c.sourceType).ModifierDamage += amount;
                    directDamage -= amount;
                }
            }

            var (sourceId2, sourceType2) = ResolveSource(cardSourceId);

            // Per-hit upgrade delta split (C2): each hit of a multi-hit card gets its upgrade bonus.
            // Fix 6: attribute the upgrade bonus to the UPGRADE SOURCE (e.g. ARMAMENTS),
            // not the triggering card (sourceId2 = STRIKE_IRONCLAD). This restores the correct
            // chain "the card that upgraded is credited for the extra damage it enabled".
            if (_pendingUpgradeDamageDelta > 0 && directDamage > 0)
            {
                int upgSplit = Math.Min(_pendingUpgradeDamageDelta, directDamage);
                string upgSrcId = _pendingUpgradeSourceId ?? sourceId2;
                string upgSrcType = _pendingUpgradeSourceType ?? sourceType2;
                // Fix D: route upgrade bonus to upgrader's origin bucket so SKILL_POTION
                // Armaments and deck Armaments don't collide on a shared ARMAMENTS bucket.
                GetOrCreate(upgSrcId, upgSrcType, originOverride: _pendingUpgradeSourceOrigin).UpgradeDamage += upgSplit;
                directDamage -= upgSplit;
            }

            // Indirect damage (poison, thorns, orbs, power-hook-triggered) → AttributedDamage
            // Direct damage (cards, relics, potions on play) → DirectDamage
            // Fix 3: if _activePowerSourceId is set, the damage ORIGINATES from
            // a power hook (not the card/relic that may also be active), so it
            // qualifies as indirect regardless of card context.
            bool hasOrbContext = ContributionMap.Instance.ActiveOrbContext != null;
            bool isIndirect = hasOrbContext
                || (cardSourceId == null && _activePowerSourceId != null);

            if (isIndirect)
            {
                // PRD I1 — multi-source stack debuff (Poison/etc): split the
                // tick by current per-source layer.Duration. Each existing
                // stack contributes 1 damage to this tick, and AfterSetAmount's
                // FIFO DecrementDebuffLayers ensures expired stacks no longer
                // contribute on subsequent turns. _debuffLayers is keyed by
                // (enemy_hash, powerId) so each enemy carries its own
                // multi-source FIFO stack — the targetHash here is the
                // self-damaged enemy.
                //
                // Orb-channelled damage skips this path: orb context already
                // names the originating card uniquely (the channeling card).
                // Player buff hooks (Rage / Juggernaut / etc) also fall
                // through naturally — _debuffLayers won't have an entry for
                // those power ids on the enemy, so fractions.Count==0 and we
                // route through the existing single-source resolver.
                bool distributed = false;
                if (!hasOrbContext && _activePowerId != null && directDamage > 0)
                {
                    // Try debuff-layer fractions first (Poison, Doom — per-enemy stacks)
                    var fractions = ContributionMap.Instance.GetDebuffSourceFractions(
                        targetHash, _activePowerId);
                    if (fractions.Count > 1)
                    {
                        int allocated = 0;
                        for (int i = 0; i < fractions.Count; i++)
                        {
                            int share = (i == fractions.Count - 1)
                                ? directDamage - allocated
                                : (int)Math.Round(directDamage * fractions[i].Fraction);
                            if (share > 0)
                                GetOrCreate(fractions[i].SourceId, fractions[i].SourceType).AttributedDamage += share;
                            allocated += share;
                        }
                        distributed = true;
                    }

                    // Fall back to global power-source distribution for player
                    // buffs with multiple sources (Thorns: BronzeScales + Abrasion,
                    // FlameBarrier from multiple relics/cards, etc.)
                    if (!distributed)
                    {
                        var powerSources = ContributionMap.Instance.GetPowerSources(_activePowerId);
                        if (powerSources != null && powerSources.Count > 1)
                        {
                            var dist = ContributionMap.Instance.DistributeByPowerSources(
                                _activePowerId, directDamage);
                            foreach (var (sid, stype, share) in dist)
                            {
                                if (share > 0)
                                    GetOrCreate(sid, stype).AttributedDamage += share;
                            }
                            distributed = true;
                        }
                    }
                }
                if (!distributed)
                    GetOrCreate(sourceId2, sourceType2).AttributedDamage += directDamage;
            }
            else
            {
                GetOrCreate(sourceId2, sourceType2).DirectDamage += directDamage;
            }
        }

        modifiers.Clear();
    }

    // ── Defense: Weak on enemies ────────────────────────────

    public void OnWeakMitigation(int actualDamage, int dealerHash, float weakMultiplier = 0.75f)
    {
        if (actualDamage <= 0) return;

        // Fix-2: Use actual multiplier to compute prevented damage
        // prevented = damage_before_weak - damage_after_weak = actualDamage/mult - actualDamage
        int prevented = (int)Math.Round(actualDamage / weakMultiplier - actualDamage);
        if (prevented <= 0) return;

        // PRD DEF-2c — FIFO head-source attribution: Weak is a boolean debuff
        // (single 0.75x multiplier regardless of stack count), so the entire
        // turn's mitigation belongs to whichever source's layer is at the
        // queue head. Pro-rating by stack count would credit a later applier
        // for damage Weak prevented before that source even existed.
        var head = ContributionMap.Instance.GetDebuffHeadSource(dealerHash, "WEAK_POWER");
        if (head != null)
            GetOrCreate(head.SourceId, head.SourceType).MitigatedByDebuff += prevented;
    }

    /// <summary>
    /// 4.18 fix: attribute a pre-computed slice of prevented damage to a
    /// specific debuff layer source. Used by AfterModifyDamage_Enemy when
    /// iterating enemy-side debuff power modifiers — we pre-split the
    /// `damage that would have been dealt without this modifier` by the
    /// FIFO layer fractions and call this per slice. Keeps WEAK_POWER
    /// mitigation on its separate OnWeakMitigation path while supporting
    /// any other duration-based debuff (Slowed, etc.) on this unified path.
    /// </summary>
    public void OnDebuffPrevention(string sourceId, string sourceType, int amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(sourceId)) return;
        GetOrCreate(sourceId, sourceType).MitigatedByDebuff += amount;
    }

    // ── Defense: Buffer / Intangible ────────────────────────

    public void OnBufferPrevention(int preventedDamage)
    {
        if (preventedDamage <= 0) return;

        var source = ContributionMap.Instance.GetPlayerBuffSource("BUFFER_POWER");
        string sourceId = source?.SourceId ?? "BUFFER_POWER";
        string sourceType = source?.SourceType ?? "power";
        GetOrCreate(sourceId, sourceType).MitigatedByBuff += preventedDamage;
    }

    public void OnIntangibleReduction(int originalDamage, int reducedTo)
    {
        int prevented = originalDamage - reducedTo;
        if (prevented <= 0) return;

        var source = ContributionMap.Instance.GetPlayerBuffSource("INTANGIBLE_POWER");
        string sourceId = source?.SourceId ?? "INTANGIBLE_POWER";
        string sourceType = source?.SourceType ?? "power";
        GetOrCreate(sourceId, sourceType).MitigatedByBuff += prevented;
    }

    /// <summary>
    /// Called when ColossusPower on the player halves damage from a Vulnerable enemy.
    /// Colossus multiplier = 0.5, so prevented = actualDamage (damage would have been 2x).
    /// </summary>
    public void OnColossusMitigation(int actualDamage, int playerHash)
    {
        if (actualDamage <= 0) return;

        // Colossus is 0.5x, so prevented = actualDamage / 0.5 - actualDamage = actualDamage
        int prevented = actualDamage;

        var source = ContributionMap.Instance.GetPlayerBuffSource("COLOSSUS_POWER");
        string sourceId = source?.SourceId ?? "COLOSSUS_POWER";
        string sourceType = source?.SourceType ?? "power";
        GetOrCreate(sourceId, sourceType).MitigatedByBuff += prevented;
    }

    /// <summary>
    /// Called when HardenedShellPower absorbs incoming HP loss (Min(amount, shellRemaining)).
    /// Round 14 v5 review §12.
    /// </summary>
    public void OnHardenedShellMitigation(int preventedDamage)
    {
        if (preventedDamage <= 0) return;
        var source = ContributionMap.Instance.GetPlayerBuffSource("HARDENED_SHELL_POWER");
        string sourceId = source?.SourceId ?? "HARDENED_SHELL_POWER";
        string sourceType = source?.SourceType ?? "power";
        GetOrCreate(sourceId, sourceType).MitigatedByBuff += preventedDamage;
    }

    /// <summary>
    /// Round 15: credit a relic that reduced incoming damage via a
    /// ModifyDamageMultiplicative override on the enemy→player path
    /// (e.g. UndyingSigil 0.5x when at low HP). The caller supplies the
    /// raw prevented amount after computing it from the pre/post damage
    /// delta and the relic's multiplier share.
    /// </summary>
    public void OnRelicIncomingDamageMitigation(string relicId, int preventedDamage)
    {
        if (preventedDamage <= 0 || string.IsNullOrEmpty(relicId)) return;
        GetOrCreate(relicId, "relic").MitigatedByBuff += preventedDamage;
    }

    // ── Block Tracking ──────────────────────────────────────

    public void OnBlockGained(int amount, string? cardPlayId)
    {
        var (sourceId, sourceType) = ResolveSource(cardPlayId);

        if (amount > 0)
        {
            // Round 14 v5: modifier contributions (Dex/Focus/Footwork/Dex potion)
            // are pushed to the block pool as metadata on the chunk. They do NOT
            // write ModifierBlock immediately. On damage absorption, ConsumeBlock
            // proportionally splits the consumed amount between base (EffectiveBlock)
            // and each modifier (ModifierBlock). This matches the existing invariant
            // "block credit only counts when actually used to absorb damage".
            List<(string Id, string Type, int Amount)>? modifierList = null;
            int modifierTotal = 0;
            var modifiers = ContributionMap.Instance.LastBlockModifiers;
            if (modifiers.Count > 0)
            {
                modifierList = new List<(string, string, int)>();
                foreach (var mod in modifiers)
                {
                    if (mod.Amount > 0)
                    {
                        modifierList.Add((mod.SourceId, mod.SourceType, mod.Amount));
                        modifierTotal += mod.Amount;
                    }
                }
                modifiers.Clear();
            }

            // Orb value contributions (Focus + relics) for Frost orb block bonus.
            foreach (var c in ContributionMap.Instance.ConsumeOrbValueContribs())
            {
                int amt = Math.Min(c.amount, amount);
                if (amt > 0)
                {
                    modifierList ??= new List<(string, string, int)>();
                    modifierList.Add((c.sourceId, c.sourceType, amt));
                    modifierTotal += amt;
                }
            }

            int baseAmount = Math.Max(0, amount - modifierTotal);

            // Per-hit upgrade block delta split (C2) — credits immediately since
            // upgrade delta is a static property of the card, not a consumption-
            // dependent contribution.
            // Fix 6: attribute to the UPGRADE SOURCE (e.g. ARMAMENTS), not the
            // triggering card being played.
            // H10/P0: the upgrade delta must not also sit in the card's base
            // block chunk, otherwise one point of block is counted twice (once
            // when the chunk is consumed as EffectiveBlock, once as UpgradeBlock).
            int upgSplit = 0;
            string upgSrcId = sourceId;
            string upgSrcType = sourceType;
            if (_pendingUpgradeBlockDelta > 0 && baseAmount > 0)
            {
                upgSplit = Math.Min(_pendingUpgradeBlockDelta, baseAmount);
                upgSrcId = _pendingUpgradeSourceId ?? sourceId;
                upgSrcType = _pendingUpgradeSourceType ?? sourceType;
            }

            int poolBaseAmount = baseAmount - upgSplit;

            // Plating/other power hooks can have multiple recorded sources.
            // Split the base block proportionally instead of crediting only
            // the first source. Modifier chunks keep the normal single-source
            // path; they are not duplicated across sources.
            if (cardPlayId == null && _activePowerId != null && poolBaseAmount > 0)
            {
                var powerSources = ContributionMap.Instance.GetPowerSources(_activePowerId);
                if (powerSources != null && powerSources.Count > 1)
                {
                    foreach (var (sid, stype, share) in ContributionMap.Instance
                        .DistributeByPowerSources(_activePowerId, poolBaseAmount))
                    {
                        if (share > 0)
                            ContributionMap.Instance.AddBlock(sid, stype, share, null);
                    }
                    poolBaseAmount = 0;
                }
            }

            if (poolBaseAmount > 0 || modifierTotal > 0)
                ContributionMap.Instance.AddBlock(sourceId, sourceType, poolBaseAmount, modifierList);

            if (upgSplit > 0)
            {
                // Fix D: route upgrade block to upgrader's origin bucket.
                GetOrCreate(upgSrcId, upgSrcType, originOverride: _pendingUpgradeSourceOrigin).UpgradeBlock += upgSplit;
            }
        }
    }

    // ── Power Application ───────────────────────────────────

    public void OnPowerSourceRecorded(string powerId)
    {
        // Fix 3.6: power > relic > potion > card priority. When a relic hook
        // (e.g. BurningBlood applying Heal, or a relic's AfterCardPlayed buff)
        // fires during card play, the power source attribution must follow
        // the hook context, not the triggering card.
        if (_activePowerSourceId != null)
            ContributionMap.Instance.RecordPowerSource(powerId, _activePowerSourceId, _activePowerSourceType ?? "card");
        else if (_activeRelicId != null)
            ContributionMap.Instance.RecordPowerSource(powerId, _activeRelicId, "relic");
        else if (_activePotionId != null)
            ContributionMap.Instance.RecordPowerSource(powerId, _activePotionId, "potion");
        else if (_activeCardId != null)
            ContributionMap.Instance.RecordPowerSource(powerId, _activeCardId, "card");
    }

    /// <summary>
    /// Set a short-lived source override for a specific power application.
    /// Used by PaleBlueDotPower.AfterCardPlayed, whose real apply happens
    /// after an await; the normal AsyncLocal active context is not reliable
    /// enough there. The marker expires after five seconds so a trigger that
    /// is cancelled by combat end cannot poison a later draw.
    /// </summary>
    public void SetPendingPowerSource(string powerId, string sourceId, string sourceType)
    {
        if (string.IsNullOrEmpty(powerId) || string.IsNullOrEmpty(sourceId)) return;
        _pendingPowerSourceAL.Value = (powerId, sourceId, sourceType, Environment.TickCount64 + 5000);
    }

    public void ClearPendingPowerSource() => _pendingPowerSourceAL.Value = null;

    private bool TryConsumePendingPowerSource(string powerId, out string sourceId, out string sourceType)
    {
        sourceId = "";
        sourceType = "";
        var pending = _pendingPowerSourceAL.Value;
        if (pending == null) return false;
        _pendingPowerSourceAL.Value = null;
        if (!string.Equals(pending.Value.PowerId, powerId, StringComparison.Ordinal)) return false;
        if (Environment.TickCount64 > pending.Value.ExpiresAtMs) return false;
        sourceId = pending.Value.SourceId;
        sourceType = pending.Value.SourceType;
        return true;
    }

    public void OnPowerApplied(string powerId, decimal amount, int creatureHash, bool isPlayerTarget)
    {
        // Fix 3.6: power > relic > potion > card priority (see ResolveSource notes).
        string? sourceId = _activePowerSourceId ?? _activeRelicId ?? _activePotionId ?? _activeCardId;
        string sourceType = _activePowerSourceId != null ? (_activePowerSourceType ?? "card") :
                            _activeRelicId != null ? "relic" :
                            _activePotionId != null ? "potion" :
                            _activeCardId != null ? "card" : "card";

        if (TryConsumePendingPowerSource(powerId, out var pendingId, out var pendingType))
        {
            sourceId = pendingId;
            sourceType = pendingType;
        }

        if (sourceId == null) return;

        int intAmount = (int)amount;

        // Fix B: Doom uses a dedicated per-enemy FIFO attribution stack instead of
        // the shared _powerSources map. Self-doom (BorrowedTime / Neurosurge) must
        // NOT be recorded anywhere — it would pollute enemy-Doom kill attribution
        // and leak into every unrelated Doom-kill source lookup.
        if (powerId == "DOOM_POWER")
        {
            if (!isPlayerTarget && intAmount > 0)
            {
                string? origin = (sourceId == _activeCardId) ? _activeCardOrigin : null;
                ContributionMap.Instance.RecordDoomLayer(creatureHash, sourceId, sourceType, intAmount, origin);
            }
            return;
        }

        if (isPlayerTarget)
        {
            // Player buffs: record globally for proportional multi-source attribution.
            // E.g. Strike's ModifierDamage gets split across all STRENGTH_POWER sources.
            //
            // Round 14 v5 Fix A: only record POSITIVE contributions. A negative
            // amount (e.g. Friendship's "Lose 2 Strength" self-cost) must not be
            // added to the global STRENGTH_POWER source list, otherwise Strike's
            // future ModifierDamage decomposition attributes a positive share to
            // Friendship — the opposite of its actual effect. Negative player
            // self-costs should flow through SelfDamage/cost channels, not the
            // positive-contribution table.
            if (intAmount > 0)
            {
                ContributionMap.Instance.RecordPowerSource(powerId, sourceId, sourceType, intAmount);
                ContributionMap.Instance.RecordPlayerBuffSource(powerId, sourceId, sourceType, intAmount);
            }
        }
        else
        {
            // Enemy debuffs: record per-creature in debuff table only. Do NOT pollute
            // the global _powerSources table (Fix 3.1). Previously when MonarchsGaze
            // applied StrengthPower(-1) to an enemy, the global table saw
            // STRENGTH_POWER → MONARCHS_GAZE_POWER @ -1, which then corrupted the
            // proportional distribution of player's own Str on Strike's ModifierDamage
            // (the denominator became sum(+2, -1)=1, producing share=4 for INFLAME and
            // share=-2 for MONARCHS_GAZE, making MonarchsGaze appear in the attack column).
            ContributionMap.Instance.RecordDebuffSource(creatureHash, powerId, sourceId, sourceType);
            // H1: Record FIFO debuff layer for duration-based debuffs (Vulnerable, Weak)
            int duration = (int)amount;
            if (duration > 0)
                ContributionMap.Instance.RecordDebuffLayer(creatureHash, powerId, sourceId, sourceType, duration);
        }
    }

    /// <summary>
    /// Called for damage from a power (e.g., Poison tick). Attributes back to origin.
    /// </summary>
    public void OnPowerDamage(string powerId, int damage)
    {
        var source = ContributionMap.Instance.GetPowerSource(powerId);
        if (source != null)
        {
            GetOrCreate(source.SourceId, source.SourceType).AttributedDamage += damage;
        }
    }

    // ── Card Draw ───────────────────────────────────────────

    // Pending draw attribution override. Contexts that cannot rely on the
    // AsyncLocal active source across an async boundary may set a one-shot
    // source here; OnCardDrawn consumes it before ResolveSource. Every combat,
    // turn, and hook-context boundary clears it (see ClearPendingDrawSource
    // callers) so a hook that did not draw cannot poison a later draw.
    private string? _pendingDrawSourceId
    {
        get => _pendingDrawSourceIdAL.Value;
        set => _pendingDrawSourceIdAL.Value = value;
    }
    private string? _pendingDrawSourceType
    {
        get => _pendingDrawSourceTypeAL.Value;
        set => _pendingDrawSourceTypeAL.Value = value;
    }

    public void SetPendingDrawSource(string sourceId, string sourceType)
    {
        _pendingDrawSourceId = sourceId;
        _pendingDrawSourceType = sourceType;
        _pendingDrawRemainingAL.Value = null; // unlimited until consumed by single draw
    }

    /// <summary>
    /// P2-3: Set pending draw source with an N-count sticky cap. Used by relics that
    /// draw multiple cards in a single hook (e.g. CentennialPuzzle → draw 3). The
    /// source is kept alive until `count` draws have been attributed to it.
    /// </summary>
    public void SetPendingDrawSource(string sourceId, string sourceType, int count)
    {
        _pendingDrawSourceId = sourceId;
        _pendingDrawSourceType = sourceType;
        _pendingDrawRemainingAL.Value =
            new System.Runtime.CompilerServices.StrongBox<int>(Math.Max(1, count));
    }

    public void ClearPendingDrawSource()
    {
        _pendingDrawSourceId = null;
        _pendingDrawSourceType = null;
        _pendingDrawRemainingAL.Value = null;
    }

    public void OnCardDrawn(bool fromHandDraw)
    {
        if (fromHandDraw)
        {
            // The base five cards are not a bonus. Only actual cards beyond
            // that consume a planned draw-bonus source; a hand-limit cut
            // therefore leaves the source with less than its planned count.
            _handDrawCount++;
            if (_handDrawCount > 5 && ContributionMap.Instance.PendingHandDrawNet > 0)
            {
                var bonus = ContributionMap.Instance.ConsumeOneHandDrawBonus();
                if (bonus != null)
                    GetOrCreate(bonus.Value.sourceId, bonus.Value.sourceType).CardsDrawn++;
            }
            return;
        }

        // Check pending power/relic draw source first (for async hooks like DarkEmbrace,
        // CentennialPuzzle). If an N-count is set, decrement and keep the source alive
        // until fully consumed; otherwise consume after a single draw.
        if (_pendingDrawSourceId != null)
        {
            GetOrCreate(_pendingDrawSourceId, _pendingDrawSourceType ?? "card").CardsDrawn++;
            var box = _pendingDrawRemainingAL.Value;
            if (box != null)
            {
                box.Value--;
                if (box.Value <= 0)
                {
                    _pendingDrawSourceId = null;
                    _pendingDrawSourceType = null;
                    _pendingDrawRemainingAL.Value = null;
                }
            }
            else
            {
                _pendingDrawSourceId = null;
                _pendingDrawSourceType = null;
            }
            return;
        }

        var (sourceId, sourceType) = ResolveSource(null);
        if (sourceId != null)
            GetOrCreate(sourceId, sourceType).CardsDrawn++;
    }

    // ── Energy Gained ───────────────────────────────────────

    public void OnEnergyGained(int amount)
    {
        if (amount <= 0) return;

        var (sourceId, sourceType) = ResolveSource(null);
        if (sourceId != null)
            GetOrCreate(sourceId, sourceType).EnergyGained += amount;
    }

    /// <summary>
    /// Direct EnergyGained write used by ModifyMaxEnergy postfix patches
    /// (Demesne, Friendship). These powers are pure modifier overrides and
    /// have no hook-fire event to set the attribution context, so the
    /// caller supplies the resolved source directly.
    /// </summary>
    public void AddEnergyBonusDirect(string sourceId, string sourceType, int amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(sourceId)) return;
        GetOrCreate(sourceId, sourceType).EnergyGained += amount;
    }

    /// <summary>Called when player gains Stars (GainStars hook).</summary>
    public void OnStarsGained(int amount)
    {
        if (amount <= 0) return;
        var (sourceId, sourceType) = ResolveSource(null);
        if (sourceId != null)
            GetOrCreate(sourceId, sourceType).StarsContribution += amount;
    }

    // ── Cost Savings (NEW-1/NEW-2: centralized play-time attribution) ──

    /// <summary>
    /// Called from CardPlayStarted to attribute energy/star savings at play time.
    /// Compares card's canonical cost vs actual cost spent.
    /// </summary>
    /// <summary>
    /// Source IDs whose cost-savings attribution is allowed to be NEGATIVE (PRD-04 §4.2).
    /// SneckoEye / FakeSneckoEye / SneckoOil randomize card costs — the change can
    /// raise the cost, in which case the energy "savings" is a negative contribution.
    /// </summary>
    private static readonly HashSet<string> _negativeSavingsAllowedSources = new()
    {
        "SNECKO_EYE",
        "FAKE_SNECKO_EYE",
        "SNECKO_OIL",
    };

    public void AttributeCostSavings(int cardHash, int canonicalEnergy, int energySpent,
        int canonicalStars, int starsSpent, bool costsX, bool isAutoPlay)
    {
        if (costsX) return; // X-cost cards excluded

        // Auto-played cards are not paid by the player, so no cost reduction
        // contributed to their play. Drop the pending tag instead of leaving it
        // to poison a later play of the same card instance.
        if (isAutoPlay)
        {
            ContributionMap.Instance.ClearCostReductionSourceTag(cardHash);
            return;
        }

        int energySaved = canonicalEnergy - energySpent;
        int starsSaved = canonicalStars - starsSpent;

        // Exception rule: generated-and-freed cards only tracked via sub-bar
        if (ContributionMap.Instance.IsCardGeneratedAndFree(cardHash))
        {
            ContributionMap.Instance.ClearCostReductionSourceTag(cardHash);
            return;
        }

        // Look up source tag
        var tag = ContributionMap.Instance.GetCostReductionSourceTag(cardHash);
        if (tag == null) return;

        var (sourceId, sourceType) = tag.Value;

        // Generated-and-free detection at consumption time. Potions call
        // SetToFreeThisTurn BEFORE AddGeneratedCardToCombat, so the origin may
        // not exist yet when the tag is written. Re-check here so a
        // potion-generated free card still follows the sub-bar path instead of
        // being credited as an energy saving.
        var origin = ContributionMap.Instance.GetCardOrigin(cardHash);
        if (origin != null && origin.Value.originId == sourceId)
        {
            ContributionMap.Instance.MarkCardAsGeneratedAndFree(cardHash);
            ContributionMap.Instance.ClearCostReductionSourceTag(cardHash);
            return;
        }

        bool allowNegative = _negativeSavingsAllowedSources.Contains(sourceId);

        // Default: only credit positive savings.
        // Snecko-family sources may also credit negative (cost increase) per PRD §4.2.
        if (allowNegative)
        {
            if (energySaved != 0) GetOrCreate(sourceId, sourceType).EnergyGained += energySaved;
            if (starsSaved != 0) GetOrCreate(sourceId, sourceType).StarsContribution += starsSaved;
        }
        else
        {
            if (energySaved <= 0 && starsSaved <= 0)
            {
                ContributionMap.Instance.ClearCostReductionSourceTag(cardHash);
                return;
            }
            if (energySaved > 0) GetOrCreate(sourceId, sourceType).EnergyGained += energySaved;
            if (starsSaved > 0) GetOrCreate(sourceId, sourceType).StarsContribution += starsSaved;
        }

        ContributionMap.Instance.ClearCostReductionSourceTag(cardHash);
    }

    // ── Extra Hand Draw ──────────────────────────────────────

    /// <summary>
    /// Called after hand draw completes to attribute extra draws from powers
    /// (PaleBlueDot, Tyranny) that modify hand draw count.
    /// </summary>
    /// <summary>
    /// Called once after hand-draw modifiers complete. Resets the actual-card
    /// counter; individual OnCardDrawn(true) events consume the planned
    /// bonuses only for cards beyond the base five.
    /// </summary>
    public void BeginHandDrawAttribution() => _handDrawCount = 0;

    // ── SeekingEdge Context ──────────────────────────────────

    public void SetSeekingEdgeContext(int primaryTargetHash)
        => ContributionMap.Instance.SetSeekingEdgeContext(primaryTargetHash);

    public void ClearSeekingEdgeContext()
        => ContributionMap.Instance.ClearSeekingEdgeContext();

    // ── Osty Damage Attribution ───────────────────────────────
    // When Osty deals damage to enemies, attribute to the specific attack card
    // but set OriginSourceId = "OSTY" for sub-bar display under OSTY parent.

    private const string OstyParentId = "OSTY";

    /// <summary>
    /// Called when Osty deals damage to an enemy. The cardSourceId is the attack card
    /// (Unleash, Poke, etc.). Damage is attributed to the card with OSTY as parent.
    /// </summary>
    public void OnOstyDamageDealt(int totalDamage, string cardSourceId)
    {
        if (totalDamage <= 0 || string.IsNullOrEmpty(cardSourceId)) return;

        // Ensure OSTY parent entry exists
        GetOrCreate(OstyParentId, "osty");

        // Attribute damage to the specific card, using the same composite
        // key that persistence will later reconstruct.
        var accum = GetOrCreate(cardSourceId, "card", OstyParentId);
        accum.DirectDamage += totalDamage;

        // Also consume modifier context if present
        var modifiers = ContributionMap.Instance.LastDamageModifiers;
        if (modifiers.Count > 0)
        {
            foreach (var mod in modifiers)
            {
                if (mod.Amount > 0)
                    GetOrCreate(mod.SourceId, mod.SourceType).ModifierDamage += mod.Amount;
            }
            modifiers.Clear();
        }
    }

    // ── Osty Defense (HP absorption) ────────────────────────
    // When Osty absorbs damage for the player, attribute defense to summon sources.

    public void OnOstyAbsorbedDamage(int damage)
    {
        if (damage <= 0) return;

        // Ensure OSTY parent entry exists
        GetOrCreate(OstyParentId, "osty");

        // Consume from LIFO stack to find which summon sources contributed
        var consumed = ContributionMap.Instance.ConsumeOstyHp(damage);
        foreach (var (sourceId, sourceType, amount) in consumed)
        {
            var accum = GetOrCreate(sourceId, sourceType, OstyParentId);
            accum.EffectiveBlock += amount;
        }

        // If stack was insufficient (shouldn't normally happen), attribute remainder to OSTY itself
        int totalConsumed = 0;
        foreach (var (_, _, amount) in consumed) totalConsumed += amount;
        int remainder = damage - totalConsumed;
        if (remainder > 0)
        {
            GetOrCreate(OstyParentId, "osty").EffectiveBlock += remainder;
        }
    }

    // ── Osty Summon Tracking ────────────────────────────────

    public void OnOstySummoned(string sourceId, string sourceType, int hpAmount)
    {
        // A fresh summon starts a fresh life: allow the next death to be recorded.
        _ostyDeathHandled = false;
        ContributionMap.Instance.PushOstyHp(sourceId, sourceType, hpAmount);
    }

    /// <summary>
    /// Called when Osty is killed (BoneShards sacrifice, Sacrifice card, etc.).
    /// Subtracts remaining Osty HP from the killer card's defense contribution
    /// (killing Osty destroys its HP pool, removing that defensive resource).
    /// Then clears the LIFO stack.
    /// Idempotent: nested/duplicated Kill prefixes for the same death are ignored.
    /// </summary>
    public void OnOstyKilled()
    {
        if (_ostyDeathHandled) return;
        _ostyDeathHandled = true;

        int remainingHp = ContributionMap.Instance.GetRemainingOstyHpTotal();

        if (remainingHp > 0 && _activeCardId != null)
        {
            // H3: Defer negative defense until next enemy attack hits player
            _pendingOstyDeathDefense = (_activeCardId, "card", remainingHp);
        }

        ContributionMap.Instance.ClearOstyHpStack();
    }

    // ── Doom Damage Attribution ─────────────────────────────

    /// <summary>
    /// Called before Doom kills enemies. Records each target's current HP.
    /// </summary>
    public void OnDoomTargetCapture(int creatureHash, int currentHp)
    {
        ContributionMap.Instance.RecordDoomTargetHp(creatureHash, currentHp);
    }

    /// <summary>
    /// Called after Doom kills. Attributes total HP as damage to the Doom power source.
    /// P2-1: Also falls back to active card/relic/power context when DOOM_POWER source
    /// is missing (can happen when Doom was applied by a non-card path, or when the
    /// ContributionMap lookup was cleared between apply and kill). This ensures the
    /// NB-DOOM-Deathbringer scenario (21-Doom AoE from Deathbringer) properly credits
    /// AttributedDamage back to DEATHBRINGER even when GetPowerSource returns null.
    /// </summary>
    public void OnDoomKillsCompleted()
    {
        var targets = ContributionMap.Instance.ConsumeDoomTargetHp();
        if (targets.Count == 0) return;

        // Fix B: walk each killed target individually and FIFO-consume its DoomLayer
        // stack. Each source that contributed Doom to this enemy gets its proportional
        // share of the enemy's HP as AttributedDamage, credited to its origin bucket.
        foreach (var (creatureHash, hp) in targets)
        {
            if (hp <= 0) continue;

            var layers = ContributionMap.Instance.ConsumeDoomLayers(creatureHash, hp);
            if (layers.Count > 0)
            {
                foreach (var (sid, stype, origin, share) in layers)
                {
                    if (share <= 0) continue;
                    GetOrCreate(sid, stype, originOverride: origin).AttributedDamage += share;
                }
                continue;
            }

            // No layer records for this target — fall back to active context.
            // Priority: activeCard > activePotion > activeRelic > activePowerSource > "DOOM".
            if (_activeCardId != null)
                GetOrCreate(_activeCardId, "card").AttributedDamage += hp;
            else if (_activePotionId != null)
                GetOrCreate(_activePotionId, "potion").AttributedDamage += hp;
            else if (_activeRelicId != null)
                GetOrCreate(_activeRelicId, "relic").AttributedDamage += hp;
            else if (_activePowerSourceId != null)
                GetOrCreate(_activePowerSourceId, _activePowerSourceType ?? "card").AttributedDamage += hp;
            else
                GetOrCreate("DOOM", "card").AttributedDamage += hp;
        }
    }

    // ── Card Origin (for sub-bar display) ───────────────────

    public void RecordCardOrigin(int cardHash, string originId, string originType)
    {
        ContributionMap.Instance.RecordCardOrigin(cardHash, originId, originType);
    }

    /// <summary>
    /// Fix C: No-op shim. Origin is now resolved at GetOrCreate time using the
    /// composite bucket key, so post-hoc retrofit is both unnecessary and wrong
    /// (last-writer-wins used to overwrite deck-native buckets with potion origins).
    /// Retained only for ABI compatibility with any remaining external callers.
    /// </summary>
    public void TagCardOrigin(string cardId, int cardHash) { }

    // ── Forge Tracking ────────────────────────────────────────

    /// <summary>
    /// Called from ForgeCmd.Forge postfix. Records source and amount of each forge event.
    /// </summary>
    public void OnForge(string sourceId, string sourceType, int amount)
    {
        if (amount <= 0) return;
        _forgeLog.Add((sourceId, sourceType, amount));
    }

    /// <summary>
    /// Called from SovereignBlade.OnPlay prefix. Writes accumulated forge sources
    /// as sub-bar entries under SOVEREIGN_BLADE so the chart shows breakdown.
    /// Each forge source gets a ContributionAccum with OriginSourceId = "SOVEREIGN_BLADE"
    /// and DirectDamage = total forged amount from that source.
    /// </summary>
    public void FlushForgeSubBars()
    {
        // Aggregate forge sources if any. Forge log may be empty on replay
        // (Sword Sage / Replay mechanic) — still write base damage below.
        if (_forgeLog.Count > 0)
        {
            var aggregated = new Dictionary<string, (string sourceType, int count, int totalAmount)>();
            foreach (var (srcId, srcType, amt) in _forgeLog)
            {
                if (aggregated.TryGetValue(srcId, out var existing))
                    aggregated[srcId] = (srcType, existing.count + 1, existing.totalAmount + amt);
                else
                    aggregated[srcId] = (srcType, 1, amt);
            }

            foreach (var (rawSrcId, (srcType, count, totalAmt)) in aggregated)
            {
                string srcId = rawSrcId;
                if (srcType == "power" && srcId.EndsWith("_POWER", StringComparison.Ordinal))
                    srcId = srcId.Substring(0, srcId.Length - "_POWER".Length);
                string key = $"FORGE:{srcId}";
                var accum = GetOrCreate(key, srcType, "SOVEREIGN_BLADE");
                accum.DirectDamage += totalAmt;
                accum.TimesPlayed += count;
            }

            // The forge log describes the forges performed since the previous
            // Sovereign Blade play. Consume it so replaying the blade (Sword
            // Sage / Replay) does not add the same forge sources twice.
            _forgeLog.Clear();
        }

        // Base damage entry — SovereignBlade starts at 10. Uses += so
        // Replay mechanics (Sword Sage) accumulate correctly across
        // multiple OnPlay calls within the same combat.
        const int baseDamage = 10;
        var baseAccum = GetOrCreate("FORGE:BASE", "card", "SOVEREIGN_BLADE");
        baseAccum.DirectDamage += baseDamage;
        baseAccum.TimesPlayed += 1;
    }

    /// <summary>
    /// Set the rest-site bonus that must be split out of the next heal.
    /// Called by the RegalPillow.ModifyRestSiteHealAmount postfix.
    /// </summary>
    public void SetRestHealBonus(int amount, string sourceId = "REGAL_PILLOW",
        string sourceType = "relic")
    {
        if (amount <= 0 || string.IsNullOrEmpty(sourceId)) return;
        _pendingRestHealBonus = (sourceId, sourceType, amount);
    }

    public void ClearRestHealBonus() => _pendingRestHealBonus = null;

    // ── Healing ──────────────────────────────────────────────

    /// <summary>
    /// Called when player is healed. Attributes to active card/relic/potion/power source.
    /// Healing can occur both during combat (e.g. Reaper card) and in the
    /// AfterCombatVictory window (Burning Blood / Black Blood), which runs
    /// before CombatManager.CombatEnded and therefore still belongs to the
    /// combat being flushed.
    /// fallbackId/fallbackType: used when no active context (rest site, event, merchant).
    /// </summary>
    public void OnHealingReceived(int amount, string? fallbackId = null, string? fallbackType = null)
    {
        if (amount <= 0) return;
        var (sourceId, sourceType) = ResolveSource(null);

        // Use fallback when no active source context (UNTRACKED means ResolveSource found nothing)
        if (sourceId == "UNTRACKED" && fallbackId != null)
        {
            sourceId = fallbackId;
            sourceType = fallbackType ?? "other";
        }

        // R2: RegalPillow's ModifyRestSiteHealAmount returns base + 15.
        // Split that single Heal event: the base remains REST_SITE, and the
        // bonus becomes a relic row. The total healed stays unchanged.
        if (_pendingRestHealBonus is { } restBonus
            && string.Equals(fallbackId, "REST_SITE", StringComparison.Ordinal))
        {
            _pendingRestHealBonus = null;
            int bonus = Math.Min(amount, Math.Max(0, restBonus.amount));
            int baseAmount = amount - bonus;
            if (baseAmount > 0)
                AddHealingRecord(sourceId, sourceType, baseAmount);
            if (bonus > 0)
                AddHealingRecord(restBonus.sourceId, restBonus.sourceType, bonus);
            return;
        }

        AddHealingRecord(sourceId, sourceType, amount);
    }

    private void AddHealingRecord(string sourceId, string sourceType, int amount)
    {
        if (amount <= 0) return;
        if (_liveCombatInProgress)
        {
            // Combat in progress, including the post-victory window before
            // CombatManager.CombatEnded. Writing by _currentCombat.Count would
            // drop the first heal of a combat and the Burning Blood / Black
            // Blood heals that land before the combat is flushed.
            GetOrCreate(sourceId, sourceType).HpHealed += amount;
        }
        else
        {
            // Outside combat (rest site, events, merchant, floor regen)
            // Write directly to run-level aggregator.
            RunContributionAggregator.Instance.AddHealing(sourceId, sourceType, amount);
        }
    }

    // ── Player Death ────────────────────────────────────────

    public void OnPlayerDied() => _playerDied = true;

    // ── Helpers ──────────────────────────────────────────────

    // Fix C: composite bucket key `sourceId\u0001originId`. Same-name cards from
    // different origins (deck vs skill-potion vs Shiv generator A vs generator B) land
    // in distinct buckets instead of colliding under a single plain-sourceId key.
    // OriginId=null collapses to plain sourceId so top-level bars (and legacy save
    // snapshots) keep their natural key shape.
    private static string MakeBucketKey(string sourceId, string? originId)
        => originId == null ? sourceId : sourceId + "\u0001" + originId;

    private string? ResolveOriginFor(string sourceId)
    {
        // Only the currently-playing card's origin propagates to its own bucket. Other
        // attribution paths (relic/potion/power-source) use origin=null (main bucket).
        if (sourceId == _activeCardId) return _activeCardOrigin;
        return null;
    }

    private ContributionAccum GetOrCreate(string sourceId, string sourceType, string? originOverride = null)
    {
        string? originId = originOverride ?? ResolveOriginFor(sourceId);
        string key = MakeBucketKey(sourceId, originId);
        if (!_currentCombat.TryGetValue(key, out var accum))
        {
            accum = new ContributionAccum
            {
                SourceId = sourceId,
                SourceType = sourceType,
                OriginSourceId = originId,
            };
            _currentCombat[key] = accum;
        }
        return accum;
    }

    /// <summary>
    /// Get a read-only snapshot of current combat contributions (for live UI).
    /// </summary>
    public IReadOnlyDictionary<string, ContributionAccum> GetCurrentCombatData() =>
        _currentCombat;
}
