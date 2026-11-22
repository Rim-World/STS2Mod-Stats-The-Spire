# Changelog

本文件遵循 Keep a Changelog。项目级版本日志见 `../7.ChangeLog/`。

## [1.7.0] - 2026-11-22

### License
- The maintenance repository and the 1.7.0 output package are released under the MIT License. See `LICENSE`.

### Added
- `PetAbsorbed`: actual HP lost by Osty or Wuppy while absorbing damage for the player. Osty absorption still uses the LIFO summon-source stack; Wuppy absorption is attributed to the `WUPPY` parent row.
- `PetWasted`: remaining Osty HP destroyed by Bone Shards or Sacrifice. It is shown as a separate red `宠物损耗` row and subtracted from total defense.
- Legacy data migration: `SourceType: "osty"` becomes `pet`; old Osty `EffectiveBlock` moves to `PetAbsorbed`; identifiable negative `EffectiveBlock` moves to `PetWasted`.

### Changed
- `EffectiveBlock` now means real block only. Osty HP absorption no longer enters it.
- The old deferred Osty-death negative-defense path is removed. Player-death cleanup and non-BoneShards/Sacrifice deaths produce no `PetWasted`.
- Pet rows share `[宠]`, pet color, and the existing defense section; Osty keeps summon-card sub-bars, Wuppy has a parent row only.

### Verified
- v0.113.1 and v0.111.1 Release builds: 0 errors, 7 existing nullable warnings; logs `8.TEST/1.7.0/build-v113-final2.log`, `build-v111-final2.log`.
- Offline migration probe: 5/5 PASS; log `8.TEST/1.7.0/migration-probe.log`.
- Tracker probe: Osty LIFO, BoneShards/Sacrifice waste, player-death guard, Wuppy callback, and TotalDefense PASS; logs `8.TEST/1.7.0/tracker-probe-owner-guard.log`, `tracker-probe-callback.log`.
- v0.113.1 in-game probe: 51/51 PASS. Real Wuppy absorbed 3 HP and excluded 7 overkill; healed BoneShards used actual HP 20 (stack 10) and wrote `PetWasted=20`; Sacrifice used actual HP 12 (stack 7) and wrote `PetWasted=12`; `EffectiveBlock` stayed 0. Damage rows, sub-cost bars, UI rows, and colors passed; log `8.TEST/1.7.0/petgame-live-15211-58d43131.log`.
- Final low-residual fixes: standalone self-damage rows keep the source name; `PetWasted` always writes to the top-level killer-card bucket so generated/potion cards cannot disappear as orphan sub-entries.
- Focused re-test: 34/34 PASS for standalone self-damage name, generated-card top-level waste, damage rows, sub-cost split, and bar scale; log `8.TEST/1.7.0/petgame-focused-f25f0778.log`.
- v0.111.1 runtime: UI-only 34/34; first-combat probe 53/53. Real Osty absorbed 4 HP; BoneShards used actual HP 20 (stack 16) and wrote `PetWasted=20`; Sacrifice used actual HP 12 (stack 7) and wrote `PetWasted=12`. Wuppy is absent and was skipped; log `8.TEST/1.7.0/v111-petprobe-live-5c-stdout.log`.
- v0.111.1 full run: 49 floors, 3 acts, 29 combat files matching 29 combat map points, no duplicates, 38 summary sources, `live` cleared, exit code 0. AutoSlay abandoned at the final boss, so the history is `win=false`; `UNTRACKED` block was 302 and needs Oracle comparison. Log `8.TEST/1.7.0/v111-autoslay-gated-62005-stdout.log`.
- Final DLL SHA-256 `f25f0778eb842c675f1aa72e9da7cda4b4270a58ea5600a88c613c32e5140e85`, 568,320 bytes.
- Not verified: victory settlement on v0.111.1; event-stream Oracle comparison for v0.111.1; natural Necrobinder run on v0.111.1 (probe character was Defect and injected Osty); manual visual acceptance.

## [1.6.1] - 2026-11-22

### Fixed
- No-reward event combat (for example the Battleworn Dummy) now saves at `CombatRoom.Exit`, before `CombatManager.Reset`. The no-reward `ProceedWithoutRewards` path exits the room before `EndCombatInternal` reaches `CombatEnded`, so the old save trigger was skipped in non-interactive and instant modes.
- Enemy attacks on Wuppy/other non-Osty pets no longer count as player damage. Wuppy's Loyalty redirects those hits to the pet; the old path put them under `UNTRACKED`.
- `EnnuiPower.ModifyHandDraw` extra cards now enter `CardsDrawn` through a runtime-optional postfix; v0.111.1 skips the patch safely.

