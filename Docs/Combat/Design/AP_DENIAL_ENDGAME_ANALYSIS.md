# AP denial endgame

Snapshot: local Chainbound tree, 2026-10-22 ~00:10 CDT, unzipped for this pass. Every figure below is computed by `ap_denial_2v2_sim.py` from that tree, or read from a file the check script opens. Claim ids are `[Cxx]`. The check script re-runs the calendars, the deterministic traces, and the 1200-trial races.

This round does not reopen the 4 AP cost, the wall tiers, or Armor's flat numbers. Those live in `AP_ECONOMY_V2_ANALYSIS.md`, `MOVE_COST_CURVE_V2_ANALYSIS.md`, `TEMPO_COUNTERPLAY_2V2_ANALYSIS.md`, `WALLS_BARRIERS_PVP_REDESIGN_V2.md`, and `ARMOR_MECHANIC_V1_ANALYSIS.md`. Armor v1 is still a proposal. It does not move AP.

The round-15 file `tempo_2v2_sim.py` is not the engine. It still looks up `MitigationWindow` and never mentions `BlockWindow` [C31]. Its `do_swap` keeps turn-start windows [C32]. The sim shipped with this note uses `BlockWindow`, clears windows on swap and on KO, retargets by slot after a swap, applies Seam Cut's speed bonus, refreshes one chemistry status, taxes Leaked at 10% max HP, and treats `RechargeTurns` 2 as "skip the next turn."

## (a) Headline

A passer who is hit by Memory Leak every turn acts at 2 AP forever. A 4 AP finisher never becomes legal [C70]. One 2 AP spend after that, and the bar stays at 0 [C71]. That lock is real in a learned pool: Keyper's signature is Memory Leak [C55]. With Kitsauto's Blade Flurry beside it, a lone Validape who passes until Hammer Sync is payable dies on turn 3. In 1200 trials the denial side wins 100% and the hammer lands 0% [C110].

Packet Skim and Transaction Tax, each on their own, are pressure. The finisher is legal on turn 4 [C72] [C73]. Tax plus Skim, both faster than the victim, holds action AP at 0 [C74]. Those two then deal no damage, and in 2v2 the victim's partner wins [C115].

A cap of 2 removed AP per target per turn does not break either lock [C81] [C82]. Giving Memory Leak a one-turn recharge does, if the victim banks the open turn [C79].

Bot's learned denial is Packet Skim, 1 AP, a drain [C41] [C54]. The lock that ends games is Encrypted, not Bot.

## (b) What denies AP today

Regen is +2 at the start of every turn for each living active, including turn 1. The config line is `CombatResolverConfig_Default.asset:16` [C01] and `CombatConstants.cs:10` matches it [C04]. The gain call is `StatusProcessor.cs:457` [C10]. The bank ceiling is `CombatResolverConfig_Default.asset:17` [C02] and `CombatConstants.cs:11` [C05], applied inside Gain at `CombatApService.cs:81` [C06]. STAB is off at `CombatResolverConfig_Default.asset:24` [C03].

Turn order on the 2v2 path starts at `CombatResolver.cs:285` [C15]. Pending turn-start statuses, including Thread Lock, land at `CombatResolver.cs:415` before regen [C14]. OwnerTurnStart windows, which is how Memory Leak's −2 is delivered, run after that regen. End of turn: Kindled's 6% burn is its own step at `StatusProcessor.cs:93` [C23], then other status ticks, and the tick includes the turn the status was applied. Recharge ticks on the bench at `StatusProcessor.cs:304` [C12]. AP does not. Thread Lock's expiry sets the reapply gap at `StatusProcessor.cs:284` [C11].

Drain floors at 0. `CombatApService.cs:107` has no per-target counter [C07]. Steal drains and then gains what was removed at `CombatApService.cs:119` [C08]. Thread Lock blocks Gain at `CombatApService.cs:65`, so it blocks regen, steals received, and refunds [C09]. There is no general refund when a drain removes 0. The refund function is a section 3.1 modifier roll at `CombatResolver.cs:7642` [C30].

