#!/usr/bin/env python3
"""Combined 2v2 sim for the proposal stack.

Extends ap_denial_2v2_sim.py, which already mirrors BlockWindow walls.
Two older sims still diverge from this snapshot and are not used for rates:
tempo_2v2_sim.py and walls_pvp_v2_sim.py parse MitigationWindow and do not
mention BlockWindow. armor_2v2_sim.py is the flat-armor formula source; its
battle loop is not the live wall engine. This file is.

Fixes applied to BOTH the baseline and the stack, because the denial sim
omits them and the snapshot engine does not:
  - Authored StageChanges and ChangeStage / Lifesteal / DrainAP riders resolve
    on a confirmed hit. Once-per-target is honoured.
  - AllEnemies hits every living foe. The cost is paid once.
  - Separate melee and ranged stages. A Melee Atk buff does not raise Ranged Atk.
  - End step order: passive ticks (Patience, Streamlined's stage), then Kindling,
    then chemistry and status ticks (Rugged, status duration), then block,
    armor, and recharge. Recharge also ticks on the bench.
  - Fee Collector drains the first opposing actor by 1 when that actor has
    more AP than the holder. It is not in a baseline kit.

The stack overlay is data only: move rows, three pool swaps, typeless
neutrality, tax-as-drain, and the narrowed chemistry stages. STAB stays off.
Armor flat 12 / 16 is on the card and is not in a default kit (Q12 and Q13
are no). A formula cell still measures it.

Usage:
  python3 stack_2v2_sim.py [snapshot] [sims_dir] [out_json]
"""
from __future__ import annotations

import json
import math
import os
import random
import sys
from collections import Counter, defaultdict
from pathlib import Path

SIMS = Path(sys.argv[2] if len(sys.argv) > 2 else os.environ.get("CHAINBOUND_SIMS", "/tmp/cb_work/sims"))
ROOT = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("CHAINBOUND_SNAPSHOT", "/tmp/cb_work/snapshot")
OUT = Path(sys.argv[3] if len(sys.argv) > 3 else "/tmp/cb_work/stack_results.json")
os.environ["CHAINBOUND_SNAPSHOT"] = str(ROOT)
sys.path.insert(0, str(SIMS))
sys.argv = [sys.argv[0], str(ROOT)]

import ap_denial_2v2_sim as sim  # noqa: E402

SEED = 20261023
STAT_ATTR = {
    "Speed": "spd_stage",
    "MeleeAtk": "ma_stage",
    "RangedAtk": "ra_stage",
    "MeleeDef": "md_stage",
    "RangedDef": "rd_stage",
    "Accuracy": "acc_stage",
    "Evasion": "eva_stage",
}


_orig_init = sim.Mon.__init__


def _init(self, sid, moves, ap=0):
    _orig_init(self, sid, moves, ap)
    self.ma_stage = 0
    self.ra_stage = 0
    self.md_stage = 0
    self.rd_stage = 0
    self.did_damage = False
    self.stream_used = False
    self.once = set()
    self.spent = 0
    self.denied = 0


def _stat(self, key):
    attr = {"spd": "spd_stage", "ma": "ma_stage", "ra": "ra_stage", "md": "md_stage", "rd": "rd_stage"}.get(key)
    stage = getattr(self, attr, 0) if attr else 0
    return max(1, int(self.base[key] * sim.smult(stage)))


def _buffed(self):
    return self.ma_stage > 0 or self.ra_stage > 0 or self.md_stage > 0 or self.spd_stage > 0 or any(
        sim.STATUSES.get(s["id"], {}).get("IsBuff") == "true" for s in self.statuses
    )


sim.Mon.__init__ = _init
sim.Mon.stat = _stat
sim.Mon.buffed = _buffed


def clone_moves():
    return {k: dict(v) for k, v in sim.DATA["moves"].items()}


BASE_MOVES = clone_moves()


def stack_moves():
    moves = clone_moves()
    moves["memory_leak"]["RechargeTurns"] = "2"
    tax = moves["transaction_tax"]["UtilityEffects"].replace("StealAP:", "DrainAP:")
    moves["transaction_tax"]["UtilityEffects"] = tax
    moves["cinder_burst"]["APCost"] = "2"
    moves["hype_burst"]["UtilityEffects"] = moves["hype_burst"]["UtilityEffects"].replace("MeleeAtk:", "RangedAtk:")
    moves["patience_passive"]["UtilityEffects"] = moves["patience_passive"]["UtilityEffects"].replace(":Self:6:", ":Self:3:")
    util = moves["streamlined_flow"]["UtilityEffects"].replace(":Self:-6:", ":Self:-1:")
    moves["streamlined_flow"]["UtilityEffects"] = util
    moves["streamlined_flow"]["TriggerOnce"] = "true"
    moves["bulwark_blow"]["APCost"] = "2"
    moves["blade_flurry"]["Power"] = "10"
    moves["cryo_ray"]["StageChanges"] = "Speed:-2:1.0:false"
    moves["volatile_shot"]["UtilityEffects"] = ""
    moves["ore_strike"]["UtilityEffects"] = "ChangeStage:MeleeDef:-1:25%:SingleEnemy:-6:false:ore_strike"
    moves["ore_strike"]["MoveEffectsOncePerTarget"] = "true"
    moves["yield_spike"]["UtilityEffects"] = "LifestealPercent:20%:yield_spike"
    moves["stake_strike"]["UtilityEffects"] = "LifestealPercent:15%:stake_strike"
    moves["combustive_shift"]["UtilityEffects"] = "DrainAP:1:50%:SingleEnemy:combustive_shift"
    moves["combustive_shift"]["MoveEffectsOncePerTarget"] = "true"
    moves["hype_train"]["StatusEffects"] = "Surging:1:1.0:Self"
    for mid in ("fork_split", "shard_scatter", "chain_fork"):
        moves[mid]["StageChanges"] = "Speed:-1:0.2:false"
        moves[mid]["MoveEffectsOncePerTarget"] = "true"
    moves["extraction_hit"]["Rarity"] = "Common"
    moves["fee_collector"]["Rarity"] = "Common"
    # Virtual Staked common. There is no Staked common in the catalog to fill.
    moves["stake_line"] = dict(moves["baseline_shot"])
    moves["stake_line"].update(
        MoveId="stake_line",
        DisplayName="Stake Line",
        BaseType="Staked",
        Rarity="Common",
        Category="DamagingRanged",
        Power="34",
        APCost="2",
        Accuracy="95",
        HitMin="1",
        HitMax="1",
        Targeting="SingleEnemy",
        Tags="DMG",
        UtilityEffects="",
        StatusEffects="",
        StageChanges="",
        Description="Ranged Staked hit.",
    )
    # Armor numbers live on the card. Default kits do not equip them.
    moves["guard_posture"]["UtilityEffects"] = "ArmorFlat:12:1:2:armor_guard_posture"
    moves["guard_stance"]["UtilityEffects"] = "ArmorFlat:16:1:2:armor_guard_stance"
    moves["guard_stance"]["StageChanges"] = ""
    if "unstable_code" in moves:
        moves["unstable_code"]["UtilityEffects"] = moves["unstable_code"]["UtilityEffects"].replace(":Self:6", ":Self:3")
    return moves


STACK_MOVES = stack_moves()


