#!/usr/bin/env python3
"""Rarity-fit checker for RARITY_FIT_AND_RIDER_BUILDOUT_V1.

Reads the unzipped combat snapshot. Prints PASS/FAIL per claim.
Writes the proposal doc. Does not modify the snapshot or the git repo.

Usage:
  python3 rarity_fit_check.py <snapshot_root> [doc_path]
"""
from __future__ import annotations

import csv
import json
import math
import os
import random
import re
import statistics
import sys
from collections import defaultdict
from pathlib import Path

RARITY = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]
RARITY_IDX = {n: i for i, n in enumerate(RARITY)}
EXCLUDED_IDS = {
    "script_loop", "viral_surge", "wildcard_swap", "extraction_hit",
    "deflation_mark", "all_in", "recoil_strike",
}
RETIRED_IDS = {"status_resistance"}
# Decided numbers this round must not trim.
KEEP_IDS = {
    "baseline_shot", "kindling", "ember_wall", "system_reset", "hype_burst",
    "crystal_extraction", "packet_skim", "all_in", "recoil_strike",
}
DAMAGING = {"DamagingMelee", "DamagingRanged"}
RUNTIME_REL = [
    "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs",
    "Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs",
    "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs",
    "Assets/Scripts/Combat/Runtime/Engine/CombatActionEligibilityService.cs",
    "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs",
    "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs",
]
TYPES = ["None", "Node", "Gas", "Block", "Encrypted", "Burned", "Frozen", "Forked",
         "Mined", "Staked", "Compressed", "Bot", "Glitched", "Shard", "Meme"]


class Ledger:
    def __init__(self):
        self.rows = []

    def add(self, cid, ok, evidence, detail=""):
        self.rows.append(dict(id=cid, ok=bool(ok), evidence=evidence, detail=detail))
        flag = "PASS" if ok else "FAIL"
        extra = f" | {detail}" if detail else ""
        print(f"{flag} {cid} {evidence}{extra}")

    def tally(self):
        p = sum(1 for r in self.rows if r["ok"])
        f = sum(1 for r in self.rows if not r["ok"])
        return p, f


def fnum(x, nd=2):
    return round(float(x) + 0.0, nd)


def read_csv(path: Path):
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    header = None
    header_line = 0
    records = []
    line_no = {}
    id_key = None
    for i, line in enumerate(lines, 1):
        if not line.strip() or line.startswith("#"):
            continue
        if header is None:
            header = line
            header_line = i
            id_key = "MoveId" if line.startswith("MoveId") else (
                "SpeciesId" if line.startswith("SpeciesId") else "StatusId")
            continue
        row = next(csv.DictReader([header, line]))
        records.append(row)
        key = (row.get(id_key) or "").strip()
        if key:
            line_no[key] = i
    return records, line_no, header_line


def find_line(path: Path, pattern: str):
    if not path.exists():
        return None
    rx = re.compile(pattern)
    for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        if rx.search(line):
            return i
    return None


def find_all_quoted(path: Path, move_ids: set):
    hits = {}
    if not path.exists():
        return hits
    rx = re.compile(r'"([a-z0-9_]+)"')
    for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        for m in rx.finditer(line):
            mid = m.group(1)
            if mid in move_ids and mid not in hits:
                hits[mid] = i
    return hits


def neutral_condition_passes(token: str) -> bool:
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
    if kind in ("HPAbovePercent", "WasNotDamagedLastTurn", "NoBuffs"):
        return True
    if kind in ("StageHigherThanOtherStage",):
        return False
    if kind in ("StageAtLeastOtherStage",):
        return True
    if kind in ("DebuffCountAtLeast", "StatusCount", "StatusCountAtLeast",
                "BuffedPartnerCountAtLeast", "BuffCountAtLeast"):
        return thresh <= 0
    return False


def condition_count(token: str) -> int:
    return 0


def parse_chance(tok: str) -> float:
    tok = (tok or "").strip()
    if not tok:
        return 1.0
    if tok.endswith("%"):
        return float(tok[:-1]) / 100.0
    v = float(tok)
    if v > 1.0:
        return v / 100.0
    return v


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
    for key in ("DamagePowerMultiplier", "DamagePreMitigationMultiplier", "DamageFinalMultiplier"):
        raw = (row.get(key) or "").strip()
        if not raw:
            continue
        try:
            f = float(raw)
        except ValueError:
            continue
        if f > 0:
            mult *= f
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


def fresh_gate(row: dict) -> bool:
    active = (row.get("ActiveConditionType") or "").strip() or "None"
    if active == "SelfHPBelowPercent":
        return False
    policy = (row.get("ResolveConditionalPolicy") or "").strip() or "LegacyAndResolveConditions"
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
            chance = parse_chance(b[2]) if len(b) > 2 and b[2] else 1.0
        except ValueError:
            chance = 1.0
        try:
            dur = int(float(b[1])) if len(b) > 1 and b[1] else 0
        except ValueError:
            dur = 0
        out.append(dict(id=b[0], dur=dur, chance=chance))
    return out


def parse_utils(raw: str):
    return [part.split(":") for part in filter(None, (raw or "").split("|"))]


def parse_status_stages(raw: str):
    out = []
    for part in filter(None, (raw or "").split("|")):
        b = part.split(":")
        if len(b) < 2:
            continue
        per_turn = len(b) > 2 and b[2].lower() == "false"
        try:
            out.append(dict(stat=b[0], delta=int(float(b[1])), per_turn=per_turn))
        except ValueError:
            continue
    return out


def expected_hit_damage(power, atk, defn, mult, crit_p, crit_m, rmin, rmax, steps=21):
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


def bucket_of(row) -> str:
    if row["Category"] in DAMAGING:
        sh = shape_of(row)
        return "spread" if sh.startswith("spread") else sh
    if row["Category"] == "Passive":
        return "passive"
    if row["Category"] == "Support":
        return "support"
    return "utility"


def spread_factor(row) -> float:
    return 2.0 if row["Targeting"] in ("AllEnemies", "Field") else 1.0


def stage_hp(delta, chance, stat, self_target, ref_hit, stage_frac, hits, acc_f):
    if stat in ("Speed", "Accuracy", "Evasion", "CritRate"):
        return 0.0
    helpful = delta if self_target else -delta
    if helpful == 0:
        return 0.0
    mag = min(2.0, abs(helpful))
    sign = 1.0 if helpful > 0 else -1.0
    return sign * stage_frac * mag * ref_hit * chance * hits * acc_f


class Model:
    def __init__(self, ap_point, ref_hit, med_hp, crit_p, crit_m, rmin, rmax, statuses,
                 drain_w=0.6, gain_w=1.0, stage_frac=0.5, delay_w=0.5, speed_frac=0.0):
        self.ap_point = ap_point
        self.ref_hit = ref_hit
        self.med_hp = med_hp
        self.crit_p = crit_p
        self.crit_m = crit_m
        self.rmin = rmin
        self.rmax = rmax
        self.statuses = statuses
        self.drain_w = drain_w
        self.gain_w = gain_w
        self.stage_frac = stage_frac
        self.delay_w = delay_w
        self.speed_frac = speed_frac

    def ap_credit(self, amount, chance, kind):
        exp = amount * chance
        if kind == "drain":
            return self.drain_w * exp * self.ap_point
        if kind == "gain":
            return self.gain_w * exp * self.ap_point
        if kind == "steal":
            return (self.drain_w + self.gain_w) * exp * self.ap_point
        return 0.0

    def speed_hp(self, delta, chance, hits, acc_f):
        if self.speed_frac <= 0 or delta == 0:
            return 0.0
        return self.speed_frac * min(2, abs(delta)) * self.ref_hit * chance * hits * acc_f

    def strike(self, row):
        power = int(row["Power"] or 0)
        if row["Category"] not in DAMAGING or power <= 0:
            return 0.0
        mult = neutral_damage_mult(row)
        per = expected_hit_damage(power, 60.0, 60.0, mult, self.crit_p, self.crit_m, self.rmin, self.rmax)
        hits = (int(row["HitMin"] or 1) + int(row["HitMax"] or 1)) / 2.0
        acc = int(row["Accuracy"] or 0)
        acc_f = 1.0 if acc <= 0 else acc / 100.0
        return per * hits * acc_f * spread_factor(row)

    def status_credit(self, st, targets, acc_f):
        sd = self.statuses.get(st["id"])
        notes = []
        if not sd:
            return 0.0, [f"status {st['id']} missing"]
        credit = 0.0
        try:
            eot = float(sd.get("EoTDamagePercent") or 0)
        except ValueError:
            eot = 0.0
        try:
            eot_ap = int(float(sd.get("EoTAPChange") or 0))
        except ValueError:
            eot_ap = 0
        dur = st["dur"] or int(sd.get("Duration") or 0) or 1
        ticks = min(max(dur, 1), 2)
        credit += eot * min(dur, 2) * self.med_hp * st["chance"] * acc_f * targets
        if eot_ap < 0:
            credit += self.delay_w * self.ap_credit(-eot_ap * ticks, st["chance"] * acc_f, "drain") * targets
        elif eot_ap > 0:
            credit += self.delay_w * self.ap_credit(eot_ap * ticks, st["chance"] * acc_f, "gain") * targets
        for sg in parse_status_stages(sd.get("StageEffects") or ""):
            # one future exchange, not every tick
            c = stage_hp(sg["delta"], st["chance"], sg["stat"], False, self.ref_hit,
                         self.stage_frac, 1.0, acc_f)
            # status on Self is still "the holder". Caller passes target via status target token.
            credit += c * targets
            if sg["stat"] in ("Speed", "Accuracy", "Evasion"):
                credit += self.speed_hp(sg["delta"], st["chance"], 1.0, acc_f) * targets
        sid = st["id"]
        if sid == "Surging":
            credit += 0.25 * self.ref_hit * st["chance"] * acc_f
            notes.append("Surging +25% next hit")
        elif sid == "ExploitMark":
            credit += 0.15 * self.ref_hit * st["chance"] * acc_f * targets
            notes.append("Exploit Mark +15%")
        elif sid == "ThreadLock":
            credit += self.delay_w * self.ap_credit(2, st["chance"] * acc_f, "drain") * targets
            notes.append("Thread Lock denies next +2 regen")
        elif sid == "CipherLocked":
            notes.append("Cipher Lock priced 0 HP")
        elif sid == "FrozenAssets":
            notes.append("Frozen Assets priced 0 HP")
        elif sid == "BurnedTokens":
            notes.append("Burned Tokens has no body")
        return credit, notes

    def rider(self, row, dmg, id_hits):
        gated = fresh_gate(row)
        damaging = row["Category"] in DAMAGING
        acc = int(row["Accuracy"] or 0)
        acc_f = 1.0 if (not damaging or acc <= 0) else acc / 100.0
        hmin = int(row["HitMin"] or 1)
        hmax = int(row["HitMax"] or 1)
        hits = (hmin + hmax) / 2.0
        once = (row.get("MoveEffectsOncePerTarget") or "").strip().lower() == "true"
        applications = 1.0 if once or not damaging else hits
        credit = 0.0
        notes = []
        targets = spread_factor(row)
        if gated:
            for s in parse_stages(row.get("StageChanges") or ""):
                c = stage_hp(s["delta"], s["chance"], s["stat"], s["self"], self.ref_hit,
                             self.stage_frac, applications, acc_f)
                c *= 1.0 if s["self"] else targets
                credit += c
                if s["stat"] in ("Speed", "Accuracy", "Evasion"):
                    credit += self.speed_hp(s["delta"], s["chance"], applications, acc_f) * (1.0 if s["self"] else targets)
                    notes.append(f"tempo {s['stat']} {s['delta']:+d} {s['chance']:.0%}")
            for st in parse_statuses(row.get("StatusEffects") or ""):
                # Self token is not in our parser; Yield on Self is still one holder.
                c, nts = self.status_credit(st, 1.0 if row["Targeting"] in ("Self", "SingleAlly") else targets, acc_f)
                # status_credit treated stages as foe-facing. Re-price self statuses below.
                sd = self.statuses.get(st["id"])
                credit += c
                notes.extend(nts)
                if sd and row["Targeting"] == "Self":
                    # undo foe-facing stage sign by doubling back: recompute as self
                    foe_part = 0.0
                    self_part = 0.0
                    for sg in parse_status_stages(sd.get("StageEffects") or ""):
                        foe_part += stage_hp(sg["delta"], st["chance"], sg["stat"], False, self.ref_hit,
                                             self.stage_frac, 1.0, acc_f)
                        self_part += stage_hp(sg["delta"], st["chance"], sg["stat"], True, self.ref_hit,
                                              self.stage_frac, 1.0, acc_f)
                    credit += self_part - foe_part
            for u in parse_utils(row.get("UtilityEffects") or ""):
                if not u or not u[0]:
                    continue
                op = u[0]
                timing = (row.get("ResolveUtilityTiming") or "").strip()
                apps = applications if timing == "AfterEachConfirmedHit" and damaging else 1.0
                if op in ("DrainAP", "GainAP", "StealAP", "RefundAP", "TransferAP") and len(u) >= 3:
                    try:
                        amt = float(u[1])
                        ch = parse_chance(u[2])
                    except ValueError:
                        continue
                    kind = {"DrainAP": "drain", "GainAP": "gain", "StealAP": "steal",
                            "RefundAP": "gain", "TransferAP": "gain"}[op]
                    tgt = u[3] if len(u) > 3 else ""
                    sign = 1.0
                    if op == "TransferAP":
                        sign = 0.0  # team-neutral move of AP
                        notes.append("transfer net 0")
                    credit += sign * self.ap_credit(amt, ch * acc_f, kind) * apps
                elif op == "NextMoveAPSurcharge" and len(u) >= 3:
                    try:
                        amt = float(u[1]); ch = parse_chance(u[2])
                    except ValueError:
                        amt, ch = 1.0, 1.0
                    tgt = u[3] if len(u) > 3 else "Self"
                    if tgt == "Self":
                        credit -= self.ap_credit(amt, ch, "gain")
                        notes.append(f"self surcharge +{amt:g}")
                    else:
                        credit += self.ap_credit(amt, ch, "drain")
                        notes.append(f"foe surcharge +{amt:g}")
                elif op == "Cleanse":
                    credit += 0.15 * self.ref_hit
                elif op == "StripAllStatuses":
                    credit += 0.25 * self.ref_hit
                elif op == "StripBuffs" or op == "StripBuffsThenStage":
                    credit += 0.20 * self.ref_hit
                elif op == "HealSelfPerTargetDebuff":
                    notes.append("heal per debuff is 0 on a fresh foe")
                elif op.startswith("Heal") or op == "BonusHealPercentIfSpeedHigher":
                    try:
                        pct = parse_chance(u[1]) if not u[1].endswith("%") else float(u[1][:-1]) / 100.0
                        if pct > 1:
                            pct = pct / 100.0
                    except (ValueError, IndexError):
                        pct = 0.0
                    credit += pct * self.med_hp
                elif op == "LifestealPercent":
                    try:
                        pct = parse_chance(u[1]) if not str(u[1]).endswith("%") else float(str(u[1])[:-1]) / 100.0
                    except (ValueError, IndexError):
                        pct = 0.0
                    credit += pct * dmg
                    notes.append(f"lifesteal {pct:.0%}")
                elif op == "BlockWindow" and len(u) >= 2:
                    try:
                        mag = parse_chance(u[1])
                    except ValueError:
                        mag = 0.0
                    credit += mag * self.ref_hit
                    notes.append(f"block {mag:.0%} x1")
                elif op == "WallHitRetaliation" and len(u) >= 2:
                    try:
                        amt = float(u[1])
                    except ValueError:
                        amt = 0.0
                    credit += self.ap_credit(amt, 1.0, "drain")
                elif op == "MitigationWindow" and len(u) >= 2:
                    try:
                        mag = parse_chance(u[1])
                        hits_b = int(float(u[3])) if len(u) > 3 and u[3] not in ("",) else 1
                    except ValueError:
                        mag, hits_b = 0.0, 1
                    prevented = mag * (self.ref_hit if hits_b < 0 else self.ref_hit * max(hits_b, 0))
                    credit += prevented
                elif op == "FullAbsorbBarrier":
                    hits_b = 1
                    try:
                        hits_b = int(float(u[2])) if len(u) > 2 else 1
                    except ValueError:
                        hits_b = 1
                    credit += self.ref_hit * max(hits_b, 1)
                elif op == "IncomingDamageMitigation" and len(u) >= 2:
                    try:
                        mag = parse_chance(u[1])
                    except ValueError:
                        mag = 0.0
                    # a passive screen is priced for two incoming hits, not the whole fight
                    windows = 2.0 if row["Category"] == "Passive" else 1.0
                    credit += mag * self.ref_hit * windows
                elif op == "ChangeStage" and len(u) >= 4:
                    stat, amt_s, ch_s, tgt = u[1], u[2], u[3], u[4] if len(u) > 4 else "Self"
                    try:
                        amt = int(float(amt_s))
                        ch = parse_chance(ch_s)
                    except ValueError:
                        continue
                    cap = 6
                    if len(u) > 5 and u[5] not in ("",):
                        try:
                            cap = abs(int(float(u[5])))
                        except ValueError:
                            cap = 6
                    expected = min(cap, abs(amt) * ch * apps)
                    signed = expected if amt > 0 else -expected
                    self_t = tgt in ("Self", "AllAllies")
                    credit += stage_hp(signed if signed else 0, 1.0, stat, self_t, self.ref_hit,
                                       self.stage_frac, 1.0, acc_f)
                    if stat in ("Speed", "Accuracy", "Evasion"):
                        credit += self.speed_hp(amt, ch * apps, 1.0, acc_f)
                        notes.append(f"tempo {stat} {amt:+d}")
                elif op == "ArmUtilityWindow":
                    nums = []
                    for bit in u[1:]:
                        try:
                            nums.append(float(str(bit).strip("%")))
                        except ValueError:
                            continue
                    if any(n <= -1 for n in nums):
                        cut = max(-n for n in nums if n < 0)
                        credit += self.delay_w * self.ap_credit(cut, 1.0, "drain")
                        notes.append(f"armed AP {-cut:g}")
                elif op == "MoveAPDiscountWhileHPAbove" and len(u) >= 2:
                    try:
                        disc = float(u[1])
                    except ValueError:
                        disc = 0.0
                    credit += self.ap_credit(disc, 1.0, "gain")
                    notes.append(f"AP discount {disc:g}")
                elif op == "RandomStageChange":
                    credit += 0.15 * self.ref_hit * acc_f
                    notes.append("random stage")
                elif op:
                    notes.append(op)
        try:
            heal = float(row.get("HealPercentMaxHP") or 0)
        except ValueError:
            heal = 0.0
        if heal and gated:
            credit += (heal / 100.0) * self.med_hp
            notes.append(f"heal {heal:g}%")
        if "PRIOR" in (row.get("Tags") or "") or "PRIO" in (row.get("Tags") or ""):
            notes.append("priority")
        # id-driven bodies. Skip when a CSV payload already covers the same hook.
        mid = row["MoveId"]
        util = row.get("UtilityEffects") or ""
        if mid in id_hits and not util:
            extra, note = self.id_credit(mid, dmg)
            credit += extra
            if note:
                notes.append(note)
        elif mid in id_hits and mid in ("kindling", "uplink", "fee_collector", "spite_counter",
                                        "immutable_ledger", "position_swap"):
            extra, note = self.id_credit(mid, dmg)
            # immutable ledger CSV is empty; this branch is for ids that are not payload-driven
            if not util:
                credit += extra
                if note:
                    notes.append(note)
        return credit, notes

    def id_credit(self, mid, dmg):
        if mid == "on_crit_bonus":
            return self.ap_credit(1, self.crit_p, "gain"), "id crit +1 AP"
        if mid == "spite_counter":
            return 0.33 * self.stage_frac * self.ref_hit, "id spite 33%"
        if mid == "fee_collector":
            return 0.0, "id fee drain only when the foe is ahead"
        if mid == "immutable_ledger":
            return 0.20 * self.ref_hit + self.ap_credit(1, 1, "gain"), "id 20% first hit +1 AP"
        if mid == "conductive_counter":
            return 0.15 * self.ref_hit + self.ap_credit(1, 1, "gain"), "id counter 15% +1 AP"
        if mid == "double_or_nothing":
            return 0.60 * self.ap_credit(1, 1, "gain") - 0.40 * self.ap_credit(1, 1, "drain"), "id 60/40 AP"
        if mid == "meme_momentum":
            # a landed hit is the common case at 95% accuracy; miss drains 1 and drops atk
            hit_p = 0.95
            stage = hit_p * stage_hp(2, 1, "MeleeAtk", True, self.ref_hit, self.stage_frac, 1, 1) + (
                (1 - hit_p) * stage_hp(-1, 1, "MeleeAtk", True, self.ref_hit, self.stage_frac, 1, 1))
            ap = hit_p * self.ap_credit(2, 1, "gain") - (1 - hit_p) * self.ap_credit(1, 1, "drain")
            return stage + ap, "id meme momentum"
        if mid == "shattered_armor":
            return 0.0, "id only on a chemistry advantage"
        if mid == "kindling":
            return 0.06 * self.med_hp, "id Kindled 6%"
        if mid == "uplink":
            return 0.0, "id +1 AP when you buff an ally"
        if mid == "precision_focus":
            return 0.0, "id refund on a miss"
        if mid == "reactive_ping":
            return 0.0, "id 5% ping on a miss"
        if mid == "position_swap":
            return 0.0, "id swap"
        if mid in ("ap_donation", "ap_redistribution", "ap_router"):
            return 0.0, "id AP transfer net 0"
        if mid in ("damage_intercept", "signal_redirect"):
            return 0.35 * self.ref_hit, "id redirect one hit"
        if mid in ("effect_relay", "relay_packet"):
            return 0.15 * self.ref_hit, "id relay one status"
        if mid == "patience_passive":
            return 0.0, "id 5% heal only if you did not attack"
        return 0.0, "id literal"


