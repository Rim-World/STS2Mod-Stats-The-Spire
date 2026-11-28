# RitsuLib 0.6.16 兼容性验证

- 编号：048-R
- 时间：2026-11-28 18:46
- 适用版本：Mod 1.11.0；游戏 v0.114.0；RitsuLib 0.6.16
- 结论：RitsuLib 0.6.16 过审包在 v0.114.0 上加载正常，设置页补丁 19/19 成功，主 Mod 设置页注册成功；AutoSlayer 完整跑局胜利，统计文件完整。v0.113.1、v0.111.1 运行与 UI 视觉未验证。

## 1. 背景

本地 `mods/STS2-RitsuLib` 也是 0.6.16，但它的清单要求 `min_game_version=0.114.0`，与 Steam 过审包的 `0.107.1` 不同。游戏加载时本地副本会覆盖 Steam 版，因此直接在用户安装目录启动不能证明过审包可用。

本次测试建立临时游戏目录，放入 Steam 过审包的完整文件，再启动游戏。用户安装目录中的本地 RitsuLib 和主 Mod 未移动、未覆盖。

## 2. 环境

| 项目 | 值 |
|---|---|
| 游戏版本 | v0.114.0，commit `a7b9a9c0` |
| 主 Mod | 1.11.0 |
| RitsuLib | 0.6.16，兼容分支 `0.114.0` |
| RitsuLib 包 | `C:\Game\Steam\steamapps\workshop\content\2868840\3747602295` |
| RitsuLib 根 DLL SHA-256 | `4a2d5fb8a4315fa240b9c7ba7fc98f64f373646dfb5a3940d18f12e39d8bb553` |
| 临时游戏目录 | `TMP/1.11.0/ritsu0616-env/Game` |
| 测试门 | `8.TEST/1.4.0/AutoSlayGate`，仅把 `NGame.IsReleaseGame()` 改为 `false` |

临时目录使用硬链接指向游戏可执行文件与 `.pck`，并使用目录联接指向游戏数据目录。`data_sts2_windows_x86_64` 与主安装目录共用，未复制。

## 3. 方法

1. 复制 Steam 过审包到临时目录的 `mods/STS2-RitsuLib`；复制 1.11.0 安装目录到 `mods/sts2_statsthespire_local`。
2. 放入 AutoSlayer 测试门；启动参数加 `SteamAppId=2868840`、`--headless`。
3. 先做 45 秒主菜单加载，检查 RitsuLib 版本、分支、补丁、设置页注册与主 Mod 初始化。
4. 再做完整跑局：`--autoslay --seed=62005 --mode=standard`。
5. 跑局结束后检查：进程退出码、Run 历史、贡献 JSON 数量、来源 ID、字段汇总、与存档战斗房间数是否一致。

## 4. 结果

### 4.1 加载

- RitsuLib 输出：`RitsuLib Version: 0.6.16 [compat branch: 0.114.0]`。
- RitsuLib 设置页补丁：`19 applied, 0 ignored, 0 failed`。
- 主 Mod：`Stats the Spire (Local) v1.11.0 initializing...`。
- 主 Mod 设置页：`[StatsTheSpire] [RitsuSettings] registered settings page`。
- 主 Mod 热键：`Ctrl+F8` 注册成功。
- 补丁绑定：818 条，454 个目标。
- 加载日志：`8.TEST/1.11.0/ritsu0616-load-stdout.log`。

### 4.2 完整跑局

| 项目 | 结果 |
|---|---|
| 种子 | 62005 |
| 模式 | Standard |
| 角色 | Silent |
| 进阶 | 10 |
| 结局 | 胜利 |
| 幕 | 3/3 |
| 最终楼层 | 49 |
| 用时 | 约 517 秒 |
| 退出码 | 0 |

游戏日志：`8.TEST/1.11.0/ritsu0616-autoslay-62005-game.log`。
标准输出：`8.TEST/1.11.0/ritsu0616-autoslay-62005-stdout.log`。

### 4.3 统计输出

Run 目录：`C:/Users/13167/AppData/Roaming/sts2_community_stats/contributions/1795862221_62005`。

| 检查 | 结果 |
|---|---|
| JSON 文件 | 31 个，全部可以解析 |
| 战斗文件 | 29 个 |
| 存档战斗房间 | 29 个，数量一致 |
| 汇总来源 | 45 个，SourceId 全部唯一 |
| 悬空 OriginSourceId | 0 |
| 汇总 DirectDamage | 1479 |
| 汇总 ModifierDamage | 3447 |
| 汇总 AttributedDamage | 565 |
| 汇总 EffectiveBlock | 913 |
| 汇总 CardsDrawn | 156 |
| 汇总 EnergyGained | 73 |
| AutoPlayedCount | 548 |
| TimesPlayed | 字段未写入；AutoSlayer 全程自动出牌，按 0 处理 |

跑局期间未发现主 Mod 或 RitsuLib 的异常。主 Mod 有一条初始化前警告：`RunHistoryAnalyzer: GetAllRunHistoryNames failed: InitProfileId must be called on SaveManager!`；该警告在 RitsuLib 加载前已存在，本次测试未把它当作回归。

## 5. 网络与外部副作用

RitsuLib 0.6.16 在启动时做了两类外部操作：

- 发送 1 条 `session_start` 遥测到 `https://ritsulib-telemetry.ritsukage.com/v1/ingest`，本地状态记录 `last_send_utc=2026-11-28T10:36:59Z`、`failure_count=0`。事件包含加载 Mod 数、会话标识与匿名安装标识，不含本报告中的账号密码。
- 扫描 65 个 Steam 订阅项，并对 Workshop 项 `3786286239` 发起更新队列请求。日志显示 1 个需要更新，未观察到实际下载完成。

主 Mod 本身没有发起网络请求。

## 6. 未验证

- v0.113.1 运行：本机没有对应游戏版本安装。
- v0.111.1 运行：本机没有对应游戏版本安装。
- 设置页实际视觉、`Ctrl+F8` 面板显示与交互：本次为无头运行，无法检查画面。
- 真实用户安装目录的 Steam 版覆盖行为：测试时本地副本未移动；18:52 已按用户要求移走本地副本，实际游戏加载创意工坊版通过，见 049-R。

## 7. 结论

RitsuLib 0.6.16 过审包与 Mod 1.11.0 在 v0.114.0 上完成了加载、设置页注册、完整跑局和统计落盘。没有发现 RitsuLib 版本不匹配或主 Mod 运行异常。测试同时确认 RitsuLib 0.6.16 默认会发送遥测并检查 Workshop 更新。