| Source | Runs today | In a learn pool | What it removes | Cap on one target |
| --- | --- | --- | --- | --- |
| Memory Leak | Yes. Window, not a status. | Keyper signature | −2 after next regen | None. Can be recast every turn |
| Packet Skim | Yes | Kitsauto | Drain 1 now | None |
| Transaction Tax | Yes | Deghoul signature | Steal 1 now | None |
| Drained | Yes, chemistry | Bot into Node, and a few other cells | −1 at end of turn, 3 ends, refresh | One copy |
| MEV Extracted | Yes, if the move is equipped | No pool [C58] | Drain 1 at end of turn, 2 ends | None |
| Thread Lock | Yes, if equipped | No pool [C58] | Blocks all Gain for 1 turn | Cannot be reapplied the next turn [C11] |
| Fee Collector | Yes, if equipped | No pool [C56] [C58] | Drain 1 from the first living opposing actor, only if that foe's AP is higher, at `CombatResolver.cs:7035` [C21] | One per holder per turn |
| Ember Wall | Yes | Cinislug signature | Drain 1 when the one melee hit consumes the wall | The drain is 1, and it happens after the attacker has paid |
| Ice Wall | Yes | Gilarime | Grants the bearer +1 if faster, max 1 on that effect | Not a drain |
| AP Drain, AP Siphon | Yes, if equipped | No pool [C58] | Drain 1, or a 50% drain on a 3 AP hit | None |
| Defensive Reflex, Double or Nothing | Yes, if equipped | No pool | Self drain, or a coin flip of +1 / −1 | Self only |
| Modifier 6.1-b AP Siphon | Yes, on a stamped hit | Stamp, not a move | 15–25% to drain 1 | One roll per hit |
| Next-move surcharges | Yes. Gas War, Verification Lock, and others | Gas War is in pools | The next move costs +1 | Not a drain |

Memory Leak's row is `Moves.csv:95` [C40]. Utility is `ArmUtilityWindow:OwnerTurnStart:1:1:-2`. Cost 2. Legendary. The description is "Target loses 2 AP next turn; user gains Speed +2." Speed +2 is an immediate stage on the user, cap +6, and it does not fall off. Packet Skim is `Moves.csv:210` [C41], `DrainAP:1`, cost 1, Common, "Cut the target's AP by 1." Transaction Tax is `Moves.csv:71` [C42], `StealAP:1`, cost 1, Epic, "Steal 1 AP from the target." MEV Extract is `Moves.csv:159` [C43]. The card says drain. Thread Lock is `Moves.csv:165` [C44]. Ember Wall is `Moves.csv:106` [C45] and the card already states the 50%, the one hit, the 1 AP drain, PRIOR, and recharge 1. Cinder Burst is `Moves.csv:107`, cost 3 [C46]. Seam Cut is `Moves.csv:207`, conditional ×1.25 while faster [C47]. Hammer Sync is `Moves.csv:198`, power 80, cost 4 [C48]. Fee Collector is `Moves.csv:75` [C49].

A voluntary swap clears the outgoing body's windows at `CombatResolver.cs:6907` [C16] and copies that body's current AP onto the incoming at `CombatResolver.cs:6911` [C17]. The incoming does not act on the swap turn. A KO replacement inherits the corpse AP at `TeamState.cs:178` [C19], and the corpse's windows are cleared at `CombatResolver.cs:1316` [C18]. One intact block per Chainling: a second add returns false at `ChainlingInstance.cs:536` [C20].

Cleanse removes General statuses only, at `StatusProcessor.cs:875` [C13]. Drained is ChemistryOnly, so cleanse leaves it. Memory Leak is not a status, so cleanse, System Reset, and Encrypted Seal do not clear the window. System Reset also skips the user's next action (recharge 2 on its own row).

Drained lasts 3 and ticks −1 [C51]. MEV Extracted lasts 2 and ticks −1 [C50]. Thread Lock lasts 1 [C52]. Leaked's row says 10% of your own max HP while the side is leaked [C53]. The code taxes `EffectiveMaxHP * 0.10` at `CombatResolver.cs:5529` before the action resolves [C22]. `StatusId.cs:52` comments MEV Extracted as "Steal 1 AP per turn" [C24]. The status row and the move both drain.

Guard Posture is still a 25% mitigation window in the CSV. Flat Armor is not in the engine. Nothing in the armor proposal changes the AP calendar.

## (c) Endgame math

Action AP below is the bar at the moment the watched body would act, after regen, after a due Memory Leak window, and after faster drains. Eight turns. The victim is Validape unless a row says otherwise. IV 0. A "hammer" is a paid move of 4 AP or more.

