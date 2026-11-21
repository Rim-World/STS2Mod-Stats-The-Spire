# 输出产物登记

本目录保存构建出的 Mod 成品。变更历史由 git 与 Release 提供。

| 时间 | 名称 | 版本 | 大小 | 说明 |
|---|---|---|---|---|
| 2026-11-22 02:00 | `sts2_statsthespire_local.dll` | 1.6.0 | 563,200 字节 | P0/P1 归因修复、16 张愚者能力牌与 15 个愚者遗物运行时覆盖、卡牌/遗物/能力上下文保存恢复、多来源格挡拆分、卡牌上下文、RitsuLib 硬依赖；SHA-256 `c206f9dd00a30384f7142525a52d37024cb3c82072577e95104d11f54e44be9e`；v0.113.1 标准完整局、愚者实机与 18/18+15/15 上下文 PASS；v0.111.1 定向探针 10/10；双版本构建 0 错误 |
| 2026-11-21 07:15 | `sts2_statsthespire_local.dll` | 1.5.2 | 519,168 字节 | PaleBlueDot 来源捕获、补丁清理、UI 生命周期、原子写与写盘防抖、生成卡重复计数、Boss 白兽雕像、路径购买、死亡来源、全局 bundle、启动降级与隐私构建；SHA-256 `7b8aedcc1e83f364ba4b176bb1b37db734bf45b05a6077380701306a18e8327b`；v0.113.1 回归探针 PASS；v0.111.1 完整实机与 PaleBlueDot PASS；双版本程序集构建 0 错误 |

注：1.6.0 包内 README 于 2026-11-22 02:26 补充许可证与授权段；DLL、`manifest.json`、`settings.cfg` 未变。

注：1.4.4、1.5.0、1.5.1 的二进制包含构建机源码路径，已从当前目录与重写后 Git 历史移除；GitHub 旧对象清除待 Support；备份在 `TMP/1.5.2/privacy-backup/old-output/`。1.4.0 至 1.4.3 的历史归档仍在各自 `TMP` 版本目录。

## 目录约定

- `10.OUTPUT/<version>/manifest.json`
- `10.OUTPUT/<version>/sts2_statsthespire_local.dll`
- `10.OUTPUT/<version>/README_ZH.md`
- `10.OUTPUT/<version>/README_EN.md`
- `10.OUTPUT/<version>/settings.cfg`
