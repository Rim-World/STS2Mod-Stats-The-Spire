using System.Reflection;
using CommunityStats.Util;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace CommunityStats.Collection;

/// <summary>
/// Identifies one concrete run instance. The game seed is only a content
/// identifier: the same seed can be used for many runs. <see cref="StartTime"/>
/// is the game's per-run instance key and is stable across save+resume.
///
/// A zero StartTime means the identity was recovered from a legacy seed-only
/// path. Callers must keep that data in the old flat layout and must not use it
/// to create a new per-run directory.
/// </summary>
public sealed record RunIdentity(long StartTime, string Seed)
{
    public bool HasStartTime => StartTime > 0;
    public bool IsValid => !string.IsNullOrWhiteSpace(Seed);
    public bool IsLegacy => !HasStartTime;

    public string RunKey => HasStartTime
        ? $"{StartTime}_{RunStorage.Sanitize(Seed)}"
        : RunStorage.Sanitize(Seed);

    public static RunIdentity FromManager(RunManager? manager, string? seed)
    {
        long startTime = 0;
        if (manager != null)
        {
            try
            {
                var field = typeof(RunManager).GetField(
                    "_startTime",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(manager) is long value && value > 0)
                    startTime = value;
                else
                    Safe.Warn("[RunIdentity] RunManager._startTime is missing or zero; using legacy seed paths.");
            }
            catch (Exception ex)
            {
                Safe.Warn($"[RunIdentity] failed to read RunManager._startTime: {ex.Message}");
            }
        }

        return new RunIdentity(startTime, seed ?? "");
    }

    public static RunIdentity FromHistory(RunHistory run) => new(run.StartTime, run.Seed ?? "");

    public static RunIdentity FromSerializableRun(SerializableRun run) => new(run.StartTime, run.SerializableRng?.Seed ?? "");

    public RunIdentity WithSeed(string seed) => this with { Seed = seed ?? "" };

    public override string ToString() => $"{RunKey} (start={StartTime}, seed={Seed})";
}