KITS = {
    "validape": ["hammer_sync", "baseline_striker", "uplink", "hub_heal"],
    "deghoul": ["transaction_tax", "gas_bump", "volatile_shot", "slipstream"],
    "blockwyrm": ["immutable_ledger", "blockade", "bulwark_blow", "stone_barrage"],
    "keyper": ["memory_leak", "cipher_shot", "cipher_burst", "system_reset"],
    "cinislug": ["ember_wall", "cinder_burst", "kindling", "hype_burst"],
    "gilarime": ["cryo_ray", "frost_shot", "ice_wall", "frost_shards"],
    "forkrook": ["fork_split", "chain_fork", "rapid_strike", "baseline_shot"],
    "kodiax": ["ore_strike", "deep_dig", "pickaxe_flurry", "baseline_slam"],
    "hopoch": ["yield_spike", "stake_strike", "patience_passive", "baseline_striker"],
    "humpacked": ["streamlined_flow", "lean_hit", "barrier_wall", "zipped_strikes"],
    "kitsauto": ["fox_feint", "blade_flurry", "packet_skim", "multi_thread_shot"],
    "crackett": ["shard_partition", "seam_cut", "parallel_passive", "baseline_jab"],
    "kreechank": ["chaos_strike", "error_correction", "defense_corrupt", "baseline_striker"],
    "rattlestake": ["ap_reserve", "compound_guard", "baseline_shot", "baseline_jab"],
    "balloom": ["fuel_surge", "volatile_shot", "gas_bump", "baseline_slam"],
    "minefolk": ["deep_reap", "pickaxe_flurry", "baseline_shot", "exploit_window"],
    "marrowhead": ["field_split", "partition_shot", "precision_strike", "baseline_snipe"],
}

# Face species used for the type spread. Partner is Humpacked, except Humpacked
# itself, which brings Rattlestake so the face is the only Compressed body.
TYPE_FACES = [
    ("Node", "validape"),
    ("Gas", "deghoul"),
    ("Block", "blockwyrm"),
    ("Encrypted", "keyper"),
    ("Burned", "cinislug"),
    ("Frozen", "gilarime"),
    ("Forked", "forkrook"),
    ("Mined", "kodiax"),
    ("Staked", "hopoch"),
    ("Compressed", "humpacked"),
    ("Bot", "kitsauto"),
    ("Shard", "crackett"),
    ("Glitched", "kreechank"),
]

ARCHETYPES = {
    "heavy": ("kodiax", "hopoch"),
    "tempo": ("crackett", "deghoul"),
    "denial": ("keyper", "kitsauto"),
    "walls": ("blockwyrm", "cinislug"),
    "stake": ("hopoch", "rattlestake"),
    "fork": ("forkrook", "marrowhead"),
    "frozen": ("gilarime", "humpacked"),
    "gas": ("balloom", "deghoul"),
}


def use_moves(stack: bool):
    sim.MOVES = STACK_MOVES if stack else BASE_MOVES


def kit_of(sid: str, stack: bool) -> list[str]:
    moves = list(KITS[sid])
    if stack and sid == "kodiax":
        moves = ["extraction_hit" if m == "baseline_slam" else m for m in moves]
    if stack and sid == "deghoul":
        moves = ["fee_collector" if m == "slipstream" else m for m in moves]
    if stack and sid == "rattlestake":
        moves = ["stake_line" if m == "baseline_shot" else m for m in moves]
    missing = [m for m in moves if m not in (STACK_MOVES if stack else BASE_MOVES)]
    if missing:
        raise KeyError(f"{sid} missing {missing}")
    return moves


def parse_utils(raw: str):
    return [p.split(":") for p in filter(None, (raw or "").split("|"))]


def parse_stages(raw: str):
    out = []
    for part in filter(None, (raw or "").split("|")):
        bits = part.split(":")
        if len(bits) < 3:
            continue
        try:
            out.append((bits[0], int(float(bits[1])), float(bits[2]), len(bits) > 3 and bits[3].lower() == "true"))
        except ValueError:
            continue
    return out


