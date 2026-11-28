# Stats the Spire (Local Edition) — STS2 Data Analysis Mod

**Version**: 1.13.0
**For**: Slay the Spire 2 v0.111.1, v0.113.1, and v0.114.0
**Dependency**: A complete `STS2-RitsuLib 0.6.16` or newer compatible installation is required; use the compat variant for the running game branch
**Dependency note**: RitsuLib may send its own telemetry to its author; this mod makes no network requests. Manage telemetry in the RitsuLib settings.
**Type**: Info display only, no gameplay impact. This mod uses no network and is fully offline.

---

## What Does This Mod Do?

Stats the Spire (Local Edition) is a pure offline data analysis tool for Slay the Spire 2. It displays potion drop chances, enemy intent state machines, in-combat contribution breakdowns, career statistics from your run history, and more to help you make better decisions during your climb.

**No network. No server. All data is local.**

---

## Feature Overview

### Infomod — In-Run Information

**Potion Chance Tracker**

An indicator on the top bar tracks your current potion drop chance. Hover over it to see the elite combat drop chance; White Beast Statue shows 100%.

**Unknown Room Preview**

Hover over unvisited unknown rooms to see the full probability list of what the next unknown room encounter could be. Relics that affect unknown room encounter chances are factored into the calculation.

**Card Drop Chance**

An indicator on the top bar shows the rare-count distribution for the next base card reward: 0 to N rare cards and at least 1 rare. The default group has 3 cards. Lasting Candy adds a fourth power card on its triggering combat; that card uses the base rarity table. Extra reward groups from Prayer Wheel, White Star, and The Hunt are excluded. Boss rewards show 100% rare; Blitz hides the indicator.

**Shop Prices**

Hover over unvisited shop rooms to see expected prices at the next shop, including Common, Uncommon, Rare, and Shop-rarity relics. Prices are color-coded so you can easily tell whether you can afford a given item with your current gold. Relics that affect shop prices are factored into the price calculation.

### Intent Graph — Monster AI Visualization

Displays monster intents as a state machine. Hover over a monster to view its intent state machine. Multi-hit attack icons use total damage; initial branches use the actual slot or encounter variant.

**Intent** — Shows what the monster will do next turn.

**Intent Group** — A group of intents enclosed in a blue box, representing all possible actions the monster may take next turn. Top-left: probability. Top-right: the maximum number of consecutive uses for this intent.

**Intent Transitions** — Always starts from the "Initial" intent. On the following turn, follows the yellow arrows to the next intent or intent group.

### Combat Contribution Chart

**Automatically pops up after each combat** (toggle with Ctrl+F8), showing the contribution of every card, relic, and potion during that combat:

```
Vs. Gremlin Nob
────────────────────────────
Damage
  Strike     x3  ████████████████  64  (42%)
  Bash       x1  ██████████  40  (26%)
  [R] Vajra       █████  20  (13%)      ← Relic contribution
  [P] Fire Potion ████  16  (10%)       ← Potion contribution
────────────────────────────
Defense
  Defend     x3  ████████████  30  (60%)
  [R] Orichalcum  ██████  15  (30%)
────────────────────────────
Card Draw
  Battle Trance x1 ████████████  3  (75%)
  [R] Bag          ████  1  (25%)
────────────────────────────
Energy Gained
  Bloodletting x1 ████████████  2  (100%)
```

**Run Summary** additionally includes a **Healing** section.

**Highlights**:
- Cards, relics, potions mixed and sorted together in one chart
- **Indirect damage tracking**: poison, Vulnerable bonus damage etc. attributed to the card that applied the effect
- **Modifier attribution**: Strength, Vulnerable bonuses etc. attributed to the source that provided the modifier
- **Defense tracking**: FIFO block attribution + Dexterity bonus + Weak mitigation + Buffer/Intangible mitigation
- **Card draw / energy** displayed independently per source
- **Potion contributions** correctly attributed
- **Sub-bars**: transformed/generated cards displayed as indented sub-bars under their source card
- **Upgrade contributions**: in-combat upgrade bonuses shown as orange segments
- **Real-time updates**: open the panel during combat to see live contribution data

