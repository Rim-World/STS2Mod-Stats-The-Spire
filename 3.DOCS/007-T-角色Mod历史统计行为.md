# 角色 Mod 历史统计行为

- 记录时间：2026-11-19 20:50
- 适用版本：Stats the Spire 1.4.4，Slay the Spire 2 v0.111.1
- 状态：代码路径已验证；未用真实角色 Mod 做启用/禁用实机对照

## 结论

角色 Mod 的历史数据由游戏保存，本 Mod 只读取。禁用角色 Mod 后，历史局仍然存在，但角色下拉不再显示该角色；该角色只能计入“全部”统计。角色名称、卡牌名称和遗物名称会回退为内部 ID。

效果与 v0.111 上的“愚者”相同。区别是：角色 Mod 重新启用后可以恢复入口和名称；“愚者”在 v0.111 上不能通过启用 Mod 恢复。

## 数据来源

游戏 `RunHistoryPlayer.Character` 是 `ModelId`，只保存分类和条目 ID，不保存角色对象引用：

```text
STS2-Reference/2.STS2-111Reference/decompiled/MegaCrit.Sts2.Core.Runs/RunHistoryPlayer.cs
[JsonPropertyName("character")]
public ModelId Character { get; init; } = ModelId.none;
```

因此加载历史文件不需要当前游戏加载对应角色 Mod。

本 Mod 的读取路径：

- `RunHistoryAnalyzer.BuildSnapshot` 调用 `SaveManager.LoadRunHistory`，按 `local.Character.Entry` 过滤。
- 角色筛选为 `null` 时，不按角色过滤，所有历史局进入“全部”统计。
- 角色下拉由当前 `ModelDb.AllCharacters` 构建，只包含当前已加载且 `IsPlayable` 的角色。

## 禁用角色 Mod 后

按当前代码路径推导：

1. 游戏仍能加载含该角色 ID 的历史文件。
2. `ModelDb.AllCharacters` 不再包含该角色。
3. 角色下拉不再显示该角色，无法选择角色专属统计。
4. “全部”统计仍包含该角色的历史局。
5. 历史详情页找不到角色模型，显示内部 ID，例如 `MYMOD_MYCHAR`。
6. 该角色专属卡牌和遗物仍按历史文件中的 ID 进入卡牌/遗物统计，名称回退为内部 ID。
7. `ComputeDeckAverages` 只统计 `ModelDb.GetByIdOrNull<CardModel>` 能找到的卡牌。缺失的角色 Mod 卡牌会计入卡组张数，但不计入攻击/技能/能力数量和稀有度平均值。

## 重新启用角色 Mod 后

角色模型回到 `ModelDb.AllCharacters` 后：

- 角色下拉重新显示该角色。
- 旧历史的 `Character.Entry` 与新模型 ID 相同时，角色筛选重新匹配。
- 角色、卡牌、遗物名称重新使用模型或本地化名称。
- 卡组类型与稀有度平均值重新计入这些卡牌。

## 角色 Mod 改 ID 或删除角色

- 旧历史仍保留旧 ID。
- 新角色使用新 ID 时，旧历史只进入“全部”统计，不能被新角色筛选命中。
- 旧 ID 的名称回退为内部 ID。
- 角色 Mod 作者必须保持角色 ID 稳定，才能让历史统计连续。

## 与愚者的对比

| 项目 | v0.111 的愚者 | 禁用的角色 Mod |
|---|---|---|
| 历史文件可读 | 是 | 是 |
| 角色下拉入口 | 无 | 无 |
| “全部”统计 | 包含 | 包含 |
| 名称显示 | 内部 ID | 内部 ID |
| 恢复方式 | 切换到 v0.113 | 重新启用 Mod |
| 卡牌类型/稀有度平均值 | 缺失模型不计入 | 缺失模型不计入 |

## 证据

- `STS2-Reference/2.STS2-111Reference/decompiled/MegaCrit.Sts2.Core.Runs/RunHistoryPlayer.cs:8-15`
- `9.SRC/src/Collection/RunHistoryAnalyzer.cs:198-203`
- `9.SRC/src/UI/CareerStatsSection.cs:432-462`（`GetPlayableCharacters` 当前模型列表）
- `9.SRC/src/UI/RunHistoryStatsSection.cs:124-149`（角色名回退）
- `9.SRC/src/Collection/RunHistoryAnalyzer.cs:327-347`（缺失卡牌模型跳过类型/稀有度）
- `9.SRC/src/Collection/RunHistoryAnalyzer.cs:807-968`（卡牌统计按历史 ID）
- `3.DOCS/005-R-v0.111.1全面实机测试.md`（v0.111 愚者探针结果）

## 未验证

未用真实角色 Mod 执行“启用 → 产生历史 → 禁用 → 重启 → 查看统计 → 重新启用”的实机流程。上述行为来自 v0.111 游戏模型与当前 Mod 代码路径推导。确认方法：安装一个带稳定角色 ID 的角色 Mod，完成一局后禁用并重启，检查角色下拉、全部统计、历史详情名称和卡组平均值。
