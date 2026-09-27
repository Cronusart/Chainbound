# Pass A Modifier Catalog Notes

Balance source of truth: `Docs/Specs/Combat/Data/Modifiers.csv`.
Runtime: `Assets/Data/Combat/Modifiers/Modifier_*.asset` plus `ModifierCatalog.asset`.
Player-facing names: `ModifierDefinitionSO.GetDisplayName()` (`displayName`, fallback `sectionId`).
`sectionId` is the stable key and is never renamed.

This pass is in-repo catalog/resolver work only. It does not change Solana programs, collections, GameConfig, OwnershipConfig, Supabase, ownership feature flags, or MagicBlock VRF.

## Bench-slot definition

Empty **bench** slots are party roster capacity on the bench, not empty move slots.

- 2v2 format: 2 active field slots + **1 bench slot** (`CombatConstants.BENCH_SLOTS_PER_TEAM`).
- `TeamState.CountEmptyBenchSlots()` = `max(0, bench capacity − living Chainlings currently occupying the bench)`.
- A party of 2 (both active) has **1** empty bench slot.
- A party of 3 with a living benched Chainling has **0**.
- A KO'd bench occupant does not occupy the slot.

Sparse Focus (`2.7-a`) and Sparse Ward (`6.8-e`) scale by that count.

## Existing catalog display names

Every pre-Pass A CSV row now has `DisplayName`. Names follow the supplied map. CSV-only rows that were not in the map (assets were missing; CSV led):

| SectionId | DisplayName | Derived from |
|-----------|-------------|--------------|
| 6.1-e | Steady Leech | Mid-tier on-hit HP drain |
| 6.1-f | Heavy Leech | High-tier on-hit HP drain |
| 6.7-a | Afterguard | 1-hit post-cast barrier |
| 6.7-b | Double Guard | 2-hit post-cast barrier |
| 6.7-c | Threshold Guard | Damage-threshold post-cast barrier |
| 8.1-a | Spare Heart | +5-8% MaxHP |
| 8.1-b | Stout Frame | +8-12% MaxHP |
| 8.1-c | Iron Hide | +12-18% MaxHP |
| 8.1-d | Oak Frame | +18-25% MaxHP |
| 8.1-e | Titan Heart | +25-33% MaxHP |

Volatile `10.1-c` stays battle-time ± power. Stamp lock still only locks amplitude; it is not flattened.

## Pass A rows

| SectionId | DisplayName | Category | MinRarity | EffectMin–Max | AllowedCategories | Scope / Trigger | Forbidden / Slot |
|-----------|-------------|----------|-----------|---------------|-------------------|-----------------|------------------|
| 1.3-a | First Dibs | INIT | Uncommon | 5–12 | StatusUtility\|Support\|Passive | Active / None | INIT slot; PRIOR tag; forbids 1.1-a\|1.2-a\|1.3-b\|4.3-a\|4.3-b |
| 1.3-b | Quiet Priority | INIT | Common | 5–8 | StatusUtility | Active / None | INIT slot; PRIOR tag; forbids sibling INIT |
| 2.7-a | Sparse Focus | POT | Common | 3–8 | *(empty, same as 2.2 accuracy)* | Active / None | — |
| 4.8-a | Ignore Walls | AUG | Epic | 0–0 | DamagingMelee\|DamagingRanged | Active / None | forbids 4.3-b\|4.6-a |
| 6.8-a | Patch Kit | TRG | Uncommon | 5–12 | StatusUtility\|Support | Active / None (on use) | — |
| 6.8-b | Pre-Emptive | TRG | Rare | 5–12 | StatusUtility\|Support\|Passive | Active / OnBattleStart | forbids 6.8-c |
| 6.8-c | Riposte | TRG | Rare | 5–12 | StatusUtility\|Support\|Passive | Active / OnIncomingHit | forbids 6.8-b |
| 6.8-d | Sticky Mirror | TRG | Uncommon | 5–15 | Passive | Passive / None | — |
| 6.8-e | Sparse Ward | TRG | Uncommon | 3–8 | StatusUtility\|Support\|Passive | Active / None | Passive hosts apply at battle start |
| 6.8-f | Cleanse Soft | TRG | Uncommon | 5–15 | StatusUtility\|Support\|Passive | Active / None | Passive hosts apply at battle start |
| 6.8-g | Spotlight | TRG | Uncommon | 5–12 | Support\|StatusUtility | Active / None (on use) | — |
| 7.3-a | Shared Buff | SYN | Epic | 0–0 | StatusUtility\|Support | Active / None | forbids 7.1-d |

`{n}` in Pass A `EffectDescription` is the locked stamp amplitude. Candy Machine reveal substitutes `{n}` / `{value}` / authored numeric ranges.

## Resolver hooks

Wired (minimum safe hooks on existing pipeline points):

| ID | Hook |
|----|------|
| 1.3-a / 1.3-b | `ResolvePriorityForAction` — same % chance path as 1.1-a |
| 2.7-a | `BuildDamageModifierContext` accuracy bonus × empty bench slots |
| 4.8-a | First-hit barrier bypass (`barrierBypassChance = 1`), same family as 4.3-b |
| 6.8-a | On-use self heal % MaxHP in `ApplyUtilityEffects` |
| 6.8-b | Battle-start chance to `ApplyUtilityEffects` for the attached Util/Sup/Pas move (no AP). Damaging hosts fail closed. |
| 6.8-c | After confirmed incoming damage, same auto-cast path as Pre-Emptive. Re-entrancy guarded. |
| 6.8-d | Extra status-mirror chance after armed IncomingDebuff windows miss; boosts existing barrier reflect damage |
| 6.8-e | 1-turn mitigation × empty bench slots on use; Passive hosts at battle start |
| 6.8-f | Chance to cleanse 1 General self debuff on use; Passive hosts at battle start |
| 6.8-g | On use, partner +accuracy for 1 turn |
| 7.3-a | Copies the move's self buffs / self stage changes to the active partner. Also covers previously unwired 7.1-d. |

Deferred / limited:

- Pre-Emptive and Riposte do **not** fully re-resolve damaging moves. Illegal damaging hosts are rejected by `AllowedCategories`; if a damaging move is attached anyway, auto-cast fails closed.
- Auto-cast targeting is a simple Self / ally / first-foe pick, not a second player targeting prompt.
- Sticky Mirror does not invent barrier reflect from zero; it boosts reflect that already exists and adds a status-mirror chance.
- Shared Buff copies the **move's** self-targeted buffs, not every buff already on the user.
- Stamp `{n}` substitution outside Candy Machine reveal remains a stamp-pipeline concern.

## Validation

`ModifierValidationService.CanApply` already fail-closes on `AllowedCategories`, `ForbiddenCombos`, and `SlotType`. Pass A Util/Sup/Pas mods therefore cannot stamp onto illegal hosts (no power-style mods on pure Passive, no Ignore Walls on utility, Quiet Priority is StatusUtility-only, Sticky Mirror is Passive-only).

EditMode coverage: `StampModifierPipelineTests.PassA_IllegalHosts_AreRejected` and `PassAModifierResolverTests`.

## Import

After CSV edits, run `Tools > Chainbound > Combat > Data Importer > Import Modifiers (CSV)`. The importer maps `DisplayName` by header name.
