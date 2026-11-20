using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace StatsTheSpire.Rq014Probe;

/// <summary>Test-only probe for RQ-014 display calculations and labels.</summary>
[ModInitializer("Initialize")]
public static class Rq014Probe
{
    private static int _frames;
    private static bool _staticProbeDone;
    private static string _lastEncounter = "";
    private static readonly HashSet<MegaCrit.Sts2.Core.Entities.Creatures.Creature> _probedCreatures = new();
    private static bool _bossCardProbed;
    private static bool _visibilityProbed;

    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        tree.ProcessFrame += () =>
        {
            _frames++;
            // ModelDb and localization are ready only after the main menu
            // finishes loading (~37 s on this install). Run after that.
            if (!_staticProbeDone && _frames >= 2400 && RunContextInfo.CurrentState != null)
            {
                _staticProbeDone = true;
                RunStaticProbe(tree);
            }
            if (_staticProbeDone)
                RunCombatProbe(tree);
        };
    }

    private static void RunStaticProbe(SceneTree tree)
    {
        Safe.Run(() => ProbeCard(tree, -0.05f, "card-neg"));
        Safe.Run(() => ProbeCard(tree, 0f, "card-zero"));
        Safe.Run(() => ProbePotion(tree, -0.10f, false, "potion-neg"));
        Safe.Run(() => ProbePotion(tree, 0.40f, true, "potion-forced"));
        Safe.Run(() => ProbeShop(tree));
        Safe.Run(ProbeAncient);
        Safe.Run(ProbeAncientId);
        Safe.Run(ProbeWriggler);
        Safe.Run(ProbeNames);
        Safe.Run(ProbeBossActs);
    }

    private static void ProbeCard(SceneTree tree, float offset, string tag)
    {
        var node = CardDropOddsIndicator.Create();
        tree.Root.AddChild(node);
        node.UpdateOffset(offset);
        Invoke(node, "ShowHoverPanel");
        LogLabels(tag, node);
        Invoke(node, "HideHoverPanel");
        node.QueueFree();
    }

    private static void ProbePotion(SceneTree tree, float odds, bool forced, string tag)
    {
        var node = PotionOddsIndicator.Create();
        tree.Root.AddChild(node);
        node.UpdateOdds(odds, forced);
        Invoke(node, "ShowHoverPanel");
        LogLabels(tag, node);
        Invoke(node, "HideHoverPanel");
        node.QueueFree();
    }

    private static void ProbeShop(SceneTree tree)
    {
        var panel = ShopPricePanel.Create(null, 0);
        tree.Root.AddChild(panel);
        LogLabels("shop", panel);
        panel.QueueFree();

        var format = typeof(ShopPricePanel).GetMethod(
            "FormatRange", BindingFlags.Static | BindingFlags.NonPublic);
        var removal = typeof(ShopPricePanel).GetMethod(
            "ComputeRemovalCost", BindingFlags.Static | BindingFlags.NonPublic);
        Safe.Info($"[RQ014Probe] shop-format relic-discount={format?.Invoke(null, new object[] { 175, 0.15f, 0.5m })}");
        Safe.Info($"[RQ014Probe] shop-format shop-relic={format?.Invoke(null, new object[] { 200, 0.15f, 1m })}");
        Safe.Info($"[RQ014Probe] removal-no-discount={removal?.Invoke(null, new object?[] { null, 0, 1m })}");
        Safe.Info($"[RQ014Probe] removal-half={removal?.Invoke(null, new object?[] { null, 0, 0.5m })}");
    }

    private static void ProbeAncient()
    {
        ProbeElder("TEZCATARA");
        ProbeElder("OROBAS");
        ProbeElder("NEOW");
        ProbeElder("DARV");
    }

    private static void ProbeElder(string id)
    {
        var info = AncientPoolMap.Get(id);
        if (info == null)
        {
            Safe.Warn($"[RQ014Probe] ancient {id} missing");
            return;
        }
        for (int i = 0; i < info.Pools.Count; i++)
        {
            var pool = info.Pools[i];
            Safe.Info($"[RQ014Probe] ancient {id} pool{i + 1}={string.Join(",", pool.RelicIds)}");
        }
        var darv = AncientPoolMap.Get("DARV");
        if (darv != null)
        {
            foreach (var pool in darv.Pools)
            {
                if (pool.ActGate == null) continue;
                foreach (var kv in pool.ActGate)
                    Safe.Info($"[RQ014Probe] darv-gate {kv.Key}={string.Join(",", kv.Value)}");
            }
        }
    }

    private static void ProbeAncientId()
    {
        var method = typeof(CommunityStats.Collection.RunHistoryAnalyzer).GetMethod(
            "ExtractRelicId", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) return;
        var choice = new MegaCrit.Sts2.Core.Runs.History.AncientChoiceHistoryEntry(
            new MegaCrit.Sts2.Core.Localization.LocString("relics", "SEA_GLASS.SILENT.title"),
            true);
        Safe.Info($"[RQ014Probe] sea-glass-id={method.Invoke(null, new object[] { choice })}");
    }

    private static void ProbeWriggler()
    {
        var method = typeof(IntentStateMachinePanel).GetMethod(
            "ResolveWrigglerFollowUp", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) return;
        foreach (var slot in new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4" })
            Safe.Info($"[RQ014Probe] wriggler {slot}->{method.Invoke(null, new object?[] { slot })}");
    }

    private static void ProbeNames()
    {
        foreach (var id in new[] { "PAEL", "TEZCATARA", "OROBAS", "NONUPEIPE" })
            Safe.Info($"[RQ014Probe] name {id}={NameLookup.Ancient(id)}");
    }

    private static void ProbeBossActs()
    {
        var method = typeof(CareerStatsSection).GetMethod(
            "BossActFor", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) return;
        foreach (var act in ModelDb.Acts)
        {
            foreach (var enc in act.AllEncounters)
            {
                if (enc.RoomType != RoomType.Boss) continue;
                var result = method.Invoke(null, new object[] { enc.Id.Entry });
                Safe.Info($"[RQ014Probe] boss-act {enc.Id.Entry} idx={act.Index} => {result}");
            }
        }
    }

    private static void RunCombatProbe(SceneTree tree)
    {
        Safe.Run(() =>
        {
            var state = CombatManager.Instance?.DebugOnlyGetState();
            if (state == null) return;
            string encounter;
            try { encounter = state.Encounter?.Id.Entry ?? ""; }
            catch { return; }
            if (string.IsNullOrEmpty(encounter)) return;
            if (encounter != _lastEncounter)
            {
                _lastEncounter = encounter;
                _probedCreatures.Clear();
                _visibilityProbed = false;
            }

            if (!_visibilityProbed)
            {
                _visibilityProbed = true;
                Callable.From(() => ProbeVisibility(tree, state.RunState)).CallDeferred();
            }

            if (!_bossCardProbed && RunContextInfo.IsBossRoom(state.RunState))
            {
                _bossCardProbed = true;
                Callable.From(() => ProbeBossCard(tree)).CallDeferred();
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy?.Monster == null) continue;
                if (!_probedCreatures.Add(enemy)) continue;
                Callable.From(() => ProbeCreature(tree, encounter, enemy)).CallDeferred();
            }
        });
    }

    private static void ProbeVisibility(SceneTree tree, MegaCrit.Sts2.Core.Runs.IRunState state)
    {
        Safe.Run(() =>
        {
            string attachInfo = "n/a";
            try
            {
                var t = typeof(CommunityStats.Patches.CombatUiOverlayPatch);
                var ensure = t.GetMethod("EnsureAttachedToTopBar", BindingFlags.Static | BindingFlags.NonPublic);
                bool attached = ensure != null && (bool)(ensure.Invoke(null, null) ?? false);
                var potionField = t.GetField("_potion", BindingFlags.Static | BindingFlags.NonPublic);
                var cardField = t.GetField("_cardDrop", BindingFlags.Static | BindingFlags.NonPublic);
                var posField = t.GetField("_positioned", BindingFlags.Static | BindingFlags.NonPublic);
                attachInfo = $"attached={attached} potionField={potionField?.GetValue(null) != null} cardField={cardField?.GetValue(null) != null} positioned={posField?.GetValue(null)}";
            }
            catch (Exception ex) { attachInfo = "error=" + ex.Message; }

            bool blitz = RunContextInfo.IsBlitz(state);
            bool potion = false, card = false;
            int potionCount = 0, cardCount = 0;
            string potionParent = "", cardParent = "";
            foreach (var node in tree.Root.FindChildren("*StatsTheSpirePotionOdds*", "Control", true, false))
            {
                if (node is not PotionOddsIndicator p) continue;
                potionCount++;
                potion = p.Visible;
                potionParent = p.GetParent()?.Name ?? "";
            }
            foreach (var node in tree.Root.FindChildren("*StatsTheSpireCardDropOdds*", "Control", true, false))
            {
                if (node is not CardDropOddsIndicator c) continue;
                cardCount++;
                card = c.Visible;
                cardParent = c.GetParent()?.Name ?? "";
            }
            bool topBar = MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi?.TopBar != null;
            bool mapButton = MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi?.TopBar?.Map != null;
            Safe.Info(
                $"[RQ014Probe] indicators blitz={blitz} potionVisible={potion} cardVisible={card} " +
                $"potionCount={potionCount} cardCount={cardCount} parents={potionParent}|{cardParent} " +
                $"topBar={topBar} mapButton={mapButton} toggles={CommunityStats.Config.ModConfig.Toggles.PotionOdds}/{CommunityStats.Config.ModConfig.Toggles.CardDropOdds} {attachInfo}");
        });
    }

    private static void ProbeBossCard(SceneTree tree)
    {
        Safe.Run(() =>
        {
            foreach (var node in tree.Root.FindChildren("*StatsTheSpireCardDropOdds*", "Control", true, false))
            {
                if (node is not CardDropOddsIndicator card) continue;
                Invoke(card, "ShowHoverPanel");
                LogLabels("card-boss", card);
                Invoke(card, "HideHoverPanel");
                return;
            }
        });
    }

    private static void ProbeCreature(SceneTree tree, string encounter, MegaCrit.Sts2.Core.Entities.Creatures.Creature enemy)
    {
        Safe.Run(() =>
        {
            if (enemy.Monster == null) return;
            string id = enemy.Monster.Id.Entry;
            var panel = IntentStateMachinePanel.Create(enemy);
            if (panel == null) return;
            tree.Root.AddChild(panel);
            LogLabels($"intent-{encounter}-{id}-{enemy.SlotName}", panel);
            LogIcons($"intent-{encounter}-{id}-{enemy.SlotName}", panel);
            panel.QueueFree();
        });
    }

    private static void LogLabels(string tag, Node root)
    {
        var labels = new List<string>();
        foreach (var child in root.FindChildren("*", "Label", true, false))
        {
            if (child is Label lbl && !string.IsNullOrEmpty(lbl.Text))
                labels.Add(lbl.Text);
        }
        Safe.Info($"[RQ014Probe] {tag} labels={string.Join(" | ", labels)}");
    }

    private static void LogIcons(string tag, Node root)
    {
        var paths = new List<string>();
        foreach (var child in root.FindChildren("*", "TextureRect", true, false))
        {
            if (child is not TextureRect rect || rect.Texture == null) continue;
            string path = rect.Texture.ResourcePath;
            if (!string.IsNullOrEmpty(path))
                paths.Add(System.IO.Path.GetFileName(path));
        }
        if (paths.Count > 0)
            Safe.Info($"[RQ014Probe] {tag} icons={string.Join(" | ", paths)}");
    }

    private static void Invoke(object target, string method)
    {
        var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        mi?.Invoke(target, null);
    }
}
