# Walls, Barriers, and Mitigation

Source of truth: combat snapshot `chainbound_combat_snapshot_20261020_0215` (local tree, 2026-10-20 ~02:15 CDT). Facts below are about that snapshot. Every current-code or current-data claim is tagged with a check id. `walls_barriers_check.py` re-reads the unzipped snapshot and prints PASS or FAIL per id.

**Check tally: 220 PASS / 0 FAIL / 220 claims.**

Suggestions are labelled **PROPOSAL**. Nothing here changes learn-pool membership. The rarity-tier rule in `SpeciesMovePools.csv` stays in force: wild pools are five moves, three at the body tier or lower, one at that tier or one step higher, and a signature that is strictly the highest rarity. Starters keep a unique top-rarity signature. Ghost and Cofi Lootwak sit outside the rule [C-pool-rule].

---

## (a) Headline

| Bucket | Count | Operational |
| --- | --- | --- |
| Authored wall / barrier moves | 9 | 8 full, 1 partly (Blockade) |
| Percent `MitigationWindow` moves | 6 | Guard Posture, Barrier Wall, Blockade, Ash Guard, Ice Wall, Compound Guard |
| Full-absorb barriers | 2 | Vapor Barrier (1 hit), Ember Wall (3 hits + 1 AP drain) |
| Damage-threshold barriers | 1 | Lattice Guard (40 damage, 20% reflect) |
| Mitigation passives | 4 | 2 data-driven, 2 hardcoded with empty payloads |
| Chemistry Firewall | 1 | Works, and the presentation layer treats it as a wall |
| "Barrier" damage moves with no breaker opcode | 4 | Inert as breakers |
| Lookalikes that are not damage walls | 2 | Guard Stance (Def stages), Proof Shield (Encrypted Seal) |
| Wall / mitigation / pierce modifiers with resolver branches | 13 | Catalog row and code branch both present |

**Top findings**

1. Hit budget `-1` means unlimited, and that default landed on Blockade, Ash Guard, Compound Guard, and Ice Wall. Multi-hit does not break those four.
2. Ember Wall is the only wall that changes a fight: 2 AP, three full absorbs, 1 AP drained per hit, and it is Cinislug's signature.
3. Blockade's numbers work. Its stack key is `blockade`, so the wall UI, popup, and wall narration never see it. Its sound id is blank. Every wall sprite is unauthored.
4. A 3 AP wall cannot be cast on the first turn. Active chainlings gain 2 AP at turn start. Ice Wall then expires at the end of the turn it is cast.
5. Two different percent walls stack. The exclusive flag clears barriers only. `Ignore Walls` ignores barriers, not percent mitigation. DoT, statuses, and chemistry go through both.

**Top proposals** (detail in section i)

1. **PROPOSAL.** Make Blockade a real wall tag and give it a sound id.
2. **PROPOSAL.** One wall-tagged percent source per Chainling. A new one replaces the old one.
3. **PROPOSAL.** Ice Wall costs 2 AP. Rewrite the speed line so it names the ally who is holding the wall.
4. **PROPOSAL.** A full absorb still pays the holder's on-hit AP (Ice Wall).
5. **PROPOSAL.** Add a catalog-only Mined move that ignores percent mitigation and still spends barrier hits. Do not put it in a learn pool.

**Open questions** are in section (k). Each has a suggested default.

**Audit defaults that landed.** Blockade's utility is `MitigationWindow:25%:2:-1:blockade` [C-blockade-csv] [C-blockade-asset]. Ash Guard, Compound Guard, and Ice Wall also use hit budget `-1` [C-ash_guard-csv] [C-compound_guard-csv] [C-ice_wall-csv]. The unlimited-hit audit test is present [C-audit-test]. Ice Wall's description is quoted and both opcodes are on the asset [C-ice-quote] [C-ice_wall-asset]. Isolation Tool applies Anchored [C-isolation-anchored]. System Reset, Hype Burst, Purge Protocol, Crystal Extraction, and Bug Fix are built, and none of them strip walls [C-system-reset] [C-hype-burst] [C-purge] [C-crystal] [C-bug-fix] [C-strips-ignore-walls]. This round builds around `-1`. It does not revert it.

---

## (b) End to end

### Two layers

Percent windows live on `ChainlingInstance.mitigationSources`. Full absorbs and damage pools live on `ChainlingInstance.barriers`.

`hitsRemaining == -1` is documented as unlimited [C-neg1-doc]. `AddMitigationSource` rejects `0` [C-zero-reject]. An active check treats `0` as already spent [C-active-zero]. Consume decrements only when `hitsRemaining > 0` [C-consume-only-positive]. The damage calculator also treats `0` as inactive [C-calc-zero-inactive]. So `-1` is never "already broken" and never gets decremented. The same `!= 0` test on a single-hit barrier would also leave `-1` unlimited [C-barrier-neg1]. No current barrier move uses that.

A non-empty stack key refreshes the matching source instead of adding a second copy [C-stack-refresh]. Different keys append. Totals multiply: each source keeps `1 - magnitude` of the hit [C-multiplicative]. One source may be up to 100% because `MITIGATION_SINGLE_SOURCE_CAP` is `1.0` [C-cap-const]. Defense notes already call that cap landed and uncapped [C-defense-uncapped]. The spec still says 30% [C-spec-cap-30]. This round does not reopen the cap.

`EXCLUSIVE_BARRIERS_AND_WALLS` is true [C-exclusive-const]. The only clear it performs is `barriers.Clear()` inside `AddBarrier` [C-exclusive-barriers-only]. Nothing in the combat scripts calls `mitigationSources.Clear` [C-no-mitigation-clear]. Two percent walls therefore stack. A new barrier replaces the previous barrier. A percent wall and a barrier can both be up.

