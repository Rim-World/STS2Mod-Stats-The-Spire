# TEST 测试文件登记

本文件登记开发过程中的测试工具、日志与测试数据。版本目录为 `8.TEST/<version>/`。

## 1.4.0

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `build1.log` | 2026-11-19 06:59 | 否 | 4 | 首次构建失败记录 | 是 |
| `build2.log` | 2026-11-19 07:00 | 否 | 5 | API 修改后构建记录 | 是 |
| `build3.log` | 2026-11-19 07:01 | 否 | 4 | 编译通过记录 | 是 |
| `build4.log` | 2026-11-19 07:06 | 否 | 4 | 最终功能构建记录 | 是 |
| `build-final.log` | 2026-11-19 07:26 | 否 | 1 | 收尾增量构建，0 错误 0 警告 | 是 |
| `game-load2.log` | 2026-11-19 07:07 | 否 | 201 | 无头加载与补丁注册日志 | 是 |
| `game-stdout.log` | 2026-11-19 07:02 | 否 | 212 | 首次加载输出 | 是 |
| `autoslay-try1.log` | 2026-11-19 07:08 | 否 | 217 | AutoSlayer 未启用测试门的记录 | 是 |
| `autoslay-run1.log` | 2026-11-19 07:10 | 否 | 228 | 测试门 DLL 名不匹配的记录 | 是 |
| `autoslay-run2.log` | 2026-11-19 07:11 | 否 | 297 | AutoSlayer 完整 Act 1 战斗日志 | 是 |
| `career-probe.log` | 2026-11-19 07:13 | 否 | 385 | 生涯统计 195 局加载 | 是 |
| `career-probe2.log` | 2026-11-19 07:14 | 否 | 430 | 模式筛选与 UI 探针 | 是 |
| `api-targets.tsv` | 2026-11-19 07:00 | 否 | 2 | 关键 API 查询清单 | 是 |
| `api-targets.log` | 2026-11-19 07:00 | 否 | 3 | 关键 API 存在性结果 | 是 |
| `test-report.md` | 2026-11-19 07:15 | 否 | 4 | 构建与实机测试报告 | 是 |
| `AutoSlayGate/AutoSlayGate.csproj` | 2026-11-19 07:09 | 否 | 1 | 测试门工程 | 是 |
| `AutoSlayGate/AutoSlayGate.cs` | 2026-11-19 07:09 | 否 | 1 | `NGame.IsReleaseGame` 测试补丁 | 是 |
| `AutoSlayGate/manifest.json` | 2026-11-19 07:09 | 否 | 1 | 测试门清单 | 是 |
| `CareerProbe/CareerProbe.csproj` | 2026-11-19 07:13 | 否 | 2 | 生涯探针工程 | 是 |
| `CareerProbe/CareerProbe.cs` | 2026-11-19 07:13 | 否 | 3 | 生涯数据与 UI 探针 | 是 |
| `CareerProbe/manifest.json` | 2026-11-19 07:13 | 否 | 1 | 生涯探针清单 | 是 |
| `autoslay-gate-build.log` | 2026-11-19 07:09 | 否 | 1 | 测试门首次构建 | 是 |
| `autoslay-gate-build2.log` | 2026-11-19 07:11 | 否 | 1 | 测试门改名后构建 | 是 |
| `career-probe-build.log` | 2026-11-19 07:13 | 否 | 1 | 生涯探针构建 | 是 |
| `ui-probe-build.log` | 2026-11-19 07:18 | 否 | 1 | UI 探针首次构建 | 是 |
| `ui-probe-build2.log` | 2026-11-19 07:19 | 否 | 1 | UI 探针开关修正后构建 | 是 |
| `ui-probe.log` | 2026-11-19 07:18 | 否 | 349 | 首次 UI 探针（贡献开关关闭） | 是 |
| `ui-probe2.log` | 2026-11-19 07:19 | 否 | 340 | 贡献/设置面板可见性验证 | 是 |
| `UiProbe/UiProbe.csproj` | 2026-11-19 07:18 | 否 | 2 | UI 探针工程 | 是 |
| `UiProbe/UiProbe.cs` | 2026-11-19 07:18 | 否 | 2 | UI 构造探针 | 是 |
| `UiProbe/manifest.json` | 2026-11-19 07:18 | 否 | 1 | UI 探针清单 | 是 |

