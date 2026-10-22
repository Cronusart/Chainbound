# Type matchups, chemistry, and STAB — v2 re-check

Snapshot: `chainbound_combat_snapshot_20261020_0215` (local tree, 2026-10-20 ~02:15 CDT). Walls notes are from `WALLS_BARRIERS_MITIGATION_ANALYSIS_20261020.md`. This round does not repeat the 2026-10-17 type essay. It re-measures what that essay, Cronus's 2026-10-18 answers, and the later plans left in the tree.

Every current-code or current-data claim is tagged `[C-…]`. `type_chem_v2_check.py` recomputes it from the unzipped snapshot and prints PASS or FAIL with the file, line, or CSV row. Numeric fights use that script: mid roll 0.925, no crit, additive +1, level factor 1, average hit count, both sides start at 0 AP and gain 2 at the start of each turn (cap 10). A 3 AP move cannot be pressed on turn 1.

Check tally: **170 PASS / 0 FAIL / 170 checked.**

## (a) Headline

Since 17 October the audit round landed two real chemistry fixes: Jammed is an owned Accuracy −1 that reverts on expiry, and end-of-turn passives now run before status ticks inside a team. The type chart itself did not change. The live asset is still the 57-cell v1 matrix. Meme is still empty. Every species is still mono-type. STAB is still off. Typeless moves still inherit the attacker's primary type for the ×1.2 / ×0.8 step, for STAB if that flag is turned on, and for the move panel.

The chart is even. The kits are not. Rattlestake has no typed damaging move. Cinislug's only typed hit costs 3 AP, so the Burned chart (+3, the best in the game) is not what that species presses on turn 1. The `types_chemistry_statuses_v1` plan is still entirely pending, and its §S misreads Cronus: it treats typeless inheritance as the decision. The decision is the opposite. Typeless hits are excluded from type effectiveness.

**PROPOSAL** (top): stop that inheritance, and do not turn STAB on in the same change. Then cap Rugged at 3, give Rattlestake a Staked Common, and drop Cinder Burst to 2 AP. Suggested STAB default, not a decision: leave it off until exclusion ships; if it comes on later, use 1.1.

## (b) Did the decisions land?

`types_chemistry_statuses_v1` has 11 todos, all `pending` [C-plan-pending]. `audit_reconciliation_v1` is completed [C-audit-done]. `combat_balance_followups_v1` is completed, completed, completed, completed, **in_progress** (P4), completed, completed, completed, completed, pending (P9) [C-fu-status].

| Item | Verdict | Where | Test |
| --- | --- | --- | --- |
| T1 Jammed = Accuracy −1, owned, reverts | Partly | Asset stage −1 on Accuracy, apply once, duration 2, summary matches [C-jammed-stage] [C-jammed-duration] [C-jammed-summary]. Owned combat stages revert on expiry. Defueled does not revert that Accuracy stage [C-defueled-skips-combat-stage]. | `Jammed_AppliesOwnedAccuracyMinusOneAndReverts` builds the stage in the test. It does not load the asset [C-test-jammed]. No Defueled+Jammed test. |
| T1.2 Rugged cap narrowed to 3 | Not landed | Constant 15 [C-rugged-cap-const], asset 15 [C-rugged-cap-asset], OnValidate still clamps 1–15 [C-rugged-clamp]. `COMBAT_Chemistry.txt` still says severity 1–15 [C-chem-doc-1-15]. | `RuggedEscalatesAndCapsWhileDripAndFomoTickOnce` asserts counter 15 [C-test-rugged-15]. |
| T2 ×1.2 / ×1.0 / ×0.8 | Landed (already live) | Asset 1.2 and 0.8 [C-adv-factor-asset] [C-dis-factor-asset]. Constants match [C-const-adv] [C-const-dis]. Dual types collapse; no pair leaves {1.2, 1.0, 0.8} [C-dual-no-extreme]. | `ChemistryDamageMultiplier_UsesOnePointTwoAndZeroPointEight` [C-test-chem-mult]. |
| STAB never for typeless | Not landed | `ResolveStabMultiplier` calls `ResolveAttackType` and has no `baseType == None` exclusion [C-stab-calls-resolve]. Shipping behavior is that nobody gets STAB, because the flag is off. | No `Stab_TypedOnly` test [C-no-stab-typed-only-test]. |
| enableStab stays off | Landed | Asset 0, authored multiplier 1.2 [C-stab-off] [C-stab-authored]. Field default false [C-enable-stab-default]. | `Assert.IsFalse(config.enableStab)` [C-test-stab-off]. |
| Passives before status ticks | Landed for one team, not the plan's global two-pass | Inside `ProcessEndOfTurnForTeam`, EndOfTurn passives run, then `ProcessEndOfTurn` [C-passives-before]. The string occurs 5 times (definition plus four calls: team 0 then team 1, at two sites) [C-eot-callers]. There is no `RunEndOfTurn` [C-no-run-end-of-turn]. Wall timers still tick after the status pass. | `EndOfTurnPassives_RunBeforeStatusDurationTick` [C-test-eot]. |
| HODLed: one ramp + stat-drop immunity | Not landed | Summary promises immunity and +1/turn [C-hodl-summary]. The asset is one repeating MeleeDef +1 [C-hodl-stage] plus an extra +1 while Def &lt; 3 [C-hodl-extra]. No `FilterNegativeStage` [C-hodl-no-immunity]. No move produces it [C-hodl-no-producer]. | `RepeatingStagesRevertWhenTheStatusExpires` asserts MeleeDef 2 after the first end step [C-test-hodl-double]. That locks the double ramp. |
| Overleveraged rework (Margin Loan) | Not landed | Summary is "Shelved; not applied by any move." [C-over-summary]. `OVERLEVERAGED_RECOIL` is unread [C-over-recoil-unread]. No MarginLoan symbol [C-no-margin-loan]. `risk_marker` is a separate 1 AP foe surcharge, effect type 8 [C-risk-marker-surcharge] [C-risk-effect]. | No Margin Loan test. |