### CSV to runtime

The importer reads `MitigationWindow:magnitude:turnBudget:hitBudget:stackKey` and stores the hit budget unchanged [C-import-window]. `ApplyMitigationWindow` passes that hit budget through [C-window-passes-hitbudget]. `FullAbsorbBarrier:turnBudget:hitBudget:stackKey` rewrites hit budget `0` to `3`; `-1` would stay `-1` [C-import-full] [C-fullabsorb-zero-becomes-3]. `WallHitRetaliation` does nothing when the executor runs it [C-retaliation-noop]. Ember Wall still stores `apGain: 1`, and barrier install copies that drain onto the barrier [C-ember-apgain] [C-barrier-copies-drain]. `IncomingDamageMitigation` is also a no-op in the executor [C-incoming-noop-in-executor]. The resolver applies those passives later as extra mitigation [C-ledger-hardcode] [C-shattered-hardcode] [C-additional-mitigation].

`GainApOnDamageIfSpeedHigher` parses a hit budget and then ignores it. The window is turns only, `requireSpeedHigher: true`, and it is added to the **target** [C-reactive-ignores-hitbudget] [C-reactive-on-target]. The speed check compares the holder's Speed to the attacker's Speed [C-reactive-holder-speed].

### Damage order

At equal stats the calculator does power × (atk/def) × progression × random + 1, then chemistry, then crit, then percent mitigation, then the floor, then `minimumFinalDamage` [C-damage-order] [C-chem-before-mitigation] [C-crit-before-mitigation]. Constants: chemistry ×1.2 / ×0.8 [C-chem-factors], crit ×1.5 [C-crit-const], random 0.85–1.0 [C-random-const], progression factor 1 [C-prog-const]. STAB defaults off [C-stab-default-off]. This round does not decide STAB.

The floor is `power * (atk/def) * levelFactor * mitigationFloor` with `MITIGATION_FLOOR = 0.10` [C-floor-const] [C-floor-formula]. That is not 10% of the post-chemistry hit. The spec still says "10% of original calculated damage" [C-spec-floor-wording]. Blockade's `minimumFinalDamage` is 1 [C-min-final-default], so a percent wall cannot zero a hit. A full absorb happens after the calculator and can zero the hit.

`ignoreMitigation` exists on the resolve profile and no combat asset sets it [C-ignore-mitigation-flag] [C-ignore-mitigation-unused].

### When things tick

End of turn, for each **active living** chainling: passive end-of-turn triggers, then status ticks (including DoT), then mitigation timers, then accuracy, reactive-AP windows, barrier timers, and armed windows [C-eot-order] [C-eot-active-only]. The bench ticks recharge only [C-bench-recharge-only]. A mitigation turn budget decrements on that end-of-turn tick [C-tick-decrements].

So `turnBudget` 1 protects the rest of the granting turn and expires when that turn ends. `turnBudget` 2 also covers the next full turn. Modifier "rest of this turn" uses duration 1 [C-rest-of-turn-is-1], so those modifiers die on the same tick.

The design rule says move, modifier, and passive timers resolve before chemistry and status ticks. The code ticks walls **after** the status processor. DoT never consults walls [C-dot-direct-hp], so the late tick does not change burn damage. It is still the wrong order.

### Swap, bench, KO

`ResolveSwap` sets the outgoing chainling inactive and does not clear mitigation or barriers [C-swap-keeps-walls]. Nothing else clears mitigation sources [C-no-mitigation-clear]. Benched walls do not tick. A 2-turn wall can be frozen on the bench and brought back later. The visual plan's north star is one lightweight wall on the slot [C-visual-slot]. The data is one wall list on the Chainling.

### Full absorb versus percent walls

On a full absorb the resolver logs the block, can still apply statuses, subtracts reflected HP directly, and `continue`s [C-statuses-on-block] [C-reflect-raw] [C-fullblock-continue]. That continue happens before hit-budget consume and before reactive AP [C-fullblock-skips-consume-and-reactive]. A barrier that eats the hit does not spend a percent wall's hit budget and does not grant Ice Wall's AP. Reactive AP also requires the holder to still have HP [C-reactive-hp-guard].

Reflected damage is a raw HP subtract. It does not pass the mitigation floor and it does not hit the attacker's own wall [C-reflect-raw]. That is opponent damage from the wall, not the shelved self-damage rule.

### Presentation

A mitigation source counts as a wall only when its tag or stack key contains the substring `wall` or `barrier` [C-wall-tag]. Popups use that test [C-popup-requires-wall-tag]. A negative hit budget formats as `BLOCK · ∞` [C-popup-infinite]. Narration has a blocked-wall line and skips bypasses [C-narr-blocked] [C-narr-skip-bypass].

Blockade's id and stack key are both `blockade` [C-blockade-not-wall-tag] [C-blockade-asset]. It mitigates and never narrates as a wall. Its `wallSfxId` is empty. The other eight wall moves have sound ids [C-blockade-sfx-empty]. All nine have `wallSprite` fileID 0 [C-wall-sprites-empty].

Chemistry Firewall uses stack key `Chemistry.Firewall` [C-firewall-rearm]. `HasActiveWallState` treats a mitigation source as a wall when that tag matches [C-has-wall-uses-tag] [C-wall-tag], so a spent Firewall charge can narrate as the wall breaking. `firewall_passive` does not call `AddMitigationSource`; it is extra mitigation, so it does not take the wall tag even though the key contains `wall` [C-firewall-passive-csv] [C-incoming-noop-in-executor].

