# 输出产物登记

本目录保存构建出的 Mod 成品。变更历史由 git 与 Release 提供。

| 时间 | 名称 | 版本 | 大小 | 说明 |
|---|---|---|---|---|
| 2026-11-28 21:02 | `sts2_statsthespire_local.dll` | 1.13.1 | 570,880 字节 | 删除失落鲁特琴重复能力上下文，`FoolContentPatch` 唯一注册；探针精确计数、真实来源、手动愚者/倾泻、数值与保存重载；v0.114.0 AutoSourceProbe 11/11 PASS；v0.113.1 主 Mod 加载、探针受环境阻塞；v0.111.1 仅构建；SHA-256 `9b5fb559fa20a71955048985010f5d3c4bc7f8330869f7a5fb12892c211a3d6c`；三版本 Release 构建 0 错误且哈希一致；安装后一次 `v1.13.1 initializing`、826 条绑定、458 个目标 |
| 2026-11-28 19:55 | `sts2_statsthespire_local.dll` | 1.13.0 | 571,392 字节 | 历史课复制牌归 `HISTORY_COURSE` 遗物行；地狱狂徒、模仿学习、失落鲁特琴建能力来源，缺失时回退到对应卡牌；精炼混沌归药水行；注能归被附魔卡牌；彼岸咆哮归卡牌自身；奇巧不拆分；SHA-256 `610ccba21a0b5590963edb94a4697ca5e0b614e8b11cfa23817e4073623aacc7`；v0.114.0 AutoSourceProbe 7/7、三版本 Release 构建 0 错误且哈希一致；安装后一次 `v1.13.0 initializing`、828 条绑定、458 个目标；v0.113.1/v0.111.1 运行与模仿学习单人触发未验证 |
| 2026-11-28 19:08 | `sts2_statsthespire_local.dll` | 1.12.0 | 570,368 字节 | 低语耳环 `AfterAutoPrePlayPhaseEnteredLate` 建立 `WHISPERING_EARRING` 遗物根来源；首回合自动牌数值归遗物行，每张自动牌增加 `AutoPlayedCount`，不建自动牌子行；+1 能量不重复；SHA-256 `f4129eb1990ac0f14fd91d1d27c648cf81a2287093a77d37dcb82b38cc8ab7c2`；v0.114.0 EarringProbe PASS（auto=2、伤害 32、格挡 5、能量 1）；三版本 Release 构建 0 错误且哈希一致；安装后一次 `v1.12.0 initializing`、820 条绑定、455 个目标；v0.113.1/v0.111.1 运行与 UI 未验证 |

旧版本目录已按「仅保留最近三个版本」规则移入回收站；完整版本记录和构建信息见 `7.ChangeLog`、git 历史与 Release。

## 目录约定

- `10.OUTPUT/<version>/manifest.json`
- `10.OUTPUT/<version>/sts2_statsthespire_local.dll`
- `10.OUTPUT/<version>/README_ZH.md`
- `10.OUTPUT/<version>/README_EN.md`
- `10.OUTPUT/<version>/LICENSE`
- `10.OUTPUT/<version>/settings.cfg`
