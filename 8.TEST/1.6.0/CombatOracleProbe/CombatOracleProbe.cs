using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace StatsTheSpire.CombatOracle;

[ModInitializer("Initialize")]
public static class CombatOracleProbe
{
    private static bool _attached;
    private static int _frames;
    private static CombatOracle? _oracle;

    public static void Initialize()
    {
        DamageGivenOraclePatch.Install();
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += OnFrame;
    }

    private static void OnFrame()
    {
        if (_attached)
        {
            try { _oracle?.PollRunEnd(); } catch (Exception ex) { GD.Print($"[CombatOracle] poll error: {ex.Message}"); }
            return;
        }

        _frames++;
        if (_frames < 60) return;
        try
        {
            var manager = CombatManager.Instance;
            _oracle = new CombatOracle(manager);
            _attached = true;
            GD.Print("[CombatOracle] attached");
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] waiting: {ex.Message}");
        }
    }
}

internal sealed class CombatOracle
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Regex RoundRegex = new(@"^Rd (?<round>\d+) \((?<side>[^)]+) turn\):", RegexOptions.Compiled);

    private static CombatOracle? _instance;
    private readonly CombatManager _manager;
    private readonly List<EntryDto> _entries = new();
    private CombatRecord? _current;
    private int _seenHistoryCount;

    public CombatOracle(CombatManager manager)
    {
        _instance = this;
        _manager = manager;
        _manager.CombatSetUp += OnCombatSetUp;
        _manager.CombatEnded += OnCombatEnded;
        _manager.History.Changed += OnHistoryChanged;
        GD.Print($"[CombatOracle] subscribed history count={_manager.History.Entries.Count()}");
    }

    internal static void RecordDamageGiven(Creature? dealer, DamageResult result, Creature target, CardModel? cardSource)
    {
        try { _instance?.AddDamageGiven(dealer, result, target, cardSource); }
        catch (Exception ex) { GD.Print($"[CombatOracle] RecordDamageGiven failed: {ex.Message}"); }
    }

    private void AddDamageGiven(Creature? dealer, DamageResult result, Creature target, CardModel? cardSource)
    {
        if (_current == null) return;
        var state = _manager.DebugOnlyGetState();
        _entries.Add(new EntryDto
        {
            Type = "DamageGivenOracle",
            Description = $"{Id(dealer) ?? "?"} dealt {result.TotalDamage} damage to {Id(result.Receiver) ?? Id(target) ?? "?"}",
            Round = state?.RoundNumber ?? 0,
            Side = state?.CurrentSide.ToString(),
            Actor = Id(dealer),
            ActorSide = Side(dealer),
            Receiver = Id(result.Receiver) ?? Id(target),
            ReceiverSide = Side(result.Receiver) ?? Side(target),
            Dealer = Id(dealer),
            DealerSide = Side(dealer),
            CardSource = cardSource?.Id.Entry,
            TotalDamage = result.TotalDamage,
            UnblockedDamage = result.UnblockedDamage,
            BlockedDamage = result.BlockedDamage,
            IntendedHpLoss = result.IntendedHpLoss,
            OverkillDamage = result.OverkillDamage,
            WasTargetKilled = result.WasTargetKilled,
        });
    }

    public void PollRunEnd()
    {
        try
        {
            if (_current != null && RunManager.Instance is { HasRunEnded: true })
                FinalizeCombat("run_end");
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] PollRunEnd failed: {ex.Message}");
        }
    }

    private void OnCombatSetUp(MegaCrit.Sts2.Core.Combat.CombatState state)
    {
        try
        {
            if (_current != null) FinalizeCombat("next_combat");

            _entries.Clear();
            _seenHistoryCount = _manager.History.Entries.Count();
            var run = RunManager.Instance?.DebugOnlyGetState();
            _current = new CombatRecord
            {
                Floor = run?.TotalFloor ?? RunDataCollector.CurrentFloor,
                EncounterId = state.Encounter?.Id.Entry ?? "unknown",
                EncounterType = state.Encounter?.RoomType.ToString().ToLowerInvariant() ?? "unknown",
                StartedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            GD.Print($"[CombatOracle] start floor={_current.Floor} encounter={_current.EncounterId}");
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] OnCombatSetUp failed: {ex.Message}");
        }
    }

    private void OnCombatEnded(CombatRoom room)
    {
        try
        {
            FinalizeCombat("combat_ended");
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] OnCombatEnded failed: {ex.Message}");
        }
    }

    private void OnHistoryChanged()
    {
        try
        {
            if (_current == null) return;
            var all = _manager.History.Entries.ToList();
            if (all.Count < _seenHistoryCount)
            {
                // The game clears History at combat end; keep our own buffer.
                _seenHistoryCount = all.Count;
                return;
            }

            for (int i = _seenHistoryCount; i < all.Count; i++)
            {
                try { _entries.Add(FromEntry(all[i])); }
                catch (Exception ex) { GD.Print($"[CombatOracle] entry {all[i].GetType().Name} failed: {ex.Message}"); }
            }
            _seenHistoryCount = all.Count;
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] OnHistoryChanged failed: {ex.Message}");
        }
    }

    private void FinalizeCombat(string reason)
    {
        if (_current == null) return;

        var current = _current;
        _current = null;

        try
        {
            var tracker = CombatTracker.Instance;
            var sources = tracker.GetCurrentCombatData();
            var last = tracker.LastCombatData;
            if (sources.Count == 0 && last != null && last.Count > 0)
                sources = last;

            var identity = ContributionPersistence.GetActiveIdentity();
            if (identity == null || !identity.IsValid)
            {
                GD.Print($"[CombatOracle] skip floor={current.Floor}: no run identity");
                return;
            }

            var dto = new OracleDto
            {
                StartTime = identity.StartTime,
                Seed = identity.Seed,
                Floor = current.Floor,
                EncounterId = current.EncounterId,
                EncounterType = current.EncounterType,
                Reason = reason,
                StartedAtUnix = current.StartedAtUnix,
                SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Entries = _entries.ToList(),
                Sources = sources.Values.Select(ToSourceDto).ToList(),
                LastSources = last == null ? new List<SourceDto>() : last.Values.Select(ToSourceDto).ToList(),
            };

            var dir = Path.Combine(ModConfig.DataDir, "oracle", identity.RunKey);
            Directory.CreateDirectory(dir);
            string path;
            int sequence = 1;
            do
            {
                path = Path.Combine(dir, $"oracle_{current.Floor}_{sequence}.json");
                sequence++;
            } while (File.Exists(path));

            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOpts));
            GD.Print($"[CombatOracle] wrote floor={current.Floor} encounter={current.EncounterId} entries={dto.Entries.Count} sources={dto.Sources.Count} reason={reason} path={path}");
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] FinalizeCombat failed: {ex}");
        }
        finally
        {
            _entries.Clear();
            _seenHistoryCount = _manager.History.Entries.Count();
        }
    }

    private static SourceDto ToSourceDto(ContributionAccum a) => new()
    {
        SourceId = a.SourceId,
        SourceType = a.SourceType,
        OriginSourceId = a.OriginSourceId,
        TimesPlayed = a.TimesPlayed,
        DirectDamage = a.DirectDamage,
        AttributedDamage = a.AttributedDamage,
        ModifierDamage = a.ModifierDamage,
        UpgradeDamage = a.UpgradeDamage,
        EffectiveBlock = a.EffectiveBlock,
        ModifierBlock = a.ModifierBlock,
        UpgradeBlock = a.UpgradeBlock,
        MitigatedByDebuff = a.MitigatedByDebuff,
        MitigatedByBuff = a.MitigatedByBuff,
        MitigatedByStrReduction = a.MitigatedByStrReduction,
        CardsDrawn = a.CardsDrawn,
        EnergyGained = a.EnergyGained,
        HpHealed = a.HpHealed,
        StarsContribution = a.StarsContribution,
        SelfDamage = a.SelfDamage,
        TotalDamage = a.TotalDamage,
        TotalDefense = a.TotalDefense,
    };

    private static EntryDto FromEntry(CombatHistoryEntry entry)
    {
        var dto = new EntryDto
        {
            Type = entry.GetType().Name,
            Description = entry.HumanReadableString,
            Actor = Id(entry.Actor),
            ActorSide = Side(entry.Actor),
        };

        var match = RoundRegex.Match(entry.HumanReadableString ?? "");
        if (match.Success)
        {
            dto.Round = int.Parse(match.Groups["round"].Value);
            dto.Side = match.Groups["side"].Value;
        }

        switch (entry)
        {
            case DamageReceivedEntry damage:
                dto.Receiver = Id(damage.Receiver);
                dto.ReceiverSide = Side(damage.Receiver);
                dto.Dealer = Id(damage.Dealer);
                dto.DealerSide = Side(damage.Dealer);
                dto.CardSource = damage.CardSource?.Id.Entry;
                dto.TotalDamage = damage.Result.TotalDamage;
                dto.UnblockedDamage = damage.Result.UnblockedDamage;
                dto.BlockedDamage = damage.Result.BlockedDamage;
                dto.IntendedHpLoss = damage.Result.IntendedHpLoss;
                dto.OverkillDamage = damage.Result.OverkillDamage;
                dto.WasTargetKilled = damage.Result.WasTargetKilled;
                break;

            case BlockGainedEntry block:
                dto.Receiver = Id(block.Receiver);
                dto.ReceiverSide = Side(block.Receiver);
                dto.Card = block.CardPlay?.Card?.Id.Entry;
                dto.Target = Id(block.CardPlay?.Target);
                dto.Amount = block.Amount;
                break;

            case CardPlayStartedEntry started:
                dto.Card = started.CardPlay.Card.Id.Entry;
                dto.Target = Id(started.CardPlay.Target);
                dto.Amount = started.CardPlay.PlayCount;
                break;

            case CardPlayFinishedEntry finished:
                dto.Card = finished.CardPlay.Card.Id.Entry;
                dto.Target = Id(finished.CardPlay.Target);
                dto.Amount = finished.CardPlay.PlayCount;
                break;

            case CardDrawnEntry drawn:
                dto.Card = drawn.Card.Id.Entry;
                dto.FromHandDraw = drawn.FromHandDraw;
                dto.Amount = 1;
                break;

            case PowerReceivedEntry power:
                dto.Power = power.Power.Id.Entry;
                dto.Receiver = Id(power.Power.Owner);
                dto.ReceiverSide = Side(power.Power.Owner);
                dto.Dealer = Id(power.Applier);
                dto.DealerSide = Side(power.Applier);
                dto.Amount = power.Amount;
                break;

            case PotionUsedEntry potion:
                dto.Potion = potion.Potion.Id.Entry;
                dto.Target = Id(potion.Target);
                dto.Amount = 1;
                break;

            case EnergySpentEntry energy:
                dto.Amount = energy.Amount;
                break;

            case OrbChanneledEntry orb:
                dto.Orb = orb.Orb.Id.Entry;
                dto.Amount = 1;
                break;

            case SummonedEntry summon:
                dto.Amount = summon.Amount;
                break;

            case StarsModifiedEntry stars:
                dto.Amount = stars.Amount;
                break;

            case CreatureAttackedEntry attack:
                dto.Amount = attack.DamageResults.Sum(r => r.TotalDamage);
                dto.TotalDamage = attack.DamageResults.Sum(r => r.TotalDamage);
                dto.UnblockedDamage = attack.DamageResults.Sum(r => r.UnblockedDamage);
                dto.BlockedDamage = attack.DamageResults.Sum(r => r.BlockedDamage);
                break;
        }

        return dto;
    }

    private static string? Id(Creature? creature)
    {
        if (creature == null) return null;
        try { return creature.ModelId.Entry; }
        catch { return null; }
    }

    private static string? Side(Creature? creature) => creature?.Side.ToString();

    private sealed class CombatRecord
    {
        public int Floor { get; init; }
        public string EncounterId { get; init; } = "";
        public string EncounterType { get; init; } = "";
        public long StartedAtUnix { get; init; }
    }

    private sealed class OracleDto
    {
        public long StartTime { get; set; }
        public string Seed { get; set; } = "";
        public int Floor { get; set; }
        public string EncounterId { get; set; } = "";
        public string EncounterType { get; set; } = "";
        public string Reason { get; set; } = "";
        public long StartedAtUnix { get; set; }
        public long SavedAtUnix { get; set; }
        public List<EntryDto> Entries { get; set; } = new();
        public List<SourceDto> Sources { get; set; } = new();
        public List<SourceDto> LastSources { get; set; } = new();
    }

    private sealed class EntryDto
    {
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public int Round { get; set; }
        public string? Side { get; set; }
        public string? Actor { get; set; }
        public string? ActorSide { get; set; }
        public string? Receiver { get; set; }
        public string? ReceiverSide { get; set; }
        public string? Dealer { get; set; }
        public string? DealerSide { get; set; }
        public string? CardSource { get; set; }
        public string? Card { get; set; }
        public string? Target { get; set; }
        public string? Power { get; set; }
        public string? Potion { get; set; }
        public string? Orb { get; set; }
        public decimal? Amount { get; set; }
        public int? TotalDamage { get; set; }
        public int? UnblockedDamage { get; set; }
        public int? BlockedDamage { get; set; }
        public int? IntendedHpLoss { get; set; }
        public int? OverkillDamage { get; set; }
        public bool? WasTargetKilled { get; set; }
        public bool? FromHandDraw { get; set; }
    }

    private sealed class SourceDto
    {
        public string SourceId { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string? OriginSourceId { get; set; }
        public int TimesPlayed { get; set; }
        public int DirectDamage { get; set; }
        public int AttributedDamage { get; set; }
        public int ModifierDamage { get; set; }
        public int UpgradeDamage { get; set; }
        public int EffectiveBlock { get; set; }
        public int ModifierBlock { get; set; }
        public int UpgradeBlock { get; set; }
        public int MitigatedByDebuff { get; set; }
        public int MitigatedByBuff { get; set; }
        public int MitigatedByStrReduction { get; set; }
        public int CardsDrawn { get; set; }
        public int EnergyGained { get; set; }
        public int HpHealed { get; set; }
        public int StarsContribution { get; set; }
        public int SelfDamage { get; set; }
        public int TotalDamage { get; set; }
        public int TotalDefense { get; set; }
    }
}


internal static class DamageGivenOraclePatch
{
    private const string HarmonyId = "stats.combat.oracle.damagegiven";

    public static void Install()
    {
        try
        {
            var method = AccessTools.Method(typeof(Hook), nameof(Hook.AfterDamageGiven));
            if (method == null)
            {
                GD.Print("[CombatOracle] AfterDamageGiven not found");
                return;
            }
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(DamageGivenOraclePatch), nameof(Prefix)));
            GD.Print("[CombatOracle] AfterDamageGiven patched");
        }
        catch (Exception ex)
        {
            GD.Print($"[CombatOracle] AfterDamageGiven patch failed: {ex}");
        }
    }

    private static void Prefix(Creature? dealer, DamageResult results, Creature target, CardModel? cardSource)
    {
        CombatOracle.RecordDamageGiven(dealer, results, target, cardSource);
    }
}
