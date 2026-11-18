# Stats the Spire 维护项目

本仓库维护 Slay the Spire 2 的本地统计 Mod「Stats the Spire」。源码基线来自作者公开仓库，适配游戏 v0.113.1，保持纯离线。

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

- 源码已整理到 `9.SRC`，版本 1.4.1。
- v0.113.1 构建通过；无头加载、AutoSlayer、生涯统计、愚者筛选与 UI 探针通过。
- 同一 DLL 目标兼容 v0.111.1 与 v0.113.1；v0.111.1 实机验证未执行。
- 测试部署已清理；人工键位与视觉验收未完成。
- 构建产物在 `10.OUTPUT/1.4.1/`。

## 来源与授权

源码基线：`https://github.com/reddots1453/STS2mod-Stats_the_Spire`。原仓库未声明许可证；公开发布前必须确认作者授权。
