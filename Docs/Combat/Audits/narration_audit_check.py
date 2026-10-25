#!/usr/bin/env python3
"""Verify the factual claims in the 2026-10-21 modifier-narration second pass.

Run: python3 /workspace/narration/narration_audit_check.py
Every check prints PASS/FAIL; exit code is 0 only when all pass.
"""
import os
import re
import sys
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
SNAP = os.path.join(ROOT, "snap")
ZIP = os.path.join(ROOT, "cb_narr_snapshot.zip")
DOC_REL = "Docs/Combat/Audits/trigger_conditional_modifier_audit.md"
DOC = os.path.join(SNAP, DOC_REL)
SECTION_HEADER = "## Second pass (2026-10-21): Modifier narration"

results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok), detail))


def read(rel):
    with open(os.path.join(SNAP, rel), "r", encoding="utf-8", errors="replace") as f:
        return f.read()


def lines(rel):
    return read(rel).splitlines()


COMP = "Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs"
PROF = "Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs"
ASSET = "Assets/Data/Combat/Narration/CombatNarration_Default.asset"
CATALOG = "Assets/Data/Combat/Modifiers/Catalog/ModifierCatalog.asset"
RES = "Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs"
PAY = "Assets/Scripts/Combat/Runtime/Data/ModifierOutcomePayload.cs"
PACE = "Assets/Scripts/Combat/Runtime/Presentation/Combat2BattlefieldPresenter.Pacing.cs"
PACEPROF = "Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatBattlePacingProfileSO.cs"
APS = "Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs"
UTIL = "Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs"
SWAP = "Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs"
CTRL = "Assets/Scripts/Combat/Runtime/Presentation/CombatBattleController.cs"
NARR = "Assets/Scripts/Combat/Runtime/Presentation/Combat2BattlefieldPresenter.Narration.cs"
ENUMS = "Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs"
EVTYPE = "Assets/Scripts/Combat/Runtime/Data/CombatEventType.cs"
DEFSO = "Assets/Scripts/Combat/Runtime/ScriptableObjects/ModifierDefinitionSO.cs"
COORD = "Assets/Scripts/Combat/Runtime/UI/UGUI/CombatSelectionCoordinatorUGUI.cs"
LOG = "Assets/Scripts/Combat/Runtime/Engine/CombatEventLog.cs"
DMG = "Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs"
WALLSTATE = "Assets/Scripts/Combat/Runtime/Data/CombatWallStateUtility.cs"
WALLLABEL = "Assets/Scripts/Combat/Runtime/Presentation/CombatWallPopupLabelUtility.cs"
DIALOG = "Assets/Scripts/Combat/Runtime/Presentation/Narration/DialogueBoxView.cs"
INTRO = "Assets/Scripts/Combat/Runtime/Presentation/Narration/BattleIntroCopySpanApplier.cs"
T_COMP = "Assets/Scripts/Combat/Tests/CombatNarrationComposerTests.cs"
T_POP = "Assets/Scripts/Combat/Tests/CombatModifierResolverAndPopupTests.cs"
T_TRIG = "Assets/Scripts/Combat/Tests/TriggerConditionalModifierAuditTests.cs"
T_ROUND = "Assets/Scripts/Combat/Tests/CombatDecisionRoundTests.cs"
NDOC = "Docs/Combat/Combat_Narration_And_Battle_Report.md"

