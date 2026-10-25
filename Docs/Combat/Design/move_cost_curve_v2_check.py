#!/usr/bin/env python3
"""Re-verify MOVE_COST_CURVE_V2 claims against the 2026-10-20 02:15 snapshot.

Usage:
  python3 move_cost_curve_v2_check.py /path/to/snapshot [/path/to/analysis.md]

Prints PASS/FAIL per claim. Writes /tmp/mcc_out/facts.json and tables.md.
Does not modify the snapshot. Stdlib only.
"""
from __future__ import annotations

import csv
import json
import math
import os
import re
import statistics
import sys
from pathlib import Path

OUT = Path(os.environ.get("MCC_OUT", "/tmp/mcc_out"))
RARITY = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]
TYPES = ["None", "Node", "Gas", "Block", "Encrypted", "Burned", "Frozen", "Forked",
         "Mined", "Staked", "Compressed", "Bot", "Glitched", "Shard", "Meme"]
# 2026-10-17 cost-curve retune (Docs/Combat/Design/MOVE_COST_CURVE_ANALYSIS.md).
# old/proposed are what that note asked. Landing is judged on THIS snapshot.
PROPOSALS = [
    # (id, move, kind, old, proposed, note)
    ("P01", "rapid_strike", "power", 52, 36, "1 AP priority finisher down to the top of the 1 AP band"),
    ("P02", "lean_hit", "power", 46, 34, "best non-priority 1 AP chip under the band cap"),
    ("P03", "baseline_jab", "power", 16, 22, "typeless 1 AP chip to 0.75 of the 1 AP center"),
    ("P04", "chaos_strike", "random_mean", 1.125, 1.0, "keep the swing, mean back to 1.0 (1.35|0.65)"),
    ("P05", "refined_striker", "power", 42, 68, "typeless rare lines up with the typed common spine"),
    ("P06", "refined_shot", "power", 38, 68, "same typeless-rare line"),
    ("P07", "refined_thrust", "power", 34, 62, "a step under the plain rare because the Def drop runs"),
    ("P08", "baseline_striker", "power", 30, 48, "typeless common 2 AP single at 0.75 of the spine"),
    ("P09", "baseline_shot", "power", 26, 48, "same line; later decision keeps Pulse Shot at 26"),
    ("P10", "baseline_thrust", "power", 26, 44, "under Snap Strike because the Def drop runs"),
    ("P11", "baseline_slam", "power", 30, 64, "3 AP typeless common center; ladder instead cut the cost"),
    ("P12", "baseline_snipe", "power", 30, 60, "same center"),
    ("P13", "baseline_multi_strike", "power", 8, 16, "same center, hit spread stays"),
    ("P14", "guardbreaker_combo", "power", 10, 23, "typeless rare 3 AP lines up with the typed 3 AP center"),
    ("P15", "overdraw_snipe", "power", 46, 85, "typeless rare 3 AP center"),
    ("P16", "refined_slam", "power", 48, 90, "same center"),
    ("P17", "refined_volley", "power", 22, 39, "typeless rare 2 AP spread"),
    ("P18", "refined_blast", "power", 28, 46, "typeless rare 3 AP spread"),
    ("P19", "refined_barrage", "power", 12, 18, "same spread target"),
    ("P20", "baseline_volley", "power", 16, 28, "typeless common 2 AP spread"),
    ("P21", "baseline_barrage", "power", 10, 11, "same target"),
    ("P22", "baseline_blast", "power", 22, 33, "typeless common 3 AP spread"),
    ("P23", "quick_lunge", "power", 26, 46, "priority rare stays under the spine"),
    ("P24", "multi_thread_shot", "power", 12, 20, "typed Bot chip beats typeless and stays under the spine"),
    ("P25", "seam_cut", "power", 52, 64, "neutral hit matches the spine"),
    ("P26", "ap_siphon", "power", 56, 70, "3 AP Gas hit on the 3 AP center"),
    ("P27", "cinder_burst", "power", 18, 24, "typed rare 3 AP multi-hit"),
    ("P28", "blade_flurry", "power", 18, 24, "typed rare 2 AP center"),
    ("P29", "zipped_strikes", "power", 26, 32, "typed uncommon on the spine"),
    ("P30", "frost_shards", "power", 12, 25, "raise power, do not cut the cost"),
    ("P31", "chain_fork", "power", 12, 25, "belongs beside Fork Split"),
    ("P32", "stone_barrage", "power", 16, 30, "belongs on the 3 AP center"),
    ("P33", "civet_beam", "ap", 2, 1, "Bean Beam down to 1 AP; ladder said leave it at 2"),
    ("P34", "bulwark_blow", "ap", 3, 2, "epic Defense hit pressable on turn 1"),
    ("P35", "frost_shards", "ap", 3, 3, "curve said stay at 3; ladder cut weak 3s to 2"),
    ("P36", "chain_fork", "ap", 3, 3, "curve said stay at 3"),
    ("P37", "stone_barrage", "ap", 3, 3, "curve said stay at 3"),
    ("P38", "baseline_slam", "ap", 3, 3, "curve said raise power and keep 3"),
    ("P39", "fork_split", "ap", 3, 3, "curve said stay 3; AP note wanted 5"),
    ("P40", "shard_scatter", "ap", 3, 3, "curve said stay 3"),
    ("P41", "ember_shower", "ap", 3, 3, "curve said stay 3"),
    ("P42", "automation_burst", "ap", 3, 3, "curve said stay 3"),
    ("P43", "hammer_sync", "ap", 2, 2, "curve said stay 2; AP note wanted 4"),
    ("P44", "cryo_ray", "ap", 2, 2, "curve said stay 2"),
    ("P45", "deep_dig", "ap", 3, 3, "curve said stay 3"),
    ("P46", "interest_strike", "power", 18, 30, "neutral damage on the 3 AP center"),
    ("P47", "interest_strike", "ap", 3, 3, "curve kept 3; ladder cut to 2"),
    ("P48", "solid_strike", "power", 64, 64, "leave the 2 AP spine"),
    ("P49", "coordinated_shot", "power", 64, 64, "leave the spine"),
    ("P50", "scorch_shot", "power", 64, 64, "leave the spine"),
    ("P51", "frost_shot", "power", 64, 64, "leave the spine"),
]


def read_csv(path: Path):
    rows = []
    line_of = {}
    with path.open(encoding="utf-8", newline="") as f:
        raw_lines = f.readlines()
    # Keep a parallel line map. DictReader on a filtered stream loses file lines, so parse with csv on the full text
    # and match MoveId back to the source line.
    text_lines = []
    for i, line in enumerate(raw_lines, 1):
        if line.startswith("#"):
            continue
        text_lines.append((i, line))
    content = "".join(l for _, l in text_lines)
    reader = csv.DictReader(content.splitlines())
    records = list(reader)
    # Map by walking non-comment lines after the header.
    header_done = False
    idx = 0
    for i, line in text_lines:
        if not header_done:
            header_done = True
            continue
        if idx < len(records):
            mid = records[idx].get("MoveId") or records[idx].get("SpeciesId") or records[idx].get("StatusId")
            if mid:
                line_of[mid] = i
            idx += 1
    return records, line_of


def find_line(path: Path, pattern: str) -> int | None:
    rx = re.compile(pattern)
    for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        if rx.search(line):
            return i
    return None


def tests_mentioning(root: Path, token: str) -> list[str]:
    hits = []
    base = root / "Assets/Scripts/Combat/Tests"
    if not base.exists():
        return hits
    for p in sorted(base.rglob("*.cs")):
        try:
            text = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        if token in text:
            hits.append(p.name)
    return hits


def first_test(root: Path, token: str) -> str:
    hits = tests_mentioning(root, token)
    return hits[0] if hits else "no test"


class Ledger:
    def __init__(self):
        self.rows = []

    def add(self, cid: str, ok: bool, evidence: str, detail: str = "", kind: str = "fact"):
        self.rows.append(dict(id=cid, ok=bool(ok), evidence=evidence, detail=detail, kind=kind))
        flag = "PASS" if ok else "FAIL"
        extra = f" | {detail}" if detail else ""
        print(f"{flag} {cid} {evidence}{extra}")

    def tally(self):
        p = sum(1 for r in self.rows if r["ok"])
        f = sum(1 for r in self.rows if not r["ok"])
        return p, f


def fnum(x, nd=2):
    return round(float(x) + 0.0, nd)


def neutral_condition_passes(token: str) -> bool:
    """Fresh turn: full HP, 0 stages, no statuses, equal speed, foe still has AP, 0 buffed partners."""
    if not token or not token.strip():
        return True
    parts = token.split(":")
    kind = parts[1] if len(parts) > 1 else parts[0]
    thresh = 0.0
    if len(parts) > 2:
        try:
            thresh = float(parts[2])
        except ValueError:
            thresh = 0.0
    if kind in ("HPBelowPercent", "APAtMost", "SpeedHigherThanOther", "WasHitLastTurn"):
        return False
    if kind in ("StageHigherThanOtherStage",):
        return False  # 0 is not higher than 0
    if kind in ("StageAtLeastOtherStage",):
        return True
    if kind in ("NoBuffs",):
        return True
    if kind in ("DebuffCountAtLeast", "StatusCount", "BuffedPartnerCountAtLeast", "BuffCountAtLeast"):
        return thresh <= 0
    if kind in ("HPAtMostPercent",):
        return False
    return False


def condition_count(token: str) -> int:
    if not token:
        return 0
    parts = token.split(":")
    kind = parts[1] if len(parts) > 1 else ""
    if kind == "BuffedPartnerCountAtLeast":
        return 0
    return 0


