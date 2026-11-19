# RitsuLib 迁移与对局身份修复

- 状态：进行中
- 层级：M
- 创建时间：2026-11-19 21:57
- 开始施工：2026-11-20 00:24
- 目标版本：1.5.0
- 游戏：Slay the Spire 2 v0.111.1、v0.113.1
- 硬依赖：STS2-RitsuLib
- 不引入：BaseLib

## 1. 目标

1. 用 RitsuLib 的模组设置页替代自绘 F7 设置面板。
2. 删除 F7 注册与输入轮询；F8 改为可重绑定热键，默认仍为 F8。
3. 用 `StartTime + seed` 建立对局身份，隔离同种子的不同对局。
4. 保持统计数据的本地外部存储；不写入游戏存档目录，不使用 RitsuLib `RunSavedData`。
5. 保留旧贡献数据的只读回退，不做破坏性自动迁移。
6. 完成单文件兼容双版本的mod构建、实机验证、文档与版本发布。

## 2. 非目标

1. 不引入 BaseLib。
2. 不重写战斗贡献归因、历史分析、生涯统计、意图状态机或统计图。
3. 不修改游戏平衡、游戏存档或游戏本体文件。
4. 不恢复联网上传、账号、社区统计或 Workshop 发布。
5. 不自动拆分无法判定归属的旧同种子数据。
6. 不迁移 `RunHistoryAnalyzer`、`CombatHistoryPatch` 等核心统计代码到基础库。

## 3. 已确认事实

### 3.1 当前数据行为

Mod 对游戏状态是只读的，但它会写入自己的本地缓存。当前主要写入：

- `%AppData%/sts2_community_stats/mod_prefs.json`：设置。
- `%AppData%/sts2_community_stats/contributions/`：贡献、汇总、运行态和商店数据。
- `%AppData%/sts2_community_stats/cache/`：生涯统计缓存。

当前贡献目录中的文件包括：

- `{seed}_{floor}.json`
- `{seed}_summary.json`
- `{seed}_live.json`
- `{seed}_shop_purchases.json`
- `{seed}_shop_offers.json`

这些文件不是游戏存档，也不会上传。它们是 Mod 自己的统计缓存。

### 3.2 同种子污染证据

`41111` 在 v0.111.1 已使用。v0.113.1 再开同种子对局后：

- 目录保留 10 个旧战斗文件。
- 新局新增 29 个文件。
- 汇总合并出 92 个来源。
- 商店购买为 7 条：旧 3 条加本轮 4 条。

干净种子 `51111` 为 75 个来源、9 条购买。证据位于 `TMP/1.4.4/evidence-41111-mixed/` 与 `TMP/1.4.4/evidence-51111/`。

### 3.3 RitsuLib 能力

RitsuLib 0.6.13 的本地源码确认：

- `RitsuLibFramework.RegisterModSettings` 注册设置页。
- `ModSettingsPageBuilder` 支持开关、滑块、选择项和键位绑定。
- `ModSettingsCallbackValueBinding<T>` 可以直接读写现有配置，不需要迁移数据存储。
- 设置页支持主菜单、跑局暂停、战斗暂停。
- `RuntimeHotkeyService` 支持运行时热键、重新绑定和文本输入抑制。
- 兼容目标包含 0.111 与 0.113。

RitsuLib 的兼容性由基础库维护。本项目只验证本 Mod 的调用是否正确，不重复验证 RitsuLib 自身对游戏版本的兼容性。

### 3.4 RitsuLib `RunSavedData` 不使用

RitsuLib 的 `RunSavedData` 会通过补丁写入游戏 run save。源码中的 `RunSavedDataSaveRunPatch` 和 `RunSavedDataSaveStoreWritePatch` 会把扩展数据注入当前存档。

这违反项目非目标与 `SP-013`：Mod 不得写入游戏存档目录。本计划明确排除 `RunSavedData`。运行态和商店数据继续写入 `%AppData%/sts2_community_stats/contributions/`，按对局分目录。

