# 输出产物登记

本目录保存构建出的 Mod 成品。仅保留最近三个版本；变更历史由 git 与 Release 提供。

| 时间 | 名称 | 版本 | 大小 | 说明 |
|---|---|---|---|---|
| 2026-11-20 02:24 | `sts2_statsthespire_local.dll` | 1.5.0 | 539,136 字节 | RitsuLib 设置页、可重绑定 F8、StartTime+seed 对局目录、legacy 回退与全结局收尾；SHA-256 `849001d91dd68219f3073e1188ee740a2839d7c484685a074fdbeb1204ccc431`；v0.111.1 设置/放弃/胜利已验证；v0.113.1 最终 DLL 胜利收尾已验证；提交 `0f77de7113`；标签 `v1.5.0`；分支 `main`，推送目标 `fork/maintenance/1.5.0` |
| 2026-11-19 20:44 | `sts2_statsthespire_local.dll` | 1.4.4 | 520,192 字节 | 跳过 `_shop_purchases` 旁路文件，消除贡献合并警告；SHA-256 `aee05dcdf954e16314f6636006a72543ca795854c86383feb4c7f5913190b47f`；v0.111.1 聚焦探针已验证；v0.113.1 完整实机已验证（种子 51111）；提交 `<旧提交已移除>`；标签 `v1.4.4` |
| 2026-11-19 20:00 | `sts2_statsthespire_local.dll` | 1.4.3 | 520,192 字节 | 修复 v0.111.1 伤害守卫补丁；SHA-256 `1b0fc3e7dcef86cb40ec9edb96330833ba6e1d1ead420dfac0471e26499c2a63`；v0.111.1 完整实机已验证；提交 `6860d46979`；标签 `v1.4.3` |
注：1.4.0 已归档到 `TMP/1.4.3/old-output/1.4.0/`；1.4.1 已归档到 `TMP/1.4.4/old-output/1.4.1/`；1.4.2 已归档到 `TMP/1.5.0/old-output/1.4.2/`。

## 目录约定

- `10.OUTPUT/<version>/manifest.json`
- `10.OUTPUT/<version>/sts2_statsthespire_local.dll`
- `10.OUTPUT/<version>/README_ZH.md`
- `10.OUTPUT/<version>/README_EN.md`