class StackBattle(sim.Battle):
    def __init__(self, teams, policies, rng, opts):
        super().__init__(teams, policies, rng, opts)
        self.spent = 0
        self.denied = 0
        self.block_actor_turns = 0
        self.actor_turns = 0
        self.picks = Counter()
        self.species_picks = Counter()
        self.capped = False

    def run(self):
        while self.turn < self.max_turns:
            self.turn += 1
            self._start()
            if self._done():
                break
            actions = []
            for t in (0, 1):
                for slot_i, idx in enumerate(list(self.active[t])):
                    mon = self.teams[t][idx]
                    if not mon.alive:
                        continue
                    act = self.policies[t](self, mon, slot_i) or ("pass",)
                    actions.append((mon, act))
            actions.sort(key=lambda item: (
                0 if item[1][0] == "swap" else (1 if item[1][0] == "move" and item[1][1] in item[0].moves and item[0].moves[item[1][1]]["prior"] else 2),
                -self.speed(item[0]),
                self.rng.random(),
            ))
            self._fee(actions)
            for mon, act in actions:
                if not mon.alive or not mon.active:
                    continue
                self._resolve(mon, act)
                self._autoswap()
                if self._done():
                    break
            if self._done():
                break
            self._end()
            self._autoswap()
            if self._done():
                break
        if self.winner is None:
            self.winner = self._hp_winner()
            self.capped = True
            self.end_turn = self.max_turns
        return self

    def _start(self):
        for t in (0, 1):
            for mon in self.actives(t):
                mon.did_damage = False
        super()._start()
        for t in (0, 1):
            for mon in self.actives(t):
                self.actor_turns += 1
                if mon.block:
                    self.block_actor_turns += 1

    def _fee(self, actions):
        acted = False
        for t in (0, 1):
            for mon in self.actives(t):
                if "fee_collector" not in mon.passive_ids:
                    continue
                first = next((actor for actor, _act in actions if actor.team != mon.team and actor.alive), None)
                if first is not None and first.ap > mon.ap:
                    removed = self._enemy_drain(first, 1, "fee")
                    self.denied += removed
                    acted = True
        return acted

    def add_stage(self, mon, stat, delta, cap):
        if delta > 0 and self.opts.get("stack") and mon.has("CipherLocked"):
            return 0
        attr = STAT_ATTR.get(stat)
        if attr is None:
            return 0
        cur = getattr(mon, attr)
        if delta > 0:
            nxt = min(cap if cap and cap > 0 else 6, cur + delta)
        else:
            floor = cap if cap is not None and cap < 0 else -6
            nxt = max(floor, cur + delta)
        setattr(mon, attr, nxt)
        return nxt - cur

    def _apply_stages(self, att, de, mv, once_key):
        once = (mv.get("once") or "").lower() == "true" or mv["id"] in att.once and False
        once = str(sim.MOVES[mv["id"]].get("MoveEffectsOncePerTarget", "")).lower() == "true"
        for stat, delta, chance, on_self in parse_stages(mv.get("stages") or ""):
            key = (mv["id"], de.sid, "stage", stat)
            if once and key in att.once:
                continue
            if self.rng.random() >= chance:
                if once:
                    att.once.add(key)
                continue
            who = att if on_self else de
            cap = 6 if delta > 0 else -6
            self.add_stage(who, stat, delta, cap)
            if once:
                att.once.add(key)
        for bits in parse_utils(mv.get("util") or ""):
            op = bits[0]
            if op == "ChangeStage" and len(bits) >= 5:
                stat, delta, chance = bits[1], int(float(bits[2])), _chance(bits[3])
                on_self = bits[4] == "Self"
                cap = int(float(bits[5])) if len(bits) > 5 and bits[5] not in ("", "false", "true") else (6 if delta > 0 else -6)
                key = (mv["id"], (att.sid if on_self else de.sid), "util", stat)
                if once and key in att.once:
                    continue
                if self.rng.random() >= chance:
                    if once:
                        att.once.add(key)
                    continue
                self.add_stage(att if on_self else de, stat, delta, cap)
                if once:
                    att.once.add(key)
            elif op == "LifestealPercent":
                continue
            elif op == "DrainAP" and len(bits) >= 3:
                key = (mv["id"], de.sid, "drain")
                if key in att.once:
                    continue
                amount = int(float(bits[1]))
                chance = _chance(bits[2])
                att.once.add(key)
                if self.rng.random() < chance:
                    self.denied += self._enemy_drain(de, amount, mv["id"])

    def _resolve(self, mon, act):
        kind = act[0]
        if self._leaked_tax(mon):
            return
        if kind == "pass":
            self.trace.append(dict(turn=self.turn, ev="pass", who=mon.sid, ap=mon.ap))
            return
        if kind == "swap":
            self._swap(mon, act[1])
            return
        if kind != "move":
            return
        mid = act[1]
        if mid not in mon.moves:
            return
        mv = mon.moves[mid]
        if mon.recharge.get(mid, 0) > 0:
            self.trace.append(dict(turn=self.turn, ev="recharge", who=mon.sid, move=mid))
            return
        cost = mv["ap"] + mon.surcharge
        if "streamlined_flow" in mon.passive_ids and mon.hp > 0.7 * mon.maxhp and mv["ap"] >= 1 and cost > 0:
            cost = max(1, cost - 1)
        if mon.ap < cost:
            self.trace.append(dict(turn=self.turn, ev="no_ap", who=mon.sid, move=mid, ap=mon.ap, cost=cost))
            return
        mon.ap -= cost
        self.spent += cost
        mon.spent += cost
        mon.surcharge = 0
        if mv["ap"] >= 4 and mon.team == 0 and self.hammer_turn is None and mv["damaging"]:
            self.hammer_turn = self.turn
        if mv["rc"] >= 100:
            mon.recharge[mid] = 999
        elif mv["rc"] > 0:
            mon.recharge[mid] = mv["rc"]
        self._stream_drop(mon)
        target = self._pick_target(mon, mv, act)
        self.picks[mid] += 1
        self.species_picks[(mon.sid, mid)] += 1
        self.trace.append(dict(turn=self.turn, ev="cast", who=mon.sid, move=mid, ap=mon.ap, tgt=getattr(target, "sid", None)))
        if mid == "memory_leak" and target is not None:
            target.leak_on = self.turn + 1
            self.add_stage(mon, "Speed", 2, 6)
            return
        if mid == "packet_skim" and target is not None:
            self.denied += self._enemy_drain(target, 1, "skim")
            return
        if mid == "transaction_tax" and target is not None:
            removed = self._enemy_drain(target, 1, "tax")
            self.denied += removed
            if removed and self.opts.get("tax_steals", True) and not mon.has("ThreadLock"):
                mon.ap = min(sim.AP_CAP, mon.ap + removed)
            return
        if mid == "hype_burst" and target is not None:
            stripped = 0
            for s in list(target.statuses):
                if stripped >= 2:
                    break
                row = sim.STATUSES.get(s["id"], {})
                if row.get("IsBuff") == "true":
                    target.statuses.remove(s)
                    stripped += 1
            stat = "RangedAtk" if self.opts.get("stack") else "MeleeAtk"
            if stripped:
                self.add_stage(mon, stat, stripped, 2)
            return
        if mid in ("ember_wall", "blockade", "ice_wall", "barrier_wall", "vapor_barrier", "compound_guard", "ash_guard", "lattice_guard"):
            self._place_wall(mon, target or mon, mid)
            return
        if mid == "guard_posture":
            bearer = target or mon
            if self.opts.get("stack"):
                bearer.armor = dict(flat=12, pct=0.0, hits=1, turns=2)
            else:
                bearer.armor = dict(flat=0, pct=0.25, hits=1, turns=2)
            return
        if mid == "guard_stance":
            bearer = target or mon
            if self.opts.get("stack"):
                bearer.armor = dict(flat=16, pct=0.0, hits=1, turns=2)
            else:
                bearer.armor = None
                self.add_stage(bearer, "MeleeDef", 2, 6)
            return
        if mid == "fuel_surge":
            if not mon.has("ThreadLock"):
                mon.ap = min(sim.AP_CAP, mon.ap + 3)
            mon.statuses.append(dict(id="GasSpike", turns=2, eot=0, revert=[]))
            self.add_stage(mon, "MeleeAtk", 2, 6)
            self.add_stage(mon, "MeleeDef", -2, -6)
            return
        if mid == "ap_reserve":
            banked = min(mon.ap, 5)
            if banked > 0:
                mon.ap -= banked
                mon.reserve_on = self.turn + 1
                mon.reserve_ap = banked + 1
            return
        if mv["damaging"] and mv["tgt"] in ("AllEnemies", "Field"):
            foes = [f for f in self.actives(1 - mon.team) if f.alive]
            for foe in foes:
                self._damage(mon, foe, mv)
            self._finish_statuses(mon, mv, True)
            if "NextMoveAPSurcharge" in (mv.get("util") or ""):
                mon.surcharge += 1
            return
        if mv["damaging"] and target is not None:
            self._damage(mon, target, mv)
            self._finish_statuses(mon, mv, True)
            if "NextMoveAPSurcharge" in (mv.get("util") or ""):
                mon.surcharge += 1
            return
        self._finish_statuses(mon, mv, False)

    def _pick_target(self, mon, mv, act):
        if mv["tgt"] == "Self":
            return mon
        if mv["tgt"] in ("SingleAlly", "AllAllies"):
            allies = [ally for ally in self.actives(mon.team) if ally is not mon and ally.alive]
            return allies[0] if allies else mon
        if len(act) > 2 and not isinstance(act[2], int):
            return act[2]
        slot = act[2] if len(act) > 2 else 0
        target = self.foe_slot_mon(mon, slot if isinstance(slot, int) else 0)
        if target is not None and mv["tgt"] in ("SingleEnemy", "AllEnemies") and (not target.alive or not target.active):
            target = self.foe_slot_mon(mon, 0)
        return target

    def _stream_drop(self, mon):
        if "streamlined_flow" not in mon.passive_ids or mon.hp <= 0.7 * mon.maxhp:
            return
        if self.opts.get("stack"):
            if mon.stream_used:
                return
            self.add_stage(mon, "MeleeDef", -1, -1)
            mon.stream_used = True
        else:
            self.add_stage(mon, "MeleeDef", -1, -6)

    def _finish_statuses(self, mon, mv, landed_damage):
        for part in filter(None, (mv.get("status") or "").split("|")):
            bits = part.split(":")
            if not bits or not bits[0]:
                continue
            chance = _chance(bits[2]) if len(bits) > 2 and bits[2] else 1.0
            if self.rng.random() >= chance:
                continue
            dur = int(float(bits[1])) if len(bits) > 1 and bits[1] else 1
            who = mon
            if len(bits) > 3 and bits[3] not in ("Self", ""):
                who = mon
            if bits[0] == "Surging":
                mon.statuses.append(dict(id="Surging", turns=dur or 1, eot=0, revert=[]))
            elif bits[0] == "CipherLocked":
                # Cipher Shot's status string has an empty target; it hits the foe.
                # Handled in _damage via the status field when the move is damaging.
                pass
            else:
                who.statuses.append(dict(id=bits[0], turns=dur or 1, eot=0, revert=[]))

    def _damage(self, att, de, mv):
        hits = mv["hmin"] if mv["hmin"] == mv["hmax"] else self.rng.randint(mv["hmin"], mv["hmax"])
        reacted = False
        total = 0
        mv = dict(mv)
        mv["ls"] = 0.0
        for bits in parse_utils(mv.get("util") or ""):
            if bits[0] == "LifestealPercent" and len(bits) > 1:
                mv["ls"] = _chance(bits[1])
        any_hit = False
        for _h in range(hits):
            if not de.alive or not att.alive:
                break
            acc = 1.0 if not mv["acc"] else min(1.0, mv["acc"] / 100 * sim.smult(att.acc_stage) / sim.smult(de.eva_stage))
            if not self.opts.get("deterministic") and self.rng.random() >= acc:
                self.trace.append(dict(turn=self.turn, ev="miss", who=att.sid, move=mv["id"]))
                continue
            any_hit = True
            atype = _attack_type(mv, att, self.opts.get("typeless_neutral"))
            chem = 1.0
            if atype is not None and (atype, de.type) in sim.MATRIX:
                chem = sim.DATA["chem_adv"]
                name, rec = sim.MATRIX[(atype, de.type)]
                if not reacted:
                    reacted = True
                    self._apply_chem(de if rec == 0 else att, name)
            elif atype is not None and (de.type, atype) in sim.MATRIX:
                chem = sim.DATA["chem_dis"]
            crit = False if self.opts.get("deterministic") else self.rng.random() < 0.0625
            roll = 0.925 if self.opts.get("deterministic") else self.rng.uniform(sim.DATA["roll_min"], sim.DATA["roll_max"])
            atk = att.stat(attack_stat_key(mv))
            df = de.stat("md" if mv["melee"] else "rd")
            cond = 1.0
            if mv["id"] == "hammer_sync":
                n = sum(1 for ally in self.actives(att.team) if ally is not att and ally.buffed())
                cond = min(1.2, 1 + 0.2 * n)
            elif "SpeedHigherThanOther" in (mv.get("cond_gate") or "") and att.stat("spd") > de.stat("spd"):
                cond = mv.get("cond_m") or 1.0
            if any(s["id"] == "Surging" and s["turns"] > 0 for s in att.statuses):
                cond *= 1.25
                att.statuses = [s for s in att.statuses if s["id"] != "Surging"]
            contact = "melee" if mv["melee"] else "ranged"
            mit = 1.0
            block = de.block
            used_block = False
            if block and block["hits"] > 0 and block["contact"] == contact and not mv.get("bypass"):
                mit *= (1 - block["mag"])
                used_block = True
            if "immutable_ledger" in de.passive_ids and de.hits_turn == 0:
                mit *= 0.80
            base = (mv["power"] * atk / df * roll + 1.0) * chem * (sim.DATA["crit_m"] if crit else 1.0) * cond
            flo = mv["power"] * atk / df * sim.DATA["floor"]
            dealt = base * mit
            if de.armor and de.armor["hits"] > 0 and not mv.get("bypass"):
                if de.armor.get("pct"):
                    dealt *= (1 - de.armor["pct"])
                dealt -= de.armor.get("flat") or 0
                de.armor["hits"] -= 1
            dealt = max(1, math.floor(max(dealt, flo)))
            de.hp -= dealt
            de.hits_turn += 1
            total += dealt
            att.did_damage = True
            if mv.get("ls"):
                heal = int(math.floor(dealt * mv["ls"]))
                if heal > 0:
                    att.hp = min(att.maxhp, att.hp + heal)
            if used_block:
                if block["drain"]:
                    self.denied += self._enemy_drain(att, block["drain"], "ember")
                if block["ap_gain"] and de.stat("spd") > att.stat("spd") and not de.has("ThreadLock"):
                    de.ap = min(sim.AP_CAP, de.ap + min(1, block["ap_gain"]))
                block["hits"] -= 1
                if block["hits"] <= 0:
                    de.block = None
            if "immutable_ledger" in de.passive_ids and de.hits_turn == 1 and not de.has("ThreadLock"):
                de.ap = min(sim.AP_CAP, de.ap + 1)
            if "kindling" in att.passive_ids and not de.has("Kindled"):
                de.statuses.append(dict(id="Kindled", turns=1, eot=0, revert=[]))
            if "extraction_hit" in att.passive_ids and self.opts.get("stack"):
                if self.rng.random() < 0.40:
                    self.add_stage(att, "MeleeAtk", 1, 3)
                    if att.ma_stage >= 3 and "extract_spd" not in att.once:
                        self.add_stage(att, "Speed", 1, 6)
                        att.once.add("extract_spd")
            self._apply_stages(att, de, mv, None)
            # Cipher Shot applies the lock as a status on the foe.
            if "CipherLocked" in (mv.get("status") or "") and not de.has("CipherLocked"):
                de.statuses.append(dict(id="CipherLocked", turns=2, eot=0, revert=[]))
            self._ko_check(de, mv["id"])
        if any_hit:
            self.trace.append(dict(turn=self.turn, ev="hit", who=att.sid, tgt=de.sid, move=mv["id"], dmg=total, hp=max(0, de.hp)))

    def _apply_chem(self, mon, name):
        row = sim.STATUSES.get(name)
        if row is None:
            return
        if name == "Shattered":
            self._shatter(mon)
            return
        duration = int(row["Duration"] or 1)
        eot = int(row["EoTAPChange"] or 0)
        existing = next((s for s in mon.statuses if s["id"] == name and s["turns"] > 0), None)
        if existing is not None:
            existing["turns"] = max(duration, 1) if duration else existing["turns"]
            self.trace.append(dict(turn=self.turn, ev="chem", who=mon.sid, status=name, refresh=True))
            return
        revert = []
        if name == "Dusted" and self.opts.get("stack"):
            wrote = self.add_stage(mon, "Speed", -2, -6)
            if wrote:
                revert.append(("Speed", wrote))
        else:
            for part in filter(None, (row.get("StageEffects") or "").split("|")):
                bits = part.split(":")
                if not bits or bits[0] not in STAT_ATTR:
                    continue
                try:
                    delta = int(float(bits[1]))
                except ValueError:
                    continue
                wrote = self.add_stage(mon, bits[0], delta, 6 if delta > 0 else -6)
                if wrote:
                    revert.append((bits[0], wrote))
        if name == "Rugged":
            mon.statuses.append(dict(id=name, turns=duration or 1, eot=eot, revert=revert, counter=1))
        elif name == "Corroded":
            mon.statuses.append(dict(id=name, turns=duration or 1, eot=eot, revert=[], corroded=0))
        else:
            mon.statuses.append(dict(id=name, turns=max(duration, 1), eot=eot, revert=revert))
        self.trace.append(dict(turn=self.turn, ev="chem", who=mon.sid, status=name, refresh=False))

    def _shatter(self, mon):
        chip = max(1, int(mon.hp * 0.08))
        if self.opts.get("stack"):
            chip = min(chip, max(0, mon.hp - 1))
        mon.hp -= chip
        self._ko_check(mon, "shattered")
        if not self.opts.get("stack"):
            self.add_stage(mon, "MeleeDef", -1, -6)
            return
        wrote_m = self.add_stage(mon, "MeleeDef", -1, -6)
        wrote_r = self.add_stage(mon, "RangedDef", -1, -6)
        revert = []
        if wrote_m:
            revert.append(("MeleeDef", wrote_m))
        if wrote_r:
            revert.append(("RangedDef", wrote_r))
        mon.statuses.append(dict(id="ShatteredHold", turns=2, eot=0, revert=revert))

    def _end(self):
        for t in (0, 1):
            for mon in self.actives(t):
                if "patience_passive" in mon.passive_ids:
                    cap = 3 if self.opts.get("stack") else 6
                    self.add_stage(mon, "MeleeDef", 1, cap)
                    if not mon.did_damage and mon.hp > 0:
                        heal = max(1, int(mon.maxhp * 0.05))
                        mon.hp = min(mon.maxhp, mon.hp + heal)
                if mon.has("Kindled"):
                    dot = max(1, int(mon.maxhp * 0.06))
                    mon.hp -= dot
                    self._ko_check(mon, "kindling")
                if not mon.alive:
                    continue
                for s in list(mon.statuses):
                    if s["id"] == "Rugged" and mon.alive:
                        cap = 3 if self.opts.get("stack") else 15
                        tick = max(1, min(cap, s.get("counter", 1)))
                        dot = max(1, int(mon.maxhp * (tick / 16.0)))
                        mon.hp -= dot
                        s["counter"] = min(cap, tick + 1)
                        self._ko_check(mon, "rugged")
                    if s["id"] == "Corroded" and mon.alive:
                        cap_c = 2 if self.opts.get("stack") else 6
                        if s.get("corroded", 0) < cap_c:
                            self.add_stage(mon, "MeleeDef", -1, -6)
                            if self.opts.get("stack"):
                                self.add_stage(mon, "RangedDef", -1, -6)
                            s["corroded"] = s.get("corroded", 0) + 1
                            s.setdefault("revert", [])
                            s["revert"].append(("MeleeDef", -1))
                            if self.opts.get("stack"):
                                s["revert"].append(("RangedDef", -1))
                    if s["eot"] < 0:
                        self.denied += self._enemy_drain(mon, abs(s["eot"]), s["id"])
                    elif s["eot"] > 0 and not mon.has("ThreadLock"):
                        mon.ap = min(sim.AP_CAP, mon.ap + s["eot"])
                if not mon.alive:
                    continue
                for s in list(mon.statuses):
                    s["turns"] -= 1
                    if s["turns"] <= 0:
                        for stat, wrote in s.get("revert") or []:
                            self.add_stage(mon, stat, -wrote, 6 if -wrote > 0 else -6)
                        if s["id"] == "ThreadLock":
                            mon.thread_until = self.turn + 2
                        mon.statuses.remove(s)
                if mon.block:
                    mon.block["turns"] -= 1
                    if mon.block["turns"] <= 0 or mon.block["hits"] <= 0:
                        mon.block = None
                if mon.armor:
                    mon.armor["turns"] -= 1
                    if mon.armor["turns"] <= 0 or mon.armor["hits"] <= 0:
                        mon.armor = None
                for mid in list(mon.recharge):
                    mon.recharge[mid] -= 1
                    if mon.recharge[mid] <= 0:
                        del mon.recharge[mid]
            for mon in self.teams[t]:
                if mon.alive and not mon.active:
                    for mid in list(mon.recharge):
                        mon.recharge[mid] -= 1
                        if mon.recharge[mid] <= 0:
                            del mon.recharge[mid]