| Case | Action AP | Hammer |
| --- | --- | --- |
| Leak every turn, victim passes | 2, 2, 2, 2, 2, 2, 2, 2 | never [C70] |
| Leak every turn, victim spends 2 when able | 2, 0, 0, 0, 0, 0, 0, 0 | never [C71] |
| Tax only, Deghoul faster, victim passes | 1, 2, 3, 4, 5, 6, 7, 8 | turn 4 [C72] |
| Skim only, Kitsauto faster, victim passes | 1, 2, 3, 4, 5, 6, 7, 8 | turn 4 [C73] |
| Tax and Skim, both faster, victim passes or spends | 0, 0, 0, 0, 0, 0, 0, 0 | never [C74] |
| Tax and Skim, victim faster than both | 2, 2, 2, 2, 2, 2, 2, 2 | never [C75] |
| Start at 8, then leak, and spend 4 when able | 10, 6, 2, 2, 2, 2, 2, 2 | turn 1 [C76] |
| Start at 4, then leak, pass | 6, 6, 6, 6, 6, 6, 6, 6 | turn 1 [C77] |
| Leak every turn, Drained applied once, pass | 2, 1, 0, 0, 0, 0, 0, 0 | never [C78] |
| Leak with a one-turn recharge, pass | 2, 2, 4, 4, 6, 6, 8, 8 | turn 3 [C79] |
| Same recharge, spend 2 on every open turn | 2, 0, 2, 0, 2, 0, 2, 0 | never [C80] |
| Cap of 2, Tax and Skim | 0 every turn | never [C81] |
| Cap of 2, Leak and Skim | 1 every turn | never [C82] |
| Fuel Surge (+3, cost 0) on turn 2 into a leak | hammer turn 2 [C83] | |
| AP Reserve (bank up to 5, release +1) into a leak | hammer turn 5 [C84] | |
| Thread Lock once | hammer turn 3 [C86] | |
| MEV once | hammer turn 3 [C87] | |
| Swap on turn 2, leaker retargets the incoming | active bar stays at 2 [C85] | |

True locks, and only these:

- Memory Leak every turn. A passer is stuck at 2. A body that spends 2 is stuck at 0. The 4 AP finisher never becomes legal. 1v1 is a draw if the leaker has no other damage, because the leaker is spending the action. 1v2 with a real attacker is a win for the denial side. See the worked example.
- Memory Leak plus one Drained. A passer who never spends still reaches 0 on turn 3 [C78]. Bot into Node is Drained at ×1.2 [C64], so Blade Flurry applies it.
- Tax plus Skim, both faster, both denying every turn. Action AP is 0 [C74]. They deal no damage while they do it. In 2v2 that loses to the free partner [C115]. It is a lock on the one body, and a loss for the side if those two are the whole team.

Fair pressure, not locks:

- Tax alone, or Skim alone. Hammer on turn 4.
- One Thread Lock, or one MEV. Hammer on turn 3. Both are unpooled [C58].
- A bank of 4 or more before the first window. Hammer on turn 1. After they spend down under 4, the leak holds them there [C76].
- Ember's 1 AP drain. It lands after the move is paid. In the trace it removes 0 [C107].
- Ice Wall's +1 and the ledger's +1. Each is at most 1, and neither stops a leak.
- Fuel Surge and AP Reserve. They break a leak. Fuel is in Balloom's pool. Reserve is in Rattlestake's pool.

A one-turn recharge on Memory Leak frees a passer on turn 3 [C79]. If they spend the 2 AP on every open turn, the finisher stays illegal [C80]. That is the fair version: bank the open turn and the heavy comes out, or take the chip and wait.

A voluntary swap does not dodge a leaker who acts after the swap. Swaps resolve first. The window that was already due fires on the outgoing body at the start of the turn, then the swap clears it [C16]. The leaker's later action resolves against the slot, so the incoming is armed for next turn. The active bar in that calendar stays at 2 [C85].

A KO is different. The window is cleared with the corpse [C18]. The replacement does not inherit it. If the leaker already acted this turn, the replacement's first action is corpse AP plus regen, with no −2. At 0 inherited AP that action is 2. They can pay Seam Cut. They cannot pay Hammer Sync. Flooring the entry bar at 2 would make that action 4, because regen still adds 2 and the window is already gone. The 1200-trial pair is in section (g).

Species used for these rows, IV 0, from the assets [C63]: Validape HP 156 speed 60, Kitsauto HP 139 speed 82, Keyper HP 102 speed 68, Deghoul HP 106 speed 72, Kodiax HP 124 speed 46, Crackett HP 112 speed 92, Cinislug HP 134 speed 28.

If Tax becomes a drain, the victim's calendar does not change [C89]. Deghoul's own end-of-turn bank does. Stealing, Deghoul ends turns at 2, 4, 6, 8, 10, 10. Draining, Deghoul ends at 1, 2, 3, 4, 5, 6 [C88]. The steal is how a Tax user banks their own 4 AP move while denying.

## (d) Counterplay, and cheap typed clicks

