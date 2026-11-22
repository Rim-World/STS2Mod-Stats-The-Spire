using System;
using System.Threading.Tasks;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;

namespace StatsTheSpire.NameProbe;

/// <summary>
/// Test-only probe for the 1.7.2 localised-name fix. It compares the game's
/// raw card title, the SmartFormatted title, and the text produced by
/// NameLookup while FoolMelodyReplacer is active.
/// </summary>
[ModInitializer("Initialize")]
public static class NameProbe
{
    private static bool _started;

    public static void Initialize()
    {
        GD.Print("[NameProbe] initialized");
        Callable.From(Run).CallDeferred();
    }

    private static async void Run()
    {
        if (_started) return;
        _started = true;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.Print("[NameProbe] FAIL: SceneTree missing");
            return;
        }

        try
        {
            for (int i = 0; i < 5; i++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            int cnPass = CheckLanguage(true, "CN");
            L.Current = L.Lang.EN;
            int enPass = CheckLanguage(false, "EN");
            int total = 14;
            GD.Print($"[NameProbe] RESULT CN+EN {cnPass + enPass}/{total} PASS");
            tree.Quit(cnPass + enPass == total ? 0 : 1);
        }
        catch (Exception ex)
        {
            GD.Print($"[NameProbe] FAIL exception: {ex}");
            tree.Quit(1);
        }
    }

    private static int CheckLanguage(bool cn, string label)
    {
        var cases = new (string Id, string Prefix, string Suffix)[]
        {
            ("WAH_SYMPHONY", "Wa", cn ? "交响乐" : "Symphony"),
            ("WAH_TOUCH", "Wa", cn ? "触碰" : "Touch"),
            ("CHO_READY", "Cho", cn ? "就绪" : "Ready"),
            ("WAH_KI", "Wa", cn ? "叽" : "Ki"),
            ("CHO_KI", "Cho", cn ? "叽" : "Ki"),
            ("STRIKE_FOOL", "", cn ? "打击" : "Strike"),
            ("DEFEND_FOOL", "", cn ? "防御" : "Defend"),
        };
        var expectedPrefix = new System.Collections.Generic.Dictionary<string, string>
        {
            ["Wa"] = cn ? "哇" : "Wah",
            ["Cho"] = cn ? "啾" : "Cho",
            ["Ki"] = cn ? "叽" : "Ki",
        };

        int pass = 0;
        foreach (var (id, prefixTag, suffix) in cases)
        {
            string raw, formatted;
            try
            {
                var loc = new LocString("cards", id + ".title");
                raw = loc.GetRawText();
                formatted = loc.GetFormattedText();
            }
            catch (Exception ex)
            {
                GD.Print($"[NameProbe] FAIL {label} {id}: loc={ex.Message}");
                continue;
            }

            string name;
            try { name = NameLookup.Card(id); }
            catch (Exception ex)
            {
                GD.Print($"[NameProbe] FAIL {label} {id}: lookup={ex.Message}");
                continue;
            }

            bool rawHasToken = raw.Contains("{Melody", StringComparison.Ordinal);
            bool markupFree = !name.Contains("{Melody", StringComparison.Ordinal)
                && !name.Contains("[img]", StringComparison.Ordinal)
                && !name.Contains("[/", StringComparison.Ordinal)
                && !name.Contains("[color", StringComparison.Ordinal)
                && !name.Contains("[lb]", StringComparison.Ordinal);
            bool suffixOk = name.Contains(suffix, StringComparison.OrdinalIgnoreCase);
            bool prefixOk = prefixTag.Length == 0
                || name.StartsWith(expectedPrefix[prefixTag], StringComparison.OrdinalIgnoreCase);
            bool duplicatePrefix = prefixTag.Length > 0
                && name.StartsWith(expectedPrefix[prefixTag] + expectedPrefix[prefixTag],
                    StringComparison.OrdinalIgnoreCase);

            bool ok = (prefixTag.Length == 0 || rawHasToken)
                && markupFree && suffixOk && prefixOk && !duplicatePrefix;
            if (ok) pass++;
            GD.Print($"[NameProbe] {(ok ? "PASS" : "FAIL")} {label} {id}: raw=[{raw}] formatted=[{formatted}] name=[{name}] token={rawHasToken} markupFree={markupFree} suffix={suffixOk} prefix={prefixOk} dup={duplicatePrefix}");
        }

        GD.Print($"[NameProbe] {label} RESULT {pass}/{cases.Length} PASS");
        return pass;
    }
}