def _chance(token: str) -> float:
    token = token.strip()
    if token.endswith("%"):
        return float(token[:-1]) / 100.0
    try:
        value = float(token)
    except ValueError:
        return 1.0
    return value / 100.0 if value > 1 else value


def _attack_type(mv, att, neutral: bool):
    if mv["type"] == "None":
        if neutral:
            return None
        return att.type
    return sim.TYPES.index(mv["type"])


def _living_foes(battle, mon):
    return [f for f in battle.actives(1 - mon.team) if f.alive]


def attack_stat_key(mv) -> str:
    row = sim.MOVES.get(mv["id"], {})
    if str(row.get("DamageOverrideAttackStat", "")).lower() == "true":
        named = row.get("DamageAttackStat") or ""
        return {"MeleeAtk": "ma", "RangedAtk": "ra", "MeleeDef": "md", "RangedDef": "rd"}.get(named, "ma" if mv["melee"] else "ra")
    return "ma" if mv["melee"] else "ra"


def estimate(att, de, mv, neutral: bool) -> float:
    if not mv["damaging"] or mv["power"] <= 0 or de is None:
        return 0.0
    atk = att.stat(attack_stat_key(mv))
    df = de.stat("md" if mv["melee"] else "rd")
    atype = _attack_type(mv, att, neutral)
    chem = 1.0
    if atype is not None:
        if (atype, de.type) in sim.MATRIX:
            chem = sim.DATA["chem_adv"]
        elif (de.type, atype) in sim.MATRIX:
            chem = sim.DATA["chem_dis"]
    hits = (mv["hmin"] + mv["hmax"]) / 2
    acc = 1.0 if not mv["acc"] else mv["acc"] / 100.0
    roll = (sim.DATA["roll_min"] + sim.DATA["roll_max"]) / 2
    cond = 1.0
    if "SpeedHigherThanOther" in (mv.get("cond_gate") or "") and att.stat("spd") > de.stat("spd"):
        cond = mv.get("cond_m") or 1.0
    per = (mv["power"] * atk / df * roll + 1.0) * chem * cond
    if de.block and de.block["hits"] > 0:
        contact = "melee" if mv["melee"] else "ranged"
        if de.block["contact"] == contact:
            per *= (1 - de.block["mag"])
    return per * hits * acc