At 0 AP the only casts are 0 AP moves. Fuel Surge (Gas, Balloom) and AP Reserve (Staked, Rattlestake) are the ones that move a bar. A 1 AP move does nothing for a body sitting at 0.

At 2 AP, under a maintained leak, the body can chip and then fall to 0, or pass and stay at 2. Neither pays a 4 AP move.

Swaps move the slot's AP and clear the outgoing window. They do not clear the leaker's next cast. Bench recharge ticks. Bench AP does not. Cleanse does not remove Drained [C13] and does not remove a Memory Leak window.

Typed damaging moves at 2 AP or less, in real learn pools [C61] [C62]:

| Type | Has a typed damage click at ≤2 AP | What it is |
| --- | --- | --- |
| Node | No | Validape's chip is Snap Strike, typeless, 2 AP, power 30 |
| Burned | No | Cinder Burst is typed and costs 3 [C46]. The 2 AP click in the pool is typeless baseline shot |
| Gas | Yes | Gas Bump |
| Block | Yes | Stone Barrage |
| Encrypted | Yes | Cipher Shot |
| Frozen | Yes | Frost Shot, Frost Shards |
| Forked | Yes | Chain Fork, Rapid Strike |
| Mined | Yes | Ore Strike and others |
| Staked | Yes | several, plus AP Reserve at 0 |
| Compressed | Yes | Lean Hit, Zipped Strikes |
| Bot | Yes | Blade Flurry, Multi-Thread Shot |
| Glitched | Yes | Chaos Strike |
| Shard | Yes | Seam Cut and others |

There is no typed Node damaging move at 2 AP anywhere in `Moves.csv`. Coordinated Shot is 3 AP. Hammer Sync is 4. Adding a new move id would fight the learn-pool plan, which says existing moves only. Pulling Hammer Sync would remove the finisher. Snap Strike already covers the turns a leaked Node has 2 AP.

Burned can be fixed without a new id and without a pull. Cinder Burst is already in Cinislug's pool [C57], Common, power 15, 2–4 hits. **PROPOSAL:** set its AP cost from 3 to 2. Rarity stays Common, so the band is unchanged: Ember Wall stays the unique Legendary signature, Kindling stays the one step above the Common body. Power 15 is a step above Frost Shards and Chain Fork, which are power 12 at 2 AP. If that feels hot, drop the power to 12 in the same change. Default in the questions: cost 2, power stays 15.

## (e) Bot identity

Bot is the denial type. The denial Bot actually learns is Packet Skim: 1 AP, drain 1, Common, on the starter Kitsauto [C54]. Fox Feint stays the only Epic, so the starter rule holds [C60]. Skim on its own is the turn-4 hammer [C73]. That is the right size.

The tools that would make Bot oppressive are unpooled. Thread Lock is Legendary. MEV Extract is Rare. Putting Thread Lock on Kitsauto would make it the new top rarity and Fox Feint would stop being the unique top. The learn-pool plan says to leave Kitsauto's membership. Same for Validape [C60]. Do not add either move.

What outruns Bot is not another Bot move. Keyper's Memory Leak removes 2 after regen, every turn, and the user also takes Speed +2. Transaction Tax removes the same 1 as Skim and pays Deghoul the point, so the denier banks a 4 AP move while the victim does not [C88]. Non-Bot denial should be the rarer, smaller effect. Tax as a steal is the stronger one. Leak as an every-turn −2 is the lock.

Meme has no chemistry. Nothing in this round gives Meme a denial move. Double or Nothing stays unpooled.

## (f) Stacking

Order used by the sim, matching the engine: the move resolves, then chemistry, then at end of turn the status ticks (Drained, MEV, Pumped). Memory Leak's −2 is the next turn's start, after regen. A wall's AP drain resolves when the hit consumes the block, which is during the move, after the attacker has paid.

Blade Flurry into Validape is 54 at the fixed roll 0.925, chemistry ×1.2, and it applies Drained [C94] [C64]. Maintained leak plus that one Drained is the zero lock in the table [C78]. A second flurry refreshes Drained. It does not add a second −1. `ChemistryResolver` sets the existing duration back to the default.

Focus fire matters only when someone is hitting. Two deniers who both spend the turn on Tax and Skim leave the partner free. Ember plus Leak, with Cinislug's wall on recharge, still kills, because Cinder Burst comes out on the gap turn. The wall drain itself removed 0 in that trace [C107]. The hammer never became legal.

Armor, as proposed, is a flat number after the wall percent and before the 10% floor. It does not add or remove AP. A wall still grants at most 1 AP. Ice Wall's text already says max 1.

## (g) Sims and four endgames

