# Changelog

本文件遵循 Keep a Changelog。项目级版本日志见 `../7.ChangeLog/`。

## [1.4.2] - 2026-11-19

### Fixed

- 商店库存读取改为运行时探测：v0.111 使用 `MerchantRoom.GetLocalInventory`，v0.113 使用 `RunManager.MerchantSynchronizer`。
- 源码同时通过 v0.111.1 与 v0.113.1 游戏程序集编译。

### Changed

- 版本号升级到 `1.4.2`。

## [1.4.1] - 2026-11-19

### Added

- 生涯统计角色筛选加入「愚者」；角色列表改为运行时读取 `ModelDb.AllCharacters`。
- 愚者卡牌、遗物与角色本地化名称。
- v0.113 的「恐惧」「极速」对局模式筛选。
- 个人 fork 维护分支源码。

### Changed

- 角色筛选只统计本地玩家为所选角色的历史。
- 对局模式按 `GameMode` 名称匹配，不再按枚举数值匹配。
- 卡牌生成钩子按运行时方法签名注册，移除 `STS2_GE_V105` 编译期分支。
- 版本号升级到 `1.4.1`。

### Fixed

- v0.111 的 `Custom=3` 与 v0.113 的 `Dread=3` 数值冲突。
- 英文界面未知角色显示原始 `char.*` 键。
- 卡牌生成来源补丁在 v0.113 注册失败。

## [1.4.0] - 2026-11-19

### Added

- 生涯统计对局模式筛选：全部、标准、自定义、每日、多人。
- 生涯统计每局平均用时、卡组大小、遗物数、卡牌类型与稀有度。
- 贡献面板拖动缩放与尺寸持久化。
- 药水掉落、卡牌掉落独立功能开关。
- 意图面板缩放设置。
- Fiddle、PaleBlueDot、VoidForm 贡献归因补丁。
- Neow 选项池动态补充。

### Changed

- 源码基线切换为公开仓库 `reddots1453/STS2mod-Stats_the_Spire`。
- 游戏程序集引用改为 `STS2GameDir` 构建属性。
- 版本号升级到 `1.4.0`。

### Removed

- 联网更新器、上传提示、历史导入、离线队列与服务器配置。

### Fixed

- 适配 Slay the Spire 2 v0.113.1：
  - `RunManager` 单人/多人启动方法改名。
  - `TemporaryStrengthPower.AfterSideTurnEnd`。
  - 商店库存改从 `MerchantSynchronizer` 读取。
  - 伤害修正方法新增 `CardPlay?` 参数。
  - `OutbreakPower` 改为运行时探测。

## [1.1.0] - 2026-05-08

### Added

- v0.105.0 Beta 兼容。
- 个人生涯统计「最近对局」筛选。

### Fixed

- Infused Core orb 值修饰器追踪。
- 荆棘/火焰屏障多源归因。
- Sword Sage Replay 1 后 FORGE:BASE。
- F7 功能开关持久化。

## [1.0.1] - 2026-05-06

### Fixed

- 怪物意图状态机面板残留。
- save/load 后平均每回合伤害数据失真。

## [1.0.0] - 2026-05-05

### Added

- 本地版初始发布：战斗贡献、生涯统计、意图状态机、商店价格、概率提示、功能开关、中英双语。

### Removed

- 社区版服务器通信、上传下载、社区统计对比。
