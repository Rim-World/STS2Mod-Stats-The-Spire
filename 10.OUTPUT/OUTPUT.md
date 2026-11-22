# 输出产物登记

本目录保存构建出的 Mod 成品。变更历史由 git 与 Release 提供。

| 时间 | 名称 | 版本 | 大小 | 说明 |
|---|---|---|---|---|
| 2026-11-23 01:53 | `sts2_statsthespire_local.dll` | 1.8.2 | 574,464 字节 | 双窗回放期间忽略贡献面板热键，避免隐藏右窗、留下左窗或重开实时面板；双窗关闭后 F8 正常开关实时面板；SHA-256 `2c151dec7342b4065254e392e8f7b11499e138d89ee63a1deab4ffb245a969b9`；v0.113.1 真实 49 层历史热键探针 PASS；双版本 Release 构建 0 错误；已安装并通过加载检查 |
| 2026-11-23 01:23 | `sts2_statsthespire_local.dll` | 1.8.1 | 574,464 字节 | 左窗删除关闭键；右窗关闭键同时关闭双窗；两窗均 680×900 并中心对称；右窗 `?`/`✕` 放大到 40×40 并提高对比度；左窗标题、外边距、分节内边距与表格列距调整；SHA-256 `12860ba0dd0b6c5931aa83ca2dac6945d1fdfe0476f68b9dc731fe3b161d554e`；v0.113.1 真实 49 层布局探针 PASS；双版本 Release 构建 0 错误；门禁 12/12；已安装并通过加载检查 |
| 2026-11-23 01:00 | `sts2_statsthespire_local.dll` | 1.8.0 | 573,440 字节 | 跑局历史「本局统计」改为左右双窗：左单局数据、右贡献图表；删除「查看贡献图表」按钮；左窗去掉重复标题并压缩间距，真实 49 层内容无纵向滚动；回放窗口固定且不污染实时面板布局；SHA-256 `39cd24133b6d6cbcf8e455c1a24c36b40c927ef9bb432908acae4d8d5c64d59b`；v0.113.1 布局探针 PASS；双版本 Release 构建 0 错误；已安装并通过加载检查 |
| 2026-11-23 00:10 | `sts2_statsthespire_local.dll` | 1.7.3 | 572,928 字节 | 修复历史回放 DPS 使用 `.run` 回合数；`EnergyNextTurnPower` 按每次施加来源分摊；`VAKUU`/`PAEL` 等先古事件名称回退 `ancients` 表；SHA-256 `7749d2e3549a270b58ea9b10882d620a6f6a15ac8fef9168b0acbd14181d7e56`；v0.113.1 定向探针 4/4；双版本 Release 构建 0 错误 |
| 2026-11-22 21:57 | `sts2_statsthespire_local.dll` | 1.7.2 | 571,392 字节 | 修复 `FoolMelodyReplacer` 替换愚者旋律标题时的占位符显示：`NameLookup` 先经 SmartFormat 解析，再转纯文本；中英文旋律短名跟随 Mod 语言；SHA-256 `7047d66f744c11c60dddbbe71d6f05b550b410d2d563b3c8065785b66b277457`；v0.113.1 CN/EN 探针各 7/7、Release 包 14/14；双版本 Release 构建 0 错误；已安装并通过 1.7.2 加载检查 |
| 2026-11-22 19:46 | `sts2_statsthespire_local.dll` | 1.7.1 | 569,856 字节 | 修复地图悬停面板：`MouseExited` 与 `NMapScreen._Input` 在鼠标离开时清理；连续悬停只保留当前面板；SHA-256 `6b9af528fd5c1429dfdd731281fcf076258d02b46a223cbb4d62fc756cc88f6c`；v0.113.1 探针 4/4 PASS；双版本 Release 构建 0 错误；已安装并通过 1.7.1 加载检查 |
| 2026-11-22 08:58 | `sts2_statsthespire_local.dll` | 1.7.0 | 568,320 字节 | 新增 `PetAbsorbed` 与 `PetWasted`：奥斯提/瓦皮 HP 吸收独立统计，碎骨/牺牲剩余 HP 损耗独立显示；旧 `osty` 数据迁移；修复非防御分节行回归、实际 Osty HP、损耗条形比例、独立自伤来源名与生成卡损耗可见性；SHA-256 `f25f0778eb842c675f1aa72e9da7cda4b4270a58ea5600a88c613c32e5140e85`；双版本 Release 构建 0 错误；v0.113.1 游戏内探针 51/51 PASS（实际治疗后 HP、Wuppy、碎骨/牺牲、UI 与子条比例） |
| 2026-11-22 07:28 | `sts2_statsthespire_local.dll` | 1.6.1 | 564,736 字节 | 无奖励事件战斗保存兜底、敌方宠物伤害过滤、Ennui 额外手抽记录；SHA-256 `b3e2f74ff6191ac43ddc8cec1d8b0813b5a44c5918d137fce04c356e55b2ee15`；v0.113.1 Fool 注入局与标准局 `UNTRACKED`=0；v0.113.1 与 v0.111.1 Release 构建 0 错误；安装后 v1.6.1 加载通过 |
| 2026-11-22 02:00 | `sts2_statsthespire_local.dll` | 1.6.0 | 563,200 字节 | P0/P1 归因修复、16 张愚者能力牌与 15 个愚者遗物运行时覆盖、卡牌/遗物/能力上下文保存恢复、多来源格挡拆分、卡牌上下文、RitsuLib 硬依赖；SHA-256 `c206f9dd00a30384f7142525a52d37024cb3c82072577e95104d11f54e44be9e`；v0.113.1 标准完整局、愚者实机与 18/18+15/15 上下文 PASS；v0.111.1 定向探针 10/10；双版本构建 0 错误 |
| 2026-11-21 07:15 | `sts2_statsthespire_local.dll` | 1.5.2 | 519,168 字节 | PaleBlueDot 来源捕获、补丁清理、UI 生命周期、原子写与写盘防抖、生成卡重复计数、Boss 白兽雕像、路径购买、死亡来源、全局 bundle、启动降级与隐私构建；SHA-256 `7b8aedcc1e83f364ba4b176bb1b37db734bf45b05a6077380701306a18e8327b`；v0.113.1 回归探针 PASS；v0.111.1 完整实机与 PaleBlueDot PASS；双版本程序集构建 0 错误 |