Damage is `floor(max((power × atk/def × roll + 1) × chemistry × conditional, power × atk/def × 0.10))`, minimum 1, IV 0, roll 0.925 unless a row says minimum 0.85, no crit. Trials: 1200, seed `1000 + sum of character codes × 17`. Traces are seed 7 with the fixed roll.

### 1. Two 2 AP hits with riders beat a 4 AP heavy

Crackett (Shard, speed 92) clicks Seam Cut. Validape (Node, speed 60, HP 156) passes until Hammer Sync is payable. Seam Cut is power 46, 2 AP, ×1.25 while the user is faster [C47], and Shard into Node is ×1.2 plus Leaked on the defender [C65].

One hit is 92. Two hits are 184, over 156. The minimum roll is 85, and two of those are 170, still over 156 [C90].

Turn 1. Both regen to 2. Crackett is faster. Seam pays 2, deals 92, applies Leaked. Validape is at 64 HP, then acts and pays the Leaked tax of 15 [C22], down to 49, and passes at 2 AP. Turn 2. Crackett seams again for 92. Validape had 49 and had just regenerated to 4 AP, which would have paid the hammer. The hit KOs first. Match ends on turn 2. Hammer never paid [C100].

The riders are doing the work. Without the ×1.25 the hit is 74. Without chemistry it is 77. With neither it is 61, and two plain hits are 122, which leaves Validape alive to hammer [C91]. Chemistry plus the Leaked tax also KOs without the speed bonus: 74 then 15 on turn 1 leaves 67, and 74 on turn 2 finishes. The real move has both.

Seam into Deghoul is 128 against 106 HP, a one-shot even before a low roll (minimum 118) [C92] [C95]. That pair cannot be the two-hit example.

In 1200 trials with accuracy, crits, and hit variance, Crackett wins 99.4%, the match ends on turn 2, and the hammer still gets out 9.7% of the time, on turn 2 when a seam misses or rolls short [C111].

### 2. The heavy gets one cast and still loses

Kitsauto clicks Blade Flurry (2 AP, three hits of power 12). Kodiax passes until Deep Dig (4 AP, power 72) is payable. Kitsauto is faster (82 vs 46). Bot into Mined is ×1.2 and BacklashType, which the status row calls a token with no contact damage [C66].

One flurry is 60. Two are 120, and Kodiax has 124, so the heavy is still legal. Three flurries are enough [C93].

Turn 1. Flurry 60. Kodiax at 64 HP passes at 2 AP. Turn 2. Flurry 60. Kodiax at 4 HP, now holding 4 AP, pays Deep Dig for 69. Kitsauto goes from 139 to 70 [C94]. Turn 3. The first hit of the next flurry KOs Kodiax. Hammer (Deep Dig) paid on turn 2. Denial side wins [C101].

In 1200 trials Kitsauto wins 100%. The heavy lands 70.2% of the time, median turn 2, and the match ends on turn 3 [C112]. The other 29.8% are crits or high rolls that KO Kodiax before the 4 AP is paid. Hammer Sync into Kitsauto is 77, minimum 71, against 139 HP, so one hammer does not KO the flurry user [C94].

### 3. The true lock

Validape passes until Hammer Sync is payable. Keyper leaks every turn. Kitsauto flurries every turn. Both are real pool moves.

Turn 1. Flurry 54 and Drained. Validape at 102 HP. Keyper leaks. Validape passes at 2. Turn 2. The window fires. Drained ticked at the end of turn 1, so action AP is 1. Flurry 54, HP 48. Pass at 1. Turn 3. The window fires. Action AP is 0. Flurry KOs. Hammer never paid. Match ends turn 3 [C102].

1200 trials: denial side 100%, heavy 0%, median end turn 3 [C110]. This is the endgame the lock is about. One finisher, empty bank, two bodies left on the other side, one of them spending the turn on denial and the other hitting.

A voluntary swap on turn 2, after Validape has already snapped once and is at 0, sends Crackett in at 0. Keyper's leak that same turn resolves onto Crackett, because the slot changed [C103]. Crackett's next action is at 0. The swap does not buy a turn.

If Validape instead dies on turn 3 and Crackett inherits 0, the corpse's window is cleared. Crackett's first action is at 2, and Seam Cut KOs Keyper (the trace pays the seam down to 0 AP) [C104]. Then Kitsauto kills Crackett. Denial side still wins 99.8% of 1200 trials, victim side 0.2%, heavy 0% [C113]. The replacement got a chip, not a hammer.

