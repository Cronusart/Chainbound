#!/usr/bin/env python3
"""Re-check every current-code claim in AP_DENIAL_ENDGAME_ANALYSIS.md.

Reads the unzipped snapshot and re-runs the endgame sim. Prints PASS or FAIL
per claim with the file:line or CSV row it checked. Exits 1 if anything fails,
including a [Cxx] tag in the doc that this script does not know.
"""
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

SNAP = Path("/tmp/chainbound_analysis/snapshot")
SIMS = Path("/tmp/chainbound_analysis/sims")
DOC = Path("/opt/cursor/artifacts/AP_DENIAL_ENDGAME_ANALYSIS.md")
SIM_PATH = Path("/opt/cursor/artifacts/ap_denial_2v2_sim.py")

results: list[tuple[str, bool, str]] = []


def add(cid: str, ok: bool, detail: str) -> None:
    results.append((cid, ok, detail))
    print(f"{'PASS' if ok else 'FAIL'} {cid}  {detail}")


def read(rel: str) -> str:
    return (SNAP / rel).read_text(encoding="utf-8", errors="replace")


def find_line(rel: str, needle: str) -> int:
    lines = read(rel).splitlines()
    hits = [i + 1 for i, line in enumerate(lines) if needle in line]
    if len(hits) != 1:
        raise AssertionError(f"{rel} needle {needle!r} hits {hits}")
    return hits[0]


def cite(cid: str, rel: str, needle: str, note: str) -> str:
    try:
        line = find_line(rel, needle)
        token = f"{Path(rel).name}:{line}"
        add(cid, True, f"{token}  {note}")
        return token
    except Exception as exc:  # noqa: BLE001 — one claim, then continue
        add(cid, False, f"{rel}  {note}  {exc}")
        return ""


def csv_row(rel: str, move_id: str) -> tuple[int, str]:
    lines = read(rel).splitlines()
    hits = [(i + 1, line) for i, line in enumerate(lines) if line.startswith(move_id + ",")]
    if len(hits) != 1:
        raise AssertionError(f"{move_id} rows { [h[0] for h in hits] }")
    return hits[0]


