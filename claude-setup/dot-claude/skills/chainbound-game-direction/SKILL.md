---
name: chainbound-game-direction
description: Project-specific design reviewer for Chainbound / Signal Jam 3D. Use when evaluating game direction, progression, difficulty clarity, economy health, ordinal and inscription acquisition, gacha hooks, retention loops, combat strategy depth, reward pacing, or feature proposals that affect player motivation and long-term balance.
---

# Chainbound Game Direction

## Overview

Use this skill to review current or proposed design decisions against the actual game shape in this repo: a combat-first creature battler where ordinal acquisition, inscription creation, capture rewards, and modifier chase all feed long-term build diversity.

Read [references/current-design-analysis.md](references/current-design-analysis.md) first. It contains the current-state model and the main risks already visible in the live specs.
For stricter economy defaults and reward-distribution rules, read `../chainbound-economy-playbook/` after the analysis pass.

## Review Workflow

1. Ground the request in the current design.
   - Start with `references/current-design-analysis.md`.
   - If the request changes reward loops or rarity, inspect:
     - `Docs/Specs/Combat/COMBAT_Rarity.txt`
     - `Docs/Specs/Combat/Data/Moves.csv`
     - `Docs/Specs/Combat/Data/Modifiers.csv`
     - `Docs/Specs/Solana/SPEC_03_BattleCapture_ChainlingPDA_InscriptionNFT_MVP.txt`
   - If the request changes combat depth, clarity, or PvP fairness, inspect:
     - `Docs/Specs/Combat/COMBAT_Moves.txt`
     - `Docs/Specs/Combat/COMBAT_BalanceReview.txt`
     - `Docs/Specs/Combat/COMBAT_Revisions_v1.md`
     - `Docs/Specs/Combat/Data/SpeciesMovePools.csv`

2. Evaluate the proposal across these lenses.
   - Reward cadence: Does each session produce visible progress?
   - Clarity: Will players understand why they won, lost, or got rewarded?
   - Difficulty: Is the challenge intentional and learnable rather than noisy?
   - Economy health: Are new faucets matched by meaningful sinks or limits?
   - Gacha health: Does randomness create excitement without invalidating planning?
   - Combat strategy: Does the change widen viable team-building and targeting choices?
   - Fairness: Does it preserve the existing "rarity is not raw power" rule?
   - MVP scope: Does it fit the current hackathon-first product shape?

3. Stress-test the proposal.
   - Check early-game readability, mid-game mastery, and long-term chase value.
   - Check whether the change helps or hurts non-whale, non-hardcore players.
   - Check whether it adds parallel systems faster than the game explains them.
   - Flag whether it creates inflation, best-in-slot lock-in, or pay-to-win perception.

4. Recommend a direction.
   - State whether to ship, revise, delay, or reject.
   - If the answer is "revise", propose the smallest viable change that keeps the intended fantasy.
   - If the answer needs tuning numbers, provide ranges and the lever that should move in testing.

## Project Heuristics

- Treat rarity as complexity, flexibility, or build identity, not automatic raw-power superiority.
- Treat entangled variants and pre-inscribed rewards as aspiration multipliers, not mandatory power gates.
- Ensure every session can advance at least one of:
  - chainling growth
  - ordinal breadth
  - modifier chase
  - roster strength
  - player mastery
- Keep early losses legible. A player should be able to say "I lost because of targeting, AP use, chemistry, speed control, or build mismatch," not "the system was opaque."
- Add a new currency or reward track only if it introduces a new decision, sink, or fantasy. Do not add currencies that only bloat the economy.
- Bias monetization and gacha toward breadth, cosmetics, convenience, or optional acceleration. Do not let premium access become required for PvP or long-term combat viability.
- Prefer build diversity through pool access, modifiers, AP dynamics, barriers, targeting, and species identity instead of raw stat inflation.

## Red Flags

- New randomness with no pity, duplicate conversion, or sink
- More reward faucets without a matching drain, cap, or long-term limiter
- Premium systems that shorten the path to competitive dominance
- Difficulty that comes from hidden formulas, timing ambiguity, or overloaded statuses
- Feature proposals that force wallet or blockchain friction before the player is already invested in the combat loop
- New chase layers added before the existing progression spine is clear

## Output Structure

Use this structure unless the user asks for something else:

1. Summary
2. Current-state fit
3. Economy and progression impact
4. Combat and meta impact
5. Risk flags
6. Recommendation
7. Next tests or questions

Prioritize concrete tradeoffs. If you recommend numbers, explain what success or failure would cause those numbers to move.
