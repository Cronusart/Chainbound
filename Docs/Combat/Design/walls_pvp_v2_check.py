#!/usr/bin/env python3
"""Post-implementation checker for the tiered Walls & Barriers v1 pass.

Usage: python Docs/Combat/Design/walls_pvp_v2_check.py [project_root]

The original balance bundle lived outside the repository at /workspace/balance/out.
This checker deliberately validates the committed project tree. If the optional sim
bundle is copied beside this file, its presence is reported so the Monte Carlo
regression can be rerun separately without making this implementation gate depend on
unversioned artifacts.
"""

from __future__ import annotations

import csv
import json
import re
import sys
from pathlib import Path


HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else HERE.parents[2]
DATA = ROOT / "Docs/Specs/Combat/Data"
PASSED = 0
FAILED = 0


def check(condition: bool, message: str) -> None:
    global PASSED, FAILED
    if condition:
        PASSED += 1
    else:
        FAILED += 1
        print(f"FAIL: {message}")


def text(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8", errors="replace")


def rows(name: str) -> dict[str, dict[str, str]]:
    with (DATA / name).open(encoding="utf-8-sig", newline="") as handle:
        source = (line for line in handle if not line.startswith("#"))
        return {row["MoveId"]: row for row in csv.DictReader(source)}


MOVES = rows("Moves.csv")
FAMILY = {
    "ember_wall": ("Ember Wall", "2", "BlockWindow:50%:Melee:2:wall_ember|WallHitRetaliation:1:wall_ember"),
    "blockade": ("Blockade", "3", "BlockWindow:60%:Melee:2:wall_blockade"),
    "ice_wall": ("Ice Wall", "2", "BlockWindow:40%:Melee:2:wall_ice|GainApOnDamageIfSpeedHigher:1:1:1:wall_ice_ap"),
    "barrier_wall": ("Buffer Barrier", "1", "BlockWindow:25%:Ranged:2:barrier_buffer"),
    "vapor_barrier": ("Vapor Barrier", "1", "BlockWindow:30%:Ranged:2:barrier_vapor"),
    "compound_guard": ("Compound Barrier", "2", "BlockWindow:35%:Ranged:2:barrier_compound"),
    "ash_guard": ("Ash Barrier", "2", "BlockWindow:35%:Ranged:2:barrier_ash"),
    "lattice_guard": ("Lattice Barrier", "2", "BlockWindow:40%:Ranged:2:barrier_lattice|BlockReflect:20%:barrier_lattice"),
}


print("A. Authored data")
for move_id, (display, ap, utility) in FAMILY.items():
    row = MOVES.get(move_id, {})
    check(row.get("DisplayName") == display, f"{move_id} display name")
    check(row.get("APCost") == ap, f"{move_id} AP cost")
    check(row.get("RechargeTurns") == "2", f"{move_id} authored recharge")
    check(row.get("Targeting") == "SingleAlly", f"{move_id} remains SingleAlly")
    check("PRIOR" in row.get("Tags", "").split("|"), f"{move_id} PRIOR tag")
    check(row.get("UtilityEffects") == utility, f"{move_id} utility payload")
    check(
        "Only one Wall or Barrier can be on a Chainling at a time." in row.get("Description", ""),
        f"{move_id} single-active card line",
    )

guard = MOVES.get("guard_posture", {})
check(guard.get("APCost") == "1" and guard.get("RechargeTurns") == "0", "Guard Posture numbers")
check(guard.get("Tags") == "ARMOR", "Guard Posture ARMOR tag")
check(
    guard.get("UtilityEffects") == "MitigationWindow:25%:2:1:armor_guard_posture",
    "Guard Posture Armor payload",
)
legend = text("Docs/Specs/Combat/Data/_LEGEND.md")
check("BlockWindow" in legend and "BlockReflect" in legend and "ARMOR" in legend, "legend opcodes and tag")


print("B. Resolver, legality, UI, and AI")
enums = text("Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs")
chainling = text("Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs")
wall_state = text("Assets/Scripts/Combat/Runtime/Data/CombatWallStateUtility.cs")
context = text("Assets/Scripts/Combat/Runtime/Data/DamageModifierContext.cs")
damage = text("Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs")
resolver = text("Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs")
eligibility = text("Assets/Scripts/Combat/Runtime/Engine/CombatActionEligibilityService.cs")
executor = text("Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs")
importer = text("Assets/Scripts/Combat/Editor/Importers/Combat2DataImporter.cs")
coordinator = text("Assets/Scripts/Combat/Runtime/UI/UGUI/CombatSelectionCoordinatorUGUI.cs")
bridge = text("Assets/Scripts/Combat/Runtime/UI/UGUI/CombatActionSelectorBridge.cs")
ai = text("Assets/Scripts/Combat/Runtime/AI/AIHelpers.cs")

check("BlockWindow" in enums and "BlockReflect" in enums, "typed utility opcodes")
check("enum BlockKind" in enums and "enum BlockTier" in enums, "block kind and tier enums")
check("case UtilityEffectType.BlockWindow:" in importer, "BlockWindow importer")
check("case UtilityEffectType.BlockReflect:" in importer, "BlockReflect importer")
check("public bool HasIntactBlock()" in chainling, "HasIntactBlock API")
check("public bool AddBlock(MitigationSource source, bool @override = false)" in chainling, "overrideable AddBlock API")
check("AppliesToContact(barrier, contactType)" in chainling, "barrier contact filter")
check("ClearTemporaryDefenses" in chainling and resolver.count("ClearTemporaryDefenses") >= 2, "swap/KO defense clear")
check('StartsWith("wall_"' in wall_state and 'StartsWith("barrier_"' in wall_state, "prefix-only wall tags")
check('IndexOf("wall"' not in wall_state, "no substring wall matching")
check("bypassBlockMitigation" in context and "bypassBlockMitigation" in damage, "D1 block-only bypass")
check("ignoreMitigation" in damage and "ConsumeMitigationHitBudgets" in resolver, "D2 mitigation/consume routing")
check("BlockIgnoredDot" in resolver, "DoT block-ignore event")
check(
    "ApplyBlockWindow" in executor
    and "case UtilityEffectType.BlockReflect:" in executor
    and "ResolveBlockReflectPercentFromMove" in executor,
    "block utility execution",
)
check("EvaluateBlockTarget" in eligibility and "BlockAlreadyQueued" in eligibility, "single-active target authority")
check("AllTargetsBlocked" in eligibility, "all-target selection denial")
check("block already active or queued" in resolver, "resolver safety net")
check("Every target already has a block." in coordinator, "all-target UI prompt")
check("already getting a block." in coordinator and "already has a block." in coordinator, "target UI prompts")
check("ReconcileQueuedBlockTargets" in bridge, "back-navigation conflict reconciliation")
check("ScoreBlockValue" in ai and "ShouldBankForGreaterBlock" in ai, "category-aware AI and banking")


print("C. Assets and generated authority")
blockade_asset = text("Assets/Data/Combat/Moves/Move_blockade.asset")
check("stackKey: wall_blockade" in blockade_asset, "Blockade stack key")
check("wallSfxId: wall_blockade" in blockade_asset, "Blockade wall SFX id")

manifest_path = ROOT / "backend/Chainbound.BattleService/manifests/gameplay-manifest.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
manifest_moves = {move["moveId"]: move for move in manifest["moves"]}
for move_id, (_, ap, utility) in FAMILY.items():
    move = manifest_moves.get(move_id, {})
    check(str(move.get("apCost")) == ap, f"manifest {move_id} AP")
    check(move.get("rechargeTurns") == 2, f"manifest {move_id} recharge")
    check(move.get("utilityEffects") == utility, f"manifest {move_id} utility")
check(
    manifest_moves.get("guard_posture", {}).get("utilityEffects")
    == "MitigationWindow:25%:2:1:armor_guard_posture",
    "manifest Guard Posture Armor payload",
)

for shared in ("Supabase/functions/_shared", "Supabase/supabase/functions/_shared"):
    root = ROOT / shared
    if not root.exists():
        continue
    ap_table = (root / "move_ap.ts").read_text(encoding="utf-8")
    flags = (root / "move_flags.generated.ts").read_text(encoding="utf-8")
    rules = (root / "pvp_rules.ts").read_text(encoding="utf-8")
    for move_id, (_, ap, _) in FAMILY.items():
        check(f'"{move_id}": {ap},' in ap_table, f"{shared} {move_id} AP")
        check(f'"{move_id}",' in flags, f"{shared} {move_id} block flag")
    check("BLOCK_MOVE_IDS" in flags and "isBlockMove" in flags, f"{shared} block helpers")
    check("block_target_duplicate" in rules, f"{shared} duplicate target validation")


print("D. Focused acceptance coverage")
tests = "\n".join(
    text(path)
    for path in (
        "Assets/Scripts/Combat/Tests/WallsBarriersTieredV1Tests.cs",
        "Assets/Scripts/Combat/Tests/CombatWallInterceptTests.cs",
        "Assets/Scripts/Combat/Tests/CombatModifierResolverAndPopupTests.cs",
        "Assets/Scripts/Combat/Tests/CombatActionSelectorBridgeTests.cs",
    )
)
required_tests = (
    "Wall_BlocksMeleeOnly_BarrierBlocksRangedOnly",
    "Block_MitigatesTierPercent_OnOneHit_ThenConsumed",
    "Block_Miss_DoesNotConsume",
    "CompoundBarrier_MeleeDefStacksOnRecast",
    "CoreBreach_IgnoresArmorAndBlock_ConsumesNeither",
    "BlockTarget_QueuedByTeammateThisTurn_IsNotSelectable",
    "Resolver_BlockedCastSafetyNet_PassNoApNoRechargeNoEffects",
    "BlockTarget_BackNavigation_ClearsInvalidQueuedBlock",
    "AI_NeverQueuesBlockedCast",
    "Resolver_BlockedCastSafetyNet_NeverFiresInScriptedBattles",
    "MoveCatalog_RarityTypePoolsUnchanged",
    "ResolverSequencesFourHitMoveAgainstOneHitBlock",
    "WallHitRetaliationDrainsAttackerApOnBlockConsume",
    "IgnoreWalls_BypassesFirstHitBarrier",
)
for test_name in required_tests:
    check(test_name in tests, f"focused test exists: {test_name}")


sim_files = ("walls_pvp_v2_sim.py", "walls_pvp_v2_sim.out.json", "walls_pvp_v2_sim.prev.json")
missing_sim = [name for name in sim_files if not (HERE / name).exists()]
if missing_sim:
    print("SIM: not evaluated; optional external artifacts are absent: " + ", ".join(missing_sim))
else:
    print("SIM: artifacts present; rerun the external simulation command from its bundle.")

print(f"RESULT: {PASSED} PASS, {FAILED} FAIL")
raise SystemExit(1 if FAILED else 0)