## (c) Typeless and type effectiveness

`ResolveAttackType` returns the move's base type when it is set, and `attacker.primaryType` when the move is typeless [C-typeless-inherits]. Chemistry uses that type. So a typeless Burned attacker into Gas is Advantage ×1.2 and can apply Rugged. The plan's §S says this "is exactly what Cronus described" [C-plan-misread] and names a test, `TypelessMove_UsesPrimaryType`, that would lock the inheritance [C-plan-wrong-test]. That test is not in the combat tests. The 2026-10-18 decision is the other way: typeless hits get neither ×1.2 nor ×0.8.

Where inheritance happens today:

- Effectiveness and the reaction: yes, via `ResolveAttackType`.
- STAB: the same function, if `enableStab` is turned on [C-stab-calls-resolve]. The flag is off, so the bug is latent [C-stab-off].
- Move panel: `CombatSelectionCoordinatorUGUI` shows the inherited type [C-ui-inherits].
- AI scoring: `EstimateDamage` ignores chemistry [C-ai-ignores-chemistry]. Bots do not exploit the bug, and they also ignore real typed advantages.
- Supabase: no `chemistryAdvantage`, `ResolveAttackType`, or `stabMultiplier` under `functions` [C-server-damage-mirror]. `move_ap.ts` matches Moves.csv with no extra keys [C-ap-server-parity] [C-extra-server-ap]. The known-pool block is 10 moves, all element 0 [C-known-count] [C-known-all-typeless].

Size. There are 21 typeless damaging moves [C-typeless-n]. Commons in live pools include Pulse Shot (power 26, AP 2) on Cinislug, Rattlestake, Forkrook, Cofi Lootwak, and Minefolk [C-pulse-power]. One hit of Cinislug's Pulse Shot into Deghoul is 54 with inheritance (×1.2, Rugged applied) and 45 with typeless excluded (×1.0, no reaction) [C-pulse-inherit-vs-excluded]. Deghoul's first Rugged tick is 6 [C-deghoul-rugged-tick]. See scenario F.

## (d) Matchup map, today

Live source: `Assets/Data/Combat/Type/Chemistry/TypeChemistryMatrix.asset`. 57 cells [C-matrix-count]. It matches `COMBAT_Interaction_Matrix_v1.csv` cell for cell (status, recipient, parameter) and matches the v1 direction grid [C-asset-matches-v1] [C-asset-matches-v1-directions]. v0 has 91 filled cells [C-v0-count]. v2 has 83 [C-v2-count] and is not the same occupancy [C-v2-drift]. The 26 cells v2 adds, and the asset does not have, are labeled only FOMO and Hyped, and every one of them touches Meme [C-v2-only]. Importing v2 would give Meme chemistry. Do not import it.

Meme cells in the asset: 0 [C-meme-cells]. Mutual advantages: 0 [C-no-mutual]. Self cells: 0 [C-no-self]. `COMBAT_TypeProfiles.md` beat lists match the asset [C-profile-beats]. All nine triangles are one-way [C-triangles]. `COMBAT_Chemistry.txt` still describes flat ×1.2 and the old 1–15 Rugged band [C-chem-doc-x12] [C-chem-doc-1-15].

Dual-type collapse is live code and has no content. Every species has `secondaryType` 0 [C-dual-species]. Across the 91 dual pairs, 249 cancel to neutral and 105 are double-advantage and stay at ×1.2 [C-cancel-pairs] [C-double-adv-pairs]. None become ×1.44 or ×0.64 [C-dual-no-extreme].

Net is offensive advantages minus defensive weaknesses, including the mirror of each cell. Offensive average is the mean multiplier into the other 12 chemistry types plus self (always neutral).

| Type | Beats | Weak to | Net | Off avg | Taken avg |
| --- | --- | --- | --- | --- | --- |
| Node | 4 Frozen, Mined, Forked, Compressed | 4 | 0 | 1.000 | 1.000 |
| Gas | 4 Bot, Node, Block, Staked | 4 | 0 | 1.000 | 1.000 |
| Block | 3 Forked, Shard, Burned | 5 | −2 | 0.969 | 1.031 |
| Encrypted | 4 Frozen, Gas, Burned, Bot | 4 | 0 | 1.000 | 1.000 |
| Burned | 7 Shard, Compressed, Frozen, Staked, Gas, Bot, Mined | 4 | +3 | 1.046 | 0.954 |
| Frozen | 5 Glitched, Gas, Bot, Shard, Staked | 5 | 0 | 1.000 | 1.000 |
| Forked | 4 Glitched, Burned, Bot, Mined | 4 | 0 | 1.000 | 1.000 |
| Mined | 5 Block, Staked, Frozen, Gas, Encrypted | 5 | 0 | 1.000 | 1.000 |
| Staked | 3 Forked, Compressed, Shard | 5 | −2 | 0.969 | 1.031 |
| Compressed | 3 Frozen, Block, Encrypted | 4 | −1 | 0.985 | 1.015 |
| Bot | 5 Mined, Node, Block, Staked, Glitched | 5 | 0 | 1.000 | 1.000 |
| Glitched | 4 Encrypted, Node, Block, Burned | 4 | 0 | 1.000 | 1.000 |
| Shard | 6 Mined, Encrypted, Node, Forked, Compressed, Glitched | 4 | +2 | 1.031 | 0.969 |
| Meme | none | none | — | 1 | 1 |

