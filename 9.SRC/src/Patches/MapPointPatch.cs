using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommunityStats.Config;
using CommunityStats.UI;
using CommunityStats.Util;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
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
    private const string HoverExitHookMeta = "sts_hover_info_mouse_exit";

    // Kept so a card removal made while the map hover panel is still open
    // can rebuild the removal price instead of leaving the stale value.
    private static readonly ConditionalWeakTable<NMapPoint, InfoModPanel> _shopPanels = new();

    // Every panel created by this patch, including unknown-room panels.
    // NMapScreen.Close hides the map screen but does not unfocus its map
    // points, so panels parented to NMapPoint survive a close/reopen unless
    // this registry is cleared explicitly.
    private static readonly ConditionalWeakTable<NMapPoint, InfoModPanel> _panels = new();

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
        // Only one map point can be hovered at a time. This also removes
        // panels whose OnUnfocus never fired because the cursor jumped
        // directly to another map point.
        HideOtherHoverPanels(mapPoint);

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
        _panels.Remove(mapPoint);
        _panels.AddOrUpdate(mapPoint, panel);
        if (point.PointType == MapPointType.Shop)
            _shopPanels.AddOrUpdate(mapPoint, panel);

        // OnUnfocus is not reliable for mouse input: the game can keep
        // Godot focus on the map point, and a fast move can skip the
        // unfocus callback. MouseExited plus the map-screen mouse-motion
        // check remove the panel independently of focus.
        EnsureMouseExitHook(mapPoint);
    }

    private static void EnsureMouseExitHook(NMapPoint mapPoint)
    {
        if (mapPoint.HasMeta(HoverExitHookMeta)) return;
        mapPoint.SetMeta(HoverExitHookMeta, true);
        mapPoint.Connect(Control.SignalName.MouseExited,
            Callable.From(() => Safe.Run(() => HideHoverPanel(mapPoint))));
    }

    /// <summary>
    /// Mouse exit is independent of Godot focus. NMapPoint can keep focus
    /// after the pointer leaves, so OnUnfocus alone is not enough. Check
    /// every open panel on mouse motion and drop any whose point no longer
    /// contains the pointer.
    /// </summary>
    private static void HidePanelsOutsidePointer()
    {
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true) return;

        var stale = new List<NMapPoint>();
        foreach (var kv in _panels)
        {
            var mapPoint = kv.Key;
            if (!Godot.GodotObject.IsInstanceValid(mapPoint)
                || !mapPoint.IsInsideTree()
                || !mapPoint.IsVisibleInTree())
            {
                stale.Add(mapPoint);
                continue;
            }
            if (!mapPoint.GetGlobalRect().HasPoint(mapPoint.GetGlobalMousePosition()))
                stale.Add(mapPoint);
        }

        foreach (var mapPoint in stale)
            HideHoverPanel(mapPoint);
    }

    private static void HideOtherHoverPanels(NMapPoint keep)
    {
        var stale = new List<NMapPoint>();
        foreach (var kv in _panels)
            if (kv.Key != keep)
                stale.Add(kv.Key);

        foreach (var mapPoint in stale)
            if (Godot.GodotObject.IsInstanceValid(mapPoint))
                HideHoverPanel(mapPoint);
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
                _panels.Remove(map);
                _panels.AddOrUpdate(map, fresh);
            }
        });
    }

    private static void HideHoverPanel(NMapPoint mapPoint)
    {
        try
        {
            if (_panels.TryGetValue(mapPoint, out var registered))
            {
                _panels.Remove(mapPoint);
                if (Godot.GodotObject.IsInstanceValid(registered))
                {
                    registered.Visible = false;
                    registered.QueueFree();
                }
            }

            foreach (var child in mapPoint.GetChildren())
            {
                if (child is not InfoModPanel panel) continue;
                if (!Godot.GodotObject.IsInstanceValid(panel)) continue;
                panel.Visible = false;
                panel.QueueFree();
            }

            _shopPanels.Remove(mapPoint);
            if (mapPoint.HasMeta(HoverPanelMeta))
                mapPoint.RemoveMeta(HoverPanelMeta);
        }
        catch { }
    }

    /// <summary>
    /// Deletes every hover panel owned by this patch. NMapScreen only hides
    /// itself on close; it does not call OnUnfocus for map points, so panels
    /// parented to NMapPoint would otherwise survive until the next map
    /// rebuild. Called on close, cleanup, reopen, and map replacement.
    /// </summary>
    public static void HideAllHoverPanels()
    {
        Safe.Run(() =>
        {
            var entries = new List<NMapPoint>();
            foreach (var kv in _panels)
                entries.Add(kv.Key);

            foreach (var mapPoint in entries)
            {
                if (!Godot.GodotObject.IsInstanceValid(mapPoint)) continue;
                HideHoverPanel(mapPoint);
            }

            _panels.Clear();
            _shopPanels.Clear();
        });
    }

    [HarmonyPatch(typeof(NMapScreen), "_Input")]
    [HarmonyPostfix]
    public static void AfterMapScreenInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseMotion) return;
        Safe.Run(HidePanelsOutsidePointer);
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
    [HarmonyPostfix]
    public static void AfterMapScreenClose()
    {
        HideAllHoverPanels();
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.CleanUp))]
    [HarmonyPostfix]
    public static void AfterMapScreenCleanUp()
    {
        HideAllHoverPanels();
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
    [HarmonyPrefix]
    public static void BeforeMapScreenOpen()
    {
        HideAllHoverPanels();
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
    [HarmonyPrefix]
    public static void BeforeMapScreenSetMap()
    {
        HideAllHoverPanels();
    }
}
