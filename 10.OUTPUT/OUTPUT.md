# 输出产物登记

本目录保存构建出的 Mod 成品。变更历史由 git 与 Release 提供。

| 时间 | 名称 | 版本 | 大小 | 说明 |
|---|---|---|---|---|
| 2026-11-21 07:15 | `sts2_statsthespire_local.dll` | 1.5.2 | 518,656 字节 | PaleBlueDot 来源捕获、补丁清理、UI 生命周期、原子写与写盘防抖、生成卡重复计数、Boss 白兽雕像、路径购买、死亡来源、全局 bundle、启动降级与隐私构建；SHA-256 `7e8144041bfe649b4dfc5a810623df9c9cb3453bc9bbe62e8a0528edb3c3dec0`；v0.113.1 回归探针 PASS；双版本程序集构建 0 错误 |

注：1.4.4、1.5.0、1.5.1 的二进制包含构建机源码路径，已从当前目录与 Git 历史移除，备份在 `TMP/1.5.2/privacy-backup/old-output/`。1.4.0 至 1.4.3 的历史归档仍在各自 `TMP` 版本目录。

## 目录约定

- `10.OUTPUT/<version>/manifest.json`
- `10.OUTPUT/<version>/sts2_statsthespire_local.dll`
- `10.OUTPUT/<version>/README_ZH.md`
- `10.OUTPUT/<version>/README_EN.md`
- `10.OUTPUT/<version>/settings.cfg`