[C-type-nets] [C-type-avgs] [C-profile-beats]

Burned and Shard are the only plus charts. Block and Staked are the only −2 charts. That fits the roles: Block is the wall with many bad matchups, Staked is a thin offensive type. The chart is not the problem. The kits are.

19 species assets, 18 learn-pool rows, Ghost has none [C-species-n] [C-pool-n] [C-ghost-no-pool]. Ghost is Meme with 0 HP and no rarity [C-ghost-type] [C-ghost-hp] [C-cited-bodies]. Every non-Meme attack type appears on some pool's typed damaging move. Meme does not [C-missing-pool-types]. Every matrix cell can be pressed by some pool against a living defender type [C-unreachable-pool]. The script's band walk (5 moves, at most one move one step above the body, flagship the unique top rarity; starters, Ghost, and Cofi skipped) found no break [C-pool-breaks].

Mono coverage is (advantage, neutral, resisted) against the 14 single types, using the best typed damaging move in the live pool, including off-type moves. Rattlestake is (0, 14, 0) [C-mono-holes] [C-no-typed-species]. Its pool is `ap_reserve`, `compound_guard`, `baseline_shot`, `baseline_jab`, `baseline_thrust` [C-rattlestake-members]. There is no Staked Common damaging move in the catalog to give it [C-no-staked-common]. The learn-pool plan said that on purpose.

Other holes and inflations [C-mono-holes] [C-cited-bodies]:

- Cinislug (Burned, 134 HP, RAtk 63, Speed 28) covers 7 / 3 / 4, but the only typed move is Cinder Burst, power 15, 2–4 hits, AP 3 [C-cinder-ap]. Scorch Shot is power 52, AP 2, and is not in the pool [C-scorch-power] [C-scorch-ap] [C-scorch-not-in-cinislug].
- Validape (Node starter, 156 HP, Speed 60) covers 4 / 6 / 4. Everyday damage is typeless Snap Strike. The typed hit is Hammer Sync, 4 AP, Legendary [C-pool-rarities]. There is no Node Common damaging move [C-no-node-common]. Uplink is in the pool; AP Router is not [C-uplink-validape].
- Kreechank's only typed damage is Chaos Strike, Mythic [C-pool-rarities]. No Glitched Common damaging move exists [C-no-glitched-common].
- Off-type moves inflate the union: Balloom's Forked recoil, Hopoch's Mined Deep Reap (8 / 4 / 2), Kodiax's Block Solid Strike (8 / 5 / 1), Marrowhead's Compressed Precision Strike (8 / 4 / 2). Own-type coverage is thinner than those rows look. Precision Strike does not get STAB on a Shard user if STAB is ever turned on.
- Blockwyrm (Block, Legendary body) covers 3 / 6 / 5. Immutable Ledger is also Legendary, so the signature is the unique top of the pool but it is not above the body band [C-blockwyrm-sig-tier] [C-pool-rarities]. Bulwark Blow attacks with MeleeDef, not MeleeAtk [C-bulwark-uses-mdef].

Species that can advantage each defender type, counting off-type moves and skipping Ghost [C-served]: Block 9, Frozen 8, Staked 8, Encrypted 7, Forked 7, Mined 7, and 6 for Node, Gas, Burned, Compressed, Bot, Glitched, and Shard. Meme 0, by design. Block is the most-answered defender, which matches a wall with a −2 chart. Burned is the least-answered plus-chart type, and its own species cannot press the chart on turn 1.

P4 did not land as a per-hit band. 25 common damaging moves, powers 8 through 36, mean 20.44 [C-common-n] [C-common-damage-min] [C-common-damage-max] [C-common-mean]. Typed common per-hit powers are 12, 12, 12, 14, 14, 15, 17, 17, 18, 20, 34, 36, mean 18.42 [C-typed-common-powers] [C-typed-common-mean]. Several of those are multi-hit and land near 34–45 expected (Blade Flurry 12×3 = 36). The two single-hit typed commons in the 30s are Gas Bump 36 and Precision Strike 34. Moves.csv, move assets, and `move_ap.ts` agree on power and AP [C-power-parity] [C-ap-asset-parity] [C-ap-server-parity] [C-no-missing-move-asset].

Rarity flag: Kitsauto's Common Blade Flurry expects 36 power-hits at 2 AP. Uncommon Multi-Thread Shot expects 30 at the same AP and the same type [C-blade-vs-multi]. The common beats the uncommon on damage.

## (e) STAB decision support

STAB is Cronus's call. This section does not turn it on.

Equal stats, power 34, attack = defense = 50, typeless excluded, no wall. Typeless stays at 32 in every column because it gets neither chemistry nor STAB [C-equal-typeless-off].

