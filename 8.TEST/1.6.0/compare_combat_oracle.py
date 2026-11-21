#!/usr/bin/env python3
"""Compare the test-only CombatOracleProbe output with the mod's combat files.

The oracle is the game's own CombatHistory. It is not a copy of the mod's
attribution logic. The comparison is intentionally limited to facts that both
sides can prove: floors, damage to enemies, block actually consumed, card plays,
and bonus draws.
"""
from __future__ import annotations

import argparse
import collections
import glob
import json
import os
import sys
from typing import Any


def load_json(path: str) -> dict[str, Any]:
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def source_totals(sources: list[dict[str, Any]]) -> dict[str, int]:
    return {
        # Persisted combat files omit computed TotalDamage; sum the components.
        "damage": sum(
            int(s.get("DirectDamage", 0) or 0)
            + int(s.get("AttributedDamage", 0) or 0)
            + int(s.get("ModifierDamage", 0) or 0)
            + int(s.get("UpgradeDamage", 0) or 0)
            for s in sources
        ),
        "block": sum(
            int(s.get("EffectiveBlock", 0) or 0)
            + int(s.get("ModifierBlock", 0) or 0)
            + int(s.get("UpgradeBlock", 0) or 0)
            for s in sources
        ),
        "plays": sum(int(s.get("TimesPlayed", 0) or 0) for s in sources),
        "draws": sum(int(s.get("CardsDrawn", 0) or 0) for s in sources),
        "heal": sum(int(s.get("HpHealed", 0) or 0) for s in sources),
    }


def oracle_totals(entries: list[dict[str, Any]]) -> dict[str, int]:
    damage = 0
    blocked = 0
    plays = 0
    non_hand_draws = 0
    hand_draws_by_round: dict[int, int] = collections.defaultdict(int)
    card_plays: collections.Counter[str] = collections.Counter()
    damage_by_card: collections.Counter[str] = collections.Counter()
    powers: collections.Counter[str] = collections.Counter()

    # DamageGivenOracle is captured from Hook.AfterDamageGiven, which also
    # runs for the killing hit. DamageReceivedEntry is skipped for a lethal
    # hit when combat is already ending, so it is only a fallback.
    damage_entries = [e for e in entries if e.get("Type") == "DamageGivenOracle"]
    if not damage_entries:
        damage_entries = [e for e in entries if e.get("Type") == "DamageReceivedEntry"]

    for e in damage_entries:
        if e.get("ReceiverSide") == "Enemy":
            dmg = int(e.get("TotalDamage", 0) or 0)
            damage += dmg
            card_source = e.get("CardSource")
            if card_source:
                damage_by_card[card_source] += dmg
        if e.get("ReceiverSide") == "Player":
            blocked += int(e.get("BlockedDamage", 0) or 0)

    for e in entries:
        typ = e.get("Type", "")
        if typ == "CardPlayFinishedEntry":
            card = e.get("Card")
            if card:
                plays += 1
                card_plays[card] += 1
        elif typ == "CardDrawnEntry":
            if e.get("FromHandDraw"):
                hand_draws_by_round[int(e.get("Round", 0) or 0)] += 1
            else:
                non_hand_draws += 1
        elif typ == "PowerReceivedEntry":
            if e.get("DealerSide") == "Player" and e.get("Power"):
                powers[e["Power"]] += 1

    # The mod counts only draws beyond the base five in a hand draw. The
    # oracle groups hand draws by round; a short hand is not a bonus.
    extra_hand_draws = sum(max(0, n - 5) for n in hand_draws_by_round.values())
    return {
        "damage": damage,
        "block": blocked,
        "plays": plays,
        "draws": non_hand_draws + extra_hand_draws,
        "card_plays": card_plays,
        "damage_by_card": damage_by_card,
        "powers": powers,
    }


