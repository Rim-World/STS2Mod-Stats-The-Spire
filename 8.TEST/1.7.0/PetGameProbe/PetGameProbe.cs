using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.ValueProps;

namespace StatsTheSpire.PetGameProbe;

/// <summary>
/// Deterministic 1.7.0 pet-accounting probe. Runs pure tracker/JSON/UI checks
/// at mod init, then uses real game commands in the first combat:
/// Wuppy redirect with overkill, BoneShards kill, Sacrifice kill.
/// </summary>
[ModInitializer("Initialize")]
public static class PetGameProbe
{
    private static int _pass;
    private static int _fail;
    private static bool _liveStarted;
    private static bool _blockAutoPlay;
    private static bool _syntheticDone;

    [ThreadStatic] private static bool _probeAutoPlay;

    public static void Initialize()
    {
        GD.Print("[PetGameProbe] initialized");
        try
        {
            var harmony = new Harmony("test.pet_game_probe_170");
            var gateTarget = AccessTools.Method(typeof(NGame), "IsReleaseGame");
            if (gateTarget != null)
            {
                harmony.Patch(gateTarget,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(PetGameProbe), nameof(ForceReleaseFalse))));
                GD.Print("[PetGameProbe] IsReleaseGame gate patched");
            }
            harmony.PatchAll(typeof(PetGameProbe).Assembly);
            GD.Print("[PetGameProbe] Harmony patched");
        }
        catch (Exception ex)
        {
            GD.Print($"[PetGameProbe] Harmony failed: {ex}");
        }

        try
        {
            if (!_syntheticDone)
            {
                _syntheticDone = true;
                RunSynthetic();
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[PetGameProbe] synthetic fatal: {ex}");
            _fail++;
        }

        if (OS.GetCmdlineArgs().Contains("--petprobe-ui-only"))
        {
            GD.Print($"[PetGameProbe] UI_ONLY_RESULT {(_fail == 0 ? "PASS" : "FAIL")} pass={_pass} fail={_fail}");
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                int exitCode = _fail == 0 ? 0 : 1;
                tree.ProcessFrame += () => tree.Quit(exitCode);
                return;
            }
        }

        try
        {
            var cm = CombatManager.Instance;
            if (cm != null)
            {
                cm.CombatBegan += OnCombatBegan;
                GD.Print("[PetGameProbe] CombatBegan subscribed");
            }
            else
            {
                GD.Print("[PetGameProbe] CombatManager.Instance null");
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[PetGameProbe] subscribe failed: {ex.Message}");
        }
    }

    public static bool ForceReleaseFalse(bool __result) => false;

    private static void Check(string name, bool ok, string detail)
    {
        GD.Print($"[PetGameProbe] {(ok ? "PASS" : "FAIL")} {name}: {detail}");
        if (ok) _pass++; else _fail++;
    }

    private static int Pet(string id)
        => CombatTracker.Instance.GetCurrentCombatData().TryGetValue(id, out var a) ? a.PetAbsorbed : 0;
    private static int Waste(string id)
        => CombatTracker.Instance.GetCurrentCombatData().TryGetValue(id, out var a) ? a.PetWasted : 0;

    // ── Synthetic: tracker, migration, JSON, files, UI ───────────────────
    private static void RunSynthetic()
    {
        RunTrackerChecks();
        RunMigrationChecks();
        RunPersistenceChecks();
        RunUiChecks();
        RunUiCategoryRegressionChecks();
        RunUiSubEntryCostChecks();
        RunUiStandaloneSelfDamageChecks();
        CombatTracker.Instance.ForceResetAllContext();
        RunContributionAggregator.Instance.Reset();
    }

    private static void RunTrackerChecks()
    {
        var tracker = CombatTracker.Instance;
        void NewCombat(string id)
        {
            tracker.ForceResetAllContext();
            RunContributionAggregator.Instance.Reset();
            tracker.OnCombatStart(id, "monster", 1);
        }

        NewCombat("PROBE_LIFO");
        tracker.OnOstySummoned("PROBE_A", "card", 3);
        tracker.OnOstySummoned("PROBE_B", "card", 5);
        tracker.OnOstyAbsorbedDamage(4);
        var b = tracker.GetCurrentCombatData().TryGetValue("PROBE_B\u0001OSTY", out var bAcc) ? bAcc : null;
        Check("synthetic-lifo-first", b?.PetAbsorbed == 4 && b?.EffectiveBlock == 0, $"pet={b?.PetAbsorbed} eff={b?.EffectiveBlock}");
        tracker.OnOstyAbsorbedDamage(3);
        var a = tracker.GetCurrentCombatData().TryGetValue("PROBE_A\u0001OSTY", out var aAcc) ? aAcc : null;
        b = tracker.GetCurrentCombatData().TryGetValue("PROBE_B\u0001OSTY", out var bAcc2) ? bAcc2 : null;
        Check("synthetic-lifo-drain", a?.PetAbsorbed == 2 && b?.PetAbsorbed == 5, $"A={a?.PetAbsorbed} B={b?.PetAbsorbed}");
        tracker.OnOstyAbsorbedDamage(2);
        var parent = tracker.GetCurrentCombatData().TryGetValue("OSTY", out var pAcc) ? pAcc : null;
        Check("synthetic-lifo-remainder", parent?.PetAbsorbed == 1 && parent?.SourceType == "pet",
            $"parent={parent?.PetAbsorbed} type={parent?.SourceType}");
        Check("synthetic-lifo-no-block", tracker.GetCurrentCombatData().Values.All(x => x.EffectiveBlock == 0),
            $"effSum={tracker.GetCurrentCombatData().Values.Sum(x => x.EffectiveBlock)}");

        NewCombat("PROBE_WUPPY");
        tracker.OnDamageDealt(7, 0, null, false, 0x21, 0x99, isWuppyReceiver: true);
        var w = tracker.GetCurrentCombatData().TryGetValue("WUPPY", out var wAcc) ? wAcc : null;
        Check("synthetic-wuppy", w?.PetAbsorbed == 7 && w?.SourceType == "pet" && w?.EffectiveBlock == 0,
            $"pet={w?.PetAbsorbed} type={w?.SourceType} eff={w?.EffectiveBlock}");
        tracker.OnDamageDealt(7, 7, null, false, 0x21, 0x99, isWuppyReceiver: true);
        Check("synthetic-wuppy-blocked", w?.PetAbsorbed == 7, $"pet={w?.PetAbsorbed}");

        NewCombat("PROBE_WASTE");
        tracker.OnCardPlayStarted("BONE_SHARDS", 0x31);
        tracker.OnOstySummoned("BONE_SHARDS", "card", 5);
        tracker.OnOstyKilled();
        tracker.OnOstyKilled();
        var bone = tracker.GetCurrentCombatData().TryGetValue("BONE_SHARDS", out var boneAcc) ? boneAcc : null;
        Check("synthetic-waste", bone?.PetWasted == 5 && bone?.EffectiveBlock == 0,
            $"waste={bone?.PetWasted} eff={bone?.EffectiveBlock}");
        tracker.OnCardPlayFinished();
        tracker.OnOstySummoned("BONE_SHARDS", "card", 4);
        tracker.OnOstyKilled();
        Check("synthetic-waste-no-active-card", bone?.PetWasted == 5, $"waste={bone?.PetWasted}");
        Check("synthetic-total-defense", bone?.TotalDefense == -5, $"total={bone?.TotalDefense}");

        NewCombat("PROBE_FORMULA");
        var f = new ContributionAccum
        {
            EffectiveBlock = 3, PetAbsorbed = 5, PetWasted = 2, SelfDamage = 1,
            ModifierBlock = 1, MitigatedByDebuff = 1, MitigatedByBuff = 1,
            MitigatedByStrReduction = 1, UpgradeBlock = 1,
        };
        Check("synthetic-formula", f.TotalDefense == 10, $"total={f.TotalDefense}");

        NewCombat("PROBE_GENERATED_WASTE");
        int generatedHash = 0x61;
        ContributionMap.Instance.RecordCardOrigin(generatedHash, "GENERATOR", "card");
        tracker.OnCardPlayStarted("BONE_SHARDS", generatedHash);
        tracker.OnOstySummoned("INITIAL", "card", 6);
        tracker.OnOstyKilled(6);
        var genTop = tracker.GetCurrentCombatData().TryGetValue("BONE_SHARDS", out var genTopAcc) ? genTopAcc : null;
        var genChild = tracker.GetCurrentCombatData().TryGetValue("BONE_SHARDS\u0001GENERATOR", out var genChildAcc) ? genChildAcc : null;
        Check("synthetic-generated-waste-top-level",
            genTop?.PetWasted == 6 && genTop?.OriginSourceId == null && (genChild?.PetWasted ?? 0) == 0,
            $"topWaste={genTop?.PetWasted} topOrigin={genTop?.OriginSourceId ?? "null"} childWaste={genChild?.PetWasted}");
        tracker.ForceResetAllContext();
    }

    private static void RunMigrationChecks()
    {
        var asm = typeof(ContributionPersistence).Assembly;
        var dtoType = asm.GetType("CommunityStats.Util.ContributionPersistence+SourceDto");
        var fromDto = typeof(ContributionPersistence).GetMethod("FromDto", BindingFlags.NonPublic | BindingFlags.Static);
        var toDto = typeof(ContributionPersistence).GetMethod("ToDto", BindingFlags.NonPublic | BindingFlags.Static);
        var jsonOpts = (JsonSerializerOptions?)typeof(ContributionPersistence)
            .GetField("JsonOpts", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        if (dtoType == null || fromDto == null || toDto == null || jsonOpts == null)
        {
            Check("synthetic-migration-reflection", false, "reflection members missing");
            return;
        }

        ContributionAccum ConvertJson(string json)
            => (ContributionAccum)fromDto.Invoke(null, new[] { JsonSerializer.Deserialize(json, dtoType, jsonOpts)! })!;

        var oldParent = ConvertJson("{\"SourceId\":\"OSTY\",\"SourceType\":\"osty\",\"EffectiveBlock\":9}");
        Check("synthetic-legacy-parent", oldParent.SourceType == "pet" && oldParent.EffectiveBlock == 0
            && oldParent.PetAbsorbed == 9 && oldParent.TotalDefense == 9,
            $"type={oldParent.SourceType} eff={oldParent.EffectiveBlock} pet={oldParent.PetAbsorbed} total={oldParent.TotalDefense}");
        var oldChild = ConvertJson("{\"SourceId\":\"UNLEASH\",\"SourceType\":\"card\",\"EffectiveBlock\":3,\"OriginSourceId\":\"OSTY\"}");
        Check("synthetic-legacy-child", oldChild.EffectiveBlock == 0 && oldChild.PetAbsorbed == 3,
            $"eff={oldChild.EffectiveBlock} pet={oldChild.PetAbsorbed}");
        var oldNeg = ConvertJson("{\"SourceId\":\"BONE_SHARDS\",\"SourceType\":\"card\",\"EffectiveBlock\":-5}");
        Check("synthetic-legacy-negative", oldNeg.EffectiveBlock == 0 && oldNeg.PetWasted == 5 && oldNeg.TotalDefense == -5,
            $"eff={oldNeg.EffectiveBlock} waste={oldNeg.PetWasted} total={oldNeg.TotalDefense}");

        var idem = oldParent;
        for (int i = 0; i < 3; i++) idem = (ContributionAccum)fromDto.Invoke(null, new[] { toDto.Invoke(null, new object[] { idem })! })!;
        Check("synthetic-migration-idempotent", idem.PetAbsorbed == 9 && idem.PetWasted == 0 && idem.EffectiveBlock == 0,
            $"pet={idem.PetAbsorbed} waste={idem.PetWasted} eff={idem.EffectiveBlock}");
        var round = new ContributionAccum
        {
            SourceId = "ROUND", SourceType = "card", EffectiveBlock = 4, PetAbsorbed = 11, PetWasted = 6,
        };
        var json = JsonSerializer.Serialize(toDto.Invoke(null, new object[] { round })!, dtoType, jsonOpts);
        var back = (ContributionAccum)fromDto.Invoke(null, new[] { JsonSerializer.Deserialize(json, dtoType, jsonOpts)! })!;
        Check("synthetic-json-roundtrip", back.EffectiveBlock == 4 && back.PetAbsorbed == 11 && back.PetWasted == 6,
            $"eff={back.EffectiveBlock} pet={back.PetAbsorbed} waste={back.PetWasted}");
    }

    private static void RunPersistenceChecks()
    {
        var identity = new RunIdentity(1795400001L, "petgameprobe170");
        var dir = RunStorage.DirectoryFor(identity);
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            ContributionPersistence.SetActiveRun(identity);
            ContributionPersistence.SaveCombat(2, new Dictionary<string, ContributionAccum>
            {
                ["BONE_SHARDS"] = new() { SourceId = "BONE_SHARDS", SourceType = "card", EffectiveBlock = 2, PetWasted = 6 },
                ["WUPPY"] = new() { SourceId = "WUPPY", SourceType = "pet", PetAbsorbed = 9 },
            });
            var assembled = ContributionPersistence.LoadRunSummary(identity);
            var b = assembled != null && assembled.TryGetValue("BONE_SHARDS", out var ba) ? ba : null;
            var w = assembled != null && assembled.TryGetValue("WUPPY", out var wa) ? wa : null;
            Check("synthetic-save-combat-load", b?.PetWasted == 6 && b?.EffectiveBlock == 2 && w?.PetAbsorbed == 9,
                $"bone waste={b?.PetWasted} eff={b?.EffectiveBlock} wuppy pet={w?.PetAbsorbed}");

            ContributionPersistence.SaveRunSummary(identity, new Dictionary<string, ContributionAccum>
            {
                ["BONE_SHARDS"] = new() { SourceId = "BONE_SHARDS", SourceType = "card", PetWasted = 7 },
                ["OSTY"] = new() { SourceId = "OSTY", SourceType = "osty", EffectiveBlock = 4 },
            });
            var summary = ContributionPersistence.LoadRunSummary(identity);
            var sb = summary != null && summary.TryGetValue("BONE_SHARDS", out var sba) ? sba : null;
            var so = summary != null && summary.TryGetValue("OSTY", out var soa) ? soa : null;
            Check("synthetic-save-summary-load", sb?.PetWasted == 7 && so?.PetAbsorbed == 4 && so?.SourceType == "pet",
                $"waste={sb?.PetWasted} osty type={so?.SourceType} pet={so?.PetAbsorbed}");

            var liveSnap = new LiveContributionSnapshot
            {
                EncounterId = "PROBE_LIVE",
                EncounterType = "monster",
                Floor = 2,
                CombatInProgress = true,
                CurrentCombat = new Dictionary<string, ContributionAccum>
                {
                    ["SACRIFICE"] = new() { SourceId = "SACRIFICE", SourceType = "card", PetWasted = 3 },
                    ["SACRIFICE\u0001OSTY"] = new() { SourceId = "SACRIFICE", SourceType = "card", PetAbsorbed = 2, OriginSourceId = "OSTY" },
                },
                RunTotals = new Dictionary<string, ContributionAccum>
                {
                    ["WUPPY"] = new() { SourceId = "WUPPY", SourceType = "pet", PetAbsorbed = 5 },
                },
            };
            ContributionPersistence.SaveLiveState(liveSnap);
            var live = ContributionPersistence.LoadLiveState(identity);
            var lw = live?.RunTotals != null && live.RunTotals.TryGetValue("WUPPY", out var lwa) ? lwa : null;
            var ls = live?.CurrentCombat != null && live.CurrentCombat.TryGetValue("SACRIFICE", out var lsa) ? lsa : null;
            var lsc = live?.CurrentCombat != null && live.CurrentCombat.TryGetValue("SACRIFICE\u0001OSTY", out var lsca) ? lsca : null;
            Check("synthetic-live-snapshot", lw?.PetAbsorbed == 5 && ls?.PetWasted == 3 && lsc?.PetAbsorbed == 2,
                $"wuppy={lw?.PetAbsorbed} waste={ls?.PetWasted} pet={lsc?.PetAbsorbed}");
        }
        finally
        {
            try { ContributionPersistence.ClearRunContext(); } catch { }
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
            Check("synthetic-file-cleanup", !Directory.Exists(dir), dir);
        }

        const string legacySeed = "petgameprobe170legacy";
        var legacyPath = Path.Combine(ModConfig.ContributionsDir, legacySeed + "_summary.json");
        try
        {
            Directory.CreateDirectory(ModConfig.ContributionsDir);
            File.WriteAllText(legacyPath,
                "{\"StartTime\":0,\"Seed\":\"" + legacySeed + "\",\"Kind\":\"summary\",\"Sources\":[" +
                "{\"SourceId\":\"OSTY\",\"SourceType\":\"osty\",\"EffectiveBlock\":9}," +
                "{\"SourceId\":\"BONE_SHARDS\",\"SourceType\":\"card\",\"EffectiveBlock\":-4}]}");
            var legacy = ContributionPersistence.LoadRunSummary(new RunIdentity(0, legacySeed));
            var lo = legacy != null && legacy.TryGetValue("OSTY", out var loa) ? loa : null;
            var lb = legacy != null && legacy.TryGetValue("BONE_SHARDS", out var lba) ? lba : null;
            Check("synthetic-legacy-file-migration", lo?.SourceType == "pet" && lo?.PetAbsorbed == 9
                && lb?.PetWasted == 4 && lb?.EffectiveBlock == 0,
                $"osty type={lo?.SourceType} pet={lo?.PetAbsorbed} waste={lb?.PetWasted} eff={lb?.EffectiveBlock}");
        }
        finally
        {
            try { if (File.Exists(legacyPath)) File.Delete(legacyPath); } catch { }
            Check("synthetic-legacy-cleanup", !File.Exists(legacyPath), legacyPath);
        }
    }

    private static void RunUiChecks()
    {
        try
        {
            var data = new Dictionary<string, ContributionAccum>
            {
                ["BONE_SHARDS"] = new() { SourceId = "BONE_SHARDS", SourceType = "card", EffectiveBlock = 3, PetWasted = 5 },
                ["WUPPY"] = new() { SourceId = "WUPPY", SourceType = "pet", PetAbsorbed = 7 },
                ["OSTY"] = new() { SourceId = "OSTY", SourceType = "pet", PetAbsorbed = 2 },
            };
            var chart = ContributionChart.Create(data, "PET_PROBE");
            var children = chart.GetChildren().OfType<Node>().ToList();

            string? HeaderText(Node n)
            {
                if (n is not PanelContainer pc) return null;
                return Descendants<Label>(pc).FirstOrDefault()?.Text;
            }

            int defenseIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.defense"));
            int drawIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.draw"));
            Check("ui-defense-section-found", defenseIdx >= 0 && drawIdx > defenseIdx, $"defense={defenseIdx} draw={drawIdx}");
            if (defenseIdx < 0 || drawIdx <= defenseIdx)
            {
                chart.Free();
                return;
            }

            var section = children.Skip(defenseIdx + 1).Take(drawIdx - defenseIdx - 1).ToList();
            var labels = section.SelectMany(Descendants<Label>).Select(l => l.Text).ToList();
            var panels = section.SelectMany(Descendants<Panel>).ToList();
            string wasteSuffix = L.Get("chart.pet_wasted_suffix");
            string petPrefix = L.Get("source.pet_prefix");
            string boneName = NameLookup.Card("BONE_SHARDS");
            string wuppyName = NameLookup.Monster("WUPPY");
            Check("ui-pet-waste-row", labels.Any(t => t.Contains(wasteSuffix) && t.Contains(boneName)),
                string.Join(" | ", labels));
            Check("ui-pet-row-prefix", labels.Any(t => t.Contains(petPrefix) && t.Contains(wuppyName)),
                string.Join(" | ", labels));
            Check("ui-negative-value-row", labels.Contains("-5"), string.Join(" | ", labels));

            var petColor = (Color?)typeof(ContributionChart).GetField("PetBarColor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            var wasteColor = (Color?)typeof(ContributionChart).GetField("PetWasteBarColor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            bool HasColor(Color? color)
                => color.HasValue && panels.Any(p => p.GetThemeStylebox("panel") is StyleBoxFlat s && s.BgColor == color.Value);
            Check("ui-pet-bar-color", HasColor(petColor), $"petColor={petColor}");
            Check("ui-pet-waste-bar-color", HasColor(wasteColor), $"wasteColor={wasteColor}");
            chart.Free();
        }
        catch (Exception ex)
        {
            Check("ui-checks", false, ex.ToString());
        }
    }

    private static void RunUiCategoryRegressionChecks()
    {
        try
        {
            var data = new Dictionary<string, ContributionAccum>
            {
                ["STRIKE_NECROBINDER"] = new() { SourceId = "STRIKE_NECROBINDER", SourceType = "card", DirectDamage = 9 },
                ["BONE_SHARDS"] = new() { SourceId = "BONE_SHARDS", SourceType = "card", DirectDamage = 9, PetWasted = 5 },
            };
            var chart = ContributionChart.Create(data, "PET_PROBE_DAMAGE");
            var children = chart.GetChildren().OfType<Node>().ToList();

            string? HeaderText(Node n)
            {
                if (n is not PanelContainer pc) return null;
                return Descendants<Label>(pc).FirstOrDefault()?.Text;
            }

            int damageIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.damage"));
            int defenseIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.defense"));
            if (damageIdx < 0 || defenseIdx <= damageIdx)
            {
                Check("ui-damage-section-found", false, $"damage={damageIdx} defense={defenseIdx}");
                chart.Free();
                return;
            }

            var section = children.Skip(damageIdx + 1).Take(defenseIdx - damageIdx - 1).ToList();
            var labels = section.SelectMany(Descendants<Label>).Select(l => l.Text).ToList();
            string strikeName = NameLookup.Card("STRIKE_NECROBINDER");
            string wasteSuffix = L.Get("chart.pet_wasted_suffix");
            Check("ui-damage-strike-row", labels.Any(t => t.Contains(strikeName)),
                string.Join(" | ", labels));
            Check("ui-damage-strike-value", labels.Any(t => t.StartsWith("9")),
                string.Join(" | ", labels));
            Check("ui-damage-no-pet-waste", !labels.Any(t => t.Contains(wasteSuffix)),
                string.Join(" | ", labels));
            chart.Free();
        }
        catch (Exception ex)
        {
            Check("ui-category-regression", false, ex.ToString());
        }
    }

    private static void RunUiSubEntryCostChecks()
    {
        try
        {
            var data = new Dictionary<string, ContributionAccum>
            {
                ["PARENT"] = new() { SourceId = "PARENT", SourceType = "card", EffectiveBlock = 3 },
                ["BONE_SHARDS\u0001PARENT"] = new()
                {
                    SourceId = "BONE_SHARDS", SourceType = "card", EffectiveBlock = 2,
                    PetWasted = 5, OriginSourceId = "PARENT",
                },
            };
            var chart = ContributionChart.Create(data, "PET_PROBE_SUB_COST");
            var children = chart.GetChildren().OfType<Node>().ToList();

            string? HeaderText(Node n)
            {
                if (n is not PanelContainer pc) return null;
                return Descendants<Label>(pc).FirstOrDefault()?.Text;
            }

            int defenseIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.defense"));
            int drawIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.draw"));
            if (defenseIdx < 0 || drawIdx <= defenseIdx)
            {
                Check("ui-subcost-section-found", false, $"defense={defenseIdx} draw={drawIdx}");
                chart.Free();
                return;
            }

            var section = children.Skip(defenseIdx + 1).Take(drawIdx - defenseIdx - 1).ToList();
            var labels = section.SelectMany(Descendants<Label>).Select(l => l.Text).ToList();
            var panels = section.SelectMany(Descendants<Panel>).ToList();
            string wasteSuffix = L.Get("chart.pet_wasted_suffix");
            string boneName = NameLookup.Card("BONE_SHARDS");

            Check("ui-subcost-split-row", labels.Count(t => t.Contains(wasteSuffix)) == 1
                && labels.Any(t => t.Contains(boneName)),
                string.Join(" | ", labels));

            var wasteColor = (Color?)typeof(ContributionChart).GetField("PetWasteBarColor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            var maxBarWidth = (float?)typeof(ContributionChart).GetField("MaxBarWidth", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? 380f;
            var wasteBars = panels
                .Where(p => wasteColor.HasValue && p.GetThemeStylebox("panel") is StyleBoxFlat s && s.BgColor == wasteColor.Value)
                .ToList();
            float actualWidth = wasteBars.Count > 0 ? wasteBars[0].CustomMinimumSize.X : -1f;
            float expectedWidth = 5f / 8f * maxBarWidth;
            Check("ui-subcost-bar-scale", wasteBars.Count == 1 && Math.Abs(actualWidth - expectedWidth) <= 1f,
                $"bars={wasteBars.Count} width={actualWidth} expected={expectedWidth} maxBarWidth={maxBarWidth}");
            chart.Free();
        }
        catch (Exception ex)
        {
            Check("ui-subcost", false, ex.ToString());
        }
    }

    private static void RunUiStandaloneSelfDamageChecks()
    {
        try
        {
            var data = new Dictionary<string, ContributionAccum>
            {
                ["OFFERING"] = new() { SourceId = "OFFERING", SourceType = "card", SelfDamage = 5 },
            };
            var chart = ContributionChart.Create(data, "PET_PROBE_SELF_DAMAGE");
            var children = chart.GetChildren().OfType<Node>().ToList();

            string? HeaderText(Node n)
            {
                if (n is not PanelContainer pc) return null;
                return Descendants<Label>(pc).FirstOrDefault()?.Text;
            }

            int defenseIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.defense"));
            int drawIdx = children.FindIndex(c => HeaderText(c) == L.Get("chart.draw"));
            if (defenseIdx < 0 || drawIdx <= defenseIdx)
            {
                Check("ui-selfdamage-section-found", false, $"defense={defenseIdx} draw={drawIdx}");
                chart.Free();
                return;
            }

            var section = children.Skip(defenseIdx + 1).Take(drawIdx - defenseIdx - 1).ToList();
            var labels = section.SelectMany(Descendants<Label>).Select(l => l.Text).ToList();
            string suffix = L.Get("chart.self_damage_suffix");
            string offeringName = NameLookup.Card("OFFERING");
            var selfRows = labels.Where(t => t.Contains(suffix)).ToList();
            Check("ui-standalone-selfdamage-name",
                selfRows.Count == 1 && selfRows[0].Contains(offeringName),
                string.Join(" | ", labels));
            chart.Free();
        }
        catch (Exception ex)
        {
            Check("ui-standalone-selfdamage", false, ex.ToString());
        }
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T t) yield return t;
            if (child is Node n)
                foreach (var inner in Descendants<T>(n))
                    yield return inner;
        }
    }

    // ── Live game test ───────────────────────────────────────────────────
    private static void OnCombatBegan(CombatState state)
    {
        if (_liveStarted) return;
        _liveStarted = true;
        _blockAutoPlay = true; // freeze AutoSlay card plays / end-turn during the probe
        TaskHelper.RunSafely(RunLive(state));
    }

    private static async Task RunLive(CombatState state)
    {
        bool ok = false;
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var me = LocalContext.GetMe(state.Players);
            Check("live-player", me != null, me?.Character.Id.Entry ?? "null");
            if (me == null) return;
            // Pet accounting is character-independent. On v0.111.1 Necrobinder
            // may still be locked; the probe injects Osty through real commands.
            Check("live-character-pet-path", true,
                me.Character.Id.Entry + (me.Character.Id.Entry.Contains("NECROBINDER") ? " natural" : " injected"));

            if (!await WaitUntil(() => me.PlayerCombatState?.Phase == PlayerTurnPhase.Play, 15000))
            {
                Check("live-play-phase", false, $"phase={me.PlayerCombatState?.Phase}");
                return;
            }
            Check("live-play-phase", true, "play");

            // ── Osty absorption with real damage ────────────────────────
            int PetTotal() => CombatTracker.Instance.GetCurrentCombatData().Values.Sum(a => a.PetAbsorbed);
            var ostyCtx = new ThrowingPlayerChoiceContext();
            if (!me.IsOstyAlive)
                await OstyCmd.Summon(ostyCtx, me, 10m, null);
            bool ostyAbsorbReady = await WaitUntil(() => me.IsOstyAlive, 5000);
            Check("live-osty-absorb-ready", ostyAbsorbReady, $"ostyHp={me.Osty?.CurrentHp}");
            if (ostyAbsorbReady)
            {
                await CreatureCmd.LoseBlock(ostyCtx, me.Creature, me.Creature.Block, null);
                int petBefore = PetTotal();
                var absorber = state.Enemies.FirstOrDefault(e => e.IsAlive);
                Check("live-osty-absorption-dealer", absorber != null, absorber?.Monster?.Id.Entry ?? "null");
                if (absorber != null)
                {
                    await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), me.Creature, 4m,
                        ValueProp.Move, absorber, null, null);
                    int delta = PetTotal() - petBefore;
                    Check("live-osty-absorption", delta == 4, $"delta={delta} expected=4 hpBefore=10");
                    Check("live-osty-no-negative-block",
                        CombatTracker.Instance.GetCurrentCombatData().Values.All(a => a.EffectiveBlock >= 0),
                        string.Join(",", CombatTracker.Instance.GetCurrentCombatData().Values.Select(a => $"{a.SourceId}:{a.EffectiveBlock}")));
                }
            }

            // ── Wuppy redirect with overkill (v0.113+ only) ─────────────
            var wuppyType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("MegaCrit.Sts2.Core.Models.Relics.Wuppy", throwOnError: false))
                .FirstOrDefault(t => t != null);
            if (wuppyType == null)
            {
                Check("live-wuppy-skip", true, "Wuppy absent in this game version");
            }
            else
            {
                var wuppyCanonical = ModelDb.AllRelics.FirstOrDefault(r => wuppyType.IsInstanceOfType(r));
                if (wuppyCanonical == null)
                {
                    Check("live-wuppy-model", false, "canonical Wuppy missing");
                }
                else
                {
                    var wuppyRelic = me.Relics.FirstOrDefault(r => wuppyType.IsInstanceOfType(r));
                    if (wuppyRelic == null)
                    {
                        await RelicCmd.Obtain(wuppyCanonical.ToMutable(), me);
                        wuppyRelic = me.Relics.FirstOrDefault(r => wuppyType.IsInstanceOfType(r));
                    }
                    var wuppyProp = wuppyType.GetProperty("CombatWuppy", BindingFlags.Public | BindingFlags.Instance);
                    Creature? WuppyCreature() => wuppyProp?.GetValue(wuppyRelic) as Creature;
                    bool wuppyReady = await WaitUntil(() => WuppyCreature() is { IsAlive: true }, 5000);
                    Check("live-wuppy-summoned", wuppyReady, $"wuppy={WuppyCreature()?.CurrentHp}");
                    if (wuppyReady)
                    {
                        var wc = WuppyCreature()!;
                        await CreatureCmd.SetCurrentHp(wc, 3m);
                        int petBefore = Pet("WUPPY");
                        var dealer = state.Enemies.FirstOrDefault(e => e.IsAlive);
                        Check("live-wuppy-dealer", dealer != null, dealer?.Monster?.Id.Entry ?? "null");
                        if (dealer != null)
                        {
                            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), me.Creature, 10m,
                                ValueProp.Move, dealer, null, null);
                            int delta = Pet("WUPPY") - petBefore;
                            Check("live-wuppy-redirect-absorption", delta == 3, $"delta={delta} expected=3 (hp was 3, damage 10)");
                            Check("live-wuppy-overkill-excluded", delta != 10, $"delta={delta} should exclude 7 overkill");
                            Check("live-wuppy-hp-zero", !wc.IsAlive, $"hp={wc.CurrentHp}");
                        }
                    }
                }
            }

            // ── BoneShards kill ─────────────────────────────────────────
            var ctx = new ThrowingPlayerChoiceContext();
            if (!me.IsOstyAlive)
                await OstyCmd.Summon(ctx, me, 10m, null);
            else
                await OstyCmd.Summon(ctx, me, 10m, null);
            bool ostyAlive = await WaitUntil(() => me.IsOstyAlive, 5000);
            Check("live-boneshards-osty-ready", ostyAlive, $"ostyHp={me.Osty?.CurrentHp}");
            if (ostyAlive)
            {
                int stackBefore = ContributionMap.Instance.GetRemainingOstyHpTotal();
                // Raise actual HP above the tracked stack so PetWasted can only
                // come from creature.CurrentHp, not the LIFO stack.
                await CreatureCmd.SetMaxAndCurrentHp(me.Osty!, 20m);
                int actualHp = me.Osty!.CurrentHp;
                Check("live-boneshards-actual-hp-distinct", actualHp == 20 && stackBefore > 0 && actualHp != stackBefore,
                    $"actualHp={actualHp} stackBefore={stackBefore}");
                int wasteBefore = Waste("BONE_SHARDS");
                bool played = await PlayCard(state, me, "BoneShards");
                Check("live-boneshards-played", played, "CardCmd.AutoPlay");
                int wasteDelta = Waste("BONE_SHARDS") - wasteBefore;
                Check("live-boneshards-waste-actual-hp", wasteDelta == actualHp,
                    $"delta={wasteDelta} actualHp={actualHp} stackBefore={stackBefore}");
                Check("live-boneshards-killed-osty", !me.IsOstyAlive, $"alive={me.IsOstyAlive}");
                Check("live-boneshards-no-negative-block",
                    CombatTracker.Instance.GetCurrentCombatData().Values.All(a => a.EffectiveBlock >= 0),
                    string.Join(",", CombatTracker.Instance.GetCurrentCombatData().Values.Select(a => $"{a.SourceId}:{a.EffectiveBlock}")));
            }

            // ── Sacrifice kill ──────────────────────────────────────────
            await OstyCmd.Summon(ctx, me, 7m, null);
            bool ostyAlive2 = await WaitUntil(() => me.IsOstyAlive, 5000);
            Check("live-sacrifice-osty-ready", ostyAlive2, $"ostyHp={me.Osty?.CurrentHp}");
            if (ostyAlive2)
            {
                int stackBefore = ContributionMap.Instance.GetRemainingOstyHpTotal();
                await CreatureCmd.SetMaxAndCurrentHp(me.Osty!, 12m);
                int actualHp = me.Osty!.CurrentHp;
                Check("live-sacrifice-actual-hp-distinct", actualHp == 12 && stackBefore > 0 && actualHp != stackBefore,
                    $"actualHp={actualHp} stackBefore={stackBefore}");
                int wasteBefore = Waste("SACRIFICE");
                bool played = await PlayCard(state, me, "Sacrifice");
                Check("live-sacrifice-played", played, "CardCmd.AutoPlay");
                int wasteDelta = Waste("SACRIFICE") - wasteBefore;
                Check("live-sacrifice-waste-actual-hp", wasteDelta == actualHp,
                    $"delta={wasteDelta} actualHp={actualHp} stackBefore={stackBefore}");
                Check("live-sacrifice-no-negative-block",
                    CombatTracker.Instance.GetCurrentCombatData().Values.All(a => a.EffectiveBlock >= 0),
                    string.Join(",", CombatTracker.Instance.GetCurrentCombatData().Values.Select(a => $"{a.SourceId}:{a.EffectiveBlock}")));
            }

            ok = _fail == 0;
            GD.Print($"[PetGameProbe] LIVE_RESULT {(_fail == 0 ? "PASS" : "FAIL")} pass={_pass} fail={_fail}");
        }
        catch (Exception ex)
        {
            _fail++;
            GD.Print($"[PetGameProbe] live fatal: {ex}");
        }
        finally
        {
            GD.Print($"[PetGameProbe] RESULT {(_fail == 0 ? "PASS" : "FAIL")} pass={_pass} fail={_fail}");
            try
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                tree?.Quit(_fail == 0 ? 0 : 1);
            }
            catch { }
        }
    }

    private static async Task<bool> PlayCard(CombatState state, Player me, string typeName)
    {
        var canonical = ModelDb.AllCards.FirstOrDefault(c => c.GetType().Name == typeName);
        if (canonical == null)
        {
            GD.Print($"[PetGameProbe] card missing: {typeName}");
            return false;
        }
        var card = state.CreateCard(canonical, me);
        await CardPileCmd.AddGeneratedCardsToCombat(new List<CardModel> { card }, PileType.Hand, me);
        _probeAutoPlay = true;
        try
        {
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, AutoPlayType.Default, skipCardPileVisuals: true);
        }
        finally
        {
            _probeAutoPlay = false;
        }
        return true;
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(50);
        return condition();
    }

    // ── Gate: keep AutoSlay from racing the probe ────────────────────────
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
    private static class AutoPlayGate
    {
        private static bool Prefix(ref Task __result)
        {
            if (_blockAutoPlay && !_probeAutoPlay)
            {
                __result = Task.CompletedTask;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.EndTurn))]
    private static class EndTurnGate
    {
        private static bool Prefix() => !_blockAutoPlay;
    }
}
