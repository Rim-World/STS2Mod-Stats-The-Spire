using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CommunityStats.Util;

/// <summary>
/// Read-only helpers for the current run's mode, room, local player, and
/// modifier state. Kept in one place so display code does not each invent a
/// different definition of "Blitz", "Boss", or "local player".
/// </summary>
public static class RunContextInfo
{
    public static IRunState? CurrentState
    {
        get
        {
            try { return RunManager.Instance?.DebugOnlyGetState(); }
            catch { return null; }
        }
    }

    /// <summary>
    /// Blitz exists only in v0.113. Compare the enum name so this compiles
    /// against v0.111, whose GameMode enum has no Blitz member.
    /// </summary>
    public static bool IsBlitz(IRunState? state)
    {
        try
        {
            return string.Equals(state?.GameMode.ToString(), "Blitz", System.StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool IsBlitz() => IsBlitz(CurrentState);

    public static bool IsBossRoom(IRunState? state)
    {
        if (state == null) return false;

        try
        {
            // Use the current room, not the current map point. After a boss
            // reward the map point can still be Boss while the next combat is
            // already an ordinary room.
            return state.CurrentRoom?.RoomType == RoomType.Boss;
        }
        catch { return false; }
    }

    public static bool IsBossRoom() => IsBossRoom(CurrentState);

    public static bool HasBigGameHunter(IRunState? state)
    {
        try
        {
            return state?.Modifiers?.Any(m => m is BigGameHunter) == true;
        }
        catch { return false; }
    }

    /// <summary>
    /// The local player. Multiplayer must never fall back to players[0],
    /// because that is another human's gold and discount relics.
    /// </summary>
    public static Player? GetLocalPlayer(IRunState? state)
    {
        if (state == null) return null;

        try
        {
            var me = LocalContext.GetMe(state);
            if (me != null) return me;
        }
        catch { }

        try
        {
            if (state.Players.Count == 1) return state.Players[0];
        }
        catch { }

        return null;
    }

    public static Player? GetLocalPlayer() => GetLocalPlayer(CurrentState);
}
