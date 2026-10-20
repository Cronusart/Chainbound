# Self-damage shelf

**Status:** shelved for a future patch (decision by Cronus, 2026-10-18). No move ships with HP self-damage in this pass.
**Scope:** design record plus implementation ledger. The replacement effects described below landed in combat balance follow-up v1; HP self-damage remains deferred.
**Related:** `Docs/Combat/Audits/AP_COST_LADDER_IMPLEMENTATION_AUDIT.md` §8C, `Docs/Combat/Design/MOVE_COST_CURVE_ANALYSIS.md` (the "double tax" note), `Docs/Combat/COMBAT_ISSUES_MASTER.md` (`MV-all_in`, `BAL-COST`), `Docs/Specs/Combat/COMBAT_TypeProfiles.md`.

## 1. Why it is shelved

A move that hurts its user can KO its user. If that KO wipes the user's team while the same action KOs the last foe, both sides are down at once. Cronus's rule is that **boss battles must never end in a draw**, so self-damage needs a defined tiebreak before any self-damage move can ship.

What already exists in the engine (verified in the working tree on 2026-10-18):

| Piece | Where | State |
|---|---|---|
| Draw outcome | `CombatResolver.CheckBattleEnd` (`CombatResolver.cs:7365-7377`) sets `winnerTeamIndex = -2` and logs `CombatEventType.Draw` when both teams are defeated | **Exists.** `BattleSnapshots.IsDraw` (`:406`), `BattleOutcomePresentation.cs:95`, `CombatBattleController.cs:2477`, `StandardBattleRewardCalculator.cs:43`, `BattleDefeatRecoveryUtility.cs:39`, and `SupabaseBattleOutcomePersistence.cs:91` (`is_draw`) all handle it |
| Self-inflicted damage that already ships | FOMO self-DoT, 1/16 MaxHP per turn (`StatusProcessor.cs:120`, `CombatConstants.FOMO_SELF_DOT_HP`); reflected barrier damage to the attacker (Lattice Guard 20%, `CombatResolver.cs` ~2180-2200, `HandleReflectedAttackerKo` `:2474`) | **Exists.** A mutual wipe, and so a draw, is already reachable today, boss battles included |
| Boss "never draw" rule | none | **Missing.** `CheckBattleEnd` does not look at encounter difficulty |
| HP cost on use | `HpCostPercent` column, imported and round-tripped; paid only by `FULL_ABSORB_WALL` support moves, floored at 1 HP (`CombatResolver.cs:2625-2636`, `CombatActionEligibilityService.cs:290-296`) | Partial |
| Recoil event | `CombatEventType.HPRecoilDamage`, consumed by narration, presenter, SFX, and stats | Consumers exist, **no producer** |
| Recoil opcode | none | Missing |

So the Draw outcome itself is not new work. The new work is the **boss tiebreak**. It is needed even without self-damage, because FOMO and reflect can already cause a mutual wipe. The follow-up plan carries it as item P2.6.

## 2. What was replaced

Every move whose text promised self-inflicted HP loss, checked against `Moves.csv` at HEAD and in the working tree:

