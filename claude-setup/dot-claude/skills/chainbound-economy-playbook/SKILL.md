---
name: chainbound-economy-playbook
description: Project-specific economy and progression playbook for Chainbound / Signal Jam 3D. Use when defining rules for Known/Stamped moves, RAM sinks, candy Stamp, Bindery Bind, rebirth, capture rewards, reward pacing, or faucet/sink balance. Ownership Spine 2026-09-15.
---

# Chainbound Economy Playbook

## Overview

Use this skill when the task is no longer "analyze the design" and is now "define the rule set." This skill turns the current design direction into concrete default policies for acquisition, sinks, pacing, and long-term grind.

**Canonical ownership (lock 2026-09-15):**

1. [`Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md`](../../../Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md) — index and lock
2. [`Docs/Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md`](../../../Docs/Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md) — faucets, sinks, candy dual role, salvage

The reference playbook under `references/` is **historical internal notes** where still useful; prefer OWNERSHIP_SPINE + ECONOMY_01 on conflict. If a proposal conflicts with the current-state review, also read `../chainbound-game-direction/references/current-design-analysis.md`.

## Workflow

1. Lock terminology (player-facing).
   - **Known** = move with 0 modifiers (non-NFT PDA/profile). Do **not** say Ordinal in player rules.
   - **Stamped** = Known stamped to Metaplex Core with 1 **or** 2 modifiers. Do **not** say Inscription in player rules.
   - **Bound** = published **Chainling** archive book only — never a move tier.
   - **RAM** = primary balancing currency and sink valve.
   - **Rebirth** = long-term stat investment sink, not a short-term convenience feature.
   - Destinations: **candy machine** = Known rolls + Stamp; **Bindery** = Bind Chainlings only.

2. Start from OWNERSHIP_SPINE + ECONOMY_01 defaults.
   - Do not improvise new faucets unless the task explicitly requires it.
   - Do not add new currencies unless the task clearly justifies a new sink or fantasy.
   - Do not reintroduce Press Charge, Bound-as-move, or Bubblegum/cNFT Stamp.

3. Evaluate changes by asking:
   - Does this make the grind clearer or noisier?
   - Does this create more supply than RAM can regulate?
   - Does this preserve the choice between farming Known moves and investing in rebirth without turning max-star mastery into the only correct loop?
   - Does this keep the **candy machine** as the main move/modifier gacha (Known roll + Core Stamp) instead of letting side-faucets dominate?
   - If Stamp uses VRF, does this preserve real randomness instead of sneaking in deterministic outcomes?

4. Produce concrete rules, not vague guidance.
   - Name the faucet or sink.
   - State the player action that triggers it.
   - State the reward or cost.
   - State the anti-abuse guardrail.
   - State the intended emotional outcome.

## Default Output Structure

1. Decision summary
2. Canon terminology
3. Faucet rules
4. Sink rules
5. Progression and rebirth rules
6. Risks and exploit checks
7. Open tuning levers

## Hard Rules

- Routine battles should mostly pay RAM and XP, not finished Stamped moves.
- Leveling should primarily farm **Known** moves from a Chainling's source pool.
- The **Academy candy machine** remains the main source of modifier variance via **Stamp** (Known + RAM → Metaplex Core Stamped move). Bindery does not stamp moves.
- Stamp / candy frustration should be solved with pool design, RAM cost, and reroll access, not guaranteed roll outcomes (**no pity**).
- Burning / salvage content must never create a self-funding loop into candy Stamp.
- Entangled rewards should feel premium, but they must not become the only viable way to progress.
- **Rebirth** must be expensive enough that choosing it means delaying short-term candy or market purchases.