## 4. 设计决策

### 4.1 依赖策略

1. `sts2_statsthespire_local.csproj` 增加 `PackageReference`，包名为 `STS2.RitsuLib`。
2. 施工开始时确认最新稳定版。当前本地参考版本为 `0.6.13`；如果届时已有更新版本，使用更新版本。
3. `PackageReference` 固定到施工时选定的精确版本，不使用 `*` 或浮动版本。每次发版前按更新流程升级。
4. `manifest.json` 增加运行时依赖：

   ```json
   "dependencies": [
     { "id": "STS2-RitsuLib", "minVersion": "<编译所用版本>" }
   ]
   ```

5. 不把 RitsuLib 的 DLL 复制进本项目产物。玩家安装完整的 `mods/STS2-RitsuLib/` 文件夹。
6. 不添加 BaseLib 依赖、代码或安装说明。

### 4.2 对局身份

对局主键使用 `StartTime`，seed 只作为内容标识和可读后缀。

- `StartTime` 来自 `RunManager._startTime`，在四个 `RunManager.SetUp...` 入口读取。
- 新局和续局都使用同一个 `StartTime`；续局不会生成新键。
- 读取失败时记录警告，并降级到 legacy seed 逻辑；不得静默使用当前时间。
- 目录名为 `{StartTime}_{sanitizedSeed}`。

### 4.3 新局与续局

| 入口 | 行为 |
|---|---|
| `SetUpNewSingleplayer` | 清空内存；建立新 RunKey；不加载旧 live 或 shop 文件 |
| `SetUpNewMultiplayer` | 同上 |
| `SetUpSavedSingleplayer` | 清空内存；使用存档 StartTime；只加载当前 RunKey 的 live 与 shop 文件 |
| `SetUpSavedMultiplayer` | 同上 |

### 4.4 设置与热键

- 设置页由 RitsuLib 注册，页面 ID 使用 Mod ID。
- 现有 `ModConfig` 和 `mod_prefs.json` 保留；通过 callback binding 读写，不迁移到新的存储格式。
- 设置页包含语言、意图面板缩放、8 个功能开关、贡献面板热键。
- F7 自绘设置页删除。
- F8 改为 RitsuLib 运行时热键，默认绑定 `F8`，可在设置页重绑定。
- 热键注册失败时记录警告并回退到 `F8`。

## 5. 目标结构

### 5.1 组件

| 组件 | 职责 | 不负责 |
|---|---|---|
| `RunIdentity` | 保存 StartTime、Seed、RunKey；判断身份是否有效 | 不读写文件 |
| `RunStorage` | 解析对局目录、读写贡献与商店文件 | 不管理 UI 和设置 |
| `RitsuSettingsPage` | 注册设置页、绑定配置、按键位变更 | 不实现统计逻辑 |
| `HotkeyRegistration` | 注册和重绑定贡献面板热键 | 不处理面板显隐逻辑 |
| `LegacyContributionReader` | 只读旧 `{seed}_*.json` | 不自动改写或移动旧文件 |

### 5.2 数据布局

| 当前 | 1.5.0 |
|---|---|
| `{seed}_{floor}.json` | `{StartTime}_{seed}/combat_{floor}_{sequence}.json` |
| `{seed}_summary.json` | `{StartTime}_{seed}/summary.json` |
| `{seed}_live.json` | `{StartTime}_{seed}/live.json` |
| `{seed}_shop_purchases.json` | `{StartTime}_{seed}/shop_purchases.json` |
| `{seed}_shop_offers.json` | `{StartTime}_{seed}/shop_offers.json` |
| 无 | `{StartTime}_{seed}/meta.json` |

`meta.json` 记录：

- `schemaVersion`
- `startTime`
- `seed`
- `gameVersion`
- `modVersion`
- `createdAt`

### 5.3 设置键