| Case | STAB off | STAB 1.1 | STAB 1.2 |
| --- | --- | --- | --- |
| Typed advantage | 38 | 42 | 46 |
| Typed neutral | 32 | 35 | 38 |
| Typed resisted | 25 | 28 | 31 |
| Typeless, any defender | 32 | 32 | 32 |

[C-equal-typed-adv-off] [C-equal-typed-neu-off] [C-equal-typed-res-off] [C-equal-adv-11] [C-equal-neu-11] [C-equal-res-11] [C-equal-adv-12] [C-equal-neu-12] [C-equal-res-12]

A rare typeless at power 40 deals 38. That ties a typed common of 34 on advantage with STAB off, and ties a typed 34 on neutral with STAB 1.2 [C-equal-rare-40] [C-equal-neu-12]. Power 44 deals 41 [C-equal-rare-44].

Trade-offs:

- STAB off: typed advantage beats equal-power typeless (38 vs 32, about +19%). Typed neutral ties typeless. The rule "typed beats typeless" is not met on neutral hits.
- STAB 1.1: neutral typed is 35 vs 32 (about +9%). Advantage × STAB is 1.32. Resisted typed is 28, still under typeless.
- STAB 1.2: neutral typed is 38 vs 32. Resisted typed is 31, which loses to equal-power typeless at 32. Advantage × STAB is 1.44, and 2.16 with a 1.5 crit. That is the stack the dual-type collapse was built to avoid as a type-chart product.

Who loses if STAB comes on: any pool whose everyday button is typeless. Rattlestake has only typeless damage. Cinislug, Forkrook, Cofi, and Minefolk press Pulse Shot (26) beside a typed move that is either 3 AP, Rare+, or a spread. Validape and Kreechank press a typeless common and keep their typed power on a 4 AP or Mythic signature. Those signatures would gain STAB. The commons would not. FU P5.2 put those typeless commons in the pools because the typed common did not exist. STAB on makes that choice louder.

Walls, from the walls note, apply after chemistry. A 25% window on the equal-stat model, floor 0.10 not binding [C-mitigation-floor]: power 34 advantage 38 → 29, and 46 → 35 with STAB 1.2 [C-wall-p34] [C-wall-p34-s12]. Power 52 advantage 58 → 44, and 70 → 53 with STAB 1.2 [C-wall-p52] [C-wall-p52-s12]. Neutral 32 → 24 [C-wall-neu]. Rugged ignores the wall. One application lasts 3 turns, so cap 15 and cap 3 deal the same three ticks (6.25%, 12.5%, 18.75%). On 134 HP that is 8, 16, 25 [C-rugged-134-cap15] [C-rugged-134-cap3]. On 161 HP it is 10, 20, 30 [C-rugged-161-cap15] [C-rugged-161-cap3]. The cap only matters when the status is refreshed. Six ticks at cap 15 on 134 HP continue 33, 41, 50.

Suggested default: leave `enableStab` off until typeless exclusion is in. If Cronus turns STAB on after that, 1.1 is the safer stack. 1.2 makes a resisted typed hit lose to an equal-power typeless hit, and it makes the Kitsauto-vs-Kodiax heavy never land (section g).

## (f) Chemistry reactions

Chemistry resolves on the hit, before damage, before walls [C-chem-before-damage]. The status reaction fires once per attacker and once per defender per action; later hits still get the multiplier [C-reaction-once]. Primary defender type owns the reaction when both types would be advantages. `CheckBarrierBypass` takes the chemistry result and does not read it [C-bypass-ignores-chem]. DoTs do not consult walls. The walls doc already covers hit budgets, full absorb, and the 25% window. This section does not redo that.

End of turn, per team: EndOfTurn passives, then the status pass, then mitigation, accuracy, and barrier timers, then bench recharge [C-passives-before]. Kindled is a literal 6% and runs first inside the status pass [C-kindled-literal]. Leaked is not in that loop. It taxes 10% MaxHP when that side acts [C-leaked-10]. A new duration status applies its once-only stages before `Resolve` returns, so FOMO's MeleeAtk +1 is on the attacker for the applying melee hit [C-fomo-apply-once] [C-fomo-stage]. Shattered's MeleeDef −1 is a raw `SetStage` in the same instant path, so the applying hit sees the drop [C-shattered-before-damage] [C-shattered-class].

Cell counts sum to 57 [C-status-counts] [C-matrix-count]. Durations are `defaultDuration` [C-durations].