| MoveId (kept) | Old display name | Type / rarity | Old text (HEAD) | Text now (working tree) | Becomes (recommended pick) |
|---|---|---|---|---|---|
| `recoil_strike` | Recoil Strike | Forked, Uncommon, 4 AP, 76 power | "76-power contact hit; take 15% MaxHP recoil." | "76-power contact hit." | **Hard Fork**: heavy contact hit, then cleanse 1 General debuff from yourself (`Cleanse:1:100%:Self:hard_fork`). Power 72, 4 AP, Uncommon |
| `volatile_shot` | Volatile Shot | Gas, Uncommon, 4 AP, 72 power | "High-power ranged hit; miss costs you 15% MaxHP." | "High-power ranged hit." | **Gas War**: heavy ranged hit; your next move costs +1 AP (`NextMoveAPSurcharge:1:100%:Self:gas_war`). Power 72, 4 AP, Uncommon |
| `glitch_barrage` | Glitch Barrage | Glitched, Mythic, 3 AP, 16×2–4 | "2-4 hits; each randomly hits target or self." | unchanged; the effect never ran | **Bit Rot**: 2–4 hits; each hit has 25% to lower a random target stat by 1 (`RandomStageChange`, `AfterEachConfirmedHit`). Power 18 |
| `risk_spike` | Risk Spike | Forked, Mythic, 1 AP utility | "Gain Atk +2 and Def -1; next hit +25% damage and +10% recoil." | unchanged; only the stages run | **Leverage Up**: gain Atk +2 and Def −1, plus Surging (your next move +25% power, existing status 306) |
| `all_in` | All-In (name kept) | Meme, Epic, 4+ AP, 98 power | "Spend all AP for huge contact hit; recoil on hit or miss; +1 AP on KO." | "Spend at least 4 AP. Any AP left is spent too. Skip your next action." | **All-In** keeps spend-all and power 98. New text: "Spends all your AP (min 4). Can't use All-In again next turn." Recoil and the KO refund stay shelved |
| status `Overleveraged` (208), used by `voltage_rush` (self, 25%) and `risk_marker` (foe) | Overleveraged | General debuff | "Next heavy hit causes recoil" | unchanged; no engine logic | Payloads move to `NextMoveAPSurcharge:1` (Voltage Rush 25% Self; Risk Marker SingleEnemy). The status id stays reserved (append-only); its summary becomes "Shelved: self-damage" |
| `guard_stance` (open question) | Guard Stance | Block, Common | "Gain Def +2; melee contact takes +10% recoil for 3 turns." | unchanged; the recoil never ran | This is thorns on the attacker, not self-damage. It can still KO an attacker during its own action, which is the same mutual-wipe case. Recommended: delete the clause and keep Def +2 (plan Q5) |

**Renames keep the MoveId by default.** Ids are stored in `Move_*.asset` file names, `SpeciesMovePools.csv`, `Supabase/functions/_shared/move_ap.ts`, the generated card catalog (`ownership_card_catalog.generated.ts`), the battle-service manifest, and player ownership records. Changing ids needs a migration (plan Q3). Every new display name was checked: it does not collide with any MoveId, move display name, Modifiers.csv section id or name, Statuses.csv id or name, `StatusId` enum value, or species asset name.

Second options, in case Cronus prefers them:

| Move | Second option | Engine need |
|---|---|---|
| Recoil Strike | **Branch Ram**: heavy contact hit, then your Speed −1 (`StageChanges Speed:-1:1.0:true`), a tempo trade-off on a fast type | none |
| Volatile Shot | **Slippage Shot**: heavy ranged hit; if it misses, you lose 2 AP | small: an on-miss utility timing (`ResolveUtilityTiming=OnMiss`) |
| Glitch Barrage | **Race Condition**: 2–4 hits, and each hit re-rolls a random living foe as its target | medium: per-hit retarget inside the hit loop (chemistry, ledger, and narration per target) |
| Risk Spike | **Leveraged Long**: gain Atk +2, Def −1, Speed +1 | none, but it sits close to Pivot Line and Voltage Rush (Forked speed buffs) |

## 3. Engine work for a future self-damage patch

Estimates from audit §8C. They assume one engineer who knows `CombatResolver`.

