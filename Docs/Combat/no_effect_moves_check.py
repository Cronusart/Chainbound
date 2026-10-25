"""Check script for COMBAT_ISSUES_MASTER.md section
"Moves with no working effect: implement or rewrite (2026-10-22)".

Read-only. Run from anywhere:  python Docs/Combat/no_effect_moves_check.py
Asserts the section's claims against Moves.csv, SpeciesMovePools.csv,
Statuses.csv, species assets, and Runtime code literals.
"""
import csv
import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "Docs", "Specs", "Combat", "Data")
RUNTIME = os.path.join(ROOT, "Assets", "Scripts", "Combat", "Runtime")
SPECIES_DIR = os.path.join(ROOT, "Assets", "Data", "Combat", "ChainlingSpecies")
MASTER = os.path.join(ROOT, "Docs", "Combat", "COMBAT_ISSUES_MASTER.md")
RARITY = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]

results = []


def check(cid, cond, detail=""):
    results.append((cid, bool(cond), detail))


def read_csv(name):
    with open(os.path.join(DATA, name), encoding="utf-8-sig", newline="") as f:
        lines = [l for l in f if not l.startswith("#")]
    return list(csv.DictReader(lines))


moves = {r["MoveId"]: r for r in read_csv("Moves.csv")}
pools = read_csv("SpeciesMovePools.csv")
statuses = {r["StatusId"]: r for r in read_csv("Statuses.csv")}


def split(v):
    return [x for x in (v or "").split("|") if x]


learn = {}   # move -> list of "species:column"
for p in pools:
    for col in ("LearnableMoves", "ExclusiveMoves", "OffTypeMoves"):
        for m in split(p[col]):
            learn.setdefault(m, []).append(p["SpeciesId"] + ":" + col)


def payload_blank(r):
    return not (r["StatusEffects"].strip() or r["StageChanges"].strip() or r["UtilityEffects"].strip()
                or r["HealPercentMaxHP"].strip() not in ("", "0"))


# Runtime literals
code = {}
for path in glob.glob(os.path.join(RUNTIME, "**", "*.cs"), recursive=True):
    with open(path, encoding="utf-8", errors="replace") as f:
        code[path] = f.read()
all_code = "\n".join(code.values())


def has_literal(move_id):
    return ('"%s"' % move_id) in all_code


# Species assets
species = {}
for path in glob.glob(os.path.join(SPECIES_DIR, "**", "*.asset"), recursive=True):
    with open(path, encoding="utf-8", errors="replace") as f:
        t = f.read()
    m_id = re.search(r"^  speciesId: (\S+)", t, re.M)
    m_pt = re.search(r"^  primaryType: (\d+)", t, re.M)
    if not (m_id and m_pt):
        continue
    m_r = re.search(r"^  rarity: (\d+)", t, re.M)
    m_s = re.search(r"^  isStarter: (\d+)", t, re.M)
    species[m_id.group(1)] = {
        "type": int(m_pt.group(1)),
        "rarity": int(m_r.group(1)) if m_r else None,
        "starter": m_s is not None and m_s.group(1) == "1",
    }

# ---------- 1. In-scope move facts ----------
facts = {
    # id: (Rarity, BaseType, Category, APCost, Power, RechargeTurns)
    "all_in": ("Epic", "Meme", "DamagingMelee", "4", "98", "2"),
    "script_loop": ("Common", "Bot", "StatusUtility", "1", "0", "0"),
    "viral_surge": ("Mythic", "Meme", "StatusUtility", "1", "0", "0"),
    "wildcard_swap": ("Common", "Meme", "StatusUtility", "2", "0", "0"),
    "extraction_hit": ("Rare", "Mined", "Passive", "0", "0", "0"),
    "deflation_mark": ("Rare", "Burned", "StatusUtility", "1", "0", "0"),
    "recoil_strike": ("Uncommon", "Forked", "DamagingMelee", "4", "72", "0"),
}
for mid, (rar, typ, cat, ap, pw, rc) in facts.items():
    r = moves.get(mid)
    check("F-%s-exists" % mid, r is not None)
    if r is None:
        continue
    got = (r["Rarity"], r["BaseType"], r["Category"], r["APCost"], r["Power"], r["RechargeTurns"])
    check("F-%s-stats" % mid, got == (rar, typ, cat, ap, pw, rc), "got %s" % (got,))

