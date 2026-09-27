# Combat Data Resync & Resolve-Time Evaluation Plan

> **Implementation note:** The beta species-pool gap closure is implemented by [Resolver Seven Buckets](../../.cursor/plans/resolver_seven_buckets_66dcc1ed.plan.md). The 118-move routing snapshot lives in [combat_species_pool_routing_inventory.md](Audits/combat_species_pool_routing_inventory.md).

> **Purpose:** Codex / implementation planning reference.  
> **Scope:** Comprehensive audit and resync of `Moves.csv` and `Modifiers.csv` against runtime move assets; ensure every claimed utility/effect has a resolver path and a defined evaluation timing.  
> **Status:** V1 resolver surface plus the 11-species beta routing pass are implemented; catalog-wide post-beta migration remains.  
> **Data source of truth:** `Docs/Specs/Combat/Data/Moves.csv`, `Docs/Specs/Combat/Data/Modifiers.csv`, `_LEGEND.md`  
> **Prerequisite for:** Forensic battle-log improvements (deferred until data and resolver truth are aligned).

---

## 1. Goal Summary

Combat design data lives in CSV, flows through the importer into `MoveDefinitionSO` assets, and is executed by resolver subsystems at runtime. Today those three layers can drift apart: CSV rows describe effects that assets do not carry, assets carry fields the resolver never reads, and descriptions promise mechanics with no execution hook.

**Primary outcomes:**

1. **Data parity** — Every CSV row for moves and modifiers matches its imported asset (and vice versa where assets are intentionally hand-maintained).
2. **Resolver routing** — Every claimed effect maps to a concrete execution path; nothing remains in description-only limbo.
3. **Resolve-time contract** — Conditional effects evaluate against **live battle state at the moment the acting chainling resolves its action**, not against a stale turn-start snapshot.
4. **Observable truth** — When logging work resumes, exported events can explain *why* an effect fired or skipped at that moment.

**Non-negotiable ordering:**

1. Define evaluation timing rules (this document, §3–§4).
2. Inventory drift and classify every row (§6–§7).
3. Resync CSV → assets via importer (§8).
4. Close resolver gaps for rows that cannot be data-only (§9).
5. Add tests and structured log hooks (§10; ties to forensic logging follow-up).

Do **not** expand battle-log transcripts until resolver routing and timing are trustworthy — logs would otherwise document missing or mis-timed effects.

---

## 1.1 V1 Implementation Surface

The first implementation pass adds a reusable move-level resolution profile rather than one-off move branches.

- `MoveDefinitionSO.resolveProfile` exposes resolve-time knobs for hit count, accuracy, crits, damage formula tuning, utility timing, and resolve-time predicates.
- `CombatFactory.CreateMove` deep-copies the profile into `MoveInstance.resolveProfile`, so battle balance can be traced to the exact runtime move instance.
- `DamageCalculator` now reads move-authored power, flat damage, formula roll, level factor, mitigation floor, minimum damage, stat override, consumed-status multiplier, exploit-mark multiplier, accuracy, and crit settings.
- `CombatResolver` now reads move-authored hit-count tuning before the hit loop and can execute utility payloads on damaging moves before first hit, after each confirmed hit, or after move completion.
- Conditional effects re-check legacy active conditions at resolve time and can also require profile predicates such as HP/AP thresholds, statuses, stages, speed comparison, or barrier state.
- Defaults preserve legacy math and routing unless a move definition explicitly opts into new tuning.

The 43-column move schema, importer/export round trip, conditional-damage profile, typed utility opcodes, and species-pool routing inventory are now implemented. Remaining work is the post-beta catalog migration and removal of compatibility `hardcoded_defId` routes listed in the audit.

---

## 2. The Core Design Principle: Resolve-Time Evaluation

### 2.1 Working definition

**Resolve-time evaluation** means: when a chainling’s action reaches the front of the speed-ordered action queue, the engine reads **current** combat state — HP, AP, statuses, stages, barriers, field effects, and same-turn ledger memory — and uses that state to decide eligibility, conditional bonuses, procs, and downstream effects.

This is **not** pre-turn planning. UI may grey out illegal moves early, but **gameplay truth** is decided at resolve time.

### 2.2 Canonical example

> A damaging move deals bonus damage if the target has a buff.