# Every path:line cited in the new section must be listed here with text the line must contain.
EXPECT = {
    f"{COMP}:70": "delivery == CombatNarrationDelivery.Silent || delivery == CombatNarrationDelivery.PopupOrHudOnly",
    f"{COMP}:101": "else if (!string.IsNullOrWhiteSpace(evt.message))",
    f"{COMP}:180": "case CombatEventType.ModifierTriggered:",
    f"{COMP}:185": "A combat modifier changed the outcome.",
    f"{COMP}:195": "case CombatEventType.BuffBlocked:",
    f"{COMP}:201": "case CombatEventType.APStolen:",
    f"{COMP}:202": "{target} lost {amount} AP.",
    f"{COMP}:237": "Wall lines first",
    f"{COMP}:244": "healTargetOrder.Count",
    f"{COMP}:261": "plan.consequencePage = NewPage",
    f"{COMP}:389": "Critical hit! ",
    f"{COMP}:412": "WallInterceptMode.Bypass",
    f"{COMP}:505": "faded.",
    f"{COMP}:522": "return TitleCaseWords",
    f"{COMP}:723": "TrySetupFeedSentence(CombatEvent",
    f"{COMP}:750": "!seen.Add(sentence)",
    f"{COMP}:776": "AppendTranscript(CombatNarrationActionPlan plan",
    f"{PROF}:63": "modifierTemplate = \"A combat modifier changed the outcome.\"",
    f"{PROF}:141": "ResolveDefaultDelivery(CombatEventType eventType)",
    f"{PROF}:170": "case CombatEventType.ModifierTriggered:",
    f"{PROF}:196": "return CombatNarrationDelivery.PopupOrHudOnly;",
    f"{ASSET}:17": "charactersPerSecond: 45",
    f"{ASSET}:20": "consequenceSeconds: 1.45",
    f"{ASSET}:21": "effectPageSkipLockoutSeconds: 1.15",
    f"{ASSET}:47": "modifierTemplate: A combat modifier changed the outcome.",
    f"{ASSET}:49": "apStolenTemplate: '{target} lost {amount} AP.'",
    f"{ASSET}:56": "eventRules: []",
    f"{PAY}:29": "public string displayText;",
    f"{PAY}:33": "public bool gameplayApplied;",
    f"{PAY}:107": "applied\" : \"skipped\")}: {payload.reason}",
    f"{PAY}:127": "payload.schemaVersion == CurrentSchemaVersion",
    f"{RES}:134": "ProcessBattleStartModifiers(state);",
    f"{RES}:206": "_log.RegisterName(c.instanceId, CombatNarrationNaming.FormatChainlingName(team, c));",
    f"{RES}:847": "hasPriority = ResolvePriorityForAction(",
    f"{RES}:2529": "ProcessRiposteModifiers(target, attacker, move, damage, state.currentTurn);",
    f"{RES}:2563": "ProcessOnKOModifiers(attacker, move, target, state.currentTurn);",
    f"{RES}:2595": "_log.LogKO(",
    f"{RES}:4832": "\"Target HP condition not met\"",
    f"{RES}:4844": "\"Attacker HP condition not met\"",
    f"{RES}:4858": "\"No ally KO this turn\"",
    f"{RES}:4872": "\"Target did not miss last turn\"",
    f"{RES}:4886": "\"No buffed partner\"",
    f"{RES}:4900": "\"No ally attack earlier this turn\"",
    f"{RES}:4913": "\"Target was not hit earlier this turn\"",
    f"{RES}:5243": "if (!context.TryMarkOutcomeEmitted(marker))",
    f"{RES}:5268": "\"Damage modifier applied after hit proof\"",
    f"{RES}:5320": "\"On-hit proc failed\"",
    f"{RES}:5331": "LogHealApplied(turn, attacker, attacker, move, actual, $\"modifier {mod.sectionId}\");",
    f"{RES}:5358": "\"On-crit proc failed\"",
    f"{RES}:5401": "\"Low HP mitigation condition not met\"",
    f"{RES}:5635": "private void LogModifierOutcome(",
    f"{RES}:5654": "_log.Log(ModifierOutcomePayload.CreateEvent(",
    f"{RES}:5937": "\"No ally for shared buff\"",
    f"{RES}:5998": "actual > 0, \"Patch Kit heal\"",
    f"{RES}:6011": "\"No empty bench slots\"",
    f"{RES}:6061": "\"No ally for Spotlight\"",
    f"{RES}:6111": "LogModifierOutcome(mod, defender, attacker, move, turn, true, \"Status mirror\");",
    f"{RES}:6173": "LogModifierOutcome(mod, actor, actor, move, turn, false, $\"{label} chance failed\");",
    f"{RES}:6191": "\"Auto-cast skipped damaging host\"",
    f"{RES}:6201": "LogModifierOutcome(mod, actor, selected ?? actor, move, turn, true, reason);",
    f"{RES}:6321": "sourceTag: $\"modifier.{section}\"",
    f"{RES}:6339": "sourceTag: $\"modifier.{section}\"",
    f"{RES}:6346": "gained a post-cast barrier from {section}{note}.",
    f"{RES}:6444": "\"OnKO proc failed\"",
    f"{RES}:6538": "took {actual} chip damage from escape trigger.",
    f"{RES}:6718": "ProcessOnEscapeModifiers(state, team, actor, state.currentTurn);",
    f"{RES}:7150": "damageAbsorbed = Math.Max(damageAbsorbed, preMitigationDamage - finalHpDamage);",
    f"{RES}:7306": "\"Priority modifier applied\"",
    f"{PACE}:332": "private IEnumerator HandleModifierTriggered(CombatEvent evt)",
    f"{PACE}:339": "if (hasPayload && !payload.gameplayApplied)",
    f"{PACE}:361": "float hold = ResolveModifierLabelHold();",
    f"{PACEPROF}:26": "public float modifierLabelHold = 0.9f;",
    f"{APS}:112": "_log?.LogAP(turn, CombatEventType.APSpent, target.instanceId, -drained, target.currentAP, CombatApMutationSource.Drain);",
    f"{APS}:123": "return Gain(source, drained, turn, mutationSource, CombatEventType.APStolen",
    f"{UTIL}:101": "_apService?.Steal(",
    f"{SWAP}:270": "$\"Cipher Lock blocked {target.displayName}'s {stat} +1.\"",
    f"{SWAP}:294": "accuracy for the rest of this turn ({modRef.sectionId})",
    f"{SWAP}:310": "mitigation for the rest of this turn ({modRef.sectionId})",
    f"{SWAP}:320": "log.Log(ModifierOutcomePayload.CreateEvent(",
    f"{CTRL}:508": "_state = _resolver.InitializeBattle(",
    f"{CTRL}:1476": "var actionQueue = _resolver.BuildActionQueueForCurrentTurn(",
    f"{CTRL}:1481": "int prevEventCount = _resolver.Log.EventCount;",
    f"{NARR}:80": "foreach (CombatNarrationPage page in _activeNarrationPlan.EnumerateResolutionPages())",
    f"{ENUMS}:106": "public enum ModifierTriggerCondition",
    f"{DEFSO}:18": "public string displayName;",
    f"{DEFSO}:89": "public string GetDisplayName()",
    f"{COORD}:757": "definition.GetDisplayName()",
    f"{LOG}:147": "public string Name(string instanceId)",
    f"{DMG}:188": "int preMitigationDamage = Math.Max(0, (int)Math.Floor(baseDamage));",
    f"{DMG}:471": "float capped = Math.Max(0f, Math.Min(sourceCap, source.mitigation));",
    f"{WALLSTATE}:75": "return barrier.sourceTag.Trim();",
    f"{WALLLABEL}:95": "public static string FormatBlockHits(int hitBudget)",
    f"{DIALOG}:304": "int totalCharacters = bodyText.textInfo.characterCount;",
    f"{INTRO}:13": "public static void Apply(TMP_Text text",
    f"{T_COMP}:430": "\"Setup Feed triggered. The next ally move gains 12% power.\", plan.consequencePage.text",
    f"{T_COMP}:460": "\"Setup Feed faded. The ally's next action was not a power move, so the 12% boost was lost.\"",
    f"{T_POP}:235": "AssertPayload(evt, \"7.2-a\", \"SYNC\"",
    f"{T_POP}:388": "def.triggerChance = 1f;",
    f"{T_TRIG}:624": "Assert.AreEqual(\"EXECUTE\", AppliedText(resolver, \"6.3-a\"));",
    f"{T_ROUND}:329": "e.message.Contains(\"+1 AP\")",
    f"{NDOC}:33": "Reveal speed: `60` characters per second",
    f"{NDOC}:36": "Consequence hold: `0.55` seconds",
}