a = moves["all_in"]
check("F-all_in-tags", "SpendRemainingAp" in split(a["Tags"]), a["Tags"])
check("F-all_in-no-utility-yet", a["UtilityEffects"] == "" and a["ResolveUtilityTiming"] == "", "decided change not yet applied")
check("F-all_in-text", a["Description"].startswith("Spends all your AP (min 4)"))
for mid in ("script_loop", "viral_surge", "wildcard_swap", "extraction_hit"):
    check("F-%s-blank" % mid, payload_blank(moves[mid]))
    check("F-%s-no-literal" % mid, not has_literal(mid))
d = moves["deflation_mark"]
check("F-deflation-payload", d["StatusEffects"].startswith("BurnedTokens:2") and d["StageChanges"] == "MeleeAtk:-1:1.0:false", d["StatusEffects"] + " / " + d["StageChanges"])
bt = statuses.get("BurnedTokens", {})
check("F-burnedtokens-no-body", bt.get("StageEffects") == "" and bt.get("EoTDamagePercent") == "0" and bt.get("EoTAPChange") == "0"
      and bt.get("IsDebuff") == "false" and bt.get("IsBuff") == "false", str(bt))
check("F-burnedtokens-no-code", "StatusId.BurnedTokens" not in all_code)
h = moves["recoil_strike"]
check("F-hardfork-name", h["DisplayName"] == "Hard Fork")
check("F-hardfork-cleanse", h["UtilityEffects"] == "Cleanse:1:100%:Self:hard_fork" and h["ResolveUtilityTiming"] == "AfterMoveComplete")

# ---------- 2. Sweep ----------
sweep = sorted(mid for mid, r in moves.items()
               if not r["Category"].startswith("Damaging") and payload_blank(r) and not has_literal(mid))
check("S-sweep-set", sweep == sorted(["script_loop", "viral_surge", "wildcard_swap", "extraction_hit", "status_resistance"]), str(sweep))
check("S-status_resistance-retired", moves["status_resistance"]["Description"].startswith("Retired"))
for mid in ("quick_lunge", "chain_fork", "shard_scatter", "hype_train", "combustive_shift"):
    check("S-plainhit-%s" % mid, payload_blank(moves[mid]) and moves[mid]["Category"].startswith("Damaging"))
check("S-quick_lunge-prior", "PRIOR" in split(moves["quick_lunge"]["Tags"]))
check("S-trig-no-trigger", all("TRIG" in split(moves[m]["Tags"]) and payload_blank(moves[m]) and not moves[m]["ConditionalDamageMultiplier"]
                              for m in ("hype_train", "ore_strike")))

# ---------- 3. Pool membership ----------
for mid in ("all_in", "script_loop", "viral_surge", "wildcard_swap", "extraction_hit", "deflation_mark"):
    check("P-%s-unpooled" % mid, mid not in learn, str(learn.get(mid)))
check("P-hardfork-balloom-offtype-only", learn.get("recoil_strike") == ["balloom:OffTypeMoves"], str(learn.get("recoil_strike")))
meme_moves = [m for m, r in moves.items() if r["BaseType"] == "Meme"]
check("P-meme-none-pooled", not any(m in learn for m in meme_moves), str([m for m in meme_moves if m in learn]))
check("P-meme-count", len(meme_moves) == 10, str(len(meme_moves)))
meme_species = sorted(s for s, v in species.items() if v["type"] == 14)
check("P-meme-only-ghost", meme_species == ["ghost"], str(meme_species))
check("P-ghost-no-pool-row", all(p["SpeciesId"] != "ghost" for p in pools))
check("P-bot-only-kitsauto", sorted(s for s, v in species.items() if v["type"] == 11) == ["kitsauto"])
check("P-burned-only-cinislug", sorted(s for s, v in species.items() if v["type"] == 5) == ["cinislug"])
check("P-mined-bodies", sorted(s for s, v in species.items() if v["type"] == 8) == ["kodiax", "minefolk"])
check("P-kitsauto-starter", species["kitsauto"]["starter"])
check("P-tiers", (species["kodiax"]["rarity"], species["minefolk"]["rarity"], species["cinislug"]["rarity"]) == (1, 2, 0))
surging_producers = [m for m, r in moves.items() if "Surging" in r["StatusEffects"]]
check("P-surging-only-forked", surging_producers == ["risk_spike"], str(surging_producers))

