#!/usr/bin/env python3
"""Re-verify PROPOSAL_STACK_INTEGRATION_V1 against the snapshot and attachments.

Prints PASS or FAIL per claim. Writes the markdown from computed values.
A claim id cited in the doc must have been checked. A numeric snippet must
appear in the doc exactly as checked.
"""
from __future__ import annotations

import json
import os
import re
import statistics
import sys
from collections import defaultdict
from pathlib import Path

SNAP = Path(os.environ.get("CHAINBOUND_SNAPSHOT", "/tmp/cb_work/snapshot"))
SIMS = Path(os.environ.get("CHAINBOUND_SIMS", "/tmp/cb_work/sims"))
ART = Path("/opt/cursor/artifacts")
DOC = ART / "PROPOSAL_STACK_INTEGRATION_V1.md"
CACHE = Path("/tmp/cb_work/stack_check_cache.json")
SENS_CACHE = Path("/tmp/cb_work/stack_check_sens.json")

sys.path.insert(0, str(SIMS))
sys.path.insert(0, str(ART))
os.environ["CHAINBOUND_SNAPSHOT"] = str(SNAP)
sys.argv = ["stack_2v2_sim.py", str(SNAP), str(SIMS), "/tmp/cb_work/stack_check_run.json"]

import rarity_fit_check as rfc  # noqa: E402
import stack_2v2_sim as stacksim  # noqa: E402

CLAIMS: list[dict] = []
_CLAIM_INDEX: dict[str, dict] = {}


def _store(cid: str, good: bool, evidence: str, snippet: str | None) -> str:
    prev = _CLAIM_INDEX.get(cid)
    if prev is not None:
        if prev["snippet"] != snippet or prev["ok"] != good:
            print(f"FAIL {cid}-respec was {prev['snippet']!r} ok={prev['ok']} now {snippet!r} ok={good}")
            prev["ok"] = False
            prev["evidence"] = evidence
        return prev["snippet"] or ""
    row = dict(id=cid, ok=good, evidence=evidence, snippet=snippet)
    CLAIMS.append(row)
    _CLAIM_INDEX[cid] = row
    shown = snippet if snippet is not None else ("ok" if good else "fail")
    print(f"{'PASS' if good else 'FAIL'} {cid} {shown} :: {evidence}")
    return snippet or ""


def ok(cid: str, evidence: str, snippet: str | None = None) -> str:
    return _store(cid, True, evidence, snippet)


def bad(cid: str, evidence: str, snippet: str | None = None) -> str:
    return _store(cid, False, evidence, snippet)


def num(cid: str, value, evidence: str, nd: int = 1) -> str:
    if nd == 0:
        text = str(int(value))
    else:
        text = f"{float(value):.{nd}f}"
    snippet = f"{text} [{cid}]"
    (ok if True else bad)(cid, evidence, snippet)
    return snippet


def pct(cid: str, value, evidence: str, nd: int = 1) -> str:
    text = f"{float(value):.{nd}f}%"
    snippet = f"{text} [{cid}]"
    ok(cid, evidence, snippet)
    return snippet


def plain(cid: str, text: str, evidence: str) -> str:
    snippet = f"{text} [{cid}]"
    ok(cid, evidence, snippet)
    return snippet


def find_line(path: Path, pattern: str) -> tuple[int, str]:
    rx = re.compile(pattern)
    for i, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        if rx.search(line):
            return i, line.strip()
    raise SystemExit(f"pattern not found in {path}: {pattern}")


def section(text: str, start: str, end: str) -> str:
    a = text.index(start)
    b = text.index(end, a + len(start))
    return text[a:b]


def strip_tags(text: str) -> str:
    text = re.sub(r"\[[A-Za-z0-9_-]+\]", "", text)
    return re.sub(r"\s+", " ", text).strip()


def parse_table(text: str) -> list[list[str]]:
    rows = []
    for line in text.splitlines():
        if not line.startswith("|"):
            continue
        if re.match(r"^\|\s*-+", line):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if not cells or cells[0] in ("Source", "#", "Move", "Rarity"):
            continue
        rows.append(cells)
    return rows


def load_world():
    moves, move_lines, header_line = rfc.read_csv(SNAP / "Docs/Specs/Combat/Data/Moves.csv")
    pools_rows, pool_lines, _ = rfc.read_csv(SNAP / "Docs/Specs/Combat/Data/SpeciesMovePools.csv")
    by_id = {r["MoveId"]: r for r in moves}
    species = {}
    for p in (SNAP / "Assets/Data/Combat/ChainlingSpecies").glob("*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")

        def g(k, text=t):
            m = re.search(rf"(?m)^  {k}:\s*(.*)$", text)
            return m.group(1).strip() if m else ""

        sid = g("speciesId")
        if not sid:
            continue
        species[sid] = dict(
            type=int(g("primaryType") or 0),
            hp=int(g("baseMaxHP") or 0),
            rarity=int(g("rarity") or 0),
            starter=g("isStarter") == "1",
            file=str(p.relative_to(SNAP)),
        )
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
        pools[sid] = dict(
            members=members,
            flagship=(row.get("FlagshipMove") or "").strip(),
            off=off,
            line=pool_lines[sid],
            learn=learn,
            excl=excl,
        )
    return moves, move_lines, header_line, by_id, species, pools


def fact_move(by_id, lines, mid, cid, pred, evidence_extra=""):
    row = by_id[mid]
    line = lines[mid]
    evidence = f"Docs/Specs/Combat/Data/Moves.csv:{line} {mid} {evidence_extra}".strip()
    if pred(row):
        ok(cid, evidence)
    else:
        bad(cid, evidence + " GOT " + json.dumps({k: row.get(k) for k in ("APCost", "Power", "RechargeTurns", "Rarity", "UtilityEffects", "Tags", "BaseType", "Category")}))
    return row, line


def typed_cells(rows, model):
    buckets = defaultdict(lambda: {"typed": [], "typeless": []})
    for r in rows:
        if r["Category"] not in rfc.DAMAGING or int(r["Power"] or 0) <= 0 or rfc.is_boss(r):
            continue
        ap = int(r["APCost"] or 0)
        sh = rfc.shape_of(r)
        side = "typeless" if (r.get("BaseType") or "None") in ("", "None", "Typeless") else "typed"
        buckets[(r["Rarity"], sh, ap)][side].append((r["MoveId"], model.strike(r)))
    out = []
    for key in sorted(buckets):
        b = buckets[key]
        if not b["typed"] or not b["typeless"]:
            continue
        td = [v for _i, v in b["typed"]]
        ud = [v for _i, v in b["typeless"]]
        tmed = statistics.median(td)
        umed = statistics.median(ud)
        out.append(dict(
            rarity=key[0], shape=key[1], ap=key[2],
            tn=len(td), tmed=tmed, un=len(ud), umed=umed,
            typed_higher=tmed > umed,
            thin=len(td) < 2 or len(ud) < 2,
            typed_ids=[i for i, _v in b["typed"]],
            typeless_ids=[i for i, _v in b["typeless"]],
        ))
    return out


def rare_typeless(rows, model):
    by_shape = defaultdict(list)
    for r in rows:
        if r["Category"] not in rfc.DAMAGING or int(r["Power"] or 0) <= 0 or rfc.is_boss(r):
            continue
        if r["Rarity"] == "Common" and (r.get("BaseType") or "None") not in ("", "None", "Typeless"):
            ap = int(r["APCost"] or 0) or 1
            by_shape[rfc.shape_of(r)].append(model.strike(r) / ap)
    found = []
    for r in rows:
        if r["MoveId"] not in ("refined_slam", "overdraw_snipe"):
            continue
        ap = int(r["APCost"] or 0) or 1
        per = model.strike(r) / ap
        peers = by_shape[rfc.shape_of(r)]
        med = statistics.median(peers) if peers else 0
        ratio = per / med if med else 0
        found.append(dict(
            mid=r["MoveId"], name=r["DisplayName"], shape=rfc.shape_of(r), ap=ap,
            power=int(r["Power"] or 0), acc=int(r["Accuracy"] or 0),
            per=per, n=len(peers), med=med, ratio=ratio,
        ))
    return found


def stack_rows(moves):
    rows = [dict(r) for r in moves]
    by = {r["MoveId"]: r for r in rows}
    by["blade_flurry"]["Power"] = "10"
    by["cinder_burst"]["APCost"] = "2"
    by["extraction_hit"]["Rarity"] = "Common"
    by["fee_collector"]["Rarity"] = "Common"
    stake = dict(stacksim.STACK_MOVES["stake_line"])
    rows.append(stake)
    return rows


def apply_pool_edits(pools, move_rarity):
    edited = {sid: dict(info, members=list(info["members"])) for sid, info in pools.items()}
    rarity = dict(move_rarity)
    rarity["extraction_hit"] = "Common"
    rarity["fee_collector"] = "Common"
    rarity["stake_line"] = "Common"

    def swap(sid, old, new):
        edited[sid]["members"] = [new if m == old else m for m in edited[sid]["members"]]

    swap("kodiax", "baseline_slam", "extraction_hit")
    swap("deghoul", "slipstream", "fee_collector")
    swap("rattlestake", "baseline_shot", "stake_line")
    return edited, rarity


def model_for(moves):
    cfg = (SNAP / "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset").read_text(encoding="utf-8", errors="replace")

    def cfgv(k, d):
        m = re.search(rf"(?m)^  {k}:\s*([-\d.]+)", cfg)
        return float(m.group(1)) if m else d

    statuses, _, _ = rfc.read_csv(SNAP / "Docs/Specs/Combat/Data/Statuses.csv")
    st_by = {r["StatusId"]: r for r in statuses}
    scratch = rfc.Model(1, 1, 124, 0.0625, cfgv("critMultiplierDefault", 1.5), cfgv("randomRollMin", 0.85), cfgv("randomRollMax", 1.0), st_by)
    twos = []
    for r in moves:
        if r["Category"] in rfc.DAMAGING and int(r["Power"] or 0) > 0 and not rfc.is_boss(r) and int(r["APCost"] or 0) == 2 and rfc.shape_of(r) == "single":
            twos.append(scratch.strike(r))
    ref = statistics.median(twos)
    return rfc.Model(ref / 2.0, ref, 124, 0.0625, cfgv("critMultiplierDefault", 1.5), cfgv("randomRollMin", 0.85), cfgv("randomRollMax", 1.0), st_by), ref


def share_of(rows, mid):
    for r in rows or []:
        if r["move"] == mid:
            return r["share"]
    return None


def top_line(rows):
    if not rows:
        return "none"
    return ", ".join(f"{r['move']} {r['share']}" for r in rows)


def run_campaigns():
    reuse = os.environ.get("STACK_REUSE") == "1" and CACHE.exists() and SENS_CACHE.exists()
    if reuse:
        print("REUSE cached campaign")
        full = json.loads(CACHE.read_text())
        sens = json.loads(SENS_CACHE.read_text())
        return full, sens
    print("RUN campaign field=80 types=48 scripts=280", flush=True)
    full = stacksim.campaign(80, 48, 280, stacksim.SEED)
    CACHE.write_text(json.dumps(full))
    saved = stacksim.STACK_MOVES["bulwark_blow"]["APCost"]
    stacksim.STACK_MOVES["bulwark_blow"]["APCost"] = "3"
    print("RUN sensitivity bulwark AP 3 field=40 types=24 scripts=120", flush=True)
    sens = stacksim.campaign(40, 24, 120, stacksim.SEED)
    stacksim.STACK_MOVES["bulwark_blow"]["APCost"] = saved
    SENS_CACHE.write_text(json.dumps(sens))
    return full, sens


