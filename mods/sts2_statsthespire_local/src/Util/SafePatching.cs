using MegaCrit.Sts2.Core.Logging;
using HarmonyLib;

namespace CommunityStats.Util;

/// <summary>
/// Wraps actions in try/catch to prevent mod crashes from disrupting the game.
/// </summary>
public static class Safe
{
    private const string Tag = "[StatsTheSpire]";

    public static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log.Warn($"{Tag} {ex.Message}\n{ex.StackTrace}"); }
    }

    public static T? Run<T>(Func<T> func, T? fallback = default)
    {
        try { return func(); }
        catch (Exception ex)
        {
            Log.Warn($"{Tag} {ex.Message}\n{ex.StackTrace}");
            return fallback;
        }
    }

    public static async void RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Log.Warn($"{Tag} Async error: {ex.Message}\n{ex.StackTrace}"); }
    }

    public static void Info(string msg) => Log.Info($"{Tag} {msg}");
    public static void Warn(string msg) => Log.Warn($"{Tag} {msg}");

    /// <summary>
    /// Patch the first method name that exists on <paramref name="type"/>.
    /// Used when a game method was renamed or moved between versions.
    /// </summary>
    public static void TryPatchFirst(Harmony harmony, Type type, string[] methodNames,
        HarmonyMethod prefix, HarmonyMethod postfix, string tag)
    {
        foreach (var methodName in methodNames)
        {
            var method = AccessTools.DeclaredMethod(type, methodName);
            if (method == null) continue;

            try
            {
                harmony.Patch(method, prefix, postfix);
                return;
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} {tag} Failed {type.Name}.{methodName}: {ex.Message}");
                return;
            }
        }

        Log.Warn($"{Tag} {tag} No compatible hook found: {type.Name} ({string.Join("/", methodNames)})");
    }
}