# ---------- 4. Learn-pool rule (plan learn_pool_rarity_6d7ff36f) ----------
def rank(mid):
    return RARITY.index(moves[mid]["Rarity"])


def pool_of(p):
    return split(p["LearnableMoves"]) + split(p["ExclusiveMoves"])


def wild_ok(tier, sig, others):
    if len(others) != 4 or any(rank(o) >= rank(sig) for o in others):
        return False
    low = [o for o in others if rank(o) <= tier]
    return len(low) >= 3 and all(rank(o) <= tier + 1 for o in others)


rule_fail = []
for p in pools:
    sid = p["SpeciesId"]
    if sid in ("ghost", "cofi_lootwak") or sid not in species:
        continue
    pm, sig = pool_of(p), p["FlagshipMove"]
    others = [m for m in pm if m != sig]
    if species[sid]["starter"]:
        ok = all(rank(o) < rank(sig) for o in others)
    else:
        ok = wild_ok(species[sid]["rarity"], sig, others)
    if not ok:
        rule_fail.append(sid)
check("L-current-pools-legal", rule_fail == [], str(rule_fail))
kod = next(p for p in pools if p["SpeciesId"] == "kodiax")
check("L-kodiax-sig-rare", kod["FlagshipMove"] == "ore_strike" and moves["ore_strike"]["Rarity"] == "Rare")
check("L-kodiax-cannot-take-extraction", not wild_ok(1, "ore_strike", [m for m in pool_of(kod) if m != "ore_strike"][:3] + ["extraction_hit"]))
mf = next(p for p in pools if p["SpeciesId"] == "minefolk")
check("L-minefolk-sig-legendary", mf["FlagshipMove"] == "deep_reap" and moves["deep_reap"]["Rarity"] == "Legendary")
check("L-minefolk-could-take-extraction", wild_ok(2, "deep_reap", [m for m in pool_of(mf) if m not in ("deep_reap", "refined_shot")] + ["extraction_hit"]))
cs = next(p for p in pools if p["SpeciesId"] == "cinislug")
check("L-cinislug-cannot-take-deflation", not wild_ok(0, "ember_wall", [m for m in pool_of(cs) if m not in ("ember_wall", "hype_burst")] + ["deflation_mark"]))
kit = next(p for p in pools if p["SpeciesId"] == "kitsauto")
check("L-kitsauto-top-fox-feint", kit["FlagshipMove"] == "fox_feint" and moves["fox_feint"]["Rarity"] == "Epic" and len(pool_of(kit)) == 5)

# ---------- 5. Rarity comparisons cited ----------
def row(mid):
    r = moves[mid]
    return (r["Rarity"], r["APCost"], r["Power"])


check("R-hammer_sync", row("hammer_sync") == ("Legendary", "4", "80") and moves["hammer_sync"]["ConditionalDamageMaxMultiplier"] == "1.2")
check("R-extend_protocol", row("extend_protocol")[:2] == ("Epic", "1") and moves["extend_protocol"]["RechargeTurns"] == "2"
      and moves["extend_protocol"]["UtilityEffects"].startswith("ExtendBuff"))
