# 回放 DPS、能量分摊与先古名称修正

- 计划编号：027-S
- 建立时间：2026-11-23 00:04
- 状态：已完成（v0.113.1 探针 4/4；双版本构建与安装；视觉与 v111 见 `033-M`）
- 状态更新：2026-11-23 04:10 旧状态收口；视觉与 v0.111.1 转入 AC-033-12、AC-033-13。
- 适用 Mod：1.7.3
- 游戏基准：v0.113.1（提交 `d09d48e5`），v0.111.1 用参考程序集构建

## 1. 目标

- 历史记录回放的「每回合平均伤害」显示数值。
- 「下回合获得能量」按每次施加来源分摊，不再由最后一张牌吞掉。
- 先古事件来源显示中文/英文官方名称。

## 2. 非目标

- 不重算已经写入磁盘的旧能量统计。
- 不改变统计字段、JSON schema、存档结构。
- 不改变普通事件、卡牌、遗物和药水名称。

## 3. 施工内容

- `9.SRC/src/UI/RunHistoryStatsSection.cs`：从 `.run` 的 `MapPointHistory` 汇总 `TurnsTaken`，传给回放面板。
- `9.SRC/src/UI/ContributionPanel.cs`：回放模式使用持久化伤害与本局回合数计算 DPS，不再读取空的实时 `CombatTracker`。
- `9.SRC/src/Collection/CombatTracker.cs`：为 `EnergyNextTurnPower` 建立按施加顺序的来源层，结算时按层消费。
- `9.SRC/src/Patches/CombatHistoryPatch.cs`：只记录本地玩家的能量层。
- `9.SRC/src/Util/NameLookup.cs`：`Event` 查不到 `events` 时回退 `Ancient`，覆盖 `VAKUU`、`PAEL` 等先古事件。

## 4. 验证

- `8.TEST/1.7.3/IssueProbe/`：4/4 PASS。
  - `WAH_KI=1`、`KI_REST=5`，合计 6。
  - 中文 `VAKUU=瓦库`、`PAEL=佩尔`；英文 `Vakuu`、`Pael`。
  - 历史回放 4118 伤害 / 114 回合，显示 `36.1`。
- 日志：`8.TEST/1.7.3/issue-probe-2-stdout.log`，`GAME_RC=0`。
- v0.113.1 与 v0.111.1 Release 构建：0 错误、7 个既有可空警告；两版 DLL SHA-256 均为 `7749d2e3549a270b58ea9b10882d620a6f6a15ac8fef9168b0acbd14181d7e56`。
- 发布门禁 12/12 PASS；1.7.3 已安装，安装目录 DLL 哈希与输出包一致。
- 未验证：用户实机视觉；v0.111.1 运行。

## 5. 施工记录

- 2026-11-23 00:04：建立计划；完成代码与 v0.113.1 探针 4/4。
- 2026-11-23 00:10：双版本 Release 构建、输出包与门禁完成。
- 2026-11-23 00:11：安装 1.7.3；Release 包探针 4/4；移除探针。
- 未完成：用户实机视觉。
