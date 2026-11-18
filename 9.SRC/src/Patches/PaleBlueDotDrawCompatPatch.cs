using System.Reflection;
using CommunityStats.Collection;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CommunityStats.Patches;

/// <summary>
/// Pale Blue Dot adds a draw through ModifyHandDraw. Attribute the extra
/// draw to the power source that applied it.
/// </summary>
[HarmonyPatch]
public static class PaleBlueDotDrawCompatPatch
{
    [HarmonyPrepare]
    public static bool Prepare() => TargetMethod() != null;

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => AccessTools.DeclaredMethod(typeof(PaleBlueDotPower), "ModifyHandDraw");

    [HarmonyPostfix]
    public static void AfterDraw(decimal __result, PaleBlueDotPower __instance, Player player, decimal count)
    {
        Safe.Run(() =>
        {
            if (player != __instance.Owner.Player) return;

            int extra = (int)(__result - count);
            if (extra <= 0) return;

            var source = ContributionMap.Instance.GetPowerSource(__instance.Id.Entry);
            if (source != null)
                ContributionMap.Instance.RecordHandDrawBonus(source.SourceId, source.SourceType, extra);
        });
    }
}