#### Detailed Contribution Rules

The combat contribution panel shows exactly how much each card, relic, and potion contributed during a combat. Every effect is tracked and attributed to **the original card (or relic, or potion) that produced it**.

##### How Damage Is Assigned

**Direct damage** goes to the card that dealt it.

**Strength bonus** does NOT go to the attack card — it goes to the card that gave you the Strength.

**Vulnerable bonus** follows the same logic. The extra damage from Vulnerable is attributed to the card that applied Vulnerable.

**Multiple sources stack proportionally**.

**Poison, Thorns, Flame Barrier, and other indirect damage** are attributed to the card that originally applied the power.

##### How Defense Is Calculated

Damage is reduced through five layers, applied in order:
1. **Reduce enemy Strength** — credited to the card that reduced Strength
2. **Debuffs on the enemy** (Weak, etc.) — credited to the source that applied the debuff
3. **Intangible** — credited to the source that applied Intangible
4. **Block** — consumed FIFO (first-generated, first-consumed)
5. **Buffer** — credited to the source that applied Buffer

##### Other Rules

- **Self-damage**: shown as a red segment in the Defense section, excluded from percentage calculation
- **In-combat upgrades**: extra damage/block goes to the upgrade source, shown as orange segments
- **Orb attribution**: passive damage belongs to the orb's creator; extra evokes go to the evoker

##### Color Legend

| Color | Meaning |
|---|---|
| **Blue** | Direct damage / direct block |
| **Purple** | Modifier bonus (Strength, Vulnerable, Dexterity, etc.) |
| **Orange** | Upgrade bonus (e.g., Armaments upgrading other cards) |
| **Red** | Self-damage |
| **Gold bar** | Relic contribution |
| **Cyan bar** | Potion contribution |
| **Green bar** | Healing contribution |

### Global Contribution Summary

Press **Ctrl+F8** anytime to open the contribution panel, with two tabs:

| Tab | Content |
|------|------|
| **This Combat** | Detailed contributions from the current (or most recent) combat |
| **Run Summary** | Cumulative contributions across all combats this run |

### Personal Career Statistics

Go to **Compendium → Character Data → Personal Career Stats** to see aggregated data from all your local RunHistory:

- **Win rate summary cards**: data range / ascension W-L / ascension win rate / highest win streak
- **Win rate trend**: rolling win rate over last 10 / 50 / 100 / all runs
- **Death cause ranking**: Top causes of death by encounter
- **Deck building + Path stats**: per-Act averages for cards obtained / bought / removed / upgraded, plus monster / elite / ? room / shop / rest site counts
- **Ancient relic pick rates**: grouped by Elder, with pick rate / pick count / win rate / win rate delta for each pool
- **Boss damage taken**: average HP loss + death rate for all act bosses
- Character filter: all characters / single character

### Run History Single-Run Stats

In **Run History details**, click the **Run Stats** button. Two windows open side by side:

- Left: character · ascension · floor · victory/defeat
- Left: deck building / path stats (per-Act tables)
- Left: ancient relic picks
- Left: boss damage taken
- Right: contribution stats for every combat in this run
- Both windows have equal width and are centered. The right close button closes both windows; Esc and the dim layer also close them.

### Card Library & Relic Collection Personal Stats

In **Compendium → Card Library / Relic Collection**, each card or relic's detail panel shows your personal sample size + pick rate + win rate + upgrade rate + removal rate + buy rate, aggregated from your local run history.

---

## Hotkeys

| Hotkey | Function |
|--------|----------|
| **Ctrl+F8** | Toggle the contribution panel by default; rebind it in the RitsuLib settings |

---

## Settings Page (RitsuLib)

RitsuLib provides the settings page. Open it from the main menu, run pause, or combat pause. The F7 custom panel was removed.

| Setting | Description |
|--------|-------------|
| **Language** | Follows the game: Chinese (simplified or traditional) uses the Chinese UI; other languages use the English UI. Restart the game after changing its language. |
| **Intent panel scale** | 0.5× to 1.25× |
| **Feature Toggles** | Enable/disable 8 sub-features |
| **Contribution panel hotkey** | Default Ctrl+F8; click to record a new shortcut; text input and the developer console suppress it |
| **Auto-clean** | Enabled by default; removes contribution data older than 90 days at startup; turning it off keeps all data; applies next launch |