| Output | Cells | Duration | What it does | Consistency |
| --- | --- | --- | --- | --- |
| Pumped | 4 | 3 | +1 AP at end of turn | Fine. |
| Drained | 5 | 3 | −1 AP at end of turn | Fine. Bot's denial shape. |
| Rugged | 5 | 3 | `max(1, int(MaxHP * sev/16))`, then sev +1, cap 15. Refresh keeps severity. Burned → Gas is one of the five, recipient foe [C-burned-gas-rugged] [C-rugged-count] | Cap is the wide swing. One application never reaches it. |
| Leaked | 2 | 3 | 10% MaxHP when that side acts | Not an end-step tick. |
| Defueled | 3 | instant, self | Removes one harmful chemistry status. Reverts a temporary Slowed delta only | Does not revert Jammed's Accuracy [C-defueled-skips-combat-stage]. |
| Jammed | 8 | 2 | Accuracy −1, owned [C-jammed-count] | Landed, except Defueled. A 95% move hits at 0.6333 [C-jammed-hit-95]. Applied after the hit check, so the applying hit is unaffected. |
| FOMO | 2 | 3 | MeleeAtk +1 before the applying hit; self tick is the config 6.25%, not the asset percent [C-fomo-const] | Chemistry self-damage, not a move. The shelf covers moves. |
| Siphoned | 1 | 3 | Heal is hardcoded 25% of damage dealt [C-siphon-25] | Catalog percent is not what the resolver uses. |
| Anchored | 6 | 3, consumed | Next damaging move collapses to one target | Fine. |
| Dusted | 4 | 2 | Speed −3 and Evasion −3 [C-dusted-stages] | Evasion −3 makes a 95% move a sure hit [C-dusted-hit-95]. Speed −3 is ×0.4 [C-speed-stage-3]. Too wide. |
| Backlash | 2 | 3 | Token. Asset summary already says so [C-backlash-summary] [C-backlash-count] | Resolver and status processor never read it [C-backlash-unread]. `StatusId` still says "Contact adds typed DoT" [C-backlash-comment]. Can fire. Does nothing. |
| Corroded | 3 | 2 | Repeating MeleeDef −1, owned, reverts [C-corroded-stage] | Does not touch RangedDef. A ranged attacker ignores it. |
| Cascade Failure | 1 | counter | Third apply deals 25% MaxHP and can KO [C-cascade-const] | Fine as a spike. Narrow. |
| Isolated | 2 | 2 | Blocks ally Support, AP transfer, healing, stage buffs | Fine. |
| Firewall | 1 | 2 | First projectile each turn takes 30% [C-firewall-const] | Counts as a wall in presentation. See the walls doc. |
| Slowed | 1 | 2 | Speed −2, reverted | The narrow version of Dusted. |
| Shattered | 4 | instant | 8% current HP, cannot KO, and MeleeDef −1 before this hit [C-shatter-const] | Kodiax Ore Strike into Blockwyrm: 48 without the drop, 72 with MeleeDef −1 already applied (86 → 57). Chip on 168 HP is 13 [C-shattered-delta]. The drop does not revert. RangedDef is untouched. |
| Drip | 1 | 3 | Heals 6% MaxHP each end step [C-drip-const] | Code heals every end step of the duration. |
| Splinter | 2 | 1, consumed | Next damaging move spreads at 65%, or +15% if it stays single [C-splinter-const] | Fine. |

Kindled (duration 1) is not a matrix output. It is the literal 6% that ticks first. HODLed and Overleveraged are not matrix outputs.

No matrix reaction is dead for lack of an attack type in a pool [C-unreachable-pool]. Backlash is the reaction that fires and does nothing. HODLed still has no producer. Reactions on a walled target still happen: full absorb does not eat the status, and Rugged then ticks through the wall (scenario G).

## (g) Speed and tempo

Turn order is speed. Deghoul 72, Cinislug 28. Kitsauto 82, Kodiax 46 [C-cited-bodies]. AP per turn is 2 [C-ap-per-turn].

**F. Inheritance vs the decision.** Deghoul Gas Bump (Gas, power 36, AP 2, resisted into Burned) vs Cinislug Pulse Shot.

Today, typeless inherits Burned, so Pulse is ×1.2 into Gas and applies Rugged [C-f-inherit]:

- Turn 1: Gas Bump 36, Cinislug 98. Pulse 54, Deghoul 52, Rugged. End tick 6, Deghoul 46.
- Turn 2: Gas Bump 36, Cinislug 62. Pulse 54 KOs Deghoul (end HP −8).

Excluded, Pulse is ×1.0 and applies nothing [C-f-excluded]:

- Turn 1: Cinislug 98, Deghoul 61.
- Turn 2: Cinislug 62, Deghoul 16.
- Turn 3: Cinislug 26, Pulse 45 KOs (end HP −29).

Each inherited Pulse is 9 HP more, plus a 6 HP tick. The fight ends a turn earlier. That is the gap on a move five species actually learn.

**A. The typed button, STAB off, typeless excluded.** Cinislug banks for Cinder (AP 3). Average 3 hits of 32 is 96, ×1.2, Rugged [C-cinder-into-deghoul] [C-a-off]:

- Turn 1: Gas Bump 36, Cinislug 98. Cinislug banks.
- Turn 2: Gas Bump 36, Cinislug 62. Cinder 96, Deghoul 10, Rugged tick 6, Deghoul 4.
- Turn 3: Gas Bump 36, Cinislug 26. Second Cinder KOs.

Cinislug wins and eats two resisted bumps plus a third. With STAB 1.2 the first Cinder is 114 and KOs on the hit, before the tick [C-a-s12]. STAB 1.1 KOs on the end-of-turn tick instead (105, then 6).

**E. Two 2 AP advantage strings vs one 4 AP heavy.** Kitsauto Blade Flurry (Bot, 12×3, AP 2, ×1.2 into Mined) vs Kodiax Deep Dig (Mined, 72, AP 4, ×0.8 into Bot).

STAB off [C-e-off]: turn 1 flurry 60, Kodiax 64, banks. Turn 2 flurry leaves Kodiax at 4, then Deep Dig 69 lands and leaves Kitsauto at 70. Turn 3 flurry KOs before Kodiax acts. The heavy connects once.

STAB 1.1 [C-e-s11]: flurry 66. Turn 2 KOs Kodiax at −8 before Deep Dig. Kitsauto is still at 139. STAB 1.2 is the same outcome, harder (Kodiax −20) [C-e-s12]. This is the tempo reason to prefer 1.1 if STAB comes on: 1.1 already deletes the 4 AP answer.