### Verified
- v0.113.1 seed 62003: floor 45 wrote `combat_45_1.json` with 1 test-injected source; 29 combat files, no duplicate floor; victory, exit 0; log `8.TEST/1.6.0/exitfix6-62003-stdout.log`.
- v0.113.1 and v0.111.1 Release builds: 0 errors, 7 existing nullable warnings; logs `8.TEST/1.6.0/build-v113-exitfix-release.log`, `build-v111-exitfix-release.log`.
- v0.113.1 seed 62005 with Fool probe: `UNTRACKED` damage 0; `ENNUI_POWER` hand draws match oracle; log `runtimefix-62005-stdout.log`, compare `runtimefix-compare-fool.txt`.
- v0.113.1 standard seed 62005: 49 floors, `UNTRACKED` 0; process cleanup timed out after the run, data complete; compare `runtimefix-compare-standard.txt`.
- Not verified: normal GUI event-combat visual acceptance; test-only `EventSourceProbe` is not shipped.

## [1.6.0] - 2026-11-22

### Added
- `AttributionGapPatch` for confirmed 1.6.0 relic/power source gaps: Belt Buckle, Ruined Helmet, Snecko Skull, Unsettling Lamp, Tungsten Rod, Beating Remnant, Brilliant Scarf, Bookmark.
- `FoolContentPatch` with 34 runtime-optional hooks for 16 Fool power cards, 15 Fool relics, God Byrd, and Wuppy's Loyalty.
- Deterministic `AttributionProbe` for first-heal placement, forge-log consumption, upgrade-block split, and pending-draw cleanup.

### Fixed
- Real damage guard uses async-local depth and binds the v0.113.1 `DamageTargetData` root overload; v0.111.1 keeps the 7-parameter fallback.
- Non-drawing relic/power hooks no longer leave a pending draw source; pending draw is cleared at hook/turn/combat boundaries.
- Block pool is cleared only when block is actually zero, preserving Barricade/Blur/Self-Forming Clay block.
- Pantograph and Bath Robes heal hooks run with relic context; post-victory Burning Blood/Black Blood/Meat on the Bone heals land in the just-ended combat.
- Osty death is observed through the collection `Kill` overload once.
- Defeat flushes the final combat before run finalization; victory and defeat both save and display through `CombatManager.CombatEnded`.
- Energy/star gains are recorded from the actual `PlayerCombatState` change with local-player filtering.
- No-star-cost cards clamp canonical stars to zero; auto-played cards do not claim cost savings.
- Sovereign Blade forge sub-bars consume their forge log; generated-card origins use the live power/relic/potion context; upgrade block no longer double counts.

### Verified
- v0.111.1 and v0.113.1 Release builds: 0 errors, 7 existing nullable warnings; final 1.6.0 DLL is 563,200 bytes, SHA-256 `c206f9dd00a30384f7142525a52d37024cb3c82072577e95104d11f54e44be9e`.
- Final v1.6.0 DLL load: `Stats the Spire (Local) v1.6.0 initializing...`; `FoolContent 34 hooks installed, 0 skipped`; AutoSlayer selected `CHARACTER.FOOL` for seed 63001; log `8.TEST/1.6.0/final-load-63001-stdout.log`.
- v0.113.1 standard AutoSlayer seed 62005: victory, Silent, Standard, act 3/3, floor 49; 29 combats, 31 combat files, 53 summary sources; log `8.TEST/1.6.0/v113.1-autoslay-62005-stdout.log`.
- v0.113.1 Fool live probe seed 62003: victory, Ironclad, act 3/3, floor 48; 28 combats, 97 summary sources, 7 shop purchases; 15 Fool relics added and 18 Fool powers applied; log `8.TEST/1.6.0/v113.1-foolprobe-62003c-stdout.log`.
- v0.113.1 Fool hook context check: 18/18 powers and 15/15 relics PASS; log `8.TEST/1.6.0/v113.1-foolprobe-context-62999-stdout.log`.
- v0.111.1 `AttributionProbe`: 10/10 PASS (`RESULT PASS`) on final code, log `8.TEST/1.6.0/attribution-probe-v111-15-stdout.log`; final rerun `v111-15` exited 0.
- v0.111.1 mod initialization: `RealDamageGuard patched v0.111 7-param`; `OstyDeathPatch patched collection Kill(Creature, bool)`; all 34 Fool targets skipped on the older game.
- Not verified: manual F8 press, pause-menu settings page, and visual acceptance of the new counters.

