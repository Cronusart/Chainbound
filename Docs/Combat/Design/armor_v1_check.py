#!/usr/bin/env python3
"""Re-verify every CURRENT-code/data claim for ARMOR_MECHANIC_V1_ANALYSIS.

Reads the unzipped snapshot and armor_2v2_sim.out.json. Prints PASS/FAIL per
claim with the file:line or CSV/asset row checked. Recomputes damage numbers
from snapshot stats. Does not edit the snapshot or the repo.

Usage:
  python3 armor_v1_check.py [snapshot] [sim_json] [analysis_md]
"""
from __future__ import annotations

import csv
import json
import math
import re
import sys
from pathlib import Path

SNAP = Path(sys.argv[1] if len(sys.argv) > 1 else "/tmp/armor_analysis/snapshot")
SIM_JSON = Path(sys.argv[2] if len(sys.argv) > 2 else "/opt/cursor/artifacts/armor_2v2_sim.out.json")
DOC = Path(sys.argv[3] if len(sys.argv) > 3 else "/opt/cursor/artifacts/ARMOR_MECHANIC_V1_ANALYSIS.md")
SIMS = Path("/tmp/armor_analysis/sims")
ROWS_OUT = Path("/opt/cursor/artifacts/armor_v1_rows.md")

RARITY = {"Common": 0, "Uncommon": 1, "Rare": 2, "Epic": 3, "Legendary": 4, "Mythic": 5}
TYPES = ["None", "Node", "Gas", "Block", "Encrypted", "Burned", "Frozen", "Forked",
         "Mined", "Staked", "Compressed", "Bot", "Glitched", "Shard", "Meme"]

CLAIMS: list[dict] = []


def add(cid, ok, where, detail, token):
    CLAIMS.append(dict(id=cid, ok=bool(ok), where=where, detail=detail, token=token))
    flag = "PASS" if ok else "FAIL"
    print(f"{flag} {cid} {where} :: {detail}")


def read(rel):
    return (SNAP / rel).read_text(encoding="utf-8", errors="replace")


def exists(rel):
    return (SNAP / rel).is_file()


def find_line(rel, needle, nth=1):
    text = read(rel)
    seen = 0
    for i, line in enumerate(text.splitlines(), 1):
        if needle in line:
            seen += 1
            if seen == nth:
                return i, line.strip()
    return None, ""


def csv_rows(rel):
    lines = [ln for ln in read(rel).splitlines() if ln and not ln.startswith("#")]
    return list(csv.DictReader(lines))


def asset_field(rel, key):
    text = read(rel)
    m = re.search(rf"(?m)^[ \t]+{re.escape(key)}:\s*(.*)$", text)
    return m.group(1).strip() if m else ""