---

## Data Storage Location

```
%AppData%/sts2_community_stats/
├── contributions/
│   └── {StartTime}_{seed}/            # One directory per run
│       ├── meta.json
│       ├── combat_{floor}_{sequence}.json
│       ├── summary.json
│       ├── live.json
│       ├── shop_purchases.json
│       └── shop_offers.json
└── mod_prefs.json                     # Mod settings
```

Files from 1.4.4 and earlier named `{seed}_*.json` are read as a legacy fallback. They are not moved or renamed. Two consecutive seed-61111 runs were verified on v0.113.1; v0.111.1 was verified with seed 91111 in a StartTime-keyed directory.

Auto-clean is on by default. At startup, contribution directories and legacy flat files older than 90 days are removed. Runs referenced by `current_run.save` and `current_run_mp.save` are protected. Turn off **Auto-clean** in the RitsuLib settings to keep all data. The setting is stored as `auto_cleanup` in `mod_prefs.json`.

---

## Installation

1. Install the complete `STS2-RitsuLib` mod folder under `mods/`. Copying only the root DLL is not enough.
2. Place the `sts2_statsthespire_local` folder into the game directory's `mods/` folder.
3. Launch the game. The manifest dependency loads RitsuLib first.

> **Keep one copy only.** The local build and the old Workshop build share the ID `sts2_statsthespire_local`. Unsubscribe from or disable the Workshop copy first. Unchecking the local copy does not disable the Workshop copy.

### File Structure

```
mods/sts2_statsthespire_local/
├── manifest.json                      # Mod metadata
├── settings.cfg                       # Shipped configuration
├── sts2_statsthespire_local.dll       # Mod main program
├── README_EN.md                       # This document
└── README_ZH.md                       # Chinese documentation

mods/STS2-RitsuLib/                    # Installed separately by the player
├── STS2-RitsuLib.dll
├── shared/
├── compat/
├── assets.zip
└── mod_manifest.json
```

---

## FAQ

**Q: Does this mod affect gameplay balance?**
A: No. The mod is marked `affects_gameplay: false` and only displays information.

**Q: Does this mod require internet?**
A: No. This is the Local Edition — all data comes from your local run history. No network connection is ever used.

**Q: Why does personal data show 0 samples?**
A: You may not have enough run history yet. Play more runs and the data will accumulate. The mod analyzes your saved `.run` files.

---

## Version Compatibility

This build targets Slay the Spire 2 v0.111.1, v0.113.1, and v0.114.0. 1.13.0 adds auto-play source attribution for HistoryCourse, Hellraiser, ImitationLearning, and LostLute. HistoryCourse's copied card credits the relic row; the three powers use their real source when known and fall back to `HELLRAISER`, `IMITATION_LEARNING`, or `LOST_LUTE` otherwise. Distilled Chaos, Imbued, HowlFromBeyond, and card `OnPlay` paths are verified; Sly remains unsplit. 1.12.0 adds Whispering Earring relic-root attribution: first-turn auto-played cards credit `WHISPERING_EARRING`, each auto play increments `AutoPlayedCount`, auto cards get no row, and the relic's +1 energy stays separate. 1.11.0 rolls auto-played cards up to their root source: Mayhem, Stampede, and Simpleton auto-plays now credit damage, block, draw, energy, healing, stars, and pet values to the source card row; manual and auto play counts are displayed separately. 1.10.0 adds v0.114.0 support while keeping v0.111.1 and v0.113.1; RitsuLib 0.6.16 supplies matching runtime variants for 0.111, 0.113, and 0.114. 1.7.0 adds separate pet absorption and pet-waste accounting: Osty and Wuppy HP loss is recorded as `PetAbsorbed`, while remaining Osty HP destroyed by Bone Shards or Sacrifice appears as a separate red `PetWasted` row; legacy `osty` data migrates automatically. 1.7.1 fixes map hover panels: the panel is removed when the pointer leaves, and only the current map point keeps a panel. 1.7.2 fixes Fool melody titles replaced by third-party localisation mods: names are resolved through SmartFormat and then converted to plain text. 1.7.3 records each "gain energy next turn" application separately, so `Ki Rest` energy is not credited to `Wah Ki`; run-history DPS uses the `.run` combat-turn total; ancient event names fall back to the `ancients` table. 1.8.0 opens the run-history statistics and contribution chart as side-by-side windows and removes the separate chart button. 1.8.1 removes the left close button, makes the right close button close both windows, uses equal centered windows, and enlarges the help and close buttons with stronger contrast. Card/relic ID changes across game versions are handled automatically. 1.8.2 ignores the contribution hotkey while the paired run-history windows are open, so it cannot hide the right window or leave the left window orphaned. 1.8.3 changes the default hotkey to Ctrl+F8 to reduce conflicts with mods that use plain F8; existing local settings are not overwritten automatically. 1.9.0 makes the mod UI follow the game language: simplified and traditional Chinese use the simplified Chinese UI; all other languages use the English UI. Game term names always use the game's current official localization table. The language setting was removed from the settings page; restart the game after changing its language.