**G. Same Cinder fight, Deghoul holding a 25% window.** Gas Bump is not walled. Cinder into the wall is 72 instead of 96. Rugged still ticks 6 [C-g-wall]. Deghoul goes 34, then 28, and the second walled Cinder KOs on turn 3. The wall taxes 24 HP per Cinder string and does not stop the burn. With STAB 1.2, Cinislug still wins and finishes at 5 HP [C-g-wall-s12].

## (h) What works, what doesn't, what could be better

Works. The v1 chart is clean: no mutuals, no self hits, no Meme, dual types cannot explode, factors are exactly 1.2 and 0.8. Jammed is finally a real Accuracy −1. Passives-before-ticks landed inside a team. CSV, assets, and the server AP table match. Pools satisfy the band walk. Risk Marker is a real foe AP tax and should be left alone.

Doesn't. Typeless still borrows the user's type, and the plan text blesses that. Rugged's cap is still 15 and the locking test wants 15. HODLed still double-ramps, still has no immunity, and still has no producer. Overleveraged is still a shelved token. Rattlestake cannot type anyone. Cinislug's typed move misses turn 1. Dusted grants sure hits. Shattered's defense drop hits the same attack and never ends. Backlash is a blank cell. Common per-hit power is about 20, not about 34. Blade Flurry outdamages the uncommon beside it.

Could be better. Exclusion first, STAB later, cap Rugged, and fill the two kit holes (Rattlestake's type, Cinislug's AP) without adding a sixth move. Narrow Dusted, Shattered, and Corroded to the same width as Jammed and Slowed. Make Defueled revert the stages it claims to remove.

## (i) Ranked recommendations

Each item is a **PROPOSAL**. Pool edits keep the learn-pool band: wild pools stay at 5, three at the body tier or lower, one at the body or one step up, signature the unique highest rarity in the pool. Starters stay exempt from the 3+1 band and keep a unique top signature. Ghost and Cofi stay outside. Boss-only moves stay out of pools and candy. No proposal adds a sixth move.

1. **PROPOSAL — exclude typeless from effectiveness and from STAB.** Do not flip `enableStab` in the same change. When `move.baseType` is None, the chemistry relation is Neutral and no reaction is applied. `ResolveStabMultiplier` returns 1 without calling `ResolveAttackType`. The move panel stops showing an inherited type. AI stays chemistry-blind until it is taught `baseType`, with None as neutral. Do not teach it the inherited type.
   Files: `DamageCalculator.cs` `ResolveAttackType` / `ResolveStabMultiplier`, `CombatSelectionCoordinatorUGUI.ResolveMoveType`.
   Test `TypelessMove_IsNeutralAndAppliesNoReaction`: Burned attacker, `baseline_shot` into Gas → multiplier 1.0 and no Rugged; Cinder Burst into Gas → 1.2 and Rugged. Test `Stab_TypedOnly` on a local config with STAB on at 1.2: same-type typed 1.2, typeless 1.0, off-type 1.0. Shipping config still returns 1.

2. **PROPOSAL — Rugged cap 3.** `CombatConstants.RUGGED_TICK_CAP = 3`, asset `ruggedTickCap: 3`. Leave the OnValidate clamp at 1–15 so a designer can still test 15. Update `COMBAT_Chemistry.txt` and the status summary. One application is unchanged (8, 16, 25 on 134 HP). A refresh plateaus at 25 instead of climbing to 50.
   Test: retarget `RuggedEscalatesAndCapsWhileDripAndFomoTickOnce` so the default cap asserts 3. A local config of 15 still reaches 15.

3. **PROPOSAL — Rattlestake gets a typed common.** There is no Staked Common to reuse. Add one: Staked, Common, ranged, power 34, AP 2, accuracy 95. Rattlestake's RangedAtk is 55 and MeleeAtk is 28, so ranged matches the body. Replace `baseline_shot` on the Rattlestake row only. Pool stays five: the new common, `baseline_jab`, `baseline_thrust`, Uncommon `compound_guard`, Legendary `ap_reserve` [C-pool-rarities] [C-rattlestake-members]. That keeps the band. Do not touch Hopoch or Cofi. Do not put it in a candy table beyond a normal Common.
   Test: Rattlestake's pool still has 5 moves, the signature is still the unique top rarity, and the new move is Staked so it is Advantage into Forked, Compressed, and Shard.

4. **PROPOSAL — Cinder Burst AP 3 → 2.** Power stays 15, hits stay 2–4. Pool membership unchanged, so the band holds. Minimum 2×15 = 30 already beats Pulse Shot 26 on a neutral hit. Expected 3×15 = 45 stays under Scorch Shot 52. Flag: a 4-hit string is 60 raw power and can beat that Rare before accuracy. Scorch Shot is not in this pool. The AP change is what lets the typed button exist on turn 1.
   Files: `Moves.csv` `cinder_burst` APCost, `Move_cinder_burst.asset`.
   Test: a Cinislug with 2 AP can pay for Cinder on turn 1, and expected damage at 3 hits is greater than Pulse Shot at equal stats.