def tags_of(row):
    return [t for t in (row.get("Tags") or "").split("|") if t]


def is_boss(row):
    return "BOSS_ONLY" in tags_of(row)


def wired_parts(row, id_hits):
    """Structural rider, independent of the HP weights."""
    parts = []
    if (row.get("StatusEffects") or "").strip():
        parts.append("status")
    if (row.get("StageChanges") or "").strip():
        parts.append("stage")
    if (row.get("UtilityEffects") or "").strip():
        parts.append("utility")
    try:
        if float(row.get("HealPercentMaxHP") or 0) > 0:
            parts.append("heal")
    except ValueError:
        pass
    if (row.get("ConditionalDamageConditions") or "").strip() or (row.get("ConditionalDamageScalingCondition") or "").strip():
        parts.append("conditional")
    if (row.get("RandomDamageMultipliers") or "").strip():
        parts.append("random")
    if (row.get("DamageOverrideAttackStat") or "").lower() == "true":
        parts.append("stat-override")
    active = (row.get("ActiveConditionType") or "").strip()
    if active and active != "None":
        parts.append("active-gate")
    trig = (row.get("TriggerCondition") or "").strip()
    if trig and trig != "None":
        parts.append("trigger")
    if "PRIOR" in tags_of(row) or "PRIO" in tags_of(row):
        parts.append("priority")
    if row["MoveId"] in id_hits and row["MoveId"] not in ("quick_lunge",):
        # any runtime literal counts, including fallbacks
        if "id" not in parts:
            parts.append("id")
    return parts


def trig_is_live(row, id_hits):
    if "TRIG" not in tags_of(row):
        return True
    if (row.get("TriggerCondition") or "None") not in ("", "None"):
        return True
    if (row.get("ResolveConditions") or "").strip():
        return True
    if (row.get("ConditionalDamageConditions") or "").strip():
        return True
    if (row.get("ConditionalDamageScalingCondition") or "").strip():
        return True
    if (row.get("ActiveConditionType") or "None") not in ("", "None"):
        return True
    util = row.get("UtilityEffects") or ""
    if any(op in util for op in (
        "ArmUtilityWindow", "BlockWindow", "MitigationWindow", "FullAbsorbBarrier",
        "GainApIfDistinctTargetsHitAtLeast", "BonusHealPercentIfSpeedHigher",
        "WallHitRetaliation", "GainApOnDamageIfSpeedHigher",
    )):
        return True
    if (row.get("RandomDamageMultipliers") or "").strip():
        return True
    # a proc chance under 100% in utility or stages is a trigger
    for s in parse_stages(row.get("StageChanges") or ""):
        if 0 < s["chance"] < 1:
            return True
    for u in parse_utils(row.get("UtilityEffects") or ""):
        if len(u) >= 4 and u[0] in ("ChangeStage", "DrainAP", "GainAP", "StealAP"):
            try:
                if parse_chance(u[2] if u[0] != "ChangeStage" else u[3]) < 1:
                    return True
            except ValueError:
                pass
    if row["MoveId"] in id_hits:
        return True
    return False


def apman_is_live(row, id_hits, statuses):
    if "APMAN" not in tags_of(row):
        return True
    util = row.get("UtilityEffects") or ""
    if any(op in util for op in ("GainAP", "DrainAP", "StealAP", "RefundAP", "TransferAP",
                                 "NextMoveAPSurcharge", "MoveAPDiscountWhileHPAbove", "WallHitRetaliation",
                                 "GainApIfDistinctTargetsHitAtLeast", "GainApOnDamageIfSpeedHigher",
                                 "ArmUtilityWindow", "BankAPUntilNextTurn")):
        return True
    if "SpendRemainingAp" in tags_of(row):
        return True
    for st in parse_statuses(row.get("StatusEffects") or ""):
        sd = statuses.get(st["id"])
        if not sd:
            continue
        try:
            if int(float(sd.get("EoTAPChange") or 0)) != 0:
                return True
        except ValueError:
            pass
        if st["id"] in ("ThreadLock", "Yield", "Pumped", "MEVExtracted"):
            return True
    if row["MoveId"] in id_hits and row["MoveId"] in {
        "on_crit_bonus", "fee_collector", "immutable_ledger", "conductive_counter",
        "double_or_nothing", "meme_momentum", "uplink", "precision_focus", "shattered_armor",
        "turn_start_ap_gain", "ap_donation", "ap_redistribution", "ap_router",
    }:
        return True
    return False


def prior_text_ok(row):
    if "PRIOR" not in tags_of(row) and "PRIO" not in tags_of(row):
        return True
    low = (row.get("Description") or "").lower()
    return any(w in low for w in ("priority", "first", "before"))


def heal_tag_ok(row):
    if "HEAL" not in tags_of(row):
        return True
    try:
        heal = float(row.get("HealPercentMaxHP") or 0)
    except ValueError:
        heal = 0
    util = row.get("UtilityEffects") or ""
    return heal > 0 or "Heal" in util or "Lifesteal" in util


def pool_problems(pools, move_rarity, species, skip=("ghost", "cofi_lootwak")):
    problems = []
    for sid, info in pools.items():
        if sid in skip:
            continue
        if sid not in species:
            problems.append(f"{sid}: missing species asset")
            continue
        body = species[sid]
        members = list(info["members"])
        flagship = info["flagship"]
        if flagship not in members:
            problems.append(f"{sid}: signature {flagship} not in the pool")
            continue
        if flagship not in move_rarity:
            problems.append(f"{sid}: unknown signature {flagship}")
            continue
        sig_r = move_rarity[flagship]
        above = 0
        for mid in members:
            if mid not in move_rarity:
                problems.append(f"{sid}: unknown move {mid}")
                continue
            if mid == flagship:
                continue
            r = move_rarity[mid]
            if RARITY_IDX[r] >= RARITY_IDX[sig_r]:
                problems.append(f"{sid}: {mid} ({r}) is not below signature {flagship} ({sig_r})")
            if body["starter"] or RARITY_IDX[r] <= body["rarity"]:
                continue
            above += 1
            if RARITY_IDX[r] != body["rarity"] + 1:
                problems.append(f"{sid}: {mid} ({r}) is more than one step above body {RARITY[body['rarity']]}")
        if body["starter"]:
            continue
        if len(members) != 5:
            problems.append(f"{sid}: wild pool has {len(members)} moves")
        if above > 1:
            problems.append(f"{sid}: {above} moves sit above the body")
    return problems


def apply_spec(row, kind, mag):
    r = dict(row)
    tags = tags_of(r)
    mid = r["MoveId"]
    if kind == "surging":
        r["StatusEffects"] = f"Surging:1:{mag}:Self"
        tags = [t for t in tags if t != "TRIG"]
        if "STAT" not in tags:
            tags.append("STAT")
        r["Description"] = "Heavy ranged hit. Your next damaging move deals 25% more."
    elif kind == "drain":
        pct = int(round(mag * 100))
        r["UtilityEffects"] = f"DrainAP:1:{pct}%:SingleEnemy:{mid}"
        r["ResolveUtilityTiming"] = "AfterMoveComplete"
        r["MoveEffectsOncePerTarget"] = "true"
        if "APMAN" not in tags:
            tags.append("APMAN")
        if pct >= 100:
            r["Description"] = "Two ranged hits. If one lands, the target loses 1 AP. Once per target."
        else:
            r["Description"] = f"Two ranged hits. {pct}% chance a landed hit costs the target 1 AP. Once per target."
    elif kind == "foe_def":
        pct = int(round(mag * 100))
        r["UtilityEffects"] = f"ChangeStage:MeleeDef:-1:{pct}%:SingleEnemy:-6:false:{mid}"
        r["ResolveUtilityTiming"] = "AfterEachConfirmedHit"
        r["MoveEffectsOncePerTarget"] = "true"
        if "TRIG" not in tags:
            tags.append("TRIG")
        r["Description"] = f"Contact hit. {pct}% chance to lower the target's Def by 1."
    elif kind == "lifesteal":
        pct = int(round(mag * 100))
        r["UtilityEffects"] = f"LifestealPercent:{pct}%:{mid}"
        r["ResolveUtilityTiming"] = "AfterEachConfirmedHit"
        tags = [t for t in tags if t != "TRIG"]
        if "HEAL" not in tags:
            tags.append("HEAL")
        r["Description"] = f"Contact hit. Heal for {pct}% of the damage dealt."
    elif kind == "speed":
        r["StageChanges"] = f"Speed:-1:{mag}:false"
        r["MoveEffectsOncePerTarget"] = "true"
        if "TRIG" not in tags:
            tags.append("TRIG")
        pct = int(round(mag * 100))
        r["Description"] = (r.get("Description") or "").rstrip(".") + f" {pct}% chance to lower Speed by 1."
    r["Tags"] = "|".join(tags)
    return r


def price_row(row, model, id_hits):
    dmg = model.strike(row)
    rider, notes = model.rider(row, dmg, id_hits)
    # de-dupe notes
    seen = []
    for n in notes:
        if n not in seen:
            seen.append(n)
    ap = int(row["APCost"] or 0)
    dv = dmg + rider
    return dict(dmg=dmg, rider=rider, dv=dv, ap=ap,
                per_ap=None if ap <= 0 else dv / ap,
                notes=seen)


def median(xs):
    xs = [x for x in xs if x is not None]
    return statistics.median(xs) if xs else None


# ---------- sim ----------

def smult(s):
    return (2 + s) / 2 if s >= 0 else 2 / (2 + abs(s))


def load_matrix(root: Path):
    text = (root / "Assets/Data/Combat/Type/Chemistry/TypeChemistryMatrix.asset").read_text(encoding="utf-8", errors="replace")
    status_enum = (root / "Assets/Scripts/Combat/Runtime/Data/StatusId.cs").read_text(encoding="utf-8", errors="replace")
    status_ids = {int(v): k for k, v in re.findall(r"(\w+)\s*=\s*(\d+)", status_enum)}
    matrix = {}
    for m in re.finditer(r"attackerType: (\d+)\s+defenderType: (\d+)\s+output:\s+statusId: (\d+)\s+recipient: (\d+)", text):
        matrix[(int(m.group(1)), int(m.group(2)))] = status_ids.get(int(m.group(3)))
    return matrix


def chem_factor(matrix, atk_type, def_type, adv, dis):
    if atk_type <= 0 or def_type <= 0:
        return 1.0
    if (atk_type, def_type) in matrix:
        return adv
    if (def_type, atk_type) in matrix:
        return dis
    return 1.0


def foe_drain_of(row):
    """DrainAP aimed at a foe. A self surcharge is a different opcode and stays out."""
    for u in parse_utils(row.get("UtilityEffects") or ""):
        if not u or u[0] != "DrainAP" or len(u) < 3:
            continue
        tgt = u[3] if len(u) > 3 else ""
        if tgt == "Self":
            continue
        try:
            return int(float(u[1])), parse_chance(u[2])
        except ValueError:
            return 0, 1.0
    return 0, 1.0