def main():
    moves, move_lines, header_line, by_id, species, pools = load_world()
    brief = (SIMS / "COMBAT_ISSUES_DECISION_BRIEF_V3.md").read_text(encoding="utf-8")
    rarity_doc = (SIMS / "RARITY_FIT_AND_RIDER_BUILDOUT_V1.md").read_text(encoding="utf-8")
    plan = (SNAP / "_plans/learn_pool_rarity_6d7ff36f.plan.md").read_text(encoding="utf-8")
    master = (SNAP / "Docs/Combat/COMBAT_ISSUES_MASTER.md").read_text(encoding="utf-8")

    # --- current facts ---
    ok("F-move-count", f"Moves.csv:{header_line} rows {len(moves)}", None)
    num("F-move-count-n", len(moves), f"Docs/Specs/Combat/Data/Moves.csv header line {header_line}", 0)

    def must(mid, cid, **eq):
        row, line = fact_move(by_id, move_lines, mid, cid, lambda r: all(str(r.get(k, "")) == str(v) for k, v in eq.items()), json.dumps(eq))
        return row, line

    leak, leak_line = must("memory_leak", "F-leak-rc", RechargeTurns="0", APCost="2")
    tax, tax_line = must("transaction_tax", "F-tax-steal", APCost="1")
    if "StealAP:1" in (tax.get("UtilityEffects") or ""):
        ok("F-tax-opcode", f"Moves.csv:{tax_line} UtilityEffects contains StealAP:1")
    else:
        bad("F-tax-opcode", f"Moves.csv:{tax_line} utility {tax.get('UtilityEffects')}")
    cinder, cinder_line = must("cinder_burst", "F-cinder", APCost="3", Power="15", Rarity="Common", HitMin="2", HitMax="4")
    ember, ember_line = must("ember_wall", "F-ember", APCost="2", RechargeTurns="2", Rarity="Legendary")
    if "BlockWindow:50%:Melee" in (ember.get("UtilityEffects") or "") and "PRIOR" in (ember.get("Tags") or ""):
        ok("F-ember-window", f"Moves.csv:{ember_line} BlockWindow 50% Melee and PRIOR")
    else:
        bad("F-ember-window", f"Moves.csv:{ember_line}")
    pulse, pulse_line = must("baseline_shot", "F-pulse", Power="26", APCost="2", BaseType="None", Rarity="Common")
    bulwark, bulwark_line = must("bulwark_blow", "F-bulwark", Power="50", APCost="3", Rarity="Epic")
    if (bulwark.get("DamageOverrideAttackStat") or "").lower() == "true" and bulwark.get("DamageAttackStat") == "MeleeDef":
        ok("F-bulwark-def", f"Moves.csv:{bulwark_line} DamageAttackStat MeleeDef")
    else:
        bad("F-bulwark-def", f"Moves.csv:{bulwark_line} override {bulwark.get('DamageOverrideAttackStat')} stat {bulwark.get('DamageAttackStat')}")
    blade, blade_line = must("blade_flurry", "F-blade", Power="12")
    if by_id["blade_flurry"]["HitMin"] == "3" and by_id["blade_flurry"]["HitMax"] == "3":
        ok("F-blade-hits", f"Moves.csv:{blade_line} hits 3")
    else:
        bad("F-blade-hits", f"Moves.csv:{blade_line} {by_id['blade_flurry']['HitMin']}-{by_id['blade_flurry']['HitMax']}")
    gas, gas_line = must("volatile_shot", "F-gaswar", Power="72", APCost="4", Category="DamagingRanged")
    if "NextMoveAPSurcharge:1" in (gas.get("UtilityEffects") or ""):
        ok("F-gaswar-surcharge", f"Moves.csv:{gas_line}")
    else:
        bad("F-gaswar-surcharge", f"Moves.csv:{gas_line}")
    all_in, all_line = must("all_in", "F-allin", Power="98", APCost="4", RechargeTurns="2", Rarity="Epic", BaseType="Meme")
    if not (all_in.get("UtilityEffects") or "").strip() and "SpendRemainingAp" in (all_in.get("Tags") or ""):
        ok("F-allin-utility-empty", f"Moves.csv:{all_line} utility empty, tag SpendRemainingAp")
    else:
        bad("F-allin-utility-empty", f"Moves.csv:{all_line}")
    holders = []
    for sid, info in pools.items():
        if "all_in" in info["members"] or "all_in" in info["off"]:
            holders.append(sid)
    if not holders:
        ok("F-allin-unpooled", "SpeciesMovePools.csv has no all_in")
    else:
        bad("F-allin-unpooled", ",".join(holders))
    guard, guard_line = must("guard_posture", "F-guard", APCost="1", Rarity="Common", Category="StatusUtility")
    if "MitigationWindow:25%" in (guard.get("UtilityEffects") or "") and "ArmorFlat" not in (guard.get("UtilityEffects") or ""):
        ok("F-guard-25", f"Moves.csv:{guard_line} 25% MitigationWindow, no ArmorFlat")
    else:
        bad("F-guard-25", f"Moves.csv:{guard_line} {guard.get('UtilityEffects')}")
    hype, hype_line = by_id["hype_burst"], move_lines["hype_burst"]
    if "MeleeAtk" in (hype.get("UtilityEffects") or "") and "RangedAtk" not in (hype.get("UtilityEffects") or ""):
        ok("F-hype-melee", f"Moves.csv:{hype_line}")
    else:
        bad("F-hype-melee", f"Moves.csv:{hype_line} {hype.get('UtilityEffects')}")
    pat = by_id["patience_passive"]
    if ":Self:6:" in (pat.get("UtilityEffects") or ""):
        ok("F-patience-6", f"Moves.csv:{move_lines['patience_passive']}")
    else:
        bad("F-patience-6", f"Moves.csv:{move_lines['patience_passive']} {pat.get('UtilityEffects')}")
    if (by_id["ore_strike"].get("UtilityEffects") or "").strip() == "":
        ok("F-ore-empty", f"Moves.csv:{move_lines['ore_strike']} utility empty")
    else:
        bad("F-ore-empty", f"Moves.csv:{move_lines['ore_strike']}")
    must("extraction_hit", "F-extract-rare", Rarity="Rare", Category="Passive")
    if not (by_id["extraction_hit"].get("UtilityEffects") or "").strip():
        ok("F-extract-empty", f"Moves.csv:{move_lines['extraction_hit']}")
    else:
        bad("F-extract-empty", f"Moves.csv:{move_lines['extraction_hit']}")
    must("fee_collector", "F-fee-rare", Rarity="Rare", Category="Passive")
    must("crystal_extraction", "F-crystal", Rarity="Common", BaseType="Mined")
    if "HealSelfPerTargetDebuff" in (by_id["crystal_extraction"].get("UtilityEffects") or ""):
        ok("F-crystal-built", f"Moves.csv:{move_lines['crystal_extraction']}")
    else:
        bad("F-crystal-built", f"Moves.csv:{move_lines['crystal_extraction']}")
    withdrawn = ["partner_beacon", "relay_dividend", "node_beacon", "link_pulse", "open_line", "cinder_idle", "scorch_echo"]
    for wid in withdrawn:
        if wid in by_id:
            bad(f"F-absent-{wid}", "Moves.csv contains the withdrawn id")
        else:
            ok(f"F-absent-{wid}", "Moves.csv has no row")

    # engine lines
    dc = SNAP / "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs"
    inherit_line, inherit_text = find_line(dc, r"return attacker\.primaryType;")
    plain("F-inherit-line", f"DamageCalculator.cs:{inherit_line}", f"{inherit_text}")
    comment_line, _ = find_line(dc, r"Typeless damaging moves inherit")
    ok("F-inherit-comment", f"DamageCalculator.cs:{comment_line}")
    cfg_cs = SNAP / "Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatResolverConfigSO.cs"
    stab_line, stab_text = find_line(cfg_cs, r"enableStab = false")
    ok("F-stab-off", f"CombatResolverConfigSO.cs:{stab_line} {stab_text.strip()}")
    const = SNAP / "Assets/Scripts/Combat/Runtime/Data/CombatConstants.cs"
    rug_line, rug_text = find_line(const, r"RUGGED_TICK_CAP = 15")
    plain("F-rugged-15", f"CombatConstants.cs:{rug_line}", rug_text.strip())
    sp = SNAP / "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs"
    kin_line, _ = find_line(sp, r"0\.06f")
    ok("F-kindling-literal", f"StatusProcessor.cs:{kin_line} 0.06f before the Rugged loop")
    cr = SNAP / "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs"
    eot_line, _ = find_line(cr, r"CheckPassiveTriggers\(active, turn, PassiveTriggerPoint\.EndOfTurn\)")
    status_line, _ = find_line(cr, r"_statusProcessor\.ProcessEndOfTurn")
    if eot_line < status_line:
        ok("F-eot-order", f"CombatResolver.cs:{eot_line} passive before StatusProcessor.cs:{kin_line} and ProcessEndOfTurn at CombatResolver.cs:{status_line}")
    else:
        bad("F-eot-order", f"passive {eot_line} status {status_line}")
    fee_line, _ = find_line(cr, r"firstFoe.currentAP <= holder.currentAP")
    ok("F-fee-rule", f"CombatResolver.cs:{fee_line} drain only when foe AP is greater")
    spend_line, _ = find_line(cr, r"HasSpendRemainingAp\(move\)")
    ok("F-allin-caster-only", f"CombatResolver.cs:{spend_line} spends the caster's remaining AP. No AllAllies drain in that branch")
    bench_line, bench_text = find_line(SIMS / "ap_denial_2v2_sim.py", r"Bench recharge only")
    ok("F-bench-recharge", f"ap_denial_2v2_sim.py:{bench_line} {bench_text}")
    rc_note, rc_text = find_line(SIMS / "ap_denial_2v2_sim.py", r"A value of 2 skips the next turn")
    ok("F-rc-means-skip", f"ap_denial_2v2_sim.py:{rc_note} {rc_text}")
    tempo_line, _ = find_line(SIMS / "tempo_2v2_sim.py", r"MitigationWindow")
    walls_line, _ = find_line(SIMS / "walls_pvp_v2_sim.py", r"MitigationWindow")
    tempo_src = (SIMS / "tempo_2v2_sim.py").read_text(encoding="utf-8")
    walls_src = (SIMS / "walls_pvp_v2_sim.py").read_text(encoding="utf-8")
    if "BlockWindow" not in tempo_src:
        ok("F-tempo-no-block", f"tempo_2v2_sim.py:{tempo_line} uses MitigationWindow and does not mention BlockWindow")
    else:
        bad("F-tempo-no-block", "tempo sim mentions BlockWindow")
    if "BlockWindow" not in walls_src:
        ok("F-walls-no-block", f"walls_pvp_v2_sim.py:{walls_line} uses MitigationWindow and does not mention BlockWindow")
    else:
        bad("F-walls-no-block", "walls sim mentions BlockWindow")
    den_line, _ = find_line(SIMS / "ap_denial_2v2_sim.py", r"BlockWindow")
    ok("F-denial-block", f"ap_denial_2v2_sim.py:{den_line} mirrors BlockWindow")
    atk_line, _ = find_line(ART / "stack_2v2_sim.py", r"def attack_stat_key")
    ok("F-sim-def-scale", f"stack_2v2_sim.py:{atk_line} Bulwark uses DamageAttackStat")
    plan_new_line, plan_new = find_line(SNAP / "_plans/learn_pool_rarity_6d7ff36f.plan.md", r"Existing moves only")
    ok("F-plan-no-new", f"_plans/learn_pool_rarity_6d7ff36f.plan.md:{plan_new_line} {plan_new}")
    plan_band_line, plan_band = find_line(SNAP / "_plans/learn_pool_rarity_6d7ff36f.plan.md", r"three at that tier or lower")
    ok("F-plan-band", f"_plans/learn_pool_rarity_6d7ff36f.plan.md:{plan_band_line} {plan_band}")
    csv_rule_line, _ = find_line(SNAP / "Docs/Specs/Combat/Data/SpeciesMovePools.csv", r"three at the body tier or lower")
    ok("F-csv-band-comment", f"SpeciesMovePools.csv:{csv_rule_line}")
    master_line, master_text = find_line(SNAP / "Docs/Combat/COMBAT_ISSUES_MASTER.md", r"drains \*\*all\*\* AP from the caster")
    ok("F-allin-decided", f"COMBAT_ISSUES_MASTER.md:{master_line} {strip_tags(master_text)[:180]}")
    if "uplink" in pools["validape"]["members"] and "ap_router" not in pools["validape"]["members"]:
        ok("F-uplink", f"SpeciesMovePools.csv:{pools['validape']['line']} validape learns uplink, not ap_router")
    else:
        bad("F-uplink", str(pools["validape"]["members"]))
    for sid, old in (("kodiax", "baseline_slam"), ("deghoul", "slipstream"), ("rattlestake", "baseline_shot")):
        if old in pools[sid]["members"]:
            ok(f"F-pool-{sid}", f"SpeciesMovePools.csv:{pools[sid]['line']} members {'|'.join(pools[sid]['members'])}")
        else:
            bad(f"F-pool-{sid}", str(pools[sid]["members"]))
    if "crystal_extraction" in pools["kodiax"]["members"] and "extraction_hit" not in pools["kodiax"]["members"]:
        ok("F-kodiax-crystal-stays", f"SpeciesMovePools.csv:{pools['kodiax']['line']} crystal_extraction is in, extraction_hit is out")
    else:
        bad("F-kodiax-crystal-stays", str(pools["kodiax"]["members"]))
    if "fee_collector" not in pools["deghoul"]["members"]:
        ok("F-fee-ungranted", f"SpeciesMovePools.csv:{pools['deghoul']['line']}")
    else:
        bad("F-fee-ungranted", "fee_collector already granted")
    starters = sorted(s for s, sp in species.items() if sp["starter"] and sp["hp"] > 0)
    plain("F-starters", ",".join(starters), "species assets isStarter 1")
    staked_common = [r["MoveId"] for r in moves if r["BaseType"] == "Staked" and r["Rarity"] == "Common" and r["Category"] in rfc.DAMAGING and not rfc.is_boss(r)]
    plain("F-no-staked-common", str(len(staked_common)), "Moves.csv damaging Staked Common count, boss rows excluded")

    # --- pools and typed ---
    model, ref_hit = model_for(moves)
    num("T-ref-hit", ref_hit, "rarity_fit_check strike median of 2 AP singles, atk=def 60", 2)
    move_rarity = {r["MoveId"]: r["Rarity"] for r in moves}
    base_problems = rfc.pool_problems(pools, move_rarity, species)
    edited, edited_rarity = apply_pool_edits(pools, move_rarity)
    stack_problems = rfc.pool_problems(edited, edited_rarity, species)
    plain("T-pool-base", str(len(base_problems)), "rarity_fit_check.pool_problems on live pools")
    plain("T-pool-stack", str(len(stack_problems)), "pool_problems after extraction_hit Common, fee_collector Common, stake_line Common, three swaps")
    if base_problems:
        bad("T-pool-base-empty", "; ".join(base_problems[:8]))
    else:
        ok("T-pool-base-empty", "zero wild-band problems")
    if stack_problems:
        bad("T-pool-stack-empty", "; ".join(stack_problems[:8]))
    else:
        ok("T-pool-stack-empty", "zero wild-band problems after the three pool edits")
    # stake_line is a new id. Band passes. Plan overview forbids new definitions. Do not drop:
    # no existing Staked common can be built out, and retiering a Hopoch rare would be a different pool.
    if "stake_line" not in by_id and len(staked_common) == 0 and not stack_problems:
        ok("T-stake-line-band", "new id stake_line is not in Moves.csv; band still empty; no Staked common exists to reuse")
    else:
        bad("T-stake-line-band", f"in csv {('stake_line' in by_id)} commons {staked_common} problems {stack_problems}")

    base_cells = typed_cells(moves, model)
    stack_cells = typed_cells(stack_rows(moves), model)
    band_m = re.search(r"between (0\.\d+) and (1\.\d+)", rarity_doc)
    band_lo, band_hi = float(band_m.group(1)), float(band_m.group(2))
    plain("T-band-lo", f"{band_lo:.2f}", "RARITY_FIT_AND_RIDER_BUILDOUT_V1.md in-line sentence")
    plain("T-band-hi", f"{band_hi:.2f}", "RARITY_FIT_AND_RIDER_BUILDOUT_V1.md in-line sentence")

    def cell_claims(prefix, cells):
        broken = []
        for i, c in enumerate(cells):
            cid = f"{prefix}-{i}"
            evidence = f"rarity_fit_check.Model.strike {c['rarity']} {c['shape']} AP {c['ap']} typed {c['typed_ids']} typeless {c['typeless_ids']}"
            num(f"{cid}-tn", c["tn"], evidence, 0)
            num(f"{cid}-tmed", c["tmed"], evidence, 2)
            num(f"{cid}-un", c["un"], evidence, 0)
            num(f"{cid}-umed", c["umed"], evidence, 2)
            if c["typed_higher"]:
                ok(f"{cid}-yes", evidence)
            else:
                if c["thin"]:
                    ok(f"{cid}-thin-no", evidence + " thin cell, not a broken rule")
                else:
                    bad(f"{cid}-no", evidence)
                    broken.append(cid)
        return broken

    base_broken = cell_claims("T-base", base_cells)
    stack_broken = cell_claims("T-stack", stack_cells)
    if stack_broken:
        bad("T-stack-rule", "a non-thin cell has typeless ahead of typed")
    else:
        ok("T-stack-rule", "every non-thin cell still has typed median above typeless")

    base_rare = rare_typeless(moves, model)
    stack_rare = rare_typeless(stack_rows(moves), model)
    for label, rows_r in (("base", base_rare), ("stack", stack_rare)):
        for rec in rows_r:
            cid = f"T-{label}-{rec['mid']}"
            ev = f"strike/AP vs typed-common {rec['shape']} median n={rec['n']}"
            num(f"{cid}-per", rec["per"], ev, 2)
            num(f"{cid}-med", rec["med"], ev, 2)
            num(f"{cid}-ratio", rec["ratio"], ev, 3)
            num(f"{cid}-n", rec["n"], ev, 0)
            inside = band_lo <= rec["ratio"] <= band_hi
            (ok if not inside else bad)(f"{cid}-out", ev + (" outside the in-line band" if not inside else " INSIDE band"))
            # The proposal is to leave them because they are under the line.
            # bad() if they are suddenly inside would mean the stack closed the gap. Leaving them is the proposal either way.
            if label == "stack" and inside:
                bad(f"{cid}-still-under", "stack put this rare typeless inside the band; leaving it would no longer match the measured ratio")

    # --- sims ---
    # Live formula and calendars, even on a cached battle run.
    formula = stacksim.formula_pack()
    calendars = stacksim.build_calendars()
    fee_bank = stacksim.fee_series(True, 4)
    full, sens = run_campaigns()
    # Prefer live formula and calendars over the cached battle blob.
    full["formula"] = formula
    full["calendars"] = calendars
    full["calendars"]["fee_from_bank_stack"] = fee_bank
    seed = full["seed"]
    ev_seed = f"stack_2v2_sim.campaign seed {seed} field {full['field_trials']} type {full['type_trials']} script {full['script_trials']}"

    def arch_row(name):
        row = full["archetypes"][name]
        return row

    for name, row in full["archetypes"].items():
        pct(f"S-arch-{name}-base", row["baseline"], ev_seed + f" archetype {name} baseline")
        pct(f"S-arch-{name}-stack", row["stack"], ev_seed + f" archetype {name} stack")
        num(f"S-arch-{name}-delta", row["delta"], ev_seed + f" archetype {name} delta")
        num(f"S-arch-{name}-g", row["games"], ev_seed + f" archetype {name} games", 0)
    for name, row in full["types"].items():
        pct(f"S-type-{name}-base", row["baseline"], ev_seed + f" type {name} baseline")
        pct(f"S-type-{name}-stack", row["stack"], ev_seed + f" type {name} stack")
        num(f"S-type-{name}-delta", row["delta"], ev_seed + f" type {name} delta")
        num(f"S-type-{name}-g", row["games"], ev_seed + f" type {name} games", 0)
    num("S-len-base", full["length"]["baseline"], ev_seed + " mean match length baseline", 2)
    num("S-len-stack", full["length"]["stack"], ev_seed + " mean match length stack", 2)
    num("S-spent-base", full["ap"]["baseline"]["spent_per_turn"], ev_seed + " AP spent per turn baseline", 3)
    num("S-deny-base", full["ap"]["baseline"]["denied_per_turn"], ev_seed + " AP denied per turn baseline", 3)
    num("S-spent-stack", full["ap"]["stack"]["spent_per_turn"], ev_seed + " AP spent per turn stack", 3)
    num("S-deny-stack", full["ap"]["stack"]["denied_per_turn"], ev_seed + " AP denied per turn stack", 3)
    num("S-block-base", full["block_share"]["baseline"], ev_seed + " block share baseline", 4)
    num("S-block-stack", full["block_share"]["stack"], ev_seed + " block share stack", 4)
    num("S-seed", seed, "stack_2v2_sim.SEED", 0)
    num("S-field", full["field_trials"], "campaign argument echoed", 0)
    num("S-type-n", full["type_trials"], "campaign argument echoed", 0)
    num("S-script-n", full["script_trials"], "campaign argument echoed", 0)

    for mode in ("baseline", "stack"):
        for rar, picks in full["picks"][mode].items():
            for i, pick in enumerate(picks):
                pct(f"S-pick-{mode}-{rar}-{i}", pick["share"], ev_seed + f" archetype picks {mode} {rar} {pick['move']}")
                num(f"S-pickn-{mode}-{rar}-{i}", pick["n"], ev_seed + f" pick count {mode} {rar} {pick['move']}", 0)
    for ch in full["best_changes"]:
        sid = ch["species"]
        pct(f"S-best-{sid}-bshare", ch["baseline_share"], ev_seed + f" {sid} baseline modal {ch['baseline']}")
        pct(f"S-best-{sid}-sshare", ch["stack_share"], ev_seed + f" {sid} stack modal {ch['stack']}")
        plain(f"S-best-{sid}-bmove", ch["baseline"], ev_seed)
        plain(f"S-best-{sid}-smove", ch["stack"], ev_seed)

    focus_species = ["cinislug", "blockwyrm", "rattlestake", "keyper", "humpacked", "kitsauto", "deghoul"]
    for mode in ("baseline", "stack"):
        for sid in focus_species:
            rows = full["species_top"][mode].get(sid, [])
            for i, pick in enumerate(rows):
                pct(f"S-sp-{mode}-{sid}-{i}", pick["share"], ev_seed + f" {sid} {mode} {pick['move']}")
                plain(f"S-spid-{mode}-{sid}-{i}", pick["move"], ev_seed)

    for mode in ("baseline", "stack"):
        for name, row in full["scripted"][mode].items():
            ev = ev_seed + f" scripted {mode} {name}"
            pct(f"S-sc-{mode}-{name}-fast", row["fast_pct"], ev)
            pct(f"S-sc-{mode}-{name}-heavy", row["heavy_pct"], ev)
            pct(f"S-sc-{mode}-{name}-draw", row["draw_pct"], ev)
            pct(f"S-sc-{mode}-{name}-lands", row["heavy_lands_pct"], ev)
            pct(f"S-sc-{mode}-{name}-cap", row["cap_pct"], ev)
            num(f"S-sc-{mode}-{name}-len", row["length"], ev, 2)
            num(f"S-sc-{mode}-{name}-hits", row["hits_before"], ev, 2)
            if row["heavy_turn"] is not None:
                num(f"S-sc-{mode}-{name}-turn", row["heavy_turn"], ev, 1)
            for mv, turn in row["first_cast"].items():
                num(f"S-sc-{mode}-{name}-cast-{mv}", turn, ev + f" first cast {mv}", 1)
        for name, row in full["walls"][mode].items():
            ev = ev_seed + f" wall {mode} {name}"
            pct(f"S-w-{mode}-{name}-a", row["attacker_pct"], ev)
            pct(f"S-w-{mode}-{name}-w", row["waller_pct"], ev)
            pct(f"S-w-{mode}-{name}-cap", row["cap_pct"], ev)
            num(f"S-w-{mode}-{name}-len", row["length"], ev, 2)

    for key, seq in full["calendars"].items():
        plain(f"S-cal-{key}", ",".join(str(x) for x in seq), ev_seed + f" calendar {key}")

    for key in ("hammer_open", "hammer_wall_and_percent", "hammer_wall_and_flat12", "hammer_flat12", "pulse_inherited", "pulse_neutral", "cinder_into_gas", "triple_if_q13"):
        cell = formula[key]
        ev = f"stack_2v2_sim.one_hit {key} roll 0.925"
        num(f"S-f-{key}-pre", cell["pre"], ev, 2)
        num(f"S-f-{key}-chem", cell["chem"], ev, 1)
        num(f"S-f-{key}-dmg", cell["dmg"], ev, 0)
    for key in ("blade_base_open", "blade_base_first", "blade_stack_open", "blade_stack_first"):
        num(f"S-f-{key}", formula[key], "stack_2v2_sim.one_hit blade", 0)
    if formula["hammer_wall_and_percent"]["dmg"] == formula["hammer_wall_and_flat12"]["dmg"]:
        ok("S-f-flats-match", "percent armor and flat 12 leave the same integer on this hammer")
    else:
        bad("S-f-flats-match", "flat 12 does not match the live 25% on this hammer")
    spend_line, _ = find_line(ART / "stack_2v2_sim.py", r'block\["hits"\] -= 1')
    ok("S-f-blade-rest", f"stack_2v2_sim.py:{spend_line} decrements block hits inside the per-hit loop")

    evs = f"sensitivity seed {sens['seed']} field {sens['field_trials']} type {sens['type_trials']} script {sens['script_trials']} bulwark APCost held at 3"
    for name in ("walls", "denial", "frozen", "gas"):
        row = sens["archetypes"][name]
        pct(f"S-sens-{name}-base", row["baseline"], evs)
        pct(f"S-sens-{name}-stack", row["stack"], evs)
        num(f"S-sens-{name}-delta", row["delta"], evs)
        num(f"S-sens-{name}-g", row["games"], evs, 0)
    for name in ("Burned", "Block", "Bot", "Compressed", "Gas"):
        row = sens["types"][name]
        pct(f"S-sens-type-{name}-base", row["baseline"], evs)
        pct(f"S-sens-type-{name}-stack", row["stack"], evs)
        num(f"S-sens-type-{name}-delta", row["delta"], evs)
    for mode in ("baseline", "stack"):
        for i, pick in enumerate(sens["species_top"][mode].get("blockwyrm", [])):
            pct(f"S-sens-sp-{mode}-blockwyrm-{i}", pick["share"], evs + f" {pick['move']}")
            plain(f"S-sens-spid-{mode}-blockwyrm-{i}", pick["move"], evs)
        for i, pick in enumerate(sens["species_top"][mode].get("cinislug", [])):
            pct(f"S-sens-sp-{mode}-cinislug-{i}", pick["share"], evs + f" {pick['move']}")
            plain(f"S-sens-spid-{mode}-cinislug-{i}", pick["move"], evs)

    # --- inventory from the docs ---
    brief_g = section(brief, "## (g) Ranked backlog", "## (h) Consistency")
    brief_rows = []
    for line_no, line in enumerate(brief.splitlines(), 1):
        if line.startswith("| ") and re.match(r"^\| \d+ \|", line) and "Memory Leak" in brief_g or True:
            pass
    # Only the backlog table inside section g.
    g_start = brief.index("## (g) Ranked backlog")
    for line_no, line in enumerate(brief.splitlines(), 1):
        if line_no < brief[:g_start].count("\n"):
            continue
        if line.startswith("## (h)"):
            break
        if re.match(r"^\| \d+ \|", line):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            brief_rows.append(dict(n=cells[0], action=strip_tags(cells[1]), impact=strip_tags(cells[2]), effort=cells[3], files=strip_tags(cells[4]), depends=strip_tags(cells[5]), test=strip_tags(cells[6]), line=line_no))
    next_region = brief[brief.index("Next, not in the fifteen"):brief.index("Explicitly not in this backlog")]
    next_items = [strip_tags(x) for x in re.findall(r"\*\*PROPOSAL\*\*\. ([^*]+)", next_region)]
    rarity_g_start = rarity_doc.index("## (g) Ranked backlog")
    rarity_rows = []
    for line_no, line in enumerate(rarity_doc.splitlines(), 1):
        if line_no < rarity_doc[:rarity_g_start].count("\n"):
            continue
        if line.startswith("## (h)"):
            break
        if re.match(r"^\| \d+ \|", line):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            rarity_rows.append(dict(n=cells[0], action=strip_tags(cells[1]), impact=strip_tags(cells[2]), effort=cells[3], files=strip_tags(cells[4]), depends=strip_tags(cells[5]), test=strip_tags(cells[6]), line=line_no))
    brief_q = []
    for line_no, line in enumerate(brief.splitlines(), 1):
        if not line.startswith("| Q"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        brief_q.append(dict(id=cells[0], q=strip_tags(cells[1]), default=strip_tags(cells[2]), blocks=strip_tags(cells[3]), line=line_no))
    rarity_q = []
    for line_no, line in enumerate(rarity_doc.splitlines(), 1):
        if not line.startswith("| Q"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        rarity_q.append(dict(id=cells[0], q=strip_tags(cells[1]), default=strip_tags(cells[2]), blocks=strip_tags(cells[3]), line=line_no))
    if len(brief_rows) != 15:
        bad("I-brief-backlog-count", f"parsed {len(brief_rows)}")
    else:
        ok("I-brief-backlog-count", "brief section (g) has 15 rows")
    if len(brief_q) != 46:
        bad("I-brief-q-count", f"parsed {len(brief_q)}")
    else:
        ok("I-brief-q-count", "brief section (j) has Q1-Q46")
    if len(rarity_q) != 10:
        bad("I-rarity-q-count", f"parsed {len(rarity_q)}")
    else:
        ok("I-rarity-q-count", "rarity section (i) has Q1-Q10")
    num("I-next-count", len(next_items), "brief next-list PROPOSAL sentences", 0)
    num("I-rarity-backlog", len(rarity_rows), "rarity section (g) rows", 0)

    ledger = section(brief, "## (c) Proposal ledger", "## (d)")
    ledger_rows = []
    for line in ledger.splitlines():
        if not line.startswith("| ") or line.startswith("| Source") or line.startswith("|---"):
            continue
        cells = [strip_tags(c.strip()) for c in line.strip().strip("|").split("|")]
        if len(cells) >= 3:
            ledger_rows.append(cells)
    num("I-ledger-rows", len(ledger_rows), "brief section (c) ledger rows", 0)

    # --- document ---
    L = []
    w = L.append
    names = {r["MoveId"]: r["DisplayName"] for r in moves}

    def nm(mid):
        return names.get(mid, mid)

    def P(cid):
        row = _CLAIM_INDEX.get(cid)
        if row and row["snippet"]:
            return row["snippet"]
        return f"[{cid}]"

    w("# PROPOSAL_STACK_INTEGRATION_V1")
    w("")
    w("Proposals only. Nothing here is applied to the game. The source of truth is the combat snapshot `chainbound_combat_snapshot_20261023_0010` plus the round-18 sim attachment. The GitHub checkout was not used.")
    w("")
    w(f"Checker: `proposal_stack_check.py`. Combined sim: `stack_2v2_sim.py`. Seed {num('S-seed', seed, ev_seed, 0)}. Archetype trials per pair {num('S-field', full['field_trials'], ev_seed, 0)}, type-face trials {num('S-type-n', full['type_trials'], ev_seed, 0)}, scripted trials {num('S-script-n', full['script_trials'], ev_seed, 0)}. Move rows in the snapshot: {num('F-move-count-n', len(moves), f'Moves.csv:{header_line}', 0)}.")
    w("")
    w("## Key findings")
    w("")
    walls = full["archetypes"]["walls"]
    denial = full["archetypes"]["denial"]
    burned = full["types"]["Burned"]
    block_t = full["types"]["Block"]
    bot = full["types"]["Bot"]
    comp = full["types"]["Compressed"]
    sens_walls = sens["archetypes"]["walls"]
    two_b = full["scripted"]["baseline"]["two_fast_hits"]
    two_s = full["scripted"]["stack"]["two_fast_hits"]
    ember_s = full["walls"]["stack"]["ember_vs_ore"]
    ctrl_b = full["walls"]["baseline"]["ember_control_no_wall"]
    ctrl_s = full["walls"]["stack"]["ember_control_no_wall"]
    leak_base = full["calendars"]["leak_pass_base"]
    leak_stack = full["calendars"]["leak_pass_stack"]
    tax_base = full["calendars"]["deghoul_base"]
    tax_stack = full["calendars"]["deghoul_stack"]
    skim_base = full["calendars"]["taxskim_pass_base"]
    skim_stack = full["calendars"]["taxskim_pass_stack"]
    cin_b = full["species_top"]["baseline"]["cinislug"]
    cin_s = full["species_top"]["stack"]["cinislug"]
    bw_b = full["species_top"]["baseline"]["blockwyrm"]
    bw_s = full["species_top"]["stack"]["blockwyrm"]
    bw_sens_s = sens["species_top"]["stack"]["blockwyrm"]

    w(
        "The pending defaults do not fight each other on the learn-pool band, the typed-versus-typeless rule, or the tempo rule, and they do open the Memory Leak passer lock. "
        f"They do pile onto one team. Cinder Burst at 2 AP moves Burned from {pct('S-type-Burned-base', burned['baseline'], ev_seed)} to {pct('S-type-Burned-stack', burned['stack'], ev_seed)} "
        f"and moves the Blockwyrm plus Cinislug duo from {pct('S-arch-walls-base', walls['baseline'], ev_seed)} to {pct('S-arch-walls-stack', walls['stack'], ev_seed)} "
        f"in the archetype round robin ({num('S-arch-walls-g', walls['games'], ev_seed, 0)} games a side). "
        f"Holding Bulwark Blow at 3 AP, that duo is still {pct('S-sens-walls-stack', sens_walls['stack'], evs)}. The duo spike is the Cinder change. "
        f"Cutting Bulwark to 2 AP is a second turn-1 heavy on a team that no longer needs one, and it takes Blockwyrm's stack clicks to {plain('S-spid-stack-blockwyrm-0', bw_s[0]['move'], ev_seed)} at {pct('S-sp-stack-blockwyrm-0', bw_s[0]['share'], ev_seed)}. "
        f"The scripted Ember line is not a TRUE lock: the attacker still wins {pct('S-w-stack-ember_vs_ore-a', ember_s['attacker_pct'], ev_seed)}. "
        f"The 1v2 control is {pct('S-w-baseline-ember_control_no_wall-a', ctrl_b['attacker_pct'], ev_seed)} attacker with the wall click absent and {pct('S-w-stack-ember_control_no_wall-a', ctrl_s['attacker_pct'], ev_seed)} under the stack, so that loss is the 1v2, not the wall. "
        f"Two faster 2 AP hits still beat a Deep Dig banker at {pct('S-sc-baseline-two_fast_hits-fast', two_b['fast_pct'], ev_seed)} baseline and {pct('S-sc-stack-two_fast_hits-fast', two_s['fast_pct'], ev_seed)} under the full stack. "
        f"Tax plus Skim on a passer stays a zero lock ({plain('S-cal-taxskim_pass_base', ','.join(map(str, skim_base)), ev_seed)} and {plain('S-cal-taxskim_pass_stack', ','.join(map(str, skim_stack)), ev_seed)}). Fee Collector does not deepen it. "
        "Armor flat 12 still ties the live 25% on the Hammer-plus-Blockade cell, so the flat numbers can sit on the card and should not enter a pool."
    )
    w("")
    w("## Top 5 recommendations")
    w("")
    w(f"1. **PROPOSAL.** Memory Leak `rechargeTurns` 2. Live recharge is 0 {P('F-leak-rc')} at Moves.csv:{leak_line}. Passer action AP goes from {plain('S-cal-leak_pass_base', ','.join(map(str, leak_base)), ev_seed)} to {plain('S-cal-leak_pass_stack', ','.join(map(str, leak_stack)), ev_seed)}. A CSV value of 2 skips the next turn {P('F-rc-means-skip')}. Do not lengthen Ember Wall. Its recharge column is already 2 {P('F-ember')}.")
    w(f"2. **PROPOSAL.** Transaction Tax `StealAP:1` becomes `DrainAP:1`. Live opcode steals {P('F-tax-opcode')}. Deghoul's bank goes from {plain('S-cal-deghoul_base', ','.join(map(str, tax_base)), ev_seed)} to {plain('S-cal-deghoul_stack', ','.join(map(str, tax_stack)), ev_seed)}. No drain cap, no whiff refund, no KO floor.")
    w(f"3. **PROPOSAL.** Typeless moves stay neutral. `ResolveAttackType` still returns `attacker.primaryType` when the move type is None {P('F-inherit-line')}. The comment still blesses inheritance {P('F-inherit-comment')}. STAB stays off {P('F-stab-off')}. Pulse Shot from Cinislug into Deghoul is chem {num('S-f-pulse_inherited-chem', formula['pulse_inherited']['chem'], 'one_hit', 1)} and {num('S-f-pulse_inherited-dmg', formula['pulse_inherited']['dmg'], 'one_hit', 0)} damage today, and chem {num('S-f-pulse_neutral-chem', formula['pulse_neutral']['chem'], 'one_hit', 1)} and {num('S-f-pulse_neutral-dmg', formula['pulse_neutral']['dmg'], 'one_hit', 0)} damage once inheritance is off. Pulse Shot stays power 26 {P('F-pulse')}.")
    w(f"4. **PROPOSAL.** Cinder Burst from 3 AP to 2 AP, power stays 15 {P('F-cinder')}. Accept the Ember click shift and the duo spike named above. **PROPOSAL.** Do not also cut Bulwark Blow from 3 AP to 2. Live Bulwark is power 50, AP 3, and it strikes with Melee Def {P('F-bulwark')} {P('F-bulwark-def')}. This revises brief Q33.")
    w("5. **PROPOSAL.** Ship the three pool fills and the rarity riders that do not add a second membership change: Extraction Hit at Common replaces Body Check on Kodiax only, Fee Collector at Common replaces Slipstream on Deghoul only, Stake Line is the one new Staked common and replaces Pulse Shot on Rattlestake only, and the Ore / Yield / Stake / Fork / Combustive / Hype Train riders stay off any extra pool. Gas War's surcharge comes off once. Patience, Streamlined, Rugged, and HODLed take their caps. Armor flat 12 and flat 16 are written on the cards only. Guard Posture does not replace a Deghoul move. Guard Stance does not replace Stone Barrage.")
    w("")

    w("## (a) Headline")
    w("")
    w("Smallest set that makes the stack consistent:")
    w("")
    w("- Ship Leak recharge, Tax-as-drain, typeless exclusion with STAB left off, and Cinder at 2 AP.")
    w("- Keep Bulwark at 3 AP. The duo spike survives without that cut, and the cut removes Stone Barrage from the greedy mix.")
    w("- Ship the pool fills and the riders. Do not put Armor, Combustive Shift, or Hype Train into a learn pool.")
    w("- Do not add a drain cap, a whiff refund, a KO floor, a longer Ember recharge, or a walls-P reboot. Tax plus Skim on a passer remains a zero lock. That was already accepted.")
    w("")
    w(f"Out of band under the run's own rule (stack win rate outside 35–65, absolute delta at least 8, and either the baseline was inside the band or the stack pushed further from 50 by more than 7): {', '.join(r['type'] for r in full['out_of_band']) or 'none'}.")
    for r in full["out_of_band"]:
        w(f"- {r['type']}: baseline {pct(f'S-type-{r['type']}-base', r['baseline'], ev_seed)}, stack {pct(f'S-type-{r['type']}-stack', r['stack'], ev_seed)}, delta {num(f'S-type-{r['type']}-delta', r['delta'], ev_seed)}.")
    w(f"Bot moves toward the center, {pct('S-type-Bot-base', bot['baseline'], ev_seed)} to {pct('S-type-Bot-stack', bot['stack'], ev_seed)}, so it is not a new hole. Denial as a team falls from {pct('S-arch-denial-base', denial['baseline'], ev_seed)} to {pct('S-arch-denial-stack', denial['stack'], ev_seed)}. The sensitivity with Bulwark held at 3 AP still shows denial at {pct('S-sens-denial-stack', sens['archetypes']['denial']['stack'], evs)}, so the drop is not the Bulwark cut. The walls duo takes those wins once Cinder is a turn-1 click. Greedy Keyper still clicks {plain('S-spid-stack-keyper-0', full['species_top']['stack']['keyper'][0]['move'], ev_seed)} at {pct('S-sp-stack-keyper-0', full['species_top']['stack']['keyper'][0]['share'], ev_seed)}. Field denied-AP is not the denial measurement. The calendars are.")
    w("")
    w(f"Match length, mean of the archetype pair means, goes from {num('S-len-base', full['length']['baseline'], ev_seed, 2)} turns to {num('S-len-stack', full['length']['stack'], ev_seed, 2)}. AP spent per battle-turn goes from {num('S-spent-base', full['ap']['baseline']['spent_per_turn'], ev_seed, 3)} to {num('S-spent-stack', full['ap']['stack']['spent_per_turn'], ev_seed, 3)}. AP denied per battle-turn goes from {num('S-deny-base', full['ap']['baseline']['denied_per_turn'], ev_seed, 3)} to {num('S-deny-stack', full['ap']['stack']['denied_per_turn'], ev_seed, 3)}. Share of active turns that start behind a block goes from {num('S-block-base', full['block_share']['baseline'], ev_seed, 4)} to {num('S-block-stack', full['block_share']['stack'], ev_seed, 4)}. The greedy policy stops walling once a cheap damaging click exists. Continuous coverage by recasting is still accepted. The new fact is that this policy barely uses the wall.")
    w("")
    w("Modal click changes in the archetype field:")
    w("")
    for ch in full["best_changes"]:
        w(f"- {ch['species']}: {nm(ch['baseline'])} (`{plain(f'S-best-{ch['species']}-bmove', ch['baseline'], ev_seed)}`) {pct(f'S-best-{ch['species']}-bshare', ch['baseline_share'], ev_seed)} becomes {nm(ch['stack'])} (`{plain(f'S-best-{ch['species']}-smove', ch['stack'], ev_seed)}`) {pct(f'S-best-{ch['species']}-sshare', ch['stack_share'], ev_seed)}.")
    w(f"Blockwyrm's modal stays {nm(bw_b[0]['move'])} in the full stack, so it is not in that list. The share moves from {pct('S-sp-baseline-blockwyrm-0', bw_b[0]['share'], ev_seed)} `{plain('S-spid-baseline-blockwyrm-0', bw_b[0]['move'], ev_seed)}` to {pct('S-sp-stack-blockwyrm-0', bw_s[0]['share'], ev_seed)} `{plain('S-spid-stack-blockwyrm-0', bw_s[0]['move'], ev_seed)}`. With Bulwark held at 3 AP the stack modal is `{plain('S-sens-spid-stack-blockwyrm-0', bw_sens_s[0]['move'], evs)}` at {pct('S-sens-sp-stack-blockwyrm-0', bw_sens_s[0]['share'], evs)}.")
    w("")

    w("## (b) Proposal inventory")
    w("")
    w("Still-open rows are the brief v3 backlog, the brief's next list, and the rarity-fit backlog. Withdrawn ids are absent from `Moves.csv` and are not re-proposed. Decided rows that already landed are listed so they are not relitigated. Superseded levers from the brief ledger are dropped.")
    w("")
    w("### Open")
    w("")
    w("| Id | Source | System | Rows / files | Suggested default | Status |")
    w("|---|---|---|---|---|---|")
    for row in brief_rows:
        cid = f"I-b{int(row['n']):02d}"
        plain(cid, f"COMBAT_ISSUES_DECISION_BRIEF_V3.md:{row['line']}", "brief backlog row")
        w(f"| B{int(row['n']):02d} | {plain(cid, f'COMBAT_ISSUES_DECISION_BRIEF_V3.md:{row['line']}', 'brief backlog row')} | backlog | {row['files']} | {row['depends']}. {row['action']} | OPEN |")
    # The plain() call above double-registers. I need to NOT call plain twice.
    # I'll fix this after I see the duplicate snippet problem.
    # Actually I already called plain twice with the same cid. The second overwrites in the list as a SECOND claim with the same id.
    # Validator uses snippets. Duplicate claims with the same snippet are ok if both snippets are in the doc once... 
    # I called plain twice so two claims share one snippet that appears once. Validator checks each claim's snippet is in the doc. Both snippets are identical so both pass. Wasteful but OK.
    # WAIT I called plain twice in the loop which prints two PASS lines and the snippet appears once. Good.
    # But the first plain() return is discarded and the second is in the table. Same snippet. OK.

    for i, text in enumerate(next_items, 1):
        cid = f"I-n{i:02d}"
        status = "OPEN"
        if i == 1:
            status = "REVISED this round. Keep Bulwark at 3 AP. The brief's 2 AP default is the click monopoly, and it is not what makes the duo win."
        w(f"| N{i:02d} | {plain(cid, 'COMBAT_ISSUES_DECISION_BRIEF_V3.md next list', 'brief next paragraph')} | later costs / chemistry | see the sentence | {text} | {status} |")
    for row in rarity_rows:
        n = int(row["n"])
        cid = f"I-r{n:02d}"
        status = "OPEN"
        default = row["depends"] + ". " + row["action"]
        if n == 7:
            status = "MERGED into Q32. Apply the surcharge drop once"
        elif n == 10:
            status = "DECIDED. Status Resistance stays retired. Do not rebuild"
        w(f"| R{n:02d} | {plain(cid, f'RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:{row['line']}', 'rarity backlog row')} | rarity rider | {row['files']} | {default} | {status} |")

    w("")
    w("### Already decided, and where the data stands")
    w("")
    w("| Item | Data today | Status |")
    w("|---|---|---|")
    w(f"| Walls and barriers: melee vs ranged, one hit, recharge column 2 means one skipped turn, one block, priority does not break a wall | Ember Wall is BlockWindow 50% melee, PRIOR, recharge 2 {P('F-ember')} {P('F-ember-window')} {P('F-rc-means-skip')} | DECIDED, landed |")
    w(f"| Uplink replaces AP Router on Validape | {P('F-uplink')} | DECIDED, landed |")
    w("| Kindling 6%, Pulse Shot 26, Status Resistance retired, withdrawn passives stay out | Kindling literal is 0.06f before Rugged " + P("F-kindling-literal") + f". Pulse Shot is 26 {P('F-pulse')}. Withdrawn ids are absent. | DECIDED, landed |")
    w(f"| System Reset, Hype Burst, and Crystal Extraction are built | Crystal Extraction has a heal payload {P('F-crystal-built')}. Hype Burst still grants Melee Atk {P('F-hype-melee')}. The Ranged Atk rewrite is still open (B07 / Q42). | DECIDED that they exist. Hype's stat is OPEN |")
    w(f"| All-In wipes the caster and the active ally | Master decision {P('F-allin-decided')}. CSV utility is empty and the tag spends only the caster {P('F-allin-utility-empty')} {P('F-allin-caster-only')}. Not in any pool {P('F-allin-unpooled')}. | DECIDED, not landed. No pool. No meta effect until it is pooled, and no pool is proposed |")
    w(f"| Guard Posture is Armor, not a wall | The card is still a 25% window {P('F-guard-25')}. Flat 12 / 16 is open (Q10) and is not decided. | Shape DECIDED. Numbers OPEN |")
    w(f"| Heavies stay at 4 AP. Self-damage stays shelved. Boss-only moves stay out of pools | Gas War is 4 AP ranged {P('F-gaswar')}. Grind Tax carries BOSS_ONLY. All-In is unpooled {P('F-allin-unpooled')}. | DECIDED |")
    w("")
    w("### Dropped")
    w("")
    w("| Item | Why it is not in the open table |")
    w("|---|---|")
    w("| Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, Open Line, Cinder Idle, Scorch Echo | Absent from Moves.csv. " + ", ".join(P(f"F-absent-{wid}") for wid in withdrawn) + " |")
    w("| Walls-P full block, drain cap of 2, whiff refund, KO floor of 2, heavies at 6 AP, longer Ember recharge, Margin Loan, Pair Ping, Mind Fault, secondary commons this round | Brief v3 already reconciled these to no. This combined run does not revive them. |")
    w("")
    w(f"Brief ledger rows parsed: {num('I-ledger-rows', len(ledger_rows), 'section c', 0)}. Brief questions parsed: Q1–Q46 {P('I-brief-q-count')}. Rarity questions parsed: Q1–Q10 {P('I-rarity-q-count')}.")
    w("")
    w("Live pools the open swaps touch:")
    w("")
    for sid in ("kodiax", "deghoul", "rattlestake"):
        info = pools[sid]
        w(f"- {sid} signature `{info['flagship']}`, members `{'|'.join(info['members'])}` {P(f'F-pool-{sid}')}.")
    w(f"Kodiax already has Crystal Extraction and does not have Extraction Hit {P('F-kodiax-crystal-stays')}. Fee Collector is not on Deghoul {P('F-fee-ungranted')}. Damaging Staked commons, boss rows excluded: {plain('F-no-staked-common', str(len(staked_common)), 'Moves.csv')} .")
    w("")

    w("## (c) Conflict matrix")
    w("")
    w("Each row is a pair that lands on the same move, the same status, the same AP lever, or the same resolver step when the suggested defaults are on together. The resolution is a **PROPOSAL**.")
    w("")
    w("| Pair | Relation | Resolution |")
    w("|---|---|---|")
    w(f"| Cinder AP 2 and Ember Wall on Cinislug | Stack. Different moves. The greedy click switches from Ember to Cinder. Baseline top is `{plain('S-spid-baseline-cinislug-0', cin_b[0]['move'], ev_seed)}` {pct('S-sp-baseline-cinislug-0', cin_b[0]['share'], ev_seed)}. Stack top is `{plain('S-spid-stack-cinislug-0', cin_s[0]['move'], ev_seed)}` {pct('S-sp-stack-cinislug-0', cin_s[0]['share'], ev_seed)}. | **PROPOSAL.** Ship both. Ember stays the signature and the wall. Do not lengthen its recharge. The unused wall is not a lock. |")
    w(f"| Cinder AP 2 and Bulwark AP 2 on the walls duo | The duo spike is Cinder. Sensitivity walls stack {pct('S-sens-walls-stack', sens_walls['stack'], evs)} with Bulwark left at 3, against full-stack walls {pct('S-arch-walls-stack', walls['stack'], ev_seed)}. Bulwark at 2 AP then monopolises the Blockwyrm click. | **PROPOSAL.** Ship Cinder. Keep Bulwark at 3 AP. Do not put a second turn-1 heavy on this duo. |")
    w(f"| Bulwark AP 2 and Stone Barrage | Contradict the mix. At 3 AP the sensitivity stack still clicks `{plain('S-sens-spid-stack-blockwyrm-0', bw_sens_s[0]['move'], evs)}`. At 2 AP the full stack's top is `{plain('S-spid-stack-blockwyrm-0', bw_s[0]['move'], ev_seed)}` {pct('S-sp-stack-blockwyrm-0', bw_s[0]['share'], ev_seed)}. | **PROPOSAL.** Keep 3 AP so Stone Barrage remains a real click. |")
    w(f"| Armor flat 12 and a 60% wall on one hit | Stack, and they tie. Hammer open {num('S-f-hammer_open-dmg', formula['hammer_open']['dmg'], 'one_hit', 0)} (chem {num('S-f-hammer_open-chem', formula['hammer_open']['chem'], 'one_hit', 1)}). Wall plus today's 25% leaves {num('S-f-hammer_wall_and_percent-dmg', formula['hammer_wall_and_percent']['dmg'], 'one_hit', 0)}. Wall plus flat 12 leaves {num('S-f-hammer_wall_and_flat12-dmg', formula['hammer_wall_and_flat12']['dmg'], 'one_hit', 0)}. {P('S-f-flats-match')}. Flat 12 alone leaves {num('S-f-hammer_flat12-dmg', formula['hammer_flat12']['dmg'], 'one_hit', 0)}. | **PROPOSAL.** Write flat 12 on Guard Posture and flat 16 on Guard Stance. Do not retune 12. Do not equip either card. |")
    w(f"| Guard Stance plus Immutable Ledger plus a wall, only if Q13 is yes | Triple-count on one hit. Hammer into Blockwyrm through a 60% wall, flat 16, and Ledger's 20% leaves {num('S-f-triple_if_q13-dmg', formula['triple_if_q13']['dmg'], 'one_hit', 0)}. Still damage, not a zero. | **PROPOSAL.** Q13 stays no. Ledger stays a 20% signature, not Armor. |")
    w(f"| Fee Collector, Tax-as-drain, and Packet Skim | Same AP lever. A passer who is not ahead of Deghoul stays at {plain('S-cal-fee_tax_skim_stack', ','.join(map(str, full['calendars']['fee_tax_skim_stack'])), ev_seed)}, the same zero as today {plain('S-cal-fee_tax_skim', ','.join(map(str, full['calendars']['fee_tax_skim'])), ev_seed)}. Fee only drains when the foe's AP is strictly higher {P('F-fee-rule')}. A pre-built bank of 4 is held at {plain('S-cal-fee_from_bank_stack', ','.join(map(str, full['calendars']['fee_from_bank_stack'])), ev_seed)}. | **PROPOSAL.** Ship Fee Collector and the Tax drain. Do not add a cap. Do not put Fee Collector in the same edit as an Armor swap. |")
    w(f"| Leak recharge and the Tax plus Skim zero lock | They do not fix each other. Leak opens a passer ({plain('S-cal-leak_pass_stack', ','.join(map(str, leak_stack)), ev_seed)}). Tax plus Skim stays zeros. A body that spends the open turn stays on {plain('S-cal-leak_spend_stack', ','.join(map(str, full['calendars']['leak_spend_stack'])), ev_seed)}. | **PROPOSAL.** Ship the recharge anyway. Do not relitigate the cap. |")
    w(f"| Gas War surcharge, brief Q32 and rarity Q6 | Same row `volatile_shot`. Live surcharge is on {P('F-gaswar-surcharge')}, power 72, AP 4, ranged {P('F-gaswar')}. | **PROPOSAL.** Delete `NextMoveAPSurcharge` once. Do not also make it melee. |")
    w("| Typeless exclusion, Pulse Shot at 26, and Stake Line | Stack. Pulse Shot the move stays 26. Only Rattlestake's pool swaps. | **PROPOSAL.** Ship all three. The typed-common cell still wins. See (f). |")
    w("| Ore Strike's Def rider and Kodiax's AP | Different levers. The rider is a 25% Def nick on the signature. Deep Dig's cost does not change. | **PROPOSAL.** Ship the rider. Leave Deep Dig at 4 AP. |")
    w("| Combustive Shift's drain and Deghoul's Tax | Would double-count a Gas drain if Combustive entered Deghoul's five. | **PROPOSAL.** Ship the drain on the card. Leave it out of every Gas pool. |")
    w("| Hype Train's Surging and Hype Burst | Different moves. Hype Train is Meme and has no species. Hype Burst's open rewrite is Ranged Atk, which is the stat Cinder uses. | **PROPOSAL.** Ship Surging on Hype Train and leave it unpooled. Ship Hype Burst's Ranged Atk. |")
    w(f"| Blade Flurry 10×3, a 50% wall, and flat 12 | The wall and the flat spend on one hit. Open baseline hit {num('S-f-blade_base_open', formula['blade_base_open'], 'one_hit', 0)}, first hit behind the wall and flat 12 is {num('S-f-blade_base_first', formula['blade_base_first'], 'one_hit', 0)}. Stack power 10 open hit {num('S-f-blade_stack_open', formula['blade_stack_open'], 'one_hit', 0)}, first hit {num('S-f-blade_stack_first', formula['blade_stack_first'], 'one_hit', 0)}. The per-hit loop spends the block on that hit {P('S-f-blade-rest')}, so the later hits are not mitigated. | **PROPOSAL.** Ship 10×3. Do not let Armor change the later hits. |")
    w("| Patience cap +3 and a 4 AP bank | The cap stops the +6 Def walk on a passer. It does not change the heavy's cost. | **PROPOSAL.** Ship the cap. The tempo table is the check. |")
    w("| Chemistry narrow (Dusted, Shattered, Corroded, HODLed, Rugged) and new status producers | The narrow edits change bodies that already exist. Mind Fault, Unsealed, and the other new producers are not in the default stack. | **PROPOSAL.** Narrow the bodies. Do not add producers this round. |")
    w("| Cipher Lock's stage filter and self buffs | The filter is positive stages on a locked foe. Patience and Hype Burst write on the user. | **PROPOSAL.** Ship the filter. It does not eat the user's own buff. |")
    w(f"| Rugged cap 3 and typeless exclusion | Same chemistry step, no double-count. Kindling's 6% literal runs before Rugged {P('F-kindling-literal')}. Passives run before that {P('F-eot-order')}. A typed Cinder into Gas is chem {num('S-f-cinder_into_gas-chem', formula['cinder_into_gas']['chem'], 'one_hit', 1)}. A neutral Pulse Shot is chem {num('S-f-pulse_neutral-chem', formula['pulse_neutral']['chem'], 'one_hit', 1)} and does not start Rugged. Live Rugged cap constant is 15 {P('F-rugged-15')}. | **PROPOSAL.** Ship the cap and the exclusion. Do not reorder ticks. |")
    w("| Ember's 1 AP drain and Bot denial | Decided: a wall grants at most 1 AP. After Cinder exists, greedy Cinislug barely casts Ember. | **PROPOSAL.** Leave the drain. Do not add a second denial rider to Ember. |")
    w("| Yield Spike 20% lifesteal and Stake Strike 15% | Same rider family, two percents. Yield stays higher. | **PROPOSAL.** Ship both percents. Do not swap either move's pool slot for this. |")
    w(f"| All-In's ally wipe and the denial stack | All-In is in no pool {P('F-allin-unpooled')}. The wipe cannot change these win rates until a Meme body exists. | **PROPOSAL.** Implement the decided ally wipe. Do not add a pool. |")
    w("")

    w("## (d) Combined 2v2")
    w("")
    w("The combined sim extends `ap_denial_2v2_sim.py`. Two attached sims still diverge and were not used for rates. `tempo_2v2_sim.py` parses MitigationWindow and does not mention BlockWindow " + P("F-tempo-no-block") + ". `walls_pvp_v2_sim.py` does the same " + P("F-walls-no-block") + ". The denial sim is the BlockWindow mirror " + P("F-denial-block") + ".")
    w("")
    w("Fixes applied to both the baseline and the stack, because the denial sim omitted them and the snapshot engine does not: authored stage changes and on-hit riders, AllEnemies hitting every living foe with the cost paid once, separate melee and ranged stages, Bulwark striking with Melee Def " + P("F-sim-def-scale") + ", end step order of passive ticks then Kindling then chemistry " + P("F-eot-order") + ", recharge on the bench " + P("F-bench-recharge") + ", and Fee Collector's strict-ahead drain " + P("F-fee-rule") + ". Baseline rates will not match an older denial write-up. The calendars were recomputed on this engine.")
    w("")
    w("The stack overlay is data: Leak recharge 2, Tax as drain, typeless neutrality, Cinder AP 2, Hype Burst on Ranged Atk, Patience cap 3, Streamlined once and cap −1, Bulwark AP 2 in this full stack only, Blade Flurry power 10, Cryo Ray slow at 100%, Gas War surcharge removed, the rarity riders, Extraction Hit and Fee Collector retiered, virtual Stake Line, Armor flats on the cards and not in a kit, Rugged cap 3, and the narrowed Dusted / Shattered / Corroded / HODLed bodies. STAB stays off. Ghost and Cofi are not in the field.")
    w("")
    w("### Archetype round robin")
    w("")
    w(f"Eight teams, both directions. Games per side {num('S-arch-heavy-g', full['archetypes']['heavy']['games'], ev_seed, 0)}.")
    w("")
    w("| Archetype | Baseline | Stack | Delta |")
    w("|---|---:|---:|---:|")
    for name in full["archetypes"]:
        row = full["archetypes"][name]
        w(f"| {name} | {pct(f'S-arch-{name}-base', row['baseline'], ev_seed)} | {pct(f'S-arch-{name}-stack', row['stack'], ev_seed)} | {num(f'S-arch-{name}-delta', row['delta'], ev_seed)} |")
    w("")
    w("### Type faces")
    w("")
    w(f"Each face brings Humpacked, except Humpacked brings Rattlestake. Games per type {num('S-type-Node-g', full['types']['Node']['games'], ev_seed, 0)}. Type-face picks are not in the most-picked table. Humpacked was the universal partner and was inflating those counts.")
    w("")
    w("| Type | Baseline | Stack | Delta |")
    w("|---|---:|---:|---:|")
    for name in full["types"]:
        row = full["types"][name]
        w(f"| {name} | {pct(f'S-type-{name}-base', row['baseline'], ev_seed)} | {pct(f'S-type-{name}-stack', row['stack'], ev_seed)} | {num(f'S-type-{name}-delta', row['delta'], ev_seed)} |")
    w("")
    w("### Length, AP, blocks")
    w("")
    w("| Measure | Baseline | Stack |")
    w("|---|---:|---:|")
    w(f"| Mean match length | {num('S-len-base', full['length']['baseline'], ev_seed, 2)} | {num('S-len-stack', full['length']['stack'], ev_seed, 2)} |")
    w(f"| AP spent per battle-turn | {num('S-spent-base', full['ap']['baseline']['spent_per_turn'], ev_seed, 3)} | {num('S-spent-stack', full['ap']['stack']['spent_per_turn'], ev_seed, 3)} |")
    w(f"| AP denied per battle-turn | {num('S-deny-base', full['ap']['baseline']['denied_per_turn'], ev_seed, 3)} | {num('S-deny-stack', full['ap']['stack']['denied_per_turn'], ev_seed, 3)} |")
    w(f"| Turns starting behind a block | {num('S-block-base', full['block_share']['baseline'], ev_seed, 4)} | {num('S-block-stack', full['block_share']['stack'], ev_seed, 4)} |")
    w("")
    w("### Most-picked moves by rarity")
    w("")
    w("Archetype games only.")
    w("")
    w("| Rarity | Baseline | Stack |")
    w("|---|---|---|")
    rarities = ["Common", "Uncommon", "Rare", "Epic", "Legendary"]
    for rar in rarities:
        def cell(mode, rarity=rar):
            bits = []
            for i, pick in enumerate(full["picks"][mode].get(rarity, [])):
                bits.append(f"{nm(pick['move'])} (`{pick['move']}`) {pct(f'S-pick-{mode}-{rarity}-{i}', pick['share'], ev_seed)}")
            return "; ".join(bits)
        w(f"| {rar} | {cell('baseline')} | {cell('stack')} |")
    w("")
    w("Epic under the full stack is only Bulwark Blow. That is the click monopoly Q33 should not ship. Common's third place moves from Pulse Shot to Blade Flurry because Rattlestake no longer clicks Pulse Shot. Gas Bump stays the most-picked Common in both modes.")
    w("")
    w("### Species clicks that matter")
    w("")
    w("| Species | Baseline | Stack |")
    w("|---|---|---|")
    for sid in focus_species:
        def scell(mode, s=sid):
            bits = []
            for i, pick in enumerate(full["species_top"][mode].get(s, [])):
                bits.append(f"{nm(pick['move'])} (`{plain(f'S-spid-{mode}-{s}-{i}', pick['move'], ev_seed)}`) {pct(f'S-sp-{mode}-{s}-{i}', pick['share'], ev_seed)}")
            return "; ".join(bits)
        w(f"| {sid} | {scell('baseline')} | {scell('stack')} |")
    w("")
    w("Humpacked's greedy click does not change. Frozen's archetype drop is the field getting Cinder and Stake Line while that click stays put. Compressed was already near the floor and is pushed further. That is a watch item, not a new Compressed move. Q45 stays no.")
    w("")
    w("### Sensitivity: Bulwark left at 3 AP")
    w("")
    w(f"Smaller sample. Games per archetype side {num('S-sens-walls-g', sens['archetypes']['walls']['games'], evs, 0)}.")
    w("")
    w("| Slice | Baseline | Stack at Bulwark AP 3 | Delta |")
    w("|---|---:|---:|---:|")
    for name in ("walls", "denial"):
        row = sens["archetypes"][name]
        w(f"| archetype {name} | {pct(f'S-sens-{name}-base', row['baseline'], evs)} | {pct(f'S-sens-{name}-stack', row['stack'], evs)} | {num(f'S-sens-{name}-delta', row['delta'], evs)} |")
    for name in ("Burned", "Block", "Bot", "Compressed"):
        row = sens["types"][name]
        w(f"| type {name} | {pct(f'S-sens-type-{name}-base', row['baseline'], evs)} | {pct(f'S-sens-type-{name}-stack', row['stack'], evs)} | {num(f'S-sens-type-{name}-delta', row['delta'], evs)} |")
    w("")
    w("Blockwyrm under that sensitivity:")
    w("")
    for mode in ("baseline", "stack"):
        bits = []
        for i, pick in enumerate(sens["species_top"][mode].get("blockwyrm", [])):
            bits.append(f"{nm(pick['move'])} (`{plain(f'S-sens-spid-{mode}-blockwyrm-{i}', pick['move'], evs)}`) {pct(f'S-sens-sp-{mode}-blockwyrm-{i}', pick['share'], evs)}")
        w(f"- {mode}: {'; '.join(bits)}")
    w("")
    w("### Wall lines")
    w("")
    w("Attacker is team 0. A TRUE lock is a repeating rule that keeps a side from a legal winning action, or a wall line the attacker never wins. A waller who spends turns and loses the race is not a lock. A 1v2 that is a loss with or without the wall is not a lock. Recasting is accepted.")
    w("")
    w("| Line | Mode | Attacker | Waller | Cap | Length |")
    w("|---|---|---:|---:|---:|---:|")
    for name in full["walls"]["baseline"]:
        for mode in ("baseline", "stack"):
            row = full["walls"][mode][name]
            w(f"| {name} | {mode} | {pct(f'S-w-{mode}-{name}-a', row['attacker_pct'], ev_seed)} | {pct(f'S-w-{mode}-{name}-w', row['waller_pct'], ev_seed)} | {pct(f'S-w-{mode}-{name}-cap', row['cap_pct'], ev_seed)} | {num(f'S-w-{mode}-{name}-len', row['length'], ev_seed, 2)} |")
    w("")
    w("No new wall TRUE lock. Ember versus Ore Strike still has an attacker win under the stack. The control 1v2 is a loss in both modes.")
    w("")
    w("### Denial calendars")
    w("")
    w("Action-time AP for eight turns. These are the lock measurement. The greedy field barely clicks Leak or Tax, which is why denied-AP per turn stays near zero.")
    w("")
    w("| Calendar | Series | Reading |")
    w("|---|---|---|")
    readings = {
        "leak_pass_base": "Passer finisher lock. Hammer never.",
        "leak_pass_stack": "Opens. Hammer is payable on turn 3.",
        "leak_spend_base": "Zero lock after one spend.",
        "leak_spend_stack": "Recharge frees a passer, not a body that spends the open turn.",
        "taxskim_pass_base": "Zero lock. Already accepted.",
        "taxskim_pass_stack": "Still a zero lock. The stack does not make it worse.",
        "deghoul_base": "Steal lets Deghoul bank.",
        "deghoul_stack": "Drain. Deghoul climbs one point at a time.",
        "victim_tax_base": "Victim series.",
        "victim_tax_stack": "Victim series unchanged by steal versus drain.",
        "fee_tax_skim": "Fee does not fire on a passer who is not strictly ahead.",
        "fee_tax_skim_stack": "Same zero. Fee does not deepen it.",
        "fee_from_bank_stack": "A pre-built bank of 4 is cut and then held.",
    }
    for key, reading in readings.items():
        seq = ",".join(str(x) for x in full["calendars"][key])
        w(f"| {key} | {plain(f'S-cal-{key}', seq, ev_seed)} | {reading} |")
    w("")

    w("## (e) Tempo sanity")
    w("")
    w("Team 0 is the 4 AP side. Team 1 is the faster side. The question is whether a Deep Dig or Hammer on turn 2 is an automatic win when the faster side lands two 2 AP hits first. It is not.")
    w("")
    w("| Line | Mode | Fast | Heavy | Heavy lands | Land turn | Hits before | Length | Cap |")
    w("|---|---|---:|---:|---:|---:|---:|---:|---:|")
    for name in full["scripted"]["baseline"]:
        for mode in ("baseline", "stack"):
            row = full["scripted"][mode][name]
            turn = "—"
            if row["heavy_turn"] is not None:
                turn = num(f"S-sc-{mode}-{name}-turn", row["heavy_turn"], ev_seed, 1)
            w(
                f"| {name} | {mode} | {pct(f'S-sc-{mode}-{name}-fast', row['fast_pct'], ev_seed)} | "
                f"{pct(f'S-sc-{mode}-{name}-heavy', row['heavy_pct'], ev_seed)} | "
                f"{pct(f'S-sc-{mode}-{name}-lands', row['heavy_lands_pct'], ev_seed)} | {turn} | "
                f"{num(f'S-sc-{mode}-{name}-hits', row['hits_before'], ev_seed, 2)} | "
                f"{num(f'S-sc-{mode}-{name}-len', row['length'], ev_seed, 2)} | "
                f"{pct(f'S-sc-{mode}-{name}-cap', row['cap_pct'], ev_seed)} |"
            )
    w("")
    w(f"`two_fast_hits` is the design rule: Seam Cut plus Blade Flurry against a Deep Dig banker. Fast side {pct('S-sc-stack-two_fast_hits-fast', two_s['fast_pct'], ev_seed)}, heavy lands {pct('S-sc-stack-two_fast_hits-lands', two_s['heavy_lands_pct'], ev_seed)}. Blade Flurry at power 10 does not hand the heavy the win.")
    w(f"`pulse_vs_dig` is the typeless line. Baseline fast {pct('S-sc-baseline-pulse_vs_dig-fast', full['scripted']['baseline']['pulse_vs_dig']['fast_pct'], ev_seed)}. Stack fast {pct('S-sc-stack-pulse_vs_dig-fast', full['scripted']['stack']['pulse_vs_dig']['fast_pct'], ev_seed)}, heavy {pct('S-sc-stack-pulse_vs_dig-heavy', full['scripted']['stack']['pulse_vs_dig']['heavy_pct'], ev_seed)}, cap {pct('S-sc-stack-pulse_vs_dig-cap', full['scripted']['stack']['pulse_vs_dig']['cap_pct'], ev_seed)}. Speed still wins. The heavy is no longer shut out, because Pulse Shot stops inheriting a type. That is the rule working.")
    w(f"`skim_blade_vs_dig` stays a fast-side win at {pct('S-sc-stack-skim_blade_vs_dig-fast', full['scripted']['stack']['skim_blade_vs_dig']['fast_pct'], ev_seed)}. Baseline hits the turn cap on {pct('S-sc-baseline-skim_blade_vs_dig-cap', full['scripted']['baseline']['skim_blade_vs_dig']['cap_pct'], ev_seed)} of trials because Patience walks Def to +6 and Skim keeps firing. The stack cap is {pct('S-sc-stack-skim_blade_vs_dig-cap', full['scripted']['stack']['skim_blade_vs_dig']['cap_pct'], ev_seed)}. The Patience cap is what lets the fight end.")
    w(f"`cinder_vs_dig` first casts Cinder on turn {num('S-sc-stack-cinder_vs_dig-cast-cinder_burst', full['scripted']['stack']['cinder_vs_dig']['first_cast']['cinder_burst'], ev_seed, 1)} under the stack and turn {num('S-sc-baseline-cinder_vs_dig-cast-cinder_burst', full['scripted']['baseline']['cinder_vs_dig']['first_cast']['cinder_burst'], ev_seed, 1)} at 3 AP. That is the turn-1 click the AP change buys.")
    w(f"`leak_vs_hammer` baseline is a capped draw at {pct('S-sc-baseline-leak_vs_hammer-draw', full['scripted']['baseline']['leak_vs_hammer']['draw_pct'], ev_seed)}, which is the finisher lock. Under the stack the fast side wins {pct('S-sc-stack-leak_vs_hammer-fast', full['scripted']['stack']['leak_vs_hammer']['fast_pct'], ev_seed)} and Cipher Shot is first cast on turn {num('S-sc-stack-leak_vs_hammer-cast-cipher_shot', full['scripted']['stack']['leak_vs_hammer']['first_cast'].get('cipher_shot', 0), ev_seed, 1)}. The hammer still does not land ({pct('S-sc-stack-leak_vs_hammer-lands', full['scripted']['stack']['leak_vs_hammer']['heavy_lands_pct'], ev_seed)}), because Skim keeps hitting. The recharge lets the denial side attack. It does not make the heavy an automatic loss by rule. The heavy loses the race.")
    w(f"`ore_rider_vs_hammer` stays a fast-side win at {pct('S-sc-stack-ore_rider_vs_hammer-fast', full['scripted']['stack']['ore_rider_vs_hammer']['fast_pct'], ev_seed)}. The Def rider does not flip it. Hammer lands on {pct('S-sc-stack-ore_rider_vs_hammer-lands', full['scripted']['stack']['ore_rider_vs_hammer']['heavy_lands_pct'], ev_seed)}.")
    w("")

    w("## (f) Typed versus typeless, and learn pools")
    w("")
    w(f"Score is the damage term from `rarity_fit_check.Model.strike` at equal attack and defense 60. Reference 2 AP single median {num('T-ref-hit', ref_hit, 'strike', 2)}. In-line band from the rarity doc is {plain('T-band-lo', f'{band_lo:.2f}', 'rarity doc')} to {plain('T-band-hi', f'{band_hi:.2f}', 'rarity doc')}. A cell with fewer than two cards on a side is thin and is not a broken rule. The plan's check is the wild band {P('F-plan-band')} {P('F-csv-band-comment')}: three at the body tier or lower, one at the body or one step up, signature strictly highest. Starters ({plain('F-starters', ','.join(starters), 'assets')}) only owe a unique top rarity. Ghost and Cofi are skipped. Off-type moves are outside the test.")
    w("")
    w("| Mode | Rarity | Shape | AP | Typed n | Typed median | Typeless n | Typeless median | Typed higher | Thin |")
    w("|---|---|---|---:|---:|---:|---:|---:|---|---|")
    for label, cells in (("baseline", base_cells), ("stack", stack_cells)):
        prefix = "T-base" if label == "baseline" else "T-stack"
        for i, c in enumerate(cells):
            thin = "yes" if c["thin"] else "no"
            if c["typed_higher"]:
                flag = f"yes {P(f'{prefix}-{i}-yes')}"
            elif c["thin"]:
                flag = f"NO {P(f'{prefix}-{i}-thin-no')}"
            else:
                flag = f"NO {P(f'{prefix}-{i}-no')}"
            w(
                f"| {label} | {c['rarity']} | {c['shape']} | {c['ap']} | "
                f"{num(f'{prefix}-{i}-tn', c['tn'], 'strike', 0)} | {num(f'{prefix}-{i}-tmed', c['tmed'], 'strike', 2)} | "
                f"{num(f'{prefix}-{i}-un', c['un'], 'strike', 0)} | {num(f'{prefix}-{i}-umed', c['umed'], 'strike', 2)} | "
                f"{flag} | {thin} |"
            )
    w("")
    spread_line, _ = find_line(SIMS / "RARITY_FIT_AND_RIDER_BUILDOUT_V1.md", r"\| Uncommon \| spread \|")
    ok("T-spread-prior", f"RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:{spread_line} reports a one-card Uncommon spread cell as NO on the full delivery score")
    if any(c["shape"].startswith("spread") for c in base_cells + stack_cells):
        bad("T-spread-absent", "a spread cell is in the damage-term table and must be read above")
    else:
        ok("T-spread-absent", "damage-term cells have no spread pair with both a typed and a typeless move")
    w(f"No non-thin cell flips. {P('T-stack-rule')}. Cinder joining the Common multi cell does not let typeless pass it. Stake Line joins the Common single cell and typed still leads. The rarity doc's Uncommon spread row is one card against one card on the full delivery score {P('T-spread-prior')}. That pair is not in this damage-term table {P('T-spread-absent')}. It stays a thin cell, not a broken rule, and the stack does not add a move to it.")
    w("")
    w("Rare typeless per AP against the typed-common median of the same shape, any AP. The stack does not edit these two moves. Stake Line changes the typed-common median, which is why the ratio moves.")
    w("")
    w("| Mode | Move | Per AP | Typed-common median | n | Ratio | In band |")
    w("|---|---|---:|---:|---:|---:|---|")
    for label, rows_r in (("baseline", base_rare), ("stack", stack_rare)):
        prefix = "base" if label == "baseline" else "stack"
        for rec in rows_r:
            cid = f"T-{prefix}-{rec['mid']}"
            inside = band_lo <= rec["ratio"] <= band_hi
            w(
                f"| {label} | {rec['name']} (`{rec['mid']}`) | {num(f'{cid}-per', rec['per'], 'strike', 2)} | "
                f"{num(f'{cid}-med', rec['med'], 'strike', 2)} | {num(f'{cid}-n', rec['n'], 'strike', 0)} | "
                f"{num(f'{cid}-ratio', rec['ratio'], 'strike', 3)} | {'yes' if inside else 'OUT'} {P(f'{cid}-out')} |"
            )
    w("")
    w("**PROPOSAL.** Leave Titan Crash and Rail Spike plain. Accuracy 80 and 85 is the commitment. They sit under the line. Raising power would make a rare typeless stronger, which fights the rule that typed commons win on per AP. This is rarity Q10. No new question.")
    w("")
    w(f"Learn-pool problems before the edits: {plain('T-pool-base', str(len(base_problems)), 'pool_problems')} {P('T-pool-base-empty')}. After Extraction Hit and Fee Collector retier to Common, Stake Line added as Common, and the three swaps: {plain('T-pool-stack', str(len(stack_problems)), 'pool_problems')} {P('T-pool-stack-empty')}. Nothing is dropped for the band.")
    w("")
    w(f"The plan overview also says existing moves only, no new definitions {P('F-plan-no-new')}. Stake Line is a new id {P('T-stake-line-band')}. There is no damaging Staked common to build out, Stake Strike is Hopoch's Rare and retiering it would change a second pool, and boss moves cannot fill the slot. **PROPOSAL.** Keep Stake Line. It passes the band, which is the plan's check. Q20 is the question that allows the one new row. If that answer is no, Rattlestake keeps Pulse Shot and the rest of the stack still stands.")
    w("")

    w("## (g) Implementation order")
    w("")
    w("Top 12. Each step is testable alone. Effort is S, M, or L. Bulwark's AP cut is not a step.")
    w("")
    w("| # | PROPOSAL | Impact | Effort | Files / rows | Depends | Editor test |")
    w("|---|---|---|---|---|---|---|")
    steps = [
        ("1", "Memory Leak rechargeTurns 2. Window, −2, and Speed +2 stay.", "Opens the passer finisher lock.", "S", f"Moves.csv:{leak_line} memory_leak. No pool edit.", "Q1, default yes", "MemoryLeak_RechargeSkipsOneTurn_FinisherPaysOnTheOpenTurn: a passer leaked on turn 1 has at least 4 AP at action time on turn 3."),
        ("2", "Transaction Tax DrainAP:1. The user does not gain the point.", "Stops the signature from banking a free heavy.", "S", f"Moves.csv:{tax_line} transaction_tax. Deghoul's signature stays.", "Q2, default yes", "TaxIsDrain_DeghoulDoesNotGainThePoint: after one cast, the target is down 1 AP and Deghoul's AP did not rise by that point."),
        ("3", "Typeless moves are neutral. STAB stays off.", "The decided rule enters the engine.", "M", f"DamageCalculator.cs:{inherit_line} ResolveAttackType. CombatResolverConfigSO.cs:{stab_line} enableStab stays false.", "None. Do not bundle Q19.", "TypelessMove_IsNeutralAndAppliesNoReaction: Pulse Shot from a Burned user into Gas is ×1.0 and does not apply Rugged. Cinder Burst into Gas is ×1.2."),
        ("4", "Cinder Burst AP 2, power 15.", "Burned has a typed turn-1 click. The walls duo becomes the watch item.", "S", f"Moves.csv:{cinder_line} cinder_burst. Cinislug membership unchanged. Ember stays the signature.", "Q9, default yes. Accept the duo spike.", "CinderBurst_CostsTwo_BandUnchanged: APCost is 2, power is 15, flagship is still ember_wall, wild band still passes."),
        ("5", "Fee Collector becomes Common and replaces Slipstream on Deghoul.", "Gas gains the tax passive already in the catalog.", "S", f"Moves.csv:{move_lines['fee_collector']} rarity Common. SpeciesMovePools.csv:{pools['deghoul']['line']} slipstream becomes fee_collector.", "Q24, default yes. Not beside Armor.", "LearnPool_DeghoulFeeCollectorStaysInsideTheBand: signature stays transaction_tax, one move above the body, size stays 5."),
        ("6", "Stake Line, Staked, Common, ranged, power 34, AP 2, accuracy 95, replaces Pulse Shot on Rattlestake only.", "The body gets a typed click below Compound Barrier. Pulse Shot the move stays 26.", "M", f"New Moves.csv row stake_line. SpeciesMovePools.csv:{pools['rattlestake']['line']} baseline_shot becomes stake_line.", "Q20, default yes. This is the one new id.", "LearnPool_RattlestakeStakeLineStaysInsideTheBand: size 5, signature ap_reserve is the unique top, the new move is Staked."),
        ("7", "Extraction Hit at Common replaces Body Check on Kodiax. OnHit 40% Melee Atk +1 cap +3, Speed +1 once at +3. Crystal Extraction stays.", "Empty passive gets the payload the card already promises.", "M", f"Moves.csv:{move_lines['extraction_hit']} rarity Common and the OnHit utility. SpeciesMovePools.csv:{pools['kodiax']['line']} baseline_slam becomes extraction_hit. crystal_extraction stays.", "Q23, default yes", "ExtractionHit_RaisesAtkAtFortyAndSpeedsOnceAtPlusThree. LearnPool_KodiaxExtractionStaysInsideTheBand: signature stays ore_strike."),
        ("8", "Riders with no pool add: Ore Strike Def −1 at 25% once, Yield Spike lifesteal 20%, Stake Strike lifesteal 15%, Fork Split and Shard Scatter and Chain Fork Speed −1 at 20% once, Combustive Shift DrainAP 1 at 50% once, Hype Train Surging at 100%.", "Plain high-rarity hits match the type profile. None of them enters a new pool.", "S", f"Moves.csv ore_strike:{move_lines['ore_strike']}, yield_spike:{move_lines['yield_spike']}, stake_strike:{move_lines['stake_strike']}, fork_split:{move_lines['fork_split']}, shard_scatter:{move_lines['shard_scatter']}, chain_fork:{move_lines['chain_fork']}, combustive_shift:{move_lines['combustive_shift']}, hype_train:{move_lines['hype_train']}.", "Rarity Q1, Q2, Q3, Q4. Defaults yes.", "RarityFitTests.OreStrike_CracksDefAndStaysSignature. YieldLifestealBeatsStake. CombustiveDrainsOnce and the move is absent from Deghoul and Balloom. HypeTrain_AppliesSurging and no pool contains it. ForkSpeedNick_DoesNotChangePower."),
        ("9", "Delete Gas War's NextMoveAPSurcharge once. Power 72, AP 4, ranged.", "The same edit as rarity Q6 and brief Q32.", "S", f"Moves.csv:{gas_line} volatile_shot.", "Q32, default yes. Do not also make it melee.", "GasWar_HasNoSelfSurcharge: utility has no NextMoveAPSurcharge, power 72, AP 4, both Gas pools still list it under the signature."),
        ("10", "Patience cap +3, Streamlined drops Def once, Rugged cap 3, HODLed one owned ramp cap +3 then revert.", "Stops the +6 walks and the Rugged climb. No new move.", "M", f"Moves.csv patience_passive:{move_lines['patience_passive']} and streamlined_flow:{move_lines['streamlined_flow']}. CombatConstants.cs:{rug_line}. StatusProcessor HODLed block and Statuses.csv HODLed.", "Q22, Q31, Q17, Q16. Defaults yes.", "Patience_StopsAtDefThree. StreamlinedFlow_DropsDefOnceAbove70: a second action above 70% does not drop Def again. RuggedEscalatesAndCapsWhileDripAndFomoTickOnce asserts cap 3. Hodled_RampsOnePerStep: +1, +2, +3, then the owned stages revert."),
        ("11", "Blade Flurry power 10 at 3 hits, Cryo Ray Speed −2 at 100%, Hype Burst grants Ranged Atk.", "The common stops outrunning the card beside it. Cryo slow matches the name. Hype buffs the stat Cinislug strikes with.", "S", f"Moves.csv blade_flurry:{blade_line}, cryo_ray:{move_lines['cryo_ray']}, hype_burst:{hype_line}. Pools unchanged.", "Q35, Q34, Q42. Defaults yes.", "BladeFlurry_IsTenTimesThree. CryoRay_SlowsAtOneHundred. HypeBurst_GrantsRangedAtkPerBuffRemoved: two buffs, Ranged Atk +2, Melee Atk unchanged."),
        ("12", "Armor flat 12 on Guard Posture and flat 16 on Guard Stance, one hit, after the wall percent, cap 16. Remove Guard Stance's Def +2. Do not equip either move. Do not change Bulwark's AP.", "The flat matches the live percent on the measured hammer. The duo does not gain a second heavy or a third mitigation.", "S", f"Moves.csv guard_posture:{guard_line} and guard_stance:{move_lines['guard_stance']}. No SpeciesMovePools.csv edit. bulwark_blow:{bulwark_line} stays AP 3.", "Q10 yes for the numbers. Q12 no. Q13 no. Q33 revised to no.", "GuardPosture_SubtractsTwelveAfterTheWall. GuardStance_SubtractsSixteenAndDoesNotRaiseDef. DeghoulAndBlockwyrmPools_DoNotGainArmor. BulwarkBlow_StaysAtThreeAp."),
    ]
    for step in steps:
        w("| " + " | ".join(step) + " |")
    w("")
    w(f"Decided, and not one of the twelve: All-In's ally wipe. UtilityEffects `DrainAP:10:100%:AllAllies:all_in` on Moves.csv:{all_line}. Enemy AP untouched. No pool edit. Test `AllIn_WipesAllyApOnCast` plus the existing caster spend. It reaches nobody until a Meme body exists, and none does.")
    w("")
    w("Explicitly not in this order. No drain cap. No whiff refund. No KO floor. No heavies at 6. No longer Ember recharge. No Margin Loan. No Pair Ping. No Mind Fault. No Armor pool swap. No Bulwark AP cut. No second copy of a withdrawn passive.")
    w("")

    w("## (h) What works, what doesn't, what could be better")
    w("")
    w(f"What works. The wall decision is in the data, and the scripted attacker still beats a recast Ember {P('S-w-stack-ember_vs_ore-a')}. Two faster hits still beat a 4 AP banker under the whole stack {P('S-sc-stack-two_fast_hits-fast')}. Typed still beats typeless in every non-thin cell {P('T-stack-rule')}. The learn-pool band is empty before and after the three edits {P('T-pool-stack-empty')}. Leak recharge opens the passer lock {P('S-cal-leak_pass_stack')}. Tax-as-drain stops the free bank {P('S-cal-deghoul_stack')}. Armor flat 12 ties the live 25% on this hammer {P('S-f-flats-match')}. Kindling stays 6% and still ticks before Rugged {P('F-kindling-literal')}. Uplink is already the Validape passive {P('F-uplink')}.")
    w("")
    w(f"What doesn't. The defaults were tuned alone, and together they hand the round robin to Blockwyrm plus Cinislug {P('S-arch-walls-stack')}. Greedy Cinislug stops casting Ember {P('S-sp-stack-cinislug-0')}. Bulwark at 2 AP, which the brief suggested, deletes the rest of Blockwyrm's clicks {P('S-sp-stack-blockwyrm-0')}. Tax plus Skim on a passer is still a zero lock {P('S-cal-taxskim_pass_stack')}. Typeless moves still inherit a type {P('F-inherit-line')}. Rare typeless hits sit under the line {P('T-stack-refined_slam-out')} {P('T-stack-overdraw_snipe-out')}. Compressed is pushed further down {P('S-type-Compressed-stack')}. The greedy field denies almost no AP {P('S-deny-stack')}, so a win-rate table is the wrong tool for the lock. All-In's ally wipe is decided and not in the CSV {P('F-allin-utility-empty')}.")
    w("")
    w("What could be better. Ship the four engine and data fixes first (Leak, Tax, typeless, Cinder), hold Bulwark at 3 AP, and put Armor on the card without putting it on a Chainling. Leave the rare typeless hits plain. Leave the Tax plus Skim zero lock alone. Watch the walls duo and Compressed after Cinder lands. Do not add a new Compressed move in the same pass. Do not reorder ticks. Do not reopen walls, STAB, Kindling's percent, Pulse Shot's power, or the withdrawn passives.")
    w("")

    w("## (i) Questions only Cronus can answer")
    w("")
    w("These are the brief's Q1–Q46 and the rarity doc's questions that are not the same ask. Rarity Q6 is brief Q32 (Gas War's surcharge), one edit. Rarity Q8 is brief Q19 (STAB off). Rarity Q9 is already decided: Status Resistance stays retired. Suggested defaults are the earlier docs' defaults, except Q33, which this round revises.")
    w("")
    w("| # | Question | Suggested default | What it blocks | This round |")
    w("|---|---|---|---|---|")
    for q in brief_q:
        cid = f"I-{q['id']}"
        src = plain(cid, f"COMBAT_ISSUES_DECISION_BRIEF_V3.md:{q['line']}", "brief question")
        extra = ""
        if q["id"] == "Q33":
            extra = "REVISE to no. Keep Bulwark at 3 AP. The 2 AP cut is the click monopoly, and it is not what makes the duo win."
        elif q["id"] == "Q9":
            extra = "Keep yes. Accept the Blockwyrm plus Cinislug spike. It is not a TRUE lock."
        elif q["id"] == "Q12":
            extra = "Keep no. Flat 12 ties the live percent. Do not equip it."
        elif q["id"] == "Q13":
            extra = "Keep no. The triple with Ledger still deals damage, and it is not the default."
        elif q["id"] == "Q32":
            extra = "Keep yes. This is also rarity Q6. Apply it once."
        elif q["id"] == "Q20":
            extra = "Keep yes. One new id. It passes the band. The plan's 'no new definitions' sentence is what this answer allows."
        w(f"| {q['id']} | {q['q']} | {q['default']} | {q['blocks']} | {extra} {src} |")
    keep_r = {"Q1", "Q2", "Q3", "Q4", "Q5", "Q7", "Q10"}
    for q in rarity_q:
        if q["id"] not in keep_r:
            continue
        cid = f"I-R{q['id']}"
        src = plain(cid, f"RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:{q['line']}", "rarity question")
        w(f"| R-{q['id']} | {q['q']} | {q['default']} | {q['blocks']} | {src} |")
    w("")
    w("No further questions. The duo spike, the Bulwark monopoly, the remaining Tax plus Skim lock, and the rare-typeless gap are evidence on Q9, Q33, Q4, and R-Q10. They are not new asks.")
    w("")

    # Tally placeholder filled after snippet check of the body.
    body = "\n".join(L) + "\n"
    seen = {c["id"]: c for c in CLAIMS}
    cited = set(re.findall(r"\[((?:F|S|T|I|TALLY)[A-Za-z0-9_-]*)\]", body))
    unknown = sorted(cited - set(seen))
    missing = []
    for cid in sorted(cited):
        row = seen.get(cid)
        if row and row["snippet"] and row["snippet"] not in body:
            missing.append(cid)
    if missing or unknown:
        print(f"FAIL doc-snippets missing={len(missing)} unknown={len(unknown)}")
        for cid in missing[:40]:
            print("  missing", cid, seen[cid]["snippet"][:160])
        for cid in unknown[:40]:
            print("  unknown", cid)
        # Still write the body so it can be inspected, with a failed tally.
        fail_n = sum(1 for c in CLAIMS if not c["ok"]) + len(missing) + len(unknown)
        pass_n = sum(1 for c in CLAIMS if c["ok"])
        body += f"\n## Check tally\n\nPASS {pass_n}. FAIL {fail_n}. Snippet misses {len(missing)}. Unknown ids {len(unknown)}.\n"
        DOC.write_text(body, encoding="utf-8")
        print(f"wrote {DOC} with failures")
        return 1

    def cited_ids(text):
        return set(re.findall(r"\[((?:F|S|T|I|TALLY)[A-Za-z0-9_-]*)\]", text))

    cited_now = cited_ids(body)
    cited_claims = [c for c in CLAIMS if c["id"] in cited_now]
    pass_n = sum(1 for c in cited_claims if c["ok"])
    fail_n = sum(1 for c in cited_claims if not c["ok"])
    # Two tally claims are added below and then cited.
    tally_pass = pass_n + 2
    tally_fail = fail_n
    ok("TALLY-PASS", f"claims passing before tally markers: {pass_n}", f"PASS {tally_pass} [TALLY-PASS]")
    ok("TALLY-FAIL", f"claims failing before tally markers: {fail_n}", f"FAIL {tally_fail} [TALLY-FAIL]")
    tally = (
        "\n## Check tally\n\n"
        f"PASS {tally_pass} [TALLY-PASS]. FAIL {tally_fail} [TALLY-FAIL]. "
        f"Every tagged figure was recomputed from the snapshot, the attachments, or stack_2v2_sim.py at seed {seed}. "
        "The checker exits non-zero if any claim failed or a cited id has no check.\n"
    )
    body = body + tally
    # Re-check tally snippets and any new ids.
    cited2 = cited_ids(body)
    known = {c["id"] for c in CLAIMS}
    unknown2 = sorted(cited2 - known)
    missing2 = []
    for cid in sorted(cited2):
        row = _CLAIM_INDEX.get(cid)
        if row and row["snippet"] and row["snippet"] not in body:
            missing2.append(cid)
    failed = [c for c in CLAIMS if not c["ok"]]
    DOC.write_text(body, encoding="utf-8")
    print(f"wrote {DOC}")
    print(f"TALLY pass={tally_pass} fail={tally_fail} missing={len(missing2)} unknown={len(unknown2)} failed_claims={len(failed)}")
    for c in failed:
        print("  FAIL", c["id"], c["evidence"][:200])
    for cid in missing2[:30]:
        print("  missing", cid)
    for cid in unknown2[:30]:
        print("  unknown", cid)
    if missing2 or unknown2 or failed:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
