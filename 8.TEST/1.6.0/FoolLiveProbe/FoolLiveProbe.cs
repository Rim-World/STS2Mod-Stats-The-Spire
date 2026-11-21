using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityStats.Collection;
using CommunityStats.Patches;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace StatsTheSpire.FoolLiveProbe;

[ModInitializer("Initialize")]
public static class FoolLiveProbe
{
    private static readonly string[] PowerTypes =
    {
        "AllIsWellPower", "ArcheryPower", "CompostPower", "CoveringFirePower",
        "EnnuiPower", "ExplosiveArrowsPower", "HiddenRootsPower", "LostLutePower",
        "LullabyPower", "MotifPower", "MythicFormPower", "SimpletonPower",
        "SongPathPower", "TunePower", "ViolentChordPower", "YahPower",
        "GodByrdPower", "WuppysLoyaltyPower",
    };

    private static readonly string[] RelicTypes =
    {
        "BathRobes", "BathTowel", "BizarreLamp", "ConductorsBaton", "ExerciseMat", "Fertilizer",
        "MightyWings", "ObsidianVice", "Ocarina", "SongBow", "Storyteller",
        "TormentousCollar", "Twine", "WhittlingKnife", "Wuppy",
    };

    private static readonly string[] CardTypes =
    {
        "Sadness", "WahChoKi", "WahCho", "KiHop", "FoolishShot",
    };

    private static int _relicsAdded;
    private static int _powersAdded;
    private static int _cardsAdded;
    private static bool _contextOnly;
    private static bool _contextVerified;

    public static void Initialize()
    {
        GD.Print("[FoolLiveProbe] initialized");
        _contextOnly = OS.GetCmdlineArgs().Contains("--foolprobe-context-only");
        try
        {
            var harmony = new Harmony("test.fool_live_probe_160");
            harmony.PatchAll(typeof(FoolLiveProbe).Assembly);
            GD.Print("[FoolLiveProbe] Harmony patched");
        }
        catch (Exception ex)
        {
            GD.Print($"[FoolLiveProbe] Harmony failed: {ex}");
        }
        try
        {
            var rm = RunManager.Instance;
            if (rm != null) rm.RunStarted += OnRunStarted;
            var cm = CombatManager.Instance;
            if (cm != null) cm.CombatBegan += OnCombatBegan;
        }
        catch (Exception ex)
        {
            GD.Print($"[FoolLiveProbe] RunManager subscribe failed: {ex.Message}");
        }
    }

    public static void AfterLaunch(RunState state)
    {
        GD.Print("[FoolLiveProbe] launch hook");
        TaskHelper.RunSafely(AddRelics(state));
    }

    private static void OnRunStarted(RunState state)
    {
        GD.Print("[FoolLiveProbe] run started event");
        TaskHelper.RunSafely(AddRelics(state));
    }

    private static void OnCombatBegan(CombatState state)
    {
        GD.Print("[FoolLiveProbe] combat began event");
        TaskHelper.RunSafely(AddPowersAndCards(state));
    }

    public static void AfterSetUpCombat(CombatState state)
    {
        GD.Print("[FoolLiveProbe] combat setup hook");
        TaskHelper.RunSafely(AddPowersAndCards(state));
    }

    private static async Task AddRelics(RunState state)
    {
        try
        {
            Player? me = LocalContext.GetMe(state.Players);
            if (me == null)
            {
                GD.Print("[FoolLiveProbe] relics: no local player");
                return;
            }

            foreach (string name in RelicTypes)
            {
                RelicModel? canonical = ModelDb.AllRelics.FirstOrDefault(r => r.GetType().Name == name);
                if (canonical == null)
                {
                    GD.Print($"[FoolLiveProbe] relic missing: {name}");
                    continue;
                }
                if (me.Relics.Any(r => r.Id == canonical.Id))
                {
                    GD.Print($"[FoolLiveProbe] relic already owned: {name}");
                    continue;
                }

                try
                {
                    await RelicCmd.Obtain(canonical.ToMutable(), me);
                    _relicsAdded++;
                    GD.Print($"[FoolLiveProbe] relic added: {name} ({canonical.Id.Entry})");
                }
                catch (Exception ex)
                {
                    GD.Print($"[FoolLiveProbe] relic failed: {name} {ex.Message}");
                }
            }
            GD.Print($"[FoolLiveProbe] relics total={_relicsAdded}");
        }
        catch (Exception ex)
        {
            GD.Print($"[FoolLiveProbe] relics fatal: {ex}");
        }
    }