def policy_greedy(battle, mon, _slot):
    foes = _living_foes(battle, mon)
    if not foes:
        return ("pass",)
    foe = min(foes, key=lambda f: f.hp / f.maxhp)
    neutral = bool(battle.opts.get("typeless_neutral"))
    best = (-1e9, ("pass",))
    best2_dpa = 0.0
    heavy = None
    for mv in mon.moves.values():
        if mv["cat"] == "Passive" or mon.recharge.get(mv["id"], 0) > 0:
            continue
        cost = mv["ap"] + mon.surcharge
        if "streamlined_flow" in mon.passive_ids and mon.hp > 0.7 * mon.maxhp and mv["ap"] >= 1 and cost > 0:
            cost = max(1, cost - 1)
        dmg = estimate(mon, foe, mv, neutral)
        if mv["tgt"] in ("AllEnemies", "Field"):
            dmg *= len(foes)
        if mv["damaging"] and mv["ap"] == 2 and cost <= mon.ap:
            best2_dpa = max(best2_dpa, dmg / 2)
        if mv["damaging"] and mv["ap"] >= 4:
            heavy = (mv, cost, dmg)
        if cost > mon.ap:
            continue
        score = dmg
        if mv["id"] in ("packet_skim", "transaction_tax") and foe.ap > 0:
            score = max(score, 16.0)
        elif mv["id"] == "memory_leak" and foe.ap > 0:
            score = max(score, 30.0)
        elif mv["id"] in ("ember_wall", "blockade", "ice_wall", "barrier_wall", "vapor_barrier", "compound_guard", "ash_guard", "lattice_guard"):
            spec = {
                "ember_wall": ("melee", 0.50), "blockade": ("melee", 0.60), "ice_wall": ("melee", 0.40),
                "barrier_wall": ("ranged", 0.25), "vapor_barrier": ("ranged", 0.30),
                "compound_guard": ("ranged", 0.35), "ash_guard": ("ranged", 0.35), "lattice_guard": ("ranged", 0.40),
            }[mv["id"]]
            bearer = mon
            allies = [a for a in battle.actives(mon.team) if a is not mon and a.alive]
            if allies and not (allies[0].block):
                bearer = allies[0]
            incoming = 0.0
            for foe_mv in foe.moves.values():
                if foe_mv["damaging"]:
                    incoming = max(incoming, estimate(foe, bearer, foe_mv, neutral))
            if bearer.block:
                score = 0.0
            else:
                contact = "melee" if any(x["melee"] and x["damaging"] for x in foe.moves.values()) else "ranged"
                # Prefer the wall only when the foe's best damaging move matches it.
                best_foe = max((x for x in foe.moves.values() if x["damaging"]), key=lambda x: x["power"], default=None)
                contact = ("melee" if best_foe["melee"] else "ranged") if best_foe else contact
                score = incoming * spec[1] if contact == spec[0] else incoming * spec[1] * 0.15
        elif mv["id"] == "system_reset":
            score = 20.0 if foe.statuses else 2.0
        elif mv["id"] == "hype_burst":
            buffs = sum(1 for s in foe.statuses if sim.STATUSES.get(s["id"], {}).get("IsBuff") == "true")
            score = 18.0 * min(2, buffs)
        if score > best[0]:
            best = (score, ("move", mv["id"], foe))
    if heavy is not None:
        mv, cost, dmg = heavy
        if mon.ap < cost <= mon.ap + sim.AP_TURN:
            roll = sim.DATA["roll_min"]
            # Minimum-roll sketch: scale the mean estimate down to the roll floor.
            mean_roll = (sim.DATA["roll_min"] + sim.DATA["roll_max"]) / 2
            min_est = dmg * (roll / mean_roll) if mean_roll else dmg
            heavy_dpa = (dmg / mv["ap"]) if mv["ap"] else 0
            if min_est >= foe.hp or (best2_dpa and heavy_dpa >= 1.15 * best2_dpa) or (not best2_dpa and dmg > 0):
                wait = dmg * 0.85
                if wait > best[0]:
                    best = (wait, ("pass",))
    return best[1]


def make_team(sids, stack, rng):
    use_moves(stack)
    return [sim.make_mon(sid, kit_of(sid, stack)) for sid in sids]


def play(sids_a, sids_b, stack, rng, max_turns=14):
    teams = [make_team(sids_a, stack, rng), make_team(sids_b, stack, rng)]
    opts = dict(
        max_turns=max_turns,
        stack=stack,
        tax_steals=not stack,
        typeless_neutral=stack,
    )
    battle = StackBattle(teams, [policy_greedy, policy_greedy], rng, opts)
    battle.run()
    return battle


