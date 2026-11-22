using System;
using System.Text.RegularExpressions;
using CommunityStats.Config;
using MegaCrit.Sts2.Core.Localization;

namespace CommunityStats.Util;

/// <summary>
/// Resolves localized display names for game model IDs using the game's
/// current localization table. Names always follow the game language; there
/// is no mod-language-specific name preference or English fallback table.
///
/// All methods return the input ID unchanged when the lookup fails so the
/// UI never shows an empty cell.
/// </summary>
public static class NameLookup
{
    public static string Encounter(string id) => Lookup("encounters", id, ".title");

    /// <summary>
    /// Resolves an event or ancient display name. Ancient events (PAEL,
    /// VAKUU, ...) are stored in the <c>ancients</c> table, not always in
    /// <c>events</c>; fall back to the ancient resolver for those IDs.
    /// </summary>
    public static string Event(string id)
    {
        var ev = TryLookup("events", id, ".title");
        if (ev != null) return ev;
        return Ancient(id);
    }
    public static string Relic(string id)     => Lookup("relics", id, ".title");
    public static string Card(string id)      => Lookup("cards", id, ".title");
    public static string Monster(string id)   => Lookup("monsters", id, ".name");
    public static string Potion(string id)    => Lookup("potions", id, ".title");

    /// <summary>
    /// Localized name for an Ancient elder (NEOW/PAEL/...).
    /// </summary>
    public static string Ancient(string elderId)
    {
        if (string.IsNullOrEmpty(elderId)) return "";
        var anc = TryLookup("ancients", elderId, ".title");
        if (anc != null) return anc;
        var ev = TryLookup("events", elderId, ".title");
        if (ev != null) return ev;
        var enc = TryLookup("encounters", elderId, ".title");
        if (enc != null) return enc;
        return elderId;
    }

    /// <summary>
    /// Resolves a death cause id (encounter/event id, or "ABANDONED").
    /// </summary>
    public static string DeathCause(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (id == "ABANDONED") return L.Get("career.cause_abandoned");

        var anc = TryLookup("ancients", id, ".title");
        if (anc != null) return anc;
        var enc = TryLookup("encounters", id, ".title");
        if (enc != null) return enc;
        var ev = TryLookup("events", id, ".title");
        if (ev != null) return ev;
        var mon = TryLookup("monsters", id, ".name");
        if (mon != null) return mon;
        return id;
    }

    /// <summary>Localized "Act N" label.</summary>
    public static string ActLabel(int actIndex)
    {
        try { return string.Format(L.Get("career.act_n"), actIndex); }
        catch { return "Act " + actIndex; }
    }

    // ── internal ──────────────────────────────────────────────

    private static string Lookup(string table, string id, string suffix)
    {
        return TryLookup(table, id, suffix) ?? id;
    }

    private static string? TryLookup(string table, string id, string suffix)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            var key = id + suffix;
            if (!LocString.Exists(table, key)) return null;

            // Titles now go through SmartFormat instead of GetRawText. Other
            // localisation mods (for example FoolMelodyReplacer) store
            // placeholders such as {MelodyWaText} in raw card titles and
            // resolve them in a SmartFormat source. Our labels do not parse
            // BBCode, so resolve the placeholders first and strip the tags.
            var formatted = FormatDisplayText(table, key);
            if (formatted != null) return formatted;

            return ToDisplayText(LocManager.Instance.GetTable(table).GetRawText(key));
        }
        catch (Exception)
        {
            return null;
        }
    }

    // BBCode is used by the game and by localisation mods to embed icons,
    // colours, and melody symbols. A plain Godot Label must never receive it.
    private static readonly Regex BbCodeTag = new(@"\[[^\[\]]*\]", RegexOptions.Compiled);
    private static readonly Regex ImageTag = new(@"\[img\][^\[\]]*?\[/img\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MelodyToken = new(@"\{Melody(Wa|Cho|Ki)(?:Text)?\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Resolves a LocString through SmartFormat, then converts the result to
    /// plain label text. Returns null when the formatted text is unusable so
    /// the caller can fall back to the raw string.
    /// </summary>
    private static string? FormatDisplayText(string table, string key)
    {
        try
        {
            var loc = new LocString(table, key);
            var raw = loc.GetRawText();
            var display = ToDisplayText(loc.GetFormattedText());
            var token = MelodyToken.Match(raw);

            if (token.Success)
            {
                var rawTail = MelodyToken.Replace(raw, "");
                var tail = rawTail.Trim();

                // Image-only display modes (FoolMelodyReplacer's "Replace")
                // resolve the title to an image plus the untouched suffix.
                // Because the suffix is the whole visible text, rebuild the
                // title from the raw text instead of guessing one melody word;
                // this also handles titles with two melody tokens. "Alongside"
                // already includes the word after the image, so its display
                // does not start with the raw suffix.
                if (string.IsNullOrEmpty(display)
                    || (!string.IsNullOrEmpty(tail)
                        && display.StartsWith(tail, StringComparison.OrdinalIgnoreCase)))
                {
                    display = ToDisplayText(raw);
                }
            }

            if (!string.IsNullOrWhiteSpace(display)) return display;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts formatted localisation text to plain label text, replacing
    /// melody placeholders with their localised short names.
    /// </summary>
    private static string ToDisplayText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = ImageTag.Replace(text, "");
        text = text.Replace("[lb]", "[").Replace("[rb]", "]");
        text = BbCodeTag.Replace(text, "");
        text = MelodyToken.Replace(text, m => MelodyShortName(m.Groups[1].Value));
        return text.Trim();
    }

    /// <summary>
    /// Short melody name used when an image-only display cannot supply text.
    /// This is a fallback for third-party localisation mods; the normal path
    /// is the game's own localized title.
    /// </summary>
    private static string MelodyShortName(string tag)
    {
        if (L.Current == L.Lang.CN)
            return tag switch { "Wa" => "哇", "Cho" => "啾", "Ki" => "叽", _ => "♪" };
        return tag switch { "Wa" => "Wah", "Cho" => "Cho", "Ki" => "Ki", _ => "♪" };
    }
}
