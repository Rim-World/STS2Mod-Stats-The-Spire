using System.Reflection;
using CommunityStats.Collection;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CommunityStats.Patches;

/// <summary>
/// Fiddle draws extra cards through ModifyHandDraw(Late). Track the bonus
/// so the contribution panel credits the relic instead of dropping the draw.
/// </summary>
[HarmonyPatch]
public static class FiddleDrawCompatPatch
{
    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (TargetMethod() != null) return true;
        Safe.Warn("[CompatPatch] Fiddle.ModifyHandDraw/ModifyHandDrawLate not found; Fiddle draw attribution disabled.");
        return false;
    }

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => AccessTools.DeclaredMethod(typeof(Fiddle), "ModifyHandDrawLate")
        ?? AccessTools.DeclaredMethod(typeof(Fiddle), "ModifyHandDraw");

    [HarmonyPostfix]
    public static void AfterDraw(decimal __result, Fiddle __instance, Player player, decimal count)
    {
        Safe.Run(() =>
        {
            if (player != __instance.Owner) return;

            int extra = (int)(__result - count);
            if (extra > 0)
                ContributionMap.Instance.RecordHandDrawBonus(__instance.Id.Entry, "relic", extra);
        });
    }
}