def neutral_damage_mult(row: dict) -> float:
    cond_raw = (row.get("ConditionalDamageConditions") or "").strip()
    require_all = (row.get("ConditionalDamageRequireAll") or "").strip().lower() != "false"
    tokens = [t for t in cond_raw.split("|") if t]
    if tokens:
        passed = [neutral_condition_passes(t) for t in tokens]
        ok = all(passed) if require_all else any(passed)
        if not ok:
            return 1.0
    try:
        mult = float(row.get("ConditionalDamageMultiplier") or "") or 1.0
    except ValueError:
        mult = 1.0
    if mult <= 0:
        mult = 1.0
    scaling = (row.get("ConditionalDamageScalingCondition") or "").strip()
    try:
        bonus = float(row.get("ConditionalDamageBonusPerCount") or "") or 0.0
    except ValueError:
        bonus = 0.0
    try:
        cap = float(row.get("ConditionalDamageMaxMultiplier") or "") or 0.0
    except ValueError:
        cap = 0.0
    if scaling and bonus:
        mult += bonus * condition_count(scaling)
    if cap > 0:
        mult = min(cap, mult)
    rand = (row.get("RandomDamageMultipliers") or "").strip()
    if rand:
        vals = []
        for bit in rand.split("|"):
            try:
                vals.append(float(bit))
            except ValueError:
                pass
        if vals:
            mult *= sum(vals) / len(vals)
    return mult


def utility_gated(row: dict) -> bool:
    """True when the damaging/utility payload's resolve gate passes on a fresh turn."""
    policy = (row.get("ResolveConditionalPolicy") or "").strip() or "LegacyAndResolveConditions"
    active = (row.get("ActiveConditionType") or "").strip() or "None"
    legacy = active in ("None", "")
    tokens = [t for t in (row.get("ResolveConditions") or "").split("|") if t]
    if not tokens:
        resolve = True
    else:
        require_all = (row.get("ResolveRequireAll") or "").strip().lower() != "false"
        flags = [neutral_condition_passes(t) for t in tokens]
        resolve = all(flags) if require_all else any(flags)
    if policy == "AlwaysApply":
        return True
    if policy == "LegacyActiveConditionOnly":
        return legacy
    if policy == "ResolveConditionsOnly":
        return resolve
    if policy == "LegacyOrResolveConditions":
        return legacy or resolve
    return legacy and resolve


def parse_stages(raw: str):
    out = []
    for part in filter(None, (raw or "").split("|")):
        b = part.split(":")
        if len(b) < 3:
            continue
        try:
            out.append(dict(stat=b[0], delta=int(float(b[1])), chance=float(b[2]),
                            self=len(b) > 3 and b[3].lower() == "true"))
        except ValueError:
            continue
    return out


def parse_statuses(raw: str):
    out = []
    for part in filter(None, (raw or "").split("|")):
        b = part.split(":")
        if not b or not b[0]:
            continue
        try:
            chance = float(b[2]) if len(b) > 2 and b[2] else 1.0
        except ValueError:
            chance = 1.0
        try:
            dur = int(float(b[1])) if len(b) > 1 and b[1] else 0
        except ValueError:
            dur = 0
        out.append(dict(id=b[0], dur=dur, chance=chance))
    return out


def parse_utils(raw: str):
    out = []
    for part in filter(None, (raw or "").split("|")):
        b = part.split(":")
        out.append(b)
    return out


def expected_hit_damage(power: float, atk: float, defn: float, mult: float, crit_p: float, crit_m: float,
                        rmin: float, rmax: float, steps: int = 21) -> float:
    if power <= 0 or defn <= 0:
        return 0.0
    total = 0.0
    for i in range(steps):
        u = rmin + (rmax - rmin) * (i / (steps - 1))
        base = (power * (atk / defn) * u + 1.0) * mult
        d0 = max(1, math.floor(base))
        d1 = max(1, math.floor(base * crit_m))
        total += (1.0 - crit_p) * d0 + crit_p * d1
    return total / steps


def shape_of(row) -> str:
    targeting = row["Targeting"]
    spread = targeting in ("AllEnemies", "Field")
    hmin = int(row["HitMin"] or 1)
    hmax = int(row["HitMax"] or 1)
    multi = hmax > hmin or hmin > 1
    if spread and multi:
        return "spread-multi"
    if spread:
        return "spread"
    if multi:
        return "multi"
    return "single"


def spread_factor(row) -> float:
    return 2.0 if row["Targeting"] in ("AllEnemies", "Field") else 1.0


def field_index(row) -> float:
    power = int(row["Power"] or 0)
    hmin = int(row["HitMin"] or 1)
    hmax = int(row["HitMax"] or 1)
    acc = int(row["Accuracy"] or 0)
    acc_f = 1.0 if acc <= 0 else acc / 100.0
    return power * ((hmin + hmax) / 2.0) * acc_f * spread_factor(row)


def legal_turn(cost: int) -> int:
    """First turn a bar that only regenerates +2 from 0 can pay `cost`."""
    if cost <= 0:
        return 1
    ap = 0
    for turn in range(1, 8):
        ap += 2
        if ap >= cost:
            return turn
    return 8


def rider_text(row) -> str:
    bits = []
    if "PRIOR" in (row.get("Tags") or "") or "PRIO" in (row.get("Tags") or ""):
        bits.append("priority")
    rc = int(row.get("RechargeTurns") or 0)
    if rc:
        bits.append(f"recharge {rc}")
    if "SpendRemainingAp" in (row.get("Tags") or ""):
        bits.append("spend remaining AP")
    if "BOSS_ONLY" in (row.get("Tags") or ""):
        bits.append("boss only")
    for s in parse_stages(row.get("StageChanges") or ""):
        who = "self" if s["self"] else "foe"
        bits.append(f"{s['stat']} {s['delta']:+d} {int(s['chance']*100)}% {who}")
    for st in parse_statuses(row.get("StatusEffects") or ""):
        bits.append(st["id"])
    for u in parse_utils(row.get("UtilityEffects") or ""):
        bits.append(u[0] if u else "")
    rand = (row.get("RandomDamageMultipliers") or "").strip()
    if rand:
        bits.append(f"random {rand}")
    if (row.get("DamageOverrideAttackStat") or "").strip().lower() == "true":
        bits.append(f"strikes with {row.get('DamageAttackStat') or 'Def'}")
    desc_bits = [b for b in bits if b]
    return ", ".join(desc_bits) if desc_bits else "—"


