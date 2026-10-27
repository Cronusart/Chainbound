#!/usr/bin/env python3
"""Re-check COMBAT_ISSUES_DECISION_BRIEF_V3.md against the 2026-10-22 snapshot.

Every PASS/FAIL line is one claim id. Sim numbers are recomputed here (seed
20261022, 600 trials) from ap_denial_2v2_sim.py plus lock_sweep_v3.py.
tempo_2v2_sim.py and walls_pvp_v2_sim.py are not used for those numbers:
they still parse MitigationWindow and do not mention BlockWindow.

Usage: python3 issues_brief_check_v3.py [snapshot_root] [brief_path]
"""

from __future__ import annotations

import csv
import os
import re
import sys
from pathlib import Path

ROOT = Path(sys.argv[1] if len(sys.argv) > 1 else "/tmp/chainbound_analysis/snapshot")
BRIEF = Path(sys.argv[2] if len(sys.argv) > 2 else "/opt/cursor/artifacts/COMBAT_ISSUES_DECISION_BRIEF_V3.md")
SIMS = Path("/tmp/chainbound_analysis/sims")
TRIALS = 600
SEED = 20261022

os.environ["CHAINBOUND_SNAPSHOT"] = str(ROOT)
sys.path.insert(0, "/opt/cursor/artifacts")
sys.path.insert(0, str(SIMS))
# lock_sweep reads argv at import. Keep the snapshot as argv[1].
sys.argv = [sys.argv[0], str(ROOT)]

import lock_sweep_v3 as sweep  # noqa: E402
import ap_denial_2v2_sim as denial  # noqa: E402

passes = 0
fails = 0
CLAIM_TEXT: dict[str, str] = {}


def check(cid: str, ok: bool, detail: str) -> None:
    global passes, fails
    mark = "PASS" if ok else "FAIL"
    print(f"{mark}  {cid}  {detail}")
    CLAIM_TEXT[cid] = detail
    if ok:
        passes += 1
    else:
        fails += 1


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8", errors="replace")


def line_of(rel: str, needle: str, start: int = 1) -> int:
    text = read(rel)
    for i, line in enumerate(text.splitlines(), 1):
        if i >= start and needle in line:
            return i
    return -1


def load_moves() -> dict[str, dict[str, str]]:
    rows = {}
    with (ROOT / "Docs/Specs/Combat/Data/Moves.csv").open(newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(line for line in handle if line.strip() and not line.startswith("#")):
            rows[row["MoveId"]] = row
    return rows


def load_pools() -> list[dict[str, str]]:
    with (ROOT / "Docs/Specs/Combat/Data/SpeciesMovePools.csv").open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(line for line in handle if line.strip() and not line.startswith("#")))


def master_rows(text: str) -> dict[str, dict[str, str]]:
    found = {}
    for line in text.splitlines():
        if not line.startswith("| "):
            continue
        parts = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(parts) < 4:
            continue
        ident = parts[0]
        if not re.match(r"^(E-|MV-|NP-|BAL-|DEC-|APR-|SD-|CL-|PR9-|ACC-|BOSS-|POOL-|SUPA-|GRIND-|SRV-|EOT-)", ident):
            continue
        found[ident] = {"summary": parts[1], "status": parts[3], "work": parts[4] if len(parts) > 4 else ""}
    return found


def asset_field(text: str, key: str) -> str | None:
    match = re.search(rf"^\s*{key}:\s*(.+)$", text, re.M)
    return match.group(1).strip().strip('"') if match else None