**PROPOSAL rejected:** give the KO replacement `max(corpse AP, 2)`. In the same match that entry is 2, regen makes the action 4, and Seam leaves 2 AP still in the bar [C105]. Victim-side wins go from 0.2% to 32.2%, denial side 67.8% [C114]. That is a large swing for a "don't enter at 0" patch, and a Hammer user would be legal on the entry turn. Leave the inherit rule. The window clear already hands the replacement a 2 AP action.

### 4. A large bank gets the hammer off and still loses the 1v2

Validape opens at 8 AP, alone, against the same leak and flurry.

Turn 1. Flurry 54, HP 102. Leak is armed for next turn. Hammer pays and KOs Keyper for 130 (Keyper has 102). Validape is left at 6 AP. Turn 2. The window that was already armed still fires. Action AP is 5. Flurry 54, HP 48. Hammer into Kitsauto for 77, Kitsauto at 62. Turn 3. Flurry KOs Validape. Match ends turn 3 [C106].

1200 trials: the heavy lands 100% of the time, on turn 1. Denial side wins 98.5%. Median end is turn 3 [C116]. A bank saves the cast. It does not save a lone body against two attackers when one hammer leaves Kitsauto alive.

Tax plus Skim, as the whole opposing side, is the other shape. Validape is zeroed and never pays Hammer. Crackett is free and kills them. Victim side wins 100% of 1200 trials, heavy 0%, median end turn 3 [C115]. A zero lock with no damage dealer is a loss in 2v2.

Ember Wall plus Leak: the snap on turn 1 is mitigated and the drain removes 0, because the snap already spent the last AP [C107]. Validape is then at 0 from the leak. Cinislug's recharge skips the next turn, then Cinder Burst starts the kill. Hammer never fires. Denial side wins 100%, median end turn 10 [C117]. The wall is not the lock. The leak is.

## (h) AI

Current scores, IV 0, no target bonus. Utility starts a status move at +30 at `UtilityStatusAI.cs:89` [C25], adds 15 per status effect at `UtilityStatusAI.cs:92` [C26], adds 10 per point of enemy stage drop, then subtracts the cost once at `UtilityStatusAI.cs:146` [C27]. It does not read DrainAP, StealAP, or ArmUtilityWindow.

Keyper's utility order: Cipher Shot 44.6, Key Rotation 29, Memory Leak 28, System Reset 27, Cipher Burst 15.84 [C120]. The control AI will not keep the lock.

Kitsauto's utility order: Debuff Script 49, Packet Skim 29, Fox Feint 29, Blade Flurry 23.99, Multi-Thread Shot 15.1 [C121]. Aggressive scores a non-damage move at 0.1 at `AggressiveAI.cs:66` [C28], which is why Memory Leak scores 0.1 [C122] and Packet Skim scores 0.1. `ShouldBankForGreaterBlock` passes only for a block above 50% that costs more than 2 AP and is not yet affordable, at `AIHelpers.cs:224` [C29]. It does not pass in order to afford Hammer Sync.

**PROPOSAL**, scoring only, no code in this round. Immediate drains:

- removed = the AP actually taken, so a skim against 0 removes 0
- score = 30 − cost + removed × 18
- +40 if the foe had at least 4 and the drain leaves them under 4
- +25 if they were already at 2 or less and the drain removed something
- if removed is 0, score falls back to 30 − cost

Memory Leak does not change this turn. Regen cancels the −2, so a passer at 4 or more still pays the finisher next turn. Score the lock only while they are already under 4: 30 − 2 + 2 × 18 + 40 = 104. At 4 or more, score 30 − 2 = 28, which loses to Cipher Shot's 44.6, so the AI attacks a body that can already hammer.

Computed from that formula [C123]:

| Move | Foe's AP | Proposed score | Compared with today |
| --- | --- | --- | --- |
| Memory Leak | 2 | 104 | Beats Cipher Shot at 44.6. Holds the lock |
| Memory Leak | 4 or 8 | 28 | Loses to Cipher Shot. The window will not stop the hammer |
| Packet Skim or Tax | 4 | 87 | The drain slips a live finisher. Beats Debuff Script at 49 |
| Packet Skim | 0 | 29 | Whiff. Loses to Debuff Script, so they stop skimming a corpse |

**PROPOSAL** for AggressiveAI: keep damage-per-AP × 10 for hits. Score a denial move as that same scale when the user is faster and the drain is what drops a live 4 AP finisher under 4. Otherwise leave it at 0.1. A leak that only freezes a bar already under 4 should still lose to a KO hit. The utility AI is the one that keeps a lock. The aggressive AI should deny only to stop a heavy that would land this turn.