### Dead or silent paths

- `WallHitRetaliation` and `IncomingDamageMitigation` do nothing in the executor [C-retaliation-noop] [C-incoming-noop-in-executor].
- Ice Wall's reactive hit budget is parsed and ignored [C-reactive-ignores-hitbudget].
- `CheckBarrierBypass` takes a chemistry result and never reads it [C-bypass-chem-unused]. The comment above it still says chemistry advantage is a bypass source [C-bypass-comment-stale].
- Four moves talk about battering barriers and ship with empty utility: Baseline Barrage, Baseline Multi Strike, Guardbreaker Combo, Refined Barrage [C-baseline_barrage-no-opcode] [C-baseline_multi_strike-no-opcode] [C-guardbreaker_combo-no-opcode] [C-refined_barrage-no-opcode].
- Immutable Ledger and Shattered Armor have empty utility. Their behavior is hardcoded [C-ledger-asset-empty] [C-shattered-asset-empty] [C-ledger-hardcode] [C-shattered-hardcode].

---

## (c) Targeting matrix

Budgets live on the Chainling, not the team. A spread attack spends one confirmed hit against **each** defender's own wall. A miss logs and continues before absorption, so a miss spends nothing [C-miss-before-absorb]. A crit is applied before mitigation and does not break a wall by itself [C-crit-before-mitigation]. Chemistry is also before mitigation [C-chem-before-mitigation]. There is no Taunt status [C-no-taunt-status].

| Attack | Percent wall (`-1` or finite) | Full absorb | Threshold (Lattice, 40) | Anomaly |
| --- | --- | --- | --- | --- |
| Single target, hits | Percent off, then 1 consume if the budget is `> 0`. `-1` is not consumed [C-consume-only-positive]. | Hit becomes 0. Percent budget is not consumed. Statuses still land [C-fullblock-skips-consume-and-reactive] [C-statuses-on-block]. | Absorbs up to the pool. Reflects 20% of absorbed damage as raw HP [C-lattice_guard-csv] [C-reflect-raw]. | Full absorb skips percent consume and reactive AP. |
| Ally / self support cast | Guard Posture is Self. The other eight are SingleAlly [C-guard_posture-csv] [C-blockade-csv]. | Same. | Same. | Isolation blocks ally support, including these walls, because the category is Support (or the move heals). It does not name the wall opcodes [C-isolation-support] [C-isolation-omits-wall-opcodes]. Self Guard Posture is not that check. |
| Spread | Each defender uses their own lists. | Each defender. | Each defender. | No shared budget. Anchored collapses a spread to one target [C-anchored-collapses-spread]. Isolation Tool is what applies Anchored [C-isolation-anchored] [C-status-anchored]. |
| Multi-hit | One consume per confirmed hit that is not fully absorbed. `-1` ignores the extra hits. | Each hit spends one barrier hit. Hit 4 breaks Ember Wall. | Many small hits are worse at breaking a damage pool than one big hit. | Spec still says multi-hit is the main counter [C-spec-multihit-counter]. That is false for the four `-1` walls. The four "barrage" moves have no breaker opcode. |
| Miss | No spend [C-miss-before-absorb]. | No spend. | No spend. | None. |
| Crit | ×1.5 before the percent [C-crit-const] [C-crit-before-mitigation]. | Still a full absorb. | A crit fills the 40 pool faster. | Crit is not a wall breaker by itself. |
| Chemistry ×1.2 / ×0.8 | Applied before the percent [C-chem-factors] [C-chem-before-mitigation]. | Absorb is after chemistry, so the wall eats the boosted hit. | Same. | Chemistry does **not** bypass. The bypass method ignores its chemistry argument [C-bypass-chem-unused]. |
| DoT (Kindled, Rugged) | Ignored. HP drops directly [C-dot-direct-hp]. On 168 HP both ticks are 10 [C-dot-168] [C-kindled-6]. | Ignored. | Ignored. | Burned's damage answer does not care that a wall is up. No narration says so. |
| Status on the hit | Lands even when the barrier then absorbs all HP [C-statuses-on-block]. | Same. | Same. | A wall is not a shield against status. |
| Redirect | Damage Intercept sends the next single-target hit to the interceptor, then that body's wall applies [C-redirect] [C-status-intercept]. | Same. | Same. | No taunt. |
| Bypass (Relay, Unsealed, Wallbreaker, Ignore Walls, Slipstream) | Percent mitigation already happened. Bypass only skips `ApplyBarrierAbsorption` [C-bypass-relay] [C-bypass-unsealed] [C-ignore-walls-is-barrier-bypass]. | Skipped. | Skipped. Bleedthrough lets a percent of the hit leak through a barrier [C-mod-code-pierce] [C-mod-4.7-a]. | `Ignore Walls` does not ignore percent walls. |
| Friendly reflect | Not a percent effect. | Reflected HP can drop the attacker to 0 [C-reflect-raw]. | Lattice reflects 20% of absorbed damage the same way. | Do not add self-KO. This is already opponent damage. |
| AI | `EstimateDamage` is power × (atk/100) × hits × accuracy. No defense, no mitigation, no barrier [C-ai-estimate-ignores-wall]. | Same. | Same. | Defensive AI casts the first legal support move when any ally is under 40% HP. It does not pick the ally who will be hit and it does not read walls [C-defensive-40] [C-support-first-legal] [C-ai-support-any]. |

---

## (d) Inventory