check("R-ap_boost", row("ap_boost")[:2] == ("Epic", "0") and moves["ap_boost"]["UtilityEffects"].startswith("GainAP:2"))
check("R-double_or_nothing", moves["double_or_nothing"]["Rarity"] == "Mythic" and moves["double_or_nothing"]["Category"] == "Passive")
check("R-lucky_scatter", moves["lucky_scatter"]["UtilityEffects"].startswith("RandomStageChange:1:1:25%:Self"))
check("R-risk_spike", moves["risk_spike"]["Rarity"] == "Mythic" and "Surging" in moves["risk_spike"]["StatusEffects"])
check("R-pivot_line", moves["pivot_line"]["Rarity"] == "Epic")
check("R-debuff_script", row("debuff_script")[:2] == ("Common", "1"))
check("R-deep_dig", row("deep_dig") == ("Uncommon", "4", "72") and payload_blank(moves["deep_dig"]))
check("R-gas_war", row("volatile_shot") == ("Uncommon", "4", "72") and moves["volatile_shot"]["UtilityEffects"].startswith("NextMoveAPSurcharge:1"))
check("R-crystal_extraction", moves["crystal_extraction"]["UtilityEffects"].startswith("HealSelfPerTargetDebuff:5"))
check("R-deep_reap", "LifestealPercent:20%" in moves["deep_reap"]["UtilityEffects"])
check("R-pickaxe", row("pickaxe_flurry") == ("Common", "3", "14") and "ChangeStage:MeleeAtk:1:100%:Self:2" in moves["pickaxe_flurry"]["UtilityEffects"])
zero_ap = sorted(m for m, r in moves.items() if r["APCost"] == "0" and r["Category"] != "Passive")
check("R-zero-ap-moves", zero_ap == sorted(["ap_boost", "fuel_surge", "crystal_focus", "ap_reserve"]), str(zero_ap))
mined_passives = [m for m, r in moves.items() if r["BaseType"] == "Mined" and r["Category"] == "Passive"]
check("R-mined-only-passive", mined_passives == ["extraction_hit"], str(mined_passives))

# ---------- 6. Engine facts cited ----------
uee = code.get(os.path.join(RUNTIME, "Engine", "UtilityEffectExecutor.cs"), "")
m = re.search(r"private void DrainApEffect\((.*?)\n        private ", uee, re.S)
check("E-drain-no-allallies-branch", m is not None and "AllEnemies" in m.group(1) and "AllAllies" not in m.group(1))
check("E-targeting-has-allallies", re.search(r"enum MoveTargeting\s*\{[^}]*AllAllies", all_code) is not None)
check("E-resolve-before-first-hit", "MoveUtilityExecutionTiming.ResolveBeforeFirstHit" in all_code)
check("E-extend-buff-exists", "public bool TryExtendShortestBuff" in all_code and "TryExtendShortestDebuff" not in all_code)
check("E-no-extenddebuff-opcode", re.search(r"enum UtilityEffectType\s*\{[^}]*ExtendDebuff", all_code) is None)
check("E-kindling-pattern", "private void TryApplyKindling" in all_code)
cfg = open(os.path.join(ROOT, "Assets", "Data", "Combat", "Config", "CombatResolverConfig_Default.asset"), encoding="utf-8").read()
check("E-apcap-10", re.search(r"apCap: 10\b", cfg) is not None)
check("E-apPerTurn-2", re.search(r"apPerTurn: 2\b", cfg) is not None)
imp = open(os.path.join(ROOT, "Assets", "Scripts", "Combat", "Editor", "Importers", "Combat2DataImporter.cs"), encoding="utf-8", errors="replace").read()
check("E-importer-drain-target", re.search(r"case UtilityEffectType\.DrainAP:.*?ParseEnum\(p3, MoveTargeting\.SingleEnemy\)", imp, re.S) is not None)

