# 采用 RitsuLib 承载设置与热键

- 状态：已接受
- 日期：2026-11-19
- 项目版本：1.4.4（决策时）；目标版本 1.5.0
- 相关文件：`2.PLANS/008-M-RitsuLib迁移与对局身份修复.md`、`9.SRC/sts2_statsthespire_local.csproj`、`9.SRC/manifest.json`、`9.SRC/src/CommunityStatsMod.cs`、`9.SRC/src/UI/FilterPanel.cs`

## 背景

当前设置页是自绘 `FilterPanel`，由 F7 轮询打开。它承担语言选择、意图面板缩放和 8 个功能开关，约 285 行。F8 同样通过逐帧 `Input.IsKeyPressed` 轮询。

这种实现有三个问题：

1. 设置页、输入路由和持久化都是项目自己维护的通用层。
2. F7 与 F8 是固定键，无法让玩家重绑定。
3. 自绘面板挂在场景根节点，可能与其他 UI Mod 冲突。

RitsuLib 0.6.13 提供模组设置页、配置绑定、运行时热键和主菜单/跑局/战斗三种设置宿主。它的兼容目标包含 v0.111 与 v0.113。BaseLib 也能生成设置页，但 `ModConfigPatch` 在非主菜单会禁用入口，无法替代跑局内的 F7。

项目非目标明确禁止写入游戏存档。RitsuLib 的 `RunSavedData` 会把数据注入 run save，因此不能用于运行态或商店数据。

## 决策

采用 RitsuLib 作为硬依赖，承载设置页和运行时热键；不引入 BaseLib；不使用 RitsuLib `RunSavedData`；贡献、商店和运行态数据继续写入 `%AppData%/sts2_community_stats/contributions/`，并按 `StartTime + seed` 分目录。

## 备选方案

### 保留自绘 F7/F8

优点是零新依赖，部署简单。

缺点是需要继续维护设置 UI、输入轮询、焦点抑制、键位持久化和多版本兼容。玩家无法重绑定 F8。场景根节点污染仍然存在。

否决。

### BaseLib 设置页

优点是可以自动生成设置控件，配置注册简单。

缺点是跑局内禁用 Mod Config 入口。若删除 F7，玩家在对局中无法修改设置。要达到同等体验，仍需自己实现对局内入口，退回自绘方案的一部分。

否决。

### RitsuLib `RunSavedData`

优点是对局数据随存档自动隔离，保存和继续逻辑简单。

缺点是它通过补丁写入游戏 run save，违反项目非目标和 `SP-013`。这会改变“只读统计”的边界。

否决。

### 把战斗归因、历史、统计图全部迁移到基础库

基础库没有对应领域模型。战斗归因依赖 107 个补丁和现有上下文栈，重写会扩大回归面，不会明显减少代码。

否决。

## 后果

获益：

- 删除自绘设置页和逐帧输入轮询。
- 设置页在三种游戏上下文可用。
- F8 可以重绑定，获得文本输入和控制台抑制。
- 现有 `mod_prefs.json` 可以通过 callback binding 继续使用，不需要迁移配置格式。
- RitsuLib 的版本兼容由基础库维护，本项目只验证自己的调用。

代价与约束：

- 玩家必须安装完整的 `mods/STS2-RitsuLib/` 文件夹。
- 本项目必须持续跟进 RitsuLib 的最新稳定版，并在每次发版前重新构建。
- 设置页的真实视觉与交互需要实机验收。
- `manifest.json` 必须声明依赖；缺少 RitsuLib 时游戏不会加载本 Mod。
- 运行态数据不能使用 `RunSavedData`，必须继续维护外部对局目录。
- 1.5.0 的新数据目录对 1.4.4 不可见；回滚时只能读取旧目录。

下游规则：

1. 不在代码中复制 RitsuLib 程序集。
2. 不使用 RitsuLib 私有 API 或反射访问内部类型。
3. 不把 `RunSavedData` 用作统计缓存。
4. 设置页只通过 `RitsuLibFramework.RegisterModSettings` 注册。
5. 热键只通过 `RuntimeHotkeyService` 注册。
6. RitsuLib 版本升级后，先构建 v0.113.1 与 v0.111.1，再运行集成冒烟测试。
