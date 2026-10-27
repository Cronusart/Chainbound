#!/usr/bin/env python3
"""AI decision quality v2 — claims checker.

Replays the shipped personalities (Dumb, Aggressive, Defensive, Utility) from
Assets/Scripts/Combat/Runtime/AI against Moves.csv, SpeciesMovePools.csv, and
species base stats. It does not import the proposal-stack sim. That sim's
policy_greedy is a different brain; this script only proves the two diverge.

Usage:
  python3 ai_decision_quality_v2_check.py <snapshot_root> [results.json]

Exit 0 when every claim passes. Prints PASS/FAIL lines and a one-line summary.
"""

from __future__ import annotations

import csv
import json
import math
import re
import sys
from copy import deepcopy
from pathlib import Path

TYPES = [
    "None", "Node", "Gas", "Block", "Encrypted", "Burned", "Frozen", "Forked",
    "Mined", "Staked", "Compressed", "Bot", "Glitched", "Shard", "Meme",
]
MEAN_ROLL = (0.85 + 1.0) / 2.0  # resolver randomRollMin/Max
AP_TURN = 2
AP_CAP = 10
DAMAGING = {"DamagingMelee", "DamagingRanged"}


class ClaimBook:
    def __init__(self):
        self.rows = []

    def check(self, cid: str, ok: bool, detail: str):
        status = "PASS" if ok else "FAIL"
        self.rows.append({"id": cid, "ok": bool(ok), "detail": detail})
        print(f"{status} {cid}: {detail}")

    def summary(self):
        passed = sum(1 for r in self.rows if r["ok"])
        failed = len(self.rows) - passed
        return passed, failed


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def stage_mult(stage: int) -> float:
    if stage >= 0:
        return (2 + stage) / 2
    return 2 / (2 + abs(stage))


def eff_stat(base: int, stage: int = 0) -> int:
    return max(1, int(base * stage_mult(stage)))


def pct_token(token: str) -> float:
    token = (token or "").strip()
    if not token:
        return 1.0
    if token.endswith("%"):
        return float(token[:-1]) / 100.0
    value = float(token)
    if value > 1:
        return value / 100.0
    return value


def split_effects(text: str) -> list[str]:
    text = (text or "").strip()
    if not text:
        return []
    return [part for part in text.split("|") if part.strip()]


class Move:
    def __init__(self, row: dict):
        self.id = row["MoveId"].strip()
        self.name = row["DisplayName"].strip()
        self.cat = row["Category"].strip()
        self.type = row["BaseType"].strip() or "None"
        self.rarity = row["Rarity"].strip()
        self.targeting = row["Targeting"].strip()
        self.contact = row["ContactType"].strip()
        self.power = int(float(row["Power"] or 0))
        self.ap = int(float(row["APCost"] or 0))
        self.acc = int(float(row["Accuracy"] or 0))
        self.hit_min = int(float(row["HitMin"] or 1))
        self.hit_max = int(float(row["HitMax"] or 1))
        self.recharge = int(float(row["RechargeTurns"] or 0))
        self.statuses = split_effects(row["StatusEffects"])
        self.stages = parse_stages(row["StageChanges"])
        self.tags = [t for t in (row["Tags"] or "").split("|") if t]
        self.once = (row["TriggerOnce"] or "").strip().lower() == "true"
        self.heal = float(row["HealPercentMaxHP"] or 0)
        self.utility_raw = row["UtilityEffects"] or ""
        self.utility = parse_utility(self.utility_raw)
        self.override_atk = (row.get("DamageOverrideAttackStat") or "").strip().lower() == "true"
        self.atk_stat = (row.get("DamageAttackStat") or "").strip()
        self.cond_mult = float(row.get("ConditionalDamageMultiplier") or 0)
        self.cond = row.get("ConditionalDamageConditions") or ""
        self.scale_cond = row.get("ConditionalDamageScalingCondition") or ""
        self.scale_bonus = float(row.get("ConditionalDamageBonusPerCount") or 0)
        self.scale_max = float(row.get("ConditionalDamageMaxMultiplier") or 0)
        self.boss = "BOSS_ONLY" in self.tags
        self.spend_all = any(t.lower() == "spendremainingap" for t in self.tags)

    @property
    def damaging(self) -> bool:
        return self.cat in DAMAGING

    @property
    def avg_hits(self) -> float:
        avg = (self.hit_min + self.hit_max) / 2
        return avg if avg >= 1 else 1

    def has_opcode(self, name: str) -> bool:
        return any(effect["op"] == name for effect in self.utility)

    def block(self):
        for effect in self.utility:
            if effect["op"] == "BlockWindow":
                return effect
        return None

    def certain_self_ap(self) -> int:
        total = 0
        for effect in self.utility:
            if effect["op"] != "GainAP":
                continue
            if effect.get("chance", 1) < 0.999:
                continue
            target = effect.get("target", "Self")
            if target not in ("Self", ""):
                continue
            total += int(effect.get("amount") or 0)
        return total

    def denial_amount(self) -> tuple[str, int]:
        """Certain immediate Steal or Drain, else an armed next-turn leak."""
        for effect in self.utility:
            if effect["op"] in ("StealAP", "DrainAP") and effect.get("chance", 1) >= 0.999:
                return effect["op"], int(effect.get("amount") or 0)
        for effect in self.utility:
            if effect["op"] == "ArmUtilityWindow" and effect.get("trigger") == "OwnerTurnStart":
                mag = int(effect.get("mag") or 0)
                if mag < 0:
                    return "LeakWindow", -mag
        return "", 0


def parse_stages(text: str) -> list[dict]:
    out = []
    for part in split_effects(text):
        bits = part.split(":")
        if len(bits) < 2:
            continue
        try:
            stages = int(float(bits[1]))
        except ValueError:
            continue
        target_self = len(bits) > 3 and bits[3].strip().lower() == "true"
        chance = pct_token(bits[2]) if len(bits) > 2 and bits[2] else 1.0
        out.append({"stat": bits[0], "stages": stages, "chance": chance, "self": target_self})
    return out


def parse_utility(text: str) -> list[dict]:
    out = []
    for part in split_effects(text):
        bits = part.split(":")
        op = bits[0]
        effect = {"op": op, "raw": part}
        if op == "BlockWindow" and len(bits) >= 3:
            effect["pct"] = pct_token(bits[1])
            effect["contact"] = "melee" if bits[2].strip().lower() == "melee" else "ranged"
            effect["turns"] = int(float(bits[3])) if len(bits) > 3 and bits[3] else 2
        elif op == "MitigationWindow" and len(bits) >= 2:
            effect["pct"] = pct_token(bits[1])
        elif op in ("StealAP", "DrainAP", "GainAP", "NextMoveAPSurcharge") and len(bits) >= 2:
            effect["amount"] = float(bits[1])
            effect["chance"] = pct_token(bits[2]) if len(bits) > 2 and bits[2] else 1.0
            effect["target"] = bits[3] if len(bits) > 3 else ""
        elif op == "ArmUtilityWindow" and len(bits) >= 2:
            effect["trigger"] = bits[1]
            # Memory Leak authors the signed AP delta in field 5 (1-based), bits[4].
            try:
                effect["mag"] = int(float(bits[4])) if len(bits) > 4 and bits[4] not in ("",) else 0
            except ValueError:
                effect["mag"] = 0
        out.append(effect)
    return out


class Body:
    def __init__(self, sid, st, move_ids):
        self.sid = sid
        self.type = TYPES[st["primary"]]
        self.base = st
        self.maxhp = st["hp"]
        self.hp = st["hp"]
        self.ap = 0
        self.stages = {k: 0 for k in ("ma", "md", "ra", "rd", "spd")}
        self.move_ids = list(move_ids)
        self.recharge = {}
        self.once = set()
        self.block = None  # (contact, pct) or None
        self.mit = 0.0
        self.armor_flat = 0
        self.statuses = set()
        self.leak = False
        self.alive = True

    def stat(self, key: str) -> int:
        return eff_stat(self.base[key], self.stages.get(key, 0))

    @property
    def speed(self) -> int:
        return self.stat("spd")


def load_moves(root: Path) -> dict[str, Move]:
    path = root / "Docs/Specs/Combat/Data/Moves.csv"
    with path.open(newline="", encoding="utf-8") as handle:
        return {row["MoveId"].strip(): Move(row) for row in csv.DictReader(handle)}


