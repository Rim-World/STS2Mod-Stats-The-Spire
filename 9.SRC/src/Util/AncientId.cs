namespace CommunityStats.Util;

/// <summary>
/// Normalizes a localization entry key into the relic id used by the
/// Ancient pool tables.
///
/// Sea Glass uses character-specific keys such as
/// "SEA_GLASS.SILENT.title"; the pool table stores "SEA_GLASS".
/// Regular relics use "RELIC_ID.title".
/// </summary>
public static class AncientId
{
    public static string Normalize(string? locEntryKey)
    {
        if (string.IsNullOrEmpty(locEntryKey)) return "";
        int dot = locEntryKey.IndexOf('.');
        return dot > 0 ? locEntryKey.Substring(0, dot) : locEntryKey;
    }
}
