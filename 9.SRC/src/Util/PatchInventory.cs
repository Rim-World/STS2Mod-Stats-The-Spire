using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CommunityStats.Util;

/// <summary>
/// Writes a runtime inventory of every Harmony target owned by this mod.
/// A missing target that is silently skipped by a compatibility patch is the
/// easiest failure mode to miss in a log; this makes the bound set explicit.
/// </summary>
public static class PatchInventory
{
    private const string OwnerId = "com.communitystats.sts2";

    public static void Log(Harmony harmony)
    {
        try
        {
            var rows = new List<(string Target, string Kind, string PatchClass)>();
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null) continue;

                AddRows(rows, method, "prefix", info.Prefixes);
                AddRows(rows, method, "postfix", info.Postfixes);
                AddRows(rows, method, "transpiler", info.Transpilers);
                AddRows(rows, method, "finalizer", info.Finalizers);
            }

            var ours = rows
                .Where(r => r.PatchClass.Length > 0)
                .OrderBy(r => r.Target, StringComparer.Ordinal)
                .ThenBy(r => r.Kind, StringComparer.Ordinal)
                .ToList();

            foreach (var row in ours)
                Safe.Info($"[PatchInventory] {row.Kind} {row.Target} <- {row.PatchClass}");

            Safe.Info($"[PatchInventory] bound entries={ours.Count}, distinct targets={ours.Select(r => r.Target).Distinct().Count()}");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[PatchInventory] failed: {ex.Message}");
        }
    }

    private static void AddRows(List<(string Target, string Kind, string PatchClass)> rows,
        MethodBase target, string kind, IEnumerable<Patch>? patches)
    {
        if (patches == null) return;
        foreach (var patch in patches)
        {
            if (!string.Equals(patch.owner, OwnerId, StringComparison.Ordinal)) continue;
            var targetName = target.DeclaringType == null
                ? target.Name
                : $"{target.DeclaringType.FullName}.{target.Name}";
            var patchClass = patch.PatchMethod.DeclaringType?.Name ?? patch.PatchMethod.Name;
            rows.Add((targetName, kind, patchClass));
        }
    }
}