def load_pools(root: Path) -> dict[str, dict]:
    path = root / "Docs/Specs/Combat/Data/SpeciesMovePools.csv"
    pools = {}
    with path.open(newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            sid = (row.get("SpeciesId") or "").strip()
            if not sid or sid.startswith("#"):
                continue
            pools[sid] = row
    return pools


def load_species(root: Path) -> dict[str, dict]:
    out = {}
    folder = root / "Assets/Data/Combat/ChainlingSpecies"
    for path in sorted(folder.glob("*.asset")):
        text = read(path)
        sid = re.search(r"^  speciesId: (\S+)", text, re.M)
        hp = re.search(r"^  baseMaxHP: (\d+)", text, re.M)
        if not sid or not hp:
            continue
        def grab(key):
            match = re.search(rf"^  {key}: (\d+)", text, re.M)
            return int(match.group(1)) if match else 0
        out[sid.group(1)] = {
            "hp": int(hp.group(1)),
            "ma": grab("baseMeleeAtk"),
            "md": grab("baseMeleeDef"),
            "ra": grab("baseRangedAtk"),
            "rd": grab("baseRangedDef"),
            "spd": grab("baseSpeed"),
            "primary": grab("primaryType"),
        }
    return out


def load_directions(root: Path) -> set[tuple[str, str]]:
    path = root / "Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1_directions.csv"
    lines = path.read_text(encoding="utf-8").splitlines()
    header_i = next(i for i, line in enumerate(lines) if line.startswith("Attacker"))
    rows = list(csv.reader(lines[header_i:]))
    header = rows[0][1:]
    adv = set()
    for row in rows[1:]:
        if not row or row[0] in ("", "Meme"):
            continue
        for col, cell in zip(header, row[1:]):
            if cell.strip() == "ADV":
                adv.add((row[0].strip(), col.strip()))
    return adv


def prefix_kit(pool: dict, moves: dict[str, Move], limit: int = 4) -> list[str]:
    ordered = []
    for column in ("InitialMoves", "FlagshipMove", "LearnableMoves", "ExclusiveMoves"):
        for mid in (pool.get(column) or "").split("|"):
            mid = mid.strip()
            if not mid or mid in ordered or mid not in moves:
                continue
            ordered.append(mid)
            if len(ordered) >= limit:
                return ordered
    return ordered


def chem_of(adv: set, attack_type: str, defend_type: str) -> float:
    if attack_type in ("None", "Meme", "") or defend_type in ("None", "Meme", ""):
        return 1.0
    if (attack_type, defend_type) in adv:
        return 1.2
    if (defend_type, attack_type) in adv:
        return 0.8
    return 1.0


def attack_type(move: Move, attacker: Body, inherit: bool) -> str:
    if move.type != "None":
        return move.type
    if inherit:
        return attacker.type
    return "None"


def atk_value(attacker: Body, move: Move, use_override: bool) -> int:
    if use_override and move.override_atk and move.atk_stat:
        key = {
            "MeleeAtk": "ma", "MeleeDef": "md", "RangedAtk": "ra", "RangedDef": "rd", "Speed": "spd",
        }.get(move.atk_stat, "ma")
        return attacker.stat(key)
    if move.cat == "DamagingMelee":
        return attacker.stat("ma")
    if move.cat == "DamagingRanged":
        return attacker.stat("ra")
    return attacker.stat("ma")


def def_value(defender: Body, move: Move) -> int:
    if move.cat == "DamagingMelee":
        return defender.stat("md")
    return defender.stat("rd")


def estimate_damage(attacker: Body, move: Move) -> float:
    """AIHelpers.EstimateDamage. No defense, chemistry, +1, walls, or overrides."""
    if not move.damaging or move.power <= 0:
        return 0.0
    atk = attacker.stat("ma") if move.cat == "DamagingMelee" else attacker.stat("ra")
    acc = move.acc / 100.0 if move.acc > 0 else 1.0
    return move.power * (atk / 100.0) * move.avg_hits * acc


def damage_per_ap(attacker: Body, move: Move) -> float:
    if move.ap <= 0:
        return 0.0
    return estimate_damage(attacker, move) / move.ap


def one_hit(power, atk, defend, chem, cond, mitigation, armor_flat) -> int:
    raw = power * (atk / defend) * MEAN_ROLL + 1.0
    raw *= chem
    raw *= cond
    raw *= 1.0 - mitigation
    raw -= armor_flat
    if mitigation > 0 or armor_flat > 0:
        floor = power * (atk / defend) * 0.1
        raw = max(raw, floor)
    return max(0, math.floor(raw))


def conditional_mult(move: Move, attacker: Body, defender: Body, partner_buffed: bool) -> float:
    mult = 1.0
    if move.cond_mult > 0 and "SpeedHigher" in (move.cond or ""):
        mult = move.cond_mult if attacker.speed > defender.speed else 1.0
    elif move.cond_mult > 1 and not (move.cond or "").strip():
        mult = move.cond_mult
    if move.scale_bonus and "BuffedPartner" in (move.scale_cond or ""):
        base = move.cond_mult if move.cond_mult > 0 else 1.0
        mult = base + (move.scale_bonus if partner_buffed else 0.0)
        if move.scale_max > 0:
            mult = min(move.scale_max, mult)
    return mult if mult > 0 else 1.0


def oracle_vs(
    attacker: Body,
    move: Move,
    defender: Body,
    adv,
    inherit: bool,
    partner_buffed: bool = False,
) -> float:
    """Expected HP removed from one defender. Mean roll, no crit. Block on first hit only."""
    if not move.damaging or move.power <= 0 or not defender.alive:
        return 0.0
    atk = atk_value(attacker, move, True)
    defend = max(1, def_value(defender, move))
    chem = chem_of(adv, attack_type(move, attacker, inherit), defender.type)
    cond = conditional_mult(move, attacker, defender, partner_buffed)
    acc = move.acc / 100.0 if move.acc > 0 else 1.0
    hits = move.avg_hits
    total = 0.0
    full = int(math.floor(hits))
    frac = hits - full
    sequence = full + (1 if frac > 1e-9 else 0)
    for i in range(sequence):
        weight = 1.0 if i < full else frac
        mit = 0.0
        flat = 0
        if i == 0:
            if defender.block and (
                (defender.block[0] == "melee" and move.cat == "DamagingMelee")
                or (defender.block[0] == "ranged" and move.cat == "DamagingRanged")
            ):
                mit = defender.block[1]
            if defender.mit > 0:
                mit = 1.0 - (1.0 - mit) * (1.0 - defender.mit)
            flat = defender.armor_flat
        total += weight * acc * one_hit(move.power, atk, defend, chem, cond, mit, flat)
    return min(defender.hp, total)


def oracle_action(attacker, move, foes, adv, inherit, partner_buffed=False) -> float:
    if move.targeting == "AllEnemies":
        return sum(oracle_vs(attacker, move, foe, adv, inherit, partner_buffed) for foe in foes if foe.alive)
    target = next((foe for foe in foes if foe.alive), None)
    if target is None:
        return 0.0
    return oracle_vs(attacker, move, target, adv, inherit, partner_buffed)


def enemy_negative_stages(move: Move) -> int:
    return sum(abs(stage["stages"]) for stage in move.stages if stage["stages"] < 0 and not stage["self"])


def positive_self_stage(move: Move) -> bool:
    return any(stage["stages"] > 0 and stage["self"] for stage in move.stages)


def score_block(move: Move, ally: Body, foes: list[Body]) -> float:
    block = move.block()
    if block is None or ally is None:
        return -1.0
    protects_melee = block["contact"] == "melee"
    largest = 0.0
    for foe in foes:
        if not foe.alive:
            continue
        for mid in foe.move_ids:
            attack = MOVE[mid]
            if not attack.damaging:
                continue
            matching = attack.cat == "DamagingMelee" if protects_melee else attack.cat == "DamagingRanged"
            if matching:
                largest = max(largest, estimate_damage(foe, attack))
    mit = block["pct"]
    prevented = largest * max(0.0, mit)
    threshold = max(1.0, ally.hp * 0.15)
    return prevented if prevented >= threshold else -1.0


def should_bank(actor: Body, allies: list[Body], foes: list[Body], moves: dict[str, Move]) -> bool:
    if actor.ap >= AP_CAP:
        return False
    for mid in actor.move_ids:
        move = moves[mid]
        block = move.block()
        if block is None:
            continue
        mit = block["pct"]
        if mit <= 0.5 or move.ap <= AP_TURN or actor.ap >= move.ap:
            continue
        for ally in allies:
            if not ally.alive or ally.block:
                continue
            if score_block(move, ally, foes) > 0:
                return True
    return False


class Action:
    def __init__(self, kind, move_id="", slot=0, block_score=0.0):
        self.kind = kind
        self.move_id = move_id
        self.slot = slot
        self.block_score = block_score


def legal_actions(actor: Body, allies: list[Body], foes: list[Body], moves, bench: bool) -> list[Action]:
    actions = []
    for mid in actor.move_ids:
        move = moves[mid]
        if move.cat == "Passive":
            continue
        if mid in actor.once or mid in actor.recharge and actor.recharge[mid] > 0:
            continue
        if move.ap > actor.ap:
            continue
        if move.block():
            for slot, ally in enumerate(allies):
                if not ally.alive or ally.block:
                    continue
                actions.append(Action("move", mid, slot, score_block(move, ally, foes)))
        else:
            actions.append(Action("move", mid, -1, 0.0))
    if bench:
        actions.append(Action("swap", "bench", 0, 0.0))
    if not actions:
        actions.append(Action("pass"))
    return actions


def most_vulnerable(foes: list[Body]) -> Body | None:
    living = [foe for foe in foes if foe.alive]
    if not living:
        return None
    if len(living) == 1:
        return living[0]
    a, b = living[0], living[1]
    va = 1 - a.hp / max(1, a.maxhp)
    vb = 1 - b.hp / max(1, b.maxhp)
    return a if va >= vb else b


def scoring_cost(actor: Body, move: Move) -> int:
    if move.spend_all:
        return max(0, actor.ap)
    cost = move.ap
    for effect in move.utility:
        if effect["op"] != "NextMoveAPSurcharge":
            continue
        if effect.get("target") not in ("Self", ""):
            continue
        chance = effect.get("chance", 1)
        cost += max(0, math.ceil(effect.get("amount", 0) * chance))
    if move.ap <= 0:
        return max(0, cost)
    return max(1, cost) if cost != move.ap else move.ap


def utility_score(actor: Body, action: Action, allies, foes, moves) -> float:
    if action.kind == "swap":
        return 1.0
    if action.kind != "move":
        return 0.0
    move = moves[action.move_id]
    if move.block():
        return action.block_score
    score = 0.0
    cost = scoring_cost(actor, move)
    if move.cat == "StatusUtility":
        score += 30
        score += len(move.statuses) * 15
        score += enemy_negative_stages(move) * 10
        living = [foe for foe in foes if foe.alive]
        if living:
            least = min(living, key=lambda foe: len(foe.statuses))
            if len(least.statuses) == 0:
                score += 10
    elif move.cat == "Support":
        score += 15
        for ally in allies:
            if ally.hp / max(1, ally.maxhp) < 0.5:
                score += 10
        if any(moves[mid].cat == "Passive" and mid == "uplink" for mid in actor.move_ids) and move.targeting in (
            "SingleAlly", "AllAllies"
        ):
            score += 12
    elif move.damaging:
        score += (estimate_damage(actor, move) / cost * 2) if cost > 0 else 0
        score += len(move.statuses) * 20
        score += enemy_negative_stages(move) * 8
        if any(mid == "kindling" for mid in actor.move_ids):
            for foe in foes:
                if foe.alive and "Kindled" not in foe.statuses:
                    score += max(1.0, foe.maxhp * 0.06)
                    break
    if cost > 0:
        score -= cost
    if move.boss and actor.ap >= cost:
        score += 60
    return score


def choose(personality: str, actor, allies, foes, moves, bench: bool) -> Action:
    legal = legal_actions(actor, allies, foes, moves, bench)
    if personality == "Dumb":
        for action in legal:
            if action.kind == "move" and moves[action.move_id].damaging:
                return action
        for action in legal:
            if action.kind == "move" and moves[action.move_id].cat != "Passive":
                return action
        return Action("pass")
    if personality == "Aggressive":
        best = -1.0
        chosen = Action("pass")
        vuln = most_vulnerable(foes)
        for action in legal:
            score = 0.0
            if action.kind == "move":
                move = moves[action.move_id]
                if move.damaging:
                    score = damage_per_ap(actor, move) * 10
                    if vuln is not None and estimate_damage(actor, move) >= vuln.hp:
                        score += 50
                else:
                    score = 0.1
            elif action.kind == "swap":
                score = 0.05
            if score > best:
                best = score
                chosen = action
        return chosen
    if personality == "Defensive":
        living = [foe for foe in foes if foe.alive]
        weakest = min(living, key=lambda foe: foe.hp) if living else None
        best_lethal = None
        best_dpa = -1.0
        if weakest is not None:
            for action in legal:
                if action.kind != "move":
                    continue
                move = moves[action.move_id]
                if not move.damaging:
                    continue
                if estimate_damage(actor, move) >= weakest.hp:
                    dpa = damage_per_ap(actor, move)
                    if dpa > best_dpa:
                        best_dpa = dpa
                        best_lethal = action
        if best_lethal is not None:
            return best_lethal
        if should_bank(actor, allies, foes, moves):
            return Action("pass")
        if actor.hp / max(1, actor.maxhp) < 0.20:
            for action in legal:
                if action.kind == "swap":
                    return action
        if any(ally.hp / max(1, ally.maxhp) < 0.40 for ally in allies):
            best_score = float("-inf")
            best = None
            for action in legal:
                if action.kind != "move":
                    continue
                move = moves[action.move_id]
                if move.cat == "Support" or "HEAL" in move.tags:
                    score = action.block_score if move.block() else 0.0
                    if score > best_score:
                        best_score = score
                        best = action
            if best is not None and best_score >= 0:
                return best
        if all(ally.hp / max(1, ally.maxhp) >= 0.55 for ally in allies):
            for action in legal:
                if action.kind != "move":
                    continue
                move = moves[action.move_id]
                if move.cat == "StatusUtility" and move.targeting == "Self" and positive_self_stage(move):
                    return action
        best_dpa = -1.0
        best = Action("pass")
        for action in legal:
            if action.kind != "move" or not moves[action.move_id].damaging:
                continue
            dpa = damage_per_ap(actor, moves[action.move_id])
            if dpa > best_dpa:
                best_dpa = dpa
                best = action
        return best
    if personality == "Utility":
        if should_bank(actor, allies, foes, moves):
            return Action("pass")
        best = -1.0
        chosen = Action("pass")
        for action in legal:
            score = utility_score(actor, action, allies, foes, moves)
            if score > best:
                best = score
                chosen = action
        return chosen
    raise AssertionError(personality)


def apply_spend_and_riders(actor: Body, move: Move, foes: list[Body]):
    if actor.ap < move.ap:
        return False
    actor.ap -= move.ap
    if move.spend_all:
        actor.ap = 0
    for effect in move.utility:
        if effect["op"] == "GainAP" and effect.get("chance", 1) >= 0.999 and effect.get("target", "Self") in ("Self", ""):
            actor.ap = min(AP_CAP, actor.ap + int(effect.get("amount") or 0))
        if effect["op"] in ("StealAP", "DrainAP") and effect.get("chance", 1) >= 0.999:
            amount = int(effect.get("amount") or 0)
            target = next((foe for foe in foes if foe.alive), None)
            if target is not None:
                removed = min(target.ap, amount)
                target.ap -= removed
                if effect["op"] == "StealAP":
                    actor.ap = min(AP_CAP, actor.ap + removed)
        if effect["op"] == "ArmUtilityWindow" and effect.get("trigger") == "OwnerTurnStart" and effect.get("mag", 0) < 0:
            target = next((foe for foe in foes if foe.alive), None)
            if target is not None:
                target.leak = True
    for stage in move.stages:
        if stage["chance"] < 0.999:
            continue
        key = {
            "MeleeAtk": "ma", "MeleeDef": "md", "RangedAtk": "ra", "RangedDef": "rd", "Speed": "spd",
        }.get(stage["stat"])
        if not key:
            continue
        host = actor if stage["self"] else next((foe for foe in foes if foe.alive), None)
        if host is None:
            continue
        host.stages[key] = max(-6, min(6, host.stages[key] + stage["stages"]))
    if move.heal > 0 and move.targeting in ("Self", "SingleAlly", "AllAllies"):
        actor.hp = min(actor.maxhp, actor.hp + max(1, math.floor(actor.maxhp * move.heal / 100.0)))
    for status in move.statuses:
        name = status.split(":")[0]
        if move.targeting == "Self":
            actor.statuses.add(name)
        else:
            target = next((foe for foe in foes if foe.alive), None)
            if target is not None:
                target.statuses.add(name)
    block = move.block()
    if block is not None:
        actor.block = (block["contact"], block["pct"], int(block.get("turns") or 2))
    if move.once:
        actor.once.add(move.id)
    if move.recharge > 0:
        actor.recharge[move.id] = move.recharge
    return True


def deal(attacker, move, defender, adv, inherit):
    if not move.damaging:
        return 0
    dealt = 0
    hits = move.hit_min  # deterministic lower hit count for fight traces; oracle uses the average
    # Fight traces use the average hit count's expected damage as one chunk so the trace matches the oracle.
    dealt = int(math.floor(oracle_vs(attacker, move, defender, adv, inherit)))
    if defender.block and (
        (defender.block[0] == "melee" and move.cat == "DamagingMelee")
        or (defender.block[0] == "ranged" and move.cat == "DamagingRanged")
    ):
        defender.block = None
    defender.hp -= dealt
    if defender.hp <= 0:
        defender.hp = 0
        defender.alive = False
    return dealt


def tick_recharge(body: Body):
    dead = []
    for mid, left in body.recharge.items():
        body.recharge[mid] = left - 1
        if body.recharge[mid] <= 0:
            dead.append(mid)
    for mid in dead:
        del body.recharge[mid]
    if body.block:
        contact, pct, turns = body.block
        turns -= 1
        body.block = None if turns <= 0 else (contact, pct, turns)


def quiet_line(personality, sid, kit, species, moves, foe: Body, turns=12) -> list[str]:
    actor = Body(sid, species[sid], kit)
    clicks = []
    for _ in range(turns):
        actor.ap = min(AP_CAP, actor.ap + AP_TURN)
        if actor.leak:
            actor.ap = max(0, actor.ap - 2)
            actor.leak = False
        action = choose(personality, actor, [actor], [foe], moves, bench=False)
        clicks.append(action.move_id or action.kind)
        if action.kind == "move":
            apply_spend_and_riders(actor, moves[action.move_id], [foe])
        tick_recharge(actor)
    return clicks


def make_foe_from(sid, species, kit, hp=None) -> Body:
    foe = Body(sid, species[sid], kit)
    if hp is not None:
        foe.hp = hp
        foe.maxhp = hp
    return foe


def fight(left, right, moves, adv, inherit, turns, left_policy, right_policy):
    """1v1. left_policy/right_policy are personality names or callables (body, foes)->Action."""
    trace = []
    for turn in range(1, turns + 1):
        if not left.alive or not right.alive:
            break
        for body in (left, right):
            if body.alive:
                body.ap = min(AP_CAP, body.ap + AP_TURN)
                if body.leak:
                    body.ap = max(0, body.ap - 2)
                    body.leak = False
        actions = []
        for body, policy, foes in ((left, left_policy, [right]), (right, right_policy, [left])):
            if isinstance(policy, str):
                action = choose(policy, body, [body], foes, moves, bench=False)
            else:
                action = policy(body, foes)
            actions.append((body, action))
        def sort_key(item):
            body, action = item
            move = moves[action.move_id] if action.kind == "move" else None
            prio = 0 if move and ("PRIO" in move.tags or "PRIOR" in move.tags) else 1
            return (prio, -body.speed, 0 if body is left else 1)
        actions.sort(key=sort_key)
        turn_clicks = []
        for body, action in actions:
            if not body.alive:
                continue
            foes = [right if body is left else left]
            label = action.move_id or action.kind
            if action.kind == "move":
                move = moves[action.move_id]
                if body.ap < move.ap:
                    turn_clicks.append({"who": body.sid, "click": label, "result": "no_ap", "ap": body.ap})
                    continue
                apply_spend_and_riders(body, move, foes)
                dealt = 0
                if move.damaging:
                    target = foes[0]
                    if move.targeting == "AllEnemies":
                        dealt = deal(body, move, target, adv, inherit)
                    else:
                        dealt = deal(body, move, target, adv, inherit)
                turn_clicks.append({"who": body.sid, "click": label, "dmg": dealt, "ap": body.ap, "foe_hp": foes[0].hp, "foe_ap": foes[0].ap})
            else:
                turn_clicks.append({"who": body.sid, "click": label, "ap": body.ap})
        for body in (left, right):
            tick_recharge(body)
        trace.append({"turn": turn, "clicks": turn_clicks, "left_hp": left.hp, "right_hp": right.hp})
        if not left.alive or not right.alive:
            break
    return trace


MOVE: dict[str, Move] = {}


def main(argv: list[str]) -> int:
    global MOVE
    if len(argv) < 2:
        print("usage: ai_decision_quality_v2_check.py <snapshot_root> [results.json]")
        return 2
    root = Path(argv[1])
    out_path = Path(argv[2]) if len(argv) > 2 else Path(__file__).with_name("ai_decision_quality_v2_results.json")
    book = ClaimBook()
    ai = root / "Assets/Scripts/Combat/Runtime/AI"
    helpers = read(ai / "AIHelpers.cs")
    aggressive = read(ai / "AggressiveAI.cs")
    defensive = read(ai / "DefensiveAI.cs")
    utility = read(ai / "UtilityStatusAI.cs")
    dumb = read(ai / "DumbAI.cs")
    evaluator = read(ai / "CombatStateEvaluator.cs")
    ai_blob = "\n".join([helpers, aggressive, defensive, utility, dumb, evaluator, read(ai / "RandomAI.cs"), read(ai / "ICombatAI.cs")])
    damage_cs = read(root / "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs")
    resolver = read(root / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs")
    controller = read(root / "Assets/Scripts/Combat/Runtime/Presentation/CombatBattleController.cs")
    enums = read(root / "Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs")
    config = read(root / "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset")
    stack_sim = read(root / "Docs/Combat/Design/stack_2v2_sim.py")
    supabase = "\n".join(path.read_text(encoding="utf-8", errors="replace") for path in (root / "Supabase").rglob("*.ts"))

    book.check("est_divides_by_100", "atk / 100f" in helpers and "EstimateDamage" in helpers, "AIHelpers.EstimateDamage uses Atk/100")
    book.check("est_omits_defense", "CoreStat.MeleeDef" not in helpers.split("EstimateDamage")[1].split("DamagePerAP")[0], "estimate block does not read a defense stat")
    book.check(
        "est_omits_chemistry",
        "ChemistryResolver" not in ai_blob and "DamageMultiplier" not in ai_blob and "no chemistry" in helpers,
        "the estimate comment says it skips chemistry, and no AI calls the resolver",
    )
    book.check("dpa_zero_at_cost_0", "if (insc.apCost <= 0) return 0f;" in helpers, "DamagePerAP returns 0 when apCost <= 0")
    book.check("untargeted_use_move", "CombatAction.UseMove(teamIndex, i)" in helpers, "non-block moves are UseMove(team, index) with target -1")
    book.check("block_use_move_targeted", "CombatAction.UseMove(teamIndex, i, teamIndex, targetSlot)" in helpers, "block moves write an ally slot")
    book.check("bank_threshold", "mitigation <= 0.5f || move.apCost <= CombatConstants.AP_PER_TURN" in helpers, "bank requires mitigation > 0.5 and cost > AP per turn")
    book.check("aggressive_no_bank", "ShouldBankForGreaterBlock" not in aggressive and "ShouldBankForGreaterBlock" in defensive and "ShouldBankForGreaterBlock" in utility, "only Defensive and Utility call the block bank")
    book.check("aggressive_nondmg_01", "score = 0.1f;" in aggressive, "Aggressive scores every non-damaging move at 0.1")
    book.check(
        "no_denial_opcodes_in_ai",
        all(token not in ai_blob for token in (
            "StealAP", "DrainAP", "GainAP", "ArmUtilityWindow", "MitigationWindow",
            "BankAP", "HealSelf", "StripBuff", "ChangeStage",
        )),
        "AI sources do not name Steal, Drain, Gain, bank, strip, cleanse, or mitigation opcodes",
    )
    book.check("no_difficulty_in_ai", "EncounterDifficulty" not in ai_blob, "no personality reads EncounterDifficulty")
    book.check("no_speed_in_choice", "CoreStat.Speed" not in ai_blob and "PRIO" not in aggressive and "PRIO" not in utility, "choice code does not read Speed or priority tags")
    book.check("no_cipher_gate", "IsCipherLocked" not in ai_blob and "BuffGate" not in ai_blob, "no AI calls the cipher-lock gate")
    book.check("no_foe_block_read", "mitigationSources" not in ai_blob and "HasIntactBlock" not in ai_blob, "no AI reads a foe's live wall, barrier, or mitigation list")
    book.check("passives_named", "uplink" in utility and "kindling" in utility and "immutable_ledger" not in ai_blob and "patience_passive" not in ai_blob and "fee_collector" not in ai_blob and "streamlined_flow" not in ai_blob, "Utility names Uplink and Kindling only")
    book.check("evaluator_unused", "CombatStateEvaluator.Evaluate" not in ai_blob.replace("public static float Evaluate", ""), "no personality calls CombatStateEvaluator.Evaluate")
    book.check("threat_only_defined", aggressive.count("ThreatScore") == 0 and "ThreatScore" in helpers, "ThreatScore is only a PickTarget option, and Aggressive does not call it")
    book.check("create_ai_ignores_difficulty", "encounter.difficulty" not in controller.split("CreateAI")[1].split("private static ICombatAI CreateAI")[0] or "CreateAI(cpuPersonality)" in controller, "CPU personality comes from encounter.aiPersonality")
    book.check("cpu_field_unread", "CreateAI(cpuAI)" not in controller and "CreateAI(cpuPersonality)" in controller, "the cpuAI inspector field is not the factory input")
    book.check("bench_recharge_exists", "TickBenchedRecharge" in resolver and "TickRechargeWhileBenched" in resolver, "recharge ticks on the bench")
    book.check("ai_no_bench_clock", "recharge" not in ai_blob.lower() or "Recharge" not in aggressive, "Aggressive never reads recharge; bench ticking is unused by the chooser")
    book.check("inherit_typeless", "return attacker.primaryType;" in damage_cs and "move.baseType != ChainlingType.None" in damage_cs, "typeless damaging moves inherit the attacker's primary type")
    book.check("stab_off", "enableStab: 0" in config, "STAB is off in the default resolver config")
    book.check("ap_config", "apPerTurn: 2" in config and "apCap: 10" in config and "mitigationFloor: 0.1" in config, "regen 2, cap 10, mitigation floor 0.1")
    book.check("no_armor_flat_opcode", "ArmorFlat" not in enums, "UtilityEffectType has no ArmorFlat opcode")
    book.check("plus_one_before_chem", damage_cs.index("additiveDamageConstant") < damage_cs.index("baseDamage *= chemMult"), "the +1 is added before the chemistry multiplier")
    book.check("no_server_ai", "ICombatAI" not in supabase and "UtilityStatusAI" not in supabase and "GenerateActions" not in supabase, "Supabase functions contain no combat chooser")
    book.check("greedy_is_not_shipped", "def policy_greedy" in stack_sim and "UtilityStatusAI" not in stack_sim and "(\"pass\",)" in stack_sim, "stack_2v2_sim policy_greedy can pass and is not the shipped AI")
    resolve_body = resolver.split("private void ResolveAction")[1].split("private void EmitBossPayoffTell")[0]
    book.check(
        "boss_tell_before_spend",
        "EmitBossPayoffTell(state, actingTeam, actor);" in resolve_body
        and resolve_body.index("EmitBossPayoffTell") < resolve_body.index("TrySpendCost"),
        "the boss tell reads AP at the start of ResolveAction, before TrySpendCost",
    )
    book.check(
        "random_uses_combat_rng",
        "CombatRng" in read(ai / "RandomAI.cs"),
        "RandomAI draws the combat RNG",
    )
    glitched = resolver.split("private CombatAction BuildRandomLegalMisAct")[1][:900]
    book.check(
        "glitched_draws_legal",
        "AIHelpers.GetLegalActions" in glitched,
        "Glitched Mind draws its mis-act from GetLegalActions",
    )

    moves = load_moves(root)
    MOVE = moves
    pools = load_pools(root)
    species = load_species(root)
    adv = load_directions(root)

    blocks = []
    for move in moves.values():
        block = move.block()
        if block:
            blocks.append((move.id, block["pct"], move.ap, block["contact"]))
    bankable = [row for row in blocks if row[1] > 0.5 and row[2] > AP_TURN]
    book.check("only_blockade_banks", bankable == [("blockade", 0.6, 3, "melee")], f"bank-eligible blocks={bankable}")
    book.check("ember_not_bankable", any(row[0] == "ember_wall" and row[1] <= 0.5 and row[2] <= AP_TURN for row in blocks), "Ember Wall is 50% and 2 AP, so ShouldBank skips it")
    book.check("eight_block_rows", len(blocks) == 8, f"BlockWindow rows={len(blocks)}")

    guard = moves["guard_posture"]
    book.check(
        "guard_is_percent",
        guard.has_opcode("MitigationWindow") and not guard.has_opcode("BlockWindow") and any(e.get("pct") == 0.25 for e in guard.utility),
        "Guard Posture is MitigationWindow 25%, tag ARMOR, not a flat and not a block",
    )
    book.check("guard_unpooled", all("guard_posture" not in (row.get("LearnableMoves") or "") + (row.get("ExclusiveMoves") or "") + (row.get("InitialMoves") or "") + (row.get("FlagshipMove") or "") for row in pools.values()), "Guard Posture is in no learn pool")
    book.check("grind_unpooled", all("grind_tax" not in "|".join(row.values()) for row in pools.values()), "Grind Tax is in no learn pool")
    book.check("thread_lock_not_on_kitsauto", "thread_lock" not in "|".join(pools["kitsauto"].values()) and "packet_skim" in pools["kitsauto"]["LearnableMoves"], "Kitsauto learns Packet Skim and does not learn Thread Lock")
    book.check("mev_unpooled", all("mev_extract" not in "|".join(row.values()) for row in pools.values()), "MEV Extract is in no learn pool")
    book.check("meme_directions_empty", not any(a == "Meme" or b == "Meme" for a, b in adv), "v1 directions give Meme no advantage cell")

    cofi = species["cofi_lootwak"]
    book.check("cofi_ra_24", cofi["ra"] == 24 and cofi["spd"] == 16 and TYPES[cofi["primary"]] == "Staked", f"Cofi RA {cofi['ra']} SPD {cofi['spd']} type {TYPES[cofi['primary']]}")
    book.check("bean_sip_gain", moves["bean_sip"].certain_self_ap() == 1 and moves["bean_sip"].recharge == 2 and "HEAL" in moves["bean_sip"].tags, "Bean Sip gains 1 AP, recharge 2, HEAL tag")
    book.check("grind_boss", moves["grind_tax"].boss and moves["grind_tax"].ap == 6 and moves["grind_tax"].targeting == "AllEnemies", "Grind Tax is boss-only, 6 AP, all foes")

    encounter = read(root / "Assets/Data/Combat/BattleProviders/CofiLootwak_Encounter.asset")
    intro = read(root / "Assets/Data/Combat/BattleProviders/IntroFirstCreature_Encounter.asset")
    manual = read(root / "Assets/Data/Combat/BattleProviders/ManualEncounterProvider.asset")
    book.check("personalities_assigned", "aiPersonality: 3" in encounter and "difficulty: 2" in encounter and "aiPersonality: 1" in intro and "aiPersonality: 4" in manual, "Cofi Utility/Boss, intro Aggressive, manual Random")
    book.check("no_defensive_encounter", "aiPersonality: 2" not in encounter and "aiPersonality: 2" not in intro and "aiPersonality: 2" not in manual, "no checked-in encounter asset selects Defensive")
    auth = read(root / "Assets/Scripts/Combat/Runtime/Services/AuthoritativeBattleEncounterProvider.cs")
    book.check("auth_aggressive", "aiPersonality = AIPersonality.Aggressive" in auth, "authoritative battles hard-code Aggressive")

    # Pinned scorer spot-checks on the Cofi encounter kit.
    cofi_body = Body("cofi_lootwak", cofi, ["bean_sip", "civet_beam", "grind_tax"])
    cofi_body.ap = 6
    foe = Body("node_dummy", {"hp": 5000, "ma": 70, "md": 55, "ra": 70, "rd": 55, "spd": 50, "primary": 1}, ["baseline_striker", "baseline_shot"])
    foe.hp = 5000
    sip = utility_score(cofi_body, Action("move", "bean_sip"), [cofi_body], [foe], moves)
    beam_est = estimate_damage(cofi_body, moves["civet_beam"])
    beam = utility_score(cofi_body, Action("move", "civet_beam"), [cofi_body], [foe], moves)
    tax = utility_score(cofi_body, Action("move", "grind_tax"), [cofi_body], [foe], moves)
    book.check("sip_score_39", abs(sip - 39) < 1e-6, f"Bean Sip utility score {sip}")
    book.check("beam_est", abs(beam_est - 2.736) < 1e-6, f"Bean Beam estimate {beam_est}")
    book.check("beam_score", abs(beam - 8.736) < 1e-6, f"Bean Beam utility score {beam}")
    book.check("tax_score", abs(tax - 68.84) < 1e-6, f"Grind Tax utility score {tax}")
    book.check("tax_without_60_loses", abs((tax - 60) - 8.84) < 1e-6 and (tax - 60) < sip, "without +60 the tax scores 8.84 and loses to Sip")

    kits = {}
    for sid, pool in pools.items():
        if sid not in species:
            book.check(f"species_{sid}", False, "pool species missing a base-stat asset")
            continue
        kits[sid] = prefix_kit(pool, moves)
    book.check("kit_count", len(kits) == 18, f"rebirth-0 prefix kits={len(kits)}")
    book.check(
        "kitsauto_prefix",
        kits["kitsauto"] == ["blade_flurry", "fox_feint", "multi_thread_shot", "packet_skim"],
        "Kitsauto prefix " + ",".join(kits["kitsauto"]),
    )
    book.check(
        "blockwyrm_prefix",
        kits["blockwyrm"] == ["immovable_stance", "immutable_ledger", "bulwark_blow", "blockade"],
        "Blockwyrm prefix " + ",".join(kits["blockwyrm"]),
    )
    book.check(
        "cinislug_prefix",
        kits["cinislug"] == ["cinder_burst", "ember_wall", "kindling", "hype_burst"],
        "Cinislug prefix " + ",".join(kits["cinislug"]),
    )
    book.check(
        "validape_prefix",
        kits["validape"] == ["baseline_striker", "hammer_sync", "hub_heal", "uplink"],
        "Validape prefix " + ",".join(kits["validape"]),
    )
    book.check(
        "deghoul_prefix",
        kits["deghoul"] == ["gas_bump", "transaction_tax", "volatile_shot", "slipstream"],
        "Deghoul prefix " + ",".join(kits["deghoul"]),
    )
    book.check(
        "keyper_prefix",
        kits["keyper"] == ["cipher_shot", "memory_leak", "cipher_burst", "key_rotation"],
        "Keyper prefix " + ",".join(kits["keyper"]),
    )

    def fresh_hammer():
        # High current HP so a KO bonus cannot fire. Max HP stays real so Kindling's 6% chip stays real.
        foe = make_foe_from("validape", species, kits["validape"])
        foe.hp = 5000
        return foe

    lines = {}
    for sid, kit in kits.items():
        lines[sid] = {}
        for personality in ("Dumb", "Aggressive", "Defensive", "Utility"):
            lines[sid][personality] = quiet_line(personality, sid, kit, species, moves, fresh_hammer(), 12)

    cofi_kit = ["bean_sip", "civet_beam", "grind_tax"]
    cofi_lines = {
        personality: quiet_line(personality, "cofi_lootwak", cofi_kit, species, moves, fresh_hammer(), 16)
        for personality in ("Dumb", "Aggressive", "Defensive", "Utility")
    }
    book.check(
        "cofi_utility_loop",
        cofi_lines["Utility"][:8] == ["bean_sip", "civet_beam", "bean_sip", "grind_tax"] * 2,
        "Utility Cofi " + ",".join(cofi_lines["Utility"][:8]),
    )
    book.check(
        "cofi_aggressive_beams",
        cofi_lines["Aggressive"].count("grind_tax") == 0 and all(c == "civet_beam" for c in cofi_lines["Aggressive"]),
        f"Aggressive Cofi taxes={cofi_lines['Aggressive'].count('grind_tax')} unique={sorted(set(cofi_lines['Aggressive']))}",
    )
    book.check(
        "cofi_dumb_beams",
        all(c == "civet_beam" for c in cofi_lines["Dumb"]),
        "Dumb Cofi never leaves Bean Beam",
    )
    book.check(
        "cofi_defensive_same_loop",
        cofi_lines["Defensive"][:8] == cofi_lines["Utility"][:8],
        "Defensive Cofi matches the Utility loop on the encounter kit: " + ",".join(cofi_lines["Defensive"][:8]),
    )

    # Both legal at 6 AP, nothing recharging.
    spot = Body("cofi_lootwak", cofi, cofi_kit)
    spot.ap = 6
    spot_foe = make_foe_from("validape", species, kits["validape"], hp=5000)
    spot_picks = {p: choose(p, spot, [spot], [spot_foe], moves, False).move_id or "pass" for p in ("Dumb", "Aggressive", "Defensive", "Utility")}
    book.check(
        "cofi_both_legal",
        spot_picks == {"Dumb": "civet_beam", "Aggressive": "grind_tax", "Defensive": "bean_sip", "Utility": "grind_tax"},
        f"at 6 AP with Sip and Tax both legal: {spot_picks}",
    )

    book.check("kits_util_opens_feint", lines["kitsauto"]["Utility"][0] == "fox_feint", "Utility Kitsauto opens " + lines["kitsauto"]["Utility"][0])
    book.check(
        "kits_util_then_skim",
        lines["kitsauto"]["Utility"][1:] == ["packet_skim"] * 11,
        "after Feint, Utility Kitsauto skims: " + ",".join(lines["kitsauto"]["Utility"][:4]),
    )
    book.check(
        "kits_agg_never_denies",
        "packet_skim" not in lines["kitsauto"]["Aggressive"] and "fox_feint" not in lines["kitsauto"]["Aggressive"],
        "Aggressive Kitsauto clicks " + ",".join(sorted(set(lines["kitsauto"]["Aggressive"]))),
    )
    book.check(
        "deghoul_util_taxes",
        all(c == "transaction_tax" for c in lines["deghoul"]["Utility"]),
        "Utility Deghoul " + ",".join(sorted(set(lines["deghoul"]["Utility"]))),
    )
    book.check(
        "deghoul_def_slip",
        all(c == "slipstream" for c in lines["deghoul"]["Defensive"]),
        "Defensive Deghoul " + ",".join(sorted(set(lines["deghoul"]["Defensive"]))),
    )
    book.check(
        "deghoul_agg_bump",
        all(c == "gas_bump" for c in lines["deghoul"]["Aggressive"]),
        "Aggressive Deghoul " + ",".join(sorted(set(lines["deghoul"]["Aggressive"]))),
    )
    book.check(
        "validape_agg_no_hammer",
        "hammer_sync" not in lines["validape"]["Aggressive"],
        "Aggressive Validape clicks " + ",".join(sorted(set(lines["validape"]["Aggressive"]))),
    )
    book.check(
        "keyper_util_ciphers",
        all(c == "cipher_shot" for c in lines["keyper"]["Utility"]) and "memory_leak" not in lines["keyper"]["Utility"],
        "Utility Keyper clicks " + ",".join(sorted(set(lines["keyper"]["Utility"]))),
    )
    book.check(
        "keyper_def_rotation",
        all(c == "key_rotation" for c in lines["keyper"]["Defensive"]) and "memory_leak" not in lines["keyper"]["Defensive"],
        "Defensive Keyper clicks " + ",".join(sorted(set(lines["keyper"]["Defensive"]))),
    )
    book.check(
        "cinislug_util_hype",
        all(c == "hype_burst" for c in lines["cinislug"]["Utility"]) and "cinder_burst" not in lines["cinislug"]["Utility"],
        "Utility Cinislug " + ",".join(sorted(set(lines["cinislug"]["Utility"]))),
    )
    book.check(
        "cinislug_agg_no_cinder",
        "cinder_burst" not in lines["cinislug"]["Aggressive"] and "ember_wall" in lines["cinislug"]["Aggressive"],
        "Aggressive Cinislug " + ",".join(lines["cinislug"]["Aggressive"][:6]),
    )
    book.check(
        "cinislug_def_accidental_pass",
        lines["cinislug"]["Defensive"][:6] == ["pass", "cinder_burst", "cinder_burst"] * 2
        and "ember_wall" not in lines["cinislug"]["Defensive"],
        "Defensive Cinislug " + ",".join(lines["cinislug"]["Defensive"][:6]),
    )
    book.check(
        "humpacked_util_buffs_then_hits",
        lines["humpacked"]["Utility"][:2] == ["compression_trade", "compression_trade"]
        and all(c == "lean_hit" for c in lines["humpacked"]["Utility"][2:])
        and "barrier_wall" not in lines["humpacked"]["Utility"],
        "Utility Humpacked " + ",".join(lines["humpacked"]["Utility"][:4]),
    )
    book.check(
        "balloom_util_surges",
        all(c == "fuel_surge" for c in lines["balloom"]["Utility"]) and all(c == "gas_bump" for c in lines["balloom"]["Aggressive"]),
        "Balloom utility " + ",".join(sorted(set(lines["balloom"]["Utility"]))) + " aggressive " + ",".join(sorted(set(lines["balloom"]["Aggressive"]))),
    )
    book.check(
        "rattle_util_reserves",
        all(c == "ap_reserve" for c in lines["rattlestake"]["Utility"]),
        "Utility Rattlestake " + ",".join(sorted(set(lines["rattlestake"]["Utility"]))),
    )
    book.check(
        "kodiax_util_extracts",
        all(c == "crystal_extraction" for c in lines["kodiax"]["Utility"]) and "deep_dig" not in lines["kodiax"]["Aggressive"],
        "Kodiax utility " + ",".join(sorted(set(lines["kodiax"]["Utility"]))) + " aggressive " + ",".join(sorted(set(lines["kodiax"]["Aggressive"]))),
    )
    book.check(
        "gilarime_list_order",
        all(c == "frost_shards" for c in lines["gilarime"]["Dumb"])
        and all(c == "frost_shot" for c in lines["gilarime"]["Aggressive"])
        and "ice_wall" not in lines["gilarime"]["Utility"]
        and "cryo_ray" not in lines["gilarime"]["Aggressive"],
        "Gilarime dumb " + ",".join(sorted(set(lines["gilarime"]["Dumb"]))) + " aggressive " + ",".join(sorted(set(lines["gilarime"]["Aggressive"]))),
    )

    # Heavy moves the quiet Aggressive line never reaches.
    never_heavy = []
    reached_heavy = []
    for sid, kit in kits.items():
        heavies = [mid for mid in kit if moves[mid].ap >= 3 and moves[mid].cat != "Passive"]
        used = set(lines[sid]["Aggressive"])
        for mid in heavies:
            (reached_heavy if mid in used else never_heavy).append(f"{sid}:{mid}")
    book.check(
        "agg_misses_heavies",
        "validape:hammer_sync" in never_heavy and "kodiax:deep_dig" in never_heavy and "cinislug:cinder_burst" in never_heavy,
        f"never={never_heavy} reached={reached_heavy}",
    )

    # Blockwyrm bank against Validape's hammer.
    wyrm = Body("blockwyrm", species["blockwyrm"], kits["blockwyrm"])
    wyrm.ap = 2
    real_ape = make_foe_from("validape", species, kits["validape"], hp=species["validape"]["hp"])
    bank_on = should_bank(wyrm, [wyrm], [real_ape], moves)
    wyrm_clicks = lines["blockwyrm"]
    book.check("wyrm_banks_vs_hammer", bank_on, f"Blockwyrm at 2 AP banks against Validape hammer={bank_on}")
    book.check(
        "wyrm_util_banks_then_stances",
        wyrm_clicks["Utility"][0] == "pass"
        and all(c == "immovable_stance" for c in wyrm_clicks["Utility"][1:])
        and "blockade" not in wyrm_clicks["Utility"]
        and "bulwark_blow" not in wyrm_clicks["Utility"],
        "Utility Blockwyrm " + ",".join(wyrm_clicks["Utility"][:4]),
    )
    book.check(
        "wyrm_def_banks_then_bulwarks",
        wyrm_clicks["Defensive"][:6] == ["pass", "bulwark_blow", "bulwark_blow"] * 2
        and "blockade" not in wyrm_clicks["Defensive"],
        "Defensive Blockwyrm " + ",".join(wyrm_clicks["Defensive"][:6]),
    )

    # Scores ignore armor, walls, and chemistry.
    ape = Body("validape", species["validape"], kits["validape"])
    ape.ap = 4
    gas = make_foe_from("deghoul", species, kits["deghoul"])
    gas_open = oracle_vs(ape, moves["hammer_sync"], gas, adv, True)
    gas.mit = 0.25
    score_open = damage_per_ap(ape, moves["hammer_sync"])
    score_armored = damage_per_ap(ape, moves["hammer_sync"])
    gas_armored = oracle_vs(ape, moves["hammer_sync"], gas, adv, True)
    gas.mit = 0
    gas.block = ("melee", 0.60)
    gas_walled = oracle_vs(ape, moves["hammer_sync"], gas, adv, True)
    book.check("armor_unread", score_open == score_armored and gas_armored < gas_open, f"hammer open {gas_open:.1f} armor25 {gas_armored:.1f} wall60 {gas_walled:.1f} dpa {score_open:.3f}")
    gas.block = None
    gas.armor_flat = 12
    gas_flat = oracle_vs(ape, moves["hammer_sync"], gas, adv, True)
    book.check("flat_armor_also_unread", score_open == damage_per_ap(ape, moves["hammer_sync"]) and gas_flat < gas_open, f"hammer flat12 {gas_flat:.1f} vs open {gas_open:.1f}")

    # Typeless inherit vs neutral, two cells.
    slug = Body("cinislug", species["cinislug"], ["baseline_shot"])
    pulse = moves["baseline_shot"]
    gas2 = make_foe_from("deghoul", species, kits["deghoul"])
    into_gas = oracle_vs(slug, pulse, gas2, adv, True)
    into_gas_neutral = oracle_vs(slug, pulse, gas2, adv, False)
    fork = make_foe_from("forkrook", species, prefix_kit(pools["forkrook"], moves))
    stake_body = Body("rattlestake", species["rattlestake"], ["baseline_shot", "stake_line"])
    # virtual stake line
    stake_line = deepcopy(moves["baseline_shot"])
    stake_line.id = "stake_line"
    stake_line.type = "Staked"
    stake_line.power = 34
    into_fork_inherit = oracle_vs(stake_body, pulse, fork, adv, True)
    into_fork_neutral = oracle_vs(stake_body, pulse, fork, adv, False)
    into_fork_typed = oracle_vs(stake_body, stake_line, fork, adv, False)
    into_gas_typed = oracle_vs(stake_body, stake_line, gas2, adv, False)
    into_gas_pulse_neutral = oracle_vs(stake_body, pulse, gas2, adv, False)
    book.check(
        "typeless_cells",
        into_gas > into_gas_neutral and into_fork_inherit > into_fork_neutral and into_fork_typed > into_fork_neutral,
        f"slug pulse into gas inherit {into_gas:.1f} neutral {into_gas_neutral:.1f}; "
        f"stake pulse into fork inherit {into_fork_inherit:.1f} neutral {into_fork_neutral:.1f}; "
        f"stake line into fork {into_fork_typed:.1f} into gas {into_gas_typed:.1f} vs neutral pulse {into_gas_pulse_neutral:.1f}",
    )
    # Aggressive scores stake line above pulse by power alone.
    dpa_pulse = damage_per_ap(stake_body, pulse)
    dpa_line = damage_per_ap(stake_body, stake_line)
    book.check("agg_prefers_stake_line_by_power", dpa_line > dpa_pulse, f"DPA pulse {dpa_pulse:.3f} stake line {dpa_line:.3f}")

    # Bulwark estimate uses Melee Attack, oracle uses Melee Defense.
    wyrm_body = Body("blockwyrm", species["blockwyrm"], kits["blockwyrm"])
    bull_est = estimate_damage(wyrm_body, moves["bulwark_blow"])
    bull_oracle = oracle_vs(wyrm_body, moves["bulwark_blow"], gas2, adv, True)
    book.check(
        "bulwark_stat_miss",
        moves["bulwark_blow"].override_atk and moves["bulwark_blow"].atk_stat == "MeleeDef" and bull_oracle > bull_est * 1.5,
        f"Bulwark estimate {bull_est:.2f} oracle vs Deghoul {bull_oracle:.2f} (MA {wyrm_body.stat('ma')} MD {wyrm_body.stat('md')})",
    )

    # Slot fixation over a short quiet-damage exchange: two Aggressive Validapes into tank slot 0 + squish slot 1.
    # Modeled as each attacker independently resolving onto slot 0.
    tank = make_foe_from("blockwyrm", species, kits["blockwyrm"])
    squish = make_foe_from("deghoul", species, kits["deghoul"])
    slot0_dmg = 0
    slot1_dmg = 0
    attackers = [Body("validape", species["validape"], kits["validape"]) for _ in range(2)]
    for _ in range(4):
        for actor in attackers:
            actor.ap = min(AP_CAP, actor.ap + AP_TURN)
            action = choose("Aggressive", actor, [actor], [tank, squish], moves, False)
            if action.kind != "move":
                continue
            move = moves[action.move_id]
            apply_spend_and_riders(actor, move, [tank, squish])
            if move.damaging and move.targeting != "AllEnemies":
                slot0_dmg += deal(actor, move, tank, adv, True)
            elif move.targeting == "AllEnemies":
                slot0_dmg += deal(actor, move, tank, adv, True)
                if squish.alive:
                    slot1_dmg += deal(actor, move, squish, adv, True)
            tick_recharge(actor)
    book.check("slot_fixation_damage", slot1_dmg == 0 and slot0_dmg > 0 and squish.hp == squish.maxhp, f"4 turns two Aggressive Validapes: slot0 {slot0_dmg} slot1 {slot1_dmg} squish hp {squish.hp}")

    # Split-board count: for each species, oracle on a resisted full front vs a weak low back.
    # Front is Blockwyrm (many types resist or not). Back is a 28 HP Deghoul.
    fixation = {p: 0 for p in ("Dumb", "Aggressive", "Defensive", "Utility")}
    fixation_other = {p: [] for p in fixation}
    fixation_n = 0
    denial_boards = {p: {"legal": 0, "picked": 0, "ignored_no_ko": 0} for p in ("Dumb", "Aggressive", "Defensive", "Utility")}
    blank_utility = {p: 0 for p in ("Utility",)}
    estimate_ratios = []
    for sid, kit in kits.items():
        actor = Body(sid, species[sid], kit)
        actor.ap = 8
        front = make_foe_from("blockwyrm", species, kits["blockwyrm"])
        back = make_foe_from("deghoul", species, kits["deghoul"])
        back.hp = 28
        foes = [front, back]
        damaging = [mid for mid in kit if moves[mid].damaging and moves[mid].ap <= 8 and moves[mid].cat != "Passive"]
        if not damaging:
            continue
        # A split board only counts when some single-target move removes strictly more from the back.
        gap = False
        for mid in damaging:
            if moves[mid].targeting == "AllEnemies":
                continue
            front_hp = oracle_vs(actor, moves[mid], front, adv, True)
            back_copy = make_foe_from("deghoul", species, kits["deghoul"])
            back_copy.hp = 28
            back_hp = oracle_vs(actor, moves[mid], back_copy, adv, True)
            knocks_back = back_hp >= back_copy.hp - 1e-6
            knocks_front = front_hp >= front.hp - 1e-6
            if back_hp >= front_hp + 8 or (knocks_back and not knocks_front):
                gap = True
                break
        if gap:
            fixation_n += 1
            for personality in fixation:
                action = choose(personality, actor, [actor], foes, moves, False)
                stuck = False
                if action.kind == "move" and moves[action.move_id].targeting == "SingleEnemy":
                    front_hp = oracle_vs(actor, moves[action.move_id], front, adv, True)
                    back_copy = make_foe_from("deghoul", species, kits["deghoul"])
                    back_copy.hp = 28
                    back_hp = oracle_vs(actor, moves[action.move_id], back_copy, adv, True)
                    if back_hp >= front_hp + 8 or (back_hp >= back_copy.hp and front_hp < front.hp):
                        fixation[personality] += 1
                        stuck = True
                if not stuck:
                    label = action.move_id if action.kind == "move" else action.kind
                    targeting = moves[label].targeting if action.kind == "move" else action.kind
                    fixation_other[personality].append(f"{sid}:{label}:{targeting}")
        # Denial boards: actor has a certain denial move, foe holds 2 AP, foe is not at 28 HP.
        denial_ids = [mid for mid in kit if moves[mid].denial_amount()[0] and moves[mid].ap <= 8]
        if denial_ids:
            healthy = make_foe_from("validape", species, kits["validape"])
            healthy.ap = 2
            for personality in denial_boards:
                denial_boards[personality]["legal"] += 1
                action = choose(personality, actor, [actor], [healthy], moves, False)
                if action.kind == "move" and action.move_id in denial_ids:
                    denial_boards[personality]["picked"] += 1
                else:
                    ko = False
                    if action.kind == "move" and moves[action.move_id].damaging:
                        ko = oracle_vs(actor, moves[action.move_id], healthy, adv, True) >= healthy.hp
                    if not ko:
                        denial_boards[personality]["ignored_no_ko"] += 1
        # Estimate / oracle on the move Aggressive actually picks, neutral-ish full foe.
        actor.ap = 8
        probe = make_foe_from("gilarime", species, prefix_kit(pools["gilarime"], moves))
        action = choose("Aggressive", actor, [actor], [probe], moves, False)
        if action.kind == "move" and moves[action.move_id].damaging:
            est = estimate_damage(actor, moves[action.move_id])
            ora = oracle_vs(actor, moves[action.move_id], probe, adv, True)
            if ora > 0:
                estimate_ratios.append((sid, action.move_id, est, ora, est / ora))

    book.check("split_boards_exist", fixation_n >= 10, f"split boards={fixation_n} fixation={fixation}")
    book.check(
        "split_aggressive_mostly_stuck",
        fixation["Aggressive"] >= int(0.7 * fixation_n),
        f"Aggressive slot-fixation {fixation['Aggressive']}/{fixation_n}",
    )
    book.check(
        "split_aggressive_exception_is_aoe",
        fixation_other["Aggressive"] == ["forkrook:fork_split:AllEnemies"],
        f"Aggressive non-fixation clicks {fixation_other['Aggressive']}",
    )
    book.check(
        "split_defensive_exceptions",
        fixation_other["Defensive"] == [
            "humpacked:compression_trade:Self",
            "deghoul:slipstream:Self",
            "forkrook:fork_split:AllEnemies",
            "balloom:slipstream:Self",
        ],
        f"Defensive non-fixation clicks {fixation_other['Defensive']}",
    )
    book.check(
        "denial_ignored_by_aggressive",
        denial_boards["Aggressive"]["picked"] == 0 and denial_boards["Aggressive"]["legal"] >= 3,
        f"denial boards {denial_boards}",
    )
    ratios = [row[4] for row in estimate_ratios]
    mean_ratio = sum(ratios) / len(ratios) if ratios else 0
    book.check(
        "estimate_not_oracle",
        len(ratios) >= 10 and (mean_ratio < 0.85 or mean_ratio > 1.15),
        f"mean estimate/oracle on Aggressive picks vs Gilarime={mean_ratio:.3f} n={len(ratios)}",
    )

    # Status already applied: Utility re-clicks a status move into a foe who already has it.
    reapply = []
    for sid, kit in kits.items():
        status_moves = [mid for mid in kit if moves[mid].statuses and moves[mid].targeting != "Self"]
        if not status_moves:
            continue
        actor = Body(sid, species[sid], kit)
        actor.ap = 8
        foe = make_foe_from("validape", species, kits["validape"])
        foe.statuses.add(moves[status_moves[0]].statuses[0].split(":")[0])
        action = choose("Utility", actor, [actor], [foe], moves, False)
        if action.kind == "move" and action.move_id in status_moves:
            reapply.append(sid + ":" + action.move_id)
    book.check(
        "status_reapply",
        "keyper:cipher_shot" in reapply and len(reapply) >= 2,
        f"utility re-clicked an already-present foe status on {reapply or 'none'}",
    )

    # Once-per-battle held? Kitsauto opens Feint with no incoming attack. Already asserted.

    # Tempo fight: Aggressive Validape vs Aggressive Kitsauto.
    left = Body("validape", species["validape"], kits["validape"])
    right = Body("kitsauto", species["kitsauto"], kits["kitsauto"])
    tempo = fight(left, right, moves, adv, True, 8, "Aggressive", "Aggressive")
    hammer_landed = any(click["click"] == "hammer_sync" and click.get("result") != "no_ap" for turn in tempo for click in turn["clicks"])
    book.check(
        "tempo_ai_never_hammers",
        not hammer_landed and (not left.alive or not right.alive or tempo[-1]["turn"] == 8),
        f"turns={len(tempo)} validape_hp={left.hp} kitsauto_hp={right.hp} hammer={hammer_landed} last={tempo[-1]['clicks'] if tempo else None}",
    )

    # Human Validape passes until 4 AP, then hammers. Kitsauto stays Aggressive.
    human = Body("validape", species["validape"], kits["validape"])
    fox = Body("kitsauto", species["kitsauto"], kits["kitsauto"])

    def human_hammer(body, foes):
        if body.ap >= 4 and "hammer_sync" not in body.recharge:
            return Action("move", "hammer_sync")
        return Action("pass")

    human_trace = fight(human, fox, moves, adv, True, 8, human_hammer, "Aggressive")
    human_hammer_turn = next((turn["turn"] for turn in human_trace if any(c["click"] == "hammer_sync" and c.get("dmg", 0) >= 0 and c.get("result") != "no_ap" for c in turn["clicks"])), None)
    book.check(
        "human_can_hammer",
        human_hammer_turn == 2,
        f"human hammer turn={human_hammer_turn} validape_hp={human.hp} kitsauto_hp={fox.hp}",
    )

    # Faster skim vs Cofi Aggressive and vs Cofi Utility.
    def always(move_id):
        def policy(body, foes):
            return Action("move", move_id)
        return policy

    cofi_a = Body("cofi_lootwak", species["cofi_lootwak"], cofi_kit)
    fox_a = Body("kitsauto", species["kitsauto"], ["packet_skim"])
    skim_vs_agg = fight(fox_a, cofi_a, moves, adv, True, 8, always("packet_skim"), "Aggressive")
    agg_beams_landed = sum(1 for turn in skim_vs_agg for c in turn["clicks"] if c["who"] == "cofi_lootwak" and c["click"] == "civet_beam" and c.get("result") != "no_ap" and "dmg" in c)
    agg_beams_fizzle = sum(1 for turn in skim_vs_agg for c in turn["clicks"] if c["who"] == "cofi_lootwak" and c.get("result") == "no_ap")
    cofi_u = Body("cofi_lootwak", species["cofi_lootwak"], cofi_kit)
    fox_u = Body("kitsauto", species["kitsauto"], ["packet_skim"])
    skim_vs_util = fight(fox_u, cofi_u, moves, adv, True, 16, always("packet_skim"), "Utility")
    taxes = [turn["turn"] for turn in skim_vs_util for c in turn["clicks"] if c["who"] == "cofi_lootwak" and c["click"] == "grind_tax" and c.get("result") != "no_ap" and "dmg" in c]
    book.check(
        "skim_alternates_aggressive_beam",
        agg_beams_landed == 4 and agg_beams_fizzle == 4,
        f"landed beams={agg_beams_landed} fizzles={agg_beams_fizzle}",
    )
    book.check(
        "skim_erases_utility_tax",
        taxes == [],
        f"utility tax turns under a faster skim={taxes}",
    )

    # Keyper utility vs Validape aggressive: does the lock happen?
    key = Body("keyper", species["keyper"], kits["keyper"])
    ape2 = Body("validape", species["validape"], kits["validape"])
    key_trace = fight(key, ape2, moves, adv, True, 8, "Utility", "Aggressive")
    key_clicks = [c["click"] for turn in key_trace for c in turn["clicks"] if c["who"] == "keyper"]
    ape_aps = []
    # Reconstruct decision AP from the trace is hard; re-read by a dedicated calendar.
    book.check("keyper_fight_clicks", key_clicks[:3] == ["cipher_shot"] * min(3, len(key_clicks)) and "memory_leak" not in key_clicks, f"Keyper clicks {key_clicks}")

    # Scripted leak every turn (a human, or a fixed policy) vs Aggressive Validape.
    leaker = Body("keyper", species["keyper"], ["memory_leak"])
    victim = Body("validape", species["validape"], kits["validape"])
    leak_trace = fight(leaker, victim, moves, adv, True, 8, always("memory_leak"), "Aggressive")
    victim_actions = [c for turn in leak_trace for c in turn["clicks"] if c["who"] == "validape"]
    hammer_under_leak = any(c["click"] == "hammer_sync" and c.get("result") != "no_ap" for c in victim_actions)
    book.check(
        "scripted_leak_stops_hammer",
        not hammer_under_leak,
        f"victim clicks {[c['click'] + (':fizzle' if c.get('result')=='no_ap' else '') for c in victim_actions]}",
    )

    # Proposal stack quiet lines for the species the stack actually retunes.
    stack_moves = deepcopy(moves)
    stack_moves["cinder_burst"].ap = 2
    stack_moves["memory_leak"].recharge = 2
    for effect in stack_moves["transaction_tax"].utility:
        if effect["op"] == "StealAP":
            effect["op"] = "DrainAP"
    stack_moves["transaction_tax"].utility_raw = stack_moves["transaction_tax"].utility_raw.replace("StealAP:", "DrainAP:")
    stack_moves["blade_flurry"].power = 10
    stack_moves["volatile_shot"].utility = [e for e in stack_moves["volatile_shot"].utility if e["op"] != "NextMoveAPSurcharge"]
    stack_moves["bulwark_blow"].ap = 3  # integration default: keep 3
    stake_line_move = deepcopy(stack_moves["baseline_shot"])
    stake_line_move.id = "stake_line"
    stake_line_move.name = "Stake Line"
    stake_line_move.type = "Staked"
    stake_line_move.power = 34
    stack_moves["stake_line"] = stake_line_move

    def swapped_kit(sid: str) -> list[str]:
        pool = dict(pools[sid])
        if sid == "deghoul":
            pool["LearnableMoves"] = pool["LearnableMoves"].replace("slipstream", "fee_collector")
        if sid == "rattlestake":
            pool["LearnableMoves"] = pool["LearnableMoves"].replace("baseline_shot", "stake_line")
        if sid == "kodiax":
            pool["LearnableMoves"] = pool["LearnableMoves"].replace("baseline_slam", "extraction_hit")
        return prefix_kit(pool, stack_moves)

    stack_lines = {}
    for sid in ("cinislug", "kitsauto", "deghoul", "keyper", "blockwyrm", "validape", "rattlestake", "kodiax", "gilarime"):
        stack_lines[sid] = {}
        kit = swapped_kit(sid)
        for personality in ("Aggressive", "Utility"):
            stack_lines[sid][personality] = quiet_line(personality, sid, kit, species, stack_moves, fresh_hammer(), 12)

    book.check(
        "stack_cinder_aggressive",
        all(c == "cinder_burst" for c in stack_lines["cinislug"]["Aggressive"]),
        "stack Aggressive Cinislug " + ",".join(sorted(set(stack_lines["cinislug"]["Aggressive"]))),
    )
    book.check(
        "live_cinder_aggressive_never",
        "cinder_burst" not in lines["cinislug"]["Aggressive"],
        "live Aggressive Cinislug " + ",".join(sorted(set(lines["cinislug"]["Aggressive"]))),
    )
    book.check(
        "stack_cinder_utility_switches",
        all(c == "cinder_burst" for c in stack_lines["cinislug"]["Utility"]),
        "stack Utility Cinislug " + ",".join(sorted(set(stack_lines["cinislug"]["Utility"]))),
    )
    book.check(
        "stack_keyper_still_ciphers",
        stack_lines["keyper"]["Utility"].count("memory_leak") == 0
        and all(c == "cipher_shot" for c in stack_lines["keyper"]["Utility"]),
        "stack Utility Keyper " + ",".join(sorted(set(stack_lines["keyper"]["Utility"]))),
    )
    book.check(
        "stack_tax_still_unseen_by_score",
        all(c == "transaction_tax" for c in stack_lines["deghoul"]["Utility"]) and "StealAP" not in ai_blob,
        "Deghoul still taxes after the opcode rename because the score never read the opcode",
    )
    book.check(
        "stack_blade_still_spammed",
        all(c == "blade_flurry" for c in stack_lines["kitsauto"]["Aggressive"]),
        "stack Aggressive Kitsauto still blades at power 10",
    )
    book.check(
        "stack_wyrm_utility_still_stance",
        stack_lines["blockwyrm"]["Utility"][0] == "pass"
        and all(c == "immovable_stance" for c in stack_lines["blockwyrm"]["Utility"][1:])
        and "bulwark_blow" not in stack_lines["blockwyrm"]["Utility"]
        and "blockade" not in stack_lines["blockwyrm"]["Utility"],
        "stack Utility Blockwyrm " + ",".join(stack_lines["blockwyrm"]["Utility"][:4]),
    )
    book.check(
        "stack_rattle_stake_line",
        "stake_line" in stack_lines["rattlestake"]["Aggressive"],
        "stack Aggressive Rattlestake " + ",".join(sorted(set(stack_lines["rattlestake"]["Aggressive"]))),
    )

    # Greedy-sim pass exists; shipped Aggressive has no pass on the Validape quiet line.
    book.check(
        "shipped_validape_never_passes",
        "pass" not in lines["validape"]["Aggressive"] and "pass" not in lines["validape"]["Utility"],
        f"agg={set(lines['validape']['Aggressive'])} util={set(lines['validape']['Utility'])}",
    )

    # Defensive double-swap: two low bodies, one bench, same index.
    low_a = Body("gilarime", species["gilarime"], prefix_kit(pools["gilarime"], moves))
    low_b = Body("gilarime", species["gilarime"], prefix_kit(pools["gilarime"], moves))
    low_a.hp = max(1, int(low_a.maxhp * 0.15))
    low_b.hp = max(1, int(low_b.maxhp * 0.15))
    low_a.ap = 4
    low_b.ap = 4
    foe_ok = make_foe_from("deghoul", species, kits["deghoul"])
    swap_a = choose("Defensive", low_a, [low_a, low_b], [foe_ok], moves, bench=True)
    swap_b = choose("Defensive", low_b, [low_a, low_b], [foe_ok], moves, bench=True)
    book.check(
        "double_swap",
        swap_a.kind == "swap" and swap_b.kind == "swap",
        f"two Defensive bodies at 15% HP both chose {swap_a.kind}/{swap_b.kind}",
    )

    # Cipher / stage cap: Bean Sip score at speed +6 equals score at +0.
    clean = make_foe_from("validape", species, kits["validape"])
    uncapped = Body("cofi_lootwak", cofi, cofi_kit)
    uncapped.ap = 2
    capped = Body("cofi_lootwak", cofi, cofi_kit)
    capped.ap = 2
    capped.stages["spd"] = 6
    sip_open = utility_score(uncapped, Action("move", "bean_sip"), [uncapped], [clean], moves)
    sip_capped = utility_score(capped, Action("move", "bean_sip"), [capped], [clean], moves)
    book.check("sip_ignores_cap", abs(sip_open - 39) < 1e-6 and abs(sip_capped - sip_open) < 1e-6, f"Sip at Speed +0 scores {sip_open}, at +6 scores {sip_capped}")

    # Pinned utility scores the quiet lines are explained by. Kindling uses the foe's real max HP.
    score_foe = fresh_hammer()
    slug = Body("cinislug", species["cinislug"], kits["cinislug"])
    slug.ap = 6
    hype_score = utility_score(slug, Action("move", "hype_burst"), [slug], [score_foe], moves)
    cinder3 = utility_score(slug, Action("move", "cinder_burst"), [slug], [score_foe], moves)
    cinder2 = utility_score(slug, Action("move", "cinder_burst"), [slug], [score_foe], stack_moves)
    key_body = Body("keyper", species["keyper"], kits["keyper"])
    key_body.ap = 6
    cipher_score = utility_score(key_body, Action("move", "cipher_shot"), [key_body], [score_foe], moves)
    leak_score = utility_score(key_body, Action("move", "memory_leak"), [key_body], [score_foe], moves)
    rotation_score = utility_score(key_body, Action("move", "key_rotation"), [key_body], [score_foe], moves)
    book.check(
        "cinder_scores",
        cinder3 < hype_score < cinder2,
        f"hype {hype_score:.3f} cinder@3 {cinder3:.3f} cinder@2 {cinder2:.3f} (Kindling chip uses max HP {score_foe.maxhp})",
    )
    book.check(
        "keyper_scores",
        cipher_score > rotation_score > leak_score,
        f"cipher {cipher_score:.3f} rotation {rotation_score:.3f} leak {leak_score:.3f}",
    )

    # Random legal-list dominated share, cheap definition: a move is HP-dominated when another
    # legal move costs <=, removes >= 8 more HP from the slot-0 foe, and the chosen move does not KO.
    dominated_actions = 0
    legal_total = 0
    for sid, kit in kits.items():
        actor = Body(sid, species[sid], kit)
        actor.ap = 4
        foe = make_foe_from("gilarime", species, prefix_kit(pools["gilarime"], moves))
        legal = legal_actions(actor, [actor], [foe], moves, bench=True)
        scored = []
        for action in legal:
            if action.kind != "move":
                scored.append((action, 0.0, 99))
                continue
            move = moves[action.move_id]
            hp = oracle_action(actor, move, [foe], adv, True) if move.damaging else 0.0
            scored.append((action, hp, move.ap))
        legal_total += len(scored)
        for action, hp, cost in scored:
            for other, ohp, oc in scored:
                if other is action:
                    continue
                if oc <= cost and ohp >= hp + 8 and ohp < foe.hp:
                    dominated_actions += 1
                    break
                if oc <= cost and ohp >= foe.hp > hp:
                    dominated_actions += 1
                    break
    random_share = dominated_actions / legal_total if legal_total else 0
    book.check("random_dominated_share", 0.05 <= random_share <= 0.8, f"random legal actions HP-dominated {dominated_actions}/{legal_total} = {random_share:.3f}")

    # Move counts the chooser cannot see.
    opcode_counts = {}
    for move in moves.values():
        for effect in move.utility:
            opcode_counts[effect["op"]] = opcode_counts.get(effect["op"], 0) + 1
    overrides = [m.id for m in moves.values() if m.override_atk]
    conds = [m.id for m in moves.values() if m.cond_mult > 1 or m.scale_bonus]
    book.check("override_moves_exist", "bulwark_blow" in overrides and len(overrides) >= 1, f"attack-stat overrides={overrides}")
    book.check("conditional_moves_exist", "seam_cut" in conds and "hammer_sync" in conds, f"conditional damage rows={len(conds)}")
    book.check("move_row_count", len(moves) >= 200, f"move rows={len(moves)}")

    results = {
        "moves": len(moves),
        "species_kits": {sid: kits[sid] for sid in kits},
        "quiet_lines": lines,
        "cofi_lines": cofi_lines,
        "cofi_both_legal": spot_picks,
        "never_heavy_aggressive": never_heavy,
        "reached_heavy_aggressive": reached_heavy,
        "blockade_bank_vs_validape": bank_on,
        "hammer_open": gas_open,
        "hammer_armor25": gas_armored,
        "hammer_wall60": gas_walled,
        "hammer_flat12": gas_flat,
        "typeless": {
            "slug_pulse_gas_inherit": into_gas,
            "slug_pulse_gas_neutral": into_gas_neutral,
            "stake_pulse_fork_inherit": into_fork_inherit,
            "stake_pulse_fork_neutral": into_fork_neutral,
            "stake_line_fork": into_fork_typed,
            "stake_line_gas": into_gas_typed,
            "stake_pulse_gas_neutral": into_gas_pulse_neutral,
        },
        "bulwark_estimate": bull_est,
        "bulwark_oracle_vs_deghoul": bull_oracle,
        "slot_damage": {"slot0": slot0_dmg, "slot1": slot1_dmg},
        "fixation": fixation,
        "fixation_n": fixation_n,
        "denial_boards": denial_boards,
        "estimate_ratio_mean": mean_ratio,
        "estimate_ratios": [{"sid": a, "move": b, "est": c, "oracle": d, "ratio": e} for a, b, c, d, e in estimate_ratios],
        "status_reapply": reapply,
        "tempo_ai": {"turns": len(tempo), "validape_hp": left.hp, "kitsauto_hp": right.hp, "hammer": hammer_landed, "trace": tempo},
        "tempo_human": {"hammer_turn": human_hammer_turn, "validape_hp": human.hp, "kitsauto_hp": fox.hp, "trace": human_trace},
        "skim_vs_agg": {"landed": agg_beams_landed, "fizzle": agg_beams_fizzle, "trace": skim_vs_agg},
        "skim_vs_util_tax_turns": taxes,
        "keyper_fight_clicks": key_clicks,
        "leak_vs_validape": [c["click"] for c in victim_actions],
        "stack_lines": stack_lines,
        "stack_kits": {sid: swapped_kit(sid) for sid in stack_lines},
        "random_dominated": {"dominated": dominated_actions, "legal": legal_total, "share": random_share},
        "opcode_counts": opcode_counts,
        "overrides": overrides,
        "double_swap": [swap_a.kind, swap_b.kind],
        "pinned_scores": {
            "hype": hype_score,
            "cinder3": cinder3,
            "cinder2": cinder2,
            "cipher": cipher_score,
            "leak": leak_score,
            "rotation": rotation_score,
            "sip_open": sip_open,
            "sip_capped": sip_capped,
            "kindling_maxhp": score_foe.maxhp,
        },
        "species_speed": {sid: species[sid]["spd"] for sid in species},
        "species_hp": {sid: species[sid]["hp"] for sid in species},
        "species_type": {sid: TYPES[species[sid]["primary"]] for sid in species},
    }
    out_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    passed, failed = book.summary()
    print(f"SUMMARY {passed} PASS / {failed} FAIL")
    print(f"WROTE {out_path}")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
