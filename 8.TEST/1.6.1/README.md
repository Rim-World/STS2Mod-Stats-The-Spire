# 1.6.1 发布测试

- 测试时间：2026-11-22 07:28
- 测试对象：`10.OUTPUT/1.6.1/sts2_statsthespire_local.dll`
- 游戏版本：v0.113.1（提交 `d09d48e5`）

## 构建和门禁

| 项目 | 结果 | 日志 |
|---|---|---|
| v0.113.1 Release 构建 | 0 错误、7 个既有可空警告；DLL 564,736 字节 | `build-v113-release.log` |
| v0.111.1 Release 构建 | 0 错误、7 个既有可空警告；DLL 564,736 字节 | `build-v111-release.log` |
| 版本一致性 | `version-ok 1.6.1` | 命令输出 |
| 发布合规 | 12 PASS、0 FAIL | `check-release-1.6.1.log` |

## 安装加载

- 安装目录：`C:\Game\Steam\steamapps\common\Slay the Spire 2\mods\sts2_statsthespire_local`
- DLL SHA-256：`b3e2f74ff6191ac43ddc8cec1d8b0813b5a44c5918d137fce04c356e55b2ee15`
- 输出包 DLL 与安装 DLL 哈希一致。
- 日志：`load-1.6.1-stdout.log`。
- 日志显示 `Stats the Spire (Local) v1.6.1 initializing...`。
- 日志显示 `EnnuiPower.ModifyHandDraw installed`。
- 日志显示 mod 列表版本 `1.6.1`。
- 游戏自动禁用同 ID 的 Steam Workshop 旧副本，只加载本地 1.6.1。

## 限制

- 无头加载在 25 秒超时后被终止，退出码 124；初始化与主菜单加载已完成。
- 1.6.1 的功能复测在版本号为 1.6.0 的同源构建上完成；1.6.1 只做了双版本构建、合规检查和加载检查。
- 未做 v0.111.1 运行测试、人工 F8、面板视觉或完整真人操作验收。
- 测试 Mod 不在游戏 `mods` 中；游戏进程已关闭。
