# 输出产物登记

本目录保存构建出的 Mod 成品。变更历史由 git 与 Release 提供。

| 时间 | 名称 | 版本 | 大小 | 说明 |
|---|---|---|---|---|
| 2026-11-28 21:02 | `sts2_statsthespire_local.dll` | 1.13.1 | 570,880 字节 | 删除失落鲁特琴重复能力上下文，`FoolContentPatch` 唯一注册；探针精确计数、真实来源、手动愚者/倾泻、数值与保存重载；v0.114.0 AutoSourceProbe 11/11 PASS；v0.113.1 主 Mod 加载、探针受环境阻塞；v0.111.1 仅构建；SHA-256 `9b5fb559fa20a71955048985010f5d3c4bc7f8330869f7a5fb12892c211a3d6c`；三版本 Release 构建 0 错误且哈希一致；安装后一次 `v1.13.1 initializing`、826 条绑定、458 个目标 |
| 2026-11-28 19:55 | `sts2_statsthespire_local.dll` | 1.13.0 | 571,392 字节 | 历史课复制牌归 `HISTORY_COURSE` 遗物行；地狱狂徒、模仿学习、失落鲁特琴建能力来源，缺失时回退到对应卡牌；精炼混沌归药水行；注能归被附魔卡牌；彼岸咆哮归卡牌自身；奇巧不拆分；SHA-256 `610ccba21a0b5590963edb94a4697ca5e0b614e8b11cfa23817e4073623aacc7`；v0.114.0 AutoSourceProbe 7/7、三版本 Release 构建 0 错误且哈希一致；安装后一次 `v1.13.0 initializing`、828 条绑定、458 个目标；v0.113.1/v0.111.1 运行与模仿学习单人触发未验证 |
| 2026-11-28 19:08 | `sts2_statsthespire_local.dll` | 1.12.0 | 570,368 字节 | 低语耳环 `AfterAutoPrePlayPhaseEnteredLate` 建立 `WHISPERING_EARRING` 遗物根来源；首回合自动牌数值归遗物行，每张自动牌增加 `AutoPlayedCount`，不建自动牌子行；+1 能量不重复；SHA-256 `f4129eb1990ac0f14fd91d1d27c648cf81a2287093a77d37dcb82b38cc8ab7c2`；v0.114.0 EarringProbe PASS（auto=2、伤害 32、格挡 5、能量 1）；三版本 Release 构建 0 错误且哈希一致；安装后一次 `v1.12.0 initializing`、820 条绑定、455 个目标；v0.113.1/v0.111.1 运行与 UI 未验证 |
| 2026-11-28 00:32 | `sts2_statsthespire_local.dll` | 1.11.0 | 570,368 字节 | 自动出牌按根来源汇总：乱战/惊逃/傻瓜自动牌的伤害、修正伤害、升级伤害、格挡、抽牌、能量、星星、治疗、自伤与宠物数值写入来源牌行；`TimesPlayed` 只算手动；新增 `AutoPlayedCount`；自动牌不再单独成行；惊逃补上下文；实际 `SIMPLETON_POWER` 映射到 `SIMPLEON`；SHA-256 `c5d6de8337bb452eb8b4351806d695266f40eff4ed25b5f15f73a39268807c0a`；三版本 Release 构建 0 错误且哈希一致；v0.114.0 定向 5/5、live JSON PASS；v0.113.1/v0.111.1 运行与 UI 视觉未验证 |
| 2026-11-27 03:16 | `sts2_statsthespire_local.dll` | 1.10.0 | 566,272 字节 | 适配 v0.114.0 与 RitsuLib 0.6.16；HappyFlower/FakeHappyFlower 改用 `AfterPlayerTurnStartEarly`，TunePower 运行时选择 `AfterSideTurnEnd`/`AfterCardPlayed`；SHA-256 `933dc72a2c29f9f50908716a0e8d336ae4a862c7d31b1827cfd1bb2b3b97e46b`；v0.111.1、v0.113.1、v0.114.0 Release 构建 0 错误且 DLL 哈希一致；v0.114.0 隔离加载 816 条绑定、453 个目标、Fool 34/34；标准 AutoSlayer 种子 71401 victory、3/3 幕、49 层、28 场战斗 |
| 2026-11-25 08:10 | `sts2_statsthespire_local.dll` | 1.9.6 | 565,248 字节 | 修正 1.9.5 的「吃不完的糖」奇偶：奖励先填充，`CombatRewardsSeen` 后递增；当前计数为奇数才是 4 张；SHA-256 `14b460b54f15ed98d63885cff85efd7f661b95b7e3f454ba683d76a4cbf70f16`；双版本 Release 构建 0 错误；纯计算 16/16、v0.113.1 UI 探针与隔离主 Mod 加载 PASS；真实糖果局 UI、v0.111.1 运行与视觉未做 |
| 2026-11-25 08:02 | `sts2_statsthespire_local.dll` | 1.9.5 | 565,248 字节 | 已废止：顶栏卡牌掉落改为「下一组基础卡牌奖励」稀有数量分布；但「吃不完的糖」3/4 张奇偶预测相反，已由 1.9.6 修正；SHA-256 `08a22766be47f8a614510b5be2e5a8a75fbd637084ee17ba1763753793e023e0`；纯计算/UI 探针通过但未覆盖真实糖果时序 |
| 2026-11-25 05:11 | `sts2_statsthespire_local.dll` | 1.9.4 | 553,472 字节 | 独立审计修复：Godot `user://` 路径转 OS 路径；读取全部三个 profile 的当前存档；仅删除 `{StartTime}_{seed}` Run 目录；未知目录保留；根与嵌套 reparse 预扫描；路径必须为绝对 OS 路径；SHA-256 `9387ef9c177d1c6d25510551a794a683638d95745d4d1e75094b93b030a4fa92`；双版本 Release 构建 0 错误；门禁 12/12；v0.113.1 真实 save 探针 PASS；已安装 1.9.4 并加载；v0.111.1 未运行 |
| 2026-11-25 04:34 | `sts2_statsthespire_local.dll` | 1.9.3 | 551,936 字节 | 恢复默认 90 天清理；新增 RitsuLib「自动清理」开关（`auto_cleanup`，默认开启）；保护 `current_run.save`/`current_run_mp.save` 引用的对局；读取失败跳过；跳过 reparse point；SHA-256 `f9d8362ea6c18a291d67fa7bdf3e096607efbd8b35d9700381429ecd0d2e971c`；v0.111.1 清理探针 PASS；双版本 Release 构建 0 错误；门禁 12/12；已安装并通过加载检查；v0.113.1 未运行；后续独立审计判定当前存档保护路径失效 |
| 2026-11-25 03:56 | `sts2_statsthespire_local.dll` | 1.9.2 | 519,168 字节 | 历史入口按钮宽度从 390 收窄到 350，高度 75、字号 33、位置 `(30,210)` 不变；点击仍打开左右双窗；SHA-256 `6ed5214993ccf3ad0d0e68612cbb6e734cb77dc9dfe67965cd061b1490b1f3cf`；v0.111.1 按钮探针 PASS；双版本 Release 构建 0 错误；已安装并通过加载检查 |
| 2026-11-25 03:45 | `sts2_statsthespire_local.dll` | 1.9.1 | 519,168 字节 | 历史记录页「本局统计」入口按钮从 `260×50`、字号 22 放大到 `390×75`、字号 33，位置保持 `(30,210)`；点击仍打开左右双窗；SHA-256 `699d6a9108fc298c69747ad4090157e931d7058b86fa5d4941b9d83743c8df11`；v0.111.1 按钮探针 PASS；双版本 Release 构建 0 错误；已安装并通过加载检查 |
| 2026-11-23 03:18 | `sts2_statsthespire_local.dll` | 1.9.0 | 518,656 字节 | Mod 界面自动跟随游戏语言；`zhs`/`zht` 使用简体中文 UI，其他语言使用英文 UI；游戏术语名称只读当前官方词表，删除英文优先与英文兜底；删除 RitsuLib 语言项与旧 `language` 读写；SHA-256 `a6ffcd0b03db6f017be57341d75966c17cec17c48ea9ba8fb807fe4667fd06fb`；四种语言探针通过；双版本 Release 构建 0 错误；门禁 12/12；已安装并通过加载检查 |
| 2026-11-23 02:21 | `sts2_statsthespire_local.dll` | 1.8.3 | 574,464 字节 | 默认贡献面板热键改为 `Ctrl+F8`，降低与单键 F8 模组冲突；非法绑定回退同步；本地设置只迁移 hotkey，其他设置保持；SHA-256 `25d96a33077dabc9bca32ae7953ebf5cc585099537f7f0edfe09b7112e517b93`；v0.113.1 启动注册 `Ctrl+F8`；双版本 Release 构建 0 错误；已安装并通过加载检查 |
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