def summarize(battles):
    n = len(battles)
    if not n:
        return {}
    wins = [0, 0, 0]
    length = 0
    spent = 0
    denied = 0
    turns = 0
    block = 0
    actors = 0
    caps = 0
    picks = Counter()
    species = Counter()
    for b in battles:
        if b.winner == 0:
            wins[0] += 1
        elif b.winner == 1:
            wins[1] += 1
        else:
            wins[2] += 1
        length += b.end_turn or b.max_turns
        spent += b.spent
        denied += b.denied
        turns += b.end_turn or b.turn
        block += b.block_actor_turns
        actors += b.actor_turns
        caps += 1 if b.capped else 0
        picks.update(b.picks)
        species.update(b.species_picks)
    return dict(
        n=n,
        a=wins[0],
        b=wins[1],
        draw=wins[2],
        a_pct=round(100.0 * wins[0] / n, 1),
        b_pct=round(100.0 * wins[1] / n, 1),
        length=round(length / n, 2),
        spent_per_turn=round(spent / max(1, turns), 3),
        denied_per_turn=round(denied / max(1, turns), 3),
        block_share=round(block / max(1, actors), 4),
        cap_share=round(caps / n, 4),
        picks=picks,
        species=species,
    )


def run_pair(sids_a, sids_b, stack, trials, seed, max_turns=14):
    battles = []
    for i in range(trials):
        rng = random.Random(seed + i + (17 if stack else 0))
        battles.append(play(sids_a, sids_b, stack, rng, max_turns))
    return summarize(battles)


def policy_skim_then(damage_ids):
    """Skim a foe who has at least 2 AP. Otherwise click the first affordable damage id."""
    def fn(battle, mon, _slot):
        foe = battle.foe_slot_mon(mon, 0)
        if foe is None:
            return ("pass",)
        if "packet_skim" in mon.moves and foe.ap >= 2 and mon.ap >= mon.moves["packet_skim"]["ap"] and mon.recharge.get("packet_skim", 0) <= 0:
            return ("move", "packet_skim", foe)
        for mid in damage_ids:
            if mid in mon.moves and mon.recharge.get(mid, 0) <= 0:
                cost = mon.moves[mid]["ap"] + mon.surcharge
                if mon.ap >= cost:
                    return ("move", mid, foe)
        return ("pass",)
    return fn


def run_script(name, sids_a, sids_b, pol_a, pol_b, stack, trials, seed, max_turns=16):
    wins = [0, 0, 0]
    hammers = []
    lands = 0
    ends = []
    caps = 0
    hits_before = []
    first_cast = defaultdict(list)
    for i in range(trials):
        rng = random.Random(seed + i + (101 if stack else 0))
        use_moves(stack)
        teams = [make_team(sids_a, stack, rng), make_team(sids_b, stack, rng)]
        opts = dict(max_turns=max_turns, stack=stack, tax_steals=not stack, typeless_neutral=stack)
        battle = StackBattle(teams, [pol_a, pol_b], rng, opts)
        battle.run()
        if battle.winner == 0:
            wins[0] += 1
        elif battle.winner == 1:
            wins[1] += 1
        else:
            wins[2] += 1
        ends.append(battle.end_turn or max_turns)
        caps += 1 if battle.capped or battle.end_turn is None else 0
        seen = set()
        before = 0
        for ev in battle.trace:
            if ev.get("ev") == "cast" and (ev.get("who"), ev.get("move")) not in seen:
                seen.add((ev.get("who"), ev.get("move")))
                first_cast[ev["move"]].append(ev["turn"])
            if ev.get("ev") == "hit" and ev.get("who") in sids_b:
                if battle.hammer_turn is None or ev.get("turn", 99) < battle.hammer_turn:
                    before += 1
        hits_before.append(before)
        if battle.hammer_turn:
            lands += 1
            hammers.append(battle.hammer_turn)
    n = trials
    casts = {mid: round(sum(v) / len(v), 2) for mid, v in first_cast.items() if v}
    return dict(
        name=name,
        n=n,
        heavy_pct=round(100.0 * wins[0] / n, 1),
        fast_pct=round(100.0 * wins[1] / n, 1),
        draw_pct=round(100.0 * wins[2] / n, 1),
        heavy_lands_pct=round(100.0 * lands / n, 1),
        heavy_turn=round(sum(hammers) / len(hammers), 2) if hammers else None,
        hits_before=round(sum(hits_before) / len(hits_before), 2) if hits_before else None,
        length=round(sum(ends) / n, 2),
        cap_pct=round(100.0 * caps / n, 1),
        first_cast=casts,
    )


def action_series(rows, aid="V"):
    return [sim.action_ap(row, aid) for row in rows]


def calendars():
    V = sim.actor("validape", team=0)
    K = sim.actor("keyper", team=1)
    D = sim.actor("deghoul", team=1)
    Kit = sim.actor("kitsauto", team=1)
    always = lambda act: (lambda st, aid: act)
    out = {}

    def add(name, actors, plan, opts=None, turns=8, watch="V"):
        rows = sim.run_calendar(actors, sim.script_always(plan), turns=turns, opts=opts or {})
        out[name] = action_series(rows, watch)

    add("leak_pass_base", {"V": dict(V), "K": dict(K)}, {"V": always(("pass",)), "K": always(("leak", "V"))})
    add("leak_pass_stack", {"V": dict(V), "K": dict(K)}, {"V": always(("pass",)), "K": always(("leak", "V"))}, opts={"leak_cd": 1})
    add("leak_spend_base", {"V": dict(V), "K": dict(K)}, {"V": sim.script_always({}) and (lambda st, aid: ("spend", 2) if st.a[aid]["ap"] >= 2 else ("pass",)), "K": always(("leak", "V"))})
    # The lambda above is messy. Rebuild spend cleanly.
    return out


def spend2(st, aid):
    return ("spend", 2) if st.a[aid]["ap"] >= 2 else ("pass",)


def build_calendars():
    V = sim.actor("validape", team=0)
    K = sim.actor("keyper", team=1)
    D = sim.actor("deghoul", team=1)
    Kit = sim.actor("kitsauto", team=1)

    def always(act):
        return lambda st, aid: act

    def add(actors, plan, opts=None, turns=8, watch="V"):
        rows = sim.run_calendar({k: dict(v) for k, v in actors.items()}, sim.script_always(plan), turns=turns, opts=opts or {})
        return [sim.action_ap(row, watch) for row in rows]

    actors_lk = {"V": V, "K": K}
    plan_pass = {"V": always(("pass",)), "K": always(("leak", "V"))}
    plan_spend = {"V": spend2, "K": always(("leak", "V"))}
    actors_ts = {"V": V, "D": D, "Kit": Kit}
    plan_ts = {"V": always(("pass",)), "D": always(("tax", "V")), "Kit": always(("skim", "V"))}
    out = {
        "leak_pass_base": add(actors_lk, plan_pass),
        "leak_pass_stack": add(actors_lk, plan_pass, {"leak_cd": 1}),
        "leak_spend_base": add(actors_lk, plan_spend),
        "leak_spend_stack": add(actors_lk, plan_spend, {"leak_cd": 1}),
        "taxskim_pass_base": add(actors_ts, plan_ts),
        "taxskim_pass_stack": add(actors_ts, plan_ts, {"tax_steals": False}),
        "deghoul_base": add({"V": V, "D": D}, {"V": always(("pass",)), "D": always(("tax", "V"))}, watch="D"),
        "deghoul_stack": add({"V": V, "D": D}, {"V": always(("pass",)), "D": always(("tax", "V"))}, {"tax_steals": False}, watch="D"),
        "victim_tax_base": add({"V": V, "D": D}, {"V": always(("pass",)), "D": always(("tax", "V"))}),
        "victim_tax_stack": add({"V": V, "D": D}, {"V": always(("pass",)), "D": always(("tax", "V"))}, {"tax_steals": False}),
    }
    out["fee_tax_skim"] = fee_series(False)
    out["fee_tax_skim_stack"] = fee_series(True)
    out["fee_from_bank_stack"] = fee_series(True, v_start=4)
    return out


