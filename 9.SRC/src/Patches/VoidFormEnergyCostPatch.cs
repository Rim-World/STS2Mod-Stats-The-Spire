using System.Reflection;
using CommunityStats.Collection;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CommunityStats.Patches;

/// <summary>
/// Void Form reduces card costs inside combat. Tag the reduction so the
/// contribution panel can credit the power source.
/// </summary>
[HarmonyPatch]
public static class VoidFormEnergyCostPatch
{
    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (TargetMethod() != null) return true;
        Safe.Warn("[CompatPatch] VoidFormPower.TryModifyEnergyCostInCombatLate not found; Void Form cost attribution disabled.");
        return false;
    }

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => AccessTools.DeclaredMethod(typeof(VoidFormPower), "TryModifyEnergyCostInCombatLate")
        ?? AccessTools.DeclaredMethod(typeof(VoidFormPower), "TryModifyEnergyCostInCombat");

    [HarmonyPostfix]
    public static void AfterVoidFormEnergy(bool __result, VoidFormPower __instance, CardModel card, decimal originalCost, decimal modifiedCost)
    {
        Safe.Run(() =>
        {
            if (!__result || originalCost <= modifiedCost) return;

            var entry = __instance.Id.Entry;
            var source = ContributionMap.Instance.GetPowerSource(entry);
            ContributionMap.Instance.TagCostReductionSource(
                card.GetHashCode(),
                source?.SourceId ?? entry,
                source?.SourceType ?? "power");
        });
    }
}
