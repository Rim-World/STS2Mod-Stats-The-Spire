# Stats the Spire 维护项目

本仓库维护 Slay the Spire 2 的本地统计 Mod「Stats the Spire」。源码基线来自作者公开仓库，适配游戏 v0.111.1 与 v0.113.1。本 Mod 代码不发起网络请求；硬依赖 RitsuLib 自身可能发送遥测。

## 文档导航

| 目录 | 内容 |
|---|---|
| `1.PRD/` | 需求、范围、验收 |
| `2.PLANS/` | 施工计划 |
| `3.DOCS/` | 调查结论、测试报告与证据 |
| `4.ADR/` | 技术取舍记录 |
| `5.SDD/` | 系统内部设计 |
| `6.SPEC/` | 对外规范 |
| `7.ChangeLog/` | 版本开发日志 |
| `8.TEST/` | 测试工具、日志与报告 |
| `9.SRC/` | 源码、构建文件与源码说明 |
| `10.OUTPUT/` | 构建产物 |
| `TMP/` | 临时文件登记 |
| `TOOLS/` | 工具登记 |

## 快速构建

```bash
dotnet build 9.SRC/sts2_statsthespire_local.csproj -c Debug
```

详细说明见 `9.SRC/README.md`。

## 当前状态

- 源码已整理到 `9.SRC`，版本 1.5.1。
- v0.113.1：RitsuLib 设置页、配置绑定、F9/F8 热键重绑定、同 seed 双局隔离、历史回放与 legacy 回退通过。
- 同一 DLL 目标兼容 v0.111.1 与 v0.113.1；源码已通过两版程序集编译。
- v0.111.1：设置页、F8/F9 重绑定、放弃/胜利收尾与同 seed 隔离通过。
- 测试部署已清理；设置页暂停入口、F8 物理按键与重启持久化待人工确认。
- 本 Mod 代码不发起网络请求；RitsuLib 自身可能向作者发送遥测。
- 1.5.1 保留原版 90 天清理；补上 legacy 续局商店购买复制、设置损坏日志与 manifest 最低版本修正。v0.113.1 兼容探针通过，v0.111.1 程序集构建通过。
- 构建产物在 `10.OUTPUT/1.5.1/`。

## 来源与授权

源码基线：`https://github.com/reddots1453/STS2mod-Stats_the_Spire`。原仓库未声明许可证；公开发布前必须确认作者授权。