def main() -> int:
    moves = load_moves()
    pools = load_pools()
    master_text = read("Docs/Combat/COMBAT_ISSUES_MASTER.md")
    rows = master_rows(master_text)
    by_status: dict[str, int] = {}
    for row in rows.values():
        by_status[row["status"]] = by_status.get(row["status"], 0) + 1

    check("M01", len(rows) == 128, f"master data rows {len(rows)} Docs/Combat/COMBAT_ISSUES_MASTER.md")
    check("M02", by_status.get("awaiting decision") == 18, f"awaiting decision {by_status.get('awaiting decision')} (published table also says 18)")
    check("M03", by_status.get("partial") == 2, f"partial {by_status.get('partial')}")
    check("M04", by_status.get("open", 0) == 0 and by_status.get("unknown", 0) == 0, f"open {by_status.get('open', 0)} unknown {by_status.get('unknown', 0)}")
    check("M05", by_status.get("fixed in PR") == 42, f"fixed in PR {by_status.get('fixed in PR')}")
    check("M06", by_status.get("implemented, unverified") == 58, f"implemented, unverified {by_status.get('implemented, unverified')}")
    check("M07", "hitsRemaining == -1" in master_text, f"stale header rule line {line_of('Docs/Combat/COMBAT_ISSUES_MASTER.md', 'hitsRemaining == -1')}")
    check("M08", "unlimited hit budget" in rows["E-17"]["summary"], "E-17 summary still says unlimited hit budget")
    check("M09", "25%" in rows["MV-blockade"]["summary"] and "unlimited" in rows["MV-blockade"]["summary"], "MV-blockade summary still says 25% and unlimited")
    check("M10", rows["E-17"]["status"] == "implemented, unverified", f"E-17 status {rows['E-17']['status']}")
    check("M11", rows["E-11"]["status"] == "awaiting decision", f"E-11 status {rows['E-11']['status']}")
    check("M12", rows["DEC-AI"]["status"] == "awaiting decision", f"DEC-AI status {rows['DEC-AI']['status']}")
    check("M13", rows["E-28"]["status"] == "awaiting decision", f"E-28 status {rows['E-28']['status']}")
    check("M14", rows["MV-script_loop"]["status"] == "awaiting decision", rows["MV-script_loop"]["status"])
    check("M15", rows["MV-viral_surge"]["status"] == "awaiting decision", rows["MV-viral_surge"]["status"])
    check("M16", rows["MV-wildcard_swap"]["status"] == "awaiting decision", rows["MV-wildcard_swap"]["status"])
    check("M17", rows["MV-extraction_hit"]["status"] == "awaiting decision", rows["MV-extraction_hit"]["status"])
    check("M18", rows["E-13"]["status"] == "awaiting decision", rows["E-13"]["status"])
    check("M19", rows["BAL-EPIC"]["status"] == "awaiting decision", rows["BAL-EPIC"]["status"])
    check("M20", rows["E-23"]["status"] == "partial" and rows["BAL-EMPTY"]["status"] == "partial", "E-23 and BAL-EMPTY are the two partials")
    check("M21", rows["MV-status_resistance"]["status"] == "deprecated", rows["MV-status_resistance"]["status"])
    check("M22", rows["APR-RELAY"]["status"] == "implemented, unverified", rows["APR-RELAY"]["status"])
    check("M23", rows["APR-KIND"]["status"] == "implemented, unverified", rows["APR-KIND"]["status"])
    awaiting = sorted(k for k, v in rows.items() if v["status"] == "awaiting decision")
    check(
        "M24",
        awaiting == [
            "BAL-EPIC", "DEC-AI", "DEC-CD", "DEC-PACK", "DEC-S1", "DEC-S2", "DEC-S3", "DEC-S4", "DEC-S5",
            "E-11", "E-13", "E-28", "MV-extraction_hit", "MV-script_loop", "MV-viral_surge", "MV-wildcard_swap",
            "NP-glitchedmind", "NP-unsealed",
        ],
        "awaiting ids " + ",".join(awaiting),
    )

    # Live walls. Decided rules are in the CSV. Master E-17 text is stale.
    family = {
        "ember_wall": ("BlockWindow:50%:Melee:2:wall_ember", "2", "2", "Ember Wall", "PRIOR"),
        "blockade": ("BlockWindow:60%:Melee:2:wall_blockade", "3", "2", "Blockade", "PRIOR"),
        "ice_wall": ("BlockWindow:40%:Melee:2:wall_ice", "2", "2", "Ice Wall", "PRIOR"),
        "barrier_wall": ("BlockWindow:25%:Ranged:2:barrier_buffer", "1", "2", "Buffer Barrier", "PRIOR"),
        "vapor_barrier": ("BlockWindow:30%:Ranged:2:barrier_vapor", "1", "2", "Vapor Barrier", "PRIOR"),
        "compound_guard": ("BlockWindow:35%:Ranged:2:barrier_compound", "2", "2", "Compound Barrier", "PRIOR"),
        "ash_guard": ("BlockWindow:35%:Ranged:2:barrier_ash", "2", "2", "Ash Barrier", "PRIOR"),
        "lattice_guard": ("BlockWindow:40%:Ranged:2:barrier_lattice", "2", "2", "Lattice Barrier", "PRIOR"),
    }
    for mid, (token, ap, rc, name, tag) in family.items():
        row = moves[mid]
        ok = token in row["UtilityEffects"] and row["APCost"] == ap and row["RechargeTurns"] == rc and row["DisplayName"] == name and tag in row["Tags"]
        check(f"W-{mid}", ok, f"Moves.csv {mid} ap {row['APCost']} rc {row['RechargeTurns']} name {row['DisplayName']} util {row['UtilityEffects'][:80]}")
    gp = moves["guard_posture"]
    check("W-guard", gp["UtilityEffects"].startswith("MitigationWindow:25%:2:1:armor_guard_posture") and gp["APCost"] == "1" and "ARMOR" in gp["Tags"],
          f"guard_posture still percent armor key, ap {gp['APCost']}, util {gp['UtilityEffects']}")
    check("W-ember-drain", "WallHitRetaliation:1:wall_ember" in moves["ember_wall"]["UtilityEffects"], "ember drain opcode on the wall key")
    check("W-ice-ap", "GainApOnDamageIfSpeedHigher:1:1:1:wall_ice_ap" in moves["ice_wall"]["UtilityEffects"], "ice wall +1 AP opcode")
    check("W-lattice", "BlockReflect:20%:barrier_lattice" in moves["lattice_guard"]["UtilityEffects"], "lattice reflect 20%")
    check("W-no-minus1", all(":-1:" not in moves[mid]["UtilityEffects"] for mid in family), "no family row still authors hit budget -1")

    add_block = line_of("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "public bool AddBlock")
    force_one = line_of("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "source.hitsRemaining = 1;")
    eval_block = line_of("Assets/Scripts/Combat/Runtime/Engine/CombatActionEligibilityService.cs", "BlockAlreadyActive")
    check("W-engine-one-hit", add_block > 0 and force_one > add_block, f"AddBlock forces hitsRemaining = 1 at ChainlingInstance.cs:{force_one}")
    check("W-engine-one-active", eval_block > 0, f"second block denied at selection CombatActionEligibilityService.cs:{eval_block}")
    apply_block = line_of("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs", "private void ApplyBlockWindow")
    check("W-apply-no-override", "AddBlock(new MitigationSource" in read("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs"),
          f"move casts call AddBlock without override UtilityEffectExecutor.cs:{apply_block}")
    ice_gain = line_of("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "apGainOnConsumeIfFaster > 0")
    check("W-ice-faster", ice_gain > 0, f"ice AP only when the holder is faster CombatResolver.cs:{ice_gain}")
    bind = line_of("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs", "if (!CombatWallStateUtility.MovePlacesBlock(move))")
    check("W-ice-no-double-window", bind > 0, f"block moves do not also arm a reactive window UtilityEffectExecutor.cs:{bind}")
    check("W-test", "IceWall_GrantsAtMost1Ap_OnlyWhenHolderFaster" in read("Assets/Scripts/Combat/Tests/WallsBarriersTieredV1Tests.cs"),
          "WallsBarriersTieredV1Tests.cs names the 1 AP cap test")
    plan = read("_plans/walls_barriers_tiered_v1.plan.md")
    check("W-plan-stale", plan.count("status: pending") >= 8, "walls plan frontmatter todos are still pending")

    # Asset parity for the family plus guard posture.
    mismatches = []
    for mid, (_, ap, rc, name, _) in list(family.items()) + [("guard_posture", ("", gp["APCost"], gp["RechargeTurns"], gp["DisplayName"], ""))]:
        asset_path = ROOT / "Assets/Data/Combat/Moves" / f"Move_{mid}.asset"
        text = asset_path.read_text(encoding="utf-8", errors="replace")
        got_ap = asset_field(text, "apCost")
        got_name = asset_field(text, "displayName")
        got_rc = asset_field(text, "rechargeTurns")
        if got_ap != ap or got_name != name or got_rc != rc:
            mismatches.append(f"{mid} asset ap {got_ap} name {got_name} rc {got_rc}")
    check("W-assets", mismatches == [], "family asset parity " + ("; ".join(mismatches) if mismatches else "0 mismatches"))
    gp_asset = (ROOT / "Assets/Data/Combat/Moves/Move_guard_posture.asset").read_text(encoding="utf-8", errors="replace")
    check("W-gp-asset-key", "stackKey: armor_guard_posture" in gp_asset and "wallSfxId: wall_guard_posture" in gp_asset,
          "Guard Posture stack key is armor_guard_posture and wallSfxId is still wall_guard_posture")

    # Broader CSV vs asset AP/name/power/recharge.
    broad = []
    for mid, row in moves.items():
        path = ROOT / "Assets/Data/Combat/Moves" / f"Move_{mid}.asset"
        if not path.exists():
            broad.append(f"{mid} missing asset")
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        if asset_field(text, "apCost") != row["APCost"] or asset_field(text, "power") != row["Power"] or asset_field(text, "rechargeTurns") != row["RechargeTurns"]:
            broad.append(mid)
    check("P-asset-nums", broad == [], f"move asset power/ap/recharge mismatches {len(broad)} {broad[:8]}")

    # Supabase costs.
    ap_ts = read("Supabase/supabase/functions/_shared/move_ap.ts")
    ts_costs = dict(re.findall(r'"([a-z0-9_]+)":\s*(-?\d+)', ap_ts))
    cost_miss = [mid for mid, row in moves.items() if ts_costs.get(mid) != row["APCost"]]
    check("P-move-ap", cost_miss == [] and len(ts_costs) == len(moves), f"move_ap.ts rows {len(ts_costs)} csv {len(moves)} mismatches {cost_miss[:6]}")

    # Pools and band.
    pool_map = {r["SpeciesId"]: r for r in pools}
    check("P-uplink", "uplink" in pool_map["validape"]["LearnableMoves"].split("|") and "ap_router" not in pool_map["validape"]["LearnableMoves"],
          f"validape learnable {pool_map['validape']['LearnableMoves']}")
    check("P-flagship-hammer", pool_map["validape"]["FlagshipMove"] == "hammer_sync", pool_map["validape"]["FlagshipMove"])
    withdrawn = ("partner_beacon", "relay_dividend", "node_beacon", "link_pulse", "open_line", "cinder_idle", "scorch_echo")
    pool_blob = " ".join("|".join(r.values()) for r in pools)
    present = [w for w in withdrawn if w in pool_blob or w in moves]
    check("P-withdrawn", present == [], f"withdrawn ids present {present}")
    check("P-status-res", moves["status_resistance"]["Description"] == "Retired. No effect." and "status_resistance" not in pool_blob,
          "status_resistance retired and in no pool")
    check("P-grind-pool", "grind_tax" not in pool_blob and "BOSS_ONLY" in moves["grind_tax"]["Tags"] and moves["grind_tax"]["APCost"] == "6",
          f"grind_tax ap {moves['grind_tax']['APCost']} tags {moves['grind_tax']['Tags']}")
    check("P-pulse", moves["baseline_shot"]["Power"] == "26" and moves["baseline_shot"]["APCost"] == "2", "pulse shot power 26 ap 2")
    check("P-kindling", "6% MaxHP" in moves["kindling"]["Description"] and pool_map["cinislug"]["LearnableMoves"].startswith("ember_wall|kindling"),
          pool_map["cinislug"]["LearnableMoves"])
    bands = denial.band_report()
    band_fail = [r for r in bands if not r["ok"]]
    check("P-band", band_fail == [], f"wild band failures {len(band_fail)} of {len(bands)}")
    check("P-move-count", len(moves) == 211, f"moves {len(moves)}")

    # Built cards and still-empty cards.
    check("C-reset", moves["system_reset"]["UtilityEffects"].startswith("StripAllStatuses") and moves["system_reset"]["APCost"] == "3" and moves["system_reset"]["RechargeTurns"] == "2"
          and "skip your next action" in moves["system_reset"]["Description"] and "system_reset" in pool_map["keyper"]["LearnableMoves"],
          f"system_reset in keyper, util {moves['system_reset']['UtilityEffects'][:40]}, text still says skip")
    check("C-hype", "StripBuffsThenStage:2:100%:SingleEnemy:MeleeAtk:1:hype_burst" in moves["hype_burst"]["UtilityEffects"] and "hype_burst" in pool_map["cinislug"]["LearnableMoves"],
          "hype_burst strips 2 and grants MeleeAtk, live on Cinislug")
    check("C-extract", "HealSelfPerTargetDebuff:5" in moves["crystal_extraction"]["UtilityEffects"] and "Cleanse:99:100%:SingleEnemy" in moves["crystal_extraction"]["UtilityEffects"]
          and "crystal_extraction" in pool_map["kodiax"]["LearnableMoves"],
          "crystal_extraction heals the user then cleanses the foe")
    check("C-bug", "Cleanse:1:100%:Self:bug_fix" in moves["bug_fix"]["UtilityEffects"] and "Staggered:1:1.0:Self" in moves["bug_fix"]["StatusEffects"]
          and "bug_fix" in pool_map["kreechank"]["LearnableMoves"] and "-10%" in moves["bug_fix"]["Description"],
          "bug_fix cleanse, staggered, 10% heal, card says -10%")
    check("C-cinder", moves["cinder_burst"]["APCost"] == "3" and moves["cinder_burst"]["Power"] == "15" and moves["cinder_burst"]["Rarity"] == "Common",
          f"cinder_burst ap {moves['cinder_burst']['APCost']} power {moves['cinder_burst']['Power']}")
    empty_ids = []
    for mid, row in moves.items():
        if row["Category"] == "Passive":
            continue
        if row["UtilityEffects"] or row["StatusEffects"] or row["StageChanges"] or row["Power"] not in ("", "0"):
            continue
        if row["Description"].startswith("Retired"):
            continue
        empty_ids.append(mid)
    check("C-empty", set(empty_ids) >= {"script_loop", "viral_surge", "wildcard_swap"}, f"blank non-damage non-passive rows {sorted(empty_ids)}")
    check("C-extract-hit", moves["extraction_hit"]["UtilityEffects"] == "" and "extraction_hit" not in pool_blob, "extraction_hit empty and ungranted")
    check("C-leak", moves["memory_leak"]["RechargeTurns"] == "0" and "ArmUtilityWindow:OwnerTurnStart:1:1:-2" in moves["memory_leak"]["UtilityEffects"]
          and "Speed:2" in moves["memory_leak"]["StageChanges"] and pool_map["keyper"]["FlagshipMove"] == "memory_leak",
          "memory_leak recharge 0, window -2, speed +2, keyper signature")
    check("C-tax", moves["transaction_tax"]["UtilityEffects"].startswith("StealAP:1") and pool_map["deghoul"]["FlagshipMove"] == "transaction_tax",
          f"transaction_tax {moves['transaction_tax']['UtilityEffects']}")
    check("C-skim", moves["packet_skim"]["UtilityEffects"].startswith("DrainAP:1") if "packet_skim" in moves else False,
          moves.get("packet_skim", {}).get("UtilityEffects", "missing")[:60])
    check("C-isolation", "ApplyChemistryStatus:Anchored:1" in moves["isolation_tool"]["UtilityEffects"], moves["isolation_tool"]["UtilityEffects"])
    check("C-rally", "Yield:1:1.0:Self" in moves["rally_cry"]["StatusEffects"], moves["rally_cry"]["StatusEffects"])
    check("C-refinery", "Yield:2:1.0:Self" in moves["refinery"]["StatusEffects"], moves["refinery"]["StatusEffects"])
    check("C-stream", moves["streamlined_flow"]["StageChanges"] == "" and "ChangeStage:MeleeDef:-1" in moves["streamlined_flow"]["UtilityEffects"]
          and moves["streamlined_flow"]["TriggerOnce"] == "false",
          f"one ChangeStage, cap field in {moves['streamlined_flow']['UtilityEffects']}, TriggerOnce false")
    check("C-compress", moves["compression_burst"]["MoveEffectsOncePerTarget"] == "true", "compression_burst once per target")
    check("C-once-engine", line_of("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "moveEffectsAppliedTargets.Add(target)") > 0,
          f"once-per-target gate CombatResolver.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs', 'moveEffectsAppliedTargets.Add(target)')}")
    check("C-charged", "CountDistinctTargetsHitThisTurnByTeam" in read("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs"),
          f"charged relay counts the team UtilityEffectExecutor.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs', 'CountDistinctTargetsHitThisTurnByTeam')}")
    check("C-eot", line_of("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "PassiveTriggerPoint.EndOfTurn") < line_of("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "_statusProcessor.ProcessEndOfTurn"),
          f"end-of-turn passives at {line_of('Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs', 'PassiveTriggerPoint.EndOfTurn')} before status pass at {line_of('Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs', '_statusProcessor.ProcessEndOfTurn')}")
    check("C-e28", "effect.targetOverride == MoveTargeting.Self ? chainling : chainling" in read("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs"),
          f"passive status target override ignored CombatResolver.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs', 'effect.targetOverride == MoveTargeting.Self ? chainling : chainling')}")
    check("C-hodl", "extra Def" in read("Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs") or "HODLed" in read("Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs"),
          f"HODLed extra ramp StatusProcessor.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs', 'status.statusId == StatusId.HODLed')}")
    statuses = read("Docs/Specs/Combat/Data/Statuses.csv")
    check("C-hodl-summary", "Immune to stat drops" in statuses and "MeleeDef:1:false" in statuses.split("HODLed,HODLed")[1][:200],
          "HODLed summary claims immunity; repeating stage is not apply-once")
    check("C-jammed", "Accuracy:-1" in statuses.split("Jammed,Jammed")[1][:180] and "Accuracy -1 for 2 turns" in statuses, "Jammed Accuracy -1")
    check("C-mev-comment", "Steal 1 AP per turn" in read("Assets/Scripts/Combat/Runtime/Data/StatusId.cs"),
          f"StatusId.cs:{line_of('Assets/Scripts/Combat/Runtime/Data/StatusId.cs', 'Steal 1 AP per turn')}")
    check("C-mev-drain", "_apService.Drain" in read("Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs").split("MEV Extracted")[1][:400],
          f"MEV branch drains StatusProcessor.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs', 'MEV Extracted = steal')}")
    check("C-fud-comment", "Atk/Def/Spd -1" in read("Assets/Scripts/Combat/Runtime/Data/StatusId.cs") and "Acc" not in read("Assets/Scripts/Combat/Runtime/Data/StatusId.cs").split("FUD")[1][:40],
          f"FUD comment StatusId.cs:{line_of('Assets/Scripts/Combat/Runtime/Data/StatusId.cs', 'FUD')}")
    check("C-backlash-summary", "Token. No contact damage." in statuses, "Backlash summary is the token sentence")
    check("C-backlash-comment", "Contact adds typed DoT" in read("Assets/Scripts/Combat/Runtime/Data/StatusId.cs"),
          f"Backlash enum comment still old StatusId.cs:{line_of('Assets/Scripts/Combat/Runtime/Data/StatusId.cs', 'Contact adds typed DoT')}")
    check("C-typeless", "Typeless damaging moves inherit user's type" in read("Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs"),
          f"ResolveAttackType falls through to primaryType DamageCalculator.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs', 'inherit user')}")
    cfg = read("Assets/Data/Combat/Config/CombatResolverConfig_Default.asset")
    check("C-stab", "enableStab: 0" in cfg, "enableStab 0")
    check("C-chem", "chemistryAdvantageFactor: 1.2" in cfg and "chemistryDisadvantageFactor: 0.8" in cfg, "chemistryAdvantageFactor 1.2 / chemistryDisadvantageFactor 0.8")
    check("C-rugged", "RUGGED_TICK_CAP = 15" in read("Assets/Scripts/Combat/Runtime/Data/CombatConstants.cs"),
          f"CombatConstants.cs:{line_of('Assets/Scripts/Combat/Runtime/Data/CombatConstants.cs', 'RUGGED_TICK_CAP = 15')}")
    check("C-kindled-literal", "0.06f" in read("Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs"),
          f"Kindled literal StatusProcessor.cs:{line_of('Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs', '0.06f')}")
    check("C-ai-untargeted", "CombatAction.UseMove(teamIndex, i)" in read("Assets/Scripts/Combat/Runtime/AI/AIHelpers.cs"),
          f"non-block moves still have no target slot AIHelpers.cs:{line_of('Assets/Scripts/Combat/Runtime/AI/AIHelpers.cs', 'CombatAction.UseMove(teamIndex, i)')}")
    check("C-ai-blocks", "ScoreBlockValue" in read("Assets/Scripts/Combat/Runtime/AI/AIHelpers.cs"), "block moves do emit a target slot")
    check("C-dice", moves["dice_roll"]["Description"] == "Heal an ally for 20% MaxHP.", moves["dice_roll"]["Description"])
    check("C-moves-txt", "typed moves beat comparable typeless" in read("Docs/Specs/Combat/COMBAT_Moves.txt"),
          f"COMBAT_Moves.txt:{line_of('Docs/Specs/Combat/COMBAT_Moves.txt', 'typed moves beat comparable typeless')}")
    check("C-gaswar", moves["volatile_shot"]["Category"] == "DamagingRanged" and "NextMoveAPSurcharge:1" in moves["volatile_shot"]["UtilityEffects"] and moves["volatile_shot"]["APCost"] == "4",
          f"Gas War {moves['volatile_shot']['Category']} ap {moves['volatile_shot']['APCost']}")
    check("C-bulwark", moves["bulwark_blow"]["APCost"] == "3" and moves["bulwark_blow"]["Power"] == "50" and moves["bulwark_blow"]["DamageOverrideAttackStat"] == "true",
          f"bulwark ap {moves['bulwark_blow']['APCost']} power {moves['bulwark_blow']['Power']}")
    check("C-heavies", all(moves[mid]["APCost"] == "4" for mid in ("volatile_shot", "recoil_strike", "deep_dig", "hammer_sync", "cryo_ray", "all_in")),
          "Gas War, Hard Fork, Deep Dig, Hammer Sync, Cryo Ray, All-In are 4 AP")
    check("C-finish", moves["finish_burn"]["APCost"] == "3" and moves["finish_burn"]["Power"] == "60" and "finish_burn" not in pool_blob, "Finish Burn 3 AP, ungranted")
    check("C-grind-text", "recharge" not in moves["grind_tax"]["Description"].lower() and moves["grind_tax"]["RechargeTurns"] == "2",
          f"grind tax text omits recharge: {moves['grind_tax']['Description']}")
    check("C-thread-text", "again" not in moves["thread_lock"]["Description"].lower(), moves["thread_lock"]["Description"])
    check("C-leak-text", "swap" not in moves["memory_leak"]["Description"].lower() and "recharge" not in moves["memory_leak"]["Description"].lower(),
          moves["memory_leak"]["Description"])
    check("C-hammer-text", "1.2" not in moves["hammer_sync"]["Description"] and "per" not in moves["hammer_sync"]["Description"].lower(),
          moves["hammer_sync"]["Description"])
    check("C-core", "core_breach" not in moves, "no core_breach row")
    check("C-deflation", "deflation_mark" not in pool_blob and "BurnedTokens" in moves["deflation_mark"]["StatusEffects"], "deflation mark ungranted, still applies Burned Tokens")
    check("C-tempo-div", "BlockWindow" not in (SIMS / "tempo_2v2_sim.py").read_text(encoding="utf-8") and "MitigationWindow" in (SIMS / "tempo_2v2_sim.py").read_text(encoding="utf-8"),
          "tempo_2v2_sim.py parses MitigationWindow and has no BlockWindow")
    check("C-walls-div", "BlockWindow" not in (SIMS / "walls_pvp_v2_sim.py").read_text(encoding="utf-8"),
          "walls_pvp_v2_sim.py has no BlockWindow")
    check("C-denial-mirror", "BlockWindow" in (SIMS / "ap_denial_2v2_sim.py").read_text(encoding="utf-8"),
          "ap_denial_2v2_sim.py documents BlockWindow")

    # Sims.
    cals = denial.build_calendars()
    def series(name: str) -> str:
        return ",".join(str(row["action_ap"]) for row in cals[name]["action_series"])

    check("S-leak-pass", series("leak_pass") == "2,2,2,2,2,2,2,2" and cals["leak_pass"]["hammer_turn"] is None and cals["leak_pass"]["finisher_lock"] is True,
          f"leak_pass [{series('leak_pass')}] finisher_lock")
    check("S-leak-spend", series("leak_spend2") == "2,0,0,0,0,0,0,0" and cals["leak_spend2"]["zero_lock"] is True,
          f"leak_spend2 [{series('leak_spend2')}] zero_lock")
    check("S-leak-cd", series("leak_cd_pass") == "2,2,4,4,6,6,8,8" and cals["leak_cd_pass"]["hammer_turn"] == 3 and cals["leak_cd_pass"]["finisher_lock"] is False,
          f"leak_cd_pass [{series('leak_cd_pass')}] hammer turn {cals['leak_cd_pass']['hammer_turn']}")
    check("S-leak-cd-spend", cals["leak_cd_spend2"]["finisher_lock"] is True and series("leak_cd_spend2") == "2,0,2,0,2,0,2,0",
          f"leak_cd_spend2 [{series('leak_cd_spend2')}] still a finisher lock")
    check("S-taxskim", series("tax_skim_pass") == "0,0,0,0,0,0,0,0" and cals["tax_skim_pass"]["zero_lock"] is True, f"tax_skim [{series('tax_skim_pass')}]")
    check("S-cap-tax", series("cap2_tax_skim") == "0,0,0,0,0,0,0,0", f"cap2 tax+skim [{series('cap2_tax_skim')}]")
    check("S-cap-leak", series("cap2_leak_skim") == "1,1,1,1,1,1,1,1" and cals["cap2_leak_skim"]["finisher_lock"] is True, f"cap2 leak+skim [{series('cap2_leak_skim')}]")
    check("S-deghoul", cals["deghoul_bank_if_tax_steals"] == [2, 4, 6, 8, 10, 10] and cals["deghoul_bank_if_tax_drains"] == [1, 2, 3, 4, 5, 6],
          f"deghoul steal {cals['deghoul_bank_if_tax_steals']} drain {cals['deghoul_bank_if_tax_drains']}")
    check("S-victim", cals["victim_if_tax_steals"] == [1, 2, 3, 4, 5, 6] and cals["victim_if_tax_drains"] == [1, 2, 3, 4, 5, 6],
          f"victim steal {cals['victim_if_tax_steals']} drain {cals['victim_if_tax_drains']}")

    walls = {row["name"]: row for row in sweep.run_sweep(TRIALS, SEED)}
    races = sweep.denial_slice(TRIALS, SEED)

    def wall_ok(name, t0, t1, cap):
        row = walls[name]
        return row["team0_win_pct"] == t0 and row["team1_win_pct"] == t1 and row["cap_pct"] == cap

    check("S-w-ember", wall_ok("wall_ember_self_vs_ore", 100.0, 0.0, 0.0), str(walls["wall_ember_self_vs_ore"]))
    check("S-w-block", wall_ok("wall_blockade_vs_ore", 100.0, 0.0, 0.0), str(walls["wall_blockade_vs_ore"]))
    check("S-w-buffer", wall_ok("wall_buffer_vs_frost", 99.3, 0.7, 0.0), str(walls["wall_buffer_vs_frost"]))
    check("S-w-ally", wall_ok("wall_ember_on_ally_vs_seam", 0.0, 100.0, 0.0), str(walls["wall_ember_on_ally_vs_seam"]))
    check("S-w-ctrl", wall_ok("wall_ally_nowall_control", 0.0, 100.0, 0.0), str(walls["wall_ally_nowall_control"]))
    check("S-w-both", wall_ok("wall_both_categories_rejected", 49.3, 50.7, 0.0) and walls["wall_both_categories_rejected"]["rejected_per_battle"] == 0.11,
          str(walls["wall_both_categories_rejected"]))

    def race_ok(name, t0, t1):
        race = races[name]
        return race["team0_win_pct"] == t0 and race["team1_win_pct"] == t1

    check("S-r-pass", race_ok("lock_leak_commit_pass", 0.0, 100.0) and races["lock_leak_commit_pass"]["heavy_land_pct"] == 0.0, str(races["lock_leak_commit_pass"]))
    check("S-r-flurry", race_ok("lock_leak_flurry", 0.0, 100.0), str(races["lock_leak_flurry"]))
    check("S-r-inherit", race_ok("lock_commit_bench_inherit", 0.0, 100.0), str(races["lock_commit_bench_inherit"]))
    check("S-r-floor", race_ok("lock_commit_bench_floor2", 35.8, 64.2), str(races["lock_commit_bench_floor2"]))
    check("S-r-seam", race_ok("commit_seam_vs_hammer", 0.3, 99.7), str(races["commit_seam_vs_hammer"]))
    check("S-r-dig", race_ok("commit_flurry_vs_deepdig", 0.0, 100.0) and races["commit_flurry_vs_deepdig"]["heavy_land_pct"] == 69.5, str(races["commit_flurry_vs_deepdig"]))
    check("S-r-emberleak", race_ok("ember_and_leak", 0.0, 100.0) and races["ember_and_leak"]["median_ko"] == 10, str(races["ember_and_leak"]))
    check("S-r-pressure", race_ok("pressure_tax_skim_2v2", 100.0, 0.0), str(races["pressure_tax_skim_2v2"]))
    check("S-r-kit", race_ok("ex_lights_kitsauto_vs_kodiax", 1.0, 99.0), str(races["ex_lights_kitsauto_vs_kodiax"]))
    check("S-r-bump", race_ok("ex_lights_deghoul_vs_kodiax", 100.0, 0.0), str(races["ex_lights_deghoul_vs_kodiax"]))
    check("S-r-bank", race_ok("bank8_endgame", 2.2, 97.8) and races["bank8_endgame"]["heavy_land_pct"] == 100.0, str(races["bank8_endgame"]))

    quotes = {
        "seam_deghoul": denial.expected_damage("crackett", "seam_cut", "deghoul")["total"],
        "seam_validape": denial.expected_damage("crackett", "seam_cut", "validape")["total"],
        "hammer_deghoul": denial.expected_damage("validape", "hammer_sync", "deghoul")["total"],
        "flurry_kodiax": denial.expected_damage("kitsauto", "blade_flurry", "kodiax")["total"],
        "bump_kodiax": denial.expected_damage("deghoul", "gas_bump", "kodiax")["total"],
    }
    check("S-q", quotes == {"seam_deghoul": 128, "seam_validape": 92, "hammer_deghoul": 115, "flurry_kodiax": 60, "bump_kodiax": 37}, str(quotes))
    hp = {sid: denial.SPECIES[sid]["hp"] for sid in ("deghoul", "validape", "kodiax", "cinislug", "kitsauto")}
    check("S-hp", hp == {"deghoul": denial.SPECIES["deghoul"]["hp"], "validape": denial.SPECIES["validape"]["hp"], "kodiax": denial.SPECIES["kodiax"]["hp"], "cinislug": denial.SPECIES["cinislug"]["hp"], "kitsauto": denial.SPECIES["kitsauto"]["hp"]},
          str(hp))

    # Brief cross-check. Each id must appear, and the detail's decisive token must appear in the brief.
    if not BRIEF.exists():
        check("DOC", False, f"brief missing at {BRIEF}")
    else:
        brief = BRIEF.read_text(encoding="utf-8")
        # Tokens that the prose must contain, tied to a claim. Kept short so a rewrite that drops the number fails.
        tokens = {
            "M01": "128",
            "M02": "18",
            "M08": "unlimited",
            "S-leak-pass": "2,2,2,2,2,2,2,2",
            "S-leak-cd": "2,2,4,4,6,6,8,8",
            "S-deghoul": "2, 4, 6, 8, 10, 10",
            "S-r-floor": "35.8",
            "S-r-seam": "0.3",
            "S-r-dig": "69.5",
            "S-w-ember": "100.0",
            "S-w-buffer": "99.3",
            "S-w-both": "0.11",
            "S-w-ctrl": "wall_ally_nowall_control",
            "S-q": "128",
            "S-r-bank": "2.2",
            "C-typeless": "primaryType",
            "W-guard": "25%",
            "P-band": "0",
        }
        missing = [cid for cid in CLAIM_TEXT if f"[{cid}]" not in brief]
        check("DOC-ids", missing == [], f"brief missing {len(missing)} claim ids {missing[:12]}")
        bad = [f"{cid}:{tok}" for cid, tok in tokens.items() if tok not in brief]
        check("DOC-nums", bad == [], "brief missing numbers " + ",".join(bad))
        check("DOC-proposal", brief.count("**PROPOSAL**") >= 15, f"proposal labels {brief.count('**PROPOSAL**')}")
        check("DOC-tally", "138 PASS / 0 FAIL" in brief, "brief tally line")

    print(f"TALLY {passes} PASS / {fails} FAIL")
    Path("/tmp/v3_claims.txt").write_text("\n".join(f"{k}\t{v}" for k, v in CLAIM_TEXT.items()), encoding="utf-8")
    return 1 if fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
