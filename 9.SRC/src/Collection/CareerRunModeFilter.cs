namespace CommunityStats.Collection;

/// <summary>
/// Run-history game mode filter for career statistics.
/// Values are matched by <c>GameMode</c> name, not by numeric value, because
/// v0.111 uses Custom at slot 3 while v0.113 uses Dread at slot 3.
/// </summary>
public enum CareerRunModeFilter
{
    All,
    Standard,
    Custom,
    Daily,
    Multiplayer,
    Dread,
    Blitz,
}