## 1.4.1

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `build-fool1.log` | 2026-11-19 07:41 | 否 | 5 | 首次构建失败：`ShowsInfiniteHp` 与编译常量缺失 | 是 |
| `build-fool2.log` | 2026-11-19 07:42 | 否 | 4 | 移除 `ShowsInfiniteHp` 分支后构建 | 是 |
| `build-fool3.log` | 2026-11-19 07:43 | 否 | 4 | 双签名卡牌生成补丁构建 | 是 |
| `build-fool4.log` | 2026-11-19 07:46 | 否 | 4 | 三段参数签名修复后构建 | 是 |
| `build-1.4.1.log` | 2026-11-19 07:51 | 否 | 4 | 1.4.1 版本号构建；0 错误 | 是 |
| `career-probe-build-fool.log` | 2026-11-19 07:43 | 否 | 1 | 愚者探针首次构建 | 是 |
| `career-probe-build-fool2.log` | 2026-11-19 07:46 | 否 | 1 | 愚者探针复构建 | 是 |
| `ui-probe-build-fool.log` | 2026-11-19 07:49 | 否 | 1 | 愚者下拉 UI 探针构建 | 是 |
| `fool-probe-console.log` | 2026-11-19 07:45 | 否 | 496 | 首次愚者探针；钩子参数数量警告 | 是 |
| `fool-probe2-console.log` | 2026-11-19 07:50 | 否 | 489 | 最终愚者探针；15 局、13 胜、名称通过 | 是 |
| `fool-ui-probe.log` | 2026-11-19 07:51 | 否 | 220 | 角色/模式下拉与愚者 UI 区块 | 是 |
| `final-load.log` | 2026-11-19 07:55 | 否 | 204 | 1.4.1 无头加载与补丁签名 | 是 |
| `fork-build-1.4.1.log` | 2026-11-19 07:53 | 否 | 5 | fork 分支源码独立构建 | 是 |
| `CareerProbe/CareerProbe.cs` | 2026-11-19 07:49 | 否 | 4 | 愚者数据/名称探针 | 是 |
| `CareerProbe/CareerProbe.csproj` | 2026-11-19 07:49 | 否 | 2 | 愚者探针工程 | 是 |
| `CareerProbe/manifest.json` | 2026-11-19 07:49 | 否 | 1 | 愚者探针清单 | 是 |
| `UiProbe/UiProbe.cs` | 2026-11-19 07:49 | 否 | 3 | 愚者下拉探针 | 是 |
| `UiProbe/UiProbe.csproj` | 2026-11-19 07:49 | 否 | 2 | 下拉探针工程 | 是 |
| `UiProbe/manifest.json` | 2026-11-19 07:49 | 否 | 1 | 下拉探针清单 | 是 |

## 1.4.2

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `v111-build.log` | 2026-11-19 08:04 | 否 | 5 | v0.111 首次编译失败：`MerchantSynchronizer` 不存在 | 是 |
| `v111-build2.log` | 2026-11-19 08:05 | 否 | 4 | 反射修复后 v0.111 编译；0 错误 | 是 |
| `v111-build-1.4.2.log` | 2026-11-19 08:05 | 否 | 4 | 1.4.2 源码 v0.111 编译；0 错误 | 是 |
| `build-1.4.2.log` | 2026-11-19 08:02 | 否 | 4 | 1.4.2 源码 v0.113.1 编译；0 错误 | 是 |
| `build-shopfix-1.4.1.log` | 2026-11-19 08:05 | 否 | 4 | 商店修复后 v0.113.1 编译 | 是 |
| `fork-build.log` | 2026-11-19 08:02 | 否 | 5 | fork 分支 1.4.2 编译 | 是 |
| `autoslay-1.4.2.log` | 2026-11-19 08:03 | 否 | 431 | v0.113.1 AutoSlayer 商店路径 | 是 |
| `v111.1-build-main.log` | 2026-11-19 19:38 | 否 | 4 | v0.111.1 主 Mod 构建；0 错误、7 警告 | 是 |
| `v111.1-build-autoslaygate.log` | 2026-11-19 19:39 | 否 | 1 | v0.111.1 AutoSlayer 测试门构建 | 是 |
| `v111.1-build-careerprobe.log` | 2026-11-19 19:39 | 否 | 1 | v0.111.1 生涯探针构建 | 是 |
| `v111.1-build-uiprobe.log` | 2026-11-19 19:39 | 否 | 1 | v0.111.1 界面探针构建 | 是 |
| `v111.1-autoslay.log` | 2026-11-19 19:40 | 否 | 106 | v0.111.1 1.4.2 基线 AutoSlayer 日志 | 是 |
| `v111.1-autoslay-stdout.log` | 2026-11-19 19:40 | 否 | 432 | v0.111.1 1.4.2 基线游戏输出；含伤害守卫补丁失败 | 是 |

## 1.4.3

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `v111.1-build-1.4.3.log` | 2026-11-19 19:54 | 否 | 4 | 1.4.3 源码 v0.111.1 构建；0 错误、7 警告 | 是 |
| `v111.1-build-main-fix.log` | 2026-11-19 19:52 | 否 | 4 | 伤害守卫修复后的中间构建 | 是 |
| `v111.1-patchcheck.log` | 2026-11-19 19:53 | 是 | 0 | 加载检查的 AutoSlayer 空日志 | 否 |
| `v111.1-patchcheck-stdout.log` | 2026-11-19 19:53 | 否 | 178 | 加载检查游戏输出；7 参数补丁注册成功 | 是 |
| `v111.1-autoslay-1.4.3.log` | 2026-11-19 19:54 | 否 | 155 | 1.4.3 完整 AutoSlayer 流程 | 是 |
| `v111.1-autoslay-1.4.3-stdout.log` | 2026-11-19 19:54 | 否 | 1236 | 1.4.3 完整游戏输出；含探针与错误归属 | 是 |

## 测试计划

| 用例 | 方法 | 状态 |
|---|---|---|
| 构建 | `dotnet build`，记录退出码与错误数 | 已验证 |
| 加载 | 启动 v0.113.1 与 v0.111.1，检查 `[StatsTheSpire]` 初始化日志 | 已验证 |
| 战斗 | AutoSlayer 进入多场战斗，检查贡献数据 | 已验证 |
| 生涯 | 加载 195/196 局历史，检查平均数据与缓存 | 已验证 |
| 生涯模式 | Standard/Multiplayer 筛选与 UI 创建 | 已验证 |
| 愚者历史 | `character=FOOL`，对照 15 局历史 | 已验证 |
| 愚者名称 | `NameLookup` 检查卡牌/遗物 | 已验证 |
| 愚者界面 | 角色下拉与愚者生涯区块 | 已验证 |
| 单文件兼容 | 同一 DLL 在 v0.113.1 加载 | 已验证 |
| 双版本编译 | v0.111.1 与 v0.113.1 的 `sts2.dll` 各编译一次 | 已验证 |
| 商店路径 | AutoSlayer 进入商店并读取库存 | 已验证 |
| v0.111.1 | 实机加载、战斗、商店与完整一局 | 已验证 |
| 伤害守卫 | v0.111.1 注册 7 参数 `CreatureCmd.Damage` | 已验证 |
| 界面 | 贡献面板显示/隐藏、设置面板显示 | 已验证 |
| 设置 | 检查药水/卡牌开关与面板尺寸持久化 | 未验证 |
| 清理 | 关闭游戏，移除测试部署 | 已验证 |