5. **PROPOSAL — HODLed, one ramp and a real immunity.** Delete the extra +1 block. Catalog: MeleeDef +1 on apply, MeleeDef +1 repeating, own contribution capped at +3, both owned, both revert. Foe negative stages are filtered. The holder's own move costs still land. Do not add Diamond Hands to Blockwyrm. That pool is already at five, and the signature is not above the body.
   Test `Hodled_RampsOnePerStep`: +1, +2, +3, then 0 on expiry. Update the test that currently asserts +2 after one end step. Immunity test: a foe Shattered or Defense Corrupt writes no stage; the holder's own self stage drop still applies.

6. **PROPOSAL — Overleveraged stays shelved. Margin Loan is a new card.** Leave `risk_marker` as the foe surcharge. Add an ungranted Compressed Rare, 0 AP, self: +2 AP now, repay 2 at the second end step, shortfall becomes a next-move surcharge capped at +2. No HP loss. Not inserted into any pool.
   Test `MarginLoanTests`: +2 AP on use, the second end step collects 2 or converts the shortfall into a surcharge of at most +2, and HP does not change.

7. **PROPOSAL — narrow the wide reactions.**
   - Dusted: Speed −2, Evasion 0. A 95% move stays 95%. Both revert.
   - Shattered: keep the 8% chip (cannot KO). Apply MeleeDef −1 and RangedDef −1 as a duration-2 owned status after the hit. The applying hit gets ×1.2 only. Expiry reverts both.
   - Corroded: repeating MeleeDef −1 and RangedDef −1, owned. Two end steps → both −2; both 0 after expiry.
   - Defueled (and any chemistry removal) calls the same owned-stage revert as expiry, including combat stages, so Jammed's Accuracy returns to 0.
   Files: `StatusDef_Dusted`, `StatusDef_Shattered`, `StatusDef_Corroded`, `Statuses.csv`, `ChemistryResolver` Defueled and Shattered cases.

8. **PROPOSAL — do not import v2.** Banner or delete `COMBAT_Interaction_Matrix_v2.csv` so nobody loads the 26 Meme cells. Meme stays empty. Ghost stays dormant.

9. **PROPOSAL — secondary typed commons, only by replacing a typeless common.** Validape (starter): a Node Common replaces `baseline_striker`. Hammer Sync stays the unique top rarity. Kreechank: a Glitched Common replaces `baseline_striker`. Chaos Strike stays Mythic and unique; Error Correction stays the one-step Uncommon [C-pool-rarities]. Do not add a sixth move. Skip this if proposal 1 is the only change Cronus wants this round. These pools are who lose most when STAB eventually turns on.

10. **PROPOSAL — Blade Flurry vs Multi-Thread Shot.** Common 12×3 = 36 expected, Uncommon 12×2.5 = 30 expected, same AP, same type [C-blade-vs-multi]. Lower Blade Flurry to 10×3 = 30, or raise Multi-Thread's hit count. Do not change pool membership. Kitsauto is a starter; the signature stays Fox Feint.

11. **PROPOSAL — small tidies.** Kindled reads `endOfTurnDamagePercent` (value stays 0.06) instead of the literal. Siphoned reads its catalog percent (value stays 0.25) instead of the hardcoded 0.25. `StatusId.BacklashType` comment matches the token summary. Optional: raise Immutable Ledger from Legendary to Mythic so Blockwyrm's signature sits above the Legendary body. Membership stays the same five moves.

STAB itself is not a proposal to ship. Section (e) is the support. Suggested default is off, then 1.1.

## (j) Consistency flags

- Plan §S vs the 2026-10-18 decision [C-plan-misread] [C-plan-wrong-test]. The plan would lock the bug.
- `COMBAT_Chemistry.txt` still says severity 1–15 and flat ×1.2 [C-chem-doc-1-15] [C-chem-doc-x12]. The asset and the constants are the live numbers.
- v2 CSV is a superset that adds Meme [C-v2-drift]. Profiles and the asset do not.
- HODLed summary claims immunity the code does not have [C-hodl-summary] [C-hodl-no-immunity]. The locking test wants the double ramp [C-test-hodl-double].
- Backlash summary is honest; the enum comment is not [C-backlash-summary] [C-backlash-comment].
- Shattered's defense drop is not a status stage, so it survives Defueled and expiry [C-shattered-before-damage].
- Jammed's test does not load `StatusDef_Jammed` [C-test-jammed].
- P4 "Commons ~34" is still `in_progress`, and the per-hit mean is 20.44 [C-fu-status] [C-common-mean].
- Blockwyrm's signature rarity equals the body rarity [C-blockwyrm-sig-tier]. The band walk still passes because it is the unique top inside the pool.
- No damage mirror on the server [C-server-damage-mirror]. A future server combat port will re-implement this whole step. Exclusion has to be in that port, not only in Unity.
- AI ignores type [C-ai-ignores-chemistry]. That hides both the inheritance bug and real advantages.
- Bulwark Blow's attack stat is MeleeDef [C-bulwark-uses-mdef]. Any sim that uses MeleeAtk 58 understates it.

## (k) Questions only Cronus can answer