def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else os.environ.get("CHAINBOUND_SNAPSHOT", "/tmp/cb_snap"))
    analysis = Path(sys.argv[2]) if len(sys.argv) > 2 else None
    OUT.mkdir(parents=True, exist_ok=True)
    L = Ledger()

    moves_path = root / "Docs/Specs/Combat/Data/Moves.csv"
    pools_path = root / "Docs/Specs/Combat/Data/SpeciesMovePools.csv"
    status_path = root / "Docs/Specs/Combat/Data/Statuses.csv"
    moves, move_lines = read_csv(moves_path)
    pools, pool_lines = read_csv(pools_path)
    statuses, status_lines = read_csv(status_path)
    by_id = {r["MoveId"]: r for r in moves}
    st_by = {r["StatusId"]: r for r in statuses}

    note_path = root / "Docs/Combat/Design/MOVE_COST_CURVE_ANALYSIS.md"
    note = note_path.read_text(encoding="utf-8", errors="replace") if note_path.exists() else ""

    # config
    cfg_path = root / "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset"
    cfg_text = cfg_path.read_text(encoding="utf-8", errors="replace")

    def cfg(k, d):
        m = re.search(rf"(?m)^  {k}:\s*([-\d.]+)", cfg_text)
        return float(m.group(1)) if m else d

    ap_turn = int(cfg("apPerTurn", 2))
    ap_cap = int(cfg("apCap", 10))
    rmin = cfg("randomRollMin", 0.85)
    rmax = cfg("randomRollMax", 1.0)
    chem_adv = cfg("chemistryAdvantageFactor", 1.2)
    chem_dis = cfg("chemistryDisadvantageFactor", 0.8)
    crit_m = cfg("critMultiplierDefault", 1.5)
    crit_p = 0.0625

    # species
    species = {}
    for p in (root / "Assets/Data/Combat/ChainlingSpecies").glob("*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")
        def g(k, text=t):
            m = re.search(rf"(?m)^  {k}:\s*(.*)$", text)
            return m.group(1).strip() if m else ""
        if not g("speciesId"):
            continue
        species[g("speciesId")] = dict(
            type=int(g("primaryType") or 0),
            hp=int(g("baseMaxHP") or 0),
            ma=int(g("baseMeleeAtk") or 0),
            md=int(g("baseMeleeDef") or 0),
            ra=int(g("baseRangedAtk") or 0),
            rd=int(g("baseRangedDef") or 0),
            spd=int(g("baseSpeed") or 0),
            rarity=int(g("rarity") or 0),
            starter=g("isStarter") == "1",
            file=p.name,
        )

    # move assets
    assets = {}
    guid_to_move = {}
    move_dir = root / "Assets/Data/Combat/Moves"
    for p in move_dir.glob("Move_*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")
        def g(k, text=t):
            m = re.search(rf"(?m)^  {k}:\s*(.*)$", text)
            return m.group(1).strip() if m else None
        mid = g("moveId")
        if not mid:
            continue
        meta = Path(str(p) + ".meta")
        guid = ""
        if meta.exists():
            gm = re.search(r"(?m)^guid:\s*(\w+)", meta.read_text(encoding="utf-8", errors="replace"))
            guid = gm.group(1) if gm else ""
        assets[mid] = dict(
            power=int(g("power") or 0), ap=int(g("apCost") or 0), acc=int(g("accuracy") or 0),
            hmin=int(g("hitCountMin") or 1), hmax=int(g("hitCountMax") or 1),
            rc=int(g("rechargeTurns") or 0), rarity=int(g("rarity") or 0),
            name=g("displayName") or "", desc=(g("description") or ""), file=p.name, guid=guid,
        )
        if guid:
            guid_to_move[guid] = mid

    # species asset learnable guids
    species_asset_pools = {}
    for p in (root / "Assets/Data/Combat/ChainlingSpecies").glob("*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")
        sid_m = re.search(r"(?m)^  speciesId:\s*(\S+)", t)
        if not sid_m:
            continue
        guids = re.findall(r"guid:\s*(\w+)", t)
        # This also catches visual refs. Restrict to learnableMoves / exclusiveMoves / flagship / initial / off-type blocks.
        def block_guids(key):
            m = re.search(rf"(?m)^  {key}:\s*\n((?:  - .*\n)*)", t)
            if not m:
                # scalar flagship
                m2 = re.search(rf"(?m)^  {key}:.*guid:\s*(\w+)", t)
                return [m2.group(1)] if m2 else []
            return re.findall(r"guid:\s*(\w+)", m.group(0))
        ids = []
        for key in ("learnableMoves", "exclusiveMoves", "initialMoves", "offTypeMoveExceptions"):
            for g in block_guids(key):
                if g in guid_to_move:
                    ids.append(guid_to_move[g])
        flag = block_guids("flagshipMove")
        species_asset_pools[sid_m.group(1)] = dict(moves=sorted(set(ids)), flagship=guid_to_move.get(flag[0]) if flag else None)

    # move_ap.ts
    ap_ts = (root / "Supabase/supabase/functions/_shared/move_ap.ts").read_text(encoding="utf-8", errors="replace")
    block = ap_ts.split("MOVE_AP_COST", 1)[-1].split("};", 1)[0]
    ts_cost = {k: int(v) for k, v in re.findall(r'"([a-z0-9_]+)":\s*(-?\d+)', block)}

    # chemistry matrix
    matrix_text = (root / "Assets/Data/Combat/Type/Chemistry/TypeChemistryMatrix.asset").read_text(encoding="utf-8", errors="replace")
    status_enum = (root / "Assets/Scripts/Combat/Runtime/Data/StatusId.cs").read_text(encoding="utf-8", errors="replace")
    status_ids = {int(v): k for k, v in re.findall(r"(\w+)\s*=\s*(\d+)", status_enum)}
    matrix = {}
    for m in re.finditer(r"attackerType: (\d+)\s+defenderType: (\d+)\s+output:\s+statusId: (\d+)\s+recipient: (\d+)", matrix_text):
        matrix[(int(m.group(1)), int(m.group(2)))] = (status_ids.get(int(m.group(3))), int(m.group(4)))
    meme = TYPES.index("Meme")
    meme_cells = [(a, d, v) for (a, d), v in matrix.items() if a == meme or d == meme]

    # learn sets
    def split_ids(s):
        return [x for x in (s or "").split("|") if x]

    learn = {}  # sid -> dict
    for row in pools:
        sid = row["SpeciesId"]
        learnable = split_ids(row["LearnableMoves"])
        exclusive = split_ids(row["ExclusiveMoves"])
        initial = split_ids(row["InitialMoves"])
        offtype = split_ids(row["OffTypeMoves"])
        flagship = (row.get("FlagshipMove") or "").strip()
        pool = []
        for m in learnable + exclusive + initial + offtype + ([flagship] if flagship else []):
            if m not in pool:
                pool.append(m)
        learn[sid] = dict(learnable=learnable, exclusive=exclusive, initial=initial, offtype=offtype,
                          flagship=flagship, pool=pool, band=learnable + exclusive, line=pool_lines.get(sid))

    damaging_cats = {"DamagingMelee", "DamagingRanged"}

    # ---- headline measurements ----
    n_rows = len(moves)
    n_dmg = sum(1 for r in moves if r["Category"] in damaging_cats)
    n_passive = sum(1 for r in moves if r["Category"] == "Passive")
    L.add("C01", n_rows == len(by_id), f"Moves.csv rows={n_rows} unique ids={len(by_id)}", f"{moves_path.name}")
    L.add("C02", True, f"damaging rows={n_dmg} passive rows={n_passive}", "Category DamagingMelee|DamagingRanged vs Passive")

    from collections import Counter
    ap_counts = Counter(int(r["APCost"] or 0) for r in moves if r["Category"] != "Passive")
    rar_counts = Counter(r["Rarity"] for r in moves)
    L.add("C03", True, "non-passive AP census " + ", ".join(f"{k}:{ap_counts[k]}" for k in sorted(ap_counts)),
          "Moves.csv APCost")
    L.add("C04", True, "rarity census " + ", ".join(f"{k}:{rar_counts[k]}" for k in RARITY), "Moves.csv Rarity")

    spine = [r for r in moves if int(r["Power"] or 0) == 64 and int(r["APCost"] or 0) == 2
             and int(r["Accuracy"] or 0) == 95 and int(r["HitMin"] or 1) == 1 and int(r["HitMax"] or 1) == 1
             and r["Category"] in damaging_cats and r["Targeting"] == "SingleEnemy"]
    L.add("C05", True, f"power-64 acc-95 1-hit 2-AP single damaging rows={len(spine)} ids=" +
          ",".join(r["MoveId"] for r in spine), "the 10-17 spine was 13 typed commons at this number")

    ap4 = [r["MoveId"] for r in moves if int(r["APCost"] or 0) == 4 and r["Category"] != "Passive"]
    ap5 = [r["MoveId"] for r in moves if int(r["APCost"] or 0) == 5 and r["Category"] != "Passive"]
    ap6 = [r["MoveId"] for r in moves if int(r["APCost"] or 0) == 6 and r["Category"] != "Passive"]
    L.add("C06", True, f"4 AP non-passive ({len(ap4)}): " + ", ".join(ap4), "Moves.csv APCost")
    L.add("C07", len(ap5) == 0, f"5 AP non-passive count={len(ap5)}", "Moves.csv APCost")
    L.add("C08", ap6 == ["grind_tax"] or set(ap6) == {"grind_tax"}, f"6 AP non-passive={ap6}", "Moves.csv APCost")

    # parity ts
    ts_miss = []
    for r in moves:
        mid = r["MoveId"]
        csv_ap = int(r["APCost"] or 0)
        if mid not in ts_cost:
            ts_miss.append(f"{mid} missing from move_ap.ts")
        elif ts_cost[mid] != csv_ap:
            ts_miss.append(f"{mid} csv {csv_ap} ts {ts_cost[mid]}")
    extra_ts = [k for k in ts_cost if k not in by_id]
    L.add("C09", not ts_miss and not extra_ts,
          f"move_ap.ts vs Moves.csv mismatches={len(ts_miss)} extra_ts={len(extra_ts)}",
          "; ".join(ts_miss[:8] + extra_ts[:4]) or "all  costs match",
          kind="flag")

    # asset parity
    asset_miss = []
    for r in moves:
        mid = r["MoveId"]
        if mid not in assets:
            asset_miss.append(f"{mid} no asset")
            continue
        a = assets[mid]
        checks = [
            ("power", int(r["Power"] or 0), a["power"]),
            ("ap", int(r["APCost"] or 0), a["ap"]),
            ("acc", int(r["Accuracy"] or 0), a["acc"]),
            ("hmin", int(r["HitMin"] or 1), a["hmin"]),
            ("hmax", int(r["HitMax"] or 1), a["hmax"]),
            ("rc", int(r["RechargeTurns"] or 0), a["rc"]),
        ]
        rar_i = RARITY.index(r["Rarity"]) if r["Rarity"] in RARITY else -1
        if rar_i != a["rarity"]:
            asset_miss.append(f"{mid} rarity csv {r['Rarity']} asset {a['rarity']}")
        for name, c, av in checks:
            if c != av:
                asset_miss.append(f"{mid} {name} csv {c} asset {av}")
    L.add("C10", not asset_miss, f"move asset vs CSV mismatches={len(asset_miss)}",
          "; ".join(asset_miss[:12]) or "power/ap/acc/hits/recharge/rarity match", kind="flag")

    # engine facts
    sp = root / "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs"
    cr = root / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs"
    elig = root / "Assets/Scripts/Combat/Runtime/Engine/CombatActionEligibilityService.cs"
    dmgc = root / "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs"
    regen_line = find_line(sp, r"Gain\(chainling, _resolverConfig\.apPerTurn")
    bench_line = find_line(cr, r"TickBenchedRecharge\(team, turn\)")
    tick_line = find_line(sp, r"void TickRechargeWhileBenched")
    fee_line = find_line(cr, r"void ApplyFeeCollectorDrains")
    spend_line = find_line(cr, r"HasSpendRemainingAp\(move\)")
    early0 = find_line(dmgc, r"if \(power <= 0\)")
    override_line = find_line(dmgc, r"if \(tuning\.overrideAttackStat\)")
    kindling_line = find_line(sp, r"0\.06f")
    cost_floor = find_line(elig, r"move\.apCost <= 0 \? Math\.Max\(0, adjusted\) : Math\.Max\(1, adjusted\)")
    L.add("C11", ap_turn == 2 and ap_cap == 10 and regen_line is not None,
          f"regen +{ap_turn} cap {ap_cap}",
          f"CombatResolverConfig_Default.asset apPerTurn/apCap; StatusProcessor.cs:{regen_line}")
    L.add("C12", bench_line is not None and tick_line is not None,
          "bench recharge ticks",
          f"CombatResolver.cs:{bench_line} calls StatusProcessor.cs:{tick_line}; test {first_test(root, 'BenchRechargeTicks')}")
    L.add("C13", fee_line is not None,
          "Fee Collector drains 1 from the first opposing actor when that actor has more AP",
          f"CombatResolver.cs:{fee_line}; test {first_test(root, 'FeeCollector')}")
    L.add("C14", early0 is not None and override_line is not None and override_line > early0,
          "power 0 still returns 0 before the attack-stat override is read",
          f"DamageCalculator.cs:{early0} then override at :{override_line}")
    L.add("C15", kindling_line is not None,
          "Kindling end-step damage is 6% Max HP",
          f"StatusProcessor.cs:{kindling_line}; test {first_test(root, 'Kindling_MarksOnce')}")
    L.add("C16", cost_floor is not None,
          "Streamlined discount cannot reduce a positive printed cost below 1",
          f"CombatActionEligibilityService.cs:{cost_floor}")

    # owned stage revert: GasSpike is not a special case; applyOnce stages are owned and reverted
    apply_owned = find_line(sp, r"status\.ApplyOwnedStage")
    revert_owned = find_line(sp, r"ownedStageDeltas")
    L.add("C17", apply_owned is not None and revert_owned is not None,
          "apply-once status stages are owned and reverted on expiry (covers Gas Spike and FUD)",
          f"StatusProcessor.cs:{apply_owned} and :{revert_owned}; test {first_test(root, 'GasSpikeAndFud_RestoreOwnedStagesOnExpiry')}")

    # strict higher gate
    higher_line = find_line(root / "Assets/Scripts/Combat/Runtime/Data/MoveResolveProfile.cs",
                            r"case MoveResolveConditionType\.StageHigherThanOtherStage")
    L.add("C18", higher_line is not None and "StageHigherThanOtherStage" in (by_id["rallying_slam"]["ResolveConditions"] or ""),
          "Rallying Slam and Interest Strike use a strict Def-stage > Atk-stage gate, so a fresh 0-vs-0 tie does not refund",
          f"MoveResolveProfile.cs:{higher_line}; Moves.csv:{move_lines.get('rallying_slam')} and :{move_lines.get('interest_strike')}; test {first_test(root, 'StageHigherThanOtherStageUsesStrictComparison')}")

    # all-in
    all_in = by_id["all_in"]
    L.add("C19", int(all_in["APCost"]) == 4 and all_in["Rarity"] == "Epic" and int(all_in["RechargeTurns"]) == 2
          and "SpendRemainingAp" in (all_in["Tags"] or "") and int(all_in["Power"]) == 98,
          f"All-In power {all_in['Power']} AP {all_in['APCost']} rarity {all_in['Rarity']} recharge {all_in['RechargeTurns']} tag SpendRemainingAp",
          f"Moves.csv:{move_lines['all_in']}; CombatResolver.cs:{spend_line}; test {first_test(root, 'AllIn_SpendsRemainingAp')}")
    L.add("C20", "all_in" not in {m for s in learn.values() for m in s["pool"]},
          "All-In is in no species learn pool",
          "SpeciesMovePools.csv")

    # built-out moves
    def has_op(mid, op):
        return op in (by_id[mid].get("UtilityEffects") or "")

    L.add("C21", int(by_id["baseline_shot"]["Power"]) == 26 and int(by_id["baseline_shot"]["APCost"]) == 2,
          f"Pulse Shot power {by_id['baseline_shot']['Power']} AP {by_id['baseline_shot']['APCost']}",
          f"Moves.csv:{move_lines['baseline_shot']}")
    L.add("C22", has_op("system_reset", "StripAllStatuses") and int(by_id["system_reset"]["APCost"]) == 3
          and int(by_id["system_reset"]["RechargeTurns"]) == 2,
          f"System Reset AP {by_id['system_reset']['APCost']} recharge {by_id['system_reset']['RechargeTurns']} utility StripAllStatuses",
          f"Moves.csv:{move_lines['system_reset']}; text={by_id['system_reset']['Description']!r}")
    L.add("C23", "skip your next action" in by_id["system_reset"]["Description"].lower(),
          "System Reset card text says it skips the next action; recharge only locks System Reset",
          f"Moves.csv:{move_lines['system_reset']}; IsMoveRecharging matches rechargingMoveId ({first_test(root, 'IsMoveRecharging')})",
          kind="flag")
    L.add("C24", has_op("hype_burst", "StripBuffsThenStage") and int(by_id["hype_burst"]["APCost"]) == 2,
          "Hype Burst strips up to 2 buffs and grants Atk +1 each, 2 AP",
          f"Moves.csv:{move_lines['hype_burst']}")
    L.add("C25", has_op("crystal_extraction", "HealSelfPerTargetDebuff") and has_op("crystal_extraction", "Cleanse"),
          "Crystal Extraction heals 5% per foe debuff then cleanses them",
          f"Moves.csv:{move_lines['crystal_extraction']}")
    L.add("C26", has_op("purge_protocol", "Cleanse"),
          "Purge Protocol utility includes Cleanse",
          f"Moves.csv:{move_lines['purge_protocol']}")
    L.add("C27", "uplink" in learn["validape"]["pool"] and "ap_router" not in learn["validape"]["pool"],
          "Validape learns Uplink and does not learn AP Router",
          f"SpeciesMovePools.csv:{learn['validape']['line']}")
    L.add("C28", "packet_skim" in learn["kitsauto"]["pool"] and has_op("packet_skim", "DrainAP")
          and int(by_id["packet_skim"]["APCost"]) == 1 and by_id["packet_skim"]["BaseType"] == "Bot",
          "Kitsauto learns Packet Skim, a 1 AP Bot drain of 1",
          f"SpeciesMovePools.csv:{learn['kitsauto']['line']}; Moves.csv:{move_lines['packet_skim']}")

    for cid, mid in (("C29", "cut_ledger"), ("C30", "batch_debit"), ("C31", "rime_jab"), ("C32", "vested_strike")):
        L.add(cid, mid not in by_id, f"10-17 concept {mid} is not in Moves.csv", "proposed then, still absent")

    # priority attacks
    prio = []
    for r in moves:
        tags = r.get("Tags") or ""
        if r["Category"] in damaging_cats and ("PRIOR" in tags or re.search(r"(^|\|)PRIO(\||$)", tags)):
            prio.append(r)
    L.add("C33", sorted(r["MoveId"] for r in prio) == ["baseline_jab", "quick_lunge", "rapid_strike"],
          "damaging PRIOR moves: " + ", ".join(f"{r['MoveId']} {r['DisplayName']} {r['BaseType']} P{r['Power']} AP{r['APCost']} {r['Category']}" for r in prio),
          "Moves.csv Tags")

    # meme chemistry
    L.add("C34", len(meme_cells) == 0,
          f"chemistry matrix cells where Meme is attacker or defender: {len(meme_cells)}",
          "TypeChemistryMatrix.asset")
    L.add("C35", abs(chem_adv - 1.2) < 1e-6 and abs(chem_dis - 0.8) < 1e-6,
          f"chemistry factors advantage {chem_adv} disadvantage {chem_dis}",
          "CombatResolverConfig_Default.asset")

    # boss only in pools
    boss_in_pool = []
    for sid, info in learn.items():
        for mid in info["pool"]:
            tags = by_id.get(mid, {}).get("Tags") or ""
            if "BOSS_ONLY" in tags:
                boss_in_pool.append(f"{sid}:{mid}")
    L.add("C36", not boss_in_pool, f"BOSS_ONLY moves inside a learn pool: {boss_in_pool or 'none'}",
          "Moves.csv Tags vs SpeciesMovePools.csv", kind="flag")

    # field index self-check
    pulse = by_id["baseline_shot"]
    L.add("C37", abs(field_index(pulse) - 24.70) < 0.001,
          f"field index Pulse Shot={field_index(pulse):.2f} (power*hits*acc*spread)",
          f"Moves.csv:{move_lines['baseline_shot']}")

    # pool rule
    pool_breaks = []
    for sid, info in learn.items():
        if sid in ("cofi_lootwak", "ghost"):
            continue
        spc = species.get(sid)
        if not spc:
            pool_breaks.append(f"{sid} missing species asset")
            continue
        band = [m for m in info["band"] if m in by_id]
        sig = info["flagship"]
        if spc["starter"]:
            if not sig or sig not in band:
                pool_breaks.append(f"{sid} starter signature {sig} not in band {band}")
                continue
            top = max(RARITY.index(by_id[m]["Rarity"]) for m in band)
            at_top = [m for m in band if RARITY.index(by_id[m]["Rarity"]) == top]
            if at_top != [sig] and not (len(at_top) == 1 and at_top[0] == sig):
                pool_breaks.append(f"{sid} starter top rarity not unique signature: {at_top} sig {sig}")
            continue
        if len(band) != 5:
            pool_breaks.append(f"{sid} wild band size {len(band)} {band}")
            continue
        if sig not in band:
            pool_breaks.append(f"{sid} signature {sig} not in band")
            continue
        body = spc["rarity"]
        others = [m for m in band if m != sig]
        sig_r = RARITY.index(by_id[sig]["Rarity"])
        if any(RARITY.index(by_id[m]["Rarity"]) >= sig_r for m in others):
            pool_breaks.append(f"{sid} signature {sig} ({by_id[sig]['Rarity']}) is not strictly highest")
        if any(RARITY.index(by_id[m]["Rarity"]) > body + 1 for m in others):
            pool_breaks.append(f"{sid} a non-signature sits more than one step above body {RARITY[body]}")
        low = [m for m in others if RARITY.index(by_id[m]["Rarity"]) <= body]
        if len(low) < 3:
            pool_breaks.append(f"{sid} only {len(low)} moves at or under body {RARITY[body]}: {others}")
    L.add("C38", not pool_breaks,
          f"learn-pool rarity-tier breaks={len(pool_breaks)}",
          "; ".join(pool_breaks) or "wild 3+1+strict signature, starters unique top rarity; Ghost and Cofi skipped",
          kind="flag")

    # species asset pool vs csv band+offtype+initial (order-insensitive on the union the asset actually stores)
    pool_asset_miss = []
    for sid, info in learn.items():
        asset_set = set(species_asset_pools.get(sid, {}).get("moves") or [])
        csv_set = set(info["pool"])
        if asset_set != csv_set:
            pool_asset_miss.append(f"{sid} csv-only {sorted(csv_set-asset_set)} asset-only {sorted(asset_set-csv_set)}")
    meta_n = len(list(root.rglob("*.meta")))
    L.add("C39", meta_n == 0,
          f"snapshot .meta files={meta_n}; species assets store learnable moves as Unity guids and cannot be joined to move ids",
          "CSV is the pool source. Move asset power/AP/accuracy/hits/recharge/rarity are checked in C10.")

    # card text: key limits
    text_flags = []
    for r in moves:
        mid = r["MoveId"]
        desc = (r.get("Description") or "")
        low = desc.lower()
        rc = int(r.get("RechargeTurns") or 0)
        tags = r.get("Tags") or ""
        if r["Category"] in damaging_cats and ("PRIOR" in tags) and not any(w in low for w in ("priority", "first", "before")):
            text_flags.append(f"{mid} PRIOR not in text")
        if rc >= 2 and rc < 100 and not any(w in low for w in ("recharg", "skip", "can't use", "cannot", "next turn", "again")):
            text_flags.append(f"{mid} recharge {rc} not in text")
        if "SpendRemainingAp" in tags and not any(w in low for w in ("spend", "all your ap", "all ap")):
            text_flags.append(f"{mid} spend-remaining not in text")
        util = r.get("UtilityEffects") or ""
        if ("DrainAP" in util or "StealAP" in util) and "ap" not in low:
            text_flags.append(f"{mid} AP change not in text")
        if "BOSS_ONLY" in tags and "boss" not in low:
            text_flags.append(f"{mid} boss-only not in text")
        if r["Category"] in damaging_cats and int(r["Power"] or 0) == 0:
            text_flags.append(f"{mid} damaging power 0")
    L.add("C40", True, f"card-text key-limit flags={len(text_flags)}",
          "; ".join(text_flags[:20]) or "none", kind="fact")

    # ---- pricing ----
    playable_sids = [s for s in learn if s in species and species[s]["hp"] > 0]
    med_hp = statistics.median(species[s]["hp"] for s in playable_sids)
    med_md = statistics.median(species[s]["md"] for s in playable_sids)
    med_rd = statistics.median(species[s]["rd"] for s in playable_sids)
    med_ma = statistics.median(species[s]["ma"] for s in playable_sids)
    med_ra = statistics.median(species[s]["ra"] for s in playable_sids)

    def strike(row, atk, defn):
        power = int(row["Power"] or 0)
        mult = neutral_damage_mult(row)
        if (row.get("DamageOverrideAttackStat") or "").lower() == "true":
            # caller passes the override stat as atk
            pass
        per = expected_hit_damage(power, atk, defn, mult, crit_p, crit_m, rmin, rmax)
        hmin = int(row["HitMin"] or 1)
        hmax = int(row["HitMax"] or 1)
        hits = (hmin + hmax) / 2.0
        acc = int(row["Accuracy"] or 0)
        acc_f = 1.0 if acc <= 0 else acc / 100.0
        return per * hits * acc_f * spread_factor(row)

    # provisional AP point from 2 AP single damaging norm strikes (ratio 1)
    norm_cache = {}
    for r in moves:
        if r["Category"] not in damaging_cats:
            continue
        norm_cache[r["MoveId"]] = strike(r, 60.0, 60.0)

    twos = [norm_cache[r["MoveId"]] for r in moves
            if r["Category"] in damaging_cats and int(r["APCost"] or 0) == 2 and shape_of(r) == "single"
            and int(r["Power"] or 0) > 0]
    ap_point = (statistics.median(twos) / 2.0) if twos else 30.0
    # one reference hit for mitigation pricing: median per-hit of those 2 AP singles, approx median/1
    ref_hit = statistics.median(twos) if twos else 60.0

    def ap_credit(amount, chance, kind):
        # kind drain 0.6, gain 1.0, steal = both
        exp = amount * chance
        if kind == "drain":
            return 0.6 * exp * ap_point
        if kind == "gain":
            return 1.0 * exp * ap_point
        if kind == "steal":
            return (0.6 + 1.0) * exp * ap_point
        return 0.0

    def stage_credit(delta, chance, stat, hits, acc_f):
        if stat in ("Speed", "Accuracy", "Evasion"):
            return 0.0  # tempo, not HP, on this screen
        # +1 atk or -1 foe def is +50% of one future reference hit. Credit one future hit, not the whole fight.
        sign = 1 if (delta > 0) else 1  # both a self buff and a foe def drop are damage
        mag = min(2, abs(delta))
        return 0.5 * mag * ref_hit * chance * hits * acc_f

    def rider_value(row, strike_dmg):
        """Neutral-turn rider HP. Only opcodes the fresh-turn gate allows."""
        gated = utility_gated(row)
        timing = (row.get("ResolveUtilityTiming") or "").strip()
        damaging = row["Category"] in damaging_cats
        util_live = gated and ((not damaging) or timing in ("AfterEachConfirmedHit", "AfterMoveComplete", "ResolveBeforeFirstHit"))
        # stages/statuses on a damaging move are inside ShouldApplyMoveEffects, which uses the same gate
        effects_live = gated
        acc = int(row["Accuracy"] or 0)
        acc_f = 1.0 if acc <= 0 else acc / 100.0
        if not damaging:
            acc_f = 1.0
        hmin = int(row["HitMin"] or 1)
        hmax = int(row["HitMax"] or 1)
        hits = (hmin + hmax) / 2.0
        once = (row.get("MoveEffectsOncePerTarget") or "").strip().lower() == "true"
        applications = 1.0 if once or not damaging else hits
        credit = 0.0
        notes = []
        if effects_live:
            targets = spread_factor(row)
            for s in parse_stages(row.get("StageChanges") or ""):
                c = stage_credit(s["delta"], s["chance"], s["stat"], applications, acc_f) * (targets if not s["self"] else 1.0)
                credit += c
                if s["stat"] in ("Speed", "Accuracy", "Evasion") and s["chance"] > 0:
                    notes.append(f"tempo {s['stat']} {s['delta']:+d}")
            for st in parse_statuses(row.get("StatusEffects") or ""):
                sd = st_by.get(st["id"])
                if not sd:
                    notes.append(f"status {st['id']} unpriced")
                    continue
                try:
                    eot = float(sd.get("EoTDamagePercent") or 0)
                except ValueError:
                    eot = 0.0
                try:
                    eot_ap = int(float(sd.get("EoTAPChange") or 0))
                except ValueError:
                    eot_ap = 0
                dur = st["dur"] or int(sd.get("Duration") or 0)
                credit += eot * dur * med_hp * st["chance"] * acc_f * targets
                if eot_ap < 0:
                    credit += ap_credit(-eot_ap * dur, st["chance"] * acc_f, "drain") * targets
                    notes.append(f"{st['id']} {-eot_ap}/turn x{dur}")
                elif eot_ap > 0:
                    credit += ap_credit(eot_ap * dur, st["chance"] * acc_f, "gain")
        if util_live:
            for u in parse_utils(row.get("UtilityEffects") or ""):
                op = u[0]
                if op in ("DrainAP", "GainAP", "StealAP") and len(u) >= 3:
                    try:
                        amt = float(u[1])
                        ch = float(u[2].rstrip("%")) / 100.0 if "%" in u[2] else float(u[2]) / 100.0
                    except ValueError:
                        continue
                    kind = {"DrainAP": "drain", "GainAP": "gain", "StealAP": "steal"}[op]
                    apps = applications if timing == "AfterEachConfirmedHit" and damaging else 1.0
                    credit += ap_credit(amt, ch * (acc_f if damaging else 1.0), kind) * apps
                elif op == "NextMoveAPSurcharge" and len(u) >= 3:
                    try:
                        amt = float(u[1]); ch = float(u[2].rstrip("%")) / 100.0
                    except ValueError:
                        amt, ch = 1.0, 1.0
                    credit -= ap_credit(amt, ch, "gain")  # the user pays it
                    notes.append(f"self surcharge +{amt:g}")
                elif op == "Cleanse":
                    credit += 0.15 * ref_hit
                elif op == "StripAllStatuses":
                    credit += 0.25 * ref_hit
                elif op == "StripBuffsThenStage":
                    credit += 0.20 * ref_hit
                elif op.startswith("Heal") or op == "HealSelfPerTargetDebuff":
                    # neutral foe has 0 debuffs, so per-debuff heal is 0 on this screen
                    if op == "HealSelfPerTargetDebuff":
                        notes.append("heal per debuff is 0 on a fresh foe")
                    else:
                        try:
                            pct = float(u[1]) / 100.0
                        except (ValueError, IndexError):
                            pct = 0
                        credit += pct * med_hp
                elif op == "MitigationWindow" and len(u) >= 3:
                    try:
                        mag = float(u[1].rstrip("%")) / 100.0
                        hits_b = int(float(u[3])) if len(u) > 3 else 1
                    except ValueError:
                        mag, hits_b = 0.0, 0
                    prevented = mag * (ref_hit if hits_b < 0 else ref_hit * max(hits_b, 0))
                    credit += prevented
                    notes.append(f"mit {mag:.0%} x{hits_b}")
                elif op == "FullAbsorbBarrier":
                    hits_b = 3
                    try:
                        hits_b = int(float(u[1]))
                    except (ValueError, IndexError):
                        pass
                    credit += ref_hit * hits_b
                    notes.append(f"absorb {hits_b}")
                elif op == "ArmUtilityWindow":
                    # Memory Leak shape: ...:-2:...
                    nums = []
                    for bit in u[1:]:
                        try:
                            nums.append(float(bit.strip("%")))
                        except ValueError:
                            continue
                    if nums and any(n <= -1 for n in nums):
                        cut = max(-n for n in nums if n < 0)
                        credit += ap_credit(cut, 1.0, "drain")
                        notes.append(f"next start {-cut:g} AP")
                elif op in ("RandomStageChange",):
                    notes.append("random stage")
                    credit += 0.15 * ref_hit * (acc_f if damaging else 1)
                else:
                    if op:
                        notes.append(op)
        # heal column
        try:
            heal = float(row.get("HealPercentMaxHP") or 0)
        except ValueError:
            heal = 0.0
        if heal and (not damaging or gated):
            credit += (heal / 100.0) * med_hp
        # priority is not HP on this screen
        if "PRIOR" in (row.get("Tags") or ""):
            notes.append("priority (tempo, not in SPR)")
        return credit, notes

    # learnable pairs
    records = []
    for sid, info in learn.items():
        if sid not in species:
            continue
        spc = species[sid]
        for mid in info["pool"]:
            if mid not in by_id:
                continue
            row = by_id[mid]
            if "BOSS_ONLY" in (row.get("Tags") or ""):
                continue
            melee = row["Category"] == "DamagingMelee"
            ranged = row["Category"] == "DamagingRanged"
            if (row.get("DamageOverrideAttackStat") or "").lower() == "true":
                atk_stat = (row.get("DamageAttackStat") or "MeleeDef")
                holder_atk = spc["md"] if "Def" in atk_stat else spc["ma"]
                holder_def = med_md if melee or "Melee" in atk_stat else med_rd
            elif melee:
                holder_atk, holder_def = spc["ma"], med_md
            elif ranged:
                holder_atk, holder_def = spc["ra"], med_rd
            else:
                holder_atk, holder_def = spc["ma"], med_md
            norm = norm_cache.get(mid, 0.0) if row["Category"] in damaging_cats else 0.0
            hold = strike(row, holder_atk, holder_def) if row["Category"] in damaging_cats else 0.0
            # crystal/bulwark norm should use ratio 1 as well (atk=def), already true
            rv, notes = rider_value(row, norm)
            spr = norm + rv
            ap = int(row["APCost"] or 0)
            per_ap = spr / ap if ap else None
            records.append(dict(
                species=sid, move=mid, name=row["DisplayName"], type=row["BaseType"], rarity=row["Rarity"],
                cat=row["Category"], ap=ap, power=int(row["Power"] or 0),
                hits=f"{row['HitMin']}-{row['HitMax']}", shape=shape_of(row),
                priority=("PRIOR" in (row.get("Tags") or "")),
                recharge=int(row["RechargeTurns"] or 0),
                rider=rider_text(row),
                norm=fnum(norm), rider_hp=fnum(rv), spr=fnum(spr),
                per_ap=None if per_ap is None else fnum(per_ap),
                holder=fnum(hold), holder_spr=fnum(hold + rv),
                legal=legal_turn(ap),
                off=mid in info["offtype"],
                flagship=mid == info["flagship"],
                band=mid in info["band"] or mid == info["flagship"],
                notes="; ".join(notes),
                line=move_lines.get(mid),
                index=fnum(field_index(row)) if row["Category"] in damaging_cats else 0,
                mult=fnum(neutral_damage_mult(row), 3),
            ))

    # unique learnable damaging for the curve (one row per move; holders listed)
    by_move = {}
    for rec in records:
        if rec["cat"] not in damaging_cats:
            continue
        slot = by_move.setdefault(rec["move"], dict(rec))
        slot.setdefault("holders", [])
        if rec["species"] not in slot["holders"]:
            slot["holders"].append(rec["species"])
        if rec["holder_spr"] > slot.get("best_holder_spr", -1):
            slot["best_holder_spr"] = rec["holder_spr"]
            slot["best_holder"] = rec["species"]
            slot["best_holder_dmg"] = rec["holder"]

    curve = list(by_move.values())
    for c in curve:
        c["holders"] = sorted(c["holders"])

    def med(xs):
        xs = [x for x in xs if x is not None]
        return statistics.median(xs) if xs else None

    # tier medians of norm SPR/AP
    tier_med = {}
    for ap in sorted(set(c["ap"] for c in curve)):
        vals = [c["per_ap"] for c in curve if c["ap"] == ap and c["per_ap"] is not None and c["power"] > 0]
        tier_med[ap] = med(vals)
    rar_med = {}
    for rar in RARITY:
        vals = [c["per_ap"] for c in curve if c["rarity"] == rar and c["per_ap"] is not None and c["power"] > 0]
        rar_med[rar] = (med(vals), len(vals))

    outliers = []
    for c in curve:
        if c["ap"] <= 0 or c["per_ap"] is None or not tier_med.get(c["ap"]):
            if c["ap"] == 0 and c["spr"] > 20:
                outliers.append(dict(c, why="free", ratio=None))
            continue
        ratio = c["per_ap"] / tier_med[c["ap"]]
        if ratio >= 1.15 or ratio <= 0.85:
            outliers.append(dict(c, why="high" if ratio >= 1.15 else "low", ratio=fnum(ratio, 3)))

    # rarity rivals: same type, lower rarity norm SPR >= 95% of a higher rarity's SPR
    rate_rivals = []
    for a in curve:
        for b in curve:
            if a["type"] != b["type"] or a["type"] == "None" or a["move"] == b["move"]:
                continue
            if RARITY.index(a["rarity"]) >= RARITY.index(b["rarity"]):
                continue
            if not a["per_ap"] or not b["per_ap"]:
                continue
            if a["ap"] <= b["ap"] and a["per_ap"] >= 0.95 * b["per_ap"]:
                rate_rivals.append(dict(low=a["move"], low_r=a["rarity"], low_ap=a["ap"], low_rate=a["per_ap"],
                                        high=b["move"], high_r=b["rarity"], high_ap=b["ap"], high_rate=b["per_ap"],
                                        type=a["type"], ratio=fnum(a["per_ap"] / b["per_ap"], 3)))

    # Catalog (not just learn pools): rare typeless vs typed common, same AP and shape.
    catalog_cmp = []
    cat_groups = {}
    for r in moves:
        if r["Category"] not in damaging_cats or "BOSS_ONLY" in (r.get("Tags") or ""):
            continue
        if int(r["Power"] or 0) <= 0:
            continue
        key = (int(r["APCost"] or 0), "spread" if "spread" in shape_of(r) else "single")
        spr = norm_cache[r["MoveId"]] + rider_value(r, norm_cache[r["MoveId"]])[0]
        slot = cat_groups.setdefault(key, {"typed_common": [], "typeless_rare": [], "typeless_common": []})
        if r["BaseType"] == "None" and r["Rarity"] == "Rare":
            slot["typeless_rare"].append((r["MoveId"], fnum(spr)))
        if r["BaseType"] == "None" and r["Rarity"] == "Common":
            slot["typeless_common"].append((r["MoveId"], fnum(spr)))
        if r["BaseType"] != "None" and r["Rarity"] == "Common":
            slot["typed_common"].append((r["MoveId"], fnum(spr)))
    for key, slot in sorted(cat_groups.items()):
        tc = [v for _, v in slot["typed_common"]]
        tr = [v for _, v in slot["typeless_rare"]]
        catalog_cmp.append(dict(
            ap=key[0], shape=key[1],
            typed_common_n=len(tc), typed_common_med=None if not tc else fnum(med(tc)),
            typeless_rare_n=len(tr), typeless_rare_med=None if not tr else fnum(med(tr)),
            typeless_rare=[m for m, _ in slot["typeless_rare"]],
        ))

    rivals = []
    for a in curve:
        for b in curve:
            if a["type"] != b["type"] or a["type"] == "None":
                continue
            if RARITY.index(a["rarity"]) >= RARITY.index(b["rarity"]):
                continue
            if b["spr"] <= 0:
                continue
            if a["spr"] >= 0.95 * b["spr"]:
                rivals.append(dict(low=a["move"], low_r=a["rarity"], low_ap=a["ap"], low_spr=a["spr"],
                                   high=b["move"], high_r=b["rarity"], high_ap=b["ap"], high_spr=b["spr"],
                                   type=a["type"], ratio=fnum(a["spr"] / b["spr"], 3)))

    # typed vs typeless
    type_rows = []
    groups = {}
    for c in curve:
        if c["power"] <= 0:
            continue
        key = (c["ap"], "spread" if "spread" in c["shape"] else "single")
        groups.setdefault(key, {"typed": [], "typeless": [], "typed_common": [], "typeless_rare": []})
        bucket = "typeless" if c["type"] == "None" else "typed"
        groups[key][bucket].append(c["spr"])
        if c["type"] == "None" and c["rarity"] == "Rare":
            groups[key]["typeless_rare"].append(c["spr"])
        if c["type"] != "None" and c["rarity"] == "Common":
            groups[key]["typed_common"].append(c["spr"])
    for key in sorted(groups):
        g = groups[key]
        type_rows.append(dict(
            ap=key[0], shape=key[1],
            typed_n=len(g["typed"]), typed_med=fnum(med(g["typed"])) if g["typed"] else None,
            typeless_n=len(g["typeless"]), typeless_med=fnum(med(g["typeless"])) if g["typeless"] else None,
            typed_common_med=fnum(med(g["typed_common"])) if g["typed_common"] else None,
            typeless_rare_med=fnum(med(g["typeless_rare"])) if g["typeless_rare"] else None,
        ))

    # denial rates
    denial = []
    for r in moves:
        util = r.get("UtilityEffects") or ""
        sts = r.get("StatusEffects") or ""
        tags = r.get("Tags") or ""
        ap = int(r["APCost"] or 0)
        removed = 0.0
        gained = 0.0
        kind = None
        if "DrainAP:" in util:
            for u in parse_utils(util):
                if u[0] == "DrainAP":
                    ch = float(u[2].rstrip("%")) / 100.0
                    removed += float(u[1]) * ch
                    kind = "drain"
                if u[0] == "GainAP":
                    ch = float(u[2].rstrip("%")) / 100.0
                    gained += float(u[1]) * ch
        if "StealAP:" in util:
            for u in parse_utils(util):
                if u[0] == "StealAP":
                    ch = float(u[2].rstrip("%")) / 100.0
                    removed += float(u[1]) * ch
                    gained += float(u[1]) * ch
                    kind = "steal"
        if "MEVExtracted" in sts:
            removed += 1 * 2  # -1 for 2 turns, both ticks if it ticks on grant; state as up to 2
            kind = "mev"
        if "ThreadLock" in sts:
            removed += 2  # blocks one +2 regen
            kind = "threadlock"
        if "ArmUtilityWindow" in util:
            for u in parse_utils(util):
                if u[0] == "ArmUtilityWindow":
                    nums = []
                    for bit in u[1:]:
                        try:
                            nums.append(float(bit))
                        except ValueError:
                            pass
                    cuts = [-n for n in nums if n <= -1]
                    if cuts:
                        removed += max(cuts)
                        kind = "next-start"
        if r["MoveId"] == "fee_collector":
            removed = 1
            kind = "passive-if-ahead"
            ap = 0
        if kind:
            rate = None if ap == 0 else removed / ap
            denial.append(dict(move=r["MoveId"], name=r["DisplayName"], type=r["BaseType"], rarity=r["Rarity"],
                               ap=ap, kind=kind, removed=removed, gained=gained,
                               per_ap=None if rate is None else fnum(rate, 3),
                               learned=sorted({rec["species"] for rec in records if rec["move"] == r["MoveId"]})))

    # 3 AP learnable that cannot fire turn 1
    slow = [c for c in curve if c["legal"] > 1]
    three = [c for c in curve if c["ap"] == 3]

    # proposals landing
    proposal_rows = []
    for pid, mid, kind, old, proposed, why in PROPOSALS:
        row = by_id.get(mid)
        in_note = mid in note
        if row is None:
            proposal_rows.append(dict(id=pid, move=mid, kind=kind, old=old, proposed=proposed, current=None,
                                      status="not landed", why=why, in_note=in_note, test="no test",
                                      line=None))
            L.add(pid, in_note, f"{mid} {kind} proposed {proposed} current ABSENT", why, kind="fact")
            continue
        if kind == "power":
            current = int(row["Power"] or 0)
        elif kind == "ap":
            current = int(row["APCost"] or 0)
        elif kind == "random_mean":
            rand = (row.get("RandomDamageMultipliers") or "").strip()
            vals = [float(x) for x in rand.split("|") if x]
            current = fnum(sum(vals) / len(vals), 3) if vals else None
        else:
            current = None
        same = lambda a, b: a == b or (isinstance(a, float) and isinstance(b, (int, float)) and abs(a - b) < 0.02)
        if same(current, proposed):
            status = "landed"
        elif same(proposed, old) and not same(current, old):
            status = "not landed"  # the note said leave this number; it moved
        elif not same(current, old):
            status = "partly"
        else:
            status = "not landed"
        # power unchanged but we still call not landed if it equals old
        test = first_test(root, mid)
        proposal_rows.append(dict(id=pid, move=mid, name=row["DisplayName"], kind=kind, old=old, proposed=proposed,
                                  current=current, status=status, why=why, in_note=in_note, test=test,
                                  line=move_lines.get(mid), ap=int(row["APCost"] or 0), power=int(row["Power"] or 0),
                                  rarity=row["Rarity"]))
        L.add(pid, in_note, f"{status}: {mid} {kind} old {old} proposed {proposed} current {current} test {test}",
              f"Moves.csv:{move_lines.get(mid)}")

    # engine proposals from the same note
    L.add("P52", True, "partly: Bot drain landed as Packet Skim (1 AP, drain 1) in Kitsauto, not Batch Debit (2 AP, drain 2)",
          f"Moves.csv:{move_lines['packet_skim']}; SpeciesMovePools.csv:{learn['kitsauto']['line']}; test {first_test(root, 'packet_skim')}")
    L.add("P53", bench_line is not None, "landed: bench recharge ticks",
          f"CombatResolver.cs:{bench_line}; test {first_test(root, 'BenchRechargeTicksWhileOtherStatusesStayPaused')}")
    L.add("P54", True, "landed: Gas Spike and FUD stages revert with the status via owned deltas",
          f"StatusProcessor.cs:{revert_owned}; test {first_test(root, 'GasSpikeAndFud_RestoreOwnedStagesOnExpiry')}")
    L.add("P55", "StageHigherThanOtherStage" in (by_id["rallying_slam"]["ResolveConditions"] or ""),
          "partly: fresh 0-vs-0 no longer refunds (strict >). The note asked for Def stage >= +1. Test covers the strict compare.",
          f"Moves.csv:{move_lines['rallying_slam']}; test {first_test(root, 'StageHigherThanOtherStageUsesStrictComparison')}")

    # crystal bash current
    bash = by_id["crystal_bash"]
    L.add("C41", int(bash["Power"]) == 44 and (bash.get("DamageOverrideAttackStat") or "").lower() == "true",
          f"Crystal Bash power {bash['Power']} AP {bash['APCost']} override {bash.get('DamageAttackStat')} double-below-30 mult {bash.get('ConditionalDamageMultiplier')}",
          f"Moves.csv:{move_lines['crystal_bash']}; not in any learn pool")

    # gas pool
    L.add("C42", True,
          "Deghoul pool " + ",".join(learn["deghoul"]["pool"]) + " | Balloom pool " + ",".join(learn["balloom"]["pool"]),
          f"SpeciesMovePools.csv:{learn['deghoul']['line']} and :{learn['balloom']['line']}")

    # volatile is Gas War, recoil is Hard Fork
    L.add("C43", by_id["volatile_shot"]["DisplayName"] == "Gas War" and by_id["recoil_strike"]["DisplayName"] == "Hard Fork",
          f"volatile_shot display '{by_id['volatile_shot']['DisplayName']}' cat {by_id['volatile_shot']['Category']} P{by_id['volatile_shot']['Power']} AP{by_id['volatile_shot']['APCost']}; recoil_strike display '{by_id['recoil_strike']['DisplayName']}' cat {by_id['recoil_strike']['Category']} P{by_id['recoil_strike']['Power']} AP{by_id['recoil_strike']['APCost']}",
          f"Moves.csv:{move_lines['volatile_shot']} and :{move_lines['recoil_strike']}")

    # drain cap absence
    cap_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs", r"int Drain\(")
    L.add("C44", cap_line is not None, "CombatApService.Drain has no per-turn cap in its signature block",
          f"CombatApService.cs:{cap_line}")

    # top/bottom 10 by per_ap among power>0 ap>0
    ranked = [c for c in curve if c["per_ap"] is not None and c["power"] > 0 and c["ap"] > 0]
    ranked.sort(key=lambda c: -c["per_ap"])
    top10 = ranked[:10]
    bot10 = list(reversed(ranked[-10:]))

    L.add("C45", True, "SPR method: norm strike uses atk=def=60 (ratio 1), floor((P*u+1)*mult) with u from "
          f"{rmin} to {rmax} in 21 steps, crit {crit_p} x{crit_m}, times expected hits, accuracy, spread x2. "
          f"AP point={ap_point:.2f} = median 2 AP single norm strike / 2. Drain x0.6, gain x1, steal both. "
          f"Stage credit is half a reference hit per stage of a damage stat, one future hit. Speed/accuracy/evasion and priority are tempo, priced at 0 HP. "
          f"Median HP={med_hp:.0f} melee def={med_md:.0f} ranged def={med_rd:.0f}.",
          "computed")

    L.add("C46", True, "tier median norm SPR/AP " + ", ".join(f"AP{k}:{None if v is None else round(v,2)}" for k, v in sorted(tier_med.items())),
          f"n={len(ranked)} learnable damaging moves with power>0")
    L.add("C47", True, "rarity median norm SPR/AP " + ", ".join(
        f"{k}:{None if rar_med[k][0] is None else round(rar_med[k][0],2)}(n={rar_med[k][1]})" for k in RARITY), "learnable damaging")

    # holder examples the doc will quote: compute a few matchup expected damages with chemistry
    def chem_mult(atk_type, def_type):
        if (atk_type, def_type) in matrix:
            return chem_adv
        if (def_type, atk_type) in matrix:
            return chem_dis
        return 1.0

    def matchup(atk_sid, mid, def_sid):
        row = by_id[mid]
        a, d = species[atk_sid], species[def_sid]
        melee = row["Category"] == "DamagingMelee"
        if (row.get("DamageOverrideAttackStat") or "").lower() == "true":
            atk = a["md"]
        else:
            atk = a["ma"] if melee else a["ra"]
        defn = d["md"] if melee else d["rd"]
        mtype = TYPES.index(row["BaseType"]) if row["BaseType"] != "None" else a["type"]
        mult = neutral_damage_mult(row) * chem_mult(mtype, d["type"])
        per = expected_hit_damage(int(row["Power"] or 0), atk, defn, mult, crit_p, crit_m, rmin, rmax)
        hits = (int(row["HitMin"] or 1) + int(row["HitMax"] or 1)) / 2.0
        acc = int(row["Accuracy"] or 0)
        acc_f = 1.0 if acc <= 0 else acc / 100.0
        total = per * hits * acc_f * (2.0 if row["Targeting"] in ("AllEnemies", "Field") else 1.0)
        return dict(atk=atk_sid, move=mid, defender=def_sid, dmg=fnum(total), chem=fnum(chem_mult(mtype, d["type"]), 2),
                    hp=d["hp"], spd_a=a["spd"], spd_d=d["spd"], ap=int(row["APCost"] or 0), power=int(row["Power"] or 0))

    pairs = [
        ("validape", "hammer_sync", "deghoul"),
        ("validape", "baseline_striker", "deghoul"),
        ("gilarime", "cryo_ray", "crackett"),
        ("gilarime", "frost_shot", "crackett"),
        ("kodiax", "deep_dig", "keyper"),
        ("kodiax", "ore_strike", "keyper"),
        ("balloom", "volatile_shot", "kitsauto"),
        ("balloom", "gas_bump", "kitsauto"),
        ("forkrook", "fork_split", "deghoul"),
        ("forkrook", "baseline_shot", "deghoul"),
        ("cinislug", "cinder_burst", "deghoul"),
        ("cinislug", "baseline_shot", "deghoul"),
        ("crackett", "seam_cut", "deghoul"),
        ("kitsauto", "blade_flurry", "cinislug"),
        ("deghoul", "gas_bump", "validape"),
        ("deghoul", "transaction_tax", "validape"),
    ]
    matchups = [matchup(*p) for p in pairs]
    L.add("C49", True, "catalog rare-typeless vs typed-common medians " +
          "; ".join(f"AP{c['ap']} {c['shape']} typed-common {c['typed_common_med']} (n={c['typed_common_n']}) "
                    f"typeless-rare {c['typeless_rare_med']} (n={c['typeless_rare_n']}) {c['typeless_rare']}"
                    for c in catalog_cmp if c["typeless_rare_n"] or c["typed_common_n"]),
          "all non-boss damaging rows, norm SPR, not only learn pools")
    L.add("C50", True, f"same-type per-AP rivals (lower rarity, AP at or under the higher one, rate >= 95%) n={len(rate_rivals)} "
          + "; ".join(f"{r['type']} {r['low']} {r['low_r']} {r['low_rate']}/AP vs {r['high']} {r['high_r']} {r['high_rate']}/AP" for r in rate_rivals),
          "learnable damaging")
    L.add("C48", True, "holder expected damage (chemistry on, crit expected, ratio of that body's atk to that foe's def) "
          + "; ".join(f"{m['atk']}:{m['move']}->{m['defender']} {m['dmg']}" for m in matchups),
          "DamageCalculator shape, neutral gates")

    cover_ids = ["barrier_wall", "blockade", "ember_wall", "ice_wall", "compound_guard"]
    cover_ok = all(m in by_id for m in cover_ids)
    cover_txt = ", ".join(
        f"{m} AP{by_id[m]['APCost']} {by_id[m]['DisplayName']} {by_id[m]['BaseType']} line {move_lines.get(m)}"
        for m in cover_ids if m in by_id)
    ember_only = cover_ok and int(by_id["ember_wall"]["APCost"]) == 2 and all(
        int(by_id[m]["APCost"]) == 3 for m in cover_ids if m != "ember_wall")
    L.add("C51", ember_only, "turn-1 cover: only Ember Wall is 2 AP; the other four covers are 3 AP and miss turn 1. " + cover_txt,
          "Moves.csv APCost")
    ml = by_id["memory_leak"]
    L.add("C52", int(ml["APCost"]) == 2 and "Speed:2" in (ml.get("StageChanges") or "") and "ArmUtilityWindow" in (ml.get("UtilityEffects") or ""),
          f"Memory Leak AP {ml['APCost']} rarity {ml['Rarity']} stages '{ml.get('StageChanges')}' utility '{ml.get('UtilityEffects')}' text '{ml.get('Description')}'",
          f"Moves.csv:{move_lines['memory_leak']}; learned by {','.join(sorted({rec['species'] for rec in records if rec['move']=='memory_leak'})) or 'nobody'}")
    tax = by_id["transaction_tax"]
    L.add("C53", "StealAP:1" in (tax.get("UtilityEffects") or "") and int(tax["APCost"]) == 1 and tax["Rarity"] == "Epic",
          f"Transaction Tax AP {tax['APCost']} {tax['Rarity']} utility '{tax.get('UtilityEffects')}' text '{tax.get('Description')}'",
          f"Moves.csv:{move_lines['transaction_tax']}; learned by {','.join(sorted({rec['species'] for rec in records if rec['move']=='transaction_tax'})) or 'nobody'}")
    gw = by_id["volatile_shot"]
    L.add("C54", "NextMoveAPSurcharge:1" in (gw.get("UtilityEffects") or "") and "+1 AP" in (gw.get("Description") or ""),
          f"Gas War utility '{gw.get('UtilityEffects')}' text '{gw.get('Description')}'",
          f"Moves.csv:{move_lines['volatile_shot']}")
    hs = by_id["hammer_sync"]
    L.add("C55", "partner is buffed" in (hs.get("Description") or "") and (hs.get("ConditionalDamageBonusPerCount") or "") == "0.2"
          and (hs.get("ConditionalDamageMaxMultiplier") or "") == "1.2",
          f"Hammer Sync text '{hs.get('Description')}' bonus {hs.get('ConditionalDamageBonusPerCount')} cap {hs.get('ConditionalDamageMaxMultiplier')} scaling '{hs.get('ConditionalDamageScalingCondition')}'",
          f"Moves.csv:{move_lines['hammer_sync']}")
    jab_holders = sorted({rec["species"] for rec in records if rec["move"] == "quick_lunge"})
    L.add("C56", jab_holders == [], f"Blink Lunge (quick_lunge) learn-pool holders={jab_holders or 'none'} power {by_id['quick_lunge']['Power']} AP {by_id['quick_lunge']['APCost']}",
          f"Moves.csv:{move_lines['quick_lunge']}; SpeciesMovePools.csv")
    fee_holders = sorted({rec["species"] for rec in records if rec["move"] == "fee_collector"})
    L.add("C57", fee_holders == [], f"Fee Collector learn-pool holders={fee_holders or 'none'} AP {by_id['fee_collector']['APCost']} cat {by_id['fee_collector']['Category']}",
          f"Moves.csv:{move_lines['fee_collector']}; SpeciesMovePools.csv")
    bot_off = ["mev_extract", "thread_lock"]
    bot_off_holders = {m: sorted({rec["species"] for rec in records if rec["move"] == m}) for m in bot_off}
    L.add("C58", all(v == [] for v in bot_off_holders.values()),
          "Bot denial not in a learn pool: " + ", ".join(
              f"{m} AP{by_id[m]['APCost']} {by_id[m]['Rarity']} holders={bot_off_holders[m] or 'none'}" for m in bot_off),
          "SpeciesMovePools.csv")
    learnable_slow = [c["move"] for c in slow]
    L.add("C59", True, "learnable damaging moves that cannot be paid on turn 1 (regen +2 from 0): "
          + ", ".join(f"{c['move']} AP{c['ap']} turn {c['legal']}" for c in slow),
          f"n={len(slow)}")

    # write tables
    def md_table(headers, rows):
        out = ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
        for row in rows:
            out.append("| " + " | ".join(str(c) for c in row) + " |")
        return "\n".join(out)

    curve_sorted = sorted(curve, key=lambda c: (c["ap"], - (c["per_ap"] or 0), c["move"]))
    curve_md = md_table(
        ["Move", "Type", "Rarity", "AP", "Pow", "Hits", "Shape", "Prio", "Rc", "Norm", "Rider", "SPR", "SPR/AP", "Legal", "Holders", "Rider text"],
        [[c["move"], c["type"], c["rarity"], c["ap"], c["power"], c["hits"], c["shape"],
          "Y" if c["priority"] else "", c["recharge"], c["norm"], c["rider_hp"], c["spr"],
          "" if c["per_ap"] is None else c["per_ap"], c["legal"], ",".join(c["holders"]), c["rider"]]
         for c in curve_sorted])

    out_md = md_table(
        ["Move", "Type", "Rarity", "AP", "SPR", "SPR/AP", "Tier med", "Ratio", "Why", "Holders"],
        [[o["move"], o["type"], o["rarity"], o["ap"], o["spr"], o["per_ap"],
          "" if not tier_med.get(o["ap"]) else round(tier_med[o["ap"]], 2),
          o.get("ratio"), o["why"], ",".join(o["holders"])] for o in outliers])

    prop_md = md_table(
        ["Id", "Move", "Field", "10-17 now", "Proposed", "Current", "Status", "Test", "CSV line"],
        [[p["id"], p["move"], p["kind"], p["old"], p["proposed"], p["current"], p["status"], p["test"], p["line"]]
         for p in proposal_rows])

    deny_md = md_table(
        ["Move", "Type", "Rarity", "AP", "Kind", "AP removed", "AP gained", "Removed per AP", "Learned by"],
        [[d["move"], d["type"], d["rarity"], d["ap"], d["kind"], d["removed"], d["gained"],
          "" if d["per_ap"] is None else d["per_ap"], ",".join(d["learned"]) or "—"] for d in denial])

    type_md = md_table(
        ["AP", "Shape", "Typed n", "Typed med SPR", "Typeless n", "Typeless med SPR", "Typed common med", "Typeless rare med"],
        [[t["ap"], t["shape"], t["typed_n"], t["typed_med"], t["typeless_n"], t["typeless_med"],
          t["typed_common_med"], t["typeless_rare_med"]] for t in type_rows])

    rival_md = md_table(
        ["Type", "Lower", "Rarity", "AP", "SPR", "Higher", "Rarity", "AP", "SPR", "Ratio"],
        [[r["type"], r["low"], r["low_r"], r["low_ap"], r["low_spr"], r["high"], r["high_r"], r["high_ap"], r["high_spr"], r["ratio"]]
         for r in rivals])

    tables = "\n\n".join([
        "<!-- TABLE:proposals -->\n" + prop_md + "\n<!-- /TABLE:proposals -->",
        "<!-- TABLE:curve -->\n" + curve_md + "\n<!-- /TABLE:curve -->",
        "<!-- TABLE:outliers -->\n" + out_md + "\n<!-- /TABLE:outliers -->",
        "<!-- TABLE:types -->\n" + type_md + "\n<!-- /TABLE:types -->",
        "<!-- TABLE:rivals -->\n" + rival_md + "\n<!-- /TABLE:rivals -->",
        "<!-- TABLE:denial -->\n" + deny_md + "\n<!-- /TABLE:denial -->",
    ])
    (OUT / "tables.md").write_text(tables, encoding="utf-8")

    facts = dict(
        ap_turn=ap_turn, ap_cap=ap_cap, ap_point=fnum(ap_point), ref_hit=fnum(ref_hit),
        med_hp=med_hp, med_md=med_md, med_rd=med_rd, med_ma=med_ma, med_ra=med_ra,
        n_rows=n_rows, n_dmg=n_dmg, n_passive=n_passive,
        ap_counts=dict(ap_counts), rar_counts=dict(rar_counts),
        spine=[r["MoveId"] for r in spine], ap4=ap4, ap5=ap5, ap6=ap6,
        tier_med={str(k): None if v is None else fnum(v) for k, v in tier_med.items()},
        rar_med={k: (None if rar_med[k][0] is None else fnum(rar_med[k][0]), rar_med[k][1]) for k in RARITY},
        curve=curve_sorted, outliers=outliers, top10=[c["move"] for c in top10], bot10=[c["move"] for c in bot10],
        rivals=rivals, rate_rivals=rate_rivals, catalog_cmp=catalog_cmp, type_rows=type_rows, denial=denial, proposals=proposal_rows,
        matchups=matchups, text_flags=text_flags, pool_breaks=pool_breaks,
        asset_miss=asset_miss, ts_miss=ts_miss, three_ap=[c["move"] for c in three],
        slow=[dict(move=c["move"], ap=c["ap"], legal=c["legal"], holders=c["holders"]) for c in slow],
        species={k: species[k] for k in playable_sids},
        learn={k: {kk: vv for kk, vv in info.items() if kk != "line"} | {"line": info["line"]} for k, info in learn.items()},
        prio=[r["MoveId"] for r in prio],
        claims=L.rows,
    )
    (OUT / "facts.json").write_text(json.dumps(facts, indent=1), encoding="utf-8")

    if analysis and analysis.exists():
        doc = analysis.read_text(encoding="utf-8", errors="replace")
        missing = [r["id"] for r in L.rows if r["id"] not in doc]
        L.add("C99", not missing, f"analysis cites every claim id; missing={missing[:20]}", str(analysis), kind="flag")
        # tables must be embedded
        for name in ("proposals", "curve", "outliers", "types", "rivals", "denial"):
            start = f"<!-- TABLE:{name} -->"
            end = f"<!-- /TABLE:{name} -->"
            block = tables.split(start)[1].split(end)[0]
            ok = block.strip() in doc
            L.add(f"T-{name}", ok, f"analysis contains generated {name} table", kind="flag")

    passed, failed = L.tally()
    print(f"\nTALLY pass={passed} fail={failed} claims={passed+failed}")
    print(f"facts: {OUT / 'facts.json'}")
    print(f"tables: {OUT / 'tables.md'}")
    # exit 0 even when flag-claims fail; those are findings. Non-zero only if a fact-claim failed.
    fact_fail = [r for r in L.rows if not r["ok"] and r["kind"] == "fact"]
    return 1 if fact_fail else 0


if __name__ == "__main__":
    sys.exit(main())
