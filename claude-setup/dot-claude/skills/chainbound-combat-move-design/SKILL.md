---
name: chainbound-combat-move-design
description: Project-specific Chainbound combat move design and audit workflow. Use when creating, editing, reviewing, balancing, or describing combat moves in Docs/Specs/Combat/Data/Moves.csv, move ScriptableObjects, species move pools, status interactions, modifier compatibility, rarity assignments, or any proposed move effect that may require CombatResolver support.
---

# Chainbound Combat Move Design

## Overview

Use this skill to keep move work data-driven, resolver-aware, and consistent with Chainbound type identity. Runtime truth wins when docs and implementation disagree: if a proposed move effect is not represented by existing data fields or `CombatResolver` support, plan a reusable resolver extension before editing move data.

## Source Of Truth

Inspect the relevant files before changing move data:

- Data: `Docs/Specs/Combat/Data/Moves.csv`, `Statuses.csv`, `Modifiers.csv`, `SpeciesMovePools.csv`, and `_LEGEND.md`.
- Design docs: `COMBAT_Moves.txt`, `COMBAT_Rarity.txt`, `COMBAT_Stats.txt`, `COMBAT_Resolution.txt`, `COMBAT_Statuses.txt`, `COMBAT_Modifiers.txt`, and `COMBAT_TypeProfiles.md`.
- Runtime support: `CombatResolver.cs`, `UtilityEffectExecutor.cs`, `CombatActionEligibilityService.cs`, `DamageCalculator.cs`, `StatusProcessor.cs`, `MoveDefinitionSO.cs`, `MoveEnums.cs`, and the importer.

## Workflow

1. Classify the request.
   - `Data-only`: the effect maps directly to existing CSV fields and resolver behavior.
   - `Resolver-supported special family`: the effect belongs to an existing special resolver family such as AP transfer, damage redirect, effect relay, position swap, mitigation windows, or full-absorb barriers.
   - `Requires resolver extension`: the effect is conditional, delayed, random, event-driven, or stateful beyond existing enums/opcodes.

2. Check resolver feasibility before balance.
   - Read [resolver-capability-matrix.md](references/resolver-capability-matrix.md).
   - Do not encode unsupported mechanics only in `Description`.
   - If resolver work is needed, propose generic data fields/opcodes and tests before changing move rows.

3. Check combat design fit.
   - Read [type-rarity-balance-guide.md](references/type-rarity-balance-guide.md).
   - Preserve type identity while allowing 1-2 off-theme moves per typed pool for variety.
   - Treat rarity as complexity, conditionality, flexibility, acquisition scarcity, or a constrained tiered-sibling intensity bump, not automatic raw power.

4. Edit or audit the move.
   - Use [move-authoring-checklist.md](references/move-authoring-checklist.md).
   - Keep `Moves.csv` as the source of truth; generated assets should be synced by the importer or updated deliberately when needed.
   - Do not directly apply ChemistryOnly statuses from move data.
   - Keep player descriptions tactical, species-neutral, and within the MoveSelectPanel budget.

## Output Expectations

When presenting move changes, include:

- The classification for each move: data-only, supported special family, or requires resolver extension.
- Any type identity or rarity concerns.
- Whether a higher-rarity move is a tiered sibling of an existing move, and why the lower-tier move remains tactically valid.
- Exact CSV fields or resolver additions required.
- Focused tests or smoke scenarios for AP, targeting, statuses, stages, barriers, and logging.

For resolver extensions, prefer reusable enums/opcodes such as `UtilityEffectType`, `ActiveConditionType`, or status definitions over new one-off `moveId` branches.
