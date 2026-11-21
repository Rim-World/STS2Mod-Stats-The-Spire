using System;
using CommunityStats.Collection;
using CommunityStats.Util;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CommunityStats.Patches;

/// <summary>
/// v0.113.1 愚者（Fool）新内容的来源上下文。
///
/// 所有新类型只存在于 v0.113.1，因此这里不写任何 <c>typeof(SongBow)</c> 之类的
/// 编译期引用：类型与方法名全部用字符串表 + <c>AccessTools.TypeByName</c> +
/// <c>AccessTools.DeclaredMethod</c> 在运行时解析。v0.111.1 上每个条目都会
/// 因为类型缺失而逐项跳过，记录 <see cref="Safe.Warn"/>，不影响其他补丁。
///
/// 上下文规则与其他 hook 一致：prefix 设 <c>SetActivePowerSource</c>（能力牌/能力）
/// 或 <c>SetActiveRelic</c>（遗物），postfix 清除。所有回调只对本地玩家一侧生效。
/// 这里刻意不调用 <c>SetPendingDrawSource</c>：抽牌靠 AsyncLocal 的 active context
/// 自然流过 await，避免把来源留给下一次非手抽牌。
///
/// 只观察和记账，不改变游戏行为。
/// </summary>
public static class FoolContentPatch
{
    private const string PowerNamespace = "MegaCrit.Sts2.Core.Models.Powers.";
    private const string RelicNamespace = "MegaCrit.Sts2.Core.Models.Relics.";

    /// <summary>
    /// (类型全名, 方法名, 是否为 PowerModel)。方法名均在 v0.113.1 反编译源码中逐个确认。
    /// 不含本轮明确排除的狂喜、磨砺、锯齿箭矢、箭袋，也不含敌人/Dominion/事件模型。
    /// </summary>
    private static readonly (string Type, string Method, bool IsPower)[] Targets =
    {
        // ── 16 张能力牌（效果实际在各自的 PowerModel 上） ──
        // 万事无忧：生成「悲伤」时获得格挡
        (PowerNamespace + "AllIsWellPower", "AfterCardGeneratedForCombat", true),
        // 箭术：每回合开始应用「瞄准」；把 ARCHERY_POWER 的来源传给 AimPower
        (PowerNamespace + "ArcheryPower", "AfterSideTurnStart", true),
        // 堆肥：敌人死亡时获得能量
        (PowerNamespace + "CompostPower", "AfterDeath", true),
        // 火力掩护：射出箭时给队友格挡（多人卡）
        (PowerNamespace + "CoveringFirePower", "AfterArrowShot", true),
        // 倦怠：手抽前生成「悲伤」（同时驱动万事无忧/肥料）
        (PowerNamespace + "EnnuiPower", "BeforeHandDraw", true),
        // 爆炸箭矢：箭被拔出时对全体敌人造成伤害
        (PowerNamespace + "ExplosiveArrowsPower", "AfterArrowImpaled", true),
        // 潜伏根系：每回合开始应用「伸展」；把来源传给 StretchPower
        (PowerNamespace + "HiddenRootsPower", "AfterSideTurnStart", true),
        // 失落鲁特琴：首次打出旋律牌时从抽牌堆自动打出
        (PowerNamespace + "LostLutePower", "AfterCardPlayed", true),
        // 摇篮曲：同一回合集齐三种旋律时获得格挡
        (PowerNamespace + "LullabyPower", "AfterCardPlayed", true),
        // 曲中意：首次打出旋律牌时抽牌（active context 越过 await 生效）
        (PowerNamespace + "MotifPower", "AfterCardPlayed", true),
        // 神话形态：手抽前生成塔罗牌
        (PowerNamespace + "MythicFormPower", "BeforeHandDraw", true),
        // 傻瓜：自动打出阶段自动打出低费牌
        (PowerNamespace + "SimpletonPower", "AfterAutoPostPlayPhaseEntered", true),
        // 歌之道：首次打出「唱」旋律时获得格挡
        (PowerNamespace + "SongPathPower", "AfterCardPlayed", true),
        // 调音：回合结束应用「免费牌」，把来源传给 FreeCardPower
        (PowerNamespace + "TunePower", "AfterSideTurnEnd", true),
        // 暴力和弦：首次打出「哇」旋律时对全体敌人造成伤害
        (PowerNamespace + "ViolentChordPower", "AfterCardPlayed", true),
        // 呀！：箭被拔出时获得能量
        (PowerNamespace + "YahPower", "AfterArrowDislodged", true),

        // ── 2 个非卡牌能力 ──
        // 异鸟猛击：玩家回合开始对全体敌人造成伤害
        (PowerNamespace + "GodByrdPower", "AfterPlayerTurnStart", true),
        // 瓦皮的忠诚：把对玩家的攻击重定向到瓦皮；上下文标记重定向窗口，
        // 实际承伤归属由 CombatHistory 的宠物分支消费。
        (PowerNamespace + "WuppysLoyaltyPower", "ModifyPreBlockDamageTarget", true),

        // ── 15 个遗物（说书人两个 hook 分别验证） ──
        // 浴袍：Boss 战开始前治疗。核心 RelicHookContextPatcher 已按运行时
        // 类型名注册同一 hook；此处不重复注册，避免双前缀/双后缀。
        // 浴巾：第一回合玩家回合结束后治疗
        (RelicNamespace + "BathTowel", "AfterPlayerTurnStartLate", false),
        // 奇怪的灯：第一回合手抽前生成「喜悦」「悲伤」
        (RelicNamespace + "BizarreLamp", "BeforeHandDraw", false),
        // 指挥棒：一回合集齐三种旋律后应用减伤能力
        (RelicNamespace + "ConductorsBaton", "AfterCardPlayed", false),
        // 健身垫：第一回合获得「伸展」
        (RelicNamespace + "ExerciseMat", "AfterSideTurnStart", false),
        // 肥料：生成「悲伤」时获得力量
        (RelicNamespace + "Fertilizer", "AfterCardGeneratedForCombat", false),
        // 强悍翅膀：单次 30+ 伤害后获得力量
        (RelicNamespace + "MightyWings", "AfterDamageGiven", false),
        // 黑曜石钳子：玩家回合结束按手牌数造成伤害
        (RelicNamespace + "ObsidianVice", "BeforeSideTurnEnd", false),
        // 陶笛：第一回合手抽前生成一张旋律牌
        (RelicNamespace + "Ocarina", "BeforeHandDraw", false),
        // 歌曲之弓：进入战斗房间时获得「瞄准」
        (RelicNamespace + "SongBow", "AfterRoomEntered", false),
        // 说书人 hook 1：进入战斗房间时获得「瞄准」
        (RelicNamespace + "Storyteller", "AfterRoomEntered", false),
        // 说书人 hook 2：玩家回合结束时获得「瞄准」
        (RelicNamespace + "Storyteller", "BeforeSideTurnEnd", false),
        // 苦痛项圈：精英战第一回合给敌人上虚弱
        (RelicNamespace + "TormentousCollar", "BeforeSideTurnStart", false),
        // 合股线：箭被拔出时获得格挡
        (RelicNamespace + "Twine", "AfterArrowDislodged", false),
        // 削木刀：洗牌后获得「瞄准」
        (RelicNamespace + "WhittlingKnife", "AfterShuffle", false),
        // 瓦皮 hook 1：战斗开始时召唤瓦皮并应用「瓦皮的忠诚」，
        // 让该能力的应用来源记录为 WUPPY 遗物
        (RelicNamespace + "Wuppy", "BeforeCombatStart", false),
        // 瓦皮 hook 2：在战斗房间内获得遗物时立即召唤
        (RelicNamespace + "Wuppy", "AfterObtained", false),
    };