- 1.13.0: expanded auto-play sources. HistoryCourse's copied card credits the `HISTORY_COURSE` relic row. Hellraiser, ImitationLearning, and LostLute use their real source when known and fall back to the `HELLRAISER`, `IMITATION_LEARNING`, or `LOST_LUTE` card row otherwise. Distilled Chaos credits the potion row; Imbued credits the enchanted card row; HowlFromBeyond credits itself; Sly remains unsplit.
- 1.12.0: Whispering Earring auto-plays roll up to the relic root. First-turn auto-played damage, block, draw, energy, stars, healing, and other values go to the `WHISPERING_EARRING` row; each auto play increments `AutoPlayedCount`, auto cards get no row, and the +1 energy is not double-counted.
- 1.11.0: auto-played cards roll up to the root source. Mayhem, Stampede, and Simpleton auto-play damage, modifier damage, upgrade damage, block, draw, energy, stars, healing, self-damage, and pet values go to the source card row; `TimesPlayed` counts manual plays only and `AutoPlayedCount` is new; auto cards no longer get their own rows, nested auto-plays inherit the root, and old JSON reads the new field as 0.
- 1.10.0: adds Slay the Spire 2 v0.114.0 support and requires RitsuLib 0.6.16. Fixes Happy Flower and Fake Happy Flower energy hooks on v0.114 (`AfterPlayerTurnStartEarly`) and selects TunePower's `AfterSideTurnEnd` (v0.113) or `AfterCardPlayed` (v0.114) at runtime. One DLL remains compatible with v0.111.1, v0.113.1, and v0.114.0.
- 1.9.6: fixes the Lasting Candy parity. Rewards are populated before `CombatRewardsSeen` increments, so the current count must be odd for the fourth card. 1.9.5 inverted the 3/4 prediction.
- 1.9.5: the card drop panel now shows the rare-count distribution for the next base card reward. It enumerates 0..N rare cards with the sequential pity rule, supports the Lasting Candy fourth card, Boss and Big Game Hunter tables, and missing-rarity remapping; extra reward groups are excluded.
- 1.9.4: fixes the 1.9.3 protection path: converts Godot `user://` paths to OS paths; reads current saves for all three profiles; only deletes `{StartTime}_{seed}` run directories; pre-scans nested reparse points; the probe now uses the real save path.
- 1.9.3: restores 90-day contribution cleanup by default; adds a RitsuLib **Auto-clean** toggle; protects runs referenced by `current_run.save` and `current_run_mp.save`; skips reparse points.
- 1.9.2: the run-history "Run Stats" entry button keeps the 1.5x height and font size; its width is trimmed from `390` to `350` to reduce side padding.

## License and authorization

- Source baseline: `https://github.com/reddots1453/STS2mod-Stats_the_Spire`.
- Original author: `reddots1453`.
- This maintenance repository is released under the MIT License. The full text is in `LICENSE`.
- The upstream repository has no declared license. The MIT grant covers only code added or modified in this maintenance repository, not upstream code.
- Upstream source and author attribution are retained.