注：1.8.3 输出包包含 MIT `LICENSE`、1.8.3 README、`manifest.json`、`settings.cfg` 与 DLL；v0.113.1 注册 `Ctrl+F8`；已安装，安装目录 DLL 哈希与输出包一致；用户物理 `Ctrl+F8` 未验证。

注：1.8.2 输出包包含 MIT `LICENSE`、1.8.2 README、`manifest.json`、`settings.cfg` 与 DLL；v0.113.1 热键探针 PASS；已安装，安装目录 DLL 哈希与输出包一致；用户物理 F8 未验证。

注：1.8.1 输出包包含 MIT `LICENSE`、1.8.1 README、`manifest.json`、`settings.cfg` 与 DLL；v0.113.1 布局探针 PASS；门禁 12/12；已安装，安装目录 DLL 哈希与输出包一致；用户实机视觉未做。

注：1.9.0 输出包包含 MIT `LICENSE`、1.9.0 README、`manifest.json`、`settings.cfg` 与 DLL；四种语言探针通过；最终加载一次 `v1.9.0 initializing`；已安装，安装目录 DLL 哈希与输出包一致；图形界面视觉与 v0.111.1 运行未做。

注：1.9.0 输出包于 2026-11-23 04:51 更新 `LICENSE` 与 README：加入上游作者 `reddots1453` 署名、上游未声明许可证说明，以及 MIT 仅覆盖本维护仓库新增与修改代码的范围；DLL、`manifest.json` 与 `settings.cfg` 未变。

注：1.8.0 输出包包含 MIT `LICENSE`、1.8.0 README、`manifest.json` 与 `settings.cfg`；Release 包布局探针 PASS；已安装，安装目录 DLL 哈希与输出包一致；用户实机视觉未做。

注：1.4.4、1.5.0、1.5.1 的二进制包含构建机源码路径，已从当前目录与重写后 Git 历史移除；用户决定 GitHub 旧对象等待平台自动清除；备份在 `TMP/1.5.2/privacy-backup/old-output/`。1.4.0 至 1.4.3 的历史归档仍在各自 `TMP` 版本目录。

## 目录约定

- `10.OUTPUT/<version>/manifest.json`
- `10.OUTPUT/<version>/sts2_statsthespire_local.dll`
- `10.OUTPUT/<version>/README_ZH.md`
- `10.OUTPUT/<version>/README_EN.md`
- `10.OUTPUT/<version>/LICENSE`
- `10.OUTPUT/<version>/settings.cfg`