    public static void PatchAll(Harmony harmony)
    {
        if (harmony == null)
        {
            Safe.Warn("[FoolContent] Harmony instance is null; skipped");
            return;
        }

        var powerPrefix = new HarmonyMethod(
            AccessTools.Method(typeof(FoolContentPatch), nameof(SetPowerContext)));
        var powerPostfix = new HarmonyMethod(
            AccessTools.Method(typeof(FoolContentPatch), nameof(ClearPowerContext)));
        var relicPrefix = new HarmonyMethod(
            AccessTools.Method(typeof(FoolContentPatch), nameof(SetRelicContext)));
        var relicPostfix = new HarmonyMethod(
            AccessTools.Method(typeof(FoolContentPatch), nameof(ClearRelicContext)));

        int installed = 0;
        int skipped = 0;
        foreach (var target in Targets)
        {
            if (TryPatch(harmony, target, powerPrefix, powerPostfix, relicPrefix, relicPostfix))
                installed++;
            else
                skipped++;
        }

        Safe.Info($"[FoolContent] {installed} hooks installed, {skipped} skipped (targets={Targets.Length})");
        TryPatchEnnuiHandDraw(harmony);
    }

    /// <summary>
    /// EnnuiPower.ModifyHandDraw is a separate hook from the context table:
    /// it adds cards to the opening hand and has no damage/block callback.
    /// Patch it manually because the type does not exist on v0.111.1.
    /// Without this, the extra cards silently disappear from CardsDrawn.
    /// </summary>
    private static void TryPatchEnnuiHandDraw(Harmony harmony)
    {
        try
        {
            var type = AccessTools.TypeByName(PowerNamespace + "EnnuiPower");
            if (type == null)
            {
                Safe.Info("[FoolContent] EnnuiPower.ModifyHandDraw skipped (pre-0.113 game)");
                return;
            }

            var method = AccessTools.DeclaredMethod(type, "ModifyHandDraw");
            if (method == null)
            {
                Safe.Warn("[FoolContent] EnnuiPower.ModifyHandDraw method not found; skipped");
                return;
            }

            var postfix = new HarmonyMethod(
                AccessTools.Method(typeof(FoolContentPatch), nameof(AfterEnnuiHandDraw)));
            harmony.Patch(method, postfix: postfix);
            Safe.Info("[FoolContent] EnnuiPower.ModifyHandDraw installed");
        }
        catch (Exception ex)
        {
            Safe.Warn($"[FoolContent] EnnuiPower.ModifyHandDraw failed: {ex.Message}");
        }
    }