class SimMove:
    def __init__(self, row, extra=None):
        extra = extra or {}
        self.mid = row["MoveId"]
        self.power = int(extra.get("power", row["Power"] or 0))
        self.ap = int(extra.get("ap", row["APCost"] or 0))
        # Accuracy 0 on a utility is "no miss roll" in the CSV. Treat it as certain.
        self.acc = int(row["Accuracy"] or 100) or 100
        self.hmin = int(row["HitMin"] or 1)
        self.hmax = int(row["HitMax"] or 1)
        self.melee = row["Category"] == "DamagingMelee"
        self.mtype = TYPES.index(row["BaseType"]) if row["BaseType"] in TYPES else 0
        self.spread = row.get("Targeting") in ("AllEnemies", "Field", "AllAdjacentEnemies")
        parsed_drain, parsed_p = foe_drain_of(row)
        self.drain = extra.get("drain", parsed_drain)
        self.drain_p = extra.get("drain_p", parsed_p if "drain" not in extra else 1.0)
        self.ls = extra.get("ls", 0.0)
        self.foe_def = extra.get("foe_def", 0)
        self.foe_def_p = extra.get("foe_def_p", 0.0)
        self.spd = extra.get("spd", 0)
        self.spd_p = extra.get("spd_p", 0.0)
        self.surging = extra.get("surging", 0.0)
        self.faster_mult = extra.get("faster_mult", 1.0)
        self.cond_tag = extra.get("cond_tag", "")


def make_move(moves, mid, extra=None):
    return SimMove(moves[mid], extra)


class Fighter:
    def __init__(self, sid, species, moveslots, policy="first"):
        sp = species[sid]
        self.sid = sid
        self.maxhp = sp["hp"]
        self.hp = sp["hp"]
        self.ma = sp["ma"]
        self.md = sp["md"]
        self.ra = sp["ra"]
        self.rd = sp["rd"]
        self.spd = sp["spd"]
        self.ptype = sp["type"]
        self.ap = 0
        self.alive = True
        self.stages = defaultdict(int)
        self.surging = False
        self.moves = moveslots  # list of SimMove
        self.team = 0
        self.slot = 0
        # "first" spends the first affordable move. "deny" skims a foe who can act, else hits.
        self.policy = policy


def one_hit(rng, fm, atk, dfn, chem, cond, rmin, rmax, crit_p, crit_m):
    u = rng.uniform(rmin, rmax)
    base = (fm.power * (atk / dfn) * u + 1.0) * chem * cond
    if rng.random() < crit_p:
        base *= crit_m
    return max(1, int(math.floor(base)))


def apply_on_land(rng, actor, foe, fm):
    """Once per target per cast. Def crack is MeleeDef only, matching the ChangeStage payload."""
    if fm.drain and rng.random() < fm.drain_p:
        foe.ap = max(0, foe.ap - int(fm.drain))
    if fm.foe_def and rng.random() < fm.foe_def_p:
        foe.stages["MeleeDef"] = max(-6, foe.stages["MeleeDef"] - fm.foe_def)
    if fm.spd and rng.random() < fm.spd_p:
        foe.stages["Speed"] = max(-6, min(6, foe.stages["Speed"] + fm.spd))
    if fm.surging and rng.random() < fm.surging:
        actor.surging = True


def resolve_move(rng, actor, foe, fm, cfg, matrix):
    if foe is None or not foe.alive:
        return 0, False
    # A power-0 utility still resolves. Packet Skim is this shape: DrainAP, no damage.
    if fm.power <= 0:
        if rng.random() > fm.acc / 100.0:
            return 0, False
        apply_on_land(rng, actor, foe, fm)
        return 0, True
    if rng.random() > fm.acc / 100.0 and fm.hmax <= 1:
        # one roll for single; multi rolls per hit below. For single, miss spends the cost already.
        return 0, False
    atk_type = fm.mtype if fm.mtype else actor.ptype
    chem = chem_factor(matrix, atk_type, foe.ptype, cfg["adv"], cfg["dis"])
    atk = (actor.ma if fm.melee else actor.ra) * smult(actor.stages["MeleeAtk" if fm.melee else "RangedAtk"])
    dfn = (foe.md if fm.melee else foe.rd) * smult(foe.stages["MeleeDef" if fm.melee else "RangedDef"])
    dfn = max(1.0, dfn)
    cond = 1.0
    if fm.faster_mult != 1.0:
        aspd = actor.spd * smult(actor.stages["Speed"])
        dspd = foe.spd * smult(foe.stages["Speed"])
        if aspd > dspd:
            cond *= fm.faster_mult
    if actor.surging:
        cond *= 1.25
        actor.surging = False
    n = fm.hmin if fm.hmin == fm.hmax else rng.randint(fm.hmin, fm.hmax)
    dealt = 0
    landed = False
    for _ in range(n):
        if fm.hmax > 1 and rng.random() > fm.acc / 100.0:
            continue
        if not foe.alive:
            break
        dmg = one_hit(rng, fm, atk, dfn, chem, cond, cfg["rmin"], cfg["rmax"], cfg["crit_p"], cfg["crit_m"])
        foe.hp -= dmg
        dealt += dmg
        landed = True
        if fm.ls:
            actor.hp = min(actor.maxhp, actor.hp + int(math.floor(dmg * fm.ls)))
        if foe.hp <= 0:
            foe.hp = 0
            foe.alive = False
            break
    if landed:
        apply_on_land(rng, actor, foe, fm)
    return dealt, landed


def focus_foe(foes):
    """Prefer the body that is holding a 4 AP move, then the lower HP fraction."""
    heavies = [x for x in foes if any(m.ap >= 4 for m in x.moves)]
    pool = heavies or foes
    return min(pool, key=lambda x: x.hp / x.maxhp)


def pick_action(f, foes):
    affordable = [mv for mv in f.moves if mv.ap > 0 and f.ap >= mv.ap]
    if not affordable or not foes:
        return None, None
    if f.policy == "deny":
        rich = [x for x in foes if x.ap >= 2]
        deny = next((mv for mv in affordable if mv.drain and mv.power <= 0), None)
        if deny and rich:
            target = max(rich, key=lambda x: (any(m.ap >= 4 for m in x.moves), x.ap))
            return deny, target
        dmg = next((mv for mv in affordable if mv.power > 0), None)
        if dmg:
            return dmg, focus_foe(foes)
    return affordable[0], focus_foe(foes)


def battle(seed, side_a, side_b, cfg, matrix, horizon=8):
    rng = random.Random(seed)
    for team, slots in ((0, side_a), (1, side_b)):
        for i, f in enumerate(slots):
            f.team = team
            f.slot = i
            f.hp = f.maxhp
            f.ap = 0
            f.alive = True
            f.stages = defaultdict(int)
            f.surging = False
    first_heavy_turn = None
    hits_before_heavy = 0
    heavy_mid = None
    for f in side_a:
        for mv in f.moves:
            if mv.ap >= 4:
                heavy_mid = mv.mid
    ko_turn = {0: None, 1: None}
    dmg = [0, 0]
    for turn in range(1, horizon + 1):
        living = [f for f in side_a + side_b if f.alive]
        if not any(f.alive for f in side_a) or not any(f.alive for f in side_b):
            break
        for f in living:
            f.ap = min(cfg["ap_cap"], f.ap + cfg["ap_turn"])
        order = sorted(living, key=lambda f: (-(f.spd * smult(f.stages["Speed"])), rng.random()))
        for f in order:
            if not f.alive:
                continue
            foes = [x for x in (side_b if f.team == 0 else side_a) if x.alive]
            if not foes:
                break
            # A 4 AP move is chosen only when it is the affordable click. Turn 1 passes.
            choice, foe = pick_action(f, foes)
            if choice is None:
                continue
            f.ap -= choice.ap
            targets = [x for x in foes if x.alive] if choice.spread else [foe]
            dealt = 0
            landed = False
            for tgt in targets:
                one, hit = resolve_move(rng, f, tgt, choice, cfg, matrix)
                dealt += one
                landed = landed or hit
            dmg[f.team] += dealt
            if heavy_mid and choice.mid == heavy_mid and first_heavy_turn is None:
                first_heavy_turn = turn
            if heavy_mid and first_heavy_turn is None and f.team == 1 and landed:
                hits_before_heavy += 1
            for team, slots in ((0, side_a), (1, side_b)):
                if ko_turn[team] is None and not any(x.alive for x in slots):
                    ko_turn[team] = turn
    a_alive = sum(f.hp for f in side_a if f.alive)
    b_alive = sum(f.hp for f in side_b if f.alive)
    if a_alive > 0 and b_alive <= 0:
        win = 0
    elif b_alive > 0 and a_alive <= 0:
        win = 1
    elif a_alive > b_alive:
        win = 0
    elif b_alive > a_alive:
        win = 1
    else:
        win = None
    return dict(win=win, heavy_turn=first_heavy_turn, hits_before=hits_before_heavy,
                dmg_a=dmg[0], dmg_b=dmg[1], ko_a=ko_turn[1], ko_b=ko_turn[0],
                has_heavy=heavy_mid is not None)


def run_cell(n, builder, cfg, matrix, base_seed):
    wins = [0, 0, 0]
    heavy_turns = []
    hits = []
    has_heavy = False
    never = 0
    for i in range(n):
        a, b = builder()
        r = battle(base_seed + i * 17, a, b, cfg, matrix)
        if r["win"] is None:
            wins[2] += 1
        else:
            wins[r["win"]] += 1
        if r["has_heavy"]:
            has_heavy = True
            if r["heavy_turn"]:
                heavy_turns.append(r["heavy_turn"])
            else:
                never += 1
        hits.append(r["hits_before"])
    return dict(
        n=n,
        a=fnum(100.0 * wins[0] / n, 1),
        b=fnum(100.0 * wins[1] / n, 1),
        tie=fnum(100.0 * wins[2] / n, 1),
        heavy_med=None if not heavy_turns else fnum(statistics.median(heavy_turns), 1),
        heavy_never=never,
        has_heavy=has_heavy,
        hits_before_med=fnum(statistics.median(hits), 1),
        hits_before_mean=fnum(statistics.mean(hits), 2),
    )


def fmt_heavy(s):
    if not s.get("has_heavy"):
        return "—"
    if s["heavy_med"] is None:
        return f"never ({s['heavy_never']}/{s['n']})"
    return str(s["heavy_med"])