| 键 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `language` | string | `CN` | 现有键 |
| `feature_toggles` | object | 全部开启 | 现有键 |
| `intent_panel_scale` | float | `0.8` | 现有键 |
| `contribution_panel_hotkey` | string | `F8` | 新增键 |

面板位置、面板尺寸、`use_my_data_only` 继续保留现有行为。

### 5.4 热键

| 项目 | 值 |
|---|---|
| 稳定 ID | `sts2_statsthespire_local.toggle_contribution_panel` |
| 默认绑定 | `F8` |
| 作用 | 切换贡献面板 |
| 设置入口 | RitsuLib 设置页 |
| 输入抑制 | 文本输入和开发者控制台可见时抑制 |
| 持久化 | `mod_prefs.json` 的 `contribution_panel_hotkey` |

## 6. 实施阶段

### 阶段 0：基线、备份与文档门禁

预计 0.5 到 1 人日。

- [ ] 记录当前 Mod 1.4.4、游戏 v0.111.1/v0.113.1、Git 提交。
- [ ] 复制 `%AppData%/sts2_community_stats/contributions/` 与 `mod_prefs.json` 到 `TMP/1.5.0/pre-migration-backup/`，只复制，不移动。
- [ ] 盘点旧贡献文件的 seed、数量、大小和对应历史记录。
- [ ] 新建 ADR-006，记录 RitsuLib 硬依赖、排除 BaseLib、排除 `RunSavedData`。
- [ ] 在 PRD 增加 RQ-011（RitsuLib 设置与 F8）和 RQ-012（同种子对局隔离）。
- [ ] 更新 SPEC：替换 SP-025、SP-026，增加 RunKey、历史定位、legacy 回退和禁止写入游戏存档的条款。
- [ ] 确认 RitsuLib 最新稳定版与 NuGet 可用性。当前本地参考为 0.6.13；NuGet 缓存只有 0.5.15。若无法获取目标版本，停止施工并汇报，不把本地 DLL 当作正式依赖。

验收：

- 文档编号与追溯表可查。
- 旧数据有独立备份。
- 依赖版本和包源明确。

### 阶段 1：RitsuLib 依赖与构建

预计 1 到 2 人日。

- [ ] 在 `9.SRC/sts2_statsthespire_local.csproj` 增加 `PackageReference`。
- [ ] 确保 RitsuLib 程序集不复制到输出目录；输出仍只有本 Mod 的 DLL。
- [ ] 在 `manifest.json` 增加 `STS2-RitsuLib` 依赖与 `minVersion`。
- [ ] 在构建说明与 README 中写明：玩家必须安装完整 RitsuLib 文件夹，不能只复制根 DLL。
- [ ] 对 v0.113.1 构建；对 v0.111.1 的 `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll` 构建。
- [ ] 启动一次带 RitsuLib 的游戏，确认依赖加载顺序和本 Mod 初始化。

验收：

- `AC-008-01` 两次构建均为 0 错误。
- `AC-008-02` 输出目录不包含 RitsuLib 程序集。
- `AC-008-03` manifest 声明依赖，游戏日志显示 RitsuLib 先加载。
- `AC-008-04` 不安装 BaseLib 也能启动。

### 阶段 2：RitsuLib 设置页

预计 1.5 到 2.5 人日。

- [ ] 新增 `9.SRC/src/Settings/RitsuSettingsPage.cs`。
- [ ] 调用 `RitsuLibFramework.RegisterModSettings` 注册页面。
- [ ] 页面标题使用 Mod 名称，页面 ID 使用 `sts2_statsthespire_local`。
- [ ] 增加语言选择项，写回 `ModConfig.Language` 和 `L.Current`。
- [ ] 增加意图面板缩放滑块，范围 0.5 到 1.25，步长 0.05。
- [ ] 遍历 `FeatureToggles.ToggleDefinitions` 生成 8 个开关。
- [ ] 所有标签使用 `ModSettingsText.Dynamic(() => L.Get(key))`。
- [ ] 所有值使用 `ModSettingsCallbackValueBinding<T>`，读写现有 `ModConfig`。
- [ ] 保存回调调用 `ModConfig.SaveSettings()`。
- [ ] 增加统一的 `ApplySettingsChanged()`，刷新顶部指示器、贡献面板和意图面板。
- [ ] 删除 `9.SRC/src/UI/FilterPanel.cs`。
- [ ] 删除 `CommunityStatsMod` 中的 F7 字段、轮询和根节点挂载。
- [ ] 更新 `FeatureToggles` 注释中过时的 F9 文案。
- [ ] 清理 `settings.*` 中只被旧面板使用的本地化键。