    public static void AfterEnnuiHandDraw(PowerModel __instance, Player player,
        decimal count, decimal __result)
    {
        Safe.Run(() =>
        {
            if (__instance.Owner?.Player != player) return;
            int extra = (int)(__result - count);
            if (extra <= 0) return;

            var source = ContributionMap.Instance.GetPowerSource(__instance.Id.Entry);
            string sourceId = source?.SourceId ?? __instance.Id.Entry;
            string sourceType = source?.SourceType ?? "power";
            ContributionMap.Instance.RecordHandDrawBonus(sourceId, sourceType, extra);
        });
    }

    private static bool TryPatch(Harmony harmony,
        (string Type, string Method, bool IsPower) target,
        HarmonyMethod powerPrefix, HarmonyMethod powerPostfix,
        HarmonyMethod relicPrefix, HarmonyMethod relicPostfix)
    {
        try
        {
            var type = AccessTools.TypeByName(target.Type);
            if (type == null)
            {
                Safe.Warn($"[FoolContent] Type not found: {target.Type}; skipped (pre-0.113 game?)");
                return false;
            }

            var method = AccessTools.DeclaredMethod(type, target.Method);
            if (method == null)
            {
                Safe.Warn($"[FoolContent] Method not found: {target.Type}.{target.Method}; skipped");
                return false;
            }

            harmony.Patch(method,
                prefix: target.IsPower ? powerPrefix : relicPrefix,
                postfix: target.IsPower ? powerPostfix : relicPostfix);
            return true;
        }
        catch (Exception ex)
        {
            Safe.Warn($"[FoolContent] Failed {target.Type}.{target.Method}: {ex.Message}");
            return false;
        }
    }

    // ── Power context ───────────────────────────────────────

    public static void SetPowerContext(PowerModel __instance,
        out CombatTracker.AttributionContext __state)
    {
        __state = CombatTracker.Instance.CaptureAttributionContext();
        Safe.Run(() =>
        {
            if (!IsLocalPowerOwner(__instance)) return;

            var powerId = __instance.Id.Entry;
            if (string.IsNullOrEmpty(powerId)) return;

            // 保存/恢复外层上下文；SetActivePowerSource 在来源未知时会清空
            // 继承的 source id，避免记到上一次的 power。
            CombatTracker.Instance.SetActivePowerSource(powerId);
        });
    }

    public static void ClearPowerContext(CombatTracker.AttributionContext __state)
    {
        Safe.Run(() => CombatTracker.Instance.RestoreAttributionContext(__state));
    }

    // ── Relic context ───────────────────────────────────────

    public static void SetRelicContext(RelicModel __instance,
        out CombatTracker.AttributionContext __state)
    {
        __state = CombatTracker.Instance.CaptureAttributionContext();
        Safe.Run(() =>
        {
            if (!CombatHistoryPatch.IsLocalPlayer(__instance.Owner)) return;

            var relicId = __instance.Id.Entry;
            if (string.IsNullOrEmpty(relicId)) return;

            CombatTracker.Instance.SetActiveRelic(relicId);
        });
    }

    public static void ClearRelicContext(CombatTracker.AttributionContext __state)
    {
        Safe.Run(() => CombatTracker.Instance.RestoreAttributionContext(__state));
    }

    // ── Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Power 的 owner 可能是本地玩家，也可能是本地玩家的宠物（瓦皮）。
    /// 敌方/中立模型不在本补丁的目标表中；为防误记，这类 owner 一律跳过。
    /// </summary>
    private static bool IsLocalPowerOwner(PowerModel power)
    {
        Creature? owner = power.Owner;
        if (owner == null) return false;
        if (owner.IsPlayer) return CombatHistoryPatch.IsLocalPlayer(owner.Player);

        var petOwner = owner.PetOwner;
        return petOwner != null && CombatHistoryPatch.IsLocalPlayer(petOwner);
    }
}
