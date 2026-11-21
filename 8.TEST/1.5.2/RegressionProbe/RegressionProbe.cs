using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommunityStats.Collection;
using CommunityStats.Config;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace StatsTheSpire.RegressionProbe;

/// <summary>
/// Test-only probe for 1.5.2 persistence and cache fixes. It runs on the
/// scene tree, cleans up its own files, and prints machine-readable lines.
/// </summary>
[ModInitializer("Initialize")]
public static class RegressionProbe
{
    private const string Seed = "codex152regression";
    private const long StartTime = 1795215201L;
    private static int _frames;
    private static bool _ran;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        tree.ProcessFrame += () =>
        {
            _frames++;
            if (_ran || _frames < 300) return;
            _ran = true;
            Safe.Run(() => Run(tree));
        };
    }

    private static void Run(SceneTree tree)
    {
        bool all = true;
        all &= ProbeDuplicateMerge();
        all &= ProbeCorruptQuarantine();
        all &= ProbeAtomicWrite();
        all &= ProbeCareerSource();
        all &= ProbeAbandonedCause();
        all &= ProbeShopPathBuys();
        all &= ProbeOstyCompositeKey();
        all &= ProbeLegacyCorruptUntouched();
        GD.Print($"[RegressionProbe] RESULT {(all ? "PASS" : "FAIL")}");
        tree.Quit();
    }

    private static bool ProbeDuplicateMerge()
    {
        var identity = new RunIdentity(StartTime, Seed);
        var runDir = RunStorage.DirectoryFor(identity);
        try
        {
            if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true);
            ContributionPersistence.SetActiveRun(identity);
            ContributionPersistence.SetActiveFloor(2);

            var native = new ContributionAccum
            {
                SourceId = "TEST_CARD",
                SourceType = "card",
                CardsDrawn = 40,
            };
            var generated = new ContributionAccum
            {
                SourceId = "TEST_CARD",
                SourceType = "card",
                OriginSourceId = "TEST_GENERATOR",
                CardsDrawn = 10,
            };
            var data = new Dictionary<string, ContributionAccum>
            {
                ["TEST_CARD"] = native,
                ["TEST_CARD\u0001TEST_GENERATOR"] = generated,
            };
            ContributionPersistence.SaveCombat(2, data);

            var merged = ContributionPersistence.AssembleFromCombats(identity);
            int entries = merged?.Count ?? 0;
            int drawn = merged?.Values.Sum(v => v.CardsDrawn) ?? 0;
            bool pass = entries == 2 && drawn == 50;
            GD.Print($"[RegressionProbe] duplicate-merge entries={entries} drawn={drawn} expect=2/50 {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] duplicate-merge FAIL {ex}");
            return false;
        }
        finally
        {
            try { if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true); } catch { }
            ContributionPersistence.ClearRunContext();
        }
    }

    private static bool ProbeCorruptQuarantine()
    {
        var identity = new RunIdentity(StartTime + 1, Seed + "corrupt");
        var runDir = RunStorage.DirectoryFor(identity);
        try
        {
            if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true);
            Directory.CreateDirectory(runDir);
            var summary = RunStorage.SummaryPath(identity);
            File.WriteAllText(summary, "{ broken");
            ContributionPersistence.SetActiveRun(identity);

            _ = ContributionPersistence.LoadRunSummary(identity);
            bool moved = Directory.EnumerateFiles(runDir, "summary.json.corrupt-*").Any();
            bool originalGone = !File.Exists(summary);
            bool pass = moved && originalGone;
            GD.Print($"[RegressionProbe] corrupt-quarantine moved={moved} originalGone={originalGone} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] corrupt-quarantine FAIL {ex}");
            return false;
        }
        finally
        {
            try { if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true); } catch { }
            ContributionPersistence.ClearRunContext();
        }
    }

    private static bool ProbeAtomicWrite()
    {
        var path = Path.Combine(ModConfig.DataDir, "probe-atomic-152.txt");
        try
        {
            Directory.CreateDirectory(ModConfig.DataDir);
            AtomicFile.WriteAllText(path, "atomic-ok");
            AtomicFile.WriteAllText(path, "atomic-ok-2");
            bool content = File.ReadAllText(path) == "atomic-ok-2";
            bool noTemp = !Directory.EnumerateFiles(ModConfig.DataDir, "probe-atomic-152.txt.tmp-*").Any();
            bool pass = content && noTemp;
            GD.Print($"[RegressionProbe] atomic replace={content} noTemp={noTemp} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] atomic FAIL {ex}");
            return false;
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    private static bool ProbeLegacyCorruptUntouched()
    {
        const string seed = "codex152legacycorrupt";
        var path = RunStorage.LegacySummaryPath(seed);
        try
        {
            Directory.CreateDirectory(ModConfig.ContributionsDir);
            if (File.Exists(path)) File.Delete(path);
            File.WriteAllText(path, "{ broken");
            _ = ContributionPersistence.LoadRunSummary(new RunIdentity(0, seed));
            bool untouched = File.Exists(path);
            bool noCorrupt = !Directory.EnumerateFiles(ModConfig.ContributionsDir, "codex152legacycorrupt_summary.json.corrupt-*").Any();
            bool pass = untouched && noCorrupt;
            GD.Print($"[RegressionProbe] legacy-corrupt untouched={untouched} noCorrupt={noCorrupt} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] legacy-corrupt FAIL {ex}");
            return false;
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    private static bool ProbeOstyCompositeKey()
    {
        try
        {
            CombatTracker.Instance.ForceResetAllContext();
            CombatTracker.Instance.OnOstyDamageDealt(5, "TEST_ATTACK");
            var data = CombatTracker.Instance.GetCurrentCombatData();
            bool plain = data.ContainsKey("TEST_ATTACK");
            bool composite = data.ContainsKey("TEST_ATTACK\u0001OSTY");
            bool pass = !plain && composite;
            GD.Print($"[RegressionProbe] osty-key plain={plain} composite={composite} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] osty-key FAIL {ex}");
            return false;
        }
        finally
        {
            CombatTracker.Instance.ForceResetAllContext();
        }
    }

    private static bool ProbeAbandonedCause()
    {
        try
        {
            var run = new RunHistory
            {
                Win = false,
                WasAbandoned = true,
                KilledByEncounter = new ModelId("ENCOUNTERS", "PROBE_BOSS"),
                MapPointHistory = new List<List<MegaCrit.Sts2.Core.Runs.History.MapPointHistoryEntry>>
                {
                    new() { new() },
                },
            };
            var method = typeof(RunHistoryAnalyzer).GetMethod(
                "ComputeDeathCauses", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                GD.Print("[RegressionProbe] abandoned-cause FAIL method missing");
                return false;
            }
            var result = method.Invoke(null, new object[] { new List<RunHistory> { run } })
                as IReadOnlyDictionary<int, IReadOnlyList<DeathEntry>>;
            var first = result?.GetValueOrDefault(1)?.FirstOrDefault();
            bool pass = first?.EncounterId == "ABANDONED" && first.Source == DeathSource.Abandoned;
            GD.Print($"[RegressionProbe] abandoned-cause cause={first?.EncounterId ?? "null"} source={first?.Source.ToString() ?? "null"} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] abandoned-cause FAIL {ex}");
            return false;
        }
    }

    private static bool ProbeShopPathBuys()
    {
        var identity = new RunIdentity(StartTime + 2, Seed + "path");
        var runDir = RunStorage.DirectoryFor(identity);
        try
        {
            if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true);
            ContributionPersistence.SetActiveRun(identity);
            ShopPurchasePersistence.Save(new List<LocalShopPurchase>
            {
                new() { ItemId = "CARD_A", ItemType = "card", Cost = 1, Floor = 2 },
                new() { ItemId = "RELIC_A", ItemType = "relic", Cost = 1, Floor = 3 },
                new() { ItemId = "CARD_B", ItemType = "card", Cost = 1, Floor = 4 },
                new() { ItemId = "CARD_C", ItemType = "card", Cost = 1, Floor = 5 },
            });
            var run = new RunHistory
            {
                Seed = identity.Seed,
                StartTime = identity.StartTime,
                MapPointHistory = new List<List<MegaCrit.Sts2.Core.Runs.History.MapPointHistoryEntry>>
                {
                    new() { new(), new(), new() },
                    new() { new(), new() },
                },
            };
            var method = typeof(RunHistoryAnalyzer).GetMethod(
                "BuildPerActShopCardBuys", BindingFlags.Static | BindingFlags.NonPublic);
            var buys = method?.Invoke(null, new object[] { run }) as Dictionary<int, int>;
            bool pass = buys != null && buys.GetValueOrDefault(1) == 1 && buys.GetValueOrDefault(2) == 2;
            GD.Print($"[RegressionProbe] shop-path act1={buys?.GetValueOrDefault(1)} act2={buys?.GetValueOrDefault(2)} expect=1/2 {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] shop-path FAIL {ex}");
            return false;
        }
        finally
        {
            try { if (Directory.Exists(runDir)) Directory.Delete(runDir, recursive: true); } catch { }
            ContributionPersistence.ClearRunContext();
        }
    }

    private static bool ProbeCareerSource()
    {
        const string filter = "PROBE152";
        try
        {
            var data = new CareerStatsData
            {
                CharacterFilter = filter,
                TotalRuns = 1,
                DeathCausesByAct = new Dictionary<int, IReadOnlyList<DeathEntry>>
                {
                    [1] = new List<DeathEntry>
                    {
                        new() { EncounterId = "EVENT_X", Count = 1, Share = 1f, Source = DeathSource.Event },
                    },
                },
            };
            CareerStatsCache.Save(data);
            var loaded = CareerStatsCache.Load(filter);
            var source = loaded?.DeathCausesByAct.GetValueOrDefault(1)?.FirstOrDefault()?.Source;
            bool pass = source == DeathSource.Event;
            GD.Print($"[RegressionProbe] career-source source={source?.ToString() ?? "null"} {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
        catch (Exception ex)
        {
            GD.Print($"[RegressionProbe] career-source FAIL {ex}");
            return false;
        }
        finally
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(ModConfig.CacheDir, "career_PROBE152_*"))
                    File.Delete(file);
            }
            catch { }
        }
    }
}
