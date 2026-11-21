# 1.6.0 阶段 0 基线测试计划

- 建立时间：2026-11-21 19:32
- 基线版本：Mod 1.5.2
- 源码提交：`79e3575`
- 游戏基线：当前安装为 Slay the Spire 2 v0.111.1
- 目标版本：1.6.0
- 关联计划：`2.PLANS/018-G-1.6.0-战斗归因修复与愚者覆盖.md`

## 1. 基线构建

| 项目 | 值 |
|---|---|
| v0.111.1 构建日志 | `8.TEST/1.6.0/baseline-v111.log` |
| v0.113.1 构建日志 | `8.TEST/1.6.0/baseline-v113.log` |
| v0.111.1 DLL | `TMP/1.6.0/build-v111/sts2_statsthespire_local.dll` |
| v0.113.1 DLL | `TMP/1.6.0/build-v113/sts2_statsthespire_local.dll` |
| 源码构建大小 | 516,608 字节 |
| 源码构建 SHA-256 | `8e5c77d8bb4b760c84ddc95f79ef722c0c4fe48d02aebc145cc0c7041c89e2db` |
| 构建结果 | 两版均 0 错误；7 个既有可空性警告 |
| v0.113.1 引用包装 | `TMP/1.6.0/refs-v113/`，sts2.dll 来自 `STS2-Reference/4.STS2-113.1Reference/assemblies` |
| v0.113.1 sts2.dll SHA-256 | `62ecbe91093e1fa862de4329aac865209dcb297804caf0f0520d92d8c0b4a9c3` |
| v0.111.1 当前游戏 sts2.dll SHA-256 | `3fb1fa1bf0249087b706a5d0f062a60579e2f5b08df4ee477ddd6f2607f654d5` |

注：源码重建的 DLL 大小 516,608 字节，与已发布的 1.5.2 成品 519,168 字节不同。基线以当前源码重建结果为准；差异不写入代码变更结论，发布阶段再核对版本与构建配置。

## 2. 固定测试种子

| 组 | 种子 | 用途 |
|---|---:|---|
| P0 | 62001 | 真实伤害守卫、待定抽牌、格挡池、治疗、奥斯提、战败 flush、辉星/能量 |
| P1 | 62002 | 减伤边际、力量变化、治疗/复活、费用、抽牌、嵌套上下文 |
| 内容 | 62003 | 16 张能力牌、15 个遗物、非卡牌能力 |
| v0.111.1 兼容 | 62004 | 新内容补丁跳过、加载与完整跑局 |
| v0.113.1 完整局 | 62005 | 愚者内容或标准角色完整 AutoSlayer |

种子只用于 1.6.0 阶段测试。测试前记录贡献目录清单；测试后把测试目录移入 `TMP/1.6.0/evidence-<seed>/`，不混入正式统计。

## 3. 探针分组

| 组 | 需要的探针 |
|---|---|
| P0 | `RealDamageGuardProbe`、`PendingDrawProbe`、`BlockPoolProbe`、`CombatStartHealProbe`、`OstyDeathProbe`、`DefeatFlushProbe`、`ResourceActualProbe`、`ForgeProbe`、`CardLifecycleProbe` |
| P1 | `MitigationSplitProbe`、`StrengthDeltaProbe`、`HealingLifecycleProbe`、`CostTagProbe`、`DrawActualProbe`、`NestedContextProbe`、`RelicPowerGapProbe` |
| 内容 | `FoolPowerProbe`、`FoolRelicProbe`、`FoolBoundaryProbe`、`FoolRuntimeSkipProbe` |
| 回归 | `FullRunProbe`、`PatchInventoryProbe`、`PerformanceProbe` |

探针工程放在 `8.TEST/1.6.0/<Probe>/`。每个探针至少输出一行带 `PASS` 或 `FAIL` 的固定格式结果；失败时保留完整游戏日志和标准输出。

## 4. 执行顺序

1. 先执行 P0 探针；全部 PASS 后进入 P1。
2. P1 已确认项全部 PASS 后进入内容探针。
3. 内容探针通过后执行双版本加载和完整跑局。
4. 最后执行构建、版本、隐私和清理检查。

## 6. 1.6.0 集成施工结果（2026-11-22 00:25）