## 1.4.4

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `v111.1-build-1.4.4.log` | 2026-11-19 20:41 | 否 | 4 | 1.4.4 源码首次 v0.111.1 构建 | 是 |
| `v111.1-build-1.4.4-final.log` | 2026-11-19 20:43 | 否 | 4 | 1.4.4 最终 v0.111.1 构建 | 是 |
| `v113.1-build-1.4.4.log` | 2026-11-19 20:43 | 否 | 4 | 1.4.4 v0.113.1 程序集编译 | 是 |
| `v111.1-build-shopfileprobe.log` | 2026-11-19 20:41 | 否 | 1 | 商店文件探针构建 | 是 |
| `v111.1-shopfileprobe-1.4.3-stdout.log` | 2026-11-19 20:42 | 否 | 203 | 1.4.3 对照：复现商店购买文件警告 | 是 |
| `v111.1-shopfileprobe-1.4.4-stdout.log` | 2026-11-19 20:43 | 否 | 210 | 1.4.4 验证：无警告且 `sources=1` | 是 |
| `ShopFileProbe/ShopFileProbe.csproj` | 2026-11-19 20:38 | 否 | 2 | 聚焦探针工程 | 是 |
| `ShopFileProbe/ShopFileProbe.cs` | 2026-11-19 20:38 | 否 | 2 | 创建战斗与商店文件，调用 `AssembleFromCombats` | 是 |
| `ShopFileProbe/manifest.json` | 2026-11-19 20:38 | 否 | 1 | 聚焦探针清单 | 是 |
| `v113.1-autoslay-1.4.4.log` | 2026-11-19 20:58 | 否 | 143 | v0.113.1 首轮 41111 完整流程；贡献混入旧版本数据 | 是 |
| `v113.1-autoslay-1.4.4-stdout.log` | 2026-11-19 20:58 | 否 | 1195 | 首轮游戏输出、警告与探针结果 | 是 |
| `v113.1-autoslay-51111.log` | 2026-11-19 21:07 | 否 | 133 | v0.113.1 干净种子 51111 完整流程 | 是 |
| `v113.1-autoslay-51111-stdout.log` | 2026-11-19 21:07 | 否 | 1152 | 干净流程游戏输出、警告与探针结果 | 是 |
| `v113.1-careerprobequit-post51111-stdout.log` | 2026-11-19 21:08 | 否 | 849 | 通关后生涯计数与自动退出 | 是 |
| `v113.1-build-1.4.4.log` | 2026-11-19 20:43 | 否 | 4 | 1.4.4 v0.113.1 构建 | 是 |
| `v113.1-build-autoslaygate.log` | 2026-11-19 20:50 | 否 | 1 | AutoSlayGate 构建 | 是 |
| `v113.1-build-careerprobe.log` | 2026-11-19 20:50 | 否 | 1 | CareerProbe 构建 | 是 |
| `v113.1-build-uiprobe.log` | 2026-11-19 20:50 | 否 | 1 | UiProbe 构建 | 是 |
| `v113.1-build-careerprobequit.log` | 2026-11-19 21:01 | 否 | 1 | CareerProbeQuit 构建 | 是 |
| `CareerProbeQuit/CareerProbeQuit.csproj` | 2026-11-19 21:00 | 否 | 2 | 通关后生涯探针工程 | 是 |
| `CareerProbeQuit/CareerProbeQuit.cs` | 2026-11-19 21:00 | 否 | 2 | 读取生涯后退出 | 是 |
| `CareerProbeQuit/manifest.json` | 2026-11-19 21:00 | 否 | 1 | 通关后生涯探针清单 | 是 |

