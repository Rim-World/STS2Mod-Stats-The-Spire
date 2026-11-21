using System;
using CommunityStats.Collection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.EventSourceProbe;

[ModInitializer("Initialize")]
public static class EventSourceProbe
{
    public static void Initialize()
    {
        var harmony = new Harmony("stats_event_source_probe_160");
        var target = AccessTools.Method(typeof(CombatManager), nameof(CombatManager.SetUpCombat));
        if (target == null)
        {
            GD.Print("[EventSourceProbe] FAIL target missing");
            return;
        }

        harmony.Patch(target, postfix: new HarmonyMethod(
            AccessTools.Method(typeof(EventSourceProbe), nameof(AfterSetUpCombat))));
        GD.Print("[EventSourceProbe] patched");
    }

    private static void AfterSetUpCombat(CombatState state)
    {
        var encounter = state?.Encounter?.Id.Entry ?? "";
        if (!encounter.Contains("BATTLEWORN", StringComparison.OrdinalIgnoreCase)) return;

        GD.Print($"[EventSourceProbe] event combat detected: {encounter}");
        Callable.From(InjectSource).CallDeferred();
    }

    private static void InjectSource()
    {
        try
        {
            var tracker = CombatTracker.Instance;
            if (!tracker.LiveCombatInProgress)
            {
                GD.Print("[EventSourceProbe] FAIL tracker not live");
                return;
            }

            tracker.OnDamageDealt(1, 0, "TEST_EVENT_SOURCE", false, 0, 0);
            GD.Print("[EventSourceProbe] injected TEST_EVENT_SOURCE");
        }
        catch (Exception ex)
        {
            GD.Print($"[EventSourceProbe] FAIL {ex.GetType().Name}: {ex.Message}");
        }
    }
}