| 项目 | 结果 | 日志 |
|---|---|---|
| v0.111.1 Release 构建 | 0 错误、7 个既有警告 | `build-v111-integrated.log` |
| v0.113.1 Release 构建 | 0 错误、7 个既有警告 | `build-v113-integrated.log` |
| `AttributionProbe` 构建 | 0 错误、0 警告 | `build-attribution-probe.log` |
| v0.111.1 定向探针（最终代码） | 首场治疗、锻造消费、升级格挡、待定抽牌、上下文、费用、抽牌、休息处、多来源格挡、嵌套卡牌 10/10 PASS | `attribution-probe-v111-15-stdout.log` |
| v0.111.1 主 Mod 加载 | 初始化成功；`RealDamageGuard patched v0.111 7-param`；`OstyDeathPatch patched collection Kill(Creature, bool)`；34 条愚者目标全部跳过 | `attribution-probe-v111-15-stdout.log` |
| v0.111.1 AutoSlayer 抽检 | 种子 62001 跑到第 25 层、14 场战斗后测试超时；战斗数据正常 | `v111-autoslay-62001-stdout.log` |
| v0.113.1 标准完整 AutoSlayer | victory，Silent，3/3 幕，49 层；29 场战斗、53 条来源；路径未经过商店 | `v113.1-autoslay-62005-stdout.log` |
| v0.113.1 愚者实机 | victory，Ironclad，3/3 幕，48 层；28 场战斗、97 条来源、7 条购买；15 遗物、18 能力 | `v113.1-foolprobe-62003c-stdout.log` |
| 愚者上下文 | 18/18 能力、15/15 遗物 PASS；34 条 hook 全部安装 | `v113.1-foolprobe-context-62999-stdout.log` |
| 16 张能力牌/15 个遗物实机 | 上下文 18/18、15/15 PASS；实机可计量来源 9 个能力、6 个遗物 | `v113.1-foolprobe-62003c-stdout.log` |
| 来源保存-恢复 | 遗物/能力/卡牌上下文已实现；伤害/格挡预览已隔离 | `attribution-probe-v111-15-stdout.log` |
| 最终双版本构建 | v0.113.1 与 v0.111.1 均 0 错误、7 个既有警告；DLL 563,200 字节 | `build-v113-160-final.log`、`build-v111-160-final.log` |
| 第 45 层事件保存兜底 | `CombatRoom.Exit` 兜底写出 `combat_45_1.json`；29 个战斗文件、无重复；victory，exit 0 | `exitfix6-62003-stdout.log` |

失败记录：

- `attribution-probe-v111-2-stdout.log`：探针把 `FORGE:` 复合键当普通键，锻造项 0/4；修正探针后通过。
- `attribution-probe-v111-stdout.log`：带 Ritsu/主 Mod 的首次无头运行在资源预加载阶段崩溃，探针未执行。
- `attribution-probe-isolated-stdout.log`：隔离探针缺少主 DLL 依赖，`FileNotFoundException` 重复报错；该失败用于确认探针必须声明主 Mod 依赖。

## 7. 归因与手抽修复复测（2026-11-22 07:20）

| 项目 | 结果 | 日志 |
|---|---|---|
| v0.113.1 Release 构建 | 0 错误、7 个既有警告 | `build-v113-runtimecheck.log` |
| v0.111.1 Release 构建 | 0 错误、7 个既有警告 | `build-v111-runtimecheck.log` |
| Fool 注入局种子 62005 | `UNTRACKED` 全为 0；`ENNUI_POWER` 手抽 2/1/6/2/2/4/1 与 oracle 一致 | `runtimefix-62005-stdout.log`、`runtimefix-compare-fool.txt` |
| 标准局种子 62005 | 49 层完整；`UNTRACKED` 全为 0；进程收尾超时，数据完整 | `runtimefix-standard-62005-stdout.log`、`runtimefix-compare-standard.txt` |
| 比对脚本 | 无 `UNTRACKED`；Fool 局可选手抽断言 PASS | `compare_runtimefix.py` |

失败记录：

- 标准局在战斗结束后的主菜单停留，900 秒超时被测试框架终止；战斗与事件文件完整，统计对照仍有效。
- v0.111.1 未做运行复测，只做兼容构建。

## 8. 记录与清理

- 每次运行保存一个新的日志文件，不覆盖旧结果。
- 测试日志放在 `8.TEST/1.6.0/`，文件登记到 `8.TEST/TEST.md`。
- 测试游戏部署只放在临时目录或游戏 `mods/` 的专用测试目录；结束后移出。
- 测试结束后关闭游戏进程，检查 `mods/` 是否残留测试目录。
- 不把测试日志、构建路径或用户目录写入发布包。