验收：

- `AC-008-05` 主菜单可打开设置。
- `AC-008-06` 跑局暂停可打开设置。
- `AC-008-07` 战斗暂停可打开设置。
- `AC-008-08` 语言、缩放、8 个开关修改后立即生效，重启后保持。
- `AC-008-09` F7 不再打开旧面板，场景树中没有 `CommunityStatsFilter`。
- `AC-008-10` 设置页没有重复条目。

### 阶段 3：F8 可重绑定

预计 0.5 到 1 人日。

- [ ] 在 `ModConfig` 增加 `ContributionPanelHotkey`，JSON 键为 `contribution_panel_hotkey`，默认 `F8`。
- [ ] 在 `LoadOverrides` 读取该键；缺失或非法时使用 `F8`。
- [ ] 在 `SaveSettings` 写入该键。
- [ ] 在设置页增加 `AddKeyBinding` 条目。
- [ ] 用 `RuntimeHotkeyService.Register` 注册热键，稳定 ID 固定。
- [ ] 设置回调中调用 `handle.TryRebind`；成功后保存，失败时保留旧绑定并提示。
- [ ] 删除 `_f8Pressed` 和 `Input.IsKeyPressed` 轮询。
- [ ] 如果 `OnProcessFrame` 没有其他用途，删除该回调并取消订阅。
- [ ] 热键注册失败时回退到 `F8`，并在日志中记录。

验收：

- `AC-008-11` 默认 F8 可切换贡献面板。
- `AC-008-12` 改成 F9 后立即生效，F8 不再触发。
- `AC-008-13` 重启游戏后仍使用 F9。
- `AC-008-14` 文本输入框聚焦时热键不触发。
- `AC-008-15` 开发者控制台可见时热键不触发。
- `AC-008-16` 非法绑定被拒绝，旧绑定保持可用。

### 阶段 4：对局身份与数据隔离

预计 2 到 3 人日。

- [ ] 新增 `RunIdentity`，保存 `StartTime`、`Seed`、`RunKey`。
- [ ] 在四个 `RunManager.SetUp...` Postfix 中读取 `_startTime`。
- [ ] 将 `RunDataCollector.OnRunStart()` 拆成 `BeginNewRun(identity)` 与 `ResumeRun(identity)`。
- [ ] 新局只清空内存，不加载任何旧文件。
- [ ] 续局只加载当前 RunKey 的 `live.json`、`shop_purchases.json`、`shop_offers.json`。
- [ ] `ContributionPersistence` 改为接收 `RunIdentity`。
- [ ] `ShopPurchasePersistence` 与 `ShopOfferingPersistence` 改为接收 `RunIdentity`。
- [ ] `RunDataCollector.OnMetricsUpload` 使用 `run.StartTime` 与 seed。
- [ ] `AfterCreateRunHistoryEntry` 使用 `run.StartTime`。
- [ ] `RunHistoryAnalyzer.BuildSingleRunStats` 填充 `StartTime`。
- [ ] `RunHistoryStatsSection` 使用 `StartTime` 查找贡献。
- [ ] `AssembleFromCombats` 只扫描当前对局目录，不按 seed 全局扫描。
- [ ] `SaveCombat` 文件名加入战斗序号，避免同楼层覆盖。
- [ ] `PruneOldFiles` 按对局目录年龄清理；legacy 文件按修改时间清理。
- [ ] 新增 legacy 回退：新目录缺失时读取旧 `{seed}_*.json`，日志标记 `legacy`。
- [ ] 不自动移动、改名或删除旧文件。