注：1.6.0 包内 README 于 2026-11-22 02:26 补充许可证与授权段；DLL、`manifest.json`、`settings.cfg` 未变。

注：1.7.0 输出包于 2026-11-22 19:10 加入 MIT `LICENSE`，README 改为 MIT 表述；DLL、`manifest.json` 与 `settings.cfg` 未变。

注：1.7.1 输出包包含 MIT `LICENSE`、1.7.1 README、`manifest.json` 与 `settings.cfg`；源码版本、DLL 版本和输出目录一致。

注：1.7.2 输出包包含 MIT `LICENSE`、1.7.2 README、`manifest.json` 与 `settings.cfg`；无头加载一次 `v1.7.2 initializing`；用户实机视觉未做。

注：1.7.3 输出包包含 MIT `LICENSE`、1.7.3 README、`manifest.json` 与 `settings.cfg`；Release 包探针 4/4 PASS；已安装，安装目录 DLL 哈希与输出包一致；用户实机视觉未做。

注：1.8.2 输出包包含 MIT `LICENSE`、1.8.2 README、`manifest.json`、`settings.cfg` 与 DLL；v0.113.1 热键探针 PASS；已安装，安装目录 DLL 哈希与输出包一致；用户物理 F8 未验证。

注：1.8.1 输出包包含 MIT `LICENSE`、1.8.1 README、`manifest.json`、`settings.cfg` 与 DLL；v0.113.1 布局探针 PASS；门禁 12/12；已安装，安装目录 DLL 哈希与输出包一致；用户实机视觉未做。

注：1.8.0 输出包包含 MIT `LICENSE`、1.8.0 README、`manifest.json` 与 `settings.cfg`；Release 包布局探针 PASS；已安装，安装目录 DLL 哈希与输出包一致；用户实机视觉未做。

注：1.4.4、1.5.0、1.5.1 的二进制包含构建机源码路径，已从当前目录与重写后 Git 历史移除；用户决定 GitHub 旧对象等待平台自动清除；备份在 `TMP/1.5.2/privacy-backup/old-output/`。1.4.0 至 1.4.3 的历史归档仍在各自 `TMP` 版本目录。

## 目录约定

- `10.OUTPUT/<version>/manifest.json`
- `10.OUTPUT/<version>/sts2_statsthespire_local.dll`
- `10.OUTPUT/<version>/README_ZH.md`
- `10.OUTPUT/<version>/README_EN.md`
- `10.OUTPUT/<version>/LICENSE`
- `10.OUTPUT/<version>/settings.cfg`