**Correct (resolve-time):** When the caster’s action resolves, check whether the target **currently** has the buff. A faster ally may have stripped that buff earlier in the same turn; the bonus should not apply.

**Incorrect (turn-start snapshot):** Cache “target has buff” at turn start and reuse that flag for all slower actors. Faster actions that remove or overwrite the buff would be ignored.

**Also incorrect (description-only):** CSV/description says “+damage vs buffed target” but no damage-calculator predicate or modifier TRG branch exists — the move always deals base damage.

### 2.3 Same-turn memory vs live state

Some conditions depend on **events earlier in the same turn** (e.g. “if you hit 2+ targets this turn”, “if an ally used a damaging move this turn”). These use **`TurnActionLedger`** — a turn-scoped, append-only memory updated as actions complete.

| Input type | Source at resolve time |
|------------|-------------------------|
| Target HP / AP / stages / statuses | Live `CombatState` on participating chainlings |
| “Acted / missed / hit / damaged / KO’d this turn” | `TurnActionLedger` |
| “Missed last turn” | Ledger rollover at `BeginTurn` |
| Modifier TRG predicates (HP threshold, ally attacked, etc.) | Live state + ledger, evaluated in the modifier phase attached to that action |
| End-of-turn riders (“gain AP at turn end”) | **Turn-end phase**, not resolve-time — must be explicitly scheduled |

Resolve-time evaluation and turn-end evaluation are both valid; the plan requires **labeling which timing each effect uses** so nothing is ambiguous.

### 2.4 Recommended term for docs and logs

Use **resolve-time** (or **at-action resolve**) as the standard phrase in specs, CSV notes, tests, and forensic events.

Avoid “realtime” alone — it is vague. Prefer:

- **Resolve-time** — predicate read when the action executes.
- **Turn-start** — hooks in `TurnStart` / turn-open phase (regeneration, duration ticks, field upkeep).
- **Turn-end** — hooks after all actions complete (delayed heals, banked AP release, EOT procs).
- **Post-hit** — after damage application on a confirmed hit (on-hit status, recoil, drain).
- **Reactive** — in response to an external event while not the actor’s queued action (passive on damage taken, intercept, wall retaliation).

Every effect in CSV should declare one primary timing bucket (see §4).

---

## 3. Evaluation Timing Taxonomy

| Timing bucket | When state is read | Typical effect families | Primary executor(s) |
|---------------|-------------------|-------------------------|---------------------|
| **UI eligibility** | Before queue commit; may refresh on target change | AP cost, recharge, `ActiveConditionType`, targeting legality | `CombatActionEligibilityService` |
| **Resolve-time (pre-hit)** | Action dequeued; before hit loop | Conditional power, accuracy context, modifier POT/TRX, conditional TRG in damage context | `CombatResolver` → `BuildDamageModifierContext`, `DamageCalculator` |
| **Resolve-time (utility)** | Action dequeued; non-damaging or support path | Gain AP, mitigation window, stage changes, status apply | `ResolveUtilityMove` → `UtilityEffectExecutor`, status processor |
| **Post-hit** | After hit confirmation and damage applied | On-hit/on-crit modifier TRG, recoil, drain, some status riders | `ProcessAfterDamageModifiers`, hit pipeline |
| **Turn-end** | After all actions in turn | Banked AP release, EOT heals, delayed cleanses, EFF bonus-action procs | Turn-end scheduler (partial today — gap list in §9) |
| **Reactive / passive** | When triggering event occurs | `OnDamageTaken`, wall hit, KO hooks | Passive handlers, wall/barrier subsystem, swap resolver |

**Rule:** If copy says “when you use this move” or “if the foe currently…”, timing is **resolve-time** unless explicitly “at end of turn” or “when hit”.

**Rule:** UI eligibility may be conservative (hide illegal moves) but must not be the only place a condition is enforced.

---

## 4. Resolver Routing Model

An effect is **routed** when it has all of:

1. **Data representation** — CSV column, utility opcode, status id, stage string, modifier section, or explicit resolver family id.
2. **Import path** — `Combat2DataImporter` writes the representation onto the asset.
3. **Runtime executor** — A named code path executes the effect (not dead serialized fields).
4. **Timing declaration** — Which bucket from §3 applies.
5. **Deterministic outcome** — Success, fail, skip-with-reason, or proc roll recorded for tests/logs.

### 4.1 Move effect channels