验收：

- `AC-008-17` 同 seed 连开两局，生成两个不同的 StartTime 目录。
- `AC-008-18` 第二局启动时商店购买列表为空，不加载第一局记录。
- `AC-008-19` 第二局不加载第一局 live 状态。
- `AC-008-20` 两局 summary 来源数与购买数分别正确。
- `AC-008-21` 同 seed 的两条历史分别打开正确贡献图。
- `AC-008-22` 存档退出再继续，只恢复当前对局的 live 状态。
- `AC-008-23` 同局同楼层多次战斗不覆盖。
- `AC-008-24` 旧数据仍可回放，界面或日志有 legacy 标记。
- `AC-008-25` 游戏存档目录无本 Mod 新增文件。

### 阶段 5：验证、文档与发布

预计 2 到 3 人日。

- [x] 构建 v0.113.1 与 v0.111.1。
- [x] 在 v0.113.1 完成一局 AutoSlayer 标准模式。
- [x] 在 v0.113.1 完成同 seed 两局隔离测试。
- [ ] 在 v0.111.1 完成加载、设置页、F8、同 seed 隔离冒烟测试。
- [x] 运行生涯探针，确认历史数量和愚者入口不回归。
- [x] 保存完整日志到 `8.TEST/1.5.0/`，在 `TEST.md` 登记。
- [x] 保存测试过程文件到 `TMP/1.5.0/`，在 `TMP.md` 登记。
- [x] 更新 PRD 状态、SPEC 追溯、SDD 组件说明。
- [x] 更新 `7.ChangeLog/001-版本日志.md`、`9.SRC/CHANGELOG.md`、双语 README。
- [x] 更新 `10.OUTPUT/1.5.0/` 与 `OUTPUT.md`。
- [x] 清理游戏 `mods/` 中的测试部署，关闭游戏进程。
- [ ] 提交、打标签 `v1.5.0`、回填提交编号。

验收：

- `AC-008-26` 双版本构建 0 错误。
- `AC-008-27` v0.113.1 完整对局通过。
- `AC-008-28` v0.111.1 冒烟通过。
- `AC-008-29` 测试日志、探针输出、版本日志完整。
- `AC-008-30` 测试部署已移出游戏目录，进程已关闭。

## 7. 测试矩阵

| 编号 | 游戏 | 场景 | 预期 |
|---|---|---|---|
| T-01 | v0.113.1 | 带 RitsuLib 启动，不装 BaseLib | Mod 初始化成功 |
| T-02 | v0.113.1 | 主菜单打开设置 | 页面可见，条目完整 |
| T-03 | v0.113.1 | 跑局暂停打开设置 | 页面可见，修改生效 |
| T-04 | v0.113.1 | 战斗暂停打开设置 | 页面可见，修改生效 |
| T-05 | v0.113.1 | 默认 F8 | 贡献面板切换 |
| T-06 | v0.113.1 | 重绑定 F9，重启 | F9 生效，F8 失效 |
| T-07 | v0.113.1 | 文本输入、控制台 | 热键被抑制 |
| T-08 | v0.113.1 | 同 seed 两局 | 两个 StartTime 目录，无混用 |
| T-09 | v0.113.1 | 第二局启动 | 不加载第一局 shop/live |
| T-10 | v0.113.1 | 两局历史回放 | 各自对应正确贡献 |
| T-11 | v0.113.1 | 存档退出再继续 | 只恢复当前对局 |
| T-12 | v0.113.1 | 旧 seed 数据回放 | 可读，标记 legacy |
| T-13 | v0.113.1 | 检查游戏存档目录 | 无本 Mod 写入 |
| T-14 | v0.111.1 | 加载、设置、F8、同 seed | 通过 |
| T-15 | 双版本 | 输出与 manifest | 版本、依赖、DLL 一致 |