def fee_series(stack: bool, v_start: int = 0):
    """Validape passes. Kitsauto skims. Deghoul taxes. Stack adds Fee Collector and tax-as-drain.

    Validape is the slowest, so the recorded value is AP at Validape's action,
    after Fee Collector and after both faster drains.
    """
    v = v_start
    d = 0
    k = 0
    series = []
    for _turn in range(8):
        v = min(sim.AP_CAP, v + sim.AP_TURN)
        d = min(sim.AP_CAP, d + sim.AP_TURN)
        k = min(sim.AP_CAP, k + sim.AP_TURN)
        if stack and v > d:
            v = max(0, v - 1)
        if k >= 1:
            k -= 1
            v = max(0, v - 1)
        if d >= 1:
            d -= 1
            took = 1 if v > 0 else 0
            v = max(0, v - 1)
            if not stack and took:
                d = min(sim.AP_CAP, d + 1)
        series.append(v)
    return series


def one_hit(att_sid, def_sid, move_id, roll=0.925, wall=0.0, armor_pct=0.0, flat=0.0, ledger=False, neutral=False, cond=1.0):
    att = sim.SPECIES[att_sid]
    de = sim.SPECIES[def_sid]
    mv = sim.move_view(move_id) if move_id in sim.DATA["moves"] else sim.move_view("baseline_shot")
    # move_view reads MOVES. Caller sets it.
    melee = mv["melee"] if move_id in sim.MOVES else True
    power = mv["power"] if move_id in sim.MOVES else 0
    if move_id in STACK_MOVES and sim.MOVES is STACK_MOVES:
        power = int(STACK_MOVES[move_id]["Power"] or 0)
        melee = STACK_MOVES[move_id]["Category"] == "DamagingMelee"
    atk = att["ma"] if melee else att["ra"]
    df = de["md"] if melee else de["rd"]
    move_type = sim.MOVES[move_id]["BaseType"] if move_id in sim.MOVES else "None"
    atype = None
    if move_type != "None":
        atype = sim.TYPES.index(move_type)
    elif not neutral:
        atype = att["type"]
    chem = 1.0
    if atype is not None:
        if (atype, de["type"]) in sim.MATRIX:
            chem = sim.DATA["chem_adv"]
        elif (de["type"], atype) in sim.MATRIX:
            chem = sim.DATA["chem_dis"]
    pre = (power * atk / df * roll + 1.0) * chem * cond
    pct = (1 - wall) * (1 - armor_pct) * (0.80 if ledger else 1.0)
    dealt = pre * pct - flat
    flo = power * atk / df * sim.DATA["floor"]
    final = max(1, math.floor(max(dealt, flo))) if power else 0
    return dict(pre=round(pre, 2), chem=chem, dmg=final, power=power, atk=atk, df=df)


def formula_pack():
    use_moves(False)
    base_open = one_hit("validape", "deghoul", "hammer_sync")
    use_moves(False)
    base_wall_pct = one_hit("validape", "deghoul", "hammer_sync", wall=0.60, armor_pct=0.25)
    base_wall_flat = one_hit("validape", "deghoul", "hammer_sync", wall=0.60, flat=12)
    base_flat = one_hit("validape", "deghoul", "hammer_sync", flat=12)
    # Blade flurry is 3 hits. Wall and armor spend on hit 1 only.
    use_moves(False)
    b_open = one_hit("kitsauto", "cinislug", "blade_flurry")
    b_first = one_hit("kitsauto", "cinislug", "blade_flurry", wall=0.50, flat=12)
    use_moves(True)
    s_open = one_hit("kitsauto", "cinislug", "blade_flurry")
    s_first = one_hit("kitsauto", "cinislug", "blade_flurry", wall=0.50, flat=12)
    # Typeless inheritance. Burned Cinislug Pulse Shot into Gas.
    use_moves(False)
    pulse_base = one_hit("cinislug", "deghoul", "baseline_shot", neutral=False)
    pulse_stack = one_hit("cinislug", "deghoul", "baseline_shot", neutral=True)
    cinder_base = one_hit("cinislug", "deghoul", "cinder_burst", neutral=False)
    # Three-stack only if Guard Stance were also equipped. Not the default.
    triple = one_hit("validape", "blockwyrm", "hammer_sync", wall=0.60, flat=16, ledger=True)
    return dict(
        hammer_open=base_open,
        hammer_wall_and_percent=base_wall_pct,
        hammer_wall_and_flat12=base_wall_flat,
        hammer_flat12=base_flat,
        flats_match=base_wall_pct["dmg"] == base_wall_flat["dmg"],
        blade_base_open=b_open["dmg"],
        blade_base_first=b_first["dmg"],
        blade_stack_open=s_open["dmg"],
        blade_stack_first=s_first["dmg"],
        blade_rest_unmitigated=True,
        pulse_inherited=pulse_base,
        pulse_neutral=pulse_stack,
        cinder_into_gas=cinder_base,
        triple_if_q13=triple,
    )


def top_picks(counter: Counter, moves, n=3):
    by_r = defaultdict(list)
    total_r = Counter()
    for mid, count in counter.items():
        row = moves.get(mid) or STACK_MOVES.get(mid)
        if row is None:
            continue
        rar = row["Rarity"]
        by_r[rar].append((mid, count))
        total_r[rar] += count
    out = {}
    for rar, pairs in by_r.items():
        pairs.sort(key=lambda x: -x[1])
        tot = total_r[rar] or 1
        out[rar] = [dict(move=mid, n=c, share=round(100.0 * c / tot, 1)) for mid, c in pairs[:n]]
    return out


def best_by_species(counter: Counter):
    best = {}
    counts = {}
    for (sid, mid), count in counter.items():
        counts.setdefault(sid, Counter())[mid] = count
    for sid, c in counts.items():
        mid, n = c.most_common(1)[0]
        best[sid] = dict(move=mid, n=n, share=round(100.0 * n / sum(c.values()), 1))
    return best