## 1.5.0

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `SettingsProbe/SettingsProbe.csproj` | 2026-11-20 00:34 | 否 | 2 | RitsuLib 设置页与热键探针工程 | 是 |
| `SettingsProbe/SettingsProbe.cs` | 2026-11-20 00:34 | 否 | 4 | 页面注册、绑定持久化、热键重绑定与非法绑定拒绝 | 是 |
| `SettingsProbe/manifest.json` | 2026-11-20 00:34 | 否 | 1 | 设置探针清单 | 是 |
| `HistoryProbe/HistoryProbe.csproj` | 2026-11-20 01:06 | 否 | 2 | 同 seed 历史与 legacy 回退探针工程 | 是 |
| `HistoryProbe/HistoryProbe.cs` | 2026-11-20 01:06 | 否 | 3 | 读取 61111 两条历史与 51111 legacy summary | 是 |
| `HistoryProbe/manifest.json` | 2026-11-20 01:06 | 否 | 1 | 历史探针清单 | 是 |
| `v113.1-settingsprobe-stdout.log` | 2026-11-20 00:47 | 否 | 209 | 首次设置探针；DLL 文件名与 manifest id 不一致，未加载探针 | 是 |
| `v113.1-settingsprobe2-stdout.log` | 2026-11-20 00:49 | 否 | 217 | 设置页注册、主菜单打开、F8/F9 重绑定与非法绑定拒绝通过 | 是 |
| `v113.1-settingsprobe3-stdout.log` | 2026-11-20 01:10 | 否 | 212 | 语言、缩放、功能开关绑定写入/恢复与持久化通过 | 是 |
| `v113.1-autoslay-61111-stdout.log` | 2026-11-20 00:29 | 否 | 7 | Steam 未启动，Mod 未加载；失败启动记录 | 否 |
| `v113.1-autoslay-61111-run1-stdout.log` | 2026-11-20 00:57 | 否 | 838 | 1.5.0 种子 61111 第一局；StartTime 1795107016，胜利 | 是 |
| `v113.1-autoslay-61111-run2-stdout.log` | 2026-11-20 01:05 | 否 | 828 | 1.5.0 种子 61111 第二局；StartTime 1795107502，胜利 | 是 |
| `v113.1-historyprobe-stdout.log` | 2026-11-20 01:06 | 否 | 352 | 两条 61111 历史分别定位；51111 legacy 75 来源 | 是 |
| `v113.1-careerprobequit-post61111-stdout.log` | 2026-11-20 01:08 | 否 | 488 | 两局后生涯 201/187、标准 199/185、多人 2、愚者 15/13 | 是 |
| `v111.1-settingsprobe-stdout.log` | 2026-11-20 01:22 | 否 | 225 | v0.111.1 页面、绑定、主菜单入口与 F8/F9 热键通过 | 是 |
| `v111.1-autoslay-71111-stdout.log` | 2026-11-20 01:22 | 否 | 766 | v0.111.1 1.5.0 种子 71111 修复前冒烟；胜利但缺少 summary/live 清理 | 是 |
| `v111.1-autoslay-71112-stdout.log` | 2026-11-20 01:39 | 否 | 807 | v0.111.1 种子 71112 放弃局；修复后 summary 73 来源、运行态清理 | 是 |
| `v111.1-autoslay-71111-postfix-stdout.log` | 2026-11-20 01:47 | 否 | 768 | v0.111.1 种子 71111 修复后胜利局；summary 66 来源、运行态清理 | 是 |
| `v113.1-autoslay-71113-blitz-stdout.log` | 2026-11-20 02:18 | 否 | 562 | 最终 DLL 在 v0.113.1 的胜利局；summary 60 来源；重复收尾被拦截；结算界面超时 | 是 |