Live pool means `SpeciesMovePools.csv`. `known_pool.generated.ts` lists none of these [C-known-absent-guard_posture] and the matching `C-known-absent-*` ids. `move_ap.ts` matches the CSV costs [C-ap-guard_posture] and the matching `C-ap-*` ids. Block's profile job is barriers, defense ramp, and grounding [C-type-block].

| Id | Name | Type | Rarity | AP | Window | Hits | Magnitude | Riders | CSV | Pool | State |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `guard_posture` | Guard Posture | None | Common | 1 | 2 turns | 1 | 25% | none | 36 | none [C-pool-absent-guard_posture] | Full. Self, StatusUtility [C-guard_posture-csv] [C-guard_posture-asset] |
| `barrier_wall` | Barrier Wall | None | Uncommon | 3 | 2 turns | 2 | 25% | none | 42 | Humpacked [C-pool-barrier_wall] | Full. Typeless, so it should lose to a typed wall. It does [C-barrier_wall-csv] |
| `vapor_barrier` | Vapor Barrier | Gas | Uncommon | 2 | 2 turns | 1 full | absorb | none | 78 | none [C-pool-absent-vapor_barrier] | Full. One-hit version under Ember [C-vapor_barrier-csv] |
| `blockade` | Blockade | Block | Uncommon | 3 | 2 turns | -1 | 25% | none | 83 | Blockwyrm [C-pool-blockade] | **Partly.** Math works. Invisible to wall UI. Empty sfx [C-blockade-csv] [C-blockade-not-wall-tag] [C-blockade-sfx-empty] |
| `ash_guard` | Ash Guard | Burned | Uncommon | 2 | 2 turns | -1 | 20% | heal 5% | 102 | none [C-pool-absent-ash_guard] | Full, and a type misfit. Cheaper than Blockade [C-ash_guard-csv] |
| `ember_wall` | Ember Wall | Burned | Legendary | 2 | 2 turns | 3 full | absorb | 1 AP drain per hit | 106 | Cinislug, and it is the flagship [C-pool-ember_wall] [C-cinislug-sig] | Full. The kept signature exception [C-ember_wall-csv] [C-ember-apgain] |
| `ice_wall` | Ice Wall | Frozen | Uncommon | 3 | 1 turn | -1 | 25% | +1 AP if the **holder** is faster | 111 | Gilarime [C-pool-ice_wall] | Full, and the text says "if you are faster" [C-ice-quote] [C-ice_wall-csv] [C-reactive-holder-speed] |
| `compound_guard` | Compound Guard | Staked | Uncommon | 3 | 2 turns | -1 | 20% | MeleeDef +1 on the ally | 140 | Rattlestake and Cofi Lootwak [C-pool-compound_guard] | Full. Cofi is outside the pool rule [C-pool-rule]. Staked is not the barrier type [C-compound_guard-csv] [C-compound-stage] |
| `lattice_guard` | Lattice Guard | Shard | Uncommon | 1 | 2 turns | pool | 40 damage, 20% reflect | reflect | 186 | none [C-pool-absent-lattice_guard] | Full. 1 AP rivals every percent wall on a medium hit [C-lattice_guard-csv] |

Passives and chemistry:

| Id | What it does | State |
| --- | --- | --- |
| `defensive_reflex` | Passive. 20% mitigation on any contact, plus Drain AP 1 on self at end of turn [C-reflex-csv] [C-reflex-asset]. Not in a pool [C-pool-absent-defensive_reflex]. | Data-driven. Not a wall tag. |
| `firewall_passive` | Passive. 25% projectile mitigation [C-firewall-passive-csv] [C-firewall-passive-asset]. Not in a pool [C-pool-absent-firewall_passive]. | Data-driven extra mitigation. The executor opcode is a no-op; the resolver reads the payload [C-incoming-noop-in-executor] [C-additional-mitigation]. |
| `immutable_ledger` | Passive, Block, Legendary. Text: first hit each turn deals 20% less and +1 AP. Utility empty [C-ledger-csv] [C-ledger-asset-empty]. | Partly. Hardcoded [C-ledger-hardcode]. Not in the candy pool [C-known-absent-immutable_ledger]. |
| `shattered_armor` | Passive, Shard, Rare. Utility empty [C-shattered-csv] [C-shattered-asset-empty]. Not in a pool [C-pool-absent-shattered_armor]. | Partly. Hardcoded on-hit mitigation [C-shattered-hardcode]. Source tag is not a wall tag. |
| Chemistry Firewall | Block attacking Burned grants Firewall on the attacker, status 126 [C-matrix-firewall-self] [C-matrix-firewall-adjacent] [C-firewall-const]. 30% projectile mitigation, 1 turn, 1 hit, rearmed with stack key `Chemistry.Firewall` [C-firewall-on-apply] [C-firewall-rearm]. | Works. Mis-classified as a move wall by the substring test [C-wall-tag]. |

Not damage walls:

- Guard Stance, row 86. Block, Common, 1 AP, `MeleeDef +2` on self, no wall opcode, not in a pool [C-guard-stance] [C-guard-stance-self] [C-pool-absent-guard_stance]. Positive stages use `(2 + stage) / 2` [C-stage-mult], so +2 doubles MeleeDef. That is a stronger melee cut than a 25% wall, at Common and 1 AP. **Rarity rival** if it ever enters a pool.
- Proof Shield, row 88. Encrypted Seal, no mitigation opcode, not in a pool [C-proof-shield] [C-pool-absent-proof_shield].
- `gecko_guard` is not in any species pool [C-pool-absent-gecko_guard].

