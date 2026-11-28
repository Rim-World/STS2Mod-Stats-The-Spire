# RitsuLib 本地副本切换与低语耳环统计评估

- 编号：049-R
- 时间：2026-11-28 18:56
- 适用版本：Mod 1.11.0；游戏 v0.114.0；RitsuLib 0.6.16
- 结论：本地 RitsuLib 副本已移走，实际游戏已确认加载创意工坊版。低语耳环适合采用自动出牌根来源口径；当前代码尚未覆盖它，现状不是同口径。

## 1. 本地 RitsuLib 切换

原目录：`C:\Game\Steam\steamapps\common\Slay the Spire 2\mods\STS2-RitsuLib`。

原目录版本为 0.6.16，但清单要求 `min_game_version=0.114.0`，与创意工坊包的 `0.107.1` 不同。该副本会覆盖创意工坊版。

处理结果：

- 原目录已移动到 `TMP/1.11.0/removed-local-ritsulib-2026-11-28-1852/STS2-RitsuLib`，大小约 18 MB。
- 游戏 `mods` 目录中已不存在本地 `STS2-RitsuLib`。
- 没有删除文件。
- 用户设置未修改。

实际游戏加载使用 45 秒无头启动，日志：`8.TEST/1.11.0/workshop-ritsu-load-1853-stdout.log`。

关键证据：

- `Loading assembly DLL C:\Game\Steam\steamapps\workshop\content\2868840\3747602295\STS2-RitsuLib.dll`。
- `RitsuLib Version: 0.6.16 [compat branch: 0.114.0]`。
- `RitsuLib [STS2-RitsuLib] (0.6.16) workshopId=3747602295`。
- `Stats the Spire (Local) v1.11.0 initializing...`。
- `[RitsuSettings] registered settings page`。
- `Ctrl+F8` 注册成功。
- 没有 RitsuLib 的本地/Steam 重复警告。

游戏进程已结束。创意工坊版加载通过。

## 2. 低语耳环效果

来源：`5.STS2-114Reference/decompiled/MegaCrit.Sts2.Core.Models.Relics/WhisperingEarring.cs`。

- ID：`WHISPERING_EARRING`。
- 稀有度：先古遗物。
- 被动：每回合开始获得 1 点能量。
- 主动效果：第一回合的自动出牌前阶段结束时，Vakuu 从手牌中选择可打出的牌，最多自动打出 13 张。
- 实现位置：`AfterAutoPrePlayPhaseEnteredLate`，第 42–81 行。
- 每张牌通过 `CardCmd.AutoPlay(..., AutoPlayType.Default, ...)` 打出。

## 3. 是否适合同口径

适合。

现有口径的根来源是「发起自动出牌链的效果」。低语耳环发起整条第一回合自动出牌链，因此根来源应该是 `WHISPERING_EARRING`，类型为 `relic`。

该口径下：

- 自动打出的每张牌不建立自己的贡献行。
- 每张牌的伤害、修正伤害、格挡、抽牌、能量、星星、治疗、能力施加等，写入低语耳环行。
- 每张自动牌给低语耳环行增加 1 次 `AutoPlayedCount`。
- 手动嵌套出牌仍按原规则独立成行。
- 低语耳环每回合 +1 能量是另一条遗物效果，已由 `ModifyMaxEnergy` 扫描单独记录，不得重复计入自动出牌伤害行。

这和乱战、惊逃、傻瓜的「自动牌归根来源」是同一原则，区别只是根来源类型从卡牌变成遗物。

## 4. 当前代码状态

当前未实现该口径。

- `RelicHookContextPatcher` 的遗物列表没有 `WhisperingEarring`。
- `CombatTracker.ResolveAutoPlayRoot` 虽有遗物分支，但低语耳环 hook 执行期间没有设置 `_activeRelicId`。
- 因此当前自动打出的牌会走后备逻辑，把每张自动牌自己当作根来源，或落到卡牌来源，而不是低语耳环行。
- 旧审计已记录同一问题：`3.DOCS/021-R-战斗贡献补丁全面审计.md:1042` 与 `:1073` 的 C7-6、C10-6。

## 5. 建议实现

在 `RelicHookContextPatcher.PatchAll` 增加：

```csharp
TryPatch(harmony, typeof(WhisperingEarring),
    "AfterAutoPrePlayPhaseEnteredLate", prefix, postfix);
```

这样 `AfterAutoPrePlayPhaseEnteredLate` 的整个异步执行期间 `_activeRelicId=WHISPERING_EARRING`。`CardCmd.AutoPlay` 进入 `OnCardPlayStarted` 时，`ResolveAutoPlayRoot` 会返回遗物来源。

实现前需要验证两点：

1. 异步 hook 的 prefix/postfix 是否覆盖完整 `await` 链；如果 Harmony 只在方法入口生效，需要改为在自动出牌循环外显式设置根来源。
2. 该遗物的首回合自动出牌是否会被 `PaelsEye`、回合结束或战斗结束提前截断；计数必须按实际打出张数，而不是固定 13。

建议用定向探针验证：

- 给玩家添加低语耳环。
- 首回合手牌放入可打出的攻击、防御、抽牌、能量牌。
- 触发 `AfterAutoPrePlayPhaseEnteredLate`。
- 检查低语耳环行的 `AutoPlayedCount`、伤害、格挡、抽牌与能量。
- 检查没有自动牌自己的行。
- 检查 +1 能量没有重复计入自动出牌行。

## 6. 未做

- 未修改代码。
- 未实现低语耳环根来源。
- 未运行低语耳环定向探针。
- 未验证 v0.113.1 与 v0.111.1 运行；对应类和方法存在，但本机没有运行时。

## 7. 加载副作用

实际游戏加载同样触发 RitsuLib 0.6.16 的遥测注册与 Workshop 扫描；行为与 048-R 记录相同。主 Mod 没有网络请求。