## 1.5.1

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `CompatProbe/CompatProbe.csproj` | 2026-11-20 04:12 | 否 | 1 | legacy 商店复制与损坏设置探针工程 | 是 |
| `CompatProbe/CompatProbe.cs` | 2026-11-20 04:12 | 否 | 6 | 创建 legacy 购买、调用续局与收尾、临时损坏并恢复设置 | 是 |
| `CompatProbe/manifest.json` | 2026-11-20 04:12 | 否 | 1 | 兼容探针清单 | 是 |
| `v113.1-build-1.5.1.log` | 2026-11-20 04:11 | 否 | 4 | v0.113.1 主 Mod 构建；0 错误、7 警告 | 是 |
| `v111.1-build-1.5.1.log` | 2026-11-20 04:11 | 否 | 4 | v0.111.1 程序集构建；0 错误、7 警告 | 是 |
| `v113.1-build-compatprobe.log` | 2026-11-20 04:12 | 否 | 1 | 兼容探针构建；0 错误 | 是 |
| `v113.1-compatprobe-stdout.log` | 2026-11-20 04:13 | 否 | 220 | 首次探针未加载；清单 id 与 DLL 文件名不一致 | 是 |
| `v113.1-compatprobe2-stdout.log` | 2026-11-20 04:15 | 否 | 211 | 修正清单后通过；legacy 复制与设置警告已验证 | 是 |
| `v111.1-build-compatprobe.log` | 2026-11-20 04:35 | 否 | 1 | v0.111.1 程序集下重建兼容探针；0 错误 | 是 |
| `v111.1-compatprobe-stdout.log` | 2026-11-20 04:36 | 否 | 213 | v0.111.1 兼容探针；legacy 复制与设置警告通过 | 是 |
| `v111.1-build-autoslaygate.log` | 2026-11-20 04:38 | 否 | 1 | v0.111.1 程序集下重建 AutoSlay 测试门；0 错误 | 是 |
| `v111.1-autoslay-81111-stdout.log` | 2026-11-20 04:44 | 否 | 754 | v0.111.1 标准 AutoSlayer；23 场战斗、62 来源、49 层放弃收尾 | 是 |
| `RQ014Probe/Rq014Probe.cs` | 2026-11-20 23:30 | 否 | 10 | 卡牌/药水/商店/先古/意图/Boss 幕号探针 | 是 |
| `RQ014Probe/Rq014Probe.csproj` | 2026-11-20 23:09 | 否 | 2 | RQ-014 探针工程 | 是 |
| `RQ014Probe/manifest.json` | 2026-11-20 23:09 | 否 | 1 | RQ-014 探针清单 | 是 |
| `v111.1-build-rq014-final6.log` | 2026-11-21 00:15 | 否 | 4 | 最终 v0.111.1 构建；0 错误、7 警告 | 是 |
| `v113.1-build-rq014-final5.log` | 2026-11-21 00:15 | 否 | 4 | 最终 v0.113.1 程序集构建；0 错误、7 警告 | 是 |
| `v111.1-build-rq014probe-final5.log` | 2026-11-20 23:30 | 否 | 1 | 最终 RQ-014 探针构建；0 错误 | 是 |
| `v111.1-autoslay-71112-rq014-final-stdout.log` | 2026-11-20 23:29 | 否 | 825 | v0.111.1 A10 种子 71112；卡牌、药水、商店、先古、Boss 幕号、扭动虫 | 是 |
| `v111.1-autoslay-71112-rq014-final.log` | 2026-11-20 23:29 | 否 | 135 | 同上局游戏日志 | 是 |
| `v111.1-autoslay-41111-rq014-stdout-console.log` | 2026-11-20 23:40 | 否 | 977 | v0.111.1 A10 种子 41111；啃咬机、图标档位、双 Boss | 是 |
| `v111.1-autoslay-41111-rq014-stdout.log` | 2026-11-20 23:40 | 否 | 154 | 同上局游戏日志 | 是 |
| `v111.1-autoslay-71112-rq014-stdout.log` | 2026-11-20 23:18 | 否 | 823 | 首轮 71112 探针；A0 基准值、意图基础标签 | 是 |
| `v111.1-autoslay-71112-rq014.log` | 2026-11-20 23:18 | 否 | 135 | 首轮 71112 游戏日志 | 是 |
| `v113.1-build-rq014-final6.log` | 2026-11-21 01:46 | 否 | 4 | v0.113.1 主 Mod 构建；0 错误、7 警告 | 是 |
| `v113.1-build-rq014-autoslaygate.log` | 2026-11-21 01:46 | 否 | 1 | v0.113.1 AutoSlay 测试门构建 | 是 |
| `v113.1-build-rq014probe.log` | 2026-11-21 01:46 | 否 | 1 | v0.113.1 RQ-014 探针构建 | 是 |
| `v113.1-build-rq014probe2.log` | 2026-11-21 02:00 | 否 | 1 | 增加顶部指示器可见性诊断后重建 | 是 |
| `v113.1-autoslay-71112-rq014-stdout.log` | 2026-11-21 01:54 | 否 | 897 | v0.113.1 A10 标准模式；胜利 Act 3/3、Floor 49 | 是 |
| `v113.1-autoslay-71112-rq014.log` | 2026-11-21 01:54 | 否 | 141 | 同上局游戏日志 | 是 |
| `v113.1-autoslay-blitz-71113-rq014-stdout.log` | 2026-11-21 01:59 | 否 | 576 | v0.113.1 Blitz；胜利 Act 3/3、Floor 24；指标隐藏 | 是 |
| `v113.1-autoslay-blitz-71113-rq014.log` | 2026-11-21 01:59 | 否 | 65 | 同上局游戏日志 | 是 |
| `v113.1-indicator-probe3-stdout.log` | 2026-11-21 02:10 | 否 | 492 | 标准模式顶部指示器可见性；`potionVisible=True cardVisible=True`；超时终止 | 是 |
| `v113.1-blitz-indicator-probe-stdout.log` | 2026-11-21 02:13 | 否 | 499 | Blitz 顶部指示器可见性；TopBar 节点 `potionVisible=False cardVisible=False`；超时终止 | 是 |
| `v113.1-build-rq014probe3.log` | 2026-11-21 02:04 | 否 | 1 | 增加 attach 诊断后重建 | 是 |
| `v113.1-build-rq014probe4.log` | 2026-11-21 02:07 | 否 | 1 | 修正探针节点查找后重建 | 是 |
| `MapFixProbe/MapFixProbe.cs` | 2026-11-21 03:50 | 否 | 4 | 地图悬停面板清理探针 | 是 |
| `MapFixProbe/MapFixProbe.csproj` | 2026-11-21 03:50 | 否 | 1 | 地图悬停面板清理探针工程 | 是 |
| `MapFixProbe/manifest.json` | 2026-11-21 03:50 | 否 | 1 | 地图探针清单；依赖主 Mod 1.5.1 | 是 |
| `mapfix-probe-stdout.log.stdout` | 2026-11-21 03:51 | 否 | 316 | v0.113.1 无头探针完整输出；`[MapFixProbe] PASS` | 是 |
| `mapfix-probe-stdout.log` | 2026-11-21 03:51 | 否 | 2 | 同一探针的游戏日志 | 是 |

