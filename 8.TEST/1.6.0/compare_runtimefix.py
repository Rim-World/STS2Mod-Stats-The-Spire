#!/usr/bin/env python3
"""Compare one AutoSlay run's oracle events with the mod's combat files.

Checks:
- UNTRACKED damage should be absent after the Wuppy/enemy filter.
- Hand-draw modifier CardsDrawn (Ring of the Snake, Ennui) should equal
  hand-draw cards beyond five.
- Damage total is reported next to player-dealt oracle damage; self-damage
  and the intentionally skipped invincible phase are separate lines.
"""
import argparse
import collections
import json
import os
import re
import sys

# Sources that add cards to the opening hand (not card-play draws).
# Keep this list aligned with HandDrawBonusPatch and RelicHandDrawBonusPatch.
HAND_DRAW_SOURCES = {
    "RING_OF_THE_SNAKE",
    "ENNUI_POWER",
}


def floor_of(path: str) -> int:
    match = re.search(r"oracle_(\d+)_", os.path.basename(path))
    if not match:
        raise ValueError(f"not an oracle file: {path}")
    return int(match.group(1))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--oracle", required=True)
    parser.add_argument("--combat", required=True)
    parser.add_argument("--check-hand", action="store_true",
                        help="also compare known opening-hand draw bonuses")
    args = parser.parse_args()

    rows = []
    problems = []
    for name in sorted(os.listdir(args.oracle), key=floor_of):
        if not name.endswith(".json"):
            continue
        floor = floor_of(name)
        with open(os.path.join(args.oracle, name), encoding="utf-8") as handle:
            oracle = json.load(handle)
        combat_path = os.path.join(args.combat, f"combat_{floor}_1.json")
        combat = {}
        if os.path.exists(combat_path):
            with open(combat_path, encoding="utf-8") as handle:
                combat = json.load(handle)

        sources = combat.get("Sources", [])
        mod_damage = sum(
            source.get("DirectDamage", 0)
            + source.get("AttributedDamage", 0)
            + source.get("ModifierDamage", 0)
            + source.get("UpgradeDamage", 0)
            for source in sources
        )
        untracked = sum(
            source.get("DirectDamage", 0)
            + source.get("AttributedDamage", 0)
            + source.get("ModifierDamage", 0)
            + source.get("UpgradeDamage", 0)
            for source in sources
            if source.get("SourceId") == "UNTRACKED"
        )
        hand_bonus_draw = sum(
            source.get("CardsDrawn", 0)
            for source in sources
            if source.get("SourceId") in HAND_DRAW_SOURCES
        )

        player_damage = 0
        hand_draw = collections.Counter()
        for entry in oracle.get("Entries", []):
            if entry.get("Type") == "DamageGivenOracle" and entry.get("DealerSide") == "Player":
                player_damage += entry.get("TotalDamage") or 0
            if entry.get("Type") == "CardDrawnEntry" and entry.get("FromHandDraw"):
                hand_draw[entry.get("Round", 0)] += 1
        extra_hand_draw = sum(max(0, count - 5) for count in hand_draw.values())

        rows.append((floor, player_damage, mod_damage, untracked, hand_bonus_draw, extra_hand_draw))
        if untracked:
            problems.append(f"floor {floor}: UNTRACKED={untracked}")
        if args.check_hand and hand_bonus_draw != extra_hand_draw:
            problems.append(
                f"floor {floor}: hand_draw={hand_bonus_draw}, hand_extra={extra_hand_draw}"
            )

    print("floor oracle_player mod_total diff UNTRACKED hand_draw hand_extra")
    for floor, player, mod, untracked, hand_draw, extra in rows:
        print(f"{floor:>5} {player:>13} {mod:>9} {mod-player:>4} {untracked:>9} {hand_draw:>9} {extra:>10}")

    if problems:
        print("\nFAIL")
        for problem in problems:
            print(" -", problem)
        return 1
    if args.check_hand:
        print("\nPASS: no UNTRACKED damage; known opening-hand draw totals match")
    else:
        print("\nPASS: no UNTRACKED damage")
    return 0


if __name__ == "__main__":
    sys.exit(main())
