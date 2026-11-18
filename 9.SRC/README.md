# Stats the Spire（本地维护版）

Slay the Spire 2 的纯离线统计 Mod。显示战斗贡献、个人生涯统计、怪物意图状态机、药水/卡牌掉落概率、问号房间概率与商店价格。

本仓库是基于作者公开源码的维护分支。同一 DLL 目标兼容游戏 v0.111.1 与 v0.113.1。Mod 不联网、不上传数据、不修改游戏平衡。

## 功能

- 战斗结束后显示卡牌、遗物、药水、能力的伤害/格挡/抽牌贡献。
- 个人生涯统计：胜率、死亡原因、路线、先古选项、Boss、卡牌与遗物。
- 生涯筛选：角色（含愚者）、进阶、最近对局数、对局模式。
- 怪物意图状态机悬停图。
- 顶栏药水掉落与卡牌稀有度概率。
- 问号房间遭遇概率与商店价格提示。
- F7 设置面板，F8 贡献面板。

## 构建

前置条件：

- .NET SDK 9.0
- Slay the Spire 2 安装目录
- 游戏自带 `data_sts2_windows_x86_64` 下的 `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll`

```bash
dotnet build sts2_statsthespire_local.csproj -c Debug -p:STS2GameDir="C:\Game\Steam\steamapps\common\Slay the Spire 2"
```

默认 `STS2GameDir` 已指向本机 Steam 安装目录；换机器或换版本时用 `-p:STS2GameDir=...` 覆盖。

输出：`bin/Debug/sts2_statsthespire_local.dll`。打包时把 DLL 与 `manifest.json` 放在同一目录。

## 安装

把以下文件放入游戏的 `mods/sts2_statsthespire_local/`：

- `manifest.json`
- `sts2_statsthespire_local.dll`
- `settings.cfg`
- `README_ZH.md`、`README_EN.md`（可选）

启动游戏后，Mod 自动加载。

## 数据目录

`%AppData%/sts2_community_stats/`

- `contributions/`：单场、本局汇总与中途快照。
- `cache/`：生涯统计缓存。
- `mod_prefs.json`：用户设置。

## 兼容性

- 单一构建：同一个 `sts2_statsthespire_local.dll` 目标兼容 v0.111.1 与 v0.113.1。
- v0.113.1：构建与实机验证通过。
- v0.111.1：代码路径按运行时探测编写；实机验证未执行。
- 不承诺与原 Workshop 包同时安装。

## 许可证与来源

源码基线来自 `https://github.com/reddots1453/STS2mod-Stats_the_Spire`。原仓库未声明许可证。公开发布衍生版前必须确认作者授权。
