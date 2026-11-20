using CommunityStats.UI;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace CommunityStats.Patches;

/// <summary>
/// Hides the contribution panel while the card reward screen is open.
/// The old SelectCard hook only fed a no-op record method and was removed.
/// </summary>
[HarmonyPatch]
public static class CardRewardScreenPatch
{
    [HarmonyPatch(typeof(NCardRewardSelectionScreen), "AfterOverlayOpened")]
    [HarmonyPostfix]
    public static void AfterOverlayOpened()
    {
        Safe.Run(() => ContributionPanel.HideIfVisible());
    }
}