TRIGGER_IDS = [
    "6.1-a", "6.1-b", "6.1-c", "6.1-d", "6.1-e", "6.1-f",
    "6.2-a", "6.2-b", "6.2-c",
    "6.3-a", "6.3-b", "6.3-c", "6.3-d", "6.3-e",
    "6.4-a", "6.4-b", "6.4-c", "6.4-d", "6.4-e", "6.4-f", "6.4-g", "6.4-h", "6.4-i",
    "6.5-a", "6.5-b", "6.6-a", "6.6-b",
    "6.7-a", "6.7-b", "6.7-c",
    "6.8-a", "6.8-b", "6.8-c", "6.8-d", "6.8-e", "6.8-f", "6.8-g",
    "7.1-a", "7.1-b", "7.1-c", "7.1-d", "7.2-a", "7.2-b", "7.2-c", "7.3-a",
]

CITED_TESTS = [
    (T_COMP, "ComposeAction_SetupFeedStatesTheExactPowerBonus"),
    (T_COMP, "ComposeAction_SetupFeedFadeStatesTheLostBonus"),
    (T_POP, "AllyFirstDamagingMoveEnablesComboPowerPayload"),
    (T_TRIG, "Execute_PopupNamesTheConditional"),
]

PROPOSED_TESTS = [
    "ComposeAction_SkipsModifierOutcomeThatWasNotApplied",
    "ComposeAction_QuietTierModifierAddsNoLine",
    "ComposeAction_ApStealNamesThiefAndVictim",
    "ComposeAction_ModifierBarrierUsesRegisteredName",
    "ComposeAction_FoldsLeechHealIntoAttributedLine",
    "ComposeAction_CoalescesMultiHitLeech",
    "ComposeAction_ApSiphonFoldsIntoDrainLine",
    "ComposeAction_StageFoldStatesDirectionAndSize",
    "ComposeAction_PrefixesOnlyTheTargetTheConditionalHit",
    "ComposeAction_ConditionalPrefixStacksBeforeCritical",
    "ComposeAction_NotMetConditionalGoesToTranscriptOnly",
    "ComposeAction_OpponentReactionNamesOwner",
    "ComposeAction_ModifierNameFallbackNeverShowsSectionId",
    "ComposeAction_ModifierLinesRespectLiveCap",
    "ModifierOutcome_CarriesDefinitionDisplayName",
    "ModifierOutcome_LeavesNameEmptyWhenDefinitionHasNone",
    "ModifierOutcomePayload_RoundTripsNameAndAmount_AndParsesOldJson",
    "PostCastBarrier_RegistersDisplayNameForWallNarration",
    "Execute_NotMet_NarratesNoLiveModifierLine",
    "Execute_Met_PrefixesTheDamageLine",
    "PartingShot_NarratesWithOwnerAndRegisteredNames",
    "CipherLockSwapBlock_UsesRegisteredNameAndReadableStat",
    "PreEmptive_BattleStartOutcomeIsNarratedOnce",
    "WallCredit_ExcludesModifierMitigation",
]


