using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommunityStats.Config;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace CommunityStats.Patches;

/// <summary>
/// Patches NMapPoint to show hover panels (unknown room odds, shop prices).
/// Encounter danger overlay removed (local edition).
/// </summary>
[HarmonyPatch]
public static class MapPointPatch
{
    // ── Hover panels (unknown room odds + shop prices) ─

    private const string HoverPanelMeta = "sts_hover_info_panel";

    // Kept so a card removal made while the map hover panel is still open
    // can rebuild the removal price instead of leaving the stale value.
    private static readonly ConditionalWeakTable<NMapPoint, InfoModPanel> _shopPanels = new();

    [HarmonyPatch(typeof(NMapPoint), "OnFocus")]
    [HarmonyPostfix]
    public static void AfterOnFocus(NMapPoint __instance)
    {
        Safe.Run(() => ShowHoverPanel(__instance));
    }

    [HarmonyPatch(typeof(NMapPoint), "OnUnfocus")]
    [HarmonyPostfix]
    public static void AfterOnUnfocus(NMapPoint __instance)
    {
        Safe.Run(() => HideHoverPanel(__instance));
    }

    private static void ShowHoverPanel(NMapPoint mapPoint)
    {
        if (mapPoint.HasMeta(HoverPanelMeta)) return;

        var point = mapPoint.Point;
        if (point == null) return;

        InfoModPanel? panel = null;

        if (point.PointType == MapPointType.Unknown
            && mapPoint.State != MapPointState.Traveled
            && ModConfig.Toggles.UnknownRoomOdds)
        {
            panel = BuildUnknownPanel();
        }
        else if (point.PointType == MapPointType.Shop
            && mapPoint.State != MapPointState.Traveled
            && ModConfig.Toggles.ShopPrices)
        {
            var runState = RunManager.Instance?.DebugOnlyGetState();
            if (RunContextInfo.IsBlitz(runState)) return;

            var player = RunContextInfo.GetLocalPlayer(runState);
            panel = ShopPricePanel.Create(player, GetShopRemovalsUsed(runState));
        }

        if (panel == null) return;

        mapPoint.AddChild(panel);
        panel.ZIndex = 200;
        panel.GlobalPosition = mapPoint.GlobalPosition + new Vector2(40f, 0f);
        mapPoint.SetMeta(HoverPanelMeta, true);
        if (point.PointType == MapPointType.Shop)
            _shopPanels.AddOrUpdate(mapPoint, panel);
    }

    private static InfoModPanel? BuildUnknownPanel()
    {
        var runState = RunManager.Instance?.DebugOnlyGetState();
        var odds = runState?.Odds?.UnknownMapPoint;
        if (odds == null) return null;
        return UnknownRoomPanel.Create(odds, runState);
    }

    private static int GetShopRemovalsUsed(IRunState? runState)
    {
        try
        {
            var me = RunContextInfo.GetLocalPlayer(runState);
            if (me == null) return 0;
            return me.ExtraFields.CardShopRemovalsUsed;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Rebuild every shop hover panel after card-removal usage changes the
    /// next removal price. The map panel can stay open while the shop UI is
    /// active, so a rebuild keeps S4 honest without a polling timer.
    /// </summary>
    public static void RefreshOpenShopPanels()
    {
        Safe.Run(() =>
        {
            var state = RunManager.Instance?.DebugOnlyGetState();
            if (RunContextInfo.IsBlitz(state)) return;

            var player = RunContextInfo.GetLocalPlayer(state);
            int removals = GetShopRemovalsUsed(state);
            var entries = new List<(NMapPoint Map, InfoModPanel Panel)>();
            foreach (var kv in _shopPanels)
                entries.Add((kv.Key, kv.Value));

            foreach (var (map, old) in entries)
            {
                if (!Godot.GodotObject.IsInstanceValid(map)
                    || !Godot.GodotObject.IsInstanceValid(old))
                    continue;
                if (old.GetParent() != map) continue;

                var fresh = ShopPricePanel.Create(player, removals);
                var position = old.GlobalPosition;
                map.RemoveChild(old);
                old.QueueFree();
                map.AddChild(fresh);
                fresh.ZIndex = 200;
                fresh.GlobalPosition = position;
                _shopPanels.Remove(map);
                _shopPanels.AddOrUpdate(map, fresh);
            }
        });
    }

    private static void HideHoverPanel(NMapPoint mapPoint)
    {
        if (!mapPoint.HasMeta(HoverPanelMeta)) return;

        foreach (var child in mapPoint.GetChildren())
        {
            if (child is InfoModPanel)
            {
                child.QueueFree();
                break;
            }
        }
        _shopPanels.Remove(mapPoint);
        mapPoint.RemoveMeta(HoverPanelMeta);
    }

}
