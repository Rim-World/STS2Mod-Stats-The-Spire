using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace StatsTheSpire.AutoSlayGate;

/// <summary>
/// Test-only helper. Makes NGame.IsReleaseGame() return false so the
/// built-in --autoslay flag can run in the release build.
/// </summary>
[ModInitializer("Initialize")]
public static class AutoSlayGate
{
    private static Harmony? _harmony;

    public static void Initialize()
    {
        _harmony = new Harmony("test.statsthespire.autoslaygate");
        var target = AccessTools.Method(typeof(NGame), "IsReleaseGame");
        var postfix = new HarmonyMethod(AccessTools.Method(typeof(AutoSlayGate), nameof(Postfix)));
        _harmony.Patch(target, postfix: postfix);
    }

    public static bool Postfix(bool __result) => false;
}