| Channel | CSV / asset fields | Runtime entry | Common limbo cause |
|---------|-------------------|---------------|-------------------|
| **Core damage** | `Power`, `Category`, tags | `ResolveDamagingMove` | Conditional power only in description |
| **Status apply** | `StatusEffects` | Status processor on hit or utility path | Chemistry-only status in move row |
| **Stage changes** | `StageChanges` | Utility or hit pipeline | Wrong target routing (`Self` vs foe) |
| **Utility opcodes** | `UtilityEffects` | `UtilityEffectExecutor` | Opcode missing; or move is `Damaging*` so utility path never runs |
| **Heal** | `HealPercentMaxHP` | Utility/support resolver | Threshold conditions only in description |
| **Active gating** | `ActiveConditionType`, threshold | Eligibility service | Condition enforced in UI only |
| **Passive trigger** | `PassiveTrigger`, tags | Passive subsystem | Enum exists but generic passive execution missing |
| **Explicit family** | Move id convention | Hard-coded branch | One-off without generalizing opcode |

### 4.2 Modifier effect channels

Modifiers attach to move instances and route through section ids (POT, EFF, AUG, TRX, TRG, SYN). See `COMBAT_MODIFIER_RESOLVER_AND_POPUP_PLAN.md` for section-level coverage.

**Resync rule:** A modifier section in `Modifiers.csv` is either:

- **Routed** — Importer can roll it; resolver applies it at the correct timing; popup/logging contract defined.
- **Quarantined** — Removed from generator pool until routed.
- **Documented partial** — Applies subset of CSV effect; CSV note + matrix updated.

Moves and modifiers must be audited **together**: a move tag (`TRIG`, `APMAN`, `PAS`) implies modifier or utility paths that must exist.

### 4.3 Disposition labels (per row / per effect)

Use these in the audit inventory:

| Label | Meaning |
|-------|---------|
| **Routed** | Data + import + executor + timing documented |
| **Data-only ready** | Fully expressible with existing fields; resync import only |
| **Resolver extension** | Needs new opcode, predicate, or scheduler before CSV claim is valid |
| **Wrong path** | Data present but executor never invoked (e.g. utility on damaging move) |
| **Description limbo** | Player-facing text claims effect; no data field |
| **Asset drift** | CSV and asset disagree |
| **Quarantined** | Intentionally disabled pending engine work |

---

## 5. Known Failure Modes (Why Resync Matters)

These are representative — the audit should confirm scope, not rely on anecdotes alone.

| Symptom | Likely routing failure |
|---------|------------------------|
| Move spends AP but “does nothing” extra | `UtilityEffects` empty on asset despite CSV payload |
| Conditional damage never triggers | Predicate only in description; damaging path has no TRG hook |
| Passive never fires | `PassiveTrigger` set but generic passive executor missing |
| Modifier rolled but silent | Section id not implemented in `BuildDamageModifierContext` / post-hit |
| Effect uses stale board state | Turn-start cache used instead of resolve-time read |
| Log says hit but player saw no bonus | Resolver applied context pre-hit on miss, or popup timing ≠ effect timing |

**Example class (asset drift):** CSV row specifies utility payload; imported `Move_*.asset` has `utilityEffects: []` — executor path exists but is never fed.

**Example class (wrong path):** Damaging move with AP gain rider — CSV describes resolve-time AP check; no opcode on damaging pipeline and utility executor not called from `ResolveDamagingMove`.

---

## 6. Audit Scope

### 6.1 Moves.csv

For **each data row** (excluding comments):

- Identity: `MoveId`, `Category`, tags, targeting, AP, power, accuracy.
- Every non-empty effect column: `StatusEffects`, `StageChanges`, `UtilityEffects`, `HealPercentMaxHP`, passive fields, `ActiveCondition*`.
- Description vs machine-readable fields — flag description-only claims.
- Importer mapping: column → asset field → executor.
- Timing bucket per effect.

### 6.2 Modifiers.csv

For **each section id**:

- Category, trigger condition, allowed categories/tags.
- Resolver method(s) and timing (pre-hit, post-hit, swap, KO, turn-end).
- Whether rolled modifiers can attach to moves that lack compatible tags.
- Popup/logging contract (defer presentation polish until resolver routed).

### 6.3 Cross-reference sets