测试原则：

1. 先保存完整日志，再截取结论。
2. 测试失败时先保留现场日志，再重跑。
3. 不在开发中反复运行全套 E2E；阶段 5 统一执行。
4. RitsuLib 自身兼容性不作为本项目测试范围；只验证本 Mod 的 API 调用。
5. 如果设置页真实点击无法在无头模式完成，保留探针结果，最后请求人工验收。

## 8. 旧数据迁移说明

### 8.1 “自动迁移”指什么

这里的迁移只针对 Mod 自己生成的 `%AppData%/sts2_community_stats/contributions/` 文件。它不会迁移游戏存档，也不会修改游戏历史。

自动迁移的含义是：读取旧 `{seed}_*.json`，根据 seed 找到对应历史，再重命名或复制到 `{StartTime}_{seed}/`。

### 8.2 为什么默认不做

旧文件只有 seed、floor 和保存时间，没有 StartTime。同一个 seed 可能有多个对局。仅凭 seed 无法判断某个文件属于哪一局。

如果自动分配，可能把 A 局的战斗记到 B 局，造成比“混合”更隐蔽的错误。

### 8.3 采用方案

1. 1.5.0 只写新目录。
2. 历史回放先查新目录。
3. 新目录不存在时，回退到旧 `{seed}_*.json`。
4. 回退数据在日志中标记 `legacy`；界面可显示“旧版数据”提示。
5. 不移动、不改名、不删除旧文件。
6. 只有确认某个 seed 在历史中只有一个对局时，才可以提供可选的“安全复制到新目录”工具；该工具默认不执行，且保留旧文件。
7. 90 天清理规则继续生效；旧文件按修改时间清理。

### 8.4 数据备份

阶段 0 复制一份 `contributions/` 和 `mod_prefs.json` 到 `TMP/1.5.0/pre-migration-backup/`。备份只用于回滚和对比，不参与运行。

## 9. 风险与回滚

| 风险 | 影响 | 处理 |
|---|---|---|
| RitsuLib 目标版本无法从 NuGet 获取 | 无法构建 | 停止施工，汇报包源问题；不把本地 DLL 冒充正式依赖 |
| RitsuLib API 与参考源码不一致 | 编译或运行失败 | 固定版本；按实际包调整；不跨版本猜 API |
| 设置页重复或镜像冲突 | 玩家看到重复条目 | 使用唯一页面 ID；检查 Ritsu 的镜像源；测试主菜单与跑局内 |
| F8 与游戏或其他 Mod 冲突 | 热键不触发 | 默认 F8；提供重绑定；注册失败日志 |
| F7 移除造成操作习惯变化 | 玩家找不到设置 | README、更新日志、Ritsu 设置入口说明 |
| 旧数据无法自动归属 | 旧历史显示混合数据 | legacy 回退并标记；不破坏性迁移 |
| StartTime 读取失败 | 无法建立新目录 | 记录警告；回退 legacy；不静默使用当前时间 |
| 新目录方案导致 1.4.4 看不到新数据 | 回滚后历史缺失 | 发布前备份；文档说明；保留 1.4.4 输出 |
| 误用 Ritsu `RunSavedData` | 写入游戏存档 | 代码审查明确禁止；测试检查存档目录 |
| 测试污染主存档 | 生涯数据变化 | 使用测试种子；测试前备份；测试后核对历史 |

回滚策略：

1. 施工在独立分支进行，保留 `v1.4.4` 标签与输出。
2. 如果 RitsuLib 集成失败，回到 1.4.4，不发布半成品。
3. 1.5.0 只新增数据目录，不删除旧目录；回滚不会破坏旧数据。
4. 不把新格式数据写回旧格式，避免污染 1.4.4。

## 10. 文档与版本

计划更新的文档：