## 1.5.2

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `build-v111-check2.log` | 2026-11-21 07:02 | 否 | 4 | v0.111.1 Release 构建；0 错误、7 个既有警告 | 是 |
| `build-v111-160-final.log` | 2026-11-21 07:24 | 否 | 4 | 最终源码的 v0.111.1 Release 构建；0 错误 | 是 |
| `build-v113-check3.log` | 2026-11-21 07:02 | 否 | 4 | v0.113.1 Release 构建；0 错误、7 个既有警告 | 是 |
| `build-v113-160-final.log` | 2026-11-21 07:24 | 否 | 4 | 最终源码的 v0.113.1 Release 构建；0 错误 | 是 |
| `build-regression-probe4.log` | 2026-11-21 07:17 | 否 | 1 | 持久化回归探针构建 | 是 |
| `build-palebluedot-probe3.log` | 2026-11-21 07:13 | 否 | 1 | PaleBlueDot 探针构建 | 是 |
| `build-pbd-v111.log` | 2026-11-21 08:42 | 否 | 1 | v0.111.1 安装下的 PaleBlueDot 探针构建；0 错误 | 是 |
| `build-ui-lifecycle-probe2.log` | 2026-11-21 07:14 | 否 | 1 | UI 生命周期探针构建 | 是 |
| `build-mapfixprobe.log` | 2026-11-21 07:16 | 否 | 1 | 地图悬停清理探针构建 | 是 |
| `regressionprobe-final-console.log` | 2026-11-21 08:07 | 否 | 320 | 最终回归完整输出；8 项 PASS，含 OSTY 与 legacy | 是 |
| `regressionprobe2-stdout.log` | 2026-11-21 07:17 | 否 | 311 | 中间回归输出；6 项 PASS | 是 |
| `regressionprobe2-game.log` | 2026-11-21 07:17 | 否 | 1 | 同一回归探针的游戏日志 | 是 |
| `palebluedot-final7b8-console.log` | 2026-11-21 08:21 | 否 | 452 | 最终 7b8 DLL 普通版；`PALE_BLUE_DOT`、mapSource、duplicateSameTurn=False | 是 |
| `palebluedot-1c-stdout.log` | 2026-11-21 07:18 | 否 | 451 | 中间普通版探针 | 是 |
| `palebluedot-final7b8-2-console.log` | 2026-11-21 08:23 | 否 | 455 | 最终 7b8 DLL 升级版；`firstDrawn=2`、mapSource、duplicateSameTurn=False | 是 |
| `palebluedot-2-stdout.log` | 2026-11-21 07:19 | 否 | 453 | 中间升级版探针 | 是 |
| `ui-lifecycle-stdout.log` | 2026-11-21 07:19 | 否 | 313 | 弹窗 helper、直接 Esc、回放清空；RESULT PASS；未触发真实 OnSubmenuHidden | 是 |
| `mapfix-152-stdout.log` | 2026-11-21 07:20 | 否 | 438 | 地图悬停清理；`[MapFixProbe] PASS` | 是 |
| `regressionprobe-v111-stdout-console.log` | 2026-11-21 07:20 | 否 | 332 | 复制安装的 v0.111.1 尝试；RitsuLib 仍选 0.113 分支，初始化失败 | 是 |
| `v111.1-final-91111-stdout.log` | 2026-11-21 08:39 | 否 | 785 | 真实 v0.111.1 完整 AutoSlay；主 Mod v1.5.2；26 战斗、60 来源、11 购买；第 49 层放弃 | 是 |
| `v111.1-final-91111.log` | 2026-11-21 08:39 | 否 | 131 | 同一局的游戏日志 | 是 |
| `v111.1-pbd-final-console.log` | 2026-11-21 08:45 | 否 | 357 | v0.111.1 PaleBlueDot；`firstDrawn=1`、来源/映射来源正确、`duplicateSameTurn=False` | 是 |
| `v111.1-pbd-final.log` | 2026-11-21 08:45 | 否 | 5 | 同一探针的游戏日志 | 是 |
| `v111.1-pbd-upgraded-console.log` | 2026-11-21 08:50 | 否 | 430 | v0.111.1 升级版；`firstDrawn=2`、来源/映射来源正确、`duplicateSameTurn=False` | 是 |
| `v111.1-pbd-upgraded.log` | 2026-11-21 08:50 | 否 | 5 | 同一探针的游戏日志 | 是 |
| `RegressionProbe/` | 2026-11-21 07:17 | 否 | 16 | 持久化、缓存、路径购买与放弃局探针工程（目录整体登记） | 是 |
| `PaleBlueDotProbe/` | 2026-11-21 07:19 | 否 | 24 | PaleBlueDot 普通/升级探针工程（目录整体登记） | 是 |
| `UiLifecycleProbe/` | 2026-11-21 07:19 | 否 | 20 | 弹窗、Esc、回放探针工程（目录整体登记） | 是 |

以上 `.log` 与 `.stdout` 由 `.gitignore` 排除，不进入版本库；只保留在本机。

