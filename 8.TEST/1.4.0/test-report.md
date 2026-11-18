# 1.4.0 构建与实机测试报告

- 时间：2026-11-19 07:15
- 测试对象：`sts2_statsthespire_local.dll`，版本 1.4.0
- 游戏：Slay the Spire 2 v0.113.1（`release_info.json`，2026-11-14）
- 测试方式：本机无头启动、AutoSlayer、CareerProbe 测试 Mod

## 构建

| 项目 | 结果 |
|---|---|
| 命令 | `dotnet build 9.SRC/sts2_statsthespire_local.csproj -c Debug -p:DefineConstants=STS2_GE_V105` |
| 退出码 | 0 |
| 错误 | 0 |
| 警告 | 7（可空引用警告，未阻断） |
| 输出 | `9.SRC/bin/Debug/sts2_statsthespire_local.dll` |
| 大小 | 514,560 字节 |
| SHA-256 | `f3d0b607f6f50fd667c039dd3e8f16adea34b4d294d3790a2ecd10db33177bec` |
| 日志 | `build1.log` 至 `build4.log`；`build-final.log` |

## 静态检查

| 检查 | 结果 |
|---|---|
| 联网符号 | 源码中未发现 `HttpClient`、`System.Net`、服务器地址、`Updater`、`UploadNotice` 等符号 |
| manifest 版本 | `1.4.0` |
| `ModConfig.ModVersion` | `1.4.0` |
| API 目标 | 25 个关键类型/方法全部存在（`api-targets.log`） |
| Harmony 目标 | 首次加载时所需的补丁目标全部注册；可选目标缺失时记录警告并继续 |

## 无头加载

| 项目 | 结果 |
|---|---|
| 日志 | `game-load2.log` |
| Mod 初始化 | `[StatsTheSpire] Stats the Spire (Local) v1.4.0 initializing...` |
| 完成 | `[StatsTheSpire] Stats the Spire initialized successfully` |
| 补丁失败 | 无 |
| 已知警告 | 启动早期 `InitProfileId must be called on SaveManager!`；后续 UI 打开时会重新加载，未导致崩溃 |

## AutoSlayer

| 项目 | 结果 |
|---|---|
| 日志 | `autoslay-run2.log` |
| 测试门 | `stats_autoslay_gate` 测试 Mod，把 `NGame.IsReleaseGame()` 置为 false |
| 进度 | 完成 Act 1 的 Event、2 场普通战斗、Shop、Elite、Treasure、RestSite，进入 Boss |
| 贡献数据 | 生成 `12345_2.json`、`12345_3.json`、`12345_5.json`、`12345_live.json` |
| 本 Mod 异常 | 未发现 |
| 无关异常 | `NSpineAutoPlayer` 骨架动画错误，来自其他 Mod，与本 Mod 无关 |
| 面板自动显示 | 未验证；用户设置中 `contribution_panel=false`，自动显示被开关关闭 |

## 生涯统计

| 项目 | 结果 |
|---|---|
| 日志 | `career-probe.log`、`career-probe2.log` |
| 样本 | 195 局历史 |
| 总览 | 195 局，183 胜 |
| 平均数据 | 用时 2511.1 秒；卡组 28.8；遗物 13.7；攻击 7.3；技能 15.5；能力 4.1；普通 6.5；罕见 9.0；稀有 3.9 |
| 模式筛选 | All 195 局；Standard 193 局；Multiplayer 2 局 |
| UI 创建 | `CareerStatsSection` 创建成功，子节点数 7；平均用时卡片显示「41 分钟 51 秒」 |
| 缓存 | `career_all_All.json`、`career_all_Standard.json`、`career_all_Multiplayer.json` 写入成功 |

## 界面探针

| 项目 | 结果 |
|---|---|
| 日志 | `ui-probe2.log` |
| 贡献面板 | 开启开关后，`ShowCombatResult` 使面板可见；`Hide` 后面板隐藏 |
| 设置面板 | `FilterPanel.Toggle` 使设置面板可见 |
| 异常 | 本 Mod 无异常 |

## 未验证

- v0.111 构建与实机。
- 「愚者」角色历史记录的专项检查。
- F7/F8 键位的人工操作与面板视觉布局。
- 贡献面板自动显示在真实战斗结束时的视觉确认（方法调用已验证，用户设置默认关闭）。
- 长时间运行的内存与性能。

## 测试部署

- 游戏 `mods/StatsTheSpire_Test/`：主 Mod 测试副本，已移出。
- 游戏 `mods/StatsTheSpire_AutoSlayGate/`：AutoSlayer 测试门，已移出。
- 游戏 `mods/StatsTheSpire_CareerProbe/`：生涯统计测试探针，已移出。
- 游戏 `mods/StatsTheSpire_UiProbe/`：界面测试探针，已移出。
- 移出的副本保存在 `TMP/1.4.0/test-deploy/`；种子 12345 的贡献数据保存在 `TMP/1.4.0/test-data/`。
- 游戏进程已关闭。
- 测试未修改玩家既有设置；`mod_prefs.json` 中 `contribution_panel=false`、`intent_state_machine=false` 是玩家原有设置。