- `1.PRD/001-项目范围与验收.md`：RQ-011、RQ-012。
- `1.PRD/PRD.md`：变更记录。
- `4.ADR/006-采用RitsuLib承载设置与热键.md`：依赖与边界。
- `6.SPEC/001-总体规范.md`：设置入口、F8、RunKey、legacy、禁止写存档。
- `6.SPEC/SPEC.md`：版本与追溯。
- `5.SDD/001-总体设计.md`、`5.SDD/002-采集与统计.md`、`5.SDD/004-界面层.md`：组件与数据流。
- `7.ChangeLog/001-版本日志.md`、`9.SRC/CHANGELOG.md`：1.5.0 记录。
- `9.SRC/README_ZH.md`、`9.SRC/README_EN.md`：RitsuLib 安装与 F7/F8 说明。
- `10.OUTPUT/1.5.0/` 与 `10.OUTPUT/OUTPUT.md`：产物登记。

版本规则：

- 目标版本 `1.5.0`，因为设置入口变化、F8 可重绑定、数据布局变化。
- RitsuLib 依赖版本在施工开始时确定；发版前检查一次更新。
- `manifest.json`、`ModConfig.ModVersion`、README、CHANGELOG、输出目录必须一致。

## 11. 完成标准

1. RitsuLib 作为硬依赖可构建、可加载；无 BaseLib。
2. F7 旧面板和轮询代码删除。
3. 设置页在三种宿主环境可用；语言、缩放、8 个开关持久化。
4. F8 默认可用，可重绑定，重启保持，文本输入和控制台抑制生效。
5. 同 seed 两局的数据、商店、live、summary 完全隔离。
6. 历史回放按 StartTime 找到正确对局。
7. 旧数据可读，标记 legacy，无破坏性迁移。
8. 游戏存档目录无本 Mod 写入。
9. v0.113.1 完整对局与 v0.111.1 冒烟通过。
10. 文档、测试、版本日志、输出产物齐全；测试部署清理；进程关闭。

## 12. 估算汇总

| 阶段 | 内容 | 估算 |
|---|---|---|
| 0 | 基线、备份、文档门禁 | 0.5 到 1 人日 |
| 1 | RitsuLib 依赖与构建 | 1 到 2 人日 |
| 2 | RitsuLib 设置页 | 1.5 到 2.5 人日 |
| 3 | F8 可重绑定 | 0.5 到 1 人日 |
| 4 | 对局身份与数据隔离 | 2 到 3 人日 |
| 5 | 验证、文档、发布 | 2 到 3 人日 |
| 合计 |  | 7.5 到 12.5 人日 |

估算不含上游 RitsuLib 或游戏版本本身的故障排查。若 RitsuLib 包源或 API 出现阻塞，按风险表停止并汇报。

## 13. 待确认

1. 施工开始时 RitsuLib 的最新稳定版号。当前参考为 0.6.13；本地 NuGet 缓存只有 0.5.15。
2. 设置页的真实点击与视觉验收是否需要人工完成。无头探针可以验证页面构造和绑定，但不能完全替代视觉检查。
3. 旧数据是否需要在 1.5.0 中提供“单历史安全复制”工具。默认不做自动迁移。

## 14. 施工记录

| 时间 | 内容 | 结果 |
|---|---|---|
| 2026-11-19 21:57 | 创建计划 | 未开始 |
| 2026-11-20 00:24 | 备份 1.4.4 用户数据；确认 RitsuLib 0.6.14 为最新稳定版并完成 NuGet 引用 | 完成 |
| 2026-11-20 00:25 | 新增 RunIdentity/RunStorage；贡献、商店、live、历史回放改用 StartTime+seed；删除 F7 自绘面板；接入 RitsuLib 设置页与可重绑定热键 | 代码完成 |
| 2026-11-20 01:10 | v0.113.1 设置页、绑定持久化、F9/F8 重绑定、同 seed 双局、历史回放与 legacy 回退通过 | v0.113.1 已验证；v0.111.1 待验证 |