**PROPOSAL** for AIHelpers: add a pass-for-heavy check beside the greater-block check. Pass only when the heavy is not yet affordable and either its minimum roll KOs the target or its damage per AP is at least 1.15× the best legal 2 AP move. Do not pass just because 4 is unaffordable when the 2 AP hit would KO first. That is the same bar the cost-curve note already used. This round does not change it.

Test names, when the scoring lands in the engine:

- `UtilityAi_ScoresLeakAboveCipherWhenTargetUnderFour` — foe at 2 AP, Memory Leak scores above Cipher Shot.
- `UtilityAi_AttacksWhenTargetAlreadyHasFour` — foe at 4 AP, Cipher Shot scores above Memory Leak.
- `UtilityAi_SkimWhiffLosesToDebuff` — foe at 0 AP, Debuff Script scores above Packet Skim.
- `AggressiveAi_DeniesWhenDrainSlipsALiveHeavy` — user is faster, foe at 4, the 1 AP drain scores above the best chip.
- `Ai_PassesForHeavyOnlyWhenMinRollKillsOrRateIsHigher` — a 2 AP move that KOs is chosen over a pass.

## (i) What works, what doesn't, what could be better

What works. Regen +2 and a ceiling of 10 are readable. Tax alone and Skim alone slip one build of AP and then the hammer arrives on turn 4. Thread Lock and MEV slip one regen and are correctly out of pools. Ember's drain cannot stop a cast that was already paid. Ice Wall's +1 is capped. The wild learn-pool band has 0 failures [C59], and the three starters each have a single move at the top rarity [C60]. A bank of 8 still fires the finisher [C116]. A faster pair of 2 AP hits, with Seam Cut's real riders, kills the banker before that finisher [C100].

What doesn't. Chained Memory Leak is a finisher lock on a passer and a zero lock after one 2 AP spend or after one Drained. The control AI scores that lock at 28 and the stat drop at 49, so it will not play the lock. The card does not say "after regen," does not say it is a window, and does not say a swap clears it. Tax's steal lets Deghoul bank a 4 AP move on the same calendar that holds the victim. A cap of 2 does not fix any of this.

What could be better. A one-turn recharge on Memory Leak, Tax as a drain, AI scores that look at the foe's bar, and card sentences that state those limits. Cinder Burst at 2 AP so Burned has a typed chip. Leave Kitsauto's pool alone. Leave the KO inherit rule alone.

## (j) Ranked proposals

Each one is a **PROPOSAL**. The learn-pool plan is a hard constraint: a wild pool is five moves, three at the body rarity or lower, one at the body rarity or one step up, and the signature is strictly the highest. Starters only need a unique top rarity. Ghost and Cofi stay as they are. Prefer filling an empty move over pulling one. No proposal below pulls a move or adds a pool member, except where it says the membership is unchanged.

1. **Memory Leak gains rechargeTurns 2.** The −2, the Speed +2, the signature, and the window stay. Authored recharge 2 skips the next turn, the same way Ember Wall already does. A passer who is leaked on turn 1 and not on turn 2 pays the finisher on turn 3 [C79]. A body that spends 2 AP on the open turn does not [C80]. The card has to say: after regen, the target loses 2 AP next turn; this is a window, not a status; a swap clears it; cleanse does not; you cannot cast it on the following turn. Files: `Moves.csv` row `memory_leak` (`Moves.csv:95`), and the description string. No pool edit. Band unchanged [C59] [C60]. Test: `MemoryLeak_RechargeSkipsOneTurn_FinisherPaysOnTheOpenTurn` asserts a passer leaked on turn 1 only has at least 4 AP at action time on turn 3, and a passer who spends 2 on turn 2 does not.

2. **Transaction Tax becomes DrainAP:1.** Cost stays 1. Rarity stays Epic. It stays Deghoul's signature [C56]. The victim calendar is unchanged [C89]. Deghoul stops ending the turn at 2, 4, 6, 8, 10 and instead ends at 1, 2, 3, 4, 5, 6 [C88]. Card: "Drain 1 AP from the target. You do not gain it." File: `Moves.csv:71`, utility `StealAP:1` to `DrainAP:1`. Pool membership unchanged, so the band is unchanged. Test: `TaxIsDrain_DeghoulDoesNotGainThePoint` asserts the utility is `DrainAP:1`, Deghoul's flagship is still Transaction Tax, and Kitsauto still has Packet Skim as a drain.