- `Statuses.csv` — move-applied statuses must be General (not Chemistry-only direct apply).
- `SpeciesMovePools.csv` — acquisition impact only; not resolver routing.
- `.cursor/skills/chainbound-combat-move-design/references/resolver-capability-matrix.md` — refresh after audit to match routed reality.

### 6.4 Runtime assets

- `Assets/Data/Combat/Moves/Move_*.asset` — one per `MoveId`.
- Spot-check modifier instance serialization on equipped moves if drift tool supports it.

---

## 7. Comprehensive Resync Workflow

### Phase A — Automated inventory (read-only)

Build a report (editor tool or scripted importer pass) that emits:

1. **CSV → asset diff** — Field-by-field mismatch per move id.
2. **Asset orphan list** — Assets with no CSV row.
3. **Effect routing table** — Each non-empty effect cell → disposition label (§4.3).
4. **Timing map** — Per effect, primary timing bucket (§3).
5. **Limbo roll-up** — Counts by label; prioritized queue for resolver work.

Output format suggestion: `Docs/Combat/Audits/combat_data_routing_inventory.md` (generated artifact, refreshed on demand).

### Phase B — Resolve-time contract sign-off

- Freeze timing vocabulary (§2.4, §3).
- Document per-subsystem rules: when `BuildDamageModifierContext` runs, when ledger entries are visible, when turn-end jobs enqueue.
- Add “timing” column to audit template for manual review of edge cases (passives, walls, field entry).

### Phase C — Row-by-row classification

- Apply move authoring checklist (skill reference: `move-authoring-checklist.md`).
- Split rows into:
  - **Batch resync** — Data-only ready + asset drift.
  - **Engine backlog** — Resolver extension + wrong path.
  - **Copy fix** — Description limbo → trim copy or add data fields.

No CSV edits that claim new mechanics until the matching resolver extension is scheduled.

### Phase D — Importer resync

- Re-run `Combat2DataImporter` for moves (and modifiers if applicable).
- Verify assets updated; commit CSV + generated assets together when policy allows.
- Re-run diff tool — target zero **asset drift** for routed rows.

### Phase E — Close resolver gaps

Priority order:

1. **Wrong path** — Wire existing opcodes into correct pipeline (e.g. utility invocation from damaging moves when CSV uses `UtilityEffects` + `EffectOnly` pattern).
2. **High-frequency TRIG tags** — Conditional damage/AP predicates used by many rows.
3. **Passive triggers** — Generic execution for declared `PassiveTrigger` values.
4. **Turn-end scheduler** — Banked AP, delayed heals, EFF bonus actions.
5. **Long-tail exotic** — Random branches, bounce, stat-sub damage formulas.

Each extension ships with: opcode/enum, importer support, executor hook, **resolve-time read points**, unit tests.

### Phase F — Verification & logging readiness

- Focused tests per disposition change (AP clamp, miss vs hit, stage caps, ledger visibility same turn).
- Forensic event stubs (optional in this phase): `EffectEvaluated`, `EffectSkipped`, `ConditionSnapshot` with resolve-time payload — full transcript work stays deferred.

---

## 8. Per-Move Routing Checklist (Abstract)

For each move, answer:

1. **Category path** — Damaging, utility, support, or passive — which `Resolve*` method runs?
2. **Effect list** — Enumerate every effect from columns + description.
3. **For each effect:**
   - Data field or opcode?
   - Executor class/method?
   - Timing bucket?
   - Ledger or live state inputs?
   - On miss / on fail behavior?
4. **Modifier compatibility** — Which tags gate rolled modifiers; any forbidden combos violated in pools?
5. **Disposition** — Routed / data-only / extension / limbo.
6. **Test id** — Single test name or scenario covering the critical path.

---

## 9. Modifier Resync Alignment

Modifiers resync is not separate from moves resync.

| Concern | Action |
|---------|--------|
| Section implemented but never rolled | Verify generator pool ⊆ routed sections |
| Section rolled but unimplemented | Quarantine in pool or implement handler |
| TRG timing wrong (pre-hit vs post-hit vs resolve-time SYN) | Align with `COMBAT_MODIFIER_RESOLVER_AND_POPUP_PLAN.md` Phase A |
| Move tag implies modifier behavior | Ensure move row tags match modifier trigger expectations |

**SYN / TRG modifiers** especially must use resolve-time predicates (ally attacked this turn, partner buffed **now**, target HP **now**).