def all_cs_text(subdir):
    out = []
    base = os.path.join(SNAP, subdir)
    for dp, _, files in os.walk(base):
        for fn in files:
            if fn.endswith(".cs"):
                with open(os.path.join(dp, fn), encoding="utf-8", errors="replace") as f:
                    out.append(f.read())
    return "\n".join(out)


def main():
    doc = open(DOC, "r", encoding="utf-8").read()
    idx = doc.find(SECTION_HEADER)
    check("doc: new section header present exactly once", idx >= 0 and doc.count(SECTION_HEADER) == 1)
    section = doc[idx:] if idx >= 0 else ""

    # 1. Citations: every path:line in the section is expected and matches.
    cites = sorted(set(re.findall(r"`((?:Assets|Docs)/[^`\s]+?\.(?:cs|asset|md)):(\d+)`", section)))
    check("citations: section cites at least 60 path:line locations", len(cites) >= 60, f"{len(cites)} found")
    for path, ln in cites:
        key = f"{path}:{ln}"
        full = os.path.join(SNAP, path)
        if not os.path.isfile(full):
            check(f"cite {key}: file exists", False)
            continue
        ls = lines(path)
        n = int(ln)
        exp = EXPECT.get(key)
        if exp is None:
            check(f"cite {key}: has an expectation entry", False)
            continue
        ok = 1 <= n <= len(ls) and exp in ls[n - 1]
        check(f"cite {key}", ok, "" if ok else f"line={ls[n-1].strip() if 1 <= n <= len(ls) else 'OUT OF RANGE'}")
    unused = [k for k in EXPECT if k not in {f"{p}:{l}" for p, l in cites}]
    check("citations: every expectation entry is actually cited", not unused, ", ".join(unused))

    # 2. Event types used in the section exist.
    evsrc = read(EVTYPE)
    for ev in ["ModifierTriggered", "APStolen", "APSpent", "APGained", "APRefunded", "APCapped", "BarrierBlocked",
               "WallExpired", "HealApplied", "StageChanged", "StatusApplied", "StatusRefreshed", "StatusCleansed",
               "BonusActionGranted", "BuffBlocked", "HPDotDamage", "SystemMessage", "BattleStart", "MoveUsed"]:
        check(f"CombatEventType.{ev} exists", re.search(rf"^\s*{ev},", evsrc, re.M) is not None)

    # 3. Delivery defaults claimed in N1/N4/N7.
    prof = read(PROF)
    body = prof[prof.index("public static CombatNarrationDelivery ResolveDefaultDelivery"):]
    outcome_block = body[:body.index("return CombatNarrationDelivery.OutcomeSummary;")]
    live_block = outcome_block[:outcome_block.index("return CombatNarrationDelivery.Live;")]
    outcome_cases = set(re.findall(r"case CombatEventType\.(\w+):", outcome_block)) - set(re.findall(r"case CombatEventType\.(\w+):", live_block))
    all_cases = set(re.findall(r"case CombatEventType\.(\w+):", body[:body.index("default:")]))
    check("delivery: ModifierTriggered and APStolen are OutcomeSummary", {"ModifierTriggered", "APStolen"} <= outcome_cases)
    for ev in ["APGained", "APRefunded", "APCapped", "APSpent", "SystemMessage", "BattleStart"]:
        check(f"delivery: {ev} falls to default PopupOrHudOnly", ev not in all_cases)
    check("delivery: StatusExpired is TranscriptOnly",
          "case CombatEventType.StatusExpired:" in body[body.index("return CombatNarrationDelivery.OutcomeSummary;"):body.index("return CombatNarrationDelivery.TranscriptOnly;")])

    # 4. N1: composer never reads gameplayApplied / boolValue for modifiers; presenter does.
    comp = read(COMP)
    mt_case = comp[comp.index("case CombatEventType.ModifierTriggered:"):comp.index("case CombatEventType.APGainBlocked:")]
    check("N1: composer ModifierTriggered case ignores gameplayApplied/boolValue",
          "gameplayApplied" not in mt_case and "boolValue" not in mt_case)
    check("N1: composer has no gameplayApplied reference at all", "gameplayApplied" not in comp)
    check("N1: Setup Feed is the only special-cased section in the composer",
          re.findall(r'"(\d+\.\d+-[a-z])"', comp) == ["7.2-c"])
    false_calls = len(re.findall(r"LogModifierOutcome\([^;]*?,\s*false,", read(RES), re.S))
    check("N1: resolver has >= 20 LogModifierOutcome(..., false, ...) call sites", false_calls >= 20, f"{false_calls}")

    # 5. N2: payload has no display-name field.
    pay = read(PAY)
    fields = re.findall(r"public (?:string|int|bool|ModifierPopupMoment) (\w+)(?: = [^;]+)?;", pay)
    check("N2: payload fields are exactly the 12 current ones (no modifierName/amount yet)",
          fields == ["schemaVersion", "sectionId", "category", "displayText", "anchorChainlingId", "moment",
                     "momentName", "gameplayApplied", "reason", "sourceChainlingId", "targetChainlingId", "moveId"],
          ",".join(fields))
    check("N2: CurrentSchemaVersion is 1", "public const int CurrentSchemaVersion = 1;" in pay)

    # 6. Catalog: 109 refs, no trigger fields, no per-modifier assets captured.
    cat = read(CATALOG)
    refs = re.findall(r"^\s*- \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}", cat, re.M)
    check("catalog: 109 modifier references", len(refs) == 109, f"{len(refs)}")
    check("catalog: asset has no triggerCondition / displayName fields",
          "triggerCondition" not in cat and "displayName" not in cat)
    data_files = [os.path.join(dp, f) for dp, _, fs in os.walk(os.path.join(SNAP, "Assets/Data")) for f in fs if not f.endswith(".meta")]
    check("catalog: no individual modifier .asset files in snapshot", len(data_files) == 2, f"{len(data_files)} data files")
    check("Modifiers.csv not captured", not os.path.exists(os.path.join(SNAP, "Docs/Specs/Combat/Data/Modifiers.csv")))

    # 7. Trigger enum: 15 values, 4 without runtime readers.
    enums = read(ENUMS)
    m = re.search(r"public enum ModifierTriggerCondition\s*\{(.*?)\}", enums, re.S)
    values = [v.strip().split("=")[0].strip() for v in re.sub(r"//.*", "", m.group(1)).split(",") if v.strip()]
    check("enum: ModifierTriggerCondition has 15 values", len(values) == 15, ",".join(values))
    runtime = all_cs_text("Assets/Scripts/Combat/Runtime")
    unread = [v for v in values if not re.search(rf"ModifierTriggerCondition\.{v}\b", runtime)]
    check("enum: exactly OnMiss, EndOfTurn, OnBattleStart, OnIncomingHit have no runtime reader",
          sorted(unread) == sorted(["OnMiss", "EndOfTurn", "OnBattleStart", "OnIncomingHit"]), ",".join(unread))

    # 8. Trigger ids: 45, each in runtime code and in the table.
    check("ids: 45 trigger/synergy ids", len(TRIGGER_IDS) == 45 and len(set(TRIGGER_IDS)) == 45)
    table = section[section.index("### 3. Per-trigger table"):section.index("### 4. Proposed narration rules")]
    s64 = {"6.4-a": "S64A", "6.4-b": "S64B", "6.4-c": "S64C", "6.4-d": "S64D", "6.4-e": "S64E"}
    for sid in TRIGGER_IDS:
        in_code = f'"{sid}"' in runtime
        check(f"id {sid}: present in runtime code and in the table", in_code and sid in table)
    gen = read("Assets/Scripts/Combat/Runtime/Services/MoveModifierGenerator.cs")
    pool = set(re.findall(r'"(\d\.\d-[a-z])"', gen))
    rest = set(TRIGGER_IDS) - (pool & set(TRIGGER_IDS))
    check("ids: the 45 = generator pool rows (6.x/7.x) plus 6.5, 6.6, 6.8, 7.3-a",
          rest == {"6.5-a", "6.5-b", "6.6-a", "6.6-b", "6.8-a", "6.8-b", "6.8-c", "6.8-d", "6.8-e", "6.8-f", "6.8-g", "7.3-a"},
          ",".join(sorted(rest)))

    # 9. Specific mechanics.
    res = read(RES)
    check("N5: TitleCase of 'modifier.6.7-a' yields 'Modifier.6.7 A'", title_case("modifier.6.7-a") == "Modifier.6.7 A")
    check("N5: resolver never registers a 'modifier.' name", "RegisterName($\"modifier." not in res)
    check("N7: battle start runs inside InitializeBattle",
          res.index("public CombatState InitializeBattle") < res.index("ProcessBattleStartModifiers(state);") < res.index("private static void ResetBattleLocalState"))
    check("N8: KO modifiers run before LogKO in the hit loop",
          not any("_log.LogKO(" in l for l in lines(RES)[2563:2594]) and lines(RES)[2594].strip().startswith("_log.LogKO("))
    check("N8: damage-context outcomes are emitted after LogDamage",
          res.index("_log.LogDamage(state.currentTurn, attacker.instanceId, target.instanceId,") < res.index("                    EmitDamageContextModifierOutcomes("))
    check("fault-6 context: full block 'continue's before EmitDamageContextModifierOutcomes",
          lines(RES)[2346].strip() == "continue;" and res[:res.index("                    EmitDamageContextModifierOutcomes(")].count("\n") + 1 > 2347)
    check("N10: escape chip HPDotDamage is a LogMessage without target id",
          "_log.LogMessage(" in lines(RES)[6535] and "CombatEventType.HPDotDamage," in lines(RES)[6536])
    check("N3: Steal drains victim then Gains on source", "int drained = Drain(target, amount, turn, mutationSource);" in read(APS))
    check("plan: CombatLogTypeNames.GetStatName exists", "CombatLogTypeNames.GetStatName(" in res)
    check("plan: CombatApMutationSource.Drain exists", "CombatApMutationSource.Drain" in read(APS))
    check("plan: DamageComputationTrace exists", "DamageComputationTrace" in read(DMG))
    check("R4: 'Blocks 1 hit' copy exists in WALLS_BARRIERS_PVP_REDESIGN_V2.md",
          "Blocks 1 hit" in read("Docs/Combat/Design/WALLS_BARRIERS_PVP_REDESIGN_V2.md"))
    check("N15: no richText usage in narration views",
          "richText" not in read(DIALOG) and "richText" not in read("Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationBoxController.cs"))

    # 10. Tests: cited exist, proposed do not, composer test count is 19.
    for path, name in CITED_TESTS:
        check(f"test exists: {name}", re.search(rf"public void {name}\(", read(path)) is not None)
    tests_src = all_cs_text("Assets/Scripts/Combat/Tests")
    for name in PROPOSED_TESTS:
        check(f"proposed test is new: {name}", f"void {name}(" not in tests_src and name in section)
    check("tests: CombatNarrationComposerTests has 19 [Test]s", read(T_COMP).count("[Test]") == 19)
    check("tests: no test asserts the generic modifier sentence", "A combat modifier changed the outcome" not in tests_src)
    check("tests: new test file does not exist yet",
          not os.path.exists(os.path.join(SNAP, "Assets/Scripts/Combat/Tests/CombatModifierNarrationTests.cs")))

    # 11. Section shape.
    for h in ["### 1. Summary", "### 2. Findings", "### 3. Per-trigger table", "### 4. Proposed narration rules",
              "### 5. Implementation plan for Codex", "### 6. Open questions for Cronus", "### Verification"]:
        check(f"section has heading: {h}", h in section)
    fids = re.findall(r"^#### (N\d+) \((High|Medium|Low|Info)\):", section, re.M)
    check("findings: N1..N16 in order, each with severity", [f for f, _ in fids] == [f"N{i}" for i in range(1, 17)])
    rules = re.findall(r"\*\*(R\d+) \(Proposal\):", section)
    check("rules: R1..R11 each labeled Proposal", rules == [f"R{i}" for i in range(1, 12)])
    qs = re.findall(r"^- \*\*(Q\d+)\.", section, re.M)
    check("questions: Q1..Q9 present", qs == [f"Q{i}" for i in range(1, 10)])
    qblock = section[section.index("### 6. Open questions for Cronus"):section.index("### Verification")]
    check("questions: each has a Default", qblock.count("Default:") == 9)

    # 12. Original content preserved; nothing else touched.
    orig = original_doc_bytes()
    cur = open(DOC, "rb").read()
    first, rest = orig.split(b"\r\n", 1)
    pointer = (b"> Second pass (2026-10-21) on modifier narration: see [Second pass (2026-10-21): Modifier narration]"
               b"(#second-pass-2026-10-21-modifier-narration) at the end of this doc.")
    expected_prefix = first + b"\r\n\r\n" + pointer + b"\r\n" + rest
    check("doc: original text preserved byte-for-byte, plus one pointer line", cur.startswith(expected_prefix))
    check("doc: CRLF line endings kept", cur.count(b"\n") == cur.count(b"\r\n"))
    changed = changed_snapshot_files()
    check("snapshot: no file other than the audit doc changed", changed == [], ", ".join(changed[:5]))

    # 13. Pass count stated in the doc must match this run.
    total = len(results) + 1
    passed_before = sum(1 for _, ok, _ in results if ok)
    stated = re.search(r"Last run: (\d+) of (\d+) checks passed\.", section)
    ok_count = stated is not None and int(stated.group(2)) == total and int(stated.group(1)) == total
    check("doc: stated pass count matches this run", ok_count and passed_before == total - 1,
          f"stated={stated.group(0) if stated else None} actual_total={total}")

    for name, ok, detail in results:
        print(("PASS " if ok else "FAIL ") + name + (f"  [{detail}]" if detail and not ok else ""))
    passed = sum(1 for _, ok, _ in results if ok)
    print(f"\n{passed} of {len(results)} checks passed.")
    return 0 if passed == len(results) else 1