3. **Score denial off the foe's bar.** UtilityStatusAI, AggressiveAI, and AIHelpers, as written in section (h). No AP numbers change. Tests are the five names in that section. `UtilityAi_ScoresLeakAboveCipherWhenTargetUnderFour` is the one that makes the lock a choice the AI can see. `UtilityAi_AttacksWhenTargetAlreadyHasFour` is the one that stops the AI leaking a body that can already hammer.

4. **Do not add a per-target drain cap of 2 as the fix.** Under that cap, Tax plus Skim is still 0 every turn [C81], and Leak plus Skim sits at 1, which still cannot pay 4 [C82]. The earlier tempo note already pointed at recharge instead of the cap. These calendars are the empty-bank endgame, and they come out the same way. Test, if anyone wires the cap anyway: `Cap2_DoesNotFreeTheFinisher` asserts both of those series.

5. **Card text, and the MEV comment.** Memory Leak's sentence is in proposal 1. Thread Lock's card should add "It cannot be applied again on the turn after." The code already sets `threadLockBlockedUntilTurn = turn + 2` [C11]. MEV stays a drain. Change the `StatusId.cs:52` comment from "Steal 1 AP per turn" to a drain [C24], and add "You do not gain that AP" on `Moves.csv:159`. Packet Skim's card should say the 1 AP cost is paid even when the target has 0, because there is no whiff refund [C30]. Test: `CardText_StatesTheLimit` asserts those four strings, and asserts MEV's status row still has EoTAP −1 [C50].

6. **Cinder Burst from 3 AP to 2 AP.** Membership unchanged [C57]. Common stays Common, so Cinislug's band still holds. This is the typed Burned chip the pool does not have [C61]. Power stays 15 unless you also want it in line with Frost Shards at 12. Test: `CinderBurst_CostsTwo_BandUnchanged` asserts APCost 2, Cinislug's flagship still Ember Wall, and the wild band check still passes.

Not proposed, on purpose. No Thread Lock or MEV on Kitsauto. No new Node move. No KO AP floor [C114]. No whiff refund while a 0-lock exists, because a refund makes Skim free once the bar is empty. No change to Armor's AP, because Armor has no AP. Withdrawn moves from the wall round stay withdrawn.

## (k) Consistency

- `StatusId.cs:52` says MEV Extracted steals. `Statuses.csv` ticks −1 and the move text says drain [C24] [C43] [C50]. The code drains.
- Memory Leak's description omits "after regen," "not a status," and "a swap clears it" [C40] [C16].
- Thread Lock's description omits the one-turn gap [C44] [C11].
- Transaction Tax says steal, and Steal does pay the user [C42] [C08]. If proposal 2 lands, the card and the utility have to change together.
- Guard Posture's text still describes a mitigation window. Flat Armor is not shipped. Do not write Armor numbers onto that card until the armor round is decided.
- `tempo_2v2_sim.py` will mis-count any future wall or Memory Leak swap test until it learns `BlockWindow` and clears turn-start windows [C31] [C32].

## (l) Questions only Cronus can answer

1. Does Memory Leak stay a window, with rechargeTurns 2, or become a cleansable status? Default: keep the window, add the recharge, rewrite the card. The recharge is what frees a passer on turn 3 [C79].
2. Does Transaction Tax become a drain? Default: yes. The victim does not feel a difference [C89]. Deghoul stops banking a free heavy [C88].
3. If a drain removes 0, is the cost refunded? Default: no. A refund makes Skim free against an empty bar, which is the lock. Revisit only after proposal 1 is in and a leak cannot hold a bar at 0 by itself.
4. Add a drain cap of 2 per target per turn? Default: no [C81] [C82].
5. Should a KO replacement enter with at least 2 AP? Default: no. The corpse's window is already cleared, so 0 inherited becomes a 2 AP action after regen. Flooring the entry at 2 makes that action 4 and swung one match from 0.2% to 32.2% [C114].
6. Should MEV Extracted become a steal? Default: no. Fix the comment. Leave it out of pools [C58].
7. May the AI pass to bank a 4 AP heavy? Default: only when the heavy's minimum roll KOs, or its damage per AP is at least 1.15× the best legal 2 AP move. A 2 AP hit that would KO comes first.
8. Add a typed Node move at 2 AP? Default: not this round. No such move exists. Snap Strike stays. Do not pull Hammer Sync.
9. Drop Cinder Burst from 3 AP to 2? Default: yes, power stays 15. Say if you also want power 12.
10. Do Armor's flat numbers stay as proposed in the armor note? Default: yes, and they do not change AP. This round does not decide them.

## Check

`ap_denial_endgame_check.py` re-reads the snapshot and re-runs the sim. Tag coverage [C130]. File cites [C131]. Tally: PASS 107 / FAIL 0.