## [1.5.2] - 2026-11-21

### Added

- Runtime patch inventory (`PatchInventory`) listing every bound target and patch class.
- One-shot pending power-source override for pale-blue-dot's delayed `DrawCardsNextTurnPower` apply.
- Atomic JSON writes with `.corrupt-*` quarantine for unreadable contribution, shop, live, and career files.

### Fixed

- Pale blue dot's next-turn draw is now credited to `PALE_BLUE_DOT`, not the fifth attack card; amount 1 and 2 probes pass.
- Run-history popup closes when the submenu hides, Esc closes only the popup, and replay data is cleared when the contribution panel hides.
- Contribution and intent panels hide on run end, abandon, save-quit, new run, and defeat.
- `live.json` writes are coalesced with a 1 s debounce and force-flushed at combat end and save-quit.
- Generated and native cards with the same source id no longer merge into a doubled summary entry.
- White Beast Statue shows 100% in boss rooms.
- Path card purchases use the shop sidecar for both colored and colorless cards.
- Career cache preserves `DeathSource`; abandoned runs display `ABANDONED`.
- Card/relic bundles stay global when a character filter is selected.
- Data-directory failures no longer prevent startup; manual patch blocks are independently isolated; intent metadata initializes after Harmony.

### Removed

- `PaleBlueDotDrawCompatPatch` (wrong target: `PaleBlueDotPower.ModifyHandDraw`).
- `CardRemovalPatch`, `CardUpgradePatch`, `EventOptionPatch`, and the no-op `SelectCard` hook.
- Empty `CombatHistory.PowerReceived` hook.
- Automatic 90-day contribution cleanup.

### Security