1. STAB off, 1.1, or 1.2, and when? Suggested default: off until typeless exclusion ships. If it comes on later, 1.1.
2. Rugged cap 3 or 2? Suggested default: 3. One application does not change. Refreshes plateau at 18.75% instead of climbing toward a full bar.
3. HODLed: one ramp and real immunity, producer still out of Blockwyrm's pool? Suggested default: yes.
4. Overleveraged becomes a new Margin Loan card, and Risk Marker stays the foe surcharge? Suggested default: yes.
5. Cinder Burst AP 3 → 2, power 15 kept, with the 4-hit ceiling flagged against Scorch Shot? Suggested default: yes.
6. New Staked Common, power 34, AP 2, ranged, replacing Pulse Shot on Rattlestake only? Suggested default: yes.
7. Dusted, Shattered, Corroded, and Defueled-revert, as in proposal 7? Suggested default: yes to all four.
8. Keep v1 and leave Meme empty, or import v2? Suggested default: keep v1.
9. FOMO's 6.25% self tick? Suggested default: keep. It is chemistry, not a move self-KO.
10. Ghost / Meme species? Suggested default: leave dormant. No chemistry, no pool.
11. Secondary Node and Glitched commons for Validape and Kreechank (proposal 9)? Suggested default: after exclusion, only if those everyday buttons still feel typeless. Not in the same change as the Rattlestake card.
12. Blade Flurry down to 10×3, or Multi-Thread Shot up? Suggested default: Blade Flurry to 10×3 so the common stops beating the uncommon.

## Check index

The script registers each id below. The tally above is the run against this file.

[C-matrix-count] [C-meme-cells] [C-no-mutual] [C-no-self] [C-asset-matches-v1] [C-asset-matches-v1-directions] [C-v2-drift] [C-v2-count] [C-v0-count] [C-cancel-pairs] [C-double-adv-pairs] [C-profile-beats] [C-triangles] [C-dual-no-extreme] [C-adv-factor-asset] [C-dis-factor-asset] [C-stab-off] [C-stab-authored] [C-const-adv] [C-const-dis] [C-typeless-inherits] [C-stab-calls-resolve] [C-passives-before] [C-no-run-end-of-turn] [C-eot-callers] [C-rugged-cap-asset] [C-rugged-cap-const] [C-rugged-clamp] [C-jammed-stage] [C-jammed-duration] [C-jammed-summary] [C-hodl-extra] [C-hodl-no-immunity] [C-hodl-summary] [C-hodl-stage] [C-over-summary] [C-over-recoil-unread] [C-no-margin-loan] [C-risk-marker-surcharge] [C-test-jammed] [C-test-eot] [C-test-hodl-double] [C-test-rugged-15] [C-test-stab-off] [C-test-chem-mult] [C-no-stab-typed-only-test] [C-ai-ignores-chemistry] [C-ui-inherits] [C-server-damage-mirror] [C-known-all-typeless] [C-power-parity] [C-ap-asset-parity] [C-ap-server-parity] [C-no-missing-move-asset] [C-dual-species] [C-ghost-type] [C-ghost-hp] [C-ghost-no-pool] [C-backlash-summary] [C-backlash-unread] [C-dusted-stages] [C-corroded-stage] [C-fomo-stage] [C-shattered-class] [C-shattered-before-damage] [C-defueled-skips-combat-stage] [C-kindled-literal] [C-bypass-ignores-chem] [C-jammed-count] [C-rugged-count] [C-backlash-count] [C-chem-doc-1-15] [C-chem-doc-x12] [C-pulse-power] [C-scorch-power] [C-scorch-not-in-cinislug] [C-uplink-validape] [C-common-damage-min] [C-common-damage-max] [C-common-n] [C-common-mean] [C-typed-common-powers] [C-typed-common-mean] [C-no-typed-species] [C-pool-breaks] [C-equal-typed-adv-off] [C-equal-typed-neu-off] [C-equal-typed-res-off] [C-equal-typeless-off] [C-equal-adv-11] [C-equal-neu-11] [C-equal-res-11] [C-equal-adv-12] [C-equal-neu-12] [C-equal-res-12] [C-equal-rare-40] [C-equal-rare-44] [C-pulse-inherit-vs-excluded] [C-cinder-into-deghoul] [C-rugged-134-cap15] [C-rugged-134-cap3] [C-rugged-161-cap15] [C-rugged-161-cap3] [C-jammed-hit-95] [C-dusted-hit-95] [C-speed-stage-3] [C-shattered-delta] [C-ap-per-turn] [C-extra-server-ap] [C-typeless-n] [C-known-count] [C-species-n] [C-pool-n] [C-unreachable-pool] [C-missing-pool-types] [C-wall-p34] [C-wall-p34-s12] [C-wall-p52] [C-wall-p52-s12] [C-wall-neu] [C-deghoul-rugged-tick] [C-cinder-ap] [C-scorch-ap] [C-blade-vs-multi] [C-burned-gas-rugged] [C-bulwark-uses-mdef] [C-leaked-10] [C-siphon-25] [C-firewall-const] [C-splinter-const] [C-shatter-const] [C-fomo-const] [C-drip-const] [C-cascade-const] [C-enable-stab-default] [C-mitigation-floor] [C-durations] [C-status-counts] [C-type-nets] [C-served] [C-mono-holes] [C-f-inherit] [C-f-excluded] [C-e-off] [C-e-s11] [C-e-s12] [C-a-off] [C-a-s12] [C-g-wall] [C-g-wall-s12] [C-no-staked-common] [C-no-node-common] [C-no-glitched-common] [C-pool-rarities] [C-blockwyrm-sig-tier] [C-rattlestake-members] [C-plan-pending] [C-plan-misread] [C-plan-wrong-test] [C-fu-status] [C-audit-done] [C-hodl-no-producer] [C-backlash-comment] [C-risk-effect] [C-chem-before-damage] [C-fomo-apply-once] [C-reaction-once] [C-cited-bodies] [C-type-avgs] [C-v2-only]