    private static async Task AddPowersAndCards(CombatState state)
    {
        try
        {
            Player? me = LocalContext.GetMe(state.Players);
            if (me == null)
            {
                GD.Print("[FoolLiveProbe] powers: no local player");
                return;
            }

            foreach (string name in PowerTypes)
            {
                PowerModel? canonical = ModelDb.AllPowers.FirstOrDefault(p => p.GetType().Name == name);
                if (canonical == null)
                {
                    GD.Print($"[FoolLiveProbe] power missing: {name}");
                    continue;
                }
                try
                {
                    await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), canonical.ToMutable(),
                        me.Creature, 1m, me.Creature, null, silent: true);
                    // Give the probe-applied power a source record. In a real run
                    // the applying card records this; the probe has no card, so
                    // use the power id itself as the live source marker.
                    ContributionMap.Instance.RecordPowerSource(canonical.Id.Entry, canonical.Id.Entry, "power", 1);
                    _powersAdded++;
                    GD.Print($"[FoolLiveProbe] power applied: {name} ({canonical.Id.Entry})");
                }
                catch (Exception ex)
                {
                    GD.Print($"[FoolLiveProbe] power failed: {name} {ex.Message}");
                }
            }

            var cards = new List<CardModel>();
            foreach (string name in CardTypes)
            {
                CardModel? canonical = ModelDb.AllCards.FirstOrDefault(c => c.GetType().Name == name);
                if (canonical == null)
                {
                    GD.Print($"[FoolLiveProbe] card missing: {name}");
                    continue;
                }
                try
                {
                    cards.Add(state.CreateCard(canonical, me));
                }
                catch (Exception ex)
                {
                    GD.Print($"[FoolLiveProbe] card create failed: {name} {ex.Message}");
                }
            }

            if (cards.Count > 0)
            {
                await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, me);
                _cardsAdded += cards.Count;
                GD.Print($"[FoolLiveProbe] cards added={cards.Count}");
            }
            GD.Print($"[FoolLiveProbe] powers total={_powersAdded} cards total={_cardsAdded}");

            if (!_contextVerified)
            {
                _contextVerified = true;
                bool ok = VerifyContexts(me);
                GD.Print($"[FoolLiveProbe] CONTEXT {(ok ? "PASS" : "FAIL")}");
                if (_contextOnly)
                    (Engine.GetMainLoop() as SceneTree)?.Quit();
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[FoolLiveProbe] powers fatal: {ex}");
        }
    }

    private static bool VerifyContexts(Player me)
    {
        int powerPass = 0, powerFail = 0, relicPass = 0, relicFail = 0;
        var empty = default(CombatTracker.AttributionContext);

        foreach (string name in PowerTypes)
        {
            PowerModel? power = me.Creature.Powers.FirstOrDefault(p => p.GetType().Name == name);
            if (power == null)
            {
                GD.Print($"[FoolLiveProbe] context power missing: {name}");
                powerFail++;
                continue;
            }

            CombatTracker.Instance.RestoreAttributionContext(empty);
            FoolContentPatch.SetPowerContext(power, out var state);
            bool ok = CombatTracker.Instance.ActivePowerSourceId == power.Id.Entry;
            FoolContentPatch.ClearPowerContext(state);
            if (ok) powerPass++; else
            {
                powerFail++;
                GD.Print($"[FoolLiveProbe] context power FAIL {name} got={CombatTracker.Instance.ActivePowerSourceId}");
            }
        }

        foreach (string name in RelicTypes)
        {
            RelicModel? relic = me.Relics.FirstOrDefault(r => r.GetType().Name == name);
            if (relic == null)
            {
                GD.Print($"[FoolLiveProbe] context relic missing: {name}");
                relicFail++;
                continue;
            }

            CombatTracker.Instance.RestoreAttributionContext(empty);
            FoolContentPatch.SetRelicContext(relic, out var state);
            bool ok = CombatTracker.Instance.ActiveRelicId == relic.Id.Entry;
            FoolContentPatch.ClearRelicContext(state);
            if (ok) relicPass++; else
            {
                relicFail++;
                GD.Print($"[FoolLiveProbe] context relic FAIL {name} got={CombatTracker.Instance.ActiveRelicId}");
            }
        }

        GD.Print($"[FoolLiveProbe] context powers={powerPass}/{PowerTypes.Length} relics={relicPass}/{RelicTypes.Length}");
        return powerFail == 0 && relicFail == 0;
    }

[HarmonyPatch]
public static class FoolLiveProbePatches
{
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
    [HarmonyPostfix]
    public static void AfterLaunch(RunState __result) => FoolLiveProbe.AfterLaunch(__result);

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
    [HarmonyPostfix]
    public static void AfterSetUpCombat(CombatState state) => FoolLiveProbe.AfterSetUpCombat(state);
}
}