Modifiers (CSV effect text and a resolver branch):

| Id | Rarity | Effect | Code |
| --- | --- | --- | --- |
| 7.1-b Shield Link | Rare | Ally gains 12–18% for the rest of this turn [C-mod-7.1-b] | [C-mod-code-shield] |
| 6.7-a Afterguard | Uncommon | After a hit, a 1-hit barrier for 2 turns [C-mod-6.7-a] | [C-mod-code-postcast] |
| 6.7-b Double Guard | Rare | 2-hit barrier for 3 turns [C-mod-6.7-b] | same post-cast path |
| 6.7-c Threshold Guard | Epic | Threshold barrier of 15–25% MaxHP for 2 turns [C-mod-6.7-c] | same |
| 6.3-c Grit Guard | Rare | Below 30% HP, 15–25% mitigation for the rest of this turn [C-mod-6.3-c] | [C-mod-code-grit] |
| 6.4-a Brace | Common | On swap in, 10–15% [C-mod-6.4-a] | [C-mod-code-swap] |
| 6.4-d Cover Ally | Uncommon | On swap in, an ally gains 8–12% [C-mod-6.4-d] | same |
| 6.4-g Fade Guard | Uncommon | On swap out, the replacement gains 5–10% [C-mod-6.4-g] | same |
| 6.8-e Sparse Ward | Uncommon | Mitigation per empty bench slot for the rest of this turn [C-mod-6.8-e] | [C-mod-code-sparse] |
| 4.3-b Wallbreaker | Legendary | First hit bypasses barriers [C-mod-4.3-b] | [C-ignore-walls-is-barrier-bypass] |
| 4.6-a Slipstream | Rare | 20–35% chance to bypass a barrier on the first hit [C-mod-4.6-a] | same bypass chance |
| 4.7-a Bleedthrough | Epic | 20–35% of damage penetrates barrier absorption [C-mod-4.7-a] | [C-mod-code-pierce] |
| 4.8-a Ignore Walls | Epic | First hit ignores barriers [C-mod-4.8-a] | Sets barrier bypass to 1. It does not ignore percent mitigation [C-ignore-walls-is-barrier-bypass] |

Shield Link's source tag is `Modifier.` plus the section id [C-shield-tag]. That string is not a wall tag [C-wall-tag], so the mitigation is silent in the wall UI. Brace, Grit Guard, and Sparse Ward are the same kind of untagged percent.

**Misfits and rivals**

- Blockade is the typed Uncommon wall and is strictly better than typeless Barrier Wall (same 25%, same 3 AP, unlimited hits versus 2). That is the typed-beats-typeless rule working. Leave Barrier Wall on Humpacked.
- Ash Guard is Uncommon Burned, 2 AP, unlimited 20%, and a 5% heal. It is cheaper than Blockade and it is not a Block move. It must not enter Cinislug beside Ember Wall.
- Lattice Guard at 1 AP absorbs a normal ~28 hit outright and reflects. That rivals Rare and Legendary walls on medium hits. It is not in a pool yet.
- Guard Stance at 1 AP Common doubles MeleeDef. It rivals every percent wall if it is ever learned.
- Compound Guard's percent is fine for an Uncommon. The type profile for barriers is Block, not Staked. The Def rider is the Staked part. Acceptable only because it is already on Rattlestake and the boss.
- Vapor Barrier is not a rival of Ember Wall. One hit versus three hits plus AP drain.

---

## (e) Riders and strips

Compound Guard's fourth stage field is `targetsSelf`, not a duration [C-stage-field-is-self]. The asset targets the ally [C-compound-not-self] [C-stage-target]. Def +1 is ×1.5 [C-stage-mult]. Expiring the wall does not revert stages [C-tick-no-stages]. Cipher Lock blocks that positive stage [C-cipher-blocks-stage] and does not know about mitigation or barriers [C-cipher-not-walls]. The percent wall still lands under Cipher Lock. The Def does not.

Ash Guard's 5% heal is a normal heal column, not a wall rider that depends on being hit [C-ash_guard-csv].

Ember Wall's 1 AP drain is copied onto the barrier at cast [C-barrier-copies-drain] [C-ember-apgain]. The retaliation opcode itself is a no-op [C-retaliation-noop].

Ice Wall's +1 AP window sits on the ally, checks the ally's Speed, and does not spend a hit budget [C-reactive-on-target] [C-reactive-holder-speed] [C-reactive-ignores-hitbudget]. The description says "If you are faster" [C-ice-quote].

Lattice reflect is raw attacker HP [C-reflect-raw]. Spec 10.1's "reflect after the mitigation floor" does not match that subtract.

Statuses land through a full absorb [C-statuses-on-block]. Chemistry still multiplies before the percent [C-chem-before-mitigation]. Firewall is a real chemistry result on Block versus Burned and is applied to the attacker [C-matrix-firewall-adjacent] [C-firewall-on-apply].

Strips and cleanses touch general statuses only [C-strips-ignore-walls]:

- System Reset is `StripAllStatuses` [C-system-reset].
- Hype Burst is `StripBuffsThenStage` [C-hype-burst].
- Purge Protocol, Crystal Extraction, and Bug Fix are `Cleanse` [C-purge] [C-crystal] [C-bug-fix].

They do not remove percent walls, barriers, or chemistry Firewall. That is consistent with "a wall is not a buff." Cipher Lock is the inconsistent half: it blocks Compound Guard's Def stage and leaves the wall.

---

## (f) Balance