## 1.6.0

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| `README.md` | 2026-11-21 19:32 | 否 | 3 | 1.6.0 阶段 0 基线测试计划、固定种子与探针分组 | 是 |
| `patch-inventory.md` | 2026-11-21 19:32 | 否 | 5 | 当前 361 条注册记录、P0 目标、16 张能力牌与 15 个遗物清点 | 是 |
| `baseline-v111.log` | 2026-11-21 19:32 | 否 | 4 | v0.111.1 基线 Release 构建；0 错误、7 个既有警告 | 是 |
| `baseline-v113.log` | 2026-11-21 19:32 | 否 | 4 | v0.113.1 基线 Release 构建；0 错误、7 个既有警告 | 是 |
| `build-v111-integrated.log` | 2026-11-21 23:45 | 否 | 4 | 1.6.0 集成源码 v0.111.1 Rebuild；0 错误、7 个既有警告 | 是 |
| `build-v113-integrated.log` | 2026-11-21 23:45 | 否 | 4 | 1.6.0 集成源码 v0.113.1 Rebuild；0 错误、7 个既有警告 | 是 |
| `build-attribution-probe.log` | 2026-11-21 23:45 | 否 | 1 | 1.6.0 定向探针构建；0 错误 | 是 |
| `AttributionProbe/` | 2026-11-21 23:10 | 否 | 16 | 首场治疗、锻造消费、升级格挡、待定抽牌探针工程（目录整体登记） | 是 |
| `attribution-probe-v111-3-stdout.log` | 2026-11-21 23:46 | 否 | 315 | v0.111.1 定向探针中间通过输出；4/4 PASS | 是 |
| `attribution-probe-v111-4-stdout.log` | 2026-11-22 00:02 | 否 | 315 | 最终代码 v0.111.1 定向探针；4/4 PASS，RESULT PASS | 是 |
| `attribution-probe-v111-5-stdout.log` | 2026-11-22 00:12 | 否 | 315 | 最终 DLL 定向探针；4/4 PASS；RESULT 后无头进程异常退出 | 是 |
| `attribution-probe-v111-6-stdout.log` | 2026-11-22 00:25 | 否 | 315 | 审阅修正后最终 DLL 定向探针；4/4 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-7-stdout.log` | 2026-11-22 00:55 | 否 | 315 | 上下文保存/恢复改动后探针；5/5 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-8-stdout.log` | 2026-11-22 01:05 | 否 | 315 | 上下文恢复与费用节省探针；6/6 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-10-stdout.log` | 2026-11-22 01:20 | 否 | 315 | 实际抽牌、上下文、费用探针；7/7 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-11-stdout.log` | 2026-11-22 01:30 | 否 | 315 | 实际抽牌、上下文、费用、休息处拆分探针；8/8 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-12-stdout.log` | 2026-11-22 01:40 | 否 | 315 | 负向抽牌净额与全部探针；8/8 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-13-stdout.log` | 2026-11-22 01:50 | 否 | 315 | 多来源格挡探针；9/9 PASS，无头退出异常 | 否 |
| `attribution-probe-v111-14-stdout.log` | 2026-11-22 01:50 | 否 | 315 | 多来源格挡最终复测；9/9 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-15-stdout.log` | 2026-11-22 02:00 | 否 | 315 | 卡牌上下文与全部探针；10/10 PASS，EXIT=0 | 是 |
| `attribution-probe-v111-9-stdout.log` | 2026-11-22 01:18 | 否 | 315 | 同组探针 7/7 PASS，但无头退出异常；保留失败记录 | 否 |
| `v111-autoslay-62001.log` | 2026-11-22 00:39 | 否 | 71 | v0.111.1 AutoSlayer 种子 62001；跑到第 25 层、14 场战斗后测试超时 | 是 |
| `v111-autoslay-62001-stdout.log` | 2026-11-22 00:39 | 否 | 654 | 同一局游戏输出；主 Mod 初始化、战斗保存与击杀补刀记录 | 是 |
| `attribution-probe-v111-2-stdout.log` | 2026-11-21 23:44 | 否 | 314 | 探针中间失败：锻造复合键读取错误；保留作失败记录 | 否 |
| `attribution-probe-v111-stdout.log` | 2026-11-21 23:43 | 否 | 310 | 首次带 Ritsu/主 Mod 的探针尝试；无头资源加载崩溃，探针未执行 | 否 |
| `attribution-probe-isolated-stdout.log` | 2026-11-21 23:45 | 否 | 14,509 | 隔离探针缺少主 DLL 依赖，FileNotFound 重复报错；保留作失败记录 | 否 |
| `build-v113-160-final.log` | 2026-11-22 02:00 | 否 | 4 | 1.6.0 最终 v0.113.1 Release 构建；0 错误、7 个既有警告 | 是 |
| `install-check-1.6.0-stdout.log` | 2026-11-22 02:41 | 否 | 328 | 正式安装后 v0.113.1 主菜单加载；v1.6.0 初始化、F8 注册、无重复实例 | 是 |
| `build-v111-160-final.log` | 2026-11-22 02:00 | 否 | 4 | 1.6.0 最终 v0.111.1 Release 构建；0 错误、7 个既有警告 | 是 |
| `build-fool-live-probe.log` | 2026-11-22 01:33 | 否 | 1 | 愚者实机/上下文探针构建；0 错误 | 是 |
| `FoolLiveProbe/` | 2026-11-22 01:33 | 否 | 20 | 愚者 18 能力/15 遗物/5 触发牌与上下文检查探针工程（目录整体登记） | 是 |
| `v113.1-autoslay-62005-stdout.log` | 2026-11-22 01:32 | 否 | 896 | v0.113.1 标准完整 AutoSlayer；victory，Silent，49 层；29 战斗、53 来源 | 是 |
| `v113.1-autoslay-62005-autoslay.log` | 2026-11-22 01:32 | 否 | 148 | 同一局 AutoSlayer 操作日志 | 是 |
| `v113.1-foolprobe-62003c-stdout.log` | 2026-11-22 01:47 | 否 | 880 | v0.113.1 愚者实机；victory，Ironclad，48 层；28 战斗、97 来源、7 购买 | 是 |
| `v113.1-foolprobe-62003c-autoslay.log` | 2026-11-22 01:47 | 否 | 123 | 同一愚者局 AutoSlayer 操作日志 | 是 |
| `v113.1-foolprobe-context-62999-stdout.log` | 2026-11-22 01:48 | 否 | 365 | 愚者上下文检查；18/18 能力、15/15 遗物 PASS | 是 |
| `final-load-63001-stdout.log` | 2026-11-22 02:10 | 否 | 610 | 最终 1.6.0 DLL 加载；v1.6.0 初始化、34 条 hook、AutoSlayer 选择愚者；外部 60 秒超时终止 | 是 |
| `v113.1-foolprobe-62003b-stdout.log` | 2026-11-22 01:40 | 否 | 779 | 首次成功加入 18 能力但未记录来源的愚者实机；保留作对照 | 否 |
| `v113.1-foolprobe-62003-stdout.log` | 2026-11-22 01:33 | 否 | 662 | 首次愚者实机；SetUpCombat patch 未生效，能力未加入；保留失败记录 | 否 |
| `CombatOracleProbe/` | 2026-11-22 04:32 | 否 | 162 | 战斗事件采集器工程：CombatHistory 记录、AfterDamageGiven 全伤害采集、战斗结束保存（目录整体登记） | 是 |
| `build-combatoracle.log` | 2026-11-22 04:32 | 否 | 1 | 采集器首次构建；0 错误 | 是 |
| `build-combatoracle-2.log` | 2026-11-22 04:40 | 否 | 1 | 修复 run-end 轮询后的构建；0 错误 | 是 |
| `build-combatoracle-3.log` | 2026-11-22 04:42 | 否 | 1 | 加入 `Hook.AfterDamageGiven` 前缀后的构建；0 错误 | 是 |
| `compare_combat_oracle.py` | 2026-11-22 04:48 | 否 | 9 | 事件流与 `combat_*.json` 的比对脚本；查楼层、伤害、格挡、出牌、抽牌 | 是 |
| `oracle-62003-stdout.log` | 2026-11-22 04:40 | 否 | 951 | 首次愚者采集运行；只用 DamageReceived，击杀伤害漏采，保留作校准记录 | 否 |
| `oracle-62003-autoslay.log` | 2026-11-22 04:40 | 否 | 123 | 首次愚者采集运行的操作日志 | 否 |
| `oracle2-62003-stdout.log` | 2026-11-22 04:50 | 否 | 864 | 第二次愚者采集运行；AfterDamageGiven 全伤害采集生效；29 事件、28 Mod 战斗 | 是 |
| `oracle2-62003-autoslay.log` | 2026-11-22 04:50 | 否 | 121 | 第二次愚者采集运行的操作日志 | 是 |
| `oracle3-62005-stdout.log` | 2026-11-22 04:59 | 否 | 936 | 标准局采集运行；29 事件、29 Mod 战斗；16/29 完全一致 | 是 |
| `oracle3-62005-autoslay.log` | 2026-11-22 04:59 | 否 | 128 | 标准局采集运行的操作日志 | 是 |
| `oracle2-compare-62003.txt` | 2026-11-22 04:50 | 否 | 3 | 愚者局比对：23 PASS、6 非 PASS；含第 45 层缺失 | 是 |
| `oracle3-compare-62005.txt` | 2026-11-22 05:00 | 否 | 3 | 标准局比对：16 PASS、13 非 PASS；含无敌阶段与 UNTRACKED 候选 | 是 |


以上 `.log` 由 `.gitignore` 排除，不进入版本库；只保留在本机。

## 变更记录

| 时间 | 变更 |
|---|---|
| 2026-11-19 07:02 | 建立测试登记表 |
| 2026-11-19 07:16 | 登记构建、加载、AutoSlayer 与生涯探针文件 |
| 2026-11-19 07:19 | 登记 UI 探针；回填生涯模式、界面与清理状态 |
| 2026-11-19 08:16 | 登记 1.4.1 愚者、单文件兼容与 fork 构建测试 |
| 2026-11-19 08:24 | 登记 1.4.2 双版本编译与商店路径测试 |
| 2026-11-19 20:10 | 登记 1.4.3 v0.111.1 构建、加载、完整流程与探针测试 |
| 2026-11-19 20:45 | 登记 1.4.4 双版本构建与商店文件对照探针 |
| 2026-11-19 21:09 | 登记 v0.113.1 两种子实机、通关后生涯探针与探针构建 |
| 2026-11-20 00:49 | 建立 1.5.0 测试表；登记设置探针与 v0.113.1 首次实机 |
| 2026-11-20 01:10 | 登记绑定持久化、同 seed 两局、历史探针与生涯探针结果 |
| 2026-11-20 01:48 | 登记 v0.111.1 修复前胜利局、放弃局与修复后胜利局 |
| 2026-11-20 02:24 | 登记最终 DLL 的 v0.113.1 收尾回归 |
| 2026-11-20 02:40 | 补登 Steam 未启动的失败启动日志 |
| 2026-11-20 04:16 | 建立 1.5.1 测试表；登记双版本构建、兼容探针与失败探针 |
| 2026-11-20 04:45 | 登记 v0.111.1 兼容探针、AutoSlay 测试门与标准对局日志 |
| 2026-11-20 04:47 | 1.4.3 空日志 `v111.1-patchcheck.log` 已移入回收站 |
| 2026-11-20 23:47 | 登记 RQ-014 探针、最终双版本构建、71112/41111 实机日志 |
| 2026-11-21 00:20 | 审阅后补测缓存 schema 与涅奥回退；最终构建 final6/final5 |
| 2026-11-21 02:00 | 登记 v0.113.1 双模式实机、构建与顶部指示器短探针 |
| 2026-11-21 02:14 | 修正探针节点查找；标准模式指标可见、Blitz 隐藏已实测 |
| 2026-11-21 03:52 | 新增地图悬停清理探针；`MapFixProbe PASS`，无 `Child is not a child` 错误 |
| 2026-11-21 07:15 | 建立 1.5.2 测试表；登记回归、PaleBlueDot、UI、地图探针与 v0.111.1 尝试 |
| 2026-11-21 08:46 | 登记真实 v0.111.1 完整实机、PaleBlueDot 与 v111 探针构建 |
| 2026-11-21 19:32 | 建立 1.6.0 阶段 0 测试表；登记双版本基线构建、测试计划与补丁清点
| 2026-11-21 23:46 | 登记 1.6.0 集成构建、定向探针工程与 v0.111.1 PASS 日志；失败尝试保留
| 2026-11-22 00:02 | 最终代码复测：双版本构建 0 错误、7 警告；v0.111.1 定向探针 4/4 PASS |
| 2026-11-22 05:05 | 登记战斗事件采集器、两局采集日志、比对脚本与结果；测试部署已移出游戏 |