| Piece | (i) Recoil as % of damage dealt | (ii) Flat %MaxHP on use / on miss |
|---|---|---|
| Data | New `UtilityEffectType.RecoilPercent` (append), e.g. `RecoilPercent:15%:<id>` with `AfterEachConfirmedHit` | Reuse `HpCostPercent`; add tag `HP_COST_ON_USE` or `HP_COST_ON_MISS` instead of relaxing the `FULL_ABSORB_WALL` gate |
| Engine | Executor case + `ApplyRecoil`, mirroring `ApplyLifesteal` (`UtilityEffectExecutor.cs:137`), ≈25 lines. Misses and fully blocked hits deal 0, so they cause no recoil | Pay before the hit loop (on use), or in the miss branch (`CombatResolver.cs` ~2040), ≈30 lines |
| KO-on-self | Floor at 1 HP (the old All-In design and the barrier-cost precedent): trivial. Lethal: generalise `HandleReflectedAttackerKo` into a self-KO helper, about +0.5 day | Same decision, plus eligibility: block the move when HP ≤ cost (barrier rule), or allow it and floor at 1 |
| Draw / boss | Uses the boss tiebreak from plan P2.6, which must land first. Non-boss mutual wipe stays a Draw | same |
| Narration | Emit `HPRecoilDamage` (consumers already exist): "X took N recoil." | Same event instead of today's popup-only `SystemMessage` |
| AI | Subtract expected self-damage in scoring. Skip the move if the recoil would KO (when lethal) or leave the user under ~25% HP | Same hook, using cost × (1 − accuracy) for on-miss |
| Tests | Recoil = floor(dmg × pct) per hit, summed for multi-hit; miss or blocked hit → 0; floor or KO path; boss tiebreak; event; importer round-trip | Pays on hit and on miss; on-miss pays only on a miss; floor and eligibility; event; round-trip |
| Size | ≈1 day (floor at 1) / ≈1.5 days (lethal) | ≈0.5–1 day; shares the KO rule and AI hook with (i) |

Both together come to about 1.5–2 days once P2.6 has landed. All-In's "+1 AP on KO" is separate, ≈0.5 day: it needs a KO-aware condition or a ledger lookup.

## 4. Which types should own self-damage

From `COMBAT_TypeProfiles.md` (summary table, Reaction Role Rules) and the older type-profile plans:

| Type | Profile evidence | Recommendation |
|---|---|---|
| **Gas** | "AP tempo disruptor. Taxes enemy AP, spikes Atk/Def trade." Keywords: "APMAN, **volatile self-trade DMG**, Bridging." | **Primary owner.** A flat HP cost or an on-miss cost is the "self-trade" the profile names. Price it in power, not AP |
| **Meme** | "Universal wildcard… Random, hype effects, **all-in plays**." Outside the chemistry pass | **Secondary owner**, one or two cards at most: All-In-style recoil on a big swing. Keep it rare and loud |
| **Glitched** | "Chaos exploiter… Random effects, exploit setups." | **Optional, Mythic only:** a *random* self-hit as chaos (the original Glitch Barrage idea). Never a flat, predictable cost |
| Mined | FOMO's self-DoT is Mined's existing self-cost via chemistry ("Ramp attacker") | Already has a self-cost through FOMO. Do not add move self-damage on top |
| **Forked** | "Chain splits propagate pressure… **without recoil stacking**." Reaction rules: "**Splinter replaces Forked's legacy recoil stacking**." | **Not an owner.** The profile removed recoil from Forked on purpose. This is why Recoil Strike becomes Hard Fork |
| Block, Staked, Compressed, Node, Encrypted, Frozen, Bot, Burned, Shard | Wall, sustain, efficiency, control, or aggression roles; none mention self-trade | Not owners |

## 5. Rules for future self-damage moves

1. **Make new moves.** Use new MoveIds and new display names. Do not revive `recoil_strike` or `volatile_shot` as self-damage cards, and do not reuse the names "Recoil Strike" or "Volatile Shot" while Hard Fork and Gas War are live, so players don't conflate them.
2. **Stay clear of the substitutes.** A new self-damage move must differ from Hard Fork, Gas War, Bit Rot, and Leverage Up in at least two of: AP cost, shape (single / AoE / multi-hit), contact type, primary rider. Within the same type, its neutral damage index must be at least 15% away from the substitute's at the same AP.
3. **Charge once.** Pay for self-damage with power: expected self-damage in index points is roughly the power premium over the type's band. Do not also raise AP. That is the "double tax" in `MOVE_COST_CURVE_ANALYSIS.md`.
4. **Bosses never draw.** No self-damage move ships before P2.6 (the boss tiebreak) has landed and has a test.
5. **Owner types only.** Gas first, Meme rarely, Glitched only as random Mythic chaos. Not Forked.
6. **Narrate it.** Self-damage must emit `HPRecoilDamage`, never a popup-only `SystemMessage`.
7. **Tell the AI.** Scoring must subtract the self-cost before the card enters any pool.