def discover_run(data_dir: str, run_key: str | None) -> tuple[str, str]:
    oracle_root = os.path.join(data_dir, "oracle")
    if not os.path.isdir(oracle_root):
        raise SystemExit(f"oracle directory not found: {oracle_root}")
    keys = sorted(
        (d for d in os.listdir(oracle_root) if os.path.isdir(os.path.join(oracle_root, d))),
        key=lambda d: os.path.getmtime(os.path.join(oracle_root, d)),
        reverse=True,
    )
    if not keys:
        raise SystemExit(f"no oracle run directories in {oracle_root}")
    key = run_key or keys[0]
    return key, os.path.join(oracle_root, key)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--data-dir", default=os.path.join(os.environ.get("APPDATA", ""), "sts2_community_stats"))
    ap.add_argument("--run-key", default=None)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    key, oracle_dir = discover_run(args.data_dir, args.run_key)
    mod_dir = os.path.join(args.data_dir, "contributions", key)
    oracle_files = sorted(glob.glob(os.path.join(oracle_dir, "oracle_*.json")))
    mod_files = sorted(glob.glob(os.path.join(mod_dir, "combat_*.json")))
    if not oracle_files:
        raise SystemExit(f"no oracle files in {oracle_dir}")
    if not mod_files:
        raise SystemExit(f"no mod combat files in {mod_dir}")

    oracle_by_floor: dict[int, list[dict[str, Any]]] = collections.defaultdict(list)
    for path in oracle_files:
        doc = load_json(path)
        oracle_by_floor[int(doc.get("Floor", 0) or 0)].append(doc)

    mod_by_floor: dict[int, list[dict[str, Any]]] = collections.defaultdict(list)
    for path in mod_files:
        doc = load_json(path)
        mod_by_floor[int(doc.get("Floor", 0) or 0)].append(doc)

    rows: list[dict[str, Any]] = []
    all_floors = sorted(set(oracle_by_floor) | set(mod_by_floor))
    for floor in all_floors:
        o_list = oracle_by_floor.get(floor, [])
        m_list = mod_by_floor.get(floor, [])
        o_entries = [e for o in o_list for e in o.get("Entries", [])]
        o = oracle_totals(o_entries)
        m_sources = [s for m in m_list for s in m.get("Sources", [])]
        m = source_totals(m_sources)
        mod_plays: collections.Counter[str] = collections.Counter()
        for source in m_sources:
            if source.get("TimesPlayed"):
                mod_plays[source.get("SourceId", "")] += int(source.get("TimesPlayed", 0) or 0)
        card_diffs = {
            card: {"oracle": o["card_plays"].get(card, 0), "mod": mod_plays.get(card, 0)}
            for card in sorted(set(o["card_plays"]) | set(mod_plays))
            if o["card_plays"].get(card, 0) != mod_plays.get(card, 0)
        }
        status = "PASS"
        if not m_list:
            status = "MISSING_MOD"
        elif not o_list:
            status = "MISSING_ORACLE"
        else:
            checks = [
                ("damage", o["damage"], m["damage"]),
                ("block", o["block"], m["block"]),
                ("plays", o["plays"], m["plays"]),
                ("draws", o["draws"], m["draws"]),
            ]
            if any(a != b for _, a, b in checks):
                status = "DIFF"

        encounter = next((x.get("EncounterId") for x in o_list if x.get("EncounterId")), None)
        rows.append({
            "floor": floor,
            "status": status,
            "encounter": encounter,
            "oracle": {k: o[k] for k in ("damage", "block", "plays", "draws")},
            "mod": {k: m[k] for k in ("damage", "block", "plays", "draws", "heal")},
            "oracle_entry_count": len(o_entries),
            "mod_file_count": len(m_list),
            "oracle_card_plays": dict(o["card_plays"]),
            "oracle_damage_by_card": dict(o["damage_by_card"]),
            "oracle_powers": dict(o["powers"]),
            "card_play_diffs": card_diffs,
        })

    if args.json:
        print(json.dumps({"run_key": key, "rows": rows}, ensure_ascii=False, indent=2))
    else:
        print(f"run={key}")
        print(f"oracle_files={len(oracle_files)} mod_files={len(mod_files)}")
        print(f"{'floor':>5} {'status':<14} {'damage':>16} {'block':>14} {'plays':>12} {'draws':>12}  encounter")
        for r in rows:
            o, m = r["oracle"], r["mod"]
            print(
                f"{r['floor']:>5} {r['status']:<14} "
                f"{o['damage']:>7}/{m['damage']:<7} "
                f"{o['block']:>6}/{m['block']:<6} "
                f"{o['plays']:>5}/{m['plays']:<5} "
                f"{o['draws']:>5}/{m['draws']:<5}  {r['encounter'] or ''}"
            )
        for r in rows:
            if r.get("card_play_diffs"):
                print(f"  floor {r['floor']} card-play diffs: {r['card_play_diffs']}")
        bad = [r for r in rows if r["status"] != "PASS"]
        print(f"\nsummary: pass={len(rows)-len(bad)} nonpass={len(bad)} total={len(rows)}")
        for r in bad:
            print(f"  floor {r['floor']}: {r['status']} {r['encounter'] or ''}")
    return 0 if all(r["status"] == "PASS" for r in rows) else 1


if __name__ == "__main__":
    sys.exit(main())