---

## 10. Anti-Patterns to Eliminate

| Anti-pattern | Replacement |
|--------------|-------------|
| Description is the spec | Machine-readable field or explicit `RequiresResolverExtension` note in audit |
| Turn-start buff check for action bonuses | Resolve-time read in damage/utility path |
| Serialized utility array never consumed | Wrong-path fix or remove from CSV |
| Importer not run after CSV edit | Resync phase D mandatory |
| One-off `if (moveId == …)` without opcode | Reusable predicate or utility opcode |
| Logging modifier context on miss as success | Post-hit or confirmed-crit gated events |
| Chemistry-only status in move row | Status system rules + CSV fix |

---

## 11. Relationship to Battle Logging

Forensic export (`CombatBattleLogExporter`, F8) remains valuable for **post-hoc** debugging but currently under-reports AP, procs, modifiers, and passives.

**After** this resync:

- Each routed effect should have a defined **log emission point** at the same timing as gameplay application.
- Skipped effects should log **skip reason** (condition false at resolve time, not “failed silently”).
- Resolve-time snapshots for conditionals enable transcript lines like: “Flame Charge: foe AP=0 → +30% damage, +1 AP”.

Until routing inventory shows near-zero **description limbo** and **asset drift**, log expansion is lower priority than resolver truth.

---

## 12. Success Criteria

| Criterion | Measure |
|-----------|---------|
| CSV ↔ asset parity | Automated diff = 0 for all non-quarantined moves |
| No description limbo | Every description clause maps to a routed effect or copy is revised |
| Timing declared | 100% of non-UI effects tagged with a §3 bucket |
| Resolve-time conditionals | Documented spot-check scenarios pass (buff stripped mid-turn, AP drained mid-turn) |
| Modifier sections | Every pool section id is Routed or Quarantined |
| Tests | Each new opcode/predicate has at least one deterministic test |
| Matrix refreshed | `resolver-capability-matrix.md` matches post-audit engine |

---

## 13. Suggested Phase Order (Summary)

```text
A. Automated inventory (diff + routing + timing map)
B. Resolve-time contract frozen (this doc §2–§3)
C. Classify every move/modifier row
D. Importer resync (data-only + drift fixes)
E. Resolver extensions (wrong path → TRIG → passives → turn-end)
F. Tests + optional forensic hooks → then battle-log transcript work
```

---

## 14. References

| Resource | Role |
|----------|------|
| `Docs/Specs/Combat/Data/Moves.csv` | Move source of truth |
| `Docs/Specs/Combat/Data/Modifiers.csv` | Modifier source of truth |
| `Docs/Specs/Combat/Data/_LEGEND.md` | Column formats |
| `Assets/Scripts/Combat/Editor/Importers/Combat2DataImporter.cs` | CSV → asset |
| `Assets/Scripts/Combat/Runtime/CombatResolver.cs` | Action resolve orchestration |
| `Assets/Scripts/Combat/Runtime/UtilityEffectExecutor.cs` | Utility opcodes |
| `Assets/Scripts/Combat/Runtime/Data/TurnActionLedger.cs` | Same-turn action memory |
| `Docs/Combat/COMBAT_MODIFIER_RESOLVER_AND_POPUP_PLAN.md` | Modifier timing & popups |
| `.cursor/skills/chainbound-combat-move-design/SKILL.md` | Authoring workflow |
| `.cursor/skills/chainbound-combat-move-design/references/resolver-capability-matrix.md` | Capability gate (to refresh) |
| `Docs/COMBAT_FORENSIC_LOGGING_PR_NOTES.md` | Current log export behavior |

---

## 15. Open Decisions (Resolve During Phase B)

1. **Damaging + utility combo pattern** — Standardize on `EffectOnly` utility column + executor hook from damaging path vs split moves.
2. **Predicate library** — Shared resolve-time conditions (`TargetAPEquals`, `TargetHasBuff`, `SelfHPBelow`, …) vs per-move branches.
3. **Quarantine policy** — Hide unimplemented moves from pools vs keep with corrected copy.
4. **Generated audit artifact location** — `Docs/Combat/Audits/` vs CI-only output.
5. **Turn-end scheduler ownership** — Single turn-end processor vs scattered `EndTurn` hooks.

These do not block Phase A inventory.