- Test logs and standard-output files under `8.TEST` are ignored.
- Test logs and old release DLLs containing the builder profile path are removed from published Git history.
- Release builds use a source path map, no PDB, and contain no `C:\Users\` path.

### Verified

- v0.113.1 persistence regression probe: duplicate merge, corrupt quarantine, atomic write, career source, abandoned cause, shop path buckets all PASS.
- v0.113.1 PaleBlueDot probes: amount 1 and amount 2 both PASS; first draw count matches and source is `PALE_BLUE_DOT`.
- v0.113.1 UI lifecycle probe: popup close, Esc, replay clear all PASS.
- v0.113.1 map probe: `MapFixProbe PASS`; no `Child is not a child` error.
- v0.111.1 and v0.113.1 Release builds: 0 errors, 7 existing nullable warnings.
- v0.111.1 runtime recheck was not possible with the current single-install copy: the copied launcher still reports v0.113.1 and RitsuLib selects its v0.113 variant. The assembly build and prior v0.111.1 evidence remain available.

## [1.5.1] - 2026-11-20

### Fixed

- Legacy shop purchases loaded from a 1.4.4-or-earlier save are now copied into the current `{StartTime}_{seed}/shop_purchases.json` at run finalization. The legacy file is not moved or deleted.
- Corrupt `mod_prefs.json` and `settings.json` now log a `[StatsTheSpire][Config]` warning and fall back to defaults. Missing files and unknown keys do not warn.
- `manifest.json` now requires game version `0.111.1` instead of the untested `0.111.0`.

### Changed

- The original 90-day cleanup remains active for both per-run directories and legacy flat files. SPEC 2.0 (`SP-057`) records this as intentional behavior.
- Version bumped to 1.5.1.

### Verified

- v0.113.1 build: 0 errors, 7 nullable warnings.
- v0.111.1 assembly build: 0 errors, 7 nullable warnings.
- v0.113.1 compatibility probe: legacy shop load 2 entries; finalization produced a new `shop_purchases.json` with 2 entries; the legacy file remained; `legacy-copy PASS`.
- v0.111.1 compatibility probe: main mod initialized; legacy shop load 2 entries; finalization copied 2 entries; the legacy file remained.
- v0.111.1 standard AutoSlayer (seed 81111): 23 combat files, 62 summary sources, 14 purchases. AutoSlayer hit its 49-floor limit and finalized the run as abandoned; `live.json` and `shop_offers.json` were removed.
- Corrupt `mod_prefs.json`: one warning, default fallback.
- Corrupt `settings.json`: one warning, filter fallback to `character_filter_mode=auto` and `min_ascension=null`.
- Test settings restored: `mod_prefs.json` SHA-256 matched the backup; the test-created `settings.json` was removed.
- Final artifact: 540,160 bytes; SHA-256 `ca9ee135b5260ae28abd08aabd50b8defaf8c4f0289e9e472e8c1a067ae18e19`.

## [1.5.0] - 2026-11-20

### Added

- RitsuLib settings page for language, intent-panel scale, 8 feature toggles, and the contribution-panel hotkey.
- Rebindable contribution-panel runtime hotkey; default F8.
- `RunIdentity` and `RunStorage`; per-run data directory `{StartTime}_{seed}`.
- `meta.json`, sequenced combat files, and legacy read-only fallback.

### Fixed

- v0.111.1 did not fire the metrics-upload event for victory or defeat runs. All run endings now finalize through the `CreateRunHistoryEntry` postfix.
- Duplicate finalization is blocked by `_finalizedRunKey`; on v0.113.1 the postfix finalizes first and the later native event path is skipped.

### Changed

- Contribution, shop purchase, shop offer, live state, and history replay now use `StartTime + seed` instead of seed alone.
- New runs create a per-run directory and do not load same-seed old data.
- Saved runs load only their own RunKey; legacy seed files remain readable.
- Plugin manifest now requires `STS2-RitsuLib` 0.6.14.
- Package reference uses `STS2.RitsuLib` 0.6.14; RitsuLib assemblies are not copied into this mod.
- Version bumped to 1.5.0.

### Removed

- `FilterPanel` and the F7 frame-polling panel.
- F8 frame polling; RitsuLib handles hotkey routing and text-input suppression.

### Verified

- v0.113.1 build: 0 errors, 7 nullable warnings.
- v0.111.1 assembly build: 0 errors, 7 nullable warnings.
- v0.113.1 RitsuLib settings page: 1 page, 2 sections, 11 entries; main-menu open request succeeded.
- v0.113.1 binding persistence: language, intent scale, and PotionOdds toggle wrote and restored; `mod_prefs.json` kept the new keys.
- v0.113.1 hotkey: F8 registered; F9 rebind succeeded; F8 restored; invalid binding rejected; text/console suppression enabled.
- v0.113.1 same-seed runs: 61111 produced StartTime 1795107016 and 1795107502, 27 combat files, 72 SourceDto entries, 14 purchases each; the second run did not load the first run.
- v0.113.1 history: both 61111 histories resolved to their own RunKey; 51111 used the legacy summary with 75 sources.
- v0.113.1 career: all 201/187, standard 199/185, multiplayer 2, Fool 15/13.
- v0.111.1 RitsuLib: compatibility branch 0.111.0 loaded; settings page built with 2 sections and 11 entries; language, scale, and toggle bindings wrote back; F9 and F8 rebinds succeeded; invalid binding rejected.
- v0.111.1 finalization: first victory run exposed a missing `OnMetricsUpload` event. All run endings now finalize through the `CreateRunHistoryEntry` postfix, with duplicate-finalization protection.
- v0.111.1 abandoned run 71112: 26 combats, 73 summary sources, 10 purchases; `live.json` and `shop_offers.json` removed.
- v0.111.1 victory run 71111: 27 combats, 66 summary sources, 11 purchases; `live.json` and `shop_offers.json` removed.
- v0.111.1 same-seed runs: two `71111` RunKeys produced separate directories; the older incomplete directory did not enter the new summary.
- v0.113.1 final DLL: seed 71113 victory, 12 combats, 60 summary sources; `live.json` and `shop_offers.json` removed; duplicate finalization was intercepted.
- Final artifact: 539,136 bytes; SHA-256 `849001d91dd68219f3073e1188ee740a2839d7c484685a074fdbeb1204ccc431`.
- Physical key and visual checks: pending.

## [1.4.4] - 2026-11-19

### Fixed

- `ContributionPersistence.AssembleFromCombats` 现在跳过 `{seed}_shop_purchases.json`。该文件是 `LocalShopPurchase` 数组，不是 `ContributionDoc`；旧代码会因此捕获一次 `JsonException` 并记录警告。
- 移除 `ContributionPersistence: load ..._shop_purchases.json failed` 日志噪声。

### Changed

- 版本号升级到 `1.4.4`。

### Verified

- v0.111.1：构建 0 错误、7 个可空警告。
- v0.113.1：构建 0 错误、7 个可空警告。
- v0.113.1：完整实机胜利（种子 51111）；26 场战斗、3 家商店、9 条购买；汇总 75 个来源。
- v0.113.1：无 `_shop_purchases` 警告。
- 聚焦探针：1.4.3 产生 1 条警告；1.4.4 无警告；两版均返回 `sources=1`。

## [1.4.3] - 2026-11-19

### Fixed

- `RealDamageGuardPatch` 改为运行时选择 `CreatureCmd.Damage` 重载：v0.111 使用 7 参数版本，v0.113 使用 8 参数版本。
- 修复 v0.111.1 上 `Harmony` 因找不到 8 参数目标而放弃整个伤害守卫补丁的问题。

### Changed

- 版本号升级到 `1.4.3`。

### Verified

- v0.111.1：构建、加载、3 个 Act 的战斗与商店流程、生涯统计、愚者历史读取、界面构造。
- v0.113.1：1.4.3 未复测；1.4.2 的 8 参数路径已在该版本验证。

## [1.4.2] - 2026-11-19

### Fixed

- 商店库存读取改为运行时探测：v0.111 使用 `MerchantRoom.GetLocalInventory`，v0.113 使用 `RunManager.MerchantSynchronizer`。
- 源码同时通过 v0.111.1 与 v0.113.1 游戏程序集编译。

### Changed

- 版本号升级到 `1.4.2`。

## [1.4.1] - 2026-11-19

### Added

- 生涯统计角色筛选加入「愚者」；角色列表改为运行时读取 `ModelDb.AllCharacters`。
- 愚者卡牌、遗物与角色本地化名称。
- v0.113 的「恐惧」「极速」对局模式筛选。
- 个人 fork 维护分支源码。

### Changed

- 角色筛选只统计本地玩家为所选角色的历史。
- 对局模式按 `GameMode` 名称匹配，不再按枚举数值匹配。
- 卡牌生成钩子按运行时方法签名注册，移除 `STS2_GE_V105` 编译期分支。
- 版本号升级到 `1.4.1`。

### Fixed

- v0.111 的 `Custom=3` 与 v0.113 的 `Dread=3` 数值冲突。
- 英文界面未知角色显示原始 `char.*` 键。
- 卡牌生成来源补丁在 v0.113 注册失败。

## [1.4.0] - 2026-11-19

### Added

- 生涯统计对局模式筛选：全部、标准、自定义、每日、多人。
- 生涯统计每局平均用时、卡组大小、遗物数、卡牌类型与稀有度。
- 贡献面板拖动缩放与尺寸持久化。
- 药水掉落、卡牌掉落独立功能开关。
- 意图面板缩放设置。
- Fiddle、PaleBlueDot、VoidForm 贡献归因补丁。
- Neow 选项池动态补充。

### Changed

- 源码基线切换为公开仓库 `reddots1453/STS2mod-Stats_the_Spire`。
- 游戏程序集引用改为 `STS2GameDir` 构建属性。
- 版本号升级到 `1.4.0`。

### Removed

- 联网更新器、上传提示、历史导入、离线队列与服务器配置。

### Fixed

- 适配 Slay the Spire 2 v0.113.1：
  - `RunManager` 单人/多人启动方法改名。
  - `TemporaryStrengthPower.AfterSideTurnEnd`。
  - 商店库存改从 `MerchantSynchronizer` 读取。
  - 伤害修正方法新增 `CardPlay?` 参数。
  - `OutbreakPower` 改为运行时探测。

## [1.1.0] - 2026-05-08

### Added

- v0.105.0 Beta 兼容。
- 个人生涯统计「最近对局」筛选。

### Fixed

- Infused Core orb 值修饰器追踪。
- 荆棘/火焰屏障多源归因。
- Sword Sage Replay 1 后 FORGE:BASE。
- F7 功能开关持久化。

## [1.0.1] - 2026-05-06

### Fixed

- 怪物意图状态机面板残留。
- save/load 后平均每回合伤害数据失真。

## [1.0.0] - 2026-05-05

### Added

- 本地版初始发布：战斗贡献、生涯统计、意图状态机、商店价格、概率提示、功能开关、中英双语。

### Removed

- 社区版服务器通信、上传下载、社区统计对比。