def main():
    moves = {r["MoveId"]: r for r in csv_rows("Docs/Specs/Combat/Data/Moves.csv")}
    pools = {r["SpeciesId"]: r for r in csv_rows("Docs/Specs/Combat/Data/SpeciesMovePools.csv")}
    mods = {r["SectionId"]: r for r in csv_rows("Docs/Specs/Combat/Data/Modifiers.csv")}

    # --- C01 Guard Posture CSV ---
    gp = moves["guard_posture"]
    gp_line, _ = find_line("Docs/Specs/Combat/Data/Moves.csv", "guard_posture,Guard Posture")
    gp_ok = (
        gp["Category"] == "StatusUtility" and gp["BaseType"] == "None" and gp["Rarity"] == "Common"
        and gp["Targeting"] == "Self" and gp["APCost"] == "1" and gp["RechargeTurns"] == "0"
        and "ARMOR" in gp["Tags"] and gp["UtilityEffects"] == "MitigationWindow:25%:2:1:armor_guard_posture"
        and "numbers unchanged" in gp["Description"]
    )
    add("C01", gp_ok, f"Docs/Specs/Combat/Data/Moves.csv:{gp_line}",
        f"guard_posture {gp['Category']}/{gp['BaseType']}/{gp['Rarity']} AP {gp['APCost']} recharge {gp['RechargeTurns']} tag {gp['Tags']} util {gp['UtilityEffects']}",
        "MitigationWindow:25%:2:1:armor_guard_posture")

    # C02 not in any pool
    in_pool = [sid for sid, row in pools.items() if "guard_posture" in (row.get("LearnableMoves", "") + row.get("ExclusiveMoves", "") + row.get("FlagshipMove", ""))]
    add("C02", in_pool == [], "Docs/Specs/Combat/Data/SpeciesMovePools.csv",
        f"guard_posture learners={in_pool or 'none'}",
        "guard_posture is in no learn pool")

    # C03 asset stale
    ga = "Assets/Data/Combat/Moves/Move_guard_posture.asset"
    g_stack = asset_field(ga, "stackKey")
    g_desc = asset_field(ga, "description")
    g_ap = asset_field(ga, "apCost")
    g_mag_line, _ = find_line(ga, "magnitude:")
    g_type_line, g_type = find_line(ga, "effectType:")
    add("C03", g_stack == "wall_guard_posture" and "0.25" in read(ga) and g_ap == "1" and "ARMOR" not in read(ga).split("tags:")[0],
        f"{ga}:{g_type_line}",
        f"asset stackKey {g_stack} effectType {g_type} apCost {g_ap} desc '{g_desc}'",
        "wall_guard_posture")

    family = {
        "blockade": ("BlockWindow:60%:Melee:2:wall_blockade", "3", "2", "Blockade"),
        "ember_wall": ("BlockWindow:50%:Melee:2:wall_ember", "2", "2", "Ember Wall"),
        "ice_wall": ("BlockWindow:40%:Melee:2:wall_ice", "2", "2", "Ice Wall"),
        "barrier_wall": ("BlockWindow:25%:Ranged:2:barrier_buffer", "1", "2", "Buffer Barrier"),
        "vapor_barrier": ("BlockWindow:30%:Ranged:2:barrier_vapor", "1", "2", "Vapor Barrier"),
        "ash_guard": ("BlockWindow:35%:Ranged:2:barrier_ash", "2", "2", "Ash Barrier"),
        "compound_guard": ("BlockWindow:35%:Ranged:2:barrier_compound", "2", "2", "Compound Barrier"),
        "lattice_guard": ("BlockWindow:40%:Ranged:2:barrier_lattice", "2", "2", "Lattice Barrier"),
    }
    fam_ok = True
    fam_bits = []
    for mid, (util_prefix, ap, rech, name) in family.items():
        row = moves[mid]
        line, _ = find_line("Docs/Specs/Combat/Data/Moves.csv", f"{mid},{name}")
        ok = (row["UtilityEffects"].startswith(util_prefix) and row["APCost"] == ap
              and row["RechargeTurns"] == rech and row["DisplayName"] == name
              and "PRIOR" in row["Tags"] and "Recharge 1" in row["Description"])
        fam_ok = fam_ok and ok
        fam_bits.append(f"{mid}:{line}:AP{row['APCost']}:{row['UtilityEffects'].split('|')[0]}")
    add("C04", fam_ok, "Docs/Specs/Combat/Data/Moves.csv",
        "family CSV " + "; ".join(fam_bits),
        "BlockWindow:60%:Melee:2:wall_blockade")

    # C05 assets stale vs CSV
    stale = {
        "Move_blockade.asset": ("blockade", "0.25", "3"),
        "Move_ember_wall.asset": ("wall_ember", "effectType: 2", "2"),
        "Move_ice_wall.asset": ("wall_ice", "apCost: 3", "3"),
        "Move_barrier_wall.asset": ("Barrier Wall", "apCost: 3", "3"),
        "Move_compound_guard.asset": ("Compound Guard", "magnitude: 0.2", "3"),
        "Move_ash_guard.asset": ("Ash Guard", "magnitude: 0.2", "2"),
        "Move_lattice_guard.asset": ("Lattice Guard", "effectType: 10", "1"),
        "Move_vapor_barrier.asset": ("wall_vapor", "effectType: 2", "2"),
    }
    stale_ok = True
    stale_where = []
    for fn, needles in stale.items():
        rel = f"Assets/Data/Combat/Moves/{fn}"
        text = read(rel)
        hit = all(n in text for n in needles)
        stale_ok = stale_ok and hit
        line, _ = find_line(rel, "displayName:" if "displayName:" in text else "m_Name:")
        stale_where.append(f"{fn}:{line}")
    add("C05", stale_ok, ", ".join(stale_where),
        "playable move assets still carry pre-tier AP, percents, FullAbsorb or threshold barriers",
        "displayName: Barrier Wall")

    # C06 server AP
    ap_text = read("Supabase/supabase/functions/_shared/move_ap.ts")
    ap_ok = True
    ap_pairs = []
    for mid, (_, ap, _, _) in {**family, "guard_posture": ("", "1", "", "")}.items():
        m = re.search(rf'"{mid}":\s*(\d+)', ap_text)
        got = m.group(1) if m else "?"
        ap_ok = ap_ok and got == ap
        line = ap_text[:m.start()].count("\n") + 1 if m else 0
        ap_pairs.append(f"{mid}:{line}={got}")
    add("C06", ap_ok, "Supabase/supabase/functions/_shared/move_ap.ts",
        "server AP " + ", ".join(ap_pairs),
        '"guard_posture": 1')

    flags = read("Supabase/supabase/functions/_shared/move_flags.generated.ts")
    block_ids = re.findall(r'"([a-z0-9_]+)"', flags.split("BLOCK_MOVE_IDS", 1)[1].split("]);", 1)[0])
    expected_blocks = ["ash_guard", "barrier_wall", "blockade", "compound_guard", "ember_wall", "ice_wall", "lattice_guard", "vapor_barrier"]
    hash_m = re.search(r"manifestHash=([0-9a-f]+)", flags)
    add("C07", block_ids == expected_blocks and "guard_posture" not in block_ids,
        "Supabase/supabase/functions/_shared/move_flags.generated.ts:10",
        f"BLOCK_MOVE_IDS={block_ids} hash={hash_m.group(1) if hash_m else 'missing'}",
        "manifestHash=92d83d7d37ca6614acf4170869b645bb9b94f57f1c30ac5fdf563e4c41cb6f02")

    # C08 multiplicative mitigation + AddBlock
    line_surv, _ = find_line("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "surviving *= (1f - perSource)")
    line_add, _ = find_line("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "public bool AddBlock")
    line_refuse, _ = find_line("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "if (!@override)")
    line_hits, _ = find_line("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "source.hitsRemaining = 1")
    add("C08", all([line_surv, line_add, line_refuse, line_hits]),
        f"ChainlingInstance.cs:{line_surv}",
        f"multiplicative surviving product at {line_surv}; AddBlock at {line_add} returns false without override at {line_refuse}; hit budget forced to 1 at {line_hits}",
        "surviving *= (1f - perSource)")

    line_over, over = find_line("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs", "@override: true")
    line_react, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs", "if (CombatWallStateUtility.MovePlacesBlock(move))")
    add("C09", "@override: true" in over and line_react is not None,
        f"UtilityEffectExecutor.cs:{line_over}",
        f"ApplyBlockWindow calls AddBlock with override true at {line_over}; reactive AP returns early for block moves at {line_react}",
        "@override: true")

    line_wall, wall_line = find_line("Assets/Scripts/Combat/Runtime/Data/CombatWallStateUtility.cs", "normalized.StartsWith(\"wall_\"")
    add("C10", "barrier_" in read("Assets/Scripts/Combat/Runtime/Data/CombatWallStateUtility.cs") and line_wall is not None,
        f"CombatWallStateUtility.cs:{line_wall}",
        wall_line + " (prefix only)",
        'StartsWith("wall_"')

    # C11 tests present / missing
    tests_text = ""
    test_root = SNAP / "Assets/Scripts/Combat/Tests"
    present = set()
    for p in test_root.glob("*.cs"):
        tests_text += p.read_text(encoding="utf-8", errors="replace") + "\n"
        for name in re.findall(r"public void (\w+)\(", p.read_text(encoding="utf-8", errors="replace")):
            present.add(name)
    have = ["Wall_BlocksMeleeOnly_BarrierBlocksRangedOnly", "Block_MitigatesTierPercent_OnOneHit_ThenConsumed",
            "GuardPosture_IsArmor_StacksWithBlock_NotABlockTarget", "WallTag_PrefixOnly_FirewallIsNotAWall",
            "CoreBreach_IgnoresArmorAndBlock_ConsumesNeither", "Resolver_BlockedCastSafetyNet_PassNoApNoRechargeNoEffects",
            "AI_NeverQueuesBlockedCast", "Block_PassiveOrModifier_OverridesIntactBlock"]
    missing = ["Block_MultiHit_BreaksOnFirstHit_RestLandFull", "IceWall_GrantsAtMost1Ap_OnlyWhenHolderFaster",
               "LatticeBarrier_Reflects20PercentOfPrevented", "Block_MitigatedHit_StillAppliesRidersAndChemistry",
               "CompoundBarrier_MeleeDefStacksOnRecast", "Block_PriorityHitAfterBlock_IsMitigated",
               "Wallbreaker_IgnoreWalls_SkipBlockFirstHit_BlockSurvives", "SwapOut_ClearsBlocks_BenchDoesNotTickThem",
               "WallTimers_TickBeforeStatusTicks", "BlockMove_AllTargetsBlocked_StaysOnMoveSelect_NoApSpent",
               "BlockTarget_BackNavigation_ClearsInvalidQueuedBlock",
               "Resolver_BlockedCastSafetyNet_NeverFiresInScriptedBattles", "MoveCatalog_RarityTypePoolsUnchanged"]
    related = "ResolverSequencesFourHitMoveAgainstOneHitBlock" in present and "WallHitRetaliationDrainsAttackerApOnBlockConsume" in present
    gp_test, _ = find_line("Assets/Scripts/Combat/Tests/WallsBarriersTieredV1Tests.cs", "Is.EqualTo(0.625f)")
    add("C11", all(n in present for n in have) and all(n not in present for n in missing) and related and gp_test,
        f"WallsBarriersTieredV1Tests.cs:{gp_test}",
        f"present {len(have)}/{len(have)}; missing plan names {len(missing)}; related multi-hit and ember-drain tests exist={related}",
        "Is.EqualTo(0.625f)")

    stale_test, stale_line = find_line("Assets/Scripts/Combat/Tests/CombatIssuesRemediationV2Tests.cs",
                                       "MitigationWindow:25%:2:-1:blockade")
    add("C12", stale_test is not None and moves["blockade"]["UtilityEffects"] != "MitigationWindow:25%:2:-1:blockade",
        f"CombatIssuesRemediationV2Tests.cs:{stale_test}",
        f"old blockade assertion still expects '{stale_line.split('\"')[1] if stale_line else ''}'",
        "MitigationWindow:25%:2:-1:blockade")

    plan = read("_plans/walls_barriers_tiered_v1.plan.md")
    pending = len(re.findall(r"(?m)^    status: pending$", plan.split("---", 2)[1] if plan.startswith("---") else plan))
    # frontmatter is between first two --- 
    parts = plan.split("---")
    front = parts[1] if len(parts) > 2 else ""
    pending = len(re.findall(r"status: pending", front))
    todo_ids = re.findall(r"- id: (p\d[^\n]+)", front)
    add("C13", pending == 9 and len(todo_ids) == 9,
        "_plans/walls_barriers_tiered_v1.plan.md:1",
        f"plan frontmatter todos {todo_ids} all pending ({pending})",
        "status: pending")

    audit = "Docs/Combat/Audits/walls_barriers_tiered_v1_tests.md"
    add("C14", not exists(audit), audit,
        "P0 baseline doc is not in the snapshot",
        "walls_barriers_tiered_v1_tests.md is absent")

    add("C15", "core_breach" not in moves, "Docs/Specs/Combat/Data/Moves.csv",
        "no MoveId core_breach; CoreBreach test fabricates the move in memory",
        "core_breach is not a Moves.csv row")

    # passives / empty moves
    gs = moves["guard_stance"]
    gs_line, _ = find_line("Docs/Specs/Combat/Data/Moves.csv", "guard_stance,")
    add("C16", gs["BaseType"] == "Block" and gs["Rarity"] == "Common" and gs["APCost"] == "1"
        and gs["StageChanges"] == "MeleeDef:2:1.0:true" and gs["UtilityEffects"] == ""
        and "guard_stance" not in "".join(pools[s].get("LearnableMoves", "") for s in pools),
        f"Moves.csv:{gs_line}",
        f"guard_stance {gs['BaseType']}/{gs['Rarity']} AP {gs['APCost']} stage {gs['StageChanges']} util empty, not in a pool",
        "MeleeDef:2:1.0:true")

    sa = moves["shattered_armor"]
    sa_line, _ = find_line("Docs/Specs/Combat/Data/Moves.csv", "shattered_armor,")
    hard_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", 'defId == "shattered_armor"')
    add("C17", sa["UtilityEffects"] == "" and sa["Rarity"] == "Rare" and sa["BaseType"] == "Shard"
        and sa["Category"] == "Passive" and hard_line is not None
        and "shattered_armor" not in "".join(r.get("LearnableMoves", "") for r in pools.values()),
        f"CombatResolver.cs:{hard_line}",
        f"shattered_armor CSV util empty; hardcoded 5% + 1 AP at {hard_line}; not in a pool",
        'defId == "shattered_armor"')

    gg = moves["gecko_guard"]
    gg_line, _ = find_line("Docs/Specs/Combat/Data/Moves.csv", "gecko_guard,")
    gila = "|".join([pools["gilarime"]["LearnableMoves"], pools["gilarime"]["ExclusiveMoves"], pools["gilarime"]["FlagshipMove"]])
    add("C18", gg["Rarity"] == "Legendary" and gg["RechargeTurns"] == "999"
        and "HPBelowThreshold" in gg["TriggerCondition"] and "gecko_guard" not in gila
        and "cryo_ray" in gila,
        f"Moves.csv:{gg_line}",
        f"gecko_guard Legendary recharge {gg['RechargeTurns']} trigger {gg['TriggerCondition']}; Gilarime pool {gila}",
        "gecko_guard is not on Gilarime")

    led_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", 'defId == "immutable_ledger"')
    led = moves["immutable_ledger"]
    add("C19", led["UtilityEffects"] == "" and led["Rarity"] == "Legendary" and "20%" in led["Description"]
        and led_line and "immutable_ledger" in pools["blockwyrm"]["FlagshipMove"],
        f"CombatResolver.cs:{led_line}",
        f"immutable_ledger CSV util empty, Blockwyrm flagship, hardcoded first-hit 20% at {led_line}",
        "immutable_ledger")

    brace = mods["6.4-a"]
    cover = mods["6.4-d"]
    fade = mods["6.4-g"]
    link = mods["7.1-b"]
    grit = mods["6.3-c"]
    sparse = mods["6.8-e"]
    leg_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs", 'return 0.125f')
    grit_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", 'Equals("6.3-c"')
    link_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", 'Equals("7.1-b"')
    sparse_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "float perSlot = RollModifierPercent(mod, 0.05f)")
    add("C20", brace["EffectMin"] == "10" and brace["EffectMax"] == "15" and cover["EffectMin"] == "8" and fade["EffectMin"] == "5"
        and link["EffectMin"] == "12" and grit["EffectMin"] == "15" and sparse["EffectMin"] == "3"
        and all([leg_line, grit_line, link_line, sparse_line]),
        f"Modifiers.csv + SwapModifierResolver.cs:{leg_line}",
        f"Brace 10-15 default 0.125 at {leg_line}; Cover 8-12; Fade 5-10; Shield Link 12-18 at {link_line}; Grit 15-25 at {grit_line}; Sparse 3-8 per slot at {sparse_line}",
        "return 0.125f")

    fw_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs", '"Chemistry.Firewall"')
    wb = mods["4.3-b"]
    ig = mods["4.8-a"]
    by_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", 'section.Equals("4.3-b"')
    add("C21", bool(fw_line and by_line and "bypass" in wb["EffectDescription"].lower()),
        f"CombatResolver.cs:{by_line}",
        f"Firewall armed at StatusProcessor.cs:{fw_line}; Wallbreaker and Ignore Walls set barrierBypassChance = 1 at {by_line}",
        "barrierBypassChance")

    # config
    cfg = read("Assets/Data/Combat/Config/CombatResolverConfig_Default.asset")
    def cfgv(k):
        m = re.search(rf"(?m)^  {k}:\s*([-\d.]+)", cfg)
        return m.group(1) if m else ""
    add("C22", cfgv("enableStab") == "0" and cfgv("mitigationFloor") == "0.1" and cfgv("apPerTurn") == "2"
        and cfgv("apCap") == "10" and cfgv("chemistryAdvantageFactor") == "1.2"
        and cfgv("chemistryDisadvantageFactor") == "0.8" and cfgv("critMultiplierDefault") == "1.5"
        and cfgv("randomRollMin") == "0.85" and cfgv("mitigationSingleSourceCap") == "1",
        "CombatResolverConfig_Default.asset:16",
        f"ap {cfgv('apPerTurn')}/{cfgv('apCap')} roll {cfgv('randomRollMin')}-{cfgv('randomRollMax')} crit {cfgv('critMultiplierDefault')} chem {cfgv('chemistryAdvantageFactor')}/{cfgv('chemistryDisadvantageFactor')} stab {cfgv('enableStab')} floor {cfgv('mitigationFloor')} sourceCap {cfgv('mitigationSingleSourceCap')}",
        "enableStab: 0")

    atk_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs", "return attacker.primaryType;")
    floor_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs", "float minDamage = power * (atk / def) * levelFactor * mitigationFloor;")
    add("C23", atk_line and floor_line,
        f"DamageCalculator.cs:{atk_line}",
        f"typeless damaging moves inherit attacker primary type at {atk_line}; unrolled floor at {floor_line}; STAB gate is enableStab",
        "return attacker.primaryType;")

    swap_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "actor.ClearTemporaryDefenses();", nth=1)
    # the voluntary swap one is later; find the comment Cassette
    swap_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "Cassette Beasts-style AP continuity")
    timer_line, _ = find_line("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "ProcessMitigationTimers(c, turn);")
    add("C24", swap_line and timer_line,
        f"CombatResolver.cs:{swap_line}",
        f"voluntary swap clears temporary defenses just after {swap_line}; mitigation timers tick at {timer_line} before status processing",
        "ClearTemporaryDefenses")

    legend_armor, _ = find_line("Docs/Specs/Combat/Data/_LEGEND.md", "| ARMOR |")
    legend_block, _ = find_line("Docs/Specs/Combat/Data/_LEGEND.md", "| `BlockWindow` |")
    add("C25", legend_armor and legend_block,
        f"_LEGEND.md:{legend_armor}",
        f"ARMOR tag at {legend_armor}; BlockWindow opcode at {legend_block}",
        "hit budget is fixed at 1")

    # pool band
    species = {}
    for p in (SNAP / "Assets/Data/Combat/ChainlingSpecies").glob("*.asset"):
        t = p.read_text(encoding="utf-8", errors="replace")
        if "baseMaxHP:" not in t:
            continue
        g = lambda k, t=t: (re.search(rf"(?m)^  {k}:\s*(.*)$", t) or [None, ""])[1].strip()
        if not g("speciesId"):
            continue
        species[g("speciesId")] = dict(
            type=int(g("primaryType") or 0), hp=int(g("baseMaxHP") or 0),
            ma=int(g("baseMeleeAtk") or 0), md=int(g("baseMeleeDef") or 0),
            ra=int(g("baseRangedAtk") or 0), rd=int(g("baseRangedDef") or 0),
            spd=int(g("baseSpeed") or 0), rarity=int(g("rarity") or 0),
            starter=g("isStarter") == "1", file=p.name,
        )

    def pool_ids(row):
        ids = []
        for col in ("LearnableMoves", "ExclusiveMoves"):
            ids += [x for x in (row.get(col) or "").split("|") if x]
        # unique preserve
        out = []
        for i in ids:
            if i not in out:
                out.append(i)
        return out

    def band_check(sid, move_ids, flagship):
        body = species[sid]
        reasons = []
        if flagship not in move_ids:
            reasons.append("signature missing from pool")
        sig_r = RARITY[moves[flagship]["Rarity"]]
        above = 0
        for mid in move_ids:
            if mid == flagship:
                continue
            r = RARITY[moves[mid]["Rarity"]]
            if r >= sig_r:
                reasons.append(f"{mid} {moves[mid]['Rarity']} is not below signature {moves[flagship]['Rarity']}")
            if body["starter"] or r <= body["rarity"]:
                continue
            above += 1
            if r != body["rarity"] + 1:
                reasons.append(f"{mid} is more than one step above body")
        if not body["starter"]:
            if len(move_ids) != 5:
                reasons.append(f"pool size {len(move_ids)} != 5")
            if above > 1:
                reasons.append(f"above-body count {above}")
        return reasons, above

    current_bad = []
    for sid, row in pools.items():
        if sid in ("ghost", "cofi_lootwak"):
            continue
        if sid not in species:
            current_bad.append(f"{sid} missing asset")
            continue
        ids = pool_ids(row)
        reasons, _ = band_check(sid, ids, row["FlagshipMove"])
        if reasons:
            current_bad.append(f"{sid}: {reasons}")
    add("C26", current_bad == [], "CombatCsvIntegrityTests.cs:130 + SpeciesMovePools.csv",
        "current playable pools follow the body band" if not current_bad else "; ".join(current_bad),
        "PlayableLearnPoolsFollowTheBodyBand")

    def swapped(sid, remove, add_id):
        row = pools[sid]
        ids = [add_id if m == remove else m for m in pool_ids(row)]
        reasons, above = band_check(sid, ids, row["FlagshipMove"])
        return reasons, above, ids

    proposals = {
        "deghoul_snap_to_guard_posture": swapped("deghoul", "baseline_striker", "guard_posture"),
        "rattlestake_jab_to_guard_posture": swapped("rattlestake", "baseline_jab", "guard_posture"),
        "blockwyrm_barrage_to_guard_stance": swapped("blockwyrm", "stone_barrage", "guard_stance"),
        "marrowhead_add_shattered_keep_partition": (
            band_check("marrowhead", pool_ids(pools["marrowhead"]) + ["shattered_armor"], pools["marrowhead"]["FlagshipMove"])[0],
            None, None),
        "gilarime_add_gecko": (
            band_check("gilarime", pool_ids(pools["gilarime"]) + ["gecko_guard"], pools["gilarime"]["FlagshipMove"])[0],
            None, None),
    }
    # marrowhead add returns reasons from a tuple I built awkwardly. Fix:
    mh_reasons, mh_above = band_check(
        "marrowhead", pool_ids(pools["marrowhead"]) + ["shattered_armor"], pools["marrowhead"]["FlagshipMove"])
    gila_reasons, _ = band_check(
        "gilarime", pool_ids(pools["gilarime"]) + ["gecko_guard"], pools["gilarime"]["FlagshipMove"])
    d_reasons, d_above, _ = swapped("deghoul", "baseline_striker", "guard_posture")
    r_reasons, r_above, _ = swapped("rattlestake", "baseline_jab", "guard_posture")
    b_reasons, b_above, _ = swapped("blockwyrm", "stone_barrage", "guard_stance")
    band_ok = d_reasons == [] and r_reasons == [] and b_reasons == [] and mh_reasons != [] and gila_reasons != []
    add("C27", band_ok, "SpeciesMovePools.csv + Moves.csv rarities + species assets",
        f"deghoul swap legal above={d_above} {d_reasons or 'ok'}; rattlestake legal above={r_above} {r_reasons or 'ok'}; "
        f"blockwyrm legal above={b_above} {b_reasons or 'ok'}; marrowhead+shattered illegal {mh_reasons}; gilarime+gecko illegal {gila_reasons}",
        "deghoul swap keeps the band")

    # chemistry + formula
    matrix_text = read("Assets/Data/Combat/Type/Chemistry/TypeChemistryMatrix.asset")
    matrix = set()
    for m in re.finditer(r"attackerType: (\d+)\s+defenderType: (\d+)", matrix_text):
        matrix.add((int(m.group(1)), int(m.group(2))))

    def chem(att_type, def_type, move_type):
        at = move_type if move_type else att_type
        if at == 0:
            return 1.0, "typeless"
        if (at, def_type) in matrix:
            return 1.2, "adv"
        if (def_type, at) in matrix:
            return 0.8, "dis"
        return 1.0, "neutral"

    def hit(att, deff, power, melee, move_type, wall=0.0, armor_pct=0.0, flat=0.0, flat_first=False,
            ledger=False, crit=False, cond=1.0, roll=0.925):
        atk = att["ma"] if melee else att["ra"]
        df = deff["md"] if melee else deff["rd"]
        cm, rel = chem(att["type"], deff["type"], move_type)
        pre = (power * atk / df * roll + 1.0) * cm * cond * (1.5 if crit else 1.0)
        extra = 0.20 if ledger else 0.0
        pct = (1 - wall) * (1 - armor_pct) * (1 - extra)
        if flat_first:
            raw = max(0.0, pre - flat) * pct
        else:
            raw = pre * pct - flat
        floor_u = power * atk / df * 0.10
        dmg = max(0.0, raw, floor_u)
        return dict(pre=round(pre, 2), rel=rel, dmg=int(math.floor(dmg)), floor=round(floor_u, 2),
                    raw=round(raw, 2))

    # move types from CSV BaseType
    def mtype(mid):
        name = moves[mid]["BaseType"]
        return TYPES.index(name)

    def melee_move(mid):
        return moves[mid]["Category"] == "DamagingMelee"

    cases = []

    def pack(label, att, deff, mid, **kw):
        row = hit(species[att], species[deff], int(moves[mid]["Power"]), melee_move(mid),
                  mtype(mid) if moves[mid]["BaseType"] != "None" else 0, **kw)
        # typeless: pass 0 so chem() uses attacker type. mtype None is 0, and chem treats 0 as typeless
        # BEFORE falling through. Guard: if move type is None, pass attacker type by using move_type None
        # via the 0 path which returns typeless. THAT IS WRONG for the engine.
        # Recompute typeless with attacker primary.
        if moves[mid]["BaseType"] == "None":
            row = hit(species[att], species[deff], int(moves[mid]["Power"]), melee_move(mid),
                      species[att]["type"], **kw)
        row.update(label=label, move=mid, att=att, deff=deff)
        cases.append(row)
        return row

    # seam cond
    def seam(**kw):
        row = hit(species["crackett"], species["deghoul"], 46, True, species["crackett"]["type"], cond=1.25, **kw)
        row.update(label=kw.get("label", "seam"), move="seam_cut", att="crackett", deff="deghoul")
        return row

    cited = {}
    cited["hammer_deghoul_open"] = pack("hammer_deghoul_open", "validape", "deghoul", "hammer_sync")
    cited["hammer_deghoul_wall60"] = pack("hammer_deghoul_wall60", "validape", "deghoul", "hammer_sync", wall=0.60)
    cited["hammer_deghoul_armor25"] = pack("hammer_deghoul_armor25", "validape", "deghoul", "hammer_sync", armor_pct=0.25)
    cited["hammer_deghoul_both"] = pack("hammer_deghoul_both", "validape", "deghoul", "hammer_sync", wall=0.60, armor_pct=0.25)
    cited["hammer_deghoul_f12"] = pack("hammer_deghoul_f12", "validape", "deghoul", "hammer_sync", wall=0.60, flat=12)
    cited["hammer_deghoul_f16"] = pack("hammer_deghoul_f16", "validape", "deghoul", "hammer_sync", wall=0.60, flat=16)
    cited["hammer_deghoul_f16_first"] = pack("hammer_deghoul_f16_first", "validape", "deghoul", "hammer_sync", wall=0.60, flat=16, flat_first=True)
    cited["hammer_deghoul_f12_alone"] = pack("hammer_deghoul_f12_alone", "validape", "deghoul", "hammer_sync", flat=12)
    cited["hammer_deghoul_crit_open"] = pack("hammer_deghoul_crit_open", "validape", "deghoul", "hammer_sync", crit=True)
    cited["hammer_deghoul_crit_both"] = pack("hammer_deghoul_crit_both", "validape", "deghoul", "hammer_sync", wall=0.60, armor_pct=0.25, crit=True)
    cited["hammer_block_open"] = pack("hammer_block_open", "validape", "blockwyrm", "hammer_sync")
    cited["hammer_block_wall60"] = pack("hammer_block_wall60", "validape", "blockwyrm", "hammer_sync", wall=0.60)
    cited["hammer_block_f16"] = pack("hammer_block_f16", "validape", "blockwyrm", "hammer_sync", wall=0.60, flat=16)
    cited["hammer_block_ledger_wall_f16"] = pack("hammer_block_ledger_wall_f16", "validape", "blockwyrm", "hammer_sync", wall=0.60, ledger=True, flat=16)
    cited["snap_open"] = pack("snap_open", "validape", "deghoul", "baseline_striker")
    cited["snap_wall60"] = pack("snap_wall60", "validape", "deghoul", "baseline_striker", wall=0.60)
    cited["snap_both"] = pack("snap_both", "validape", "deghoul", "baseline_striker", wall=0.60, armor_pct=0.25)
    cited["snap_f12"] = pack("snap_f12", "validape", "deghoul", "baseline_striker", wall=0.60, flat=12)
    cited["snap_f12_alone"] = pack("snap_f12_alone", "validape", "deghoul", "baseline_striker", flat=12)
    cited["deep_open"] = pack("deep_open", "kodiax", "keyper", "deep_dig")
    cited["deep_wall60"] = pack("deep_wall60", "kodiax", "keyper", "deep_dig", wall=0.60)
    cited["deep_both"] = pack("deep_both", "kodiax", "keyper", "deep_dig", wall=0.60, armor_pct=0.25)
    cited["deep_f12"] = pack("deep_f12", "kodiax", "keyper", "deep_dig", wall=0.60, flat=12)
    cited["cryo_open"] = pack("cryo_open", "gilarime", "humpacked", "cryo_ray")
    cited["cryo_wall25"] = pack("cryo_wall25", "gilarime", "humpacked", "cryo_ray", wall=0.25)
    cited["cryo_f12"] = pack("cryo_f12", "gilarime", "humpacked", "cryo_ray", flat=12)
    cited["cryo_f16"] = pack("cryo_f16", "gilarime", "humpacked", "cryo_ray", flat=16)
    cited["pulse_open"] = pack("pulse_open", "cinislug", "blockwyrm", "baseline_shot")
    cited["pulse_wall25"] = pack("pulse_wall25", "cinislug", "blockwyrm", "baseline_shot", wall=0.25)
    cited["pulse_f12"] = pack("pulse_f12", "cinislug", "blockwyrm", "baseline_shot", flat=12)
    cited["pulse_f16"] = pack("pulse_f16", "cinislug", "blockwyrm", "baseline_shot", flat=16)
    # blade multi
    blade_parts = []
    for kw in (dict(), dict(wall=0.50), dict(wall=0.50, flat=16), dict(flat=16)):
        blade_parts.append(hit(species["kitsauto"], species["cinislug"], 12, True, species["kitsauto"]["type"], **kw))
    b_open = blade_parts[0]["dmg"] * 3
    b_ember = blade_parts[1]["dmg"] + blade_parts[0]["dmg"] * 2
    b_one = blade_parts[2]["dmg"] + blade_parts[0]["dmg"] * 2
    b_all = blade_parts[2]["dmg"] * 3  # wrong if later hits also have wall; all-hits armor WITHOUT repeating wall
    b_all = blade_parts[3]["dmg"] * 3 if False else (blade_parts[2]["dmg"] + hit(species["kitsauto"], species["cinislug"], 12, True, species["kitsauto"]["type"], flat=16)["dmg"] * 2)
    # ember on hit 1 only + flat 16 on all hits:
    h1 = hit(species["kitsauto"], species["cinislug"], 12, True, species["kitsauto"]["type"], wall=0.50, flat=16)["dmg"]
    hrest = hit(species["kitsauto"], species["cinislug"], 12, True, species["kitsauto"]["type"], flat=16)["dmg"]
    b_all = h1 + hrest * 2
    b_pct = hit(species["kitsauto"], species["cinislug"], 12, True, species["kitsauto"]["type"], wall=0.50, armor_pct=0.25)["dmg"] + b_open // 3 * 2

    formula_ok = (
        cited["hammer_deghoul_open"]["rel"] == "dis"
        and cited["deep_open"]["rel"] == "adv"
        and cited["hammer_deghoul_both"]["dmg"] == cited["hammer_deghoul_f12"]["dmg"]
        and cited["snap_f12"]["dmg"] == math.floor(cited["snap_f12"]["floor"]) or cited["snap_f12"]["raw"] < cited["snap_f12"]["floor"]
        and cited["pulse_f16"]["dmg"] <= math.floor(cited["pulse_f16"]["floor"]) + 1
    )
    # compare to sim json melee cells
    sim = json.loads(SIM_JSON.read_text())
    by = {}
    for row in sim["formulas"]:
        by[(row["case"], row["pack"])] = row

    def near(case, pack, key):
        return by[(case, pack)]["dmg"] == cited[key]["dmg"]

    json_match = (
        near("hammer_deghoul", "open", "hammer_deghoul_open")
        and near("hammer_deghoul", "wall60", "hammer_deghoul_wall60")
        and near("hammer_deghoul", "both25_60", "hammer_deghoul_both")
        and near("hammer_deghoul", "f12_after_60", "hammer_deghoul_f12")
        and near("hammer_deghoul", "f16_after_60", "hammer_deghoul_f16")
        and near("hammer_deghoul", "f16_before_60", "hammer_deghoul_f16_first")
        and near("snap_deghoul", "f12_after_60", "snap_f12")
        and near("deep_keyper", "open", "deep_open")
        and near("cryo_humpacked", "open", "cryo_open")
        and near("cryo_humpacked", "wall25", "cryo_wall25")
        and near("cryo_humpacked", "f12_after", "cryo_f12")
        and near("pulse_blockwyrm", "open", "pulse_open")
        and near("pulse_blockwyrm", "f16_after", "pulse_f16")
        and sim["multi"][0]["total"] == b_open
        and sim["multi"][3]["total"] == (h1 + blade_parts[0]["dmg"] * 2)
        and sim["multi"][4]["total"] == b_all
    )
    # ranged bug: both25_60 on cryo should NOT equal a 60% melee wall. Record it.
    cryo_both_json = by[("cryo_humpacked", "both25_60")]["dmg"]
    cryo_both_real = cited["cryo_wall25"]["dmg"]  # buffer is 25, not 60
    ranged_bug = cryo_both_json != pack("cryo_wrong_probe", "gilarime", "humpacked", "cryo_ray", wall=0.25, armor_pct=0.25)["dmg"] or True
    # The bug is: json both25_60 applies wall 60 because the zeroing requires pname startswith wall.
    cryo_armor_and_buffer = pack("cryo_both25", "gilarime", "humpacked", "cryo_ray", wall=0.25, armor_pct=0.25)
    cited["cryo_both25"] = cryo_armor_and_buffer
    ranged_mislabel = cryo_both_json != cryo_armor_and_buffer["dmg"]

    add("C28", formula_ok and json_match,
        f"species assets + TypeChemistryMatrix.asset + {SIM_JSON.name}",
        "formula recomputed from snapshot and matches sim JSON on the cited melee and safe ranged cells",
        f"hammer open {cited['hammer_deghoul_open']['dmg']}")

    add("C29", ranged_mislabel,
        "armor_2v2_sim.py formula_table",
        f"cryo both25_60 in the sim JSON is {cryo_both_json}; a real Buffer 25% plus 25% armor is {cryo_armor_and_buffer['dmg']}. Melee-wall pack names were applied to ranged rows.",
        "ranged pack names mis-apply melee walls")

    # sim result rows
    results = {(r["scenario"], r["model"]): r for r in sim["results"]}

    def cell(sc, model):
        r = results[(sc, model)]
        return (f"{sc} {model} n={r['n']} atk_win={r['atk_win']} tko3={r['tko3']} tko_mean={r['tko_mean']} "
                f"capped={r['capped']} wall_casts={r['wall_casts']} armor_casts={r['armor_casts']} "
                f"ap_walls={r['ap_walls']} ap_armor={r['ap_armor']} heavy_dmg={r['heavy_dmg']}")

    watch = [
        ("hs_open", "pct"), ("hs_wall", "pct"), ("hs_b", "pct"), ("hs_b", "f12"), ("hs_b", "f16"),
        ("hs_b", "f20"), ("hs_b", "pool24"), ("hs_b", "nostack"), ("hs_b", "f16_before"), ("hs_b", "f16_all"),
        ("hs_armor", "pct"), ("hs_armor", "f12"), ("hs_armor", "f16"),
        ("hs_swap", "f12"), ("dd_b", "pct"), ("dd_b", "f12"), ("dd_b", "nostack"),
        ("multi", "pct"), ("multi", "f16"), ("multi", "f16_all"), ("multi", "pool24"),
        ("chip", "pct"), ("chip_slow", "pct"), ("chip_slow", "f12"), ("chip_slow", "f16"), ("chip_slow", "f16_all"),
        ("chip_slow_open", "pct"), ("ray", "pct"), ("ray", "f16"),
        ("tank", "pct"), ("tank", "f16"), ("tank", "f16_all"), ("tank_open", "pct"),
        ("tank_guard", "f16"), ("stall_armor", "f16"),
    ]
    row_lines = [cell(*k) for k in watch]
    sim_ok = sim["trials"] == 500 and results[("hs_b", "pct")]["capped"] == 0.0 and results[("tank", "f16")]["capped"] < 5
    add("C30", sim_ok and len(results) >= 50,
        str(SIM_JSON),
        f"trials={sim['trials']} jobs={len(results)} sanity={sim['sanity']}",
        "trials=500")

    # round-14 divergences
    tempo = (SIMS / "tempo_2v2_sim.py").read_text(encoding="utf-8", errors="replace")
    v2 = (SIMS / "walls_pvp_v2_sim.py").read_text(encoding="utf-8", errors="replace")
    div_ok = (
        "BlockWindow" not in tempo.split("def damaging", 1)[-1][:500] or "MitigationWindow" in tempo
    )
    div_ok = ("is_wall_key" in tempo and 'k == "blockade"' in tempo
              and "guard_posture" in tempo[tempo.find("WALL_MOVES"):tempo.find("WALL_MOVES") + 200]
              and "max(1, math.floor(dmg))" in tempo
              and "One block per category" in v2)
    add("C31", div_ok, "tempo_2v2_sim.py:97 and walls_pvp_v2_sim.py:13",
        "round-14 sims predate BlockWindow: substring wall tags, guard_posture counted as a wall, damage forced to at least 1, one block per category",
        "One block per category")

    # species spot checks
    sp_ok = (species["deghoul"]["hp"] == 106 and species["deghoul"]["spd"] == 72 and species["deghoul"]["rarity"] == 0
             and species["blockwyrm"]["hp"] == 168 and species["blockwyrm"]["rarity"] == 4 and species["blockwyrm"]["spd"] == 24
             and species["validape"]["starter"] and species["crackett"]["spd"] == 92 and species["crackett"]["rarity"] == 3
             and species["cinislug"]["type"] == 5 and species["kitsauto"]["type"] == 11)
    add("C32", sp_ok, "Assets/Data/Combat/ChainlingSpecies/*.asset",
        f"deghoul hp {species['deghoul']['hp']} spd {species['deghoul']['spd']} rarity {species['deghoul']['rarity']}; "
        f"blockwyrm hp {species['blockwyrm']['hp']} md {species['blockwyrm']['md']} spd {species['blockwyrm']['spd']} rarity {species['blockwyrm']['rarity']}; "
        f"crackett spd {species['crackett']['spd']} rarity {species['crackett']['rarity']}",
        "blockwyrm rarity 4")

    # hammer power / blade hits
    add("C33", moves["hammer_sync"]["Power"] == "80" and moves["hammer_sync"]["APCost"] == "4"
        and moves["blade_flurry"]["HitMin"] == "3" and moves["blade_flurry"]["Power"] == "12"
        and moves["baseline_shot"]["Power"] == "26" and moves["seam_cut"]["ConditionalDamageMultiplier"] == "1.25"
        and moves["blockade"]["APCost"] == "3" and "PRIOR" in moves["ember_wall"]["Tags"],
        "Moves.csv",
        "hammer 80/4, blade 12 x3, pulse 26, seam x1.25, blockade AP 3, ember PRIOR",
        "ConditionalDamageMultiplier")

    # write rows appendix
    lines = ["<!-- CHECK-ROWS-BEGIN -->",
             f"trials={sim['trials']} jobs={len(results)}",
             ""]
    lines.append("### Formula cells (iv 0, roll 0.925, no crit unless named)")
    lines.append("")
    lines.append("| cell | chem | pre | floor | dmg |")
    lines.append("|---|---|---:|---:|---:|")
    for key in ["hammer_deghoul_open", "hammer_deghoul_wall60", "hammer_deghoul_armor25", "hammer_deghoul_both",
                "hammer_deghoul_f12", "hammer_deghoul_f16", "hammer_deghoul_f16_first", "hammer_deghoul_f12_alone",
                "hammer_deghoul_crit_open", "hammer_deghoul_crit_both",
                "hammer_block_open", "hammer_block_wall60", "hammer_block_f16", "hammer_block_ledger_wall_f16",
                "snap_open", "snap_wall60", "snap_both", "snap_f12", "snap_f12_alone",
                "deep_open", "deep_wall60", "deep_both", "deep_f12",
                "cryo_open", "cryo_wall25", "cryo_f12", "cryo_f16", "cryo_both25",
                "pulse_open", "pulse_wall25", "pulse_f12", "pulse_f16"]:
        r = cited[key]
        lines.append(f"| {key} | {r['rel']} | {r['pre']} | {r['floor']} | {r['dmg']} |")
    lines.append("")
    lines.append(f"blade open total {b_open}; ember on hit 1 then full {b_ember}; ember+flat16 on hit 1 then full {h1 + blade_parts[0]['dmg'] * 2}; ember on hit 1 and flat16 on every hit {b_all}; one blade hit pre {blade_parts[0]['pre']} floor {blade_parts[0]['floor']} open {blade_parts[0]['dmg']}.")
    lines.append("")
    lines.append("### Battle rows (500 trials)")
    lines.append("")
    for ln in row_lines:
        lines.append(ln)
    lines.append("")
    lines.append(f"cryo both25_60 json dmg={cryo_both_json} category-correct buffer+25% armor dmg={cryo_armor_and_buffer['dmg']}.")
    lines.append("")
    lines.append("### Trace lines (iv 0, roll 0.925, no crit, max 8 turns)")
    lines.append("")
    for name in ("hs_b_pct", "hs_b_f12", "multi_f16_one", "multi_f16_all", "tank_f16", "hs_swap_f12"):
        tr = sim["traces"][name]
        hammers = [e for e in tr["events"] if e[1] == "hit" and e[4] == "hammer_sync" and e[2] == "validape"]
        blades = [e for e in tr["events"] if e[1] == "hit" and e[4] == "blade_flurry"]
        seams = [e for e in tr["events"] if e[1] == "hit" and e[4] == "seam_cut" and e[3] == "blockwyrm"]
        swaps = [e for e in tr["events"] if e[1] == "swap"]
        end_deg = next((m for m in tr["end"] if m["sid"] == "deghoul"), None)
        end_blk = next((m for m in tr["end"] if m["sid"] == "blockwyrm"), None)
        lines.append(
            f"trace {name} winner={tr['winner']} turns={tr['turns']} "
            f"hammers={[(e[0], e[5]) for e in hammers]} "
            f"blade_hits={[(e[0], e[5], e[6]) for e in blades]} "
            f"seam_on_blockwyrm={[(e[0], e[5]) for e in seams]} "
            f"swaps={[(e[0], e[2], e[3]) for e in swaps]} "
            f"end_deghoul={None if not end_deg else (end_deg['hp'], end_deg['maxhp'])} "
            f"end_blockwyrm={None if not end_blk else (end_blk['hp'], end_blk['maxhp'])}"
        )
    lines.append("<!-- CHECK-ROWS-END -->")
    text = "\n".join(lines) + "\n"
    ROWS_OUT.write_text(text)
    print(f"WROTE {ROWS_OUT}")

    # doc check
    doc_ok = True
    if not DOC.is_file():
        add("C34", False, str(DOC), "analysis markdown is missing", "ARMOR_MECHANIC_V1_ANALYSIS.md")
        doc_ok = False
    else:
        doc = DOC.read_text(encoding="utf-8")
        missing_ids = [c["id"] for c in CLAIMS if f"[{c['id']}]" not in doc]
        missing_tokens = [c["id"] for c in CLAIMS if c["token"] not in doc]
        rows_body = "\n".join(lines[1:-1])  # without markers? require markers and every row line
        missing_rows = [ln for ln in lines if ln.startswith("| ") or ln.startswith("hs_") or ln.startswith("multi")
                        or ln.startswith("dd_") or ln.startswith("chip") or ln.startswith("ray")
                        or ln.startswith("tank") or ln.startswith("stall") or ln.startswith("blade")
                        or ln.startswith("cryo") or ln.startswith("trials=") or ln.startswith("trace ")]
        missing_rows = [ln for ln in missing_rows if ln not in doc]
        tally_pass = sum(1 for c in CLAIMS if c["ok"])
        # C34 itself not yet added
        expected_tally = f"CHECK TALLY: {tally_pass + 1} PASS / 0 FAIL" if not missing_ids and not missing_tokens and not missing_rows else None
        add("C34", not missing_ids and not missing_tokens and not missing_rows and (expected_tally or "") in doc,
            str(DOC),
            f"missing ids {missing_ids} missing tokens {missing_tokens} missing rows {len(missing_rows)}",
            "CHECK TALLY:")
        if missing_rows[:8]:
            print("MISSING ROW SAMPLE:")
            for ln in missing_rows[:8]:
                print("  ", ln)
        doc_ok = CLAIMS[-1]["ok"]

    passed = sum(1 for c in CLAIMS if c["ok"])
    failed = len(CLAIMS) - passed
    print(f"CHECK TALLY: {passed} PASS / {failed} FAIL")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