def title_case(tag):
    # Mirrors CombatNarrationComposer.ResolveWallName fallback + TitleCaseWords.
    value = tag.strip().replace("_", " ").replace("-", " ")
    out, cap = [], True
    for c in value:
        if c.isspace():
            out.append(" ")
            cap = True
            continue
        out.append(c.upper() if cap else c.lower())
        cap = False
    return "".join(out).strip()


def original_doc_bytes():
    if os.path.exists(ZIP):
        with zipfile.ZipFile(ZIP) as z:
            for n in z.namelist():
                if n.replace("\\", "/") == DOC_REL:
                    return z.read(n)
    return open(os.path.join(ROOT, "original_audit.md"), "rb").read()


def changed_snapshot_files():
    changed = []
    if not os.path.exists(ZIP):
        return ["(snapshot zip missing; cannot compare)"]
    with zipfile.ZipFile(ZIP) as z:
        names = set()
        for info in z.infolist():
            rel = info.filename.replace("\\", "/")
            if rel.endswith("/"):
                continue
            names.add(rel)
            if rel == DOC_REL:
                continue
            p = os.path.join(SNAP, rel)
            if not os.path.isfile(p) or open(p, "rb").read() != z.read(info):
                changed.append(rel)
    for dp, _, files in os.walk(SNAP):
        for fn in files:
            rel = os.path.relpath(os.path.join(dp, fn), SNAP).replace(os.sep, "/")
            if rel not in names:
                changed.append("extra:" + rel)
    return changed


if __name__ == "__main__":
    sys.exit(main())