def campaign(field_trials=100, type_trials=60, script_trials=240, seed=SEED):
    for sid, moves in KITS.items():
        for m in moves:
            if m not in BASE_MOVES:
                raise SystemExit(f"kit {sid} has unknown {m}")

    arch_names = list(ARCHETYPES)
    arch_stats = {name: {"base_w": 0, "base_g": 0, "stack_w": 0, "stack_g": 0} for name in arch_names}
    length = {"base": [], "stack": []}
    ap = {"base": [], "stack": []}
    block = {"base": [], "stack": []}
    picks = {"base": Counter(), "stack": Counter()}
    species = {"base": Counter(), "stack": Counter()}
    for i, a in enumerate(arch_names):
        for b in arch_names[i + 1:]:
            for stack, key in ((False, "base"), (True, "stack")):
                # Both directions so neither archetype is always the heavy side.
                for left, right, sign in ((a, b, 1), (b, a, -1)):
                    summary = run_pair(ARCHETYPES[left], ARCHETYPES[right], stack, field_trials, seed + i * 50 + arch_names.index(b), 12)
                    win_left = summary["a"]
                    win_right = summary["b"]
                    arch_stats[left][f"{key}_w"] += win_left
                    arch_stats[left][f"{key}_g"] += summary["n"]
                    arch_stats[right][f"{key}_w"] += win_right
                    arch_stats[right][f"{key}_g"] += summary["n"]
                    length[key].append(summary["length"])
                    ap[key].append((summary["spent_per_turn"], summary["denied_per_turn"], summary["n"]))
                    block[key].append((summary["block_share"], summary["n"]))
                    picks[key].update(summary["picks"])
                    species[key].update(summary["species"])

    def wr(stats, key):
        g = stats[f"{key}_g"] or 1
        return round(100.0 * stats[f"{key}_w"] / g, 1)

    archetypes = {}
    for name, stats in arch_stats.items():
        archetypes[name] = dict(
            baseline=wr(stats, "base"),
            stack=wr(stats, "stack"),
            delta=round(wr(stats, "stack") - wr(stats, "base"), 1),
            games=stats["base_g"],
        )

    type_stats = {t: {"base_w": 0, "base_g": 0, "stack_w": 0, "stack_g": 0} for t, _s in TYPE_FACES}
    for i, (ta, sa) in enumerate(TYPE_FACES):
        for tb, sb in TYPE_FACES[i + 1:]:
            partner_a = "rattlestake" if sa == "humpacked" else "humpacked"
            partner_b = "rattlestake" if sb == "humpacked" else "humpacked"
            if partner_a == sb:
                partner_a = "rattlestake"
            if partner_b == sa:
                partner_b = "rattlestake"
            for stack, key in ((False, "base"), (True, "stack")):
                summary = run_pair((sa, partner_a), (sb, partner_b), stack, type_trials, seed + 4000 + i * 20, 12)
                type_stats[ta][f"{key}_w"] += summary["a"]
                type_stats[ta][f"{key}_g"] += summary["n"]
                type_stats[tb][f"{key}_w"] += summary["b"]
                type_stats[tb][f"{key}_g"] += summary["n"]
    types = {}
    for t, stats in type_stats.items():
        types[t] = dict(baseline=wr(stats, "base"), stack=wr(stats, "stack"),
                        delta=round(wr(stats, "stack") - wr(stats, "base"), 1), games=stats["base_g"])

    def mean_len(key):
        return round(sum(length[key]) / len(length[key]), 2)

    def mean_ap(key):
        spent = sum(s * n for s, _d, n in ap[key])
        denied = sum(d * n for _s, d, n in ap[key])
        games = sum(n for _s, _d, n in ap[key])
        return dict(spent_per_turn=round(spent / games, 3), denied_per_turn=round(denied / games, 3))

    def mean_block(key):
        num = sum(s * n for s, n in block[key])
        den = sum(n for _s, n in block[key])
        return round(num / den, 4)

    # Scripted tempo. Team 0 is the 4 AP side. Team 1 is the faster side.
    scripts = []
    scenarios = [
        ("seam_vs_dig", ("kodiax", "hopoch"), ("crackett", "deghoul"),
         sim.policy_bank("deep_dig", "ore_strike"), sim.policy_click(["seam_cut", "gas_bump"])),
        ("pulse_vs_dig", ("kodiax", "hopoch"), ("forkrook", "crackett"),
         sim.policy_bank("deep_dig", "ore_strike"), sim.policy_click(["baseline_shot", "baseline_jab"])),
        ("skim_blade_vs_dig", ("kodiax", "hopoch"), ("kitsauto", "deghoul"),
         sim.policy_commit("deep_dig"), policy_skim_then(["blade_flurry", "gas_bump"])),
        ("two_fast_hits", ("kodiax", "hopoch"), ("crackett", "kitsauto"),
         sim.policy_commit("deep_dig"), sim.policy_click(["seam_cut", "blade_flurry"])),
        ("cinder_vs_dig", ("kodiax", "hopoch"), ("cinislug", "deghoul"),
         sim.policy_bank("deep_dig", "ore_strike"), sim.policy_click(["cinder_burst", "gas_bump"])),
        ("ore_rider_vs_hammer", ("validape", "hopoch"), ("kodiax", "crackett"),
         sim.policy_commit("hammer_sync"), sim.policy_click(["ore_strike", "seam_cut"])),
        ("leak_vs_hammer", ("validape", "hopoch"), ("keyper", "kitsauto"),
         sim.policy_commit("hammer_sync"), sim.policy_click(["memory_leak", "cipher_shot", "packet_skim", "blade_flurry"])),
    ]
    # policy_click and policy_bank are shared across both mons on a side. That is
    # intentional for these scripted lines: each mon clicks the first move it owns.
    scripted = {"baseline": {}, "stack": {}}
    for name, side_h, side_f, pol_h, pol_f in scenarios:
        for stack, key in ((False, "baseline"), (True, "stack")):
            # Policies close over move ids, not over MOVES. Build fresh each call.
            pol_heavy = pol_h if name != "skim_blade_vs_dig" else sim.policy_commit("deep_dig")
            scripted[key][name] = run_script(
                name, side_h, side_f, pol_heavy, pol_f, stack, script_trials, seed + 9000, 14)

    # Wall lines. Attacker is team 0.
    wall_specs = {
        "ember_vs_ore": (("kodiax",), ("cinislug",), sim.policy_click(["ore_strike"]), sim.policy_click(["ember_wall", "cinder_burst"])),
        "blockade_vs_ore": (("kodiax",), ("blockwyrm",), sim.policy_click(["ore_strike"]), sim.policy_click(["blockade", "bulwark_blow", "stone_barrage"])),
        "ember_control_no_wall": (("crackett",), ("cinislug", "blockwyrm"), sim.policy_click(["seam_cut"]), sim.policy_click(["cinder_burst", "stone_barrage"])),
    }
    walls = {"baseline": {}, "stack": {}}
    for name, (a, b, pa, pb) in wall_specs.items():
        for stack, key in ((False, "baseline"), (True, "stack")):
            # run_script treats team 0 as heavy. Here team 0 is the attacker.
            row = run_script(name, a, b, pa, pb, stack, script_trials, seed + 12000, 18)
            walls[key][name] = dict(
                attacker_pct=row["heavy_pct"],
                waller_pct=row["fast_pct"],
                cap_pct=row["cap_pct"],
                length=row["length"],
            )

    def species_top(counter, n=3):
        grouped = {}
        for (sid, mid), count in counter.items():
            grouped.setdefault(sid, Counter())[mid] = count
        out = {}
        for sid, c in grouped.items():
            tot = sum(c.values()) or 1
            out[sid] = [dict(move=mid, n=cnt, share=round(100.0 * cnt / tot, 1)) for mid, cnt in c.most_common(n)]
        return out

    species_detail = {"baseline": species_top(species["base"]), "stack": species_top(species["stack"])}
    base_best = best_by_species(species["base"])
    stack_best = best_by_species(species["stack"])
    changed = []
    for sid in sorted(set(base_best) | set(stack_best)):
        b = base_best.get(sid, {}).get("move")
        s = stack_best.get(sid, {}).get("move")
        if b and s and b != s:
            changed.append(dict(
                species=sid,
                baseline=b,
                baseline_share=base_best[sid]["share"],
                stack=s,
                stack_share=stack_best[sid]["share"],
            ))

    out_of_band = []
    for t, row in types.items():
        outside = row["stack"] < 35.0 or row["stack"] > 65.0
        was_inside = 35.0 <= row["baseline"] <= 65.0
        pushed = abs(row["stack"] - 50.0) > abs(row["baseline"] - 50.0) + 7.0
        if outside and abs(row["delta"]) >= 8.0 and (was_inside or pushed):
            out_of_band.append(dict(type=t, stack=row["stack"], baseline=row["baseline"], delta=row["delta"],
                                    was_inside=was_inside))

    return dict(
        seed=seed,
        field_trials=field_trials,
        type_trials=type_trials,
        script_trials=script_trials,
        archetypes=archetypes,
        types=types,
        length=dict(baseline=mean_len("base"), stack=mean_len("stack")),
        ap=dict(baseline=mean_ap("base"), stack=mean_ap("stack")),
        block_share=dict(baseline=mean_block("base"), stack=mean_block("stack")),
        picks=dict(baseline=top_picks(picks["base"], BASE_MOVES), stack=top_picks(picks["stack"], STACK_MOVES)),
        species_top=species_detail,
        best_changes=changed,
        out_of_band=out_of_band,
        scripted=scripted,
        walls=walls,
        calendars=build_calendars(),
        formula=formula_pack(),
    )


def main():
    field = int(os.environ.get("STACK_FIELD", "80"))
    types_n = int(os.environ.get("STACK_TYPES", "40"))
    scripts = int(os.environ.get("STACK_SCRIPTS", "200"))
    result = campaign(field, types_n, scripts, SEED)
    # Counters are not in the returned dict. json-safe already.
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"wrote {OUT}")
    print("archetypes", json.dumps(result["archetypes"]))
    print("types", json.dumps(result["types"]))
    print("length", result["length"], "ap", result["ap"], "block", result["block_share"])
    print("changes", result["best_changes"])
    print("band", result["out_of_band"])
    print("tempo", json.dumps(result["scripted"]))
    print("walls", json.dumps(result["walls"]))
    print("cal", json.dumps(result["calendars"]))
    print("formula", json.dumps(result["formula"]))


if __name__ == "__main__":
    main()