Examples use equal stats, random 0.925, no crit, chemistry ×1, progression 1. A power-30 hit is 28, and 25% mitigation makes it 21 (7 saved) [C-dmg-30-at-25]. Power 35 is 33 → 25 [C-dmg-35-at-25]. Power 40 is 38 → 28 [C-dmg-40-at-25]. Power 26 (Pulse Shot's kept power) is 25 → 18 [C-dmg-26-at-25]. One hit of a power-8 multi-hit is 8 → 6 [C-dmg-8-at-25]. Four of those save 8 and all four land on a `-1` wall. A 20% wall turns 28 into 23 (5 saved) [C-dmg-30-at-20].

Blockwyrm is 168 HP, MeleeDef 86, Speed 24 [C-stat-blockwyrm]. Seven saved HP is about 4% of that body. Kindled and the first Rugged tick are each 10 on 168 HP, and both ignore the wall [C-dot-168].

`currentAP` starts unset and the factory does not assign it [C-start-ap-zero]. Turn start grants 2 AP to active living chainlings only [C-ap-per-turn] [C-turn-start-ap] [C-ap-active-only]. A 3 AP wall cannot be cast on turn 1. If they spend the 2 AP, turn 2 is also 2 AP. They have to bank.

**Blockade on Blockwyrm, versus a faster attacker.** Crackett is Speed 92 [C-stat-crackett]. Crackett acts first. Turn 1 Blockwyrm has 2 AP and Blockade costs 3, so the wall is not up. A power-30 hit lands for ~28 into 168 HP. Later, if Blockwyrm banks and casts, Blockade's 2-turn budget still starts after Crackett has already moved that turn. The useful coverage is the next turn: about 7 HP on one equal-stat power-30 hit, for 3 AP and the action. Unlimited hits do not make this big. The body is too large and the percent is too small.

**Ice Wall on Gilarime.** Gilarime is Speed 49, 161 HP [C-stat-gilarime]. Ice Wall costs 3 and the budget is 1 turn [C-ice_wall-asset]. It cannot be cast on turn 1. When it is cast, the end-of-turn tick clears it [C-tick-decrements]. If Gilarime is slower than the foe, the foe already hit, and the wall is gone before the next round. The AP rider pays the holder only when the holder is faster than the attacker [C-reactive-holder-speed], which is the opposite of the slow-tank case. The card text still says "if you are faster" [C-ice-quote].

**Ember Wall on Cinislug.** Cinislug is Speed 28, 134 HP, and the flagship is Ember Wall at 2 AP [C-stat-cinislug] [C-cinislug-sig] [C-ember_wall-csv]. Turn 1 the foe hits first, then Cinislug can spend both AP and raise the wall. The next hits are fully absorbed, up to three, and each drains 1 AP [C-ember-apgain]. One power-30 hit saves ~28 HP instead of ~7, and taxes AP. A 4-hit move breaks it on hit 4. This is the combat-defining wall. The `-1` percent walls are not.

**Compound Guard.** The ally also gets Def +1, which turns MeleeDef 86 into 129 [C-dmg-compound-def]. A power-30 hit into that def is 19, then 15 after 20% mitigation, versus 28 with neither. The stage stays after the wall expires [C-tick-no-stages]. The lasting value is the Def, not the 5 HP the percent saves. Cipher Lock deletes the Def and leaves the weak percent [C-cipher-blocks-stage] [C-cipher-not-walls].

**Two percent walls.** 25% and 20% multiply to 40% (`1 - 0.75 × 0.80`) [C-multiplicative]. Both can be unlimited. That is a larger cut than either card is priced for, and nothing in the kit is a clean answer except a barrier bypass that does not even touch the percent.

**Lattice Guard at 1 AP.** A 28-damage hit fits inside the 40 pool, so the hit is absorbed and 20% of the absorbed damage is reflected with no floor [C-lattice_guard-csv] [C-reflect-raw]. One AP for a full negate of a common hit is underpriced next to 2 AP and 3 AP walls.

---

## (g) What more could be built

**PROPOSAL. A Mined percent-wall breaker.** Mined crashes tanks. Bot stays the AP-denial type, and multi-hit is no longer a counter to `-1`. Add one catalog move, suggested name Core Breach: Mined, Uncommon, single-target melee, power 32, 3 AP, `ignoreMitigation` on its own hits [C-ignore-mitigation-flag]. It still spends full-absorb hits and still fills a damage-threshold pool. It answers Blockade, Compound Guard, Ice Wall, and Ash Guard. It does not delete Ember Wall. It is not a raw upgrade over a 30–40 common, because 3 AP and power 32 lose to two cheaper hits when no wall is up. Do not add it to Minefolk or Kodiax. Both already have five learnable moves [C-minefolk-five] [C-kodiax-five] [C-pool-rule]. Do not make typeless Rapid Pummel or the empty barrages the best breaker. Those four barrages stay as damage moves until a real opcode is worth authoring. Prefer this new card over pretending their descriptions already pierce.

**PROPOSAL. Typed resists are not this round.** A wall that only blocks one contact or one type is a second system. Firewall already does the projectile slice as chemistry [C-firewall-const]. Keep move walls untyped until the percent-stack and the breaker exist.

**PROPOSAL. Reflect stays on Lattice and Ember's AP drain stays the thorns.** Do not add a second reflect wall. Do not let reflect KO through a self-damage rule. Lattice's reflect is already opponent HP [C-reflect-raw].

**PROPOSAL. Wall plus chemistry.** Block versus Burned already grants Firewall on the attacker [C-matrix-firewall-adjacent]. That is the combo. Do not also put Ash Guard on a Burned learn pool.

**PROPOSAL. AI.** Score a wall only if it will be up before the foe acts (caster Speed versus the threat, and the turn budget). Target the ally who will be hit. Do not recast over a wall-tagged source that is still active. Multiply `EstimateDamage` by `(1 - mitigation)` and treat one remaining full-absorb hit as 0 for that one hit [C-ai-estimate-ignores-wall].

**PROPOSAL. UI.** One wall-tagged percent source, a stack key that contains `wall`, a sound id, and a line when DoT ignores the wall. Per-move sprites are still empty [C-wall-sprites-empty]. Art can follow the tag fix. Do not wait on art to make Blockade visible.

**PROPOSAL. Empty cards.** Leave Vapor Barrier, Ash Guard, Lattice Guard, and Guard Posture in the catalog. Build Lattice's cost before any pool add. Do not delete them to tidy the list.

---

## (h) What works, what doesn't, what could be better

**Works.** `-1` is actually unlimited everywhere that consumes hits. Ember Wall is readable: three hits, then it breaks, and the attacker loses AP. Exclusive barriers stop two full absorbs. Strips do not eat walls. Typed Blockade beats typeless Barrier Wall on the numbers. DoT ignores walls, which keeps Burned from being blanked by a support click. Misses do not pop walls.

**Doesn't.** Blockade is invisible. Ice Wall and the other 3 AP walls are often dead tempo on slow bodies. Two percent walls stack into a flat tax with no hit counter. `Ignore Walls` does not ignore the walls players will actually see once Blockade is tagged, because those are percent walls. The spec's multi-hit counter, 30% cap, and 10% floor wording are stale. AI cannot see a wall. Walls freeze on the bench. Firewall chemistry can narrate as a move wall breaking. No wall has a sprite.

**Could be better.** One visible wall per Chainling, a 2 AP Ice Wall whose text matches the holder, reactive AP that still pays when a barrier eats the hit, and one Mined move that ignores percent and still respects Ember Wall. Tick wall timers before status ticks so the code matches the timer rule. The HP result barely changes. The order should still match.

---

## (i) Ranked recommendations

Learn-pool rule for every item below: **no pool membership changes.** No sixth move, no second step-up, no second signature [C-pool-rule]. Boss-only cards stay out of candy rolls. The new breaker is catalog-only.

1. **PROPOSAL. Blockade becomes visible.** Change the stack key from `blockade` to `wall_blockade` in `Moves.csv` row 83 and `Move_blockade.asset`. Set `wallSfxId` to `wall_blockade`. No number change. Reimport. **PROPOSAL** alongside it: match wall tags on a `wall_` / `barrier_` prefix, not on the substring `wall`, so `Chemistry.Firewall` stops counting [C-wall-tag] [C-firewall-rearm]. Test `Blockade_IsAWallForPresentation`: after the move resolves, `HasActiveWallState` is true and the popup path accepts the source.

2. **PROPOSAL. One wall-tagged percent source.** In `ChainlingInstance.AddMitigationSource`, if the new source's tag or stack key is a wall, remove other wall-tagged mitigation sources first. Passives, Shield Link, and Firewall chemistry stay stacked [C-additional-mitigation] [C-firewall-rearm]. Do not change `MITIGATION_SINGLE_SOURCE_CAP`. Test `TwoWalls_SecondReplacesFirst_PassivesStillStack`: Blockade 25% then Compound Guard 20% leaves 20%, and a 25% passive still multiplies.

3. **PROPOSAL. Ice Wall costs 2 AP.** `Moves.csv` row 111, `Move_ice_wall.asset`, and `move_ap.ts` [C-ap-ice_wall]. Keep 25%, turn budget 1, hit budget `-1`. Change the description from "If you are faster" to "If that ally is faster than the attacker" [C-ice-quote] [C-reactive-holder-speed]. Do not move the AP onto the caster. Gilarime already has the move [C-pool-ice_wall]. This is a cost and text change, not a pool change. Test `IceWall_Costs2_AndApWindowIsOnTheHolder`.

4. **PROPOSAL. Full absorb still grants reactive AP.** In the full-block branch of `CombatResolver`, call `GrantReactiveApOnDamage` before the `continue` [C-fullblock-continue] [C-fullblock-skips-consume-and-reactive]. Test `FullAbsorb_StillGrantsReactiveAp`: an ally faster than the attacker, Ice Wall up, Vapor Barrier also up, one hit, barrier absorbs it, holder gains 1 AP.

5. **PROPOSAL. Core Breach, catalog only.** New `Moves.csv` row. Mined, Uncommon, DamagingMelee, SingleEnemy, power 32, 3 AP, resolve profile `ignoreMitigation = true`. It does not bypass barriers and it does not set bleedthrough. No `SpeciesMovePools.csv` edit. No `known_pool` edit. Test `CoreBreach_IgnoresPercentMitigation_StillSpendsBarrierHits`: versus Blockade the hit equals the unmitigated number; versus Ember Wall the first hit is absorbed and the barrier's hit count drops by 1.

**Lower, still PROPOSAL**

6. Lattice Guard 1 AP → 2 AP in row 186, the asset, and `move_ap.ts`, before any pool add. Stay Uncommon Shard. Do not add it to a pool in this pass. Test `LatticeGuard_Costs2`.

7. Ash Guard stays out of Cinislug [C-cinislug-sig] [C-pool-absent-ash_guard]. If it remains Burned, do not give it a longer window than Blockade. Optional: turn budget 2 → 1 so 2 AP buys the shorter window. Test only if that number changes: `AshGuard_TurnBudgetIs1`.

8. Clear mitigation sources and barriers on swap-out and on KO. Do not tick them on the bench [C-bench-recharge-only] [C-swap-keeps-walls]. Test `SwapOut_ClearsWalls_BenchDoesNotTickThem`.

9. Tick mitigation and barrier timers before `_statusProcessor.ProcessEndOfTurn` [C-eot-order]. Test `WallTimers_TickBeforeStatusTicks`.

10. `Ignore Walls` (4.8-a) stays a barrier bypass [C-mod-4.8-a]. Do not let an Epic stamp erase Blockade. Delete the stale chemistry-bypass comment [C-bypass-comment-stale]. Test `IgnoreWalls_DoesNotZeroPercentMitigation`.

11. Narrate DoT that lands while a wall is up: the burn ignores the wall. Do not make DoT spend hit budget [C-dot-direct-hp].

12. AI changes in section (g). Test `DefensiveAI_CastsWallOnTheAllyWhoWillBeHit_AndDoesNotRecast`.

13. Guard Stance stays out of pools [C-pool-absent-guard_stance]. Do not remove the move. If a future pool wants it, it is a Common that halves melee damage and must be judged against Uncommon walls first.

This round does not retune the 10% floor, does not recap the single-source cap, and does not re-propose a Compressed soft wall. Barrier Wall already holds Humpacked's Uncommon slot [C-pool-barrier_wall].

---

## (j) Consistency flags

| Where | Says | Code or data |
| --- | --- | --- |
| `COMBAT_Resolution.txt` §8.4 [C-spec-cap-30] | No source above 30% | Cap is 1.0 [C-cap-const]. Defense notes already decided uncapped [C-defense-uncapped]. Spec is stale. |
| §8.2 [C-spec-floor-wording] | Floor is 10% of original calculated damage | Floor is 10% of power × (atk/def) × level factor [C-floor-formula]. |
| §8.3 [C-spec-guard-general] | Guard and barrier effects are general statuses | They are separate lists, so strips miss them [C-strips-ignore-walls]. |
| §6.2 [C-spec-multihit-counter] | Multi-hit is the primary wall counter | The four audit walls use `-1` [C-consume-only-positive] [C-audit-test]. |
| §10.1 reflect-after-floor | Reflect uses the floored hit | Reflect subtracts raw HP [C-reflect-raw]. |
| `CheckBarrierBypass` comment [C-bypass-comment-stale] | Chemistry advantage bypasses | The chemistry argument is unused [C-bypass-chem-unused]. |
| Ice Wall description [C-ice-quote] | "If you are faster" | The holder is compared to the attacker [C-reactive-holder-speed]. |
| Visual plan [C-visual-slot] | One wall on the slot | Walls stick to the Chainling and survive a swap [C-swap-keeps-walls]. |
| Timer rule | Move and modifier ticks before status ticks | Status ticks, then wall ticks [C-eot-order]. |
| `Ignore Walls` [C-mod-4.8-a] | "First hit ignores barriers" | The code matches that sentence. It does not ignore percent walls. The name over-promises once Blockade is visible. |
| v1 matrix asset | Block vs Burned → Firewall on the attacker [C-matrix-firewall-adjacent] | Runtime matches that asset. |
| Supabase `move_ap.ts` | Costs for the nine wall moves | Matches CSV [C-ap-blockade] [C-ap-ice_wall] [C-ap-lattice_guard] and the other `C-ap-*` ids. |
| `known_pool.generated.ts` | Candy / known list | No wall ids [C-known-absent-blockade] and the other `C-known-absent-*` ids. |
| Audit test [C-audit-test] | Unlimited hits | Present, and the four CSV rows match. |
| All nine wall sprites | Should identify the move | fileID 0 [C-wall-sprites-empty]. Blockade sound id blank [C-blockade-sfx-empty]. |

---

## (k) Questions only Cronus can answer

1. Does a wall stay on the Chainling when they bench, or does it drop when they leave the slot? **Default:** drop on swap-out and on KO. Do not tick walls on the bench. Recharge still ticks on the bench.
2. Can two different percent walls stack on one ally? **Default:** no. One wall-tagged mitigation source. A new wall replaces it. Passives and Firewall chemistry still stack.
3. Ice Wall's "+1 AP if you are faster": the caster, or the ally holding the wall? **Default:** the holder, which is what the code does. Change the description. Do not move the AP onto the caster.
4. Should Ignore Walls (4.8-a) also ignore percent mitigation? **Default:** no. Barriers only. An Epic stamp should not erase Blockade.
5. Should a full absorb still trigger the holder's on-hit AP? **Default:** yes.
6. Is the 10% floor 10% of the post-chemistry hit, or 10% of power × (atk/def)? **Default:** keep the code. Do not retune it this round.
7. Should DoT keep ignoring walls? **Default:** yes. Narrate it.
8. Lattice Guard is 1 AP. Raise it to 2 before any pool add? **Default:** yes. Stay Uncommon Shard. Do not add it to a pool in this pass.
9. Should Ash Guard enter Cinislug? **Default:** no. Do not put a second Burned wall beside Ember Wall.
10. Ice Wall from 3 AP to 2 AP? **Default:** yes. Keep the one-turn window and `-1` hits. Pool membership stays as it is.

---

## Check script

`walls_barriers_check.py` reads `CHAINBOUND_SNAPSHOT` or `/tmp/chainbound_snapshot`. It prints `PASS` or `FAIL`, the claim id, and a file:line or CSV row. Exit code is 0 only when every claim passes.

**Tally: 220 PASS / 0 FAIL / 220 claims.**