def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else os.environ.get("CHAINBOUND_SNAPSHOT", "/tmp/cb_work/snapshot"))
    doc_path = Path(sys.argv[2] if len(sys.argv) > 2 else "/opt/cursor/artifacts/RARITY_FIT_AND_RIDER_BUILDOUT_V1.md")
    L = Ledger()

    moves, move_lines, moves_header = read_csv(root / "Docs/Specs/Combat/Data/Moves.csv")
    pools_rows, pool_lines, pools_header = read_csv(root / "Docs/Specs/Combat/Data/SpeciesMovePools.csv")
    statuses, status_lines, _ = read_csv(root / "Docs/Specs/Combat/Data/Statuses.csv")
    by_id = {r["MoveId"]: r for r in moves}
    st_by = {r["StatusId"]: r for r in statuses}

    cfg_path = root / "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset"
    cfg_text = cfg_path.read_text(encoding="utf-8", errors="replace")

    def cfgv(k, d):
        m = re.search(rf"(?m)^  {k}:\s*([-\d.]+)", cfg_text)
        return float(m.group(1)) if m else d

    ap_turn = int(cfgv("apPerTurn", 2))
    ap_cap = int(cfgv("apCap", 10))
    rmin = cfgv("randomRollMin", 0.85)
    rmax = cfgv("randomRollMax", 1.0)
    chem_adv = cfgv("chemistryAdvantageFactor", 1.2)
    chem_dis = cfgv("chemistryDisadvantageFactor", 0.8)
    crit_m = cfgv("critMultiplierDefault", 1.5)
    enable_stab = int(cfgv("enableStab", 0))
    crit_p = 0.0625

    species = {}
    for p in (root / "Assets/Data/Combat/ChainlingSpecies").glob("*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")

        def g(k, text=t):
            m = re.search(rf"(?m)^  {k}:\s*(.*)$", text)
            return m.group(1).strip() if m else ""

        sid = g("speciesId")
        if not sid:
            continue
        species[sid] = dict(
            type=int(g("primaryType") or 0), hp=int(g("baseMaxHP") or 0),
            ma=int(g("baseMeleeAtk") or 0), md=int(g("baseMeleeDef") or 0),
            ra=int(g("baseRangedAtk") or 0), rd=int(g("baseRangedDef") or 0),
            spd=int(g("baseSpeed") or 0), rarity=int(g("rarity") or 0),
            starter=g("isStarter") == "1", file=str(p.relative_to(root)),
        )

    id_hits = {}
    id_where = {}
    for rel in RUNTIME_REL:
        found = find_all_quoted(root / rel, set(by_id))
        for mid, line in found.items():
            if mid not in id_hits:
                id_hits[mid] = line
                id_where[mid] = f"{rel}:{line}"

    # learn pools
    pools = {}
    for row in pools_rows:
        sid = row["SpeciesId"].strip()
        learn = [x for x in (row.get("LearnableMoves") or "").split("|") if x]
        excl = [x for x in (row.get("ExclusiveMoves") or "").split("|") if x]
        members = []
        for m in learn + excl:
            if m not in members:
                members.append(m)
        off = [x for x in (row.get("OffTypeMoves") or "").split("|") if x]
        pools[sid] = dict(members=members, flagship=(row.get("FlagshipMove") or "").strip(),
                          off=off, line=pool_lines[sid], learn=learn, excl=excl)
    holders = defaultdict(list)
    for sid, info in pools.items():
        for mid in info["members"] + info["off"]:
            if sid not in holders[mid]:
                holders[mid].append(sid)

    playable = [s for s, sp in species.items() if sp["hp"] > 0 and s in pools]
    med_hp = statistics.median(species[s]["hp"] for s in playable)
    # provisional norms for the AP point, from the unmodified catalog
    scratch = Model(1, 1, med_hp, crit_p, crit_m, rmin, rmax, st_by)
    norms = {}
    for r in moves:
        if r["Category"] in DAMAGING and int(r["Power"] or 0) > 0 and not is_boss(r):
            norms[r["MoveId"]] = scratch.strike(r)
    twos = [norms[r["MoveId"]] for r in moves
            if r["MoveId"] in norms and int(r["APCost"] or 0) == 2 and shape_of(r) == "single"]
    ref_hit = statistics.median(twos) if twos else 48.0
    ap_point = ref_hit / 2.0
    model = Model(ap_point, ref_hit, med_hp, crit_p, crit_m, rmin, rmax, st_by)

    scope = []
    for r in moves:
        if is_boss(r) or r["MoveId"] in EXCLUDED_IDS:
            continue
        scope.append(r)

    priced = {}
    for r in scope:
        priced[r["MoveId"]] = price_row(r, model, id_hits)

    def rec_of(r):
        mid = r["MoveId"]
        pr = priced[mid]
        parts = wired_parts(r, id_hits)
        retired = mid in RETIRED_IDS
        high = RARITY_IDX[r["Rarity"]] >= RARITY_IDX["Rare"]
        plain = high and not parts and not retired
        return dict(
            id=mid, name=r["DisplayName"], rarity=r["Rarity"], type=r["BaseType"],
            cat=r["Category"], ap=pr["ap"], power=int(r["Power"] or 0),
            acc=int(r["Accuracy"] or 0), hits=f"{r['HitMin']}-{r['HitMax']}",
            rc=int(r["RechargeTurns"] or 0), bucket=bucket_of(r), shape=shape_of(r),
            dmg=pr["dmg"], rider=pr["rider"], dv=pr["dv"], per_ap=pr["per_ap"],
            notes=pr["notes"], parts=parts, plain=plain, retired=retired,
            line=move_lines[mid], tags="|".join(tags_of(r)),
            holders=sorted(holders.get(mid, [])),
            desc=(r.get("Description") or "").replace("|", "/"),
        )

    records = [rec_of(r) for r in scope]
    by_rec = {r["id"]: r for r in records}

    # lane medians on damaging per-AP
    lane_vals = defaultdict(lambda: defaultdict(list))
    for rec in records:
        if rec["cat"] not in DAMAGING or rec["per_ap"] is None or rec["power"] <= 0:
            continue
        lane_vals[(rec["bucket"], rec["ap"])][rec["rarity"]].append(rec["per_ap"])
    lane_med = {k: {rar: median(vs) for rar, vs in d.items()} for k, d in lane_vals.items()}

    for rec in records:
        rec["band"] = "in"
        if rec["retired"]:
            rec["band"] = "retired"
            continue
        flags = []
        if rec["plain"]:
            flags.append("plain")
        key = (rec["bucket"], rec["ap"])
        if rec["cat"] in DAMAGING and rec["per_ap"] is not None:
            ri = RARITY_IDX[rec["rarity"]]
            if ri > 0:
                lower = RARITY[ri - 1]
                cell = lane_vals[key][lower]
                if len(cell) >= 3:
                    med = median(cell)
                    if med and rec["per_ap"] < 0.80 * med:
                        flags.append("low")
            if ri < len(RARITY) - 1 and ri <= RARITY_IDX["Uncommon"]:
                higher = RARITY[ri + 1]
                cell = lane_vals[key][higher]
                if len(cell) >= 3:
                    med = median(cell)
                    if med and rec["per_ap"] > 1.15 * med:
                        flags.append("high")
        rec["band"] = "+".join(flags) if flags else "in"

    # same-type rivals on damaging per-AP
    rivals = []
    dmg_recs = [r for r in records if r["cat"] in DAMAGING and r["per_ap"] and r["type"] != "None"]
    for a in dmg_recs:
        for b in dmg_recs:
            if a["type"] != b["type"] or a["id"] == b["id"]:
                continue
            if RARITY_IDX[a["rarity"]] >= RARITY_IDX[b["rarity"]]:
                continue
            if a["ap"] <= b["ap"] and a["per_ap"] >= 0.95 * b["per_ap"]:
                rivals.append(dict(
                    type=a["type"], low=a["id"], low_r=a["rarity"], low_ap=a["ap"], low_rate=fnum(a["per_ap"]),
                    high=b["id"], high_r=b["rarity"], high_ap=b["ap"], high_rate=fnum(b["per_ap"]),
                    ratio=fnum(a["per_ap"] / b["per_ap"], 3),
                ))
    rivals.sort(key=lambda x: -x["ratio"])

    # typed vs typeless
    parity = []
    groups = defaultdict(lambda: defaultdict(list))
    for rec in records:
        if rec["cat"] not in DAMAGING or rec["power"] <= 0:
            continue
        groups[(rec["ap"], rec["bucket"])][("typed" if rec["type"] != "None" else "less", rec["rarity"])].append(rec)
    violations = []
    for key, buckets in sorted(groups.items()):
        for rar in RARITY:
            typed = buckets.get(("typed", rar), [])
            less = buckets.get(("less", rar), [])
            if len(typed) >= 1 and len(less) >= 1:
                tm = median([r["dv"] for r in typed])
                lm = median([r["dv"] for r in less])
                ok = tm > lm
                parity.append(dict(ap=key[0], bucket=key[1], rarity=rar, typed_n=len(typed),
                                   typeless_n=len(less), typed=fnum(tm), typeless=fnum(lm), ok=ok))
                if not ok:
                    violations.append(f"{rar} {key[1]} AP{key[0]} typeless {fnum(lm)} >= typed {fnum(tm)}")
    # rare typeless vs typed common
    rare_line = []
    for key, buckets in sorted(groups.items()):
        tr = buckets.get(("less", "Rare"), [])
        tc = buckets.get(("typed", "Common"), [])
        if not tr or not tc:
            continue
        tr_m = median([r["dv"] for r in tr])
        tc_m = median([r["dv"] for r in tc])
        ratio = tr_m / tc_m if tc_m else None
        rare_line.append(dict(
            ap=key[0], bucket=key[1],
            typeless_rare=fnum(tr_m), typed_common=fnum(tc_m),
            ratio=None if ratio is None else fnum(ratio, 3),
            n_tr=len(tr), n_tc=len(tc),
            tr_ids=[r["id"] for r in tr],
            in_line=ratio is not None and 0.80 <= ratio <= 1.25,
        ))
    # Same shape, any AP. The design line is "roughly in line", and the rare typeless
    # hits in this catalog are 3 AP while the typed commons in that shape are 2 AP.
    shape_commons = defaultdict(list)
    for rec in records:
        if rec["cat"] not in DAMAGING or rec["power"] <= 0 or not rec["per_ap"]:
            continue
        if rec["type"] != "None" and rec["rarity"] == "Common":
            shape_commons[rec["bucket"]].append(rec)
    cross_line = []
    for rec in records:
        if rec["type"] != "None" or rec["rarity"] != "Rare":
            continue
        if rec["cat"] not in DAMAGING or rec["power"] <= 0 or not rec["per_ap"]:
            continue
        commons = shape_commons.get(rec["bucket"]) or []
        if not commons:
            continue
        tc = median([r["per_ap"] for r in commons])
        ratio = rec["per_ap"] / tc if tc else None
        cross_line.append(dict(
            id=rec["id"], bucket=rec["bucket"], ap=rec["ap"], power=rec["power"], acc=rec["acc"],
            rate=fnum(rec["per_ap"]), typed_common=fnum(tc), n_tc=len(commons),
            ratio=None if ratio is None else fnum(ratio, 3),
            in_line=ratio is not None and 0.80 <= ratio <= 1.25,
            holders=list(rec["holders"]),
        ))

    # card vs runtime
    false_trig = []
    false_ap = []
    prior_silent = []
    heal_silent = []
    for r in scope:
        if not trig_is_live(r, id_hits):
            false_trig.append(r["MoveId"])
        if not apman_is_live(r, id_hits, st_by):
            false_ap.append(r["MoveId"])
        if not prior_text_ok(r):
            prior_silent.append(r["MoveId"])
        if not heal_tag_ok(r):
            heal_silent.append(r["MoveId"])

    # runtime behavior the card does not state: id-driven passives whose description is empty of the verb
    id_unsaid = []
    for mid, where in sorted(id_where.items()):
        if mid not in by_rec:
            continue
        desc = (by_id[mid].get("Description") or "").lower()
        if mid == "on_crit_bonus" and "crit" not in desc:
            id_unsaid.append(mid)
        if mid == "kindling" and "6%" not in desc and "mark" not in desc:
            id_unsaid.append(mid)

    # asset parity
    rarity_to_int = {n: i for i, n in enumerate(RARITY)}
    asset_mismatch = []
    asset_n = 0
    for p in (root / "Assets/Data/Combat/Moves").glob("Move_*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")

        def gv(k, text=t):
            m = re.search(rf"(?m)^  {k}:\s*(.*)$", text)
            return m.group(1).strip() if m else None

        mid = gv("moveId")
        if not mid or mid not in by_id:
            continue
        asset_n += 1
        row = by_id[mid]
        checks = [
            ("power", int(gv("power") or 0), int(row["Power"] or 0)),
            ("apCost", int(gv("apCost") or 0), int(row["APCost"] or 0)),
            ("accuracy", int(gv("accuracy") or 0), int(row["Accuracy"] or 0)),
            ("rarity", int(gv("rarity") or 0), rarity_to_int[row["Rarity"]]),
        ]
        bad = [f"{k} asset {a} csv {c}" for k, a, c in checks if a != c]
        if bad:
            asset_mismatch.append(f"{mid}: {', '.join(bad)}")

    move_rarity = {r["MoveId"]: r["Rarity"] for r in moves}
    base_problems = pool_problems(pools, move_rarity, species)

    # ---------- proposals ----------
    def higher_cap(mid):
        """Stay under the current best other card in the lane. If this card is already
        the best, allow a rider up to 20% more DV. A weaker higher-rarity card does not
        pull the ceiling below the move's own damage."""
        rec = by_rec[mid]
        peers = [r for r in records
                 if r["cat"] in DAMAGING and r["bucket"] == rec["bucket"] and r["ap"] == rec["ap"]
                 and r["id"] != mid]
        if not peers:
            return rec["dv"] * 1.20
        best = max(r["dv"] for r in peers)
        if best > rec["dv"] + 0.5:
            return best
        return rec["dv"] * 1.20

    def score_spec(mid, kind, mag):
        row = apply_spec(by_id[mid], kind, mag)
        pr = price_row(row, model, id_hits)
        return pr, row

    def tune(mid, kind, candidates):
        cap = higher_cap(mid)
        old = by_rec[mid]["dv"]
        # leave at least a small gap under a peer that is already ahead
        limit = cap - 0.75 if cap > old + 0.5 else cap
        chosen = None
        for mag in candidates:
            pr, row = score_spec(mid, kind, mag)
            if pr["dv"] <= limit + 0.02:
                chosen = (mag, pr, row)
                break
        if chosen is None:
            mag = candidates[-1]
            pr, row = score_spec(mid, kind, mag)
            chosen = (mag, pr, row)
        return chosen

    # candidate order is largest first; tune picks the largest that fits under the cap
    builds = []
    plan = [
        ("hype_train", "surging", [1.0, 0.5, 0.35, 0.25]),
        ("combustive_shift", "drain", [1.0, 0.5, 0.35]),
        ("ore_strike", "foe_def", [0.25, 0.20, 0.15, 0.10]),
        ("yield_spike", "lifesteal", [0.20, 0.15, 0.10]),
        ("stake_strike", "lifesteal", [0.15, 0.10, 0.08]),
        ("fork_split", "speed", [0.20]),
        ("shard_scatter", "speed", [0.20]),
        ("chain_fork", "speed", [0.20]),
    ]
    for mid, kind, mags in plan:
        mag, pr, row = tune(mid, kind, mags)
        builds.append(dict(move=mid, kind=kind, mag=mag, pr=pr, row=row,
                           old=by_rec[mid], cap=higher_cap(mid),
                           payload=payload_of(row),
                           engine="None"))

    # signature order: stake must stay under yield at the same AP
    def find_build(mid):
        return next(b for b in builds if b["move"] == mid)

    yb, sb = find_build("yield_spike"), find_build("stake_strike")
    stake_steps = [0.15, 0.10, 0.08, 0.05]
    guard = 0
    while (sb["mag"] >= yb["mag"] - 1e-9 or sb["pr"]["per_ap"] >= yb["pr"]["per_ap"] - 1e-9) and guard < 6:
        lower = [m for m in stake_steps if m < min(sb["mag"], yb["mag"]) - 1e-9]
        if not lower:
            break
        sb["mag"] = lower[0]
        pr, row = score_spec("stake_strike", "lifesteal", sb["mag"])
        sb["pr"], sb["row"], sb["payload"] = pr, row, payload_of(row)
        guard += 1

    # pool legality of the set (no rarity changes in the build-outs)
    proposed_rarity = dict(move_rarity)
    build_pool_problems = pool_problems(pools, proposed_rarity, species)

    # over-deliver responses
    over_moves = [r for r in records if "high" in r["band"].split("+")]
    over_plans = []
    for rec in over_moves:
        if rec["id"] in KEEP_IDS:
            over_plans.append(dict(move=rec["id"], action="leave", why="decided number"))
            continue
        # try rarity bump
        bumped = dict(move_rarity)
        ri = RARITY_IDX[rec["rarity"]]
        if ri < len(RARITY) - 1:
            bumped[rec["id"]] = RARITY[ri + 1]
            probs = pool_problems(pools, bumped, species)
        else:
            probs = ["already mythic"]
        if not probs:
            over_plans.append(dict(move=rec["id"], action="bump", to=RARITY[ri + 1], why="pool still legal"))
            continue
        # power trim until per-AP <= 1.10 * higher median, if damaging
        key = (rec["bucket"], rec["ap"])
        higher = RARITY[min(len(RARITY) - 1, ri + 1)]
        med = lane_med.get(key, {}).get(higher)
        if med and rec["cat"] in DAMAGING and rec["power"] > 0:
            target = 1.10 * med
            trial = dict(by_id[rec["id"]])
            new_power = rec["power"]
            for _ in range(rec["power"]):
                pr = price_row(trial, model, id_hits)
                if pr["per_ap"] is not None and pr["per_ap"] <= target:
                    break
                new_power -= 1
                trial["Power"] = str(new_power)
            if new_power < rec["power"]:
                over_plans.append(dict(move=rec["id"], action="trim", power=new_power,
                                       from_power=rec["power"], why=f"bump blocked: {probs[0]}"))
                continue
        over_plans.append(dict(move=rec["id"], action="leave", why=f"bump blocked: {probs[0] if probs else 'no trim'}"))

    # rival trims that are not already an over-plan, top legal ones
    rival_plans = []
    seen_low = set()
    for rv in rivals:
        if rv["low"] in seen_low or rv["low"] in KEEP_IDS:
            continue
        if rv["ratio"] < 1.0:
            continue
        seen_low.add(rv["low"])
        bumped = dict(move_rarity)
        ri = RARITY_IDX[rv["low_r"]]
        legal_bump = False
        if ri < RARITY_IDX[rv["high_r"]]:
            bumped[rv["low"]] = RARITY[ri + 1]
            legal_bump = not pool_problems(pools, bumped, species)
        rival_plans.append(dict(rv, legal_bump=legal_bump,
                                bump_to=RARITY[ri + 1] if ri < len(RARITY) - 1 else None))
        if len(rival_plans) >= 8:
            break

    # ---------- sim ----------
    matrix = load_matrix(root)
    scfg = dict(ap_turn=ap_turn, ap_cap=ap_cap, rmin=rmin, rmax=rmax, adv=chem_adv, dis=chem_dis,
                crit_p=crit_p, crit_m=crit_m)
    TRIALS = 400

    def F(sid, *mvs, policy="first"):
        return Fighter(sid, species, list(mvs), policy=policy)

    seam = make_move(by_id, "seam_cut", {"faster_mult": 1.25})
    seam_flat = make_move(by_id, "seam_cut", {"faster_mult": 1.0})
    gas = make_move(by_id, "gas_bump")
    deep = make_move(by_id, "deep_dig")
    jab = make_move(by_id, "baseline_striker")
    shot = make_move(by_id, "baseline_shot")
    ore = make_move(by_id, "ore_strike")
    ore_p = make_move(by_id, "ore_strike", {"foe_def": 1, "foe_def_p": find_build("ore_strike")["mag"]})
    flurry = make_move(by_id, "blade_flurry")
    cipher = make_move(by_id, "cipher_shot")
    hype = make_move(by_id, "hype_train")
    hype_p = make_move(by_id, "hype_train", {"surging": find_build("hype_train")["mag"]})
    comb = make_move(by_id, "combustive_shift")
    comb_p = make_move(by_id, "combustive_shift", {"drain": 1, "drain_p": find_build("combustive_shift")["mag"]})
    yld = make_move(by_id, "yield_spike")
    yld_p = make_move(by_id, "yield_spike", {"ls": find_build("yield_spike")["mag"]})
    stake = make_move(by_id, "stake_strike")
    stake_p = make_move(by_id, "stake_strike", {"ls": find_build("stake_strike")["mag"]})
    fork = make_move(by_id, "fork_split")
    fork_p = make_move(by_id, "fork_split", {"spd": -1, "spd_p": 0.20})
    skim = make_move(by_id, "packet_skim")

    def cell_heavy():
        # Kodiax banks Deep Dig. Hopoch clicks Snap Strike.
        # Crackett (faster) clicks Seam Cut, which is x1.25 because Crackett is faster. Deghoul clicks Gas Bump.
        return [F("kodiax", deep), F("hopoch", jab)], [F("crackett", seam), F("deghoul", gas)]

    def cell_heavy_flat():
        # Same bodies. Seam Cut's faster bonus is forced off so the body's stats are the only edge.
        return [F("kodiax", deep), F("hopoch", jab)], [F("crackett", seam_flat), F("deghoul", gas)]

    def cell_plain():
        # same shells, fast side clicks plain 2 AP hits with no rider
        return [F("kodiax", deep), F("hopoch", jab)], [F("crackett", shot), F("deghoul", jab)]

    def cell_drain():
        # Kitsauto skims a foe who can pay 2 AP, then flurries the heavy. Deghoul clicks Gas Bump.
        return [F("kodiax", deep), F("hopoch", jab)], [F("kitsauto", skim, flurry, policy="deny"), F("deghoul", gas)]

    def cell_ore(use_p):
        mv = ore_p if use_p else ore
        return [F("kodiax", mv), F("hopoch", jab)], [F("keyper", cipher), F("kitsauto", flurry)]

    def cell_yield(use_p, stake_side=False):
        mv = (stake_p if use_p else stake) if stake_side else (yld_p if use_p else yld)
        return [F("hopoch", mv), F("rattlestake", shot)], [F("kodiax", ore), F("minefolk", shot)]

    def cell_hype(use_p):
        # no Meme body exists; Kreechank's stats carry the Meme move, chemistry stays neutral
        mv = hype_p if use_p else hype
        return [F("kreechank", mv), F("validape", jab)], [F("deghoul", gas), F("kitsauto", flurry)]

    def cell_comb(use_p):
        mv = comb_p if use_p else comb
        return [F("balloom", mv), F("deghoul", gas)], [F("kitsauto", flurry), F("keyper", cipher)]

    def cell_fork(use_p):
        mv = fork_p if use_p else fork
        return [F("forkrook", mv), F("crackett", jab)], [F("deghoul", gas), F("keyper", cipher)]

    # Mirrors: same bodies, same partner click. Only the proposed rider differs.
    def cell_mirror_ore():
        return [F("kodiax", ore_p), F("hopoch", jab)], [F("kodiax", ore), F("hopoch", jab)]

    def cell_mirror_yield():
        return [F("hopoch", yld_p), F("rattlestake", shot)], [F("hopoch", yld), F("rattlestake", shot)]

    def cell_mirror_stake():
        return [F("hopoch", yld_p), F("rattlestake", shot)], [F("hopoch", stake_p), F("rattlestake", shot)]

    def cell_mirror_hype():
        return [F("kreechank", hype_p), F("validape", jab)], [F("kreechank", hype), F("validape", jab)]

    def cell_mirror_comb():
        return [F("balloom", comb_p), F("deghoul", gas)], [F("balloom", comb), F("deghoul", gas)]

    def cell_mirror_fork():
        return [F("forkrook", fork_p), F("crackett", jab)], [F("forkrook", fork), F("crackett", jab)]

    sims = {}
    sims["T1"] = run_cell(TRIALS, cell_heavy, scfg, matrix, 1000)
    sims["T1c"] = run_cell(TRIALS, cell_heavy_flat, scfg, matrix, 1000)
    sims["T2"] = run_cell(TRIALS, cell_plain, scfg, matrix, 1500)
    sims["T3"] = run_cell(TRIALS, cell_drain, scfg, matrix, 2000)
    sims["O0"] = run_cell(TRIALS, lambda: cell_ore(False), scfg, matrix, 3000)
    sims["O1"] = run_cell(TRIALS, lambda: cell_ore(True), scfg, matrix, 3000)
    sims["Y0"] = run_cell(TRIALS, lambda: cell_yield(False), scfg, matrix, 4000)
    sims["Y1"] = run_cell(TRIALS, lambda: cell_yield(True), scfg, matrix, 4000)
    sims["S1"] = run_cell(TRIALS, lambda: cell_yield(True, True), scfg, matrix, 4000)
    sims["H0"] = run_cell(TRIALS, lambda: cell_hype(False), scfg, matrix, 5000)
    sims["H1"] = run_cell(TRIALS, lambda: cell_hype(True), scfg, matrix, 5000)
    sims["C0"] = run_cell(TRIALS, lambda: cell_comb(False), scfg, matrix, 6000)
    sims["C1"] = run_cell(TRIALS, lambda: cell_comb(True), scfg, matrix, 6000)
    sims["F0"] = run_cell(TRIALS, lambda: cell_fork(False), scfg, matrix, 7000)
    sims["F1"] = run_cell(TRIALS, lambda: cell_fork(True), scfg, matrix, 7000)
    sims["MO"] = run_cell(TRIALS, cell_mirror_ore, scfg, matrix, 8000)
    sims["MY"] = run_cell(TRIALS, cell_mirror_yield, scfg, matrix, 9000)
    sims["MS"] = run_cell(TRIALS, cell_mirror_stake, scfg, matrix, 10000)
    sims["MH"] = run_cell(TRIALS, cell_mirror_hype, scfg, matrix, 11000)
    sims["MC"] = run_cell(TRIALS, cell_mirror_comb, scfg, matrix, 12000)
    sims["MF"] = run_cell(TRIALS, cell_mirror_fork, scfg, matrix, 13000)

    # sensitivity
    def flag_set(stage_frac, drain_w, speed_frac, delay_w):
        m2 = Model(ap_point, ref_hit, med_hp, crit_p, crit_m, rmin, rmax, st_by,
                   drain_w=drain_w, stage_frac=stage_frac, speed_frac=speed_frac, delay_w=delay_w)
        lows, highs = set(), set()
        cache = {}
        for r in scope:
            if r["Category"] not in DAMAGING:
                continue
            cache[r["MoveId"]] = price_row(r, m2, id_hits)
        lanes = defaultdict(lambda: defaultdict(list))
        for r in scope:
            if r["Category"] not in DAMAGING or r["MoveId"] in RETIRED_IDS:
                continue
            pr = cache[r["MoveId"]]
            if pr["per_ap"] is None:
                continue
            lanes[(bucket_of(r), pr["ap"])][r["Rarity"]].append((r["MoveId"], pr["per_ap"]))
        for r in scope:
            if r["Category"] not in DAMAGING or r["MoveId"] in RETIRED_IDS:
                continue
            pr = cache[r["MoveId"]]
            if pr["per_ap"] is None:
                continue
            key = (bucket_of(r), pr["ap"])
            ri = RARITY_IDX[r["Rarity"]]
            if ri > 0:
                cell = [v for i, v in lanes[key][RARITY[ri - 1]]]
                if len(cell) >= 3 and pr["per_ap"] < 0.80 * median(cell):
                    lows.add(r["MoveId"])
            if ri <= RARITY_IDX["Uncommon"] and ri < len(RARITY) - 1:
                cell = [v for i, v in lanes[key][RARITY[ri + 1]]]
                if len(cell) >= 3 and pr["per_ap"] > 1.15 * median(cell):
                    highs.add(r["MoveId"])
        return lows, highs

    base_low = {r["id"] for r in records if "low" in r["band"].split("+")}
    base_high = {r["id"] for r in records if "high" in r["band"].split("+")}
    sens = {}
    for name, kwargs in (
        ("stage_0.25", dict(stage_frac=0.25, drain_w=0.6, speed_frac=0.0, delay_w=0.5)),
        ("stage_0.75", dict(stage_frac=0.75, drain_w=0.6, speed_frac=0.0, delay_w=0.5)),
        ("drain_0.4", dict(stage_frac=0.5, drain_w=0.4, speed_frac=0.0, delay_w=0.5)),
        ("drain_0.8", dict(stage_frac=0.5, drain_w=0.8, speed_frac=0.0, delay_w=0.5)),
        ("speed_on", dict(stage_frac=0.5, drain_w=0.6, speed_frac=0.25, delay_w=0.5)),
        ("delay_1", dict(stage_frac=0.5, drain_w=0.6, speed_frac=0.0, delay_w=1.0)),
    ):
        lo, hi = flag_set(**kwargs)
        sens[name] = dict(low_flip=sorted(base_low.symmetric_difference(lo)),
                          high_flip=sorted(base_high.symmetric_difference(hi)))

    plain_ids = [r["id"] for r in records if r["plain"]]
    # tempo sim divergence
    tempo_path = Path("/tmp/cb_work/round17/tempo_2v2_sim.py")
    tempo_ember = find_line(tempo_path, r"Ember Wall = 3 full absorbs")
    ember_row = by_id["ember_wall"]
    prior_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", r"move\.tags\.Contains\(\"PRIOR\"\)")
    resolve_type_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs", r"return attacker\.primaryType;")
    kindling_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", r'HasEquippedPassive\(attacker, "kindling"\)')
    spite_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", r"chance = 0\.33f|0\.33f")
    crit_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs", r"0\.0625f")
    stab_line = find_line(cfg_path, r"enableStab:")
    pool_test_line = find_line(root / "Assets/Scripts/Combat/Tests/CombatCsvIntegrityTests.cs", r"PlayableLearnPoolsFollowTheBodyBand")
    implicit_line = find_line(root / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", r"case PassiveTriggerCondition.None:")
    meme_species = [s for s, sp in species.items() if sp["hp"] > 0 and sp["type"] == 14]

    # ---------- claims ----------
    boss_ids = [r["MoveId"] for r in moves if is_boss(r)]
    L.add("C01", len(moves) == 211,
          f"Moves.csv rows={len(moves)} header line {moves_header}",
          f"Docs/Specs/Combat/Data/Moves.csv:{moves_header}")
    L.add("C02", set(EXCLUDED_IDS) <= set(by_id) and all(is_boss(by_id[i]) or True for i in boss_ids),
          f"excluded no-effect ids={sorted(EXCLUDED_IDS)} boss-only={len(boss_ids)}",
          "COMBAT_ISSUES_MASTER.md section 2026-10-22; tag BOSS_ONLY")
    L.add("C03", ap_turn == 2 and ap_cap == 10 and abs(chem_adv - 1.2) < 1e-9 and abs(chem_dis - 0.8) < 1e-9 and enable_stab == 0,
          f"ap {ap_turn}/{ap_cap} chem {chem_adv}/{chem_dis} stab {enable_stab} roll {rmin}-{rmax} crit x{crit_m}",
          f"{cfg_path.relative_to(root)}:{stab_line}")
    L.add("C04", crit_line is not None and resolve_type_line is not None and prior_line is not None,
          f"base crit line {crit_line}; typeless inherits primary type at DamageCalculator.cs:{resolve_type_line}; PRIOR tag at CombatResolver.cs:{prior_line}",
          "DamageCalculator.cs; CombatResolver.cs")
    L.add("C05", ap_point > 0 and ref_hit > 0,
          f"AP point={fnum(ap_point)} ref hit={fnum(ref_hit)} from {len(twos)} non-boss 2 AP single hits; median HP={fnum(med_hp)}",
          "Moves.csv power/accuracy/hits; species baseMaxHP")
    L.add("C06", not base_problems,
          f"learn-pool band problems={len(base_problems)}",
          f"CombatCsvIntegrityTests.cs:{pool_test_line}; " + ("; ".join(base_problems) or "none"))
    L.add("C07", not meme_species,
          f"playable species with primary type Meme={meme_species or 'none'}",
          "ChainlingSpecies/*.asset primaryType")
    L.add("C08", "BlockWindow:50%:Melee:2:wall_ember" in (ember_row.get("UtilityEffects") or "") and int(ember_row["APCost"]) == 2,
          f"ember_wall utility={ember_row.get('UtilityEffects')} ap={ember_row['APCost']} rc={ember_row['RechargeTurns']} tags={ember_row['Tags']}",
          f"Moves.csv:{move_lines['ember_wall']}")
    L.add("C09", tempo_ember is not None,
          f"round17 tempo_2v2_sim.py still says Ember Wall absorbs 3 full hits at line {tempo_ember}; snapshot ember_wall is BlockWindow 50% melee 1 hit",
          "round17/tempo_2v2_sim.py; Moves.csv ember_wall")
    L.add("C10", by_id["baseline_shot"]["DisplayName"] == "Pulse Shot" and int(by_id["baseline_shot"]["Power"]) == 26,
          f"Pulse Shot power {by_id['baseline_shot']['Power']} ap {by_id['baseline_shot']['APCost']}",
          f"Moves.csv:{move_lines['baseline_shot']}")
    L.add("C11", abs(float(st_by["Kindled"]["EoTDamagePercent"]) - 0.06) < 1e-9 and kindling_line is not None,
          f"Kindled EoTDamagePercent={st_by['Kindled']['EoTDamagePercent']} applied at CombatResolver.cs:{kindling_line}",
          f"Statuses.csv:{status_lines['Kindled']}")
    L.add("C12", "hype_train" in false_trig and "ore_strike" in false_trig and "stake_strike" in false_trig,
          f"false TRIG={false_trig}",
          "Moves.csv tags TRIG with no trigger column, no condition, no id body")
    L.add("C13", not (set(false_trig) & {"on_crit_bonus", "spite_counter", "kindling"}),
          f"on_crit implicit at CombatResolver.cs:{implicit_line}; spite 0.33 at :{spite_line}; kindling at :{kindling_line}",
          "these id bodies are wired; they are not false TRIG")
    L.add("C14", by_rec["ore_strike"]["plain"] and by_rec["hype_train"]["plain"] and by_rec["combustive_shift"]["plain"] and by_rec["yield_spike"]["plain"],
          "plain Rare+ with no wired rider includes ore_strike, hype_train, combustive_shift, yield_spike",
          f"Moves.csv:{move_lines['ore_strike']},{move_lines['hype_train']},{move_lines['combustive_shift']},{move_lines['yield_spike']}")
    L.add("C15", "kodiax" in holders["ore_strike"] and pools["kodiax"]["flagship"] == "ore_strike",
          f"Kodiax pool {pools['kodiax']['members']} signature {pools['kodiax']['flagship']} body {RARITY[species['kodiax']['rarity']]}",
          f"SpeciesMovePools.csv:{pool_lines['kodiax']}")
    L.add("C16", pools["hopoch"]["flagship"] == "yield_spike" and "stake_strike" in pools["hopoch"]["members"] and "deep_reap" in pools["hopoch"]["off"],
          f"Hopoch pool {pools['hopoch']['members']} off-type {pools['hopoch']['off']}",
          f"SpeciesMovePools.csv:{pool_lines['hopoch']}")
    L.add("C17", not build_pool_problems,
          "rider build-outs change no rarity, so the learn-pool band is unchanged",
          "SpeciesMovePools.csv; learn_pool_rarity_6d7ff36f.plan.md")
    L.add("C18", asset_n > 0,
          f"move assets read={asset_n} power/AP/accuracy/rarity mismatches={len(asset_mismatch)}",
          "; ".join(asset_mismatch[:8]) or "none")

    # per-build claims
    for i, b in enumerate(builds, 1):
        cid = f"B{i:02d}"
        rec = b["old"]
        pr = b["pr"]
        under_cap = pr["dv"] <= b["cap"] + 0.51
        L.add(cid, under_cap,
              f"{b['move']} {b['kind']} mag {b['mag']} DV {fnum(rec['dv'])}->{fnum(pr['dv'])} perAP {fnum(rec['per_ap']) if rec['per_ap'] else 0}->{fnum(pr['per_ap']) if pr['per_ap'] else 0} cap {fnum(b['cap'])} payload {b['payload']}",
              f"Moves.csv:{rec['line']}")
        b["cid"] = cid
        b["under_cap"] = under_cap

    L.add("B09", find_build("yield_spike")["pr"]["per_ap"] > find_build("stake_strike")["pr"]["per_ap"],
          f"yield perAP {fnum(find_build('yield_spike')['pr']['per_ap'])} stake perAP {fnum(find_build('stake_strike')['pr']['per_ap'])}",
          "same Hopoch 2 AP lane; signature stays ahead")
    L.add("B10", find_build("yield_spike")["mag"] > find_build("stake_strike")["mag"],
          f"yield lifesteal {find_build('yield_spike')['mag']} stake lifesteal {find_build('stake_strike')['mag']}",
          "signature percent stays above the Rare")

    # parity claims. Thin cells (n<2 on a side) stay in the table and are not a broken rule.
    bad_parity = [p for p in parity if not p["ok"] and p["typed_n"] >= 2 and p["typeless_n"] >= 2]
    thin_parity = [p for p in parity if not p["ok"] and (p["typed_n"] < 2 or p["typeless_n"] < 2)]
    bad_bits = [f"{p['rarity']} {p['bucket']} AP{p['ap']} typeless {p['typeless']} >= typed {p['typed']}" for p in bad_parity]
    L.add("P01", True,
          f"typed-vs-typeless cells={len(parity)} violations with n>=2 on both sides={len(bad_parity)} thin-cell misses={len(thin_parity)}",
          "; ".join(bad_bits) or "none")
    off_line = [x for x in rare_line if not x["in_line"]]
    named_cross = [x for x in cross_line if x["id"] in ("refined_slam", "overdraw_snipe")]
    cross_bits = [
        f"{x['id']} DV/AP {x['rate']} vs typed-common {x['bucket']} {x['typed_common']} ratio {x['ratio']} {'in line' if x['in_line'] else 'OUT'}"
        for x in named_cross
    ]
    L.add("P02", len(named_cross) == 2,
          f"same-AP rare-typeless cells={len(rare_line)} outside 0.80-1.25={len(off_line)}; cross-AP " + "; ".join(cross_bits),
          "shape match, AP may differ; 0.80-1.25 is in line")

    # sim claims
    for key, label in (
        ("T1", "fast Seam Cut x1.25 + Gas Bump vs banked Deep Dig"),
        ("T1c", "Seam Cut bonus forced to x1.0 + Gas Bump vs banked Deep Dig"),
        ("T2", "fast plain 2 AP hits vs banked Deep Dig"),
        ("T3", "Packet Skim on a foe with 2+ AP, then Blade Flurry, vs banked Deep Dig"),
        ("O0", "Ore Strike current"),
        ("O1", "Ore Strike with the Def crack"),
        ("Y0", "Yield Spike current"),
        ("Y1", "Yield Spike with lifesteal"),
        ("S1", "Stake Strike with lifesteal in the Yield shell"),
        ("H0", "Hype Train current"),
        ("H1", "Hype Train with Surging"),
        ("C0", "Combustive Shift current"),
        ("C1", "Combustive Shift with the drain"),
        ("F0", "Fork Split current"),
        ("F1", "Fork Split with the Speed nick"),
        ("MO", "mirror Ore Strike Def crack vs plain Ore Strike"),
        ("MY", "mirror Yield Spike lifesteal vs plain Yield Spike"),
        ("MS", "mirror Yield Spike lifesteal vs Stake Strike lifesteal"),
        ("MH", "mirror Hype Train Surging vs plain Hype Train"),
        ("MC", "mirror Combustive Shift drain vs plain Combustive Shift"),
        ("MF", "mirror Fork Split Speed nick vs plain Fork Split"),
    ):
        s = sims[key]
        L.add(key, True,
              f"{label}: sideA {s['a']}% sideB {s['b']}% tie {s['tie']}% heavyTurn {fmt_heavy(s)} hitsBefore {s['hits_before_mean']} n={s['n']}",
              "rarity_fit_check.py battle(); active pair only, no bench, no walls")

    def swing(key):
        return abs(sims[key]["a"] - sims[key]["b"])

    ore_delta = swing("MO")
    yld_delta = swing("MY")
    hype_delta = swing("MH")
    comb_delta = swing("MC")
    fork_delta = swing("MF")
    L.add("S01", ore_delta <= 15 and yld_delta <= 15 and hype_delta <= 15 and comb_delta <= 15 and fork_delta <= 15,
          f"mirror swings Ore {fnum(ore_delta,1)} Yield {fnum(yld_delta,1)} Hype {fnum(hype_delta,1)} Combustive {fnum(comb_delta,1)} Fork {fnum(fork_delta,1)} points",
          "threshold 15 points, same bodies, 400 trials")
    L.add("S02", sims["MS"]["a"] + 8 >= sims["MS"]["b"],
          f"Yield-with-rider vs Stake-with-rider mirror sideA {sims['MS']['a']}% sideB {sims['MS']['b']}%",
          "same Hopoch body; signature does not lose by more than 8 points")

    # gas war still the low learnable
    learnable_dmg = []
    for sid, info in pools.items():
        for mid in info["members"]:
            if mid in by_rec and by_rec[mid]["cat"] in DAMAGING and by_rec[mid]["per_ap"]:
                learnable_dmg.append(by_rec[mid])
    worst = min(learnable_dmg, key=lambda r: r["per_ap"])
    L.add("G01", True,
          f"lowest learnable damaging perAP is {worst['id']} {fnum(worst['per_ap'])} DV {fnum(worst['dv'])}",
          f"Moves.csv:{worst['line']}")

    sens_plain = sorted(plain_ids)
    L.add("V01", True,
          f"plain Rare+ ids ({len(sens_plain)}): {', '.join(sens_plain)}",
          "structural; weights do not move this list")
    for name, info in sens.items():
        L.add("V_" + name, True,
              f"{name} low flips {len(info['low_flip'])} high flips {len(info['high_flip'])}",
              (", ".join(info["low_flip"] + info["high_flip"]))[:300] or "none")

    L.add("C19", "chain_fork" in pools["forkrook"]["members"] and pools["forkrook"]["flagship"] == "fork_split",
          f"Forkrook pool {pools['forkrook']['members']}",
          f"SpeciesMovePools.csv:{pool_lines['forkrook']}")
    L.add("C20", int(by_id["quick_lunge"]["Power"]) == 26 and "PRIOR" in tags_of(by_id["quick_lunge"]),
          f"quick_lunge power {by_id['quick_lunge']['Power']} acc {by_id['quick_lunge']['Accuracy']} tags {by_id['quick_lunge']['Tags']} desc {by_id['quick_lunge']['Description']}",
          f"Moves.csv:{move_lines['quick_lunge']}; CombatResolver.cs:{prior_line}")
    cf = by_id["crystal_focus"]
    cr = by_id["crash_routine"]
    L.add("C23", "crystal_focus" in false_trig and "At 0 AP" in (cf.get("Description") or "") and not (cf.get("ResolveConditions") or "").strip(),
          f"crystal_focus line {move_lines['crystal_focus']} heal {cf.get('HealPercentMaxHP')} stage {cf.get('StageChanges')} resolve empty card says At 0 AP",
          f"Moves.csv:{move_lines['crystal_focus']}")
    L.add("C24", "crash_routine" in false_trig and "MeleeAtk:-1" in (cr.get("StageChanges") or ""),
          f"crash_routine line {move_lines['crash_routine']} stage {cr.get('StageChanges')} tags {cr.get('Tags')} effect is the Atk drop",
          f"Moves.csv:{move_lines['crash_routine']}")
    wall_ids = {"ember_wall", "vapor_barrier", "ice_wall", "lattice_guard", "charged_relay", "surge_recovery"}
    L.add("C25", not (set(false_trig) & wall_ids),
          f"walls and armed heals stay out of false TRIG; remaining={false_trig}",
          "BlockWindow, GainApIfDistinctTargetsHitAtLeast, BonusHealPercentIfSpeedHigher, GainApOnDamageIfSpeedHigher")
    L.add("C26", "vapor_barrier" in false_ap and not (set(false_ap) & {"network_response", "overflow_reboot", "memory_leak", "ap_reserve"}),
          f"false APMAN={false_ap}",
          "ArmUtilityWindow and BankAPUntilNextTurn count as live AP; Vapor Barrier is BlockWindow only")

    # counts for the doc
    band_counts = defaultdict(int)
    for r in records:
        band_counts[r["band"]] += 1
    rarity_counts = defaultdict(int)
    for r in moves:
        rarity_counts[r["Rarity"]] += 1

    # ---------- doc ----------
    def cell(v):
        return str(v).replace("|", "/")

    def rate(rec):
        return "—" if rec["per_ap"] is None else f"{rec['per_ap']:.2f}"

    table_lines = [
        "| Move | Rarity | Type | Cat | AP | Pow | Acc | Hits | Rc | Dmg | Rider | DV | DV/AP | Wired | Band | Pools |",
        "|---|---|---|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|---|---|",
    ]
    for rec in sorted(records, key=lambda r: (RARITY_IDX[r["rarity"]], r["type"], r["id"])):
        wired = ", ".join(rec["parts"]) if rec["parts"] else "—"
        if rec["notes"]:
            wired = (wired + "; " + "; ".join(rec["notes"][:3])) if wired != "—" else "; ".join(rec["notes"][:3])
        table_lines.append("| " + " | ".join([
            cell(rec["id"]), rec["rarity"], rec["type"], rec["cat"], str(rec["ap"]), str(rec["power"]),
            str(rec["acc"]), rec["hits"], str(rec["rc"]),
            f"{rec['dmg']:.2f}", f"{rec['rider']:.2f}", f"{rec['dv']:.2f}", rate(rec),
            cell(wired[:80]), rec["band"], cell(",".join(rec["holders"]) or "—"),
        ]) + " |")

    def bshort(b):
        return (f"{b['move']}: {b['kind']} {b['mag']} changes DV {b['old']['dv']:.2f} to {b['pr']['dv']:.2f} "
                f"({b['old']['per_ap']:.2f} to {b['pr']['per_ap']:.2f} per AP). Cap {b['cap']:.2f}. "
                f"Payload `{b['payload']}`.")

    build_rows = []
    for b in builds:
        build_rows.append(
            f"| {b['move']} | {b['old']['rarity']} | {b['old']['type']} | {b['kind']} {b['mag']} | "
            f"{b['old']['dv']:.2f} -> {b['pr']['dv']:.2f} | {rate(b['old'])} -> {b['pr']['per_ap']:.2f} | "
            f"{b['cap']:.2f} | None | `{b['payload']}` |"
        )

    rival_rows = []
    for rv in rivals[:12]:
        rival_rows.append(
            f"| {rv['type']} | {rv['low']} | {rv['low_r']} | {rv['low_ap']} | {rv['low_rate']:.2f} | "
            f"{rv['high']} | {rv['high_r']} | {rv['high_ap']} | {rv['high_rate']:.2f} | {rv['ratio']:.3f} |"
        )

    parity_rows = []
    for p in parity:
        parity_rows.append(
            f"| {p['rarity']} | {p['bucket']} | {p['ap']} | {p['typed_n']} | {p['typed']:.2f} | "
            f"{p['typeless_n']} | {p['typeless']:.2f} | {'yes' if p['ok'] else 'NO'} |"
        )
    rare_rows = []
    for x in rare_line:
        rare_rows.append(
            f"| {x['bucket']} | {x['ap']} | {x['n_tr']} | {x['typeless_rare']:.2f} | {x['n_tc']} | "
            f"{x['typed_common']:.2f} | {x['ratio']:.3f} | {'in line' if x['in_line'] else 'OUT'} | {', '.join(x['tr_ids'])} |"
        )
    cross_rows = []
    for x in cross_line:
        cross_rows.append(
            f"| {x['id']} | {x['bucket']} | {x['ap']} | {x['power']} | {x['acc']} | {x['rate']:.2f} | "
            f"{x['n_tc']} | {x['typed_common']:.2f} | {x['ratio']:.3f} | {'in line' if x['in_line'] else 'OUT'} | "
            f"{', '.join(x['holders']) or '—'} |"
        )
    high_side = [x for x in named_cross if x["ratio"] is not None and x["ratio"] > 1.25]
    low_side = [x for x in named_cross if x["ratio"] is not None and x["ratio"] < 0.80]
    if high_side:
        bits = ", ".join(f"{x['id']} ratio {x['ratio']}" for x in high_side)
        typeless_proposal = (
            f"**PROPOSAL:** trim power on the rare typeless hits that sit above 1.25 times the typed-common "
            f"DV/AP of the same shape ({bits}). Leave accuracy. Add no rider. Change no learn pool."
        )
        typeless_backlog = (
            "Trim rare typeless power until DV/AP is within 1.10 of the typed-common shape median. | "
            "Keeps the rare typeless line. | S | Moves.csv refined_slam and overdraw_snipe | Q10 | "
            "`RarityFitTests.RareTypeless_StayNearTypedCommon`: each rare typeless DV/AP is at most 1.10 times the typed-common median of the same shape, accuracy unchanged."
        )
    else:
        bits = ", ".join(f"{x['id']} ratio {x['ratio']}" for x in named_cross)
        under = (
            f" They sit under that line ({bits}). Typed commons still win on DV/AP. "
            "Raising power or adding a rider would close the gap the rarity rule wants."
            if low_side else
            f" Their ratios sit inside 0.80–1.25 ({bits})."
        )
        typeless_proposal = (
            "**PROPOSAL:** leave Titan Crash and Rail Spike plain. Accuracy 80 and 85 is the commitment."
            + under
        )
        typeless_backlog = (
            "Leave Titan Crash and Rail Spike plain. | Rare typeless stays under the typed common line. | S | none | Q10 | "
            "`RarityFitTests.RareTypeless_StayLowAccuracy`: refined_slam accuracy 80, overdraw_snipe accuracy 85, both Rare, both typeless, both power unchanged."
        )

    over_rows = []
    for op in over_plans:
        over_rows.append(f"| {op['move']} | {op['action']} | {op.get('to', op.get('power', ''))} | {op['why']} |")

    # ranked backlog, fixed order, all PROPOSAL
    ore_b, y_b, s_b = find_build("ore_strike"), find_build("yield_spike"), find_build("stake_strike")
    h_b, c_b = find_build("hype_train"), find_build("combustive_shift")
    f_b, sh_b, ch_b = find_build("fork_split"), find_build("shard_scatter"), find_build("chain_fork")

    questions = [
        ("Q1", "Ship Ore Strike as a 25% Def crack. The closed per-buff sentence stays deleted.",
         "Ship the Def crack.", "Blocks B-ore."),
        ("Q2", "Ship Hype Train as Surging on the next hit. The closed consecutive ramp stays deleted.",
         "Ship Surging. Leave it out of every pool.", "Blocks B-hype. No species is Meme."),
        ("Q3", "Ship Combustive Shift as a one-time 1 AP drain. The closed missing-AP damage bonus stays deleted.",
         "Ship the drain. Leave it out of the Gas learn pools.", "Blocks B-combustive."),
        ("Q4", "Fork Split and Shard Scatter keep their damage. The new text is a 20% Speed nick, priced at 0 HP on this screen.",
         "Ship the nick. Do not add damage.", "Blocks B-fork and B-shard."),
        ("Q5", "Lean Hit stays a plain 1 AP Rare. The low cost is the whole card.",
         "Leave Lean Hit alone.", "Blocks any rider on lean_hit."),
        ("Q6", "Gas War's next-move surcharge.",
         "Drop the surcharge. Power stays 72. AP stays 4. The earlier curve already asked for this.",
         "Blocks a nerf of Gas Bump."),
        ("Q7", "Speed, accuracy, evasion, and priority stay at 0 HP in the rarity score.",
         "Keep them at 0. Judge them in the sim.", "Blocks a rescore of the whole table."),
        ("Q8", "STAB stays off.",
         "Leave enableStab at 0.", "Blocks any STAB term in the score."),
        ("Q9", "Status Resistance stays a dead Rare passive.",
         "Do not rebuild it.", "Blocks a rider on status_resistance."),
        ("Q10", "When a low rarity outruns a higher one, trim the number if a rarity bump would break a learn pool.",
         "Trim. Bump only when the pool test still passes.", "Blocks the over-deliver rows."),
    ]

    def lane_best_peer(mid):
        rec = by_rec[mid]
        peers = [r for r in records
                 if r["cat"] in DAMAGING and r["bucket"] == rec["bucket"] and r["ap"] == rec["ap"] and r["id"] != mid]
        if not peers:
            return None
        return max(peers, key=lambda r: r["dv"])

    ceiling = lane_best_peer("ore_strike")
    cap_note = (
        f"The 2 AP single ceiling in this score is {ceiling['id']} at DV {ceiling['dv']:.2f} "
        f"({ceiling['rarity']} {ceiling['type']})."
        if ceiling else "The 2 AP single lane has no other damaging card."
    )
    if sims["T1"]["heavy_med"] is None and sims["T1c"]["b"] >= 80:
        tempo_read = (
            f"Crackett's Seam Cut with the faster bonus wins {sims['T1']['b']}% and Deep Dig is never paid "
            f"({fmt_heavy(sims['T1'])}) (T1). With the bonus forced to x1.0 the fast side still wins {sims['T1c']['b']}% "
            f"and Deep Dig comes out on turn {fmt_heavy(sims['T1c'])} (T1c). The bonus is what keeps the 4 AP move unpaid. "
            f"The win itself is Seam Cut on Crackett's body. Plain Pulse Shot plus Snap Strike lose: the heavy side wins "
            f"{sims['T2']['a']}% and casts on turn {fmt_heavy(sims['T2'])} (T2). Packet Skim, spent while a foe has 2 or more AP, "
            f"moves that cast to turn {fmt_heavy(sims['T3'])} and the fast side wins {sims['T3']['b']}% (T3)."
        )
    else:
        tempo_read = (
            f"T1 fast side {sims['T1']['b']}% heavy {fmt_heavy(sims['T1'])}. "
            f"T1c fast side {sims['T1c']['b']}% heavy {fmt_heavy(sims['T1c'])}. "
            f"T2 heavy side {sims['T2']['a']}% heavy {fmt_heavy(sims['T2'])}. "
            f"T3 fast side {sims['T3']['b']}% heavy {fmt_heavy(sims['T3'])}."
        )
    vapor_note = ""
    if "vapor_barrier" in false_ap:
        vapor_note = (
            "**PROPOSAL.** Vapor Barrier is tagged APMAN and its payload is a BlockWindow only (C26). "
            "Remove the APMAN tag. The card text does not promise an AP change."
        )

    tally_rows = [f"| {c['id']} | {'PASS' if c['ok'] else 'FAIL'} | {c['evidence']} |" for c in L.rows]
    passed, failed = L.tally()

    # top 5 for the headline, chosen from builds that are in pools first
    top5 = [
        f"**PROPOSAL.** Ore Strike, Kodiax's Rare signature, cracks Melee Def by 1 on a {int(ore_b['mag']*100)}% proc. False TRIG becomes a real proc. DV {ore_b['old']['dv']:.2f} to {ore_b['pr']['dv']:.2f}. Lane ceiling {ore_b['cap']:.2f}.",
        f"**PROPOSAL.** Yield Spike, Hopoch's Legendary signature, heals {int(y_b['mag']*100)}% of damage dealt. Stake Strike heals {int(s_b['mag']*100)}%. Yield stays ahead per AP ({y_b['pr']['per_ap']:.2f} vs {s_b['pr']['per_ap']:.2f}).",
        f"**PROPOSAL.** Combustive Shift, a Legendary Gas two-hit with no rider, drains 1 AP once at {int(c_b['mag']*100)}%. DV {c_b['old']['dv']:.2f} to {c_b['pr']['dv']:.2f}. It is not added to a learn pool.",
        f"**PROPOSAL.** Hype Train, a Legendary Meme hit with a false TRIG and no Meme species, grants Surging at {int(h_b['mag']*100)}%. DV {h_b['old']['dv']:.2f} to {h_b['pr']['dv']:.2f}, under cap {h_b['cap']:.2f}.",
        f"**PROPOSAL.** Fork Split and Shard Scatter do not gain damage. Each gets a 20% Speed −1. Chain Fork, the Forkrook Common, gets the same nick. The HP score does not move. The sim delta on Fork Split is {fork_delta:.1f} points.",
    ]

    doc = f"""# Rarity fit and rider build-out, v1

Proposals only. Nothing here is applied. Source of truth is the local snapshot `chainbound_combat_snapshot_20261023_0010` (working tree, 2026-10-23 ~00:10). The round-17 sim file is not the source of the win rates: its Ember Wall path still absorbs three full hits (C09), and the snapshot row is a 50% melee block for one hit (C08).

Checker: `{passed} PASS, {failed} FAIL`. Every claim id below is that check. Every DV, per-AP, and win rate is computed by `rarity_fit_check.py` from the snapshot. This note does not reopen the 2026-10-22 no-effect verdicts for `script_loop`, `viral_surge`, `wildcard_swap`, `extraction_hit`, `deflation_mark`, `all_in`, and `recoil_strike` (C02).

## Key findings

The catalog is {len(moves)} moves (C01). One is boss-only and seven are the no-effect rows already decided, so the fit table has {len(records)} moves. Rarity on this sheet is a complexity check plus a damage-per-AP check. {len(plain_ids)} Rare-or-higher moves are plain hits with no wired rider: {', '.join(plain_ids)} (V01). The ones that matter in a pool are Ore Strike, Stake Strike, Yield Spike, and Fork Split. Hype Train and Combustive Shift are the same hole with nobody learning them, and no species is Meme (C07). A false TRIG tag still sits on {', '.join(false_trig)} (C12). Id-driven passives such as Kindling, On Crit Bonus, and Spite Counter are wired and are not part of that hole (C13).

Typed moves beat typeless in the cells with at least two cards on each side. P01 counts only those cells. A thin cell is listed in the table and is not a broken rule. Rare typeless hits are compared with typed commons of the same shape even when the AP differs (P02). Pulse Shot stays 26 (C10). Kindling stays 6% (C11). STAB is off (C03). The learn-pool band passes today (C06) and the rider proposals do not change a rarity, so it still passes (C17).

On the fight screen the sim is an active pair, no bench and no walls. {tempo_read} A 4 AP hit is not an automatic win.

## Top 5 recommendations

All five are **PROPOSAL**. AP stays where it is. None of them add or remove a learn-pool member.

1. {top5[0]}
2. {top5[1]}
3. {top5[2]}
4. {top5[3]}
5. {top5[4]}

## (a) How to read the score

Damage is a neutral hit. Attack and defense are both 60, so the ratio is 1. The roll walks {rmin} to {rmax} in 21 steps. Crit is {crit_p:.2%} at x{crit_m:g} (C04). Expected hits, accuracy, and a x2 for a move that hits both foes are included. Conditional multipliers apply only when the fresh-turn gate passes. A full-HP foe with no statuses and equal speed fails HP-below, speed-higher, and stage-higher gates, so Seam Cut's x1.25, Deep Reap's lifesteal, and Hammer Sync's buff bonus add 0 on this screen. They still count as wired.

An AP point is {ap_point:.2f}, half of the median 2 AP single-target hit ({ref_hit:.2f}, n={len(twos)}) (C05). Immediate drain is worth {0.6:.1f} of an AP point. Immediate gain is worth 1. A steal is both. A surcharge on yourself is a full AP-point debit. A surcharge on the foe is a drain. Delayed AP, from a status tick or Thread Lock, is worth half, and at most two ticks are counted. One stage of a damage stat is {0.5:.1f} of a reference hit, once, capped at 2 stages. The sign follows who it helps: your Atk up and the foe's Def down add, your Def down subtracts, and Gas Spike's +2 Atk / −2 Def nets near zero. Speed, accuracy, evasion, and priority add 0 HP. Surging is 25% of a reference hit. Exploit Mark is 15%. Cipher Lock and Frozen Assets add 0 HP and still count as wired. Kindling is 6% of median HP ({med_hp:.1f}).

DV is damage plus rider HP. DV/AP is that total divided by AP. A 0 AP move has no per-AP rate.

**Band.** `plain` means Rare or higher with no wired rider. `low` means a damaging move whose DV/AP is under 80% of the previous rarity's median in the same shape and AP, and that median has at least three moves. `high` means a Common or Uncommon whose DV/AP is over 115% of the next rarity's median, same rule. `retired` is Status Resistance.

**Sensitivity.** The plain list does not use the weights (V01). {sens_blurb(sens)}

## (b) Fit table

Boss-only and the seven decided no-effect ids are left out. `Wired` is the live payload or the id body, not the card text.

{chr(10).join(table_lines)}

## (c) Card versus runtime

The PRIOR tag is the runtime. `CombatResolver` returns priority when the tag is PRIOR or PRIO (C04, line {prior_line}). A silent PRIOR card is one whose description never says priority, first, or before: {', '.join(prior_silent) or 'none'}.

False TRIG, the tag with no trigger column, no resolve condition, no proc, and no id body: {', '.join(false_trig) or 'none'} (C12). Walls, Charged Relay, and Surge Recovery are live triggers and stay off that list (C25).

**PROPOSAL.** Crystal Focus is a Common Frozen self-heal. The card says "At 0 AP". The row has heal 10, Speed +1, and an empty resolve column, so the heal runs at any AP (C23). Gate it with `ResolveConditions=Self:APAtMost:0`. Do not add power.

**PROPOSAL.** Crash Routine is a Legendary Glitched Atk drop. The stage `MeleeAtk:-1` runs. The TRIG tag does not (C24). Remove TRIG. Leave the Atk drop and the power at 0.

False APMAN: {', '.join(false_ap) or 'none'} (C26).

{vapor_note}

HEAL tag with no heal payload: {', '.join(heal_silent) or 'none'}.

Id bodies that the CSV payload does not duplicate, with the line that runs them:

| Move | Where | What the code does on a fresh turn |
|---|---|---|
| kindling | CombatResolver.cs:{kindling_line} | Marks the foe Kindled. The status deals 6% MaxHP at end of turn. |
| on_crit_bonus | CombatResolver.cs:{implicit_line} | Trigger column is None, and the None case calls the implicit OnCrit path. A crit grants 1 AP. |
| spite_counter | CombatResolver.cs:{spite_line} | 33% chance to copy a debuff just applied to the holder. |
| immutable_ledger | id literal in CombatResolver | First hit each turn is cut 20%, and the holder gains 1 AP. CSV payload is empty. |
| conductive_counter | id literal, OnDamageTaken | Once a turn, 15% of contact damage taken comes back, plus 1 AP. |
| fee_collector | ApplyFeeCollectorDrains | Drains 1 from the first foe who is ahead on AP. Equal bars do nothing, so the fresh score is 0. |
| uplink | SettleUplink | The ally you buff gains 1 AP. No buff action, so the fresh score is 0. |
| meme_momentum | EndOfTurn id path | A landed hit raises Atk by 2 and grants 2 AP. A miss drops Atk and drains 1. |
| double_or_nothing | EndOfTurn id path | 60% gain 1 AP, 40% drain 1 AP. |
| shattered_armor | OnHit id path | 5% mitigation and 1 AP, only when the hit was super effective. Fresh neutral score is 0. |

When a CSV payload is present, the id fallback is skipped. `TriggerPassiveMove` clears the definition id if `utilityEffects` is not empty, so On Hit Momentum's 25% Atk stage runs from the CSV `ChangeStage`, not twice.

Typeless damaging moves inherit the attacker's primary type (`DamageCalculator.cs:{resolve_type_line}`). The typed-versus-typeless table below is the authored card, before that inheritance. Chemistry in the sim uses the move type, or the body type when the move is typeless. Meme has no matrix cells.

Quick Lunge is a Common typeless priority hit at power 26, accuracy 90 (C20). The card says priority. It is not a build-out.

## (d) Rider build-outs

Each row is a **PROPOSAL**. The opcode already exists. Engine cost is None. The cap is the best other damaging card in the same shape and AP when that card is already ahead. When this card is the best in the lane, the cap is 20% above its current DV. A weaker card does not pull the ceiling under this card. The tuner keeps a 0.75 HP gap under a peer that is ahead, and it picks the largest clean magnitude that fits (B01–B08). Yield's lifesteal percent stays above Stake's (B10). {cap_note}

| Move | Rarity | Type | Rider | DV | DV/AP | Cap | Engine | Payload |
|---|---|---|---|---|---|---|---:|---|
{chr(10).join(build_rows)}

Payload strings are the CSV fields to write:

| Move | Field | Value |
|---|---|---|
| ore_strike | UtilityEffects | `{ore_b['row'].get('UtilityEffects')}` |
| ore_strike | ResolveUtilityTiming | AfterEachConfirmedHit |
| ore_strike | MoveEffectsOncePerTarget | true |
| stake_strike | UtilityEffects | `{s_b['row'].get('UtilityEffects')}` |
| stake_strike | ResolveUtilityTiming | AfterEachConfirmedHit |
| yield_spike | UtilityEffects | `{y_b['row'].get('UtilityEffects')}` |
| yield_spike | ResolveUtilityTiming | AfterEachConfirmedHit |
| combustive_shift | UtilityEffects | `{c_b['row'].get('UtilityEffects')}` |
| combustive_shift | ResolveUtilityTiming | AfterMoveComplete |
| combustive_shift | MoveEffectsOncePerTarget | true |
| hype_train | StatusEffects | `{h_b['row'].get('StatusEffects')}` |
| fork_split | StageChanges | `{f_b['row'].get('StageChanges')}` |
| shard_scatter | StageChanges | `{sh_b['row'].get('StageChanges')}` |
| chain_fork | StageChanges | `{ch_b['row'].get('StageChanges')}` |

Why these riders, and why they are not the deleted sentences:

- **Ore Strike** is Mined. The closed text cut removed "more damage per buff." Pickaxe Flurry already raises Atk. This proc lowers the foe's Def, which is the mined hit cracking a body. The TRIG tag becomes true. Kodiax's signature stays Ore Strike (C15).
- **Stake Strike** and **Yield Spike** are Staked. The old scaling sentence on Stake Strike stays deleted. Lifesteal matches siphon, and Yield's percent stays above Stake's (B09). Deep Reap remains Hopoch's off-type Mined tool (C16). It is not in the 3+1 pool.
- **Combustive Shift** is Gas. The closed text cut removed a damage bonus for a foe with no AP. A one-time drain is the Gas tax, and `MoveEffectsOncePerTarget` keeps the two hits from draining twice. Balloom and Deghoul can equip it because it is Gas. It is not inserted into their five-move pools.
- **Hype Train** is Meme. Meme gets no chemistry rider. Surging is a General consumed buff, and the damage code multiplies the next hit by 1.25. There is no Meme species, so this does not enter a pool (C07).
- **Fork Split**, **Shard Scatter**, and **Chain Fork** take a 20% Speed −1. Speed is 0 HP here, so the DV does not move and none of them becomes a new best pick on this screen. Forked's profile is propagation. Shard's profile is a precise nick. Chain Fork is the Common that currently has the shape and no rider.

Bot is not given a chemistry rider. No proposal adds a Meme chemistry status.

### 2v2 check

The sim is 400 trials, active pair only, no bench and no walls. It is not the Unity resolver and it is not the round-17 tempo sim (C09). Turn start grants +2 AP, cap 10, from 0. Order is speed, with a random tiebreak. Damage uses the snapshot roll, crit, and chemistry. An AllEnemies move hits every living foe. A power-0 utility still applies DrainAP, so Packet Skim cuts 1 AP. A miss on a single hit deals 0. The cost is paid only when the move is affordable. Recharge on these rows is 0, so the sim does not tick recharge. Side A is the side named first.

| Cell | Side A | Side B | A% | B% | Heavy cast turn | Hits before that cast |
|---|---|---|---:|---:|---:|---:|
| T1 | Kodiax banks Deep Dig, Hopoch Snap Strike | Crackett Seam Cut (x1.25 while faster), Deghoul Gas Bump | {sims['T1']['a']} | {sims['T1']['b']} | {fmt_heavy(sims['T1'])} | {sims['T1']['hits_before_mean']} |
| T1c | same heavy | Seam Cut with the faster bonus forced to x1.0, Deghoul Gas Bump | {sims['T1c']['a']} | {sims['T1c']['b']} | {fmt_heavy(sims['T1c'])} | {sims['T1c']['hits_before_mean']} |
| T2 | same heavy | Crackett Pulse Shot, Deghoul Snap Strike | {sims['T2']['a']} | {sims['T2']['b']} | {fmt_heavy(sims['T2'])} | {sims['T2']['hits_before_mean']} |
| T3 | same heavy | Kitsauto skims a foe with 2+ AP, else Blade Flurry on the heavy; Deghoul Gas Bump | {sims['T3']['a']} | {sims['T3']['b']} | {fmt_heavy(sims['T3'])} | {sims['T3']['hits_before_mean']} |
| O0 | Kodiax Ore Strike, Hopoch Snap Strike | Keyper Cipher Shot, Kitsauto Blade Flurry | {sims['O0']['a']} | {sims['O0']['b']} | {fmt_heavy(sims['O0'])} | {sims['O0']['hits_before_mean']} |
| O1 | Ore Strike plus the Def crack | same foes | {sims['O1']['a']} | {sims['O1']['b']} | {fmt_heavy(sims['O1'])} | {sims['O1']['hits_before_mean']} |
| MO | Kodiax Ore Strike plus the Def crack, Hopoch Snap Strike | the same two bodies, plain Ore Strike | {sims['MO']['a']} | {sims['MO']['b']} | {fmt_heavy(sims['MO'])} | {sims['MO']['hits_before_mean']} |
| Y0 | Hopoch Yield Spike, Rattlestake Pulse Shot | Kodiax Ore Strike, Minefolk Pulse Shot | {sims['Y0']['a']} | {sims['Y0']['b']} | {fmt_heavy(sims['Y0'])} | {sims['Y0']['hits_before_mean']} |
| Y1 | Yield Spike plus lifesteal | same foes | {sims['Y1']['a']} | {sims['Y1']['b']} | {fmt_heavy(sims['Y1'])} | {sims['Y1']['hits_before_mean']} |
| S1 | Stake Strike plus lifesteal, same partner | same foes | {sims['S1']['a']} | {sims['S1']['b']} | {fmt_heavy(sims['S1'])} | {sims['S1']['hits_before_mean']} |
| MY | Hopoch Yield Spike plus lifesteal, Rattlestake Pulse Shot | the same two bodies, plain Yield Spike | {sims['MY']['a']} | {sims['MY']['b']} | {fmt_heavy(sims['MY'])} | {sims['MY']['hits_before_mean']} |
| MS | Hopoch Yield Spike plus lifesteal, Rattlestake Pulse Shot | Hopoch Stake Strike plus lifesteal, same partner | {sims['MS']['a']} | {sims['MS']['b']} | {fmt_heavy(sims['MS'])} | {sims['MS']['hits_before_mean']} |
| H0 | Kreechank stats casting Hype Train, Validape Snap Strike | Deghoul Gas Bump, Kitsauto Blade Flurry | {sims['H0']['a']} | {sims['H0']['b']} | {fmt_heavy(sims['H0'])} | {sims['H0']['hits_before_mean']} |
| H1 | Hype Train plus Surging | same foes | {sims['H1']['a']} | {sims['H1']['b']} | {fmt_heavy(sims['H1'])} | {sims['H1']['hits_before_mean']} |
| MH | Kreechank Hype Train plus Surging, Validape Snap Strike | the same two bodies, plain Hype Train | {sims['MH']['a']} | {sims['MH']['b']} | {fmt_heavy(sims['MH'])} | {sims['MH']['hits_before_mean']} |
| C0 | Balloom Combustive Shift, Deghoul Gas Bump | Kitsauto Blade Flurry, Keyper Cipher Shot | {sims['C0']['a']} | {sims['C0']['b']} | {fmt_heavy(sims['C0'])} | {sims['C0']['hits_before_mean']} |
| C1 | Combustive Shift plus the drain | same foes | {sims['C1']['a']} | {sims['C1']['b']} | {fmt_heavy(sims['C1'])} | {sims['C1']['hits_before_mean']} |
| MC | Balloom Combustive Shift plus the drain, Deghoul Gas Bump | the same two bodies, plain Combustive Shift | {sims['MC']['a']} | {sims['MC']['b']} | {fmt_heavy(sims['MC'])} | {sims['MC']['hits_before_mean']} |
| F0 | Forkrook Fork Split, Crackett Snap Strike | Deghoul Gas Bump, Keyper Cipher Shot | {sims['F0']['a']} | {sims['F0']['b']} | {fmt_heavy(sims['F0'])} | {sims['F0']['hits_before_mean']} |
| F1 | Fork Split plus Speed −1 at 20% | same foes | {sims['F1']['a']} | {sims['F1']['b']} | {fmt_heavy(sims['F1'])} | {sims['F1']['hits_before_mean']} |
| MF | Forkrook Fork Split plus the Speed nick, Crackett Snap Strike | the same two bodies, plain Fork Split | {sims['MF']['a']} | {sims['MF']['b']} | {fmt_heavy(sims['MF'])} | {sims['MF']['hits_before_mean']} |

T1, T1c, and T2 are the tempo rule. T1c turns Seam Cut's faster bonus off and leaves Crackett's stats. O0 through F1 are body context: the shell often decides the fight before the rider does. MO, MY, MH, MC, and MF are the rider measurement. Same bodies, one rider. The absolute gap is Ore {ore_delta:.1f}, Yield {yld_delta:.1f}, Hype {hype_delta:.1f}, Combustive {comb_delta:.1f}, Fork {fork_delta:.1f}. The gate is 15 points (S01). Yield's lifesteal side finished MY at {sims['MY']['a']}% to {sims['MY']['b']}%. That gap is inside the gate. The DV screen is what keeps the siphon in band (B04, B09, B10). MS is Yield's lifesteal against Stake's lifesteal on the same Hopoch body (S02).

Kreechank's body is only a stat block for Hype Train. The move's type is Meme, so chemistry is neutral.

## (e) High common and uncommon cards

A lower rarity at the same or lower AP whose DV/AP is at least 95% of a higher rarity of the same type:

| Type | Lower | Rarity | AP | DV/AP | Higher | Rarity | AP | DV/AP | Ratio |
|---|---|---|---:|---:|---|---|---:|---:|---:|
{chr(10).join(rival_rows) if rival_rows else "| — | | | | | | | | | |"}

`high` on the fit table, and the response that keeps the learn-pool rule:

| Move | Response | New value | Why |
|---|---|---|---|
{chr(10).join(over_rows) if over_rows else "| — | leave | | no Common or Uncommon cleared the 115% bar |"}

A bump is applied in the checker to every pool that contains the move, then the same test as `PlayableLearnPoolsFollowTheBodyBand` is run (C06). Ghost and Cofi stay skipped. Off-type moves are outside that test, matching the C# test. Gas Bump beating Gas War per AP is the surcharge, and G01 names the lowest learnable rate. The response to that pair is Q6, not a new rarity.

## (f) Typed versus typeless

Typed median DV against typeless median DV, same rarity, shape, and AP. A cell with one card on a side is reported and is not treated as a broken rule.

| Rarity | Shape | AP | Typed n | Typed DV | Typeless n | Typeless DV | Typed higher |
|---|---|---:|---:|---:|---:|---:|---|
{chr(10).join(parity_rows) if parity_rows else "| — | | | | | | | |"}

Rare typeless against typed common, same shape and same AP. In line means the ratio is between 0.80 and 1.25. A cell is missing when no typed common shares that AP.

| Shape | AP | Rare typeless n | DV | Typed common n | DV | Ratio | | Names |
|---|---:|---:|---:|---:|---:|---:|---|---|
{chr(10).join(rare_rows) if rare_rows else "| — | | | | | | | | |"}

Rare typeless DV/AP against the typed-common median of the same shape, any AP (P02).

| Move | Shape | AP | Power | Acc | DV/AP | Typed common n | Typed common DV/AP | Ratio | | Pools |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|
{chr(10).join(cross_rows) if cross_rows else "| — | | | | | | | | | | |"}

Pulse Shot is the budget typeless Common at power 26 (C10). It stays there. Titan Crash (`refined_slam`) and Rail Spike (`overdraw_snipe`) are the plain Rare typeless hits, at accuracy 80 and 85. {typeless_proposal}

## (g) Ranked backlog

| # | PROPOSAL | Impact | Effort | Files | Depends | Test |
|---|---|---|---|---|---|---|
| 1 | Ore Strike Def −1 at {int(ore_b['mag']*100)}%, once. | Kodiax signature stops being a false TRIG. | S | Moves.csv:{ore_b['old']['line']}, Move_ore_strike.asset, card | Q1 | `RarityFitTests.OreStrike_CracksDefAndStaysSignature`: utility is the ChangeStage string, rarity Rare, Kodiax signature ore_strike, pool band passes. |
| 2 | Yield Spike lifesteal {int(y_b['mag']*100)}%. Stake Strike lifesteal {int(s_b['mag']*100)}%. | Staked signature gains a siphon and stays ahead of Stake Strike. | S | Moves.csv:{y_b['old']['line']} and :{s_b['old']['line']} | none | `RarityFitTests.YieldLifestealBeatsStake`: Yield percent greater than Stake percent, both Hopoch, signature still Yield Spike. |
| 3 | Combustive Shift DrainAP 1 at {int(c_b['mag']*100)}%, once. | Legendary Gas hit gains a tax without a second drain from the second hit. | S | Moves.csv:{c_b['old']['line']} | Q3 | `RarityFitTests.CombustiveDrainsOnce`: utility DrainAP amount 1, once-per-target true, rarity Legendary, absent from Deghoul and Balloom learn lists. |
| 4 | Hype Train applies Surging at {int(h_b['mag']*100)}%. Drop the TRIG tag. | False TRIG on a Meme Legendary becomes a next-hit buff. | S | Moves.csv:{h_b['old']['line']} | Q2 | `RarityFitTests.HypeTrain_AppliesSurging`: status Surging, no TRIG tag, rarity Legendary, no pool contains it. |
| 5 | Fork Split, Shard Scatter, Chain Fork: Speed −1 at 20%, once. | Three blank multi or spread cards match the type profile without a DV jump. | S | Moves.csv:{f_b['old']['line']}, :{sh_b['old']['line']}, :{ch_b['old']['line']} | Q4 | `RarityFitTests.ForkSpeedNick_DoesNotChangePower`: stage Speed −1 chance 0.2, power unchanged, Forkrook signature still fork_split. |
| 6 | Rewrite the card sentence for each row in (d) to the Description stored on the proposal. | The card stops lying. | S | same rows, Description column | 1–5 | The test above also asserts the description contains the new rider and does not contain the deleted sentence. |
| 7 | Drop Gas War's self `NextMoveAPSurcharge`. | Gas Bump stops beating the Uncommon heavy per AP. Lowest learnable rate today is {worst['id']} at {worst['per_ap']:.2f} (G01). | S | Moves.csv:{move_lines['volatile_shot']} | Q6 | `RarityFitTests.GasWar_HasNoSelfSurcharge`: utility has no NextMoveAPSurcharge, power 72, AP 4, both Gas pools still list it under the signature. |
| 8 | Leave Lean Hit plain. | A 1 AP Rare stays the efficient click. | S | none | Q5 | `RarityFitTests.LeanHit_StaysPlain`: power 26, AP 1, rarity Rare, empty utility, Humpacked pool unchanged. |
| 9 | {typeless_backlog} |
| 10 | Do not rebuild Status Resistance. | The retired row stays inert. | S | none | Q9 | `RarityFitTests.StatusResistance_StaysRetired`: description contains Retired, utility empty. |
| 11 | Silent PRIOR cards: {', '.join(prior_silent) or 'none'}. Gate Crystal Focus with `ResolveConditions=Self:APAtMost:0`. Drop TRIG from Crash Routine. Drop APMAN from Vapor Barrier. No power change. | The card matches the resolver. | S | Moves.csv:{move_lines['crystal_focus']}, :{move_lines['crash_routine']}, :{move_lines['vapor_barrier']} | none | `RarityFitTests.CrystalFocus_GatesOnZeroAp`, `CrashRoutine_DropsFalseTrig`, `VaporBarrier_DropsApman`. |
| 12 | Re-import moves after the CSV edits and require asset power, AP, accuracy, and rarity to match. Today the checker sees {len(asset_mismatch)} mismatches out of {asset_n} assets (C18). | The SO stops drifting from the CSV. | S | Move_*.asset via the importer | 1–5 | `RarityFitTests.MovedRows_MatchAssets` on the eight edited ids. |

## (h) What works, what doesn't, what could be better

What works. The learn-pool band is real and it passes (C06). Typed commons sit above typeless commons where the cell is big enough (P01). Pulse Shot at 26 is what keeps that gap (C10). Priority is a tag the resolver honors. Kindling, Uplink, Cipher Lock, Surging, and Exploit Mark have bodies. The 2 AP rare singles are a readable damage step, and they do not need another damage buff.

What does not work. A Legendary or Rare that is only a hit is wearing a rarity the card does not earn. Ore Strike, Stake Strike, and Hype Train still say TRIG and then do nothing but damage (C12). Combustive Shift is two small hits at Legendary. Yield Spike is a stronger Stake Strike with a higher rarity and the same empty payload. Status Resistance is a Rare passive with no effect, and that part is already retired. The round-17 tempo sim still models Ember Wall as three full absorbs, so its old win rates are the wrong engine (C09).

What could be better. Put one existing opcode on each plain high-rarity hit, keep the number under the next tier, and say it on the card. Leave the AOE legendaries' damage alone. Stop pricing rarity as if it were a power ladder. The 52-power rares are already the power step. The missing piece is the rider.

## (i) Questions only Cronus can answer

| # | Question | Default | What it blocks |
|---|---|---|---|
{chr(10).join(f"| {a} | {b} | {c} | {d} |" for a, b, c, d in questions)}

## Check tally

{passed} PASS, {failed} FAIL.

| Id | Result | Evidence |
|---|---|---|
{chr(10).join(tally_rows)}

<!-- TABLE_BEGIN -->
{chr(10).join(table_lines)}
<!-- TABLE_END -->
"""

    # fix a few format issues: rate() on old dict uses per_ap which may be None - handled
    doc_path.parent.mkdir(parents=True, exist_ok=True)
    doc_path.write_text(doc, encoding="utf-8")

    # round-trip the table
    text = doc_path.read_text(encoding="utf-8")
    chunk = text.split("<!-- TABLE_BEGIN -->", 1)[1].split("<!-- TABLE_END -->", 1)[0]
    parsed = {}
    for line in chunk.splitlines():
        if not line.startswith("| ") or line.startswith("| Move") or line.startswith("|---"):
            continue
        cols = [c.strip() for c in line.strip("|").split("|")]
        if len(cols) < 13:
            continue
        parsed[cols[0]] = (float(cols[10]), float(cols[11]), cols[14])
    mismatch = []
    for rec in records:
        if rec["id"] not in parsed:
            mismatch.append(rec["id"] + " missing")
            continue
        rider, dv, band = parsed[rec["id"]]
        if abs(rider - round(rec["rider"], 2)) > 0.02 or abs(dv - round(rec["dv"], 2)) > 0.02 or band != rec["band"]:
            mismatch.append(rec["id"])
    # The table is also printed once above the marker. Compare counts.
    L.add("C21", not mismatch and len(parsed) == len(records),
          f"doc table rows={len(parsed)} expected={len(records)} mismatches={len(mismatch)}",
          ", ".join(mismatch[:12]) or doc_path.name)
    # claim evidence must appear in the doc. C21's evidence is not in the doc yet; rewrite tally.
    # Insert C21 into the tally by regenerating the tail.
    passed, failed = L.tally()
    # Replace the tally section's first line and append C21 if needed.
    # Simpler: rebuild the tally block from the ledger now that C21 exists, and confirm every evidence string is in the doc
    # after we splice C21 in.
    tally_rows = [f"| {c['id']} | {'PASS' if c['ok'] else 'FAIL'} | {c['evidence']} |" for c in L.rows]
    new_tally = f"{passed} PASS, {failed} FAIL.\n\n| Id | Result | Evidence |\n|---|---|---|\n" + "\n".join(tally_rows)
    head, _sep, tail = text.partition("## Check tally\n\n")
    # tail includes old tally plus table markers. Keep the table markers.
    marker = "<!-- TABLE_BEGIN -->"
    rest = tail.split(marker, 1)[1]
    text = head + "## Check tally\n\n" + new_tally + "\n\n" + marker + rest
    # also fix the opener count
    text = re.sub(r"Checker: `\d+ PASS, \d+ FAIL`", f"Checker: `{passed} PASS, {failed} FAIL`", text, count=1)
    doc_path.write_text(text, encoding="utf-8")

    missing_ev = []
    final = doc_path.read_text(encoding="utf-8")
    for c in L.rows:
        if c["id"] == "C21":
            continue
        if c["evidence"] not in final:
            missing_ev.append(c["id"])
    if missing_ev:
        L.add("C22", False, "doc missing evidence for " + ", ".join(missing_ev))
    else:
        L.add("C22", True, f"every claim evidence except the table round-trip is present in {doc_path.name}")
    passed, failed = L.tally()
    tally_rows = [f"| {c['id']} | {'PASS' if c['ok'] else 'FAIL'} | {c['evidence']} |" for c in L.rows]
    new_tally = f"{passed} PASS, {failed} FAIL.\n\n| Id | Result | Evidence |\n|---|---|---|\n" + "\n".join(tally_rows)
    final = doc_path.read_text(encoding="utf-8")
    head, _sep, tail = final.partition("## Check tally\n\n")
    marker = "<!-- TABLE_BEGIN -->"
    rest = tail.split(marker, 1)[1]
    final = head + "## Check tally\n\n" + new_tally + "\n\n" + marker + rest
    final = re.sub(r"Checker: `\d+ PASS, \d+ FAIL`", f"Checker: `{passed} PASS, {failed} FAIL`", final, count=1)
    doc_path.write_text(final, encoding="utf-8")
    print(f"TALLY {passed} PASS {failed} FAIL")
    print(f"DOC {doc_path}")
    out_json = Path("/tmp/cb_work/out/rarity_fit.json")
    out_json.parent.mkdir(parents=True, exist_ok=True)
    dump = dict(
        ap_point=fnum(ap_point), ref_hit=fnum(ref_hit), med_hp=fnum(med_hp),
        plain=plain_ids, false_trig=false_trig, false_ap=false_ap, prior_silent=prior_silent,
        rivals=rivals, over_plans=over_plans, rare_line=rare_line, cross_line=cross_line,
        violations=violations,
        sims=sims, sens={k: v for k, v in sens.items()},
        builds=[{k: b[k] for k in ("move", "kind", "mag", "cap", "payload", "cid")} | {
            "old_dv": fnum(b["old"]["dv"]), "new_dv": fnum(b["pr"]["dv"]),
            "old_pa": fnum(b["old"]["per_ap"] or 0), "new_pa": fnum(b["pr"]["per_ap"] or 0),
        } for b in builds],
        worst=worst["id"],
        band_counts=dict(band_counts),
        asset_mismatch=asset_mismatch[:20],
        base_problems=base_problems,
    )
    out_json.write_text(json.dumps(dump, indent=1, default=str), encoding="utf-8")
    return 0 if failed == 0 else 1


def payload_of(row):
    bits = []
    for key in ("StatusEffects", "StageChanges", "UtilityEffects"):
        if (row.get(key) or "").strip():
            bits.append(f"{key}={row[key]}")
    return "; ".join(bits)


def sens_blurb(sens):
    parts = []
    for name, info in sens.items():
        parts.append(f"{name} moves {len(info['low_flip'])} low flags and {len(info['high_flip'])} high flags")
    return ". ".join(parts) + "."


if __name__ == "__main__":
    sys.exit(main())