# ---------- 7. Stale-table values ----------
stale = {
    "ap_drain": lambda r: r["Rarity"] == "Uncommon",
    "network_sync": lambda r: r["Rarity"] == "Epic" and r["BaseType"] == "Node",
    "wildcard_swap": lambda r: r["APCost"] == "2",
    "jackpot_shot": lambda r: (r["Rarity"], r["Power"]) == ("Mythic", "58"),
    "lucky_scatter": lambda r: (r["Rarity"], r["Power"], r["APCost"]) == ("Common", "17", "3"),
    "shard_scatter": lambda r: (r["Rarity"], r["Power"], r["APCost"]) == ("Legendary", "22", "4"),
    "ap_siphon": lambda r: (r["Rarity"], r["Power"]) == ("Rare", "54") and r["UtilityEffects"].startswith("DrainAP:1:50%"),
    "momentum_drill": lambda r: (r["Rarity"], r["Power"], r["ConditionalDamageMultiplier"]) == ("Epic", "54", "1.25"),
    "interest_strike": lambda r: r["Rarity"] == "Epic" and r["UtilityEffects"].startswith("GainAP:1") and r["ResolveConditions"] != "",
    "rallying_slam": lambda r: (r["Rarity"], r["Power"]) == ("Epic", "52") and r["UtilityEffects"].startswith("GainAP:1"),
    "glitch_feedback": lambda r: "Cleanse:1" in r["UtilityEffects"],
    "chain_seal": lambda r: "ChangeStage:Evasion" in r["UtilityEffects"],
    "key_rotation": lambda r: "ChangeStage:Evasion" in r["UtilityEffects"],
    "zip_step": lambda r: "ChangeStage:Evasion" in r["UtilityEffects"],
    "accuracy_focus": lambda r: "ChangeStage:Accuracy" in r["UtilityEffects"],
    "evasion_maneuver": lambda r: "ChangeStage:Evasion" in r["UtilityEffects"],
    "accuracy_disrupt": lambda r: "ChangeStage:Accuracy" in r["UtilityEffects"],
    "evasion_pin": lambda r: "ChangeStage:Evasion" in r["UtilityEffects"],
    "optimize_aim": lambda r: "ChangeStage:Accuracy" in r["UtilityEffects"],
    "blockade": lambda r: r["Rarity"] == "Uncommon" and r["UtilityEffects"].startswith("BlockWindow:60%:Melee") and "PRIOR" in split(r["Tags"]),
    "ice_wall": lambda r: r["APCost"] == "2" and r["UtilityEffects"].startswith("BlockWindow:40%:Melee"),
    "bulwark_blow": lambda r: (r["Rarity"], r["Power"], r["APCost"]) == ("Epic", "50", "3"),
    "guard_posture": lambda r: "ARMOR" in split(r["Tags"]) and "armor_guard_posture" in r["UtilityEffects"],
}
for mid, fn in stale.items():
    check("T-%s" % mid, fn(moves[mid]))
epic_dmg = [m for m, r in moves.items() if r["Rarity"] == "Epic" and r["Category"].startswith("Damaging")]
check("T-nine-epic-damaging", len(epic_dmg) == 9, str(len(epic_dmg)))
block_family = ["ash_guard", "barrier_wall", "blockade", "compound_guard", "ember_wall", "ice_wall", "lattice_guard", "vapor_barrier"]
check("T-block-family", all(moves[m]["UtilityEffects"].startswith("BlockWindow:") and moves[m]["RechargeTurns"] == "2"
                             and "PRIOR" in split(moves[m]["Tags"]) for m in block_family))
check("T-no-unlimited-budget", not any(re.search(r"MitigationWindow:[^|]*:-1:", r["UtilityEffects"]) for r in moves.values()))
names = {m: moves[m]["DisplayName"] for m in ("ash_guard", "compound_guard", "lattice_guard", "barrier_wall")}
check("T-renames", names == {"ash_guard": "Ash Barrier", "compound_guard": "Compound Barrier",
                              "lattice_guard": "Lattice Barrier", "barrier_wall": "Buffer Barrier"}, str(names))

# ---------- 8. Master doc ----------
with open(MASTER, encoding="utf-8") as f:
    mlines = f.read().split("\n")
mtext = "\n".join(mlines)
check("D-section-present", "## Moves with no working effect: implement or rewrite (2026-10-22)" in mtext)
check("D-pointer-near-top", any("#moves-with-no-working-effect-implement-or-rewrite-2026-10-22" in l for l in mlines[:10]))
check("D-stale-L25", "hitsRemaining == -1" in mlines[24])
check("D-stale-L272", mlines[271].startswith("| E-17 |"))
check("D-stale-L96", mlines[95].startswith("| E-16 |"))
check("D-stale-L168", mlines[167].startswith("| MV-blockade |"))
check("D-stale-L261", mlines[260].startswith("| BAL-EPIC |"))

passed = sum(1 for _, ok, _ in results if ok)
failed = [(c, d) for c, ok, d in results if not ok]
for c, d in failed:
    print("FAIL %s %s" % (c, d))
print("%d PASS / %d FAIL" % (passed, len(failed)))
sys.exit(1 if failed else 0)
