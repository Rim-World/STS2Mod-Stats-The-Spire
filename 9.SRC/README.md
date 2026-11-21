# Stats the Spire（本地维护版）

Slay the Spire 2 的纯离线统计 Mod。显示战斗贡献、个人生涯统计、怪物意图状态机、药水/卡牌掉落概率、问号房间概率与商店价格。

本仓库是基于作者公开源码的维护分支。原仓库与本维护仓库都未声明许可证。同一 DLL 目标兼容游戏 v0.111.1 与 v0.113.1。本 Mod 代码不联网、不上传数据、不修改游戏平衡；硬依赖 RitsuLib 自身可能发送遥测。

## 功能

- 战斗结束后显示卡牌、遗物、药水、能力的伤害/格挡/抽牌贡献。
- 个人生涯统计：胜率、死亡原因、路线、先古选项、Boss、卡牌与遗物。
- 生涯筛选：角色（含愚者）、进阶、最近对局数、对局模式。
- 怪物意图状态机悬停图。
- 顶栏药水掉落与卡牌稀有度概率。
- 问号房间遭遇概率与商店价格提示。
- RitsuLib 设置页；贡献面板默认 F8，可重绑定。

## 构建

前置条件：

- .NET SDK 9.0
- Slay the Spire 2 安装目录
- 游戏自带 `data_sts2_windows_x86_64` 下的 `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll`
- NuGet 包 `STS2.RitsuLib 0.6.14`（`dotnet restore` 自动获取）

```bash
dotnet build sts2_statsthespire_local.csproj -c Debug -p:STS2GameDir="<Slay the Spire 2 安装目录>"
```

`STS2GameDir` 必须指向包含 `data_sts2_windows_x86_64` 的游戏安装目录。

输出：`bin/Debug/sts2_statsthespire_local.dll`。打包时把 DLL 与 `manifest.json` 放在同一目录。

## 安装

先把完整的 `STS2-RitsuLib` 文件夹放入游戏的 `mods/`。只复制根 DLL 不足以加载设置页。

再把以下文件放入游戏的 `mods/sts2_statsthespire_local/`：

- `manifest.json`
- `sts2_statsthespire_local.dll`
- `settings.cfg`
- `README_ZH.md`、`README_EN.md`（可选）

启动游戏后，游戏按 manifest 依赖顺序先加载 RitsuLib，再加载本 Mod。

## 数据目录

`%AppData%/sts2_community_stats/`

- `contributions/{StartTime}_{seed}/`：每局独立的单场、汇总、中途快照和商店数据。
- `contributions/{seed}_*.json`：1.4.4 及更早的只读回退数据；不会自动清理或改名。
- `cache/`：生涯统计缓存。
- `mod_prefs.json`：用户设置。

## 兼容性

- 单一构建：同一个 `sts2_statsthespire_local.dll` 目标兼容 v0.111.1 与 v0.113.1。
- 源码已通过两个版本的 `sts2.dll` 编译。
- 1.4.4 已在 v0.113.1 与 v0.111.1 完成实机验证。
- 1.5.0 的 RitsuLib 设置页、热键、放弃/胜利收尾和同 seed 隔离已在 v0.111.1 与 v0.113.1 验证。
- 1.5.1 补上 legacy 续局商店购买复制、设置损坏日志与 `min_game_version` 修正；源码通过两版程序集构建。
- 1.5.2 删除四个无消费者补丁与 PaleBlueDot 错误钩子，修复 PaleBlueDot 来源捕获、UI 生命周期、原子写、损坏隔离、写盘防抖、生成卡重复计数、Boss 白兽雕像、路径购买、死亡来源与角色筛选 bundle；发布构建不嵌入本机源码路径。
- 1.6.0 修复战斗归因并补齐愚者内容；v0.113.1 完成标准完整局、愚者实机与上下文检查；v0.111.1 完成定向探针 10/10。
- 不承诺与原 Workshop 包同时安装。

## 许可证与来源

源码基线来自 `https://github.com/reddots1453/STS2mod-Stats_the_Spire`，原作者为 `reddots1453`。原仓库与本维护仓库都未声明许可证。在取得作者明确授权前，不得公开再分发本构建或衍生源码。本节只记录授权状态，不构成授权。