def load_sim():
    sys.argv = ["ap_denial_2v2_sim.py", str(SNAP), "/tmp/ap_denial_check_out.json"]
    spec = importlib.util.spec_from_file_location("ap_denial_2v2_sim", SIM_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    if not SNAP.is_dir():
        print(f"FAIL snapshot missing at {SNAP}")
        return 1
    doc = DOC.read_text(encoding="utf-8") if DOC.exists() else ""
    sim = load_sim()

    tokens = []

    def keep(cid, rel, needle, note):
        token = cite(cid, rel, needle, note)
        if token:
            tokens.append((cid, token))

    # --- engine ---
    keep("C01", "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset", "apPerTurn: 2", "regen is +2")
    keep("C02", "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset", "apCap: 10", "bank ceiling is 10")
    keep("C03", "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset", "enableStab: 0", "STAB is off")
    keep("C04", "Assets/Scripts/Combat/Runtime/Data/CombatConstants.cs", "public const int AP_PER_TURN = 2;", "constant matches config")
    keep("C05", "Assets/Scripts/Combat/Runtime/Data/CombatConstants.cs", "public const int AP_CAP = 10;", "constant cap matches config")
    keep("C06", "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs", "int apCap = _resolverConfig.apCap;", "Gain clamps to the cap")
    keep("C07", "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs", "target.currentAP = Math.Max(0, target.currentAP - amount);", "Drain floors at 0 and has no per-target cap")
    keep("C08", "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs", "int drained = Drain(target, amount, turn, mutationSource);", "Steal drains, then gains what was removed")
    keep("C09", "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs", "HasActiveStatus(target, StatusId.ThreadLock)", "Thread Lock blocks Gain")
    keep("C10", "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs", "_apService.Gain(chainling, _resolverConfig.apPerTurn, turn, \"TurnRegen\");", "living actives regen at turn start")
    keep("C11", "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs", "chainling.threadLockBlockedUntilTurn = turn + 2;", "Thread Lock cannot be reapplied the next turn")
    keep("C12", "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs", "public void TickRechargeWhileBenched", "recharge ticks on the bench")
    keep("C13", "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs", "status.origin != StatusOrigin.General || def == null", "cleanse skips chemistry statuses such as Drained")
    keep("C14", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "ApplyPendingTurnStartStatusesForTeam(team, turn);", "turn-start statuses land before regen")
    keep("C15", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "// AP gain first", "2v2 turn start runs regen, then OwnerTurnStart windows")
    keep("C16", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "actor.ClearArmedUtilityWindows();", "voluntary swap clears armed windows")
    keep("C17", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "newActive.currentAP = slotAp;", "voluntary swap copies slot AP onto the incoming")
    keep("C18", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "active.ClearArmedUtilityWindows();", "KO auto-swap clears the corpse window")
    keep("C19", "Assets/Scripts/Combat/Runtime/Data/TeamState.cs", "chainlings[benchIdx].currentAP = slotAp;", "KO replacement inherits the corpse AP")
    keep("C20", "Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs", "if (!@override)", "a second intact block is rejected")
    keep("C21", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "if (firstFoe == null || firstFoe.currentAP <= holder.currentAP)", "Fee Collector drains 1 only when the foe has more AP")
    keep("C22", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "int tax = Math.Max(1, (int)(actor.EffectiveMaxHP * 0.10f));", "Leaked taxes 10% max HP on action")
    keep("C23", "Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs", "Kindled burns", "Kindled 6% is handled before the other end step")
    keep("C24", "Assets/Scripts/Combat/Runtime/Data/StatusId.cs", "MEVExtracted        = 206,  // Steal 1 AP per turn", "StatusId comment says steal")
    keep("C25", "Assets/Scripts/Combat/Runtime/AI/UtilityStatusAI.cs", "score += 30f;", "utility base is +30")
    keep("C26", "Assets/Scripts/Combat/Runtime/AI/UtilityStatusAI.cs", "score += move.statusEffects.Count * 15f;", "utility adds 15 per status")
    keep("C27", "Assets/Scripts/Combat/Runtime/AI/UtilityStatusAI.cs", "score -= effectiveCost * 1f;", "utility subtracts the AP cost once")
    keep("C28", "Assets/Scripts/Combat/Runtime/AI/AggressiveAI.cs", "score = 0.1f;", "aggressive scores non-damage at 0.1")
    keep("C29", "Assets/Scripts/Combat/Runtime/AI/AIHelpers.cs", "if (mitigation <= 0.5f || move.apCost <= CombatConstants.AP_PER_TURN || actor.currentAP >= move.apCost)", "banking is only for a block above 50% that costs more than 2")
    keep("C30", "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs", "private void CheckAPRefund", "the only AP refund path is a section 3.1 modifier")

    # tempo sim divergence, read from the attachment, not the snapshot engine
    tempo = (SIMS / "tempo_2v2_sim.py").read_text(encoding="utf-8")
    add("C31", "BlockWindow" not in tempo and "MitigationWindow" in tempo, "tempo_2v2_sim.py still reads MitigationWindow and has no BlockWindow")
    swap_body = tempo.split("def do_swap", 1)[1].split("def resolve_target", 1)[0]
    add("C32", "turnstart" not in swap_body and "ClearArmed" not in swap_body, "tempo do_swap keeps turnstart windows")

    # --- CSV rows ---
    moves = "Docs/Specs/Combat/Data/Moves.csv"
    expected_rows = {
        "C40": ("memory_leak", "ArmUtilityWindow:OwnerTurnStart:1:1:-2", "Target loses 2 AP next turn", "0", "Legendary"),
        "C41": ("packet_skim", "DrainAP:1", "Cut the target's AP by 1.", "1", "Common"),
        "C42": ("transaction_tax", "StealAP:1", "Steal 1 AP from the target.", "1", "Epic"),
        "C43": ("mev_extract", "MEVExtracted:2", "drain 1 AP per turn for 2 turns", "2", "Rare"),
        "C44": ("thread_lock", "ThreadLock:1", "cannot gain AP next turn", "2", "Legendary"),
        "C45": ("ember_wall", "WallHitRetaliation:1", "The attacker loses 1 AP.", "2", "Legendary"),
        "C46": ("cinder_burst", "DMG|MULTI", "2-4 hits", "3", "Common"),
        "C47": ("seam_cut", "1.25", "SpeedHigherThanOther", "2", "Rare"),
        "C48": ("hammer_sync", "80", "4", "4", "Legendary"),
        "C49": ("fee_collector", "the foe who acts first loses 1 AP", "Passive", "0", "Rare"),
    }
    for cid, (mid, a, b, ap, rarity) in expected_rows.items():
        try:
            line, row = csv_row(moves, mid)
            ok = a in row and b in row and f",{ap}," in row and rarity in row
            add(cid, ok, f"Moves.csv:{line} {mid} contains {a!r} ap {ap} {rarity}")
            if ok:
                tokens.append((cid, f"Moves.csv:{line}"))
        except Exception as exc:  # noqa: BLE001
            add(cid, False, str(exc))

    status_line, status_row = csv_row("Docs/Specs/Combat/Data/Statuses.csv", "MEVExtracted")
    add("C50", ",-1" in status_row and status_row.split(",")[4] == "2", f"Statuses.csv:{status_line} MEVExtracted duration 2, EoTAP -1")
    drained_line, drained_row = csv_row("Docs/Specs/Combat/Data/Statuses.csv", "Drained")
    add("C51", drained_row.split(",")[4] == "3" and drained_row.endswith("-1"), f"Statuses.csv:{drained_line} Drained lasts 3 and ticks -1")
    thread_line, thread_row = csv_row("Docs/Specs/Combat/Data/Statuses.csv", "ThreadLock")
    add("C52", thread_row.split(",")[4] == "1", f"Statuses.csv:{thread_line} ThreadLock duration 1")
    leaked_line, leaked_row = csv_row("Docs/Specs/Combat/Data/Statuses.csv", "Leaked")
    add("C53", "10% own MaxHP" in leaked_row, f"Statuses.csv:{leaked_line} Leaked is 10% max HP on action")

    pools = {r["SpeciesId"]: r for r in sim.read_csv(SNAP / "Docs/Specs/Combat/Data/SpeciesMovePools.csv")}
    kit = pools["kitsauto"]
    kit_blob = "|".join(kit.get(c) or "" for c in kit)
    add("C54", "packet_skim" in kit_blob and "thread_lock" not in kit_blob and "mev_extract" not in kit_blob and kit["FlagshipMove"] == "fox_feint", "Kitsauto learns Packet Skim, signature Fox Feint, and does not learn Thread Lock or MEV")
    key = pools["keyper"]
    add("C55", key["FlagshipMove"] == "memory_leak" and "memory_leak" in (key.get("LearnableMoves") or ""), "Keyper signature is Memory Leak")
    deg = pools["deghoul"]
    add("C56", deg["FlagshipMove"] == "transaction_tax" and "fee_collector" not in "|".join(deg.get(c) or "" for c in deg), "Deghoul signature is Transaction Tax; Fee Collector left the pool")
    cin = pools["cinislug"]
    add("C57", cin["FlagshipMove"] == "ember_wall" and "cinder_burst" in (cin.get("LearnableMoves") or "") and "baseline_shot" in (cin.get("LearnableMoves") or ""), "Cinislug signature Ember Wall, Cinder Burst and typeless baseline_shot in the pool")
    holders = {
        "thread_lock": [],
        "mev_extract": [],
        "fee_collector": [],
        "ap_drain": [],
        "ap_siphon": [],
    }
    for sid, row in pools.items():
        blob = "|".join(row.get(c) or "" for c in ("LearnableMoves", "ExclusiveMoves", "OffTypeMoves", "FlagshipMove"))
        for mid in holders:
            if mid in blob.split("|"):
                holders[mid].append(sid)
    add("C58", all(v == [] for v in holders.values()), f"Thread Lock, MEV, Fee Collector, AP Drain, and AP Siphon are in no pool {holders}")

    bands = sim.band_report()
    fails = [r for r in bands if not r["ok"]]
    add("C59", fails == [], f"wild learn-pool band failures: {len(fails)}")

    def top_unique(sid):
        ids = sim.pool_ids(sid)
        rarities = [sim.RARITY.index(sim.MOVES[m]["Rarity"]) for m in ids if m in sim.MOVES]
        top = max(rarities)
        return sum(1 for r in rarities if r == top) == 1

    add("C60", top_unique("validape") and top_unique("kitsauto") and top_unique("gilarime"), "starter signatures are the only move at the top rarity")

    gaps = {g["type"]: g for g in sim.cheap_options()}
    add("C61", gaps["Node"]["has_typed_chip"] is False and gaps["Burned"]["has_typed_chip"] is False, "Node and Burned have no typed damaging move at 2 AP or less in a real pool")
    add("C62", gaps["Bot"]["has_typed_chip"] and "blade_flurry" in gaps["Bot"]["typed_damage_at_2_or_less"], "Bot has Blade Flurry as a typed 2 AP chip")

    # species from assets via the sim loader
    want = {
        "validape": (156, 60),
        "kitsauto": (139, 82),
        "keyper": (102, 68),
        "deghoul": (106, 72),
        "kodiax": (124, 46),
        "crackett": (112, 92),
        "cinislug": (134, 28),
    }
    species_ok = all(sim.SPECIES[sid]["hp"] == hp and sim.SPECIES[sid]["spd"] == spd for sid, (hp, spd) in want.items())
    add("C63", species_ok, "species HP and speed match the assets used by the sim")

    chem = sim.chemistry_pairs([(11, 1), (13, 1), (11, 8)])
    add("C64", chem["Bot->Node"]["status"] == "Drained" and chem["Bot->Node"]["factor"] == 1.2, "Bot into Node is Drained at x1.2")
    add("C65", chem["Shard->Node"]["status"] == "Leaked" and chem["Shard->Node"]["factor"] == 1.2, "Shard into Node is Leaked at x1.2")
    add("C66", chem["Bot->Mined"]["status"] == "BacklashType" and chem["Bot->Mined"]["factor"] == 1.2, "Bot into Mined is BacklashType at x1.2")

    # --- calendars ---
    cals = sim.build_calendars()

    def series(name):
        return [row["action_ap"] for row in cals[name]["action_series"]]

    checks = {
        "C70": (series("leak_pass") == [2, 2, 2, 2, 2, 2, 2, 2] and cals["leak_pass"]["hammer_turn"] is None, "leak pass stays at 2, finisher never legal"),
        "C71": (series("leak_spend2") == [2, 0, 0, 0, 0, 0, 0, 0], "one 2 AP spend under leak then stays at 0"),
        "C72": (series("tax_pass") == [1, 2, 3, 4, 5, 6, 7, 8] and cals["tax_pass"]["hammer_turn"] == 4, "tax alone, hammer on turn 4"),
        "C73": (series("skim_pass") == [1, 2, 3, 4, 5, 6, 7, 8] and cals["skim_pass"]["hammer_turn"] == 4, "skim alone, hammer on turn 4"),
        "C74": (series("tax_skim_pass") == [0, 0, 0, 0, 0, 0, 0, 0], "tax plus skim, action AP stays 0"),
        "C75": (series("tax_skim_victim_faster") == [2, 2, 2, 2, 2, 2, 2, 2], "a faster victim stays at 2 under tax plus skim"),
        "C76": (series("bank8_leak_hammer")[0] == 10 and cals["bank8_leak_hammer"]["hammer_turn"] == 1, "a bank of 8 hammers on turn 1 into a leak"),
        "C77": (series("bank4_leak_pass")[0] == 6 and cals["bank4_leak_pass"]["hammer_turn"] == 1, "a bank of 4 before the first window hammers on turn 1"),
        "C78": (series("leak_plus_drained_pass") == [2, 1, 0, 0, 0, 0, 0, 0], "one Drained plus maintained leak zeros a passer"),
        "C79": (series("leak_cd_pass")[2] >= 4 and cals["leak_cd_pass"]["hammer_turn"] == 3, "leak recharge lets a passer hammer on turn 3"),
        "C80": (cals["leak_cd_spend2"]["hammer_turn"] is None and series("leak_cd_spend2") == [2, 0, 2, 0, 2, 0, 2, 0], "spending the open turn keeps the finisher locked"),
        "C81": (series("cap2_tax_skim") == [0, 0, 0, 0, 0, 0, 0, 0], "a cap of 2 still zeros tax plus skim"),
        "C82": (series("cap2_leak_skim") == [1, 1, 1, 1, 1, 1, 1, 1] and cals["cap2_leak_skim"]["hammer_turn"] is None, "a cap of 2 leaves leak plus skim at 1"),
        "C83": (cals["fuel_breaks_leak"]["hammer_turn"] == 2, "Fuel Surge breaks the leak on turn 2"),
        "C84": (cals["reserve_vs_leak"]["hammer_turn"] == 5, "AP Reserve breaks the leak on turn 5"),
        "C85": (all(max(row["active0"].values()) == 2 for row in cals["swap_releak"]["action_series"]), "after a re-leak swap the active bar stays at 2"),
        "C86": (cals["thread_once"]["hammer_turn"] == 3, "one Thread Lock, hammer on turn 3"),
        "C87": (cals["mev_once"]["hammer_turn"] == 3, "one MEV, hammer on turn 3"),
        "C88": (cals["deghoul_bank_if_tax_steals"] == [2, 4, 6, 8, 10, 10] and cals["deghoul_bank_if_tax_drains"] == [1, 2, 3, 4, 5, 6], "steal banks Deghoul; a drain does not"),
        "C89": (cals["victim_if_tax_steals"] == cals["victim_if_tax_drains"], "the victim calendar is the same if Tax steals or drains"),
    }
    for cid, (ok, note) in checks.items():
        add(cid, ok, note)

    # --- damage quotes ---
    seam = sim.expected_damage("crackett", "seam_cut", "validape")
    seam_min = sim.expected_damage("crackett", "seam_cut", "validape", roll=sim.DATA["roll_min"])
    no_speed = sim.expected_damage("crackett", "seam_cut", "validape", speed_bonus=False)
    no_chem = sim.expected_damage("crackett", "seam_cut", "validape", chem=1.0)
    plain = sim.expected_damage("crackett", "seam_cut", "validape", chem=1.0, speed_bonus=False)
    seam_deg = sim.expected_damage("crackett", "seam_cut", "deghoul")
    flurry_k = sim.expected_damage("kitsauto", "blade_flurry", "kodiax")
    flurry_v = sim.expected_damage("kitsauto", "blade_flurry", "validape")
    deep = sim.expected_damage("kodiax", "deep_dig", "kitsauto")
    hammer_k = sim.expected_damage("validape", "hammer_sync", "kitsauto")
    hammer_d_min = sim.expected_damage("validape", "hammer_sync", "deghoul", roll=sim.DATA["roll_min"])
    add("C90", seam["total"] == 92 and seam["total"] * 2 > 156 and seam_min["total"] == 85 and seam_min["total"] * 2 > 156, f"Seam into Validape {seam['total']} / min {seam_min['total']}")
    add("C91", no_speed["total"] == 74 and no_chem["total"] == 77 and plain["total"] == 61 and plain["total"] * 2 < 156, f"Seam riders off: no speed {no_speed['total']}, no chem {no_chem['total']}, plain {plain['total']}")
    add("C92", seam_deg["total"] == 128 and seam_deg["total"] >= 106, f"Seam into Deghoul {seam_deg['total']} one-shots 106 HP")
    add("C93", flurry_k["total"] == 60 and flurry_k["total"] * 2 < 124 <= flurry_k["total"] * 3, f"Flurry into Kodiax {flurry_k['total']}")
    add("C94", flurry_v["total"] == 54 and deep["total"] == 69 and hammer_k["total"] == 77 and hammer_k["total"] < 139, f"flurry Validape {flurry_v['total']}, Deep Dig Kitsauto {deep['total']}, hammer Kitsauto {hammer_k['total']}")
    add("C95", hammer_d_min["total"] == 106, f"Hammer into Deghoul minimum roll {hammer_d_min['total']}")

    # --- deterministic traces ---
    specs = sim.battle_specs()

    def trace(name):
        return sim.trace_battle(lambda rng, name=name: sim.build_battle(specs[name], rng, {"deterministic": True}), seed=7)

    seam_tr = trace("commit_seam_vs_hammer")
    add("C100", seam_tr["hammer_turn"] is None and seam_tr["winner"] == 1 and seam_tr["end_turn"] == 2 and any(ev.get("dmg") == 92 for ev in seam_tr["trace"]) and any(ev.get("ev") == "leaked_tax" and ev.get("dmg") == 15 for ev in seam_tr["trace"]), "Seam trace: two hits, Leaked tax 15, hammer never, match ends turn 2")
    dig_tr = trace("commit_flurry_vs_deepdig")
    add("C101", dig_tr["hammer_turn"] == 2 and dig_tr["winner"] == 1 and any(ev.get("move") == "deep_dig" and ev.get("dmg") == 69 for ev in dig_tr["trace"]), "Deep Dig trace: heavy pays on turn 2 for 69 and still loses")
    lock_tr = trace("lock_leak_commit_pass")
    add("C102", lock_tr["hammer_turn"] is None and lock_tr["winner"] == 1 and lock_tr["end_turn"] == 3, "commit-pass leak plus flurry: hammer never, match ends turn 3")
    bench_tr = trace("lock_leak_flurry_with_bench")
    add("C103", any(ev.get("ev") == "swap" and ev.get("inn") == "crackett" for ev in bench_tr["trace"]) and any(ev.get("ev") == "cast" and ev.get("move") == "memory_leak" and ev.get("tgt") == "crackett" for ev in bench_tr["trace"]) and any(ev.get("ev") == "pass" and ev.get("who") == "crackett" and ev.get("ap") == 0 for ev in bench_tr["trace"]), "swap re-targets the leak onto Crackett, who then acts at 0")
    inherit_tr = trace("lock_commit_bench_inherit")
    add("C104", any(ev.get("ev") == "autoswap" and ev.get("ap") == 0 for ev in inherit_tr["trace"]) and any(ev.get("move") == "seam_cut" and ev.get("who") == "crackett" and ev.get("ap") == 0 for ev in inherit_tr["trace"]), "KO inherit 0 still allows one Seam, paid down to 0")
    floor_tr = trace("lock_commit_bench_floor2")
    add("C105", any(ev.get("ev") == "autoswap" and ev.get("ap") == 2 for ev in floor_tr["trace"]) and any(ev.get("move") == "seam_cut" and ev.get("who") == "crackett" and ev.get("ap") == 2 for ev in floor_tr["trace"]), "KO floor 2 leaves 2 AP after Seam, so the action opened at 4")
    bank_tr = trace("bank8_endgame")
    add("C106", bank_tr["hammer_turn"] == 1 and bank_tr["winner"] == 1 and bank_tr["end_turn"] == 3, "bank of 8 hammers on turn 1 and the match still ends on turn 3")
    ember_tr = trace("ember_and_leak")
    add("C107", ember_tr["hammer_turn"] is None and ember_tr["winner"] == 1 and any(ev.get("why") == "ember" and ev.get("n") == 0 for ev in ember_tr["trace"]), "Ember drain removes 0 after the snap is paid; the hammer never fires")

    # --- 1200-trial races, same seeds as the sim ---
    def race(name):
        return sim.run_trials(lambda rng, name=name: sim.build_battle(specs[name], rng), 1200, seed=1000 + sum(ord(ch) for ch in name) * 17)

    race_expect = {
        "C110": ("lock_leak_commit_pass", dict(team1_win_pct=100.0, heavy_land_pct=0.0, median_end=3)),
        "C111": ("commit_seam_vs_hammer", dict(team1_win_pct=99.4, heavy_land_pct=9.7, median_end=2)),
        "C112": ("commit_flurry_vs_deepdig", dict(team1_win_pct=100.0, heavy_land_pct=70.2, median_end=3)),
        "C113": ("lock_commit_bench_inherit", dict(team0_win_pct=0.2, team1_win_pct=99.8, heavy_land_pct=0.0)),
        "C114": ("lock_commit_bench_floor2", dict(team0_win_pct=32.2, team1_win_pct=67.8)),
        "C115": ("pressure_tax_skim_2v2", dict(team0_win_pct=100.0, heavy_land_pct=0.0, median_end=3)),
        "C116": ("bank8_endgame", dict(team1_win_pct=98.5, heavy_land_pct=100.0, median_heavy_turn=1, median_end=3)),
        "C117": ("ember_and_leak", dict(team1_win_pct=100.0, heavy_land_pct=0.0, median_end=10)),
    }
    for cid, (name, expect) in race_expect.items():
        got = race(name)
        ok = all(got[key] == value for key, value in expect.items())
        detail = " ".join(f"{key}={got[key]}" for key in expect)
        add(cid, ok, f"{name} {detail}")

    # --- AI numbers from the same formulas the sim prints ---
    key_u = sim.utility_scores("keyper", sim.pool_ids("keyper"))
    kit_u = sim.utility_scores("kitsauto", sim.pool_ids("kitsauto"))
    key_a = sim.aggressive_scores("keyper", sim.pool_ids("keyper"))
    add("C120", key_u[0]["move"] == "cipher_shot" and key_u[0]["score"] == 44.6 and next(r["score"] for r in key_u if r["move"] == "memory_leak") == 28.0, "utility prefers Cipher Shot 44.6 over Memory Leak 28")
    add("C121", kit_u[0]["move"] == "debuff_script" and kit_u[0]["score"] == 49.0 and next(r["score"] for r in kit_u if r["move"] == "packet_skim") == 29.0, "utility prefers Debuff Script 49 over Packet Skim 29")
    add("C122", next(r["score"] for r in key_a if r["move"] == "memory_leak") == 0.1, "aggressive scores Memory Leak at 0.1")
    proposed = { (r["move"], r["target_ap"]): r["proposed"] for r in [
        sim.proposed_denial_score("memory_leak", 2),
        sim.proposed_denial_score("memory_leak", 4),
        sim.proposed_denial_score("packet_skim", 4),
        sim.proposed_denial_score("packet_skim", 0),
    ] }
    add("C123", proposed[("memory_leak", 2)] == 104 and proposed[("memory_leak", 4)] == 28 and proposed[("packet_skim", 4)] == 87 and proposed[("packet_skim", 0)] == 29, f"proposed scores {proposed}")

    # doc tags
    import re
    used = set(re.findall(r"\[(C\d+)\]", doc))
    known = {cid for cid, _, _ in results}
    missing_in_doc = sorted(known - used, key=lambda x: int(x[1:]))
    unknown_in_doc = sorted(used - known - {"C130", "C131"}, key=lambda x: int(x[1:]))
    add("C130", not missing_in_doc and not unknown_in_doc, f"doc tags missing {missing_in_doc} unknown {unknown_in_doc}")
    bad_tokens = [f"{cid} {token}" for cid, token in tokens if token not in doc]
    add("C131", not bad_tokens, f"doc missing file:line cites {bad_tokens[:8]}")

    passed = sum(1 for _, ok, _ in results if ok)
    failed = sum(1 for _, ok, _ in results if not ok)
    print(f"TALLY PASS {passed} FAIL {failed}")
    if f"PASS {passed} / FAIL {failed}" not in doc:
        print("FAIL C132  doc tally line does not match this run")
        return 1
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
