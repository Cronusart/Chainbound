# Move cost curve, v2

Proposals only. Nothing here is applied. Source of truth is the 2026-10-20 02:15 snapshot plus the round-13 design notes and sims. The 2026-10-17 cost-curve note is the thing this round builds on. It is not repeated.

Checker: 114 claims, all passing, against the unzipped snapshot. Each claim id below is that check. Sim: 131 cells, 320 trials each, in `move_cost_curve_v2_sim.py`. Win rate is the attacking side. A KO median of 99 means that side did not record a KO inside the horizon.

## Key findings

The 10-17 spine is gone and the AP ladder is what landed instead. Zero damaging moves still sit at power 64, accuracy 95, one hit, 2 AP (C05). The catalog now has ten 4 AP moves and one 6 AP move, and no 5 AP move (C06, C07, C08). Of the 51 number retunes in that note, 0 landed on the nose, 8 moved part of the way, and 43 did not land. Chainlings still open at 0 AP and gain 2 per turn, cap 10 (C11), so a 1–2 AP move is a turn-1 click and a 3–4 AP move is a turn-2 bank (C59). On the moves a species can actually learn, the Rare band sits at 23.82 SPR per AP. The full 2 AP tier, typeless chips included, sits at 17.09. The 4 AP tier sits at 18.25. Banking Hammer Sync does not beat clicking Snap Strike (78.4% vs 79.4%). Banking Cryo Ray does not beat Frost Shot (99.7% vs 100.0%). Banking Deep Dig loses a line the 2 AP Ore Strike wins (42.2% vs 81.6%). Banking Gas War loses to Gas Bump (63.8% vs 78.4%) and the heavy KO rate in that cell is 0.0%. Gas War is also the worst learnable damaging rate, 10.48 SPR per AP, because the next-move surcharge is priced as a full AP point against the user (C54, C50). The only cover that can be clicked on turn 1 today is Ember Wall at 2 AP. Blockade, Ice Wall, Compound Guard, and Barrier Wall are 3 AP (C51), so a turn-1 Seam Cut is not answered by Blockade. Walls v2 is still a proposal. Under that proposal the same wall team absorbs about one melee hit and several melee win rates fall by 25 points or more. The three priority attacks are not absorbed. They also do not remove the wall.

## Top 5 recommendations

All five are **PROPOSAL**. AP is set from the fight, not from a formula. None of them add or remove a learn-pool member, so the 3+1+strict-signature rule in `_plans/learn_pool_rarity_6d7ff36f.plan.md` stays intact (C38). Ghost and Cofi stay skipped.

1. **PROPOSAL. Gas War drops the +1 next-move surcharge. Power stays 72. AP stays 4. Rarity stays Uncommon.** Deghoul and Balloom already learn it under Transaction Tax and Fuel Surge (C42). The surcharge is why Gas Bump, a Common, beats it per AP (16.57 vs 10.48, C50). Expected damage into Kitsauto is 64.63 on a 139 HP body (C48), so this is a chunk, not a KO, and the bank cell already loses to two Bumps. Touch `Moves.csv` row `volatile_shot` (line 72), `Move_volatile_shot.asset`, and the card sentence. Test `MoveCostCurveV2Tests.GasWar_HasNoNextMoveSurcharge`: utility does not contain `NextMoveAPSurcharge`, description does not say the next move costs more, AP is 4, power is 72, and both Gas pools still list it as the Uncommon under an Epic or Legendary signature.
2. **PROPOSAL. Bulwark Blow goes from 3 AP to 2 AP. Power stays 50. Rarity stays Epic. It still strikes with MeleeDef.** It is the only learnable Epic damaging move, at 14.48 SPR per AP against a 3 AP tier median of 13.86 (C46, C47). Stone Barrage, a Common, beats it per AP (C50). It cannot be clicked on turn 1 (C59). The solo cell wins 20.3%. Blockwyrm is Legendary. Immutable Ledger stays the unique top rarity, and Bulwark Blow stays the Epic under it. Touch `Moves.csv` row `bulwark_blow` and `Move_bulwark_blow.asset`. Test `MoveCostCurveV2Tests.BulwarkBlow_CostsTwo`: AP is 2, power is 50, rarity is Epic, attack stat override is MeleeDef, Blockwyrm's signature is still Immutable Ledger, and no second Legendary is in that pool.
3. **PROPOSAL. Transaction Tax drains 1 AP and does not give it to Deghoul. Cost stays 1. Rarity stays Epic. It stays the signature.** Packet Skim removes 1 per 1 AP and grants 0 (C28, removed-per-AP 1.0). Tax removes 1 per 1 AP and grants 1 (C53, removed-per-AP 1.0, gained 1.0). Same removal price, better net, and it is Gas rather than Bot. Gas keeps an affordable denial click, which is what the type is allowed to have. It stops out-trading Bot. Touch `Moves.csv` row `transaction_tax`: `StealAP:1` becomes `DrainAP:1`, and the card says "Cut the target's AP by 1." Test `MoveCostCurveV2Tests.TransactionTax_DrainsWithoutRefund`: utility is `DrainAP:1`, AP is 1, rarity is Epic, Deghoul's pool still has Tax as the unique top rarity, and Kitsauto's pool still has Packet Skim at drain 1.
4. **PROPOSAL. Cryo Ray's Speed −2 becomes certain. Power stays 80. AP stays 4. Chance goes from 25% to 100%.** Frost Shot is Rare, 2 AP, 23.82 SPR per AP, and it already carries a 25% Speed −1. Cryo Ray is Legendary, 4 AP, 18.28 SPR per AP (C50). The Gilarime light line wins 100.0% with a KO median of turn 2. The bank wins 99.7% and the heavy KOs in 94.7% of trials, so more power is not the missing piece. Speed is priced at 0 HP in the curve (C45). This proposal does not move Cryo Ray's SPR. It makes the turn you spent banking buy a slow the 2 AP shot does not guarantee. Touch `Moves.csv` row `cryo_ray` stage `Speed:-2:0.25` to `Speed:-2:1.0`, and the card. Test `MoveCostCurveV2Tests.CryoRay_SpeedDropIsCertain`: stage is Speed −2 at chance 1, AP is 4, power is 80, and Gilarime's pool still has Cryo Ray as the only Legendary.
5. **PROPOSAL. Do not republish the 10-17 power table.** The ladder already moved the weak 3 AP hits down to 2 AP and the real heavies up to 4 AP (P35–P45, P43, P44). Putting power back to 64 on Frost Shot, Solid Strike, Scorch Shot, and Coordinated Shot (P48–P51) would stack a damage buff on cards that are already the efficient band. Pulse Shot stays 26 (C21). That decision is what keeps the typeless common under the typed common.

## (a) What changed since 10-17

The old note measured a catalog whose damaging costs stopped at 3 AP, whose typed commons shared power 64, and whose All-In, Recoil Strike, Volatile Shot, and Crystal Bash did not pay their written costs. This snapshot is past that.

- AP regen is still +2 from 0, cap 10 (C11). Bench recharge ticks (C12, P53). Kindling is 6% of max HP (C15). Chemistry is 1.2 / 0.8 and Meme has no cells (C34, C35).
- The power-64 spine has no remaining rows (C05). Pulse Shot is power 26, AP 2, field index 24.70 (C21, C37).
- Crystal Bash is power 44, AP 2, and strikes with MeleeDef. It is in no learn pool (C41). A power of 0 still returns 0 before the override is read (C14), so that path is fixed by giving the move a power, which this row now has.
- All-In is power 98, AP 4, Epic, recharge 2, and spends the rest of the bar. It is in no pool (C19, C20).
- System Reset, Hype Burst, and Crystal Extraction are built. Uplink replaced AP Router on Validape. Purge Protocol cleanses (C22, C24, C25, C26, C27). Those stay as decided.
- Packet Skim is the Bot drain that landed: 1 AP, drain 1, Kitsauto only. Batch Debit, Cut Ledger, Rime Jab, and Vested Strike are still absent (C28, C29, C30, C31, C32, P52).
- Gas Spike and FUD stages revert with the status (P54). Rallying Slam and Interest Strike refund only when Def stage is strictly above Atk stage, so a fresh tie does not refund (C18, P55). The note had asked for Def stage at least +1. That part did not land.
- Streamlined Flow cannot cut a positive cost below 1 (C16). Fee Collector drains 1 from the first opposing actor who is ahead on AP, and the drain call has no per-turn cap (C13, C44). Fee Collector is not in a learn pool (C57).

The sim copy used here diverges from the attached round-13 file on four engine facts, and the header of `tempo_2v2_sim.py` says so. Streamlined floors at 1. An override attack stat is read. Fee Collector drains. All-In empties the bar after the printed cost, and the damage stays flat. Those four match the snapshot. The battle rules are still a model, not the Unity resolver.

## (b) Did the 10-17 proposals land?

Landing is judged on this snapshot. "Partly" means the live number moved off the old value and did not reach the proposed value. "Not landed" means it is still the old number, or the note said to leave a number and the ladder moved it anyway. Of P01–P51: 8 partly, 43 not landed, 0 landed. P52 is partly (Packet Skim, not Batch Debit). P53 and P54 landed. P55 is partly.

The eight partial power moves all went down, not up onto the old spine: Rapid Strike, Lean Hit, Seam Cut, AP Siphon, Cinder Burst, Blade Flurry, Zipped Strikes, Stone Barrage (P01, P02, P25–P29, P32). The AP rows the note wanted left at 3 are now 2 (Frost Shards, Chain Fork, Stone Barrage, Body Check, Interest Strike) or 4 (Fork Split, Shard Scatter, Ember Shower, Automation Burst, Deep Dig). Hammer Sync and Cryo Ray are 4, not the 2 the note wanted to keep (P35–P45). Bulwark Blow is still 3 (P34). Bean Beam is still 2 (P33). Chaos Strike's random mean is still 1.125 (P04).

No test covers many of the power rows. Where a test exists it is named in the table. System Reset's "skip your next action" has no test that the recharge skips only that move (C23).

| Id | Move | Field | 10-17 now | Proposed | Current | Status | Test | CSV line |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| P01 | rapid_strike | power | 52 | 36 | 26 | partly | no test | 117 |
| P02 | lean_hit | power | 46 | 34 | 26 | partly | no test | 153 |
| P03 | baseline_jab | power | 16 | 22 | 16 | not landed | CaptureDossierLoadoutTests.cs | 4 |
| P04 | chaos_strike | random_mean | 1.125 | 1.0 | 1.125 | not landed | no test | 168 |
| P05 | refined_striker | power | 42 | 68 | 42 | not landed | no test | 12 |
| P06 | refined_shot | power | 38 | 68 | 38 | not landed | no test | 17 |
| P07 | refined_thrust | power | 34 | 62 | 34 | not landed | no test | 15 |
| P08 | baseline_striker | power | 30 | 48 | 30 | not landed | CombatCsvIntegrityTests.cs | 2 |
| P09 | baseline_shot | power | 26 | 48 | 26 | not landed | AuditReconciliationV1Tests.cs | 5 |
| P10 | baseline_thrust | power | 26 | 44 | 26 | not landed | no test | 10 |
| P11 | baseline_slam | power | 30 | 64 | 30 | not landed | ApCostLadderTests.cs | 3 |
| P12 | baseline_snipe | power | 30 | 60 | 30 | not landed | ApCostLadderTests.cs | 6 |
| P13 | baseline_multi_strike | power | 8 | 16 | 8 | not landed | ApCostLadderTests.cs | 11 |
| P14 | guardbreaker_combo | power | 10 | 23 | 10 | not landed | ApCostLadderTests.cs | 16 |
| P15 | overdraw_snipe | power | 46 | 85 | 46 | not landed | no test | 18 |
| P16 | refined_slam | power | 48 | 90 | 48 | not landed | no test | 13 |
| P17 | refined_volley | power | 22 | 39 | 22 | not landed | no test | 19 |
| P18 | refined_blast | power | 28 | 46 | 28 | not landed | no test | 20 |
| P19 | refined_barrage | power | 12 | 18 | 12 | not landed | no test | 21 |
| P20 | baseline_volley | power | 16 | 28 | 16 | not landed | no test | 7 |
| P21 | baseline_barrage | power | 10 | 11 | 10 | not landed | no test | 9 |
| P22 | baseline_blast | power | 22 | 33 | 22 | not landed | no test | 8 |
| P23 | quick_lunge | power | 26 | 46 | 26 | not landed | no test | 14 |
| P24 | multi_thread_shot | power | 12 | 20 | 12 | not landed | ApCostLadderTests.cs | 161 |
| P25 | seam_cut | power | 52 | 64 | 46 | partly | WildBattleKitComposerTests.cs | 207 |
| P26 | ap_siphon | power | 56 | 70 | 54 | partly | no test | 76 |
| P27 | cinder_burst | power | 18 | 24 | 15 | partly | CombatIssuesRemediationV2Tests.cs | 107 |
| P28 | blade_flurry | power | 18 | 24 | 12 | partly | ApCostLadderTests.cs | 202 |
| P29 | zipped_strikes | power | 26 | 32 | 18 | partly | no test | 155 |
| P30 | frost_shards | power | 12 | 25 | 12 | not landed | ApCostLadderTests.cs | 114 |
| P31 | chain_fork | power | 12 | 25 | 12 | not landed | ApCostLadderTests.cs | 123 |
| P32 | stone_barrage | power | 16 | 30 | 14 | partly | ApCostLadderTests.cs | 85 |
| P33 | civet_beam | ap | 2 | 1 | 2 | not landed | ApCostLadderTests.cs | 205 |
| P34 | bulwark_blow | ap | 3 | 2 | 3 | not landed | WildBattleKitComposerTests.cs | 206 |
| P35 | frost_shards | ap | 3 | 3 | 2 | not landed | ApCostLadderTests.cs | 114 |
| P36 | chain_fork | ap | 3 | 3 | 2 | not landed | ApCostLadderTests.cs | 123 |
| P37 | stone_barrage | ap | 3 | 3 | 2 | not landed | ApCostLadderTests.cs | 85 |
| P38 | baseline_slam | ap | 3 | 3 | 2 | not landed | ApCostLadderTests.cs | 3 |
| P39 | fork_split | ap | 3 | 3 | 4 | not landed | ApCostLadderTests.cs | 118 |
| P40 | shard_scatter | ap | 3 | 3 | 4 | not landed | ApCostLadderTests.cs | 187 |
| P41 | ember_shower | ap | 3 | 3 | 4 | not landed | ApCostLadderTests.cs | 104 |
| P42 | automation_burst | ap | 3 | 3 | 4 | not landed | ApCostLadderTests.cs | 162 |
| P43 | hammer_sync | ap | 2 | 2 | 4 | not landed | ApCostLadderTests.cs | 198 |
| P44 | cryo_ray | ap | 2 | 2 | 4 | not landed | ApCostLadderTests.cs | 200 |
| P45 | deep_dig | ap | 3 | 3 | 4 | not landed | ApCostLadderTests.cs | 133 |
| P46 | interest_strike | power | 18 | 30 | 18 | not landed | ApCostLadderTests.cs | 147 |
| P47 | interest_strike | ap | 3 | 3 | 2 | not landed | ApCostLadderTests.cs | 147 |
| P48 | solid_strike | power | 64 | 64 | 52 | not landed | no test | 82 |
| P49 | coordinated_shot | power | 64 | 64 | 56 | not landed | ApCostLadderTests.cs | 66 |
| P50 | scorch_shot | power | 64 | 64 | 52 | not landed | CombatIssuesRemediationV2Tests.cs | 99 |
| P51 | frost_shot | power | 64 | 64 | 52 | not landed | no test | 112 |

## (c) Live curve

Scope is every damaging move a current species pool actually contains: 39 moves with power above 0 (C46). Catalog size is 211 moves, 75 damaging, 31 passive (C01, C02). Non-passive AP counts: 0 AP × 4, 1 AP × 54, 2 AP × 86, 3 AP × 25, 4 AP × 10, 6 AP × 1 (C03). Rarity counts: Common 62, Uncommon 43, Rare 43, Epic 31, Legendary 22, Mythic 10 (C04).

### How a move is priced

SPR method: norm strike uses atk=def=60 (ratio 1), floor((P*u+1)*mult) with u from 0.85 to 1.0 in 21 steps, crit 0.0625 x1.5, times expected hits, accuracy, spread x2. AP point=23.82 = median 2 AP single norm strike / 2. Drain x0.6, gain x1, steal both. Stage credit is half a reference hit per stage of a damage stat, one future hit. Speed/accuracy/evasion and priority are tempo, priced at 0 HP. Median HP=124 melee def=53 ranged def=48. (C45).

Norm SPR is that strike times expected hits, times accuracy, times 2 when the move hits both foes. Rider HP is added only for payloads the resolver runs on a fresh turn: a drain is worth 0.6 of an AP point, a gain is worth 1, a steal is both. One stage of a damage stat is half a reference hit, once. Speed, accuracy, evasion, and priority add 0 HP. They show up in the sim, not in the curve. A self-penalty such as Gas War's surcharge is negative rider HP.

The AP point is 23.82, half of the median 2 AP single-target norm strike (47.64). Median body: HP 124.0, melee attack 56.0, melee defense 53.0, ranged attack 47.0.

Tier median SPR per AP, learnable damaging with power above 0, n named in C46: AP1 24.13, AP2 17.09, AP3 13.86, AP4 18.25 (C46). Rarity medians: Common 14.99 (n=17), Uncommon 16.58 (n=7), Rare 23.82 (n=8), Epic 14.48 (n=1), Legendary 25.64 (n=5), Mythic 29.87 (n=1) (C47). Rare sits above Uncommon because eight Rare 2 AP singles cluster at 23.82. Epic is one move, Bulwark Blow, and it is the low one. Mythic is Chaos Strike alone.

Outliers are learnable damaging moves whose SPR per AP is at least 20% off their AP-tier median, keeping the best and worst holders so both directions show.

| Move | Type | Rarity | AP | SPR | SPR/AP | Tier med | Ratio | Why | Holders |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline_striker | None | Common | 2 | 27.68 | 13.84 | 17.09 | 0.81 | low | deghoul,hopoch,kreechank,validape |
| frost_shot | Frozen | Rare | 2 | 47.64 | 23.82 | 17.09 | 1.394 | high | gilarime |
| chaos_strike | Glitched | Mythic | 2 | 59.73 | 29.87 | 17.09 | 1.748 | high | kreechank |
| cinder_burst | Burned | Common | 3 | 55.75 | 18.58 | 13.86 | 1.341 | high | cinislug |
| baseline_shot | None | Common | 2 | 24.13 | 12.06 | 17.09 | 0.706 | low | cinislug,cofi_lootwak,forkrook,minefolk,rattlestake |
| baseline_jab | None | Common | 1 | 14.99 | 14.99 | 24.13 | 0.621 | low | crackett,rattlestake |
| seam_cut | Shard | Rare | 2 | 42.18 | 21.09 | 17.09 | 1.234 | high | crackett |
| volatile_shot | Gas | Uncommon | 4 | 41.93 | 10.48 | 18.25 | 0.574 | low | balloom,deghoul |
| fork_split | Forked | Legendary | 4 | 102.56 | 25.64 | 18.25 | 1.405 | high | forkrook |
| civet_beam | Staked | Uncommon | 2 | 22.79 | 11.4 | 17.09 | 0.667 | low | cofi_lootwak |
| yield_spike | Staked | Legendary | 2 | 51.28 | 25.64 | 17.09 | 1.5 | high | hopoch |
| stake_strike | Staked | Rare | 2 | 47.64 | 23.82 | 17.09 | 1.394 | high | hopoch |
| deep_reap | Mined | Legendary | 2 | 53.1 | 26.55 | 17.09 | 1.554 | high | hopoch,minefolk |
| partition_shot | Shard | Rare | 2 | 47.64 | 23.82 | 17.09 | 1.394 | high | marrowhead |
| baseline_snipe | None | Common | 2 | 26.23 | 13.11 | 17.09 | 0.767 | low | marrowhead |
| ore_strike | Mined | Rare | 2 | 47.64 | 23.82 | 17.09 | 1.394 | high | kodiax |
| baseline_slam | None | Common | 2 | 24.77 | 12.38 | 17.09 | 0.724 | low | balloom,kodiax |
| solid_strike | Block | Rare | 2 | 47.64 | 23.82 | 17.09 | 1.394 | high | kodiax |

High side, same shape: the 2 AP typed rares (Frost Shot, Ore Strike, Solid Strike, Stake Strike, Partition Shot) plus Yield Spike, Deep Reap, Seam Cut, Lean Hit, Rapid Strike, Chaos Strike, Fork Split, and Cinder Burst. Low side: the typeless commons (Pulse Shot, Body Check, Focus Shot, Snap Strike, Quick Jab), Bean Beam, and Gas War.

Same-type rivals, lower rarity at or under the higher rarity's AP, rate at least 95% of the higher card:

| Type | Lower | Rarity | AP | SPR | Higher | Rarity | AP | SPR | Ratio |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Bot | blade_flurry | Common | 2 | 34.19 | multi_thread_shot | Uncommon | 2 | 34.15 | 1.001 |
| Compressed | zipped_strikes | Common | 2 | 33.62 | lean_hit | Rare | 1 | 24.13 | 1.393 |
| Encrypted | cipher_burst | Common | 3 | 39.7 | cipher_shot | Uncommon | 2 | 36.82 | 1.078 |
| Forked | chain_fork | Common | 2 | 34.19 | rapid_strike | Rare | 1 | 24.13 | 1.417 |
| Compressed | precision_strike | Common | 2 | 32.92 | lean_hit | Rare | 1 | 24.13 | 1.364 |
| Mined | deep_dig | Uncommon | 4 | 65.75 | deep_reap | Legendary | 2 | 53.1 | 1.238 |
| Mined | deep_dig | Uncommon | 4 | 65.75 | ore_strike | Rare | 2 | 47.64 | 1.38 |
| Block | solid_strike | Rare | 2 | 47.64 | bulwark_blow | Epic | 3 | 43.45 | 1.096 |
| Forked | recoil_strike | Uncommon | 4 | 72.89 | rapid_strike | Rare | 1 | 24.13 | 3.021 |

The per-AP screen in C50 is the stricter one. Five pairs qualify. Frost Shot beats Cryo Ray. Blade Flurry ties Multi-Thread Shot. Gas Bump beats Gas War. Stone Barrage and Solid Strike both beat Bulwark Blow. The wider table above also shows total-SPR cases where a cheaper card's whole hit is larger because the higher card is a 1 AP tick (Zipped Strikes vs Lean Hit, Chain Fork vs Rapid Strike). Those are not the same complaint. A 2 AP move should deal more total damage than a 1 AP move. It should not deal more per AP than a higher-rarity card of the same type that costs the same or more.

| Move | Type | Rarity | AP | Pow | Hits | Shape | Prio | Rc | Norm | Rider | SPR | SPR/AP | Legal | Holders | Rider text |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| lean_hit | Compressed | Rare | 1 | 26 | 1-1 | single |  | 0 | 24.13 | 0.0 | 24.13 | 24.13 | 1 | humpacked | — |
| rapid_strike | Forked | Rare | 1 | 26 | 1-1 | single | Y | 0 | 24.13 | 0.0 | 24.13 | 24.13 | 1 | forkrook | priority |
| baseline_jab | None | Common | 1 | 16 | 1-1 | single | Y | 0 | 14.99 | 0.0 | 14.99 | 14.99 | 1 | crackett,rattlestake | priority |
| chaos_strike | Glitched | Mythic | 2 | 58 | 1-1 | single |  | 0 | 59.73 | 0.0 | 59.73 | 29.87 | 1 | kreechank | random 1.5|0.75 |
| deep_reap | Mined | Legendary | 2 | 58 | 1-1 | single |  | 0 | 53.1 | 0.0 | 53.1 | 26.55 | 1 | hopoch,minefolk | LifestealPercent |
| yield_spike | Staked | Legendary | 2 | 56 | 1-1 | single |  | 0 | 51.28 | 0.0 | 51.28 | 25.64 | 1 | hopoch | — |
| frost_shot | Frozen | Rare | 2 | 52 | 1-1 | single |  | 0 | 47.64 | 0.0 | 47.64 | 23.82 | 1 | gilarime | Speed -1 25% foe |
| ore_strike | Mined | Rare | 2 | 52 | 1-1 | single |  | 0 | 47.64 | 0.0 | 47.64 | 23.82 | 1 | kodiax | — |
| partition_shot | Shard | Rare | 2 | 52 | 1-1 | single |  | 0 | 47.64 | 0.0 | 47.64 | 23.82 | 1 | marrowhead | FrozenAssets |
| solid_strike | Block | Rare | 2 | 52 | 1-1 | single |  | 0 | 47.64 | 0.0 | 47.64 | 23.82 | 1 | kodiax | Speed -1 25% foe |
| stake_strike | Staked | Rare | 2 | 52 | 1-1 | single |  | 0 | 47.64 | 0.0 | 47.64 | 23.82 | 1 | hopoch | — |
| seam_cut | Shard | Rare | 2 | 46 | 1-1 | single |  | 0 | 42.18 | 0.0 | 42.18 | 21.09 | 1 | crackett | — |
| cipher_shot | Encrypted | Uncommon | 2 | 40 | 1-1 | single |  | 0 | 36.82 | 0.0 | 36.82 | 18.41 | 1 | keyper | CipherLocked |
| blade_flurry | Bot | Common | 2 | 12 | 3-3 | multi |  | 0 | 34.19 | 0.0 | 34.19 | 17.1 | 1 | kitsauto | — |
| chain_fork | Forked | Common | 2 | 12 | 2-4 | multi |  | 0 | 34.19 | 0.0 | 34.19 | 17.1 | 1 | forkrook | — |
| frost_shards | Frozen | Common | 2 | 12 | 2-4 | multi |  | 0 | 34.19 | 0.0 | 34.19 | 17.1 | 1 | gilarime | Speed -1 20% foe |
| multi_thread_shot | Bot | Uncommon | 2 | 12 | 2-3 | multi |  | 0 | 28.49 | 5.66 | 34.15 | 17.08 | 1 | kitsauto | GainAP |
| zipped_strikes | Compressed | Common | 2 | 18 | 2-2 | multi |  | 0 | 33.62 | 0.0 | 33.62 | 16.81 | 1 | humpacked | Speed +1 25% self |
| refined_shot | None | Uncommon | 2 | 38 | 1-1 | single |  | 0 | 33.16 | 0.0 | 33.16 | 16.58 | 1 | minefolk | — |
| gas_bump | Gas | Common | 2 | 36 | 1-1 | single |  | 0 | 33.14 | 0.0 | 33.14 | 16.57 | 1 | balloom,deghoul | — |
| stone_barrage | Block | Common | 2 | 14 | 2-3 | multi |  | 0 | 33.04 | 0.0 | 33.04 | 16.52 | 1 | blockwyrm | Speed -1 20% foe |
| precision_strike | Compressed | Common | 2 | 34 | 1-1 | single |  | 0 | 32.92 | 0.0 | 32.92 | 16.46 | 1 | marrowhead | — |
| baseline_volley | None | Common | 2 | 16 | 1-1 | spread |  | 0 | 29.98 | 0.0 | 29.98 | 14.99 | 1 | forkrook | — |
| baseline_thrust | None | Common | 2 | 26 | 1-1 | single |  | 0 | 24.13 | 5.66 | 29.78 | 14.89 | 1 | rattlestake | MeleeDef -1 25% foe |
| baseline_striker | None | Common | 2 | 30 | 1-1 | single |  | 0 | 27.68 | 0.0 | 27.68 | 13.84 | 1 | deghoul,hopoch,kreechank,validape | — |
| baseline_snipe | None | Common | 2 | 30 | 1-1 | single |  | 0 | 26.23 | 0.0 | 26.23 | 13.11 | 1 | marrowhead | — |
| baseline_slam | None | Common | 2 | 30 | 1-1 | single |  | 0 | 24.77 | 0.0 | 24.77 | 12.38 | 1 | balloom,kodiax | — |
| baseline_shot | None | Common | 2 | 26 | 1-1 | single |  | 0 | 24.13 | 0.0 | 24.13 | 12.06 | 1 | cinislug,cofi_lootwak,forkrook,minefolk,rattlestake | — |
| civet_beam | Staked | Uncommon | 2 | 12 | 1-1 | spread |  | 0 | 22.79 | 0.0 | 22.79 | 11.4 | 1 | cofi_lootwak | Speed -1 100% foe |
| cinder_burst | Burned | Common | 3 | 15 | 2-4 | multi |  | 0 | 42.17 | 13.58 | 55.75 | 18.58 | 2 | cinislug | MeleeAtk -1 20% foe |
| bulwark_blow | Block | Epic | 3 | 50 | 1-1 | single |  | 0 | 43.45 | 0.0 | 43.45 | 14.48 | 2 | blockwyrm | strikes with MeleeDef |
| cipher_burst | Encrypted | Common | 3 | 17 | 2-3 | multi |  | 0 | 39.7 | 0.0 | 39.7 | 13.23 | 2 | keyper | — |
| pickaxe_flurry | Mined | Common | 3 | 14 | 2-4 | multi |  | 0 | 39.65 | 0.0 | 39.65 | 13.22 | 2 | kodiax,minefolk | ChangeStage |
| fork_split | Forked | Legendary | 4 | 56 | 1-1 | spread |  | 0 | 102.56 | 0.0 | 102.56 | 25.64 | 2 | forkrook | — |
| cryo_ray | Frozen | Legendary | 4 | 80 | 1-1 | single |  | 0 | 73.11 | 0.0 | 73.11 | 18.28 | 2 | gilarime | Speed -2 25% foe |
| hammer_sync | Node | Legendary | 4 | 80 | 1-1 | single |  | 0 | 73.11 | 0.0 | 73.11 | 18.28 | 2 | validape | — |
| recoil_strike | Forked | Uncommon | 4 | 72 | 1-1 | single |  | 0 | 65.75 | 7.15 | 72.89 | 18.22 | 2 | balloom | Cleanse |
| deep_dig | Mined | Uncommon | 4 | 72 | 1-1 | single |  | 0 | 65.75 | 0.0 | 65.75 | 16.44 | 2 | kodiax | — |
| volatile_shot | Gas | Uncommon | 4 | 72 | 1-1 | single |  | 0 | 65.75 | -23.82 | 41.93 | 10.48 | 2 | balloom,deghoul | NextMoveAPSurcharge |

## (d) Tempo-adjusted value

Chainlings start at 0 and gain 2 at the start of each turn (C11). Turn 1 can pay 2. Turn 2 can pay 4. Turn 3 can pay 6. A 3 AP move and a 4 AP move share a first legal turn (C59). The 3 AP move then leaves 1 AP unspent. The ten learnable damaging moves that miss turn 1 are Hammer Sync, Cryo Ray, Cinder Burst, Cipher Burst, Gas War, Fork Split, Bulwark Blow, Pickaxe Flurry, Deep Dig, and Hard Fork.

The 2v2 sim is 320 trials per cell. Format is 2 active plus 1 bench, four move slots, real pools, chemistry, passives, statuses, and today's walls unless the cell says P. Modes: L clicks the 2 AP move every turn. B passes until the heavy is affordable, then clicks it. BB is that bank plus the partner's turn-1 buff. The partner does not focus the carrier's target unless the cell says focus. Speed ties are a coin flip. Recharge ticks on the bench.

| Line | Mode | Heavy | Win% | KO med | KO mean | Heavy% | Heavy turn | Heavy KO% | Atk dmg | Def dmg | Turns |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| HS | L | hammer_sync | 79.4 | 4 | 4.6 | 0.0 | 0.0 | 0.0 | 384.7 | 327.8 | 6 |
| HS | B | hammer_sync | 78.4 | 4 | 4.0 | 63.4 | 2.1 | 50.0 | 383.6 | 299.3 | 5 |
| HS | BB | hammer_sync | 50.3 | 2 | 4.2 | 74.7 | 2.3 | 73.1 | 316.5 | 345.8 | 6 |
| CR | L | cryo_ray | 100.0 | 2 | 2.0 | 0.0 | 0.0 | 0.0 | 366.7 | 207.5 | 4 |
| CR | B | cryo_ray | 99.7 | 2 | 2.2 | 94.7 | 2.0 | 94.7 | 363.4 | 223.3 | 4 |
| CR | BB | cryo_ray | 81.9 | 2 | 2.2 | 95.6 | 2.0 | 95.6 | 360.5 | 314.6 | 6 |
| DD | L | deep_dig | 81.6 | 1 | 1.6 | 0.0 | 0.0 | 0.0 | 390.9 | 344.7 | 8 |
| DD | B | deep_dig | 42.2 | 2 | 2.4 | 93.8 | 2.0 | 93.8 | 357.3 | 387.7 | 8 |
| DD | BB | deep_dig | 28.8 | 2 | 2.2 | 95.9 | 2.0 | 95.9 | 318.8 | 393.2 | 8 |
| GW | L | volatile_shot | 78.4 | 4 | 4.0 | 0.0 | 0.0 | 0.0 | 382.2 | 366.8 | 8 |
| GW | B | volatile_shot | 63.8 | 4 | 4.9 | 68.1 | 2.0 | 0.0 | 362.0 | 380.1 | 8 |
| FS | L | fork_split | 95.3 | 3 | 2.7 | 0.0 | 0.0 | 0.0 | 363.5 | 249.8 | 6 |
| FS | B | fork_split | 95.6 | 3 | 2.6 | 95.0 | 2.0 | 49.1 | 367.0 | 227.3 | 5 |
| CS | L | — | 76.9 | 1 | 1.9 | 0.0 | 0.0 | 0.0 | 368.1 | 289.5 | 5 |
| CS | B | — | 75.6 | 2 | 2.0 | 0.0 | 0.0 | 0.0 | 368.2 | 291.3 | 5 |

Lines. HS: Validape Hammer Sync into Deghoul. Partner Crackett. Deghoul is faster (72 vs 60). CR: Gilarime Cryo Ray into Crackett. Crackett is faster (92 vs 49). DD: Kodiax Deep Dig into Keyper. Keyper is faster (68 vs 46). GW: Balloom Gas War into Kitsauto, after Fuel Surge is available. Kitsauto is faster (82 vs 52). FS: Forkrook Fork Split into Deghoul and Keyper. Forkrook is faster (88 vs 72). CS: Kreechank Chaos Strike, a 2 AP click, into Keyper.

Hammer Sync into Deghoul is expected 113.1 on 106 HP at chemistry 0.8 (C48). Snap Strike is 42.61. The bank wins 78.4% and the light line wins 79.4%. The heavy is cast in 63.4% of banks, median turn 2.1, and it KOs in 50.0%. BB, with the partner spending turn 1 on Shard Partition, drops the bank to 50.3% because the ally is not hitting. A 4 AP Node hammer on turn 2 is a fair option. It is not the click that wins the game.

Cryo Ray into Crackett is expected 135.3 on 112 HP, chemistry 1.2. Frost Shot is 88.23. Lights win 100.0% by turn 2. The bank wins 99.7% and the ray KOs in 94.7% of trials. Gilarime is the slow one. The 2 AP shot is the whole line.

Deep Dig into Keyper is expected 137.68 on 102 HP, so the hit does KO, and the sim agrees (93.8% heavy KOs, land rate 93.8%). The team still loses: bank win 42.2% against light win 81.6%. Kodiax at speed 46 gives Keyper at 68 a free turn. Ore Strike at 99.67 is the correct race click. Do not discount Deep Dig to make the slow body faster.

Gas War into Kitsauto is expected 64.63 on 139 HP. Gas Bump is 49.98, and it hits on turn 1. Bank win 63.8% against light win 78.4%. The war is cast in 68.1% of banks and KOs in 0.0%. The surcharge then taxes the next click (C54).

Fork Split is the exception. Expected spread damage into the Deghoul side is 204.07 against Pulse Shot at 47.64 (C48). Lights win 95.3% and the bank wins 95.6%. The heavy KOs in only 49.1% of trials because the damage is split, and the side still falls over. Forkrook is faster, so the skipped turn is cheap.

Chaos Strike has no separate heavy. L and B are the same 2 AP click. Win 76.9% and 75.6%. The mythic swing is a turn-1 move. That is why its SPR per AP leads the curve and why a wall, not a cost change, is the lever.

### Do the obvious counters stop a bank?

Deny means the defender clicks Transaction Tax, Packet Skim, or Memory Leak once the carrier is visibly banking. Focus means the attacking partner hits the named target and the defender hits the carrier. Swap is the defender leaving. Dwall replaces the defender's ally with a cover user, so it is not comparable to the "none" team. Compare engine to P on that row. Engine is today's walls. P is the pending recommendation.

| Line | Counter | Design | Win% | KO med | Heavy% | Heavy turn | Heavy KO% | Absorbed | No-AP |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| HS | deny | engine | 100.0 | 3 | 98.4 | 3.2 | 94.1 | 0.00 | 1.32 |
| HS | focus | engine | 100.0 | 1 | 0.0 | 0.0 | 0.0 | 0.00 | 0.00 |
| HS | swap | engine | 93.1 | 4 | 94.4 | 4.2 | 81.6 | 0.00 | 0.00 |
| HS | dwall | engine | 44.1 | 4 | 6.9 | 4.0 | 5.6 | 0.00 | 0.00 |
| HS | dwall | P | 35.6 | 4 | 61.6 | 2.1 | 46.2 | 0.00 | 0.00 |
| CR | deny | engine | 100.0 | 2 | 94.7 | 2.0 | 94.1 | 0.00 | 0.00 |
| CR | focus | engine | 99.1 | 2 | 3.8 | 2.0 | 3.8 | 0.00 | 0.00 |
| CR | swap | engine | 96.2 | 4 | 96.6 | 3.3 | 96.2 | 0.00 | 0.00 |
| CR | dwall | engine | 98.1 | 2 | 94.1 | 2.0 | 79.7 | 0.00 | 0.00 |
| CR | dwall | P | 99.4 | 2 | 95.3 | 2.0 | 94.4 | 0.00 | 0.00 |
| DD | deny | engine | 83.1 | 13 | 12.5 | 7.0 | 12.5 | 0.00 | 1.96 |
| DD | focus | engine | 10.6 | 3 | 0.0 | 0.0 | 0.0 | 0.00 | 0.00 |
| DD | swap | engine | 24.7 | 99 | 15.6 | 5.3 | 15.6 | 0.00 | 0.00 |
| DD | dwall | engine | 64.1 | 2 | 92.8 | 2.0 | 92.5 | 0.00 | 0.00 |
| DD | dwall | P | 58.1 | 2 | 92.5 | 2.0 | 92.5 | 0.01 | 0.00 |
| GW | deny | engine | 66.2 | 9 | 80.0 | 5.6 | 11.9 | 0.00 | 2.13 |
| GW | focus | engine | 84.4 | 3 | 0.0 | 0.0 | 0.0 | 0.00 | 0.00 |
| GW | swap | engine | 10.0 | 8 | 30.6 | 4.0 | 0.0 | 0.00 | 0.00 |
| GW | dwall | engine | 70.3 | 4 | 67.5 | 2.0 | 0.0 | 0.00 | 0.00 |
| GW | dwall | P | 73.8 | 4 | 68.8 | 2.0 | 0.0 | 0.00 | 0.00 |
| FS | deny | engine | 98.4 | 3 | 95.0 | 2.0 | 54.4 | 0.00 | 0.00 |
| FS | focus | engine | 97.5 | 1 | 3.4 | 2.0 | 3.4 | 0.00 | 0.00 |
| FS | swap | engine | 91.9 | 2 | 52.5 | 2.0 | 29.4 | 0.00 | 0.00 |
| FS | dwall | engine | 77.2 | 3 | 95.3 | 2.0 | 46.6 | 0.00 | 0.00 |
| FS | dwall | P | 84.1 | 3 | 95.9 | 2.0 | 48.8 | 0.00 | 0.00 |

Denial delays Hammer Sync and does not flip it. Land turn moves from 2.1 to 3.2, no-AP events average 1.32 per battle, and the attacker still wins 100.0% because Deghoul and Kitsauto spent the turns taxing. Deep Dig denial drops the land rate from 93.8% to 12.5% and the attacker win rises to 83.1%, with a KO median of 13. The defenders are not racing. Gas War denial pushes the land to turn 5.6 and the win stays 66.2%. Cryo Ray denial does nothing useful. The ray still lands on turn 2.0 and wins 100.0%.

Focus fire replaces the Hammer bank. Partner Seam Cut deletes Deghoul. Win 100.0%, KO median turn 1, heavy cast in 0.0%. The same focus does not save Deep Dig: win 10.6%, heavy never cast. Swap delays Hammer to turn 4.2 and the attacker still wins 93.1%. Swap cuts Deep Dig to 24.7% and Gas War to 10.0%. A swap answers a slow bank. It does not answer Frost Shot or Fork Split, which stay above 96.2% and 91.9%.

Today's walls are mostly a turn-2 tool, because four of the five covers cost 3 (C51). On the Hammer bank the engine wall cell lands the heavy in only 6.9% of trials and the win falls to 44.1%. That cell has also swapped Kitsauto out for Blockwyrm, so part of the drop is the partner, not the wall. Deep Dig's engine wall cell still lands 92.8% of the time. Cryo Ray's still lands 94.1%. A 3 AP Blockade does not catch a turn-1 ranged shot, and it does not catch a turn-2 heavy that is already being clicked into a different body.

## (e) Sensitivity to walls v2

Walls v2 is not decided. The cell marked P is the round-13 recommendation: walls block melee, barriers block ranged, the cover move is priority, it stops one hit and then drops, recharge column is 3 (the cast turn's end tick plus two skipped turns), and the cover costs 2 AP. Same ally, same seed family, engine rules against P.

| Move | Type | Shape | Prio | AP | Engine win | P win | Delta | Eng abs | P abs | Eng land | P land |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline_thrust | None | single |  | 2 | 86.6 | 25.6 | -61.0 | 0.00 | 0.93 | 1.0 | 2.0 |
| baseline_shot | None | single |  | 2 | 97.2 | 36.9 | -60.3 | 0.00 | 0.92 | 1.0 | 2.8 |
| ore_strike | Mined | single |  | 2 | 86.9 | 30.6 | -56.3 | 0.00 | 0.93 | 1.1 | 2.0 |
| baseline_striker | None | single |  | 2 | 88.1 | 34.7 | -53.4 | 0.00 | 0.69 | 1.4 | 2.2 |
| pickaxe_flurry | Mined | multi |  | 3 | 78.8 | 35.3 | -43.5 | 0.00 | 0.00 | 2.0 | 2.0 |
| blade_flurry | Bot | multi |  | 2 | 71.2 | 30.0 | -41.2 | 0.00 | 1.00 | 1.0 | 1.0 |
| stake_strike | Staked | single |  | 2 | 55.6 | 16.6 | -39.0 | 0.00 | 0.96 | 1.1 | 2.0 |
| lean_hit | Compressed | single |  | 1 | 93.1 | 55.3 | -37.8 | 0.00 | 1.15 | 1.0 | 2.0 |
| yield_spike | Staked | single |  | 2 | 59.4 | 23.1 | -36.3 | 0.00 | 0.94 | 1.1 | 2.0 |
| chaos_strike | Glitched | single |  | 2 | 66.9 | 32.8 | -34.1 | 0.00 | 0.93 | 1.1 | 2.0 |
| zipped_strikes | Compressed | multi |  | 2 | 48.4 | 16.2 | -32.2 | 0.00 | 1.00 | 1.0 | 1.2 |
| interest_strike | — | — |  | — | 59.4 | 29.4 | -30.0 | 0.00 | 1.00 | 1.0 | 1.1 |
| baseline_slam | None | single |  | 2 | 82.8 | 55.0 | -27.8 | 0.00 | 0.83 | 1.1 | 2.0 |
| bulwark_blow | Block | single |  | 3 | 61.9 | 36.2 | -25.7 | 0.00 | 0.00 | 2.1 | 2.1 |
| chain_fork | Forked | multi |  | 2 | 90.3 | 65.0 | -25.3 | 0.00 | 1.02 | 1.0 | 1.0 |
| baseline_jab | None | single | Y | 1 | 52.8 | 27.8 | -25.0 | 0.00 | 0.00 | 1.1 | 1.0 |
| stone_barrage | Block | multi |  | 2 | 58.4 | 34.1 | -24.3 | 0.00 | 1.43 | 1.0 | 1.0 |
| rapid_strike | Forked | single | Y | 1 | 92.8 | 69.7 | -23.1 | 0.00 | 0.00 | 1.1 | 1.1 |
| baseline_snipe | None | single |  | 2 | 87.5 | 64.7 | -22.8 | 0.00 | 0.90 | 1.0 | 2.2 |
| volatile_shot | Gas | single |  | 4 | 88.4 | 76.2 | -12.2 | 0.00 | 0.00 | 2.7 | 2.8 |
| cinder_burst | Burned | multi |  | 3 | 91.2 | 87.8 | -3.4 | 0.00 | 0.00 | 2.1 | 2.1 |
| fork_split | Forked | spread |  | 4 | 92.2 | 90.3 | -1.9 | 0.00 | 0.00 | 2.0 | 2.0 |
| civet_beam | Staked | spread |  | 2 | 31.2 | 29.7 | -1.5 | 0.00 | 0.96 | 1.1 | 2.0 |
| partition_shot | Shard | single |  | 2 | 79.7 | 78.8 | -0.9 | 0.00 | 0.93 | 1.0 | 2.0 |
| frost_shot | Frozen | single |  | 2 | 100.0 | 100.0 | 0.0 | 0.00 | 0.94 | 1.0 | 2.0 |
| frost_shards | Frozen | multi |  | 2 | 100.0 | 100.0 | 0.0 | 0.00 | 1.00 | 1.0 | 1.0 |
| deep_reap | Mined | single |  | 2 | 98.8 | 99.4 | 0.6 | 0.00 | 0.96 | 1.1 | 2.0 |
| cipher_burst | Encrypted | multi |  | 3 | 74.1 | 75.9 | 1.8 | 0.00 | 0.00 | 2.0 | 2.0 |
| quick_lunge | — | — | Y | — | 13.8 | 16.6 | 2.8 | 0.00 | 0.00 | 1.1 | 1.1 |

Material means a win-rate move of about 15 points or an absorb that goes from 0 to about 1. Melee singles move. Ore Strike, Snap Strike, Body Check, Pressure Point, Stake Strike, Yield Spike, and Chaos Strike drop by 30 to 60 points, and P logs about 0.9 absorbs. Multi-hit melee loses the first hit and often the game: Blade Flurry, Chain Fork, Stone Barrage, Zipped Strikes, and Interest Strike log about 1.0 to 1.4 absorbs. Frost Shards logs an absorb and still wins the cell. That Frozen line is not the wall's to take.

The three priority attacks log 0 absorbs under both designs. Rapid Strike's win still falls, and Quick Jab's falls, because the priority hit lands before the wall and does not break it. The wall then catches someone else. Blink Lunge is the same shape and is not in any pool (C33, C56). The cell puts it on Crackett as a counterfactual. Its win stays low either way. This matches the round-13 finding that a priority hit is not a wall answer. This round does not propose making it one.

Ranged heavies barely move when they actually land. Fork Split's absorb stays 0 and the win stays within 2 points. It hits both foes, and one barrier does not cover the field. Cinder Burst's win stays within 4 points and logs no absorb in this pairing. Frost Shot, a 2 AP ranged single into a barrier, does log about 0.9 absorbs and its land turn slips from about 1 to about 2, and the win stays 100%. The barrier buys a turn. It does not save the body. Gas War's land rate is already about 3 to 5% in the outlier cell, so there is almost nothing for a barrier to catch.

Seam Cut into today's Blockade, 2 AP melee, 320 trials: engine-wall win 43.1%, P win 32.2%. The worked trace below shows why. Today's Blockade costs 3 and the user passes turn 1. Under P it costs 2, has priority, and eats the 2 AP hit.

Do not retune multi-hit power in advance of this decision. If P is adopted, Blade Flurry, Stone Barrage, and Chain Fork get cheaper in the curve sense only because their first hit disappears. If P is rejected, they are ordinary 2 AP clicks.

## (f) Type and rule checks

Typed moves beat typeless on the learnable singles. From the generated type table: 1 AP singles, typed median SPR 24.13 (n=2) against Quick Jab at 14.99. 2 AP singles, typed median 39.5 (n=18) against typeless 26.95 (n=6). The typed common 2 AP single, catalog scope, is 33.62 (n=7) (C49). Pulse Shot at 26 is inside that typeless band on purpose (C21).

The spread cell goes the other way. The one learnable typed 2 AP spread is Bean Beam at 22.79. The typeless spread, Scatter Shot, is 29.98. Bean Beam is on Cofi, and the pool rules say not to touch Cofi. Leave it.

| AP | Shape | Typed n | Typed med SPR | Typeless n | Typeless med SPR | Typed common med | Typeless rare med |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | single | 2 | 24.13 | 1 | 14.99 | None | None |
| 2 | single | 18 | 39.5 | 6 | 26.95 | 33.62 | None |
| 2 | spread | 1 | 22.79 | 1 | 29.98 | None | None |
| 3 | single | 4 | 41.58 | 0 | None | 39.7 | None |
| 4 | single | 5 | 72.89 | 0 | None | None | None |
| 4 | spread | 1 | 102.56 | 0 | None | None | None |

A rare typeless move is supposed to sit near a typed common. The only catalog cell where both exist is the 3 AP single: typed common median SPR 46.49 (n=5), typeless rare 37.4 (n=2, Refined Slam and Overdraw Snipe) (C49). That is about four fifths, and neither move is in a learn pool. There is no rare typeless 2 AP single in the catalog at all. Refined Shot is Uncommon. The live game keeps the rule by making typeless commons the chip, not by lining a rare typeless up with a typed common. **PROPOSAL: do not buff Refined Slam or Overdraw Snipe into a pool to force the lineup.** They would have to enter a band that already has a signature, and the chip rule is already doing the job.

Bot is the AP-denial type. Learned denial, removed AP per printed AP:

| Move | Type | Rarity | AP | Kind | AP removed | AP gained | Removed per AP | Learned by |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ap_drain | None | Uncommon | 2 | drain | 1.0 | 0.25 | 0.5 | — |
| defensive_reflex | None | Legendary | 0 | drain | 1.0 | 0.0 |  | — |
| transaction_tax | Gas | Epic | 1 | steal | 1.0 | 1.0 | 1.0 | deghoul |
| fee_collector | Gas | Rare | 0 | passive-if-ahead | 1 | 0.0 |  | — |
| ap_siphon | Gas | Rare | 3 | drain | 0.5 | 0.0 | 0.167 | — |
| memory_leak | Encrypted | Legendary | 2 | next-start | 2.0 | 0.0 | 1.0 | keyper |
| mev_extract | Bot | Rare | 2 | mev | 2.0 | 0.0 | 1.0 | — |
| thread_lock | Bot | Legendary | 2 | threadlock | 2.0 | 0.0 | 1.0 | — |
| packet_skim | Bot | Common | 1 | drain | 1.0 | 0.0 | 1.0 | kitsauto |

Packet Skim is 1 removed per 1 AP, learned by Kitsauto only (C28). Transaction Tax matches that removal rate and also grants the point to Deghoul (C53). Memory Leak matches the removal rate at 2 for 2 AP, delayed to next turn start, and also grants Speed +2. It is Keyper's Legendary signature (C52). Fee Collector removes 1 for 0 AP when the foe is ahead, and it is not learnable (C13, C57). MEV Extract and Thread Lock are Bot and sit in no pool (C58). No learned non-Bot move removes more AP per printed AP than Packet Skim. Tax is not cheaper on that ratio. It is strictly better on the net, because of the grant. That is the overlap, and recommendation 3 removes it. Memory Leak's rate is equal, and the Speed stage is the Encrypted signature's extra. Leave it.

Gas. Deghoul's pool is Transaction Tax, Gas War, Gas Bump, Slipstream, Snap Strike. Balloom's is Fuel Surge, Gas War, Gas Bump, Slipstream, Body Check, and Hard Fork off-type (C42). Deghoul has 106 HP. The affordable chip is Gas Bump, power 36, AP 2, 16.57 SPR per AP, expected 47.69 into Validape (C48). The affordable denial is Tax at 1 AP. Both exist. They overlap Bot only on the denial, and only because Tax steals. The chip does not overlap Bot. Gas War is the card that fails the "affordable upgrade" test, which is recommendation 1. Hard Fork is Balloom's off-type 4 AP melee at 18.22 SPR per AP with a Cleanse rider. It is not a second Gas chip.

Meme has no chemistry cells (C34). All-In is the Meme epic, it is not in a pool, and this round does not put it in one (C19, C20). Script Loop and the other Meme cards stay deferred.

## (g) Worked examples

Four traces from `move_cost_curve_v2_sim.py`. Each is one seed. The win rates above are the 320-trial cells. Damage figures in the trace are that seed's rolls.

### 1. A 4 AP bank against a faster denial line

**Validape banks Hammer Sync. Faster Deghoul clicks Gas Bump (Jammed). Kitsauto can skim.** One seed. Winner: attacker. Turns: 5. Design: engine.
- Turn 1. validape 156 HP / 2 AP pass, crackett 112 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 139 HP / 2 AP move blade_flurry. crackett seam_cut 84, kitsauto blade_flurry 30, kitsauto blade_flurry 17, kitsauto blade_flurry 18, deghoul gas_bump 68.
- Turn 2. validape 88 HP / 4 AP move hammer_sync, crackett 47 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move transaction_tax, kitsauto 55 HP / 2 AP move packet_skim. crackett seam_cut 89, KO kitsauto by crackett, no AP validape hammer_sync.
- Turn 3. validape 88 HP / 5 AP move hammer_sync, crackett 47 HP / 2 AP move seam_cut, deghoul 106 HP / 4 AP move transaction_tax, blockwyrm 168 HP / 4 AP move stone_barrage. validape hammer_sync 123, KO deghoul by validape, blockwyrm stone_barrage 21, blockwyrm stone_barrage 21, blockwyrm stone_barrage 22, KO crackett by blockwyrm.
- Turn 4. validape 88 HP / 2 AP pass, kodiax 124 HP / 2 AP move ore_strike, blockwyrm 168 HP / 4 AP move stone_barrage. kodiax ore_strike 59, blockwyrm stone_barrage 12, blockwyrm stone_barrage 12, blockwyrm stone_barrage 11.
- Turn 5. validape 88 HP / 4 AP move hammer_sync, kodiax 89 HP / 2 AP move ore_strike, blockwyrm 96 HP / 5 AP move stone_barrage. validape hammer_sync 70, kodiax ore_strike 100, KO blockwyrm by kodiax.
End: validape 88 HP, crackett 0 HP, kodiax 89 HP, deghoul 0 HP, kitsauto 0 HP, blockwyrm 0 HP.

Validape opens at 2 and passes. Deghoul's Gas Bump and Kitsauto's Packet Skim are both legal. On turn 2 Validape has 4 and the hammer fails for lack of AP, because Tax and Skim have already taken the bar. The hammer lands on turn 3, after Kitsauto is gone, and KOs Deghoul. Denial cost the attacker one turn. It did not cost the game. The aggregate deny cell wins 100% for the attacker with the land on turn 3.

### 2. The same bank with no denial, against clicking the 2 AP shot

**Validape banks Hammer Sync into Deghoul with no denial. Both open at 2 AP.** One seed. Winner: attacker. Turns: 5. Design: engine.
- Turn 1. validape 156 HP / 2 AP pass, crackett 112 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 139 HP / 2 AP move blade_flurry. crackett seam_cut 83, kitsauto blade_flurry 18, kitsauto blade_flurry 20, deghoul gas_bump 47.
- Turn 2. validape 109 HP / 4 AP move hammer_sync, crackett 74 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 56 HP / 2 AP move blade_flurry. crackett seam_cut 81, KO kitsauto by crackett, deghoul gas_bump 71, validape hammer_sync 116, KO deghoul by validape.
- Turn 3. validape 38 HP / 2 AP pass, crackett 74 HP / 2 AP move seam_cut, blockwyrm 168 HP / 4 AP move stone_barrage. crackett seam_cut 37, blockwyrm stone_barrage 21, blockwyrm stone_barrage 22, blockwyrm stone_barrage 22.
- Turn 4. validape 38 HP / 4 AP move hammer_sync, crackett 9 HP / 2 AP move seam_cut, blockwyrm 131 HP / 5 AP move stone_barrage. crackett seam_cut 33, validape hammer_sync 63, blockwyrm stone_barrage 20, KO crackett by blockwyrm.
- Turn 5. validape 38 HP / 2 AP pass, kodiax 124 HP / 2 AP move ore_strike, blockwyrm 35 HP / 6 AP move stone_barrage. kodiax ore_strike 53, KO blockwyrm by kodiax.
End: validape 38 HP, crackett 0 HP, kodiax 124 HP, deghoul 0 HP, kitsauto 0 HP, blockwyrm 0 HP.

With no tax, the hammer lands on turn 2 for a KO and Validape lives, low, on the back of two Gas Bumps. Deghoul is faster, so both bumps hit before the hammer in the order. That is the tempo rule in one fight: the 4 AP move resolves, and the faster 2 AP line has already been paid.

**Validape clicks Snap Strike every turn instead of banking.** One seed. Winner: attacker. Turns: 6. Design: engine.
- Turn 1. validape 156 HP / 2 AP move baseline_striker, crackett 112 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 139 HP / 2 AP move blade_flurry. crackett seam_cut 83, kitsauto blade_flurry 18, kitsauto blade_flurry 20, deghoul gas_bump 47.
- Turn 2. validape 109 HP / 2 AP move baseline_striker, crackett 74 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 56 HP / 2 AP move blade_flurry. crackett seam_cut 79, KO kitsauto by crackett, deghoul gas_bump 48.
- Turn 3. validape 61 HP / 2 AP move baseline_striker, crackett 74 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, blockwyrm 168 HP / 4 AP move stone_barrage. crackett seam_cut 37, deghoul gas_bump 50, blockwyrm stone_barrage 20, blockwyrm stone_barrage 22.
- Turn 4. validape 11 HP / 2 AP move baseline_striker, crackett 32 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, blockwyrm 131 HP / 5 AP move stone_barrage. crackett seam_cut 36, deghoul gas_bump 47, KO validape by deghoul, blockwyrm stone_barrage 20, blockwyrm stone_barrage 21, KO crackett by blockwyrm.
- Turn 5. kodiax 124 HP / 4 AP move ore_strike, deghoul 106 HP / 2 AP move gas_bump, blockwyrm 95 HP / 6 AP move stone_barrage. deghoul gas_bump 57, kodiax ore_strike 109, KO deghoul by kodiax, blockwyrm stone_barrage 19, blockwyrm stone_barrage 11.
- Turn 6. kodiax 37 HP / 5 AP move ore_strike, blockwyrm 95 HP / 6 AP move stone_barrage. kodiax ore_strike 83, blockwyrm stone_barrage 11, blockwyrm stone_barrage 13.
End: validape 0 HP, crackett 0 HP, kodiax 13 HP, deghoul 0 HP, kitsauto 0 HP, blockwyrm 5 HP.

Clicking Snap Strike every turn never deletes Deghoul early. Gas Bump KOs Validape on turn 4 of this seed, and the fight goes to the horizon with one body each. The aggregate light line still wins about as often as the bank. The bank's value on this seed is keeping Validape alive long enough to cash the hammer, not a higher win rate.

### 3. A 3 AP move cannot be paid on turn 1

**Cinislug's only equipped move is Cinder Burst (3 AP). Turn 1 opens at 2.** One seed. Winner: defender. Turns: 6. Design: engine.
- Turn 1. cinislug 134 HP / 2 AP pass, crackett 112 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 139 HP / 2 AP move blade_flurry. crackett seam_cut 85, kitsauto blade_flurry 19, kitsauto blade_flurry 20, kitsauto blade_flurry 20, deghoul gas_bump 33.
- Turn 2. cinislug 101 HP / 4 AP move cinder_burst, crackett 53 HP / 2 AP move seam_cut, deghoul 106 HP / 2 AP move gas_bump, kitsauto 54 HP / 2 AP move blade_flurry. crackett seam_cut 91, KO kitsauto by crackett, deghoul gas_bump 36, cinislug cinder_burst 30, cinislug cinder_burst 31.
- Turn 3. cinislug 65 HP / 3 AP move cinder_burst, crackett 53 HP / 2 AP move seam_cut, deghoul 39 HP / 2 AP move gas_bump, hopoch 120 HP / 4 AP move yield_spike. crackett seam_cut 44, deghoul gas_bump 36, hopoch yield_spike 103, KO crackett by hopoch, cinislug cinder_burst 31, cinislug cinder_burst 29, KO deghoul by cinislug.
- Turn 4. cinislug 29 HP / 2 AP pass, kodiax 124 HP / 2 AP move ore_strike, hopoch 101 HP / 4 AP move yield_spike. hopoch yield_spike 53, kodiax ore_strike 51.
- Turn 5. cinislug 29 HP / 4 AP move cinder_burst, kodiax 64 HP / 2 AP move ore_strike, hopoch 63 HP / 4 AP move yield_spike. hopoch yield_spike 83, KO kodiax by hopoch, cinislug cinder_burst 21, cinislug cinder_burst 20, cinislug cinder_burst 20.
- Turn 6. cinislug 29 HP / 3 AP move cinder_burst, hopoch 22 HP / 4 AP move yield_spike. hopoch yield_spike 53, KO cinislug by hopoch.
End: cinislug 0 HP, crackett 0 HP, kodiax 0 HP, deghoul 0 HP, kitsauto 0 HP, hopoch 22 HP.

Cinislug equips only Cinder Burst. Turn 1 is 2 AP, so Cinislug passes while Gas Bump and Blade Flurry hit. The burst becomes legal on turn 2 and still needs a second turn to KO Deghoul. This seed loses. Cinder Burst is the high 3 AP outlier on the curve because of the attack drop, and it is still a turn late. Cinislug's real pool also has Pulse Shot at 2 AP. The trap is equipping the 3 as the only click.

### 4. Today's 3 AP wall misses the turn-1 hit. Walls P does not.

**Crackett Seam Cut into Deghoul. Ally Blockade as it works today.** One seed. Winner: attacker. Turns: 6. Design: engine.
- Turn 1. crackett 112 HP / 2 AP move seam_cut, validape 156 HP / 2 AP move baseline_striker, deghoul 106 HP / 2 AP move gas_bump, blockwyrm 168 HP / 2 AP pass. crackett seam_cut 123, KO deghoul by crackett, validape baseline_striker 18.
- Turn 2. crackett 112 HP / 2 AP move seam_cut, validape 156 HP / 2 AP move baseline_striker, hopoch 120 HP / 4 AP move yield_spike, blockwyrm 150 HP / 5 AP move stone_barrage. crackett seam_cut 41, validape baseline_striker 23, hopoch yield_spike 105, blockwyrm stone_barrage 13, blockwyrm stone_barrage 13, blockwyrm stone_barrage 13.
- Turn 3. crackett 7 HP / 2 AP move seam_cut, validape 117 HP / 2 AP move baseline_striker, hopoch 82 HP / 4 AP move yield_spike, blockwyrm 150 HP / 5 AP move stone_barrage. crackett seam_cut 33, validape baseline_striker 18, hopoch yield_spike 100, KO crackett by hopoch, blockwyrm stone_barrage 12, blockwyrm stone_barrage 12.
- Turn 4. kodiax 124 HP / 2 AP move ore_strike, validape 93 HP / 2 AP move baseline_striker, hopoch 56 HP / 4 AP move yield_spike, blockwyrm 150 HP / 5 AP move stone_barrage. validape baseline_striker 13, hopoch yield_spike 59, kodiax ore_strike 42, blockwyrm stone_barrage 12, blockwyrm stone_barrage 13.
- Turn 5. kodiax 58 HP / 2 AP move ore_strike, validape 68 HP / 2 AP move baseline_striker, hopoch 15 HP / 4 AP move yield_spike, blockwyrm 150 HP / 5 AP move stone_barrage. validape baseline_striker 18, KO hopoch by validape, kodiax ore_strike 87, blockwyrm stone_barrage 14.
- Turn 6. kodiax 51 HP / 2 AP move ore_strike, validape 54 HP / 2 AP move baseline_striker, blockwyrm 51 HP / 6 AP move stone_barrage. validape baseline_striker 28, kodiax ore_strike 131, KO blockwyrm by kodiax.
End: crackett 0 HP, validape 54 HP, kodiax 51 HP, deghoul 0 HP, blockwyrm 0 HP, hopoch 0 HP.

**Same Seam Cut. Blockade under walls P (priority, 1 melee hit, 2 AP, recharge column 3).** One seed. Winner: defender. Turns: 6. Design: P.
- Turn 1. crackett 112 HP / 2 AP move seam_cut, validape 156 HP / 2 AP move baseline_striker, deghoul 106 HP / 2 AP move gas_bump, blockwyrm 168 HP / 2 AP move blockade. absorb seam_cut, deghoul gas_bump 51, validape baseline_striker 20.
- Turn 2. crackett 61 HP / 2 AP move seam_cut, validape 156 HP / 2 AP move baseline_striker, deghoul 106 HP / 2 AP move gas_bump, blockwyrm 148 HP / 3 AP move stone_barrage. crackett seam_cut 135, KO deghoul by crackett, validape baseline_striker 19, blockwyrm stone_barrage 14, blockwyrm stone_barrage 12.
- Turn 3. crackett 61 HP / 2 AP move seam_cut, validape 130 HP / 2 AP move baseline_striker, hopoch 120 HP / 4 AP move yield_spike, blockwyrm 129 HP / 4 AP move stone_barrage. crackett seam_cut 40, validape baseline_striker 21, hopoch yield_spike 101, KO crackett by hopoch, blockwyrm stone_barrage 12.
- Turn 4. kodiax 124 HP / 2 AP move ore_strike, validape 118 HP / 2 AP move baseline_striker, hopoch 84 HP / 4 AP move yield_spike, blockwyrm 129 HP / 4 AP move stone_barrage. validape baseline_striker 16, hopoch yield_spike 61, kodiax ore_strike 53, blockwyrm stone_barrage 14, blockwyrm stone_barrage 13.
- Turn 5. kodiax 56 HP / 2 AP move ore_strike, validape 91 HP / 2 AP move baseline_striker, hopoch 30 HP / 4 AP move yield_spike, blockwyrm 129 HP / 4 AP move stone_barrage. validape baseline_striker 14, hopoch yield_spike 55, kodiax ore_strike 42, KO hopoch by kodiax, blockwyrm stone_barrage 12, blockwyrm stone_barrage 20.
- Turn 6. validape 59 HP / 2 AP move baseline_striker, blockwyrm 129 HP / 4 AP move stone_barrage. validape baseline_striker 17, blockwyrm stone_barrage 14, blockwyrm stone_barrage 12, blockwyrm stone_barrage 14.
End: crackett 0 HP, validape 19 HP, kodiax 0 HP, deghoul 0 HP, blockwyrm 112 HP, hopoch 0 HP.

Same seed. Today Blockwyrm passes, because Blockade costs 3, and Seam Cut KOs Deghoul on turn 1. Under P, Blockade costs 2 and is priority, and the same Seam Cut is absorbed. The KO slips to turn 2. Across 320 trials that is a win-rate drop from 43.1% to 32.2%. The wall delayed a 2 AP melee. It did not stop the line.

## (h) What works, what doesn't, what could be better

Works. The 2 AP typed rare is a real click and a real curve. Packet Skim is the right size. Pulse Shot at 26 keeps typeless under typed. Hammer Sync is optional, which is what a 4 AP move should be. Fork Split is worth banking because it is a spread and Forkrook is fast. Bench recharge and owned stage revert are in.

Doesn't. Gas War taxes its own follow-up and loses to Gas Bump. Bulwark Blow is an Epic you cannot press on turn 1, outrated by a Common. The 3 AP band is the worst rate and the same first turn as 4 AP. Four covers cost 3, so they spectate turn 1. Transaction Tax is a better AP trade than the Bot drain. Cryo Ray does not win a game Frost Shot is not already winning. System Reset's card promises a skipped action the recharge does not apply. Grind Tax hides its recharge.

Better. Take the surcharge off Gas War. Let Bulwark Blow be pressed on turn 1. Make Tax a drain. Make Cryo Ray's slow certain. Leave the 10-17 power table in the drawer. Decide walls before touching multi-hit numbers. Do not put MEV Extract, Thread Lock, or Blink Lunge into a pool that already has a denial or a priority jab.

## (i) Ranked recommendations

The top five are in the section above. The rest, still **PROPOSAL**, in order.

6. **PROPOSAL. System Reset's card drops the sentence "skip your next action."** Recharge 2 already locks System Reset only (C22, C23). There is no test. Touch the description on `Moves.csv` row `system_reset` and `Move_system_reset.asset`. Test `MoveCostCurveV2Tests.SystemReset_CardMatchesRecharge`: the description does not say the user skips an action, utility stays `StripAllStatuses`, AP stays 3, recharge stays 2, and a recharge lock matches `rechargingMoveId` rather than a full-turn skip. No pool change. Keyper already learns it inside the band.

7. **PROPOSAL. Grind Tax's card states recharge 2.** It is boss-only, 6 AP, three hits, both foes, Speed −1 once per foe (C08, C36, C40). Players still read the card. Touch the description. Test `MoveCostCurveV2Tests.GrindTax_CardStatesRecharge`: the description contains the recharge, tags still contain `BOSS_ONLY`, and no learn pool contains it. Do not cut it to a playable cost.

8. **PROPOSAL. Leave Deep Dig at 4 AP and power 72.** The hit KOs Keyper in the expected-damage math and in the sim. The line loses because Kodiax is slow. Cutting the cost would make a slow body play like a fast one. No file change. The verifiable statement is the existing row: `MoveCostCurveV2Tests.DeepDig_StaysATurnTwoHit` asserts AP 4, power 72, and Kodiax's pool unchanged, with Deep Dig under Ore Strike.

9. **PROPOSAL. Leave Hammer Sync at 4 AP and power 80, bonus +0.2 per buffed partner, cap 1.2.** The card already says the partner clause (C55). The bank does not beat the light line. No file change. Test `MoveCostCurveV2Tests.HammerSync_StaysFour` asserts those three numbers and that Validape's signature is still the only top rarity.

10. **PROPOSAL. Leave Chaos Strike's 1.5|0.75 swing.** Mean stays 1.125 (P04). Walls P, if adopted, is the melee tax, and this round's cell drops from the mid-60s to the low-30s under P. Pre-nerfing the swing would double up. No file change. Test `MoveCostCurveV2Tests.ChaosStrike_KeepsTheSwing` asserts the two random multipliers and that it remains Kreechank's unique top rarity.

11. **PROPOSAL. Do not add Batch Debit, and do not move MEV Extract or Thread Lock into Kitsauto.** Packet Skim is the Common denial. A second Bot denial in that pool either breaks the band or sits on top of a card that already costs 1. C29, C30, C58. No new move row.

12. **PROPOSAL. Leave Blink Lunge out of every pool.** It is implemented, typeless, priority, power 26, AP 2, and unequipped (C33, C56). Crackett and Rattlestake already learn Quick Jab. A second priority on those bodies is the same job. Cofi is skipped by the pool rules. No membership change.

13. **PROPOSAL. Leave Bean Beam at 2 AP.** The 10-17 cut to 1 AP did not land (P33). It is Cofi's, and Cofi is out of scope. The spread losing to Scatter Shot is real and not worth a Cofi exception.

14. **PROPOSAL. Leave Memory Leak's denial and its Speed +2.** Removal per AP matches Packet Skim. The speed is the signature. Changing it to protect Bot's identity would take Keyper's Legendary apart (C52).

15. **PROPOSAL. Barrier Wall's card should say it is typeless**, or a later type pass should move it onto Block. It is the barrier move and its type is None, AP 3 (C51). This round does not retype it, because walls are pending and a type change is a chemistry change. Flag only. If Cronus retypes it, the test is `MoveCostCurveV2Tests.BarrierWall_TypeMatchesTheBarrierRole` and the pool rules still skip nothing on Humpacked: Streamlined Flow stays the unique Legendary.

## (j) Consistency flags

| Flag | Where | Check |
| --- | --- | --- |
| Grind Tax recharge 2 is not in the card text. | Moves.csv grind_tax | C40 |
| System Reset says the user skips the next action. Recharge only locks System Reset. No test. | Moves.csv system_reset | C23 |
| Barrier Wall is type None. Block is the barrier type. | Moves.csv barrier_wall | C51 |
| Gas War's id is volatile_shot and Hard Fork's id is recoil_strike. Cards use the display names. | Moves.csv | C43 |
| Species assets store learn pools as Unity guids. This snapshot has no .meta files, so the CSV is the pool source. Move asset power, AP, accuracy, hits, recharge, and rarity match the CSV. | C39, C10 | C39 |
| move_ap.ts matches Moves.csv on every cost. | Supabase move_ap.ts | C09 |
| Hammer Sync's gate threshold is 0 and the bonus is +0.2 per buffed partner, cap 1.2. The card's "if your partner is buffed" matches the per-count bonus. It is not a free +20%. | Moves.csv hammer_sync | C55 |
| Crystal Bash's low-HP double is a real multiplier now that power is 44. The power-0 early return is still in the calculator for any future power-0 row. | DamageCalculator.cs | C14, C41 |
| Blink Lunge, Fee Collector, MEV Extract, Thread Lock, and All-In are real rows with no learn pool. | SpeciesMovePools.csv | C20, C56, C57, C58 |
| Learn-pool rarity bands have no breaks. Starters keep a unique top rarity. Ghost and Cofi are skipped. | SpeciesMovePools.csv | C38 |

## (k) Questions only Cronus can answer

1. Adopt walls P (melee walls, ranged barriers, priority, one hit, recharge column 3, 2 AP)? Suggested default: yes. Today's 3 AP covers miss turn 1, and P is what makes a 2 AP melee pay for a wall. Do not also cut melee power in the same pass.
2. Should a priority hit break a wall? Suggested default: no. The three priority attacks are Power 16 or 26. They already land first. Letting Quick Jab pop Blockade makes the jab the answer to every cover.
3. Remove Gas War's surcharge, or cut Gas War to 3 AP instead? Suggested default: remove the surcharge and keep 4 AP. A 3 AP Gas War is still turn 2, still not a KO on Kitsauto, and it joins the worst rate band.
4. Bulwark Blow to 2 AP? Suggested default: yes. Power stays 50. The Epic stops losing per AP to Stone Barrage and becomes a turn-1 Def hit.
5. Transaction Tax from steal to drain? Suggested default: yes. Gas keeps a 1 AP denial. Bot keeps the only learned denial that is not matched by a steal.
6. Cryo Ray Speed −2 from 25% to 100%? Suggested default: yes. Leave power and AP. The curve will not show it, because speed is priced at 0 HP. The Gilarime line will.
7. Turn STAB on? Suggested default: no. Typed 2 AP singles already lead typeless ones (39.5 vs 26.95 SPR) without it. STAB would widen a gap the chip rule already draws.
8. Put the 10-17 power-64 spine back? Suggested default: no. The ladder moved costs. Putting the damage back stacks the two.
9. Touch Cofi's Bean Beam, or add Blink Lunge somewhere? Suggested default: no to both. Cofi is skipped. Blink Lunge duplicates Quick Jab.
10. Rewrite System Reset and Grind Tax so the card states the real limit? Suggested default: yes. No mechanic change.

## Check tally

Source file: the checker ledger in the facts dump this report was built from. Pass 114, fail 0, claims 114.

| Id | Result | Evidence |
| --- | --- | --- |
| C01 | PASS | Moves.csv rows=211 unique ids=211 |
| C02 | PASS | damaging rows=75 passive rows=31 |
| C03 | PASS | non-passive AP census 0:4, 1:54, 2:86, 3:25, 4:10, 6:1 |
| C04 | PASS | rarity census Common:62, Uncommon:43, Rare:43, Epic:31, Legendary:22, Mythic:10 |
| C05 | PASS | power-64 acc-95 1-hit 2-AP single damaging rows=0 ids= |
| C06 | PASS | 4 AP non-passive (10): volatile_shot, ember_shower, fork_split, recoil_strike, deep_dig, automation_burst, shard_scatter, all_in, hammer_sync, cryo_ray |
| C07 | PASS | 5 AP non-passive count=0 |
| C08 | PASS | 6 AP non-passive=['grind_tax'] |
| C09 | PASS | move_ap.ts vs Moves.csv mismatches=0 extra_ts=0 |
| C10 | PASS | move asset vs CSV mismatches=0 |
| C11 | PASS | regen +2 cap 10 |
| C12 | PASS | bench recharge ticks |
| C13 | PASS | Fee Collector drains 1 from the first opposing actor when that actor has more AP |
| C14 | PASS | power 0 still returns 0 before the attack-stat override is read |
| C15 | PASS | Kindling end-step damage is 6% Max HP |
| C16 | PASS | Streamlined discount cannot reduce a positive printed cost below 1 |
| C17 | PASS | apply-once status stages are owned and reverted on expiry (covers Gas Spike and FUD) |
| C18 | PASS | Rallying Slam and Interest Strike use a strict Def-stage > Atk-stage gate, so a fresh 0-vs-0 tie does not refund |
| C19 | PASS | All-In power 98 AP 4 rarity Epic recharge 2 tag SpendRemainingAp |
| C20 | PASS | All-In is in no species learn pool |
| C21 | PASS | Pulse Shot power 26 AP 2 |
| C22 | PASS | System Reset AP 3 recharge 2 utility StripAllStatuses |
| C23 | PASS | System Reset card text says it skips the next action; recharge only locks System Reset |
| C24 | PASS | Hype Burst strips up to 2 buffs and grants Atk +1 each, 2 AP |
| C25 | PASS | Crystal Extraction heals 5% per foe debuff then cleanses them |
| C26 | PASS | Purge Protocol utility includes Cleanse |
| C27 | PASS | Validape learns Uplink and does not learn AP Router |
| C28 | PASS | Kitsauto learns Packet Skim, a 1 AP Bot drain of 1 |
| C29 | PASS | 10-17 concept cut_ledger is not in Moves.csv |
| C30 | PASS | 10-17 concept batch_debit is not in Moves.csv |
| C31 | PASS | 10-17 concept rime_jab is not in Moves.csv |
| C32 | PASS | 10-17 concept vested_strike is not in Moves.csv |
| C33 | PASS | damaging PRIOR moves: baseline_jab Quick Jab None P16 AP1 DamagingMelee, quick_lunge Blink Lunge None P26 AP2 DamagingMelee, rapid_strike Rapid Strike Forked P26 AP1 DamagingMelee |
| C34 | PASS | chemistry matrix cells where Meme is attacker or defender: 0 |
| C35 | PASS | chemistry factors advantage 1.2 disadvantage 0.8 |
| C36 | PASS | BOSS_ONLY moves inside a learn pool: none |
| C37 | PASS | field index Pulse Shot=24.70 (power*hits*acc*spread) |
| C38 | PASS | learn-pool rarity-tier breaks=0 |
| C39 | PASS | snapshot .meta files=0; species assets store learnable moves as Unity guids and cannot be joined to move ids |
| C40 | PASS | card-text key-limit flags=1 |
| P01 | PASS | partly: rapid_strike power old 52 proposed 36 current 26 test no test |
| P02 | PASS | partly: lean_hit power old 46 proposed 34 current 26 test no test |
| P03 | PASS | not landed: baseline_jab power old 16 proposed 22 current 16 test CaptureDossierLoadoutTests.cs |
| P04 | PASS | not landed: chaos_strike random_mean old 1.125 proposed 1.0 current 1.125 test no test |
| P05 | PASS | not landed: refined_striker power old 42 proposed 68 current 42 test no test |
| P06 | PASS | not landed: refined_shot power old 38 proposed 68 current 38 test no test |
| P07 | PASS | not landed: refined_thrust power old 34 proposed 62 current 34 test no test |
| P08 | PASS | not landed: baseline_striker power old 30 proposed 48 current 30 test CombatCsvIntegrityTests.cs |
| P09 | PASS | not landed: baseline_shot power old 26 proposed 48 current 26 test AuditReconciliationV1Tests.cs |
| P10 | PASS | not landed: baseline_thrust power old 26 proposed 44 current 26 test no test |
| P11 | PASS | not landed: baseline_slam power old 30 proposed 64 current 30 test ApCostLadderTests.cs |
| P12 | PASS | not landed: baseline_snipe power old 30 proposed 60 current 30 test ApCostLadderTests.cs |
| P13 | PASS | not landed: baseline_multi_strike power old 8 proposed 16 current 8 test ApCostLadderTests.cs |
| P14 | PASS | not landed: guardbreaker_combo power old 10 proposed 23 current 10 test ApCostLadderTests.cs |
| P15 | PASS | not landed: overdraw_snipe power old 46 proposed 85 current 46 test no test |
| P16 | PASS | not landed: refined_slam power old 48 proposed 90 current 48 test no test |
| P17 | PASS | not landed: refined_volley power old 22 proposed 39 current 22 test no test |
| P18 | PASS | not landed: refined_blast power old 28 proposed 46 current 28 test no test |
| P19 | PASS | not landed: refined_barrage power old 12 proposed 18 current 12 test no test |
| P20 | PASS | not landed: baseline_volley power old 16 proposed 28 current 16 test no test |
| P21 | PASS | not landed: baseline_barrage power old 10 proposed 11 current 10 test no test |
| P22 | PASS | not landed: baseline_blast power old 22 proposed 33 current 22 test no test |
| P23 | PASS | not landed: quick_lunge power old 26 proposed 46 current 26 test no test |
| P24 | PASS | not landed: multi_thread_shot power old 12 proposed 20 current 12 test ApCostLadderTests.cs |
| P25 | PASS | partly: seam_cut power old 52 proposed 64 current 46 test WildBattleKitComposerTests.cs |
| P26 | PASS | partly: ap_siphon power old 56 proposed 70 current 54 test no test |
| P27 | PASS | partly: cinder_burst power old 18 proposed 24 current 15 test CombatIssuesRemediationV2Tests.cs |
| P28 | PASS | partly: blade_flurry power old 18 proposed 24 current 12 test ApCostLadderTests.cs |
| P29 | PASS | partly: zipped_strikes power old 26 proposed 32 current 18 test no test |
| P30 | PASS | not landed: frost_shards power old 12 proposed 25 current 12 test ApCostLadderTests.cs |
| P31 | PASS | not landed: chain_fork power old 12 proposed 25 current 12 test ApCostLadderTests.cs |
| P32 | PASS | partly: stone_barrage power old 16 proposed 30 current 14 test ApCostLadderTests.cs |
| P33 | PASS | not landed: civet_beam ap old 2 proposed 1 current 2 test ApCostLadderTests.cs |
| P34 | PASS | not landed: bulwark_blow ap old 3 proposed 2 current 3 test WildBattleKitComposerTests.cs |
| P35 | PASS | not landed: frost_shards ap old 3 proposed 3 current 2 test ApCostLadderTests.cs |
| P36 | PASS | not landed: chain_fork ap old 3 proposed 3 current 2 test ApCostLadderTests.cs |
| P37 | PASS | not landed: stone_barrage ap old 3 proposed 3 current 2 test ApCostLadderTests.cs |
| P38 | PASS | not landed: baseline_slam ap old 3 proposed 3 current 2 test ApCostLadderTests.cs |
| P39 | PASS | not landed: fork_split ap old 3 proposed 3 current 4 test ApCostLadderTests.cs |
| P40 | PASS | not landed: shard_scatter ap old 3 proposed 3 current 4 test ApCostLadderTests.cs |
| P41 | PASS | not landed: ember_shower ap old 3 proposed 3 current 4 test ApCostLadderTests.cs |
| P42 | PASS | not landed: automation_burst ap old 3 proposed 3 current 4 test ApCostLadderTests.cs |
| P43 | PASS | not landed: hammer_sync ap old 2 proposed 2 current 4 test ApCostLadderTests.cs |
| P44 | PASS | not landed: cryo_ray ap old 2 proposed 2 current 4 test ApCostLadderTests.cs |
| P45 | PASS | not landed: deep_dig ap old 3 proposed 3 current 4 test ApCostLadderTests.cs |
| P46 | PASS | not landed: interest_strike power old 18 proposed 30 current 18 test ApCostLadderTests.cs |
| P47 | PASS | not landed: interest_strike ap old 3 proposed 3 current 2 test ApCostLadderTests.cs |
| P48 | PASS | not landed: solid_strike power old 64 proposed 64 current 52 test no test |
| P49 | PASS | not landed: coordinated_shot power old 64 proposed 64 current 56 test ApCostLadderTests.cs |
| P50 | PASS | not landed: scorch_shot power old 64 proposed 64 current 52 test CombatIssuesRemediationV2Tests.cs |
| P51 | PASS | not landed: frost_shot power old 64 proposed 64 current 52 test no test |
| P52 | PASS | partly: Bot drain landed as Packet Skim (1 AP, drain 1) in Kitsauto, not Batch Debit (2 AP, drain 2) |
| P53 | PASS | landed: bench recharge ticks |
| P54 | PASS | landed: Gas Spike and FUD stages revert with the status via owned deltas |
| P55 | PASS | partly: fresh 0-vs-0 no longer refunds (strict >). The note asked for Def stage >= +1. Test covers the strict compare. |
| C41 | PASS | Crystal Bash power 44 AP 2 override MeleeDef double-below-30 mult 2 |
| C42 | PASS | Deghoul pool transaction_tax,volatile_shot,gas_bump,slipstream,baseline_striker / Balloom pool fuel_surge,volatile_shot,gas_bump,slipstream,baseline_slam,recoil_strike |
| C43 | PASS | volatile_shot display 'Gas War' cat DamagingRanged P72 AP4; recoil_strike display 'Hard Fork' cat DamagingMelee P72 AP4 |
| C44 | PASS | CombatApService.Drain has no per-turn cap in its signature block |
| C45 | PASS | SPR method: norm strike uses atk=def=60 (ratio 1), floor((P*u+1)*mult) with u from 0.85 to 1.0 in 21 steps, crit 0.0625 x1.5, times expected hits, accuracy, spread x2. AP point=23.82 = median 2 AP single norm strike / 2. Drain x0.6, gain x1, steal both. Stage credit is half a reference hit per stage of a damage stat, one future hit. Speed/accuracy/evasion and priority are tempo, priced at 0 HP. Median HP=124 melee def=53 ranged def=48. |
| C46 | PASS | tier median norm SPR/AP AP1:24.13, AP2:17.09, AP3:13.86, AP4:18.25 |
| C47 | PASS | rarity median norm SPR/AP Common:14.99(n=17), Uncommon:16.58(n=7), Rare:23.82(n=8), Epic:14.48(n=1), Legendary:25.64(n=5), Mythic:29.87(n=1) |
| C49 | PASS | catalog rare-typeless vs typed-common medians AP2 single typed-common 33.62 (n=7) typeless-rare None (n=0) []; AP3 single typed-common 46.49 (n=5) typeless-rare 37.4 (n=2) ['refined_slam', 'overdraw_snipe'] |
| C50 | PASS | same-type per-AP rivals (lower rarity, AP at or under the higher one, rate >= 95%) n=5 Frozen frost_shot Rare 23.82/AP vs cryo_ray Legendary 18.28/AP; Bot blade_flurry Common 17.1/AP vs multi_thread_shot Uncommon 17.08/AP; Gas gas_bump Common 16.57/AP vs volatile_shot Uncommon 10.48/AP; Block stone_barrage Common 16.52/AP vs bulwark_blow Epic 14.48/AP; Block solid_strike Rare 23.82/AP vs bulwark_blow Epic 14.48/AP |
| C48 | PASS | holder expected damage (chemistry on, crit expected, ratio of that body's atk to that foe's def) validape:hammer_sync->deghoul 113.1; validape:baseline_striker->deghoul 42.61; gilarime:cryo_ray->crackett 135.3; gilarime:frost_shot->crackett 88.23; kodiax:deep_dig->keyper 137.68; kodiax:ore_strike->keyper 99.67; balloom:volatile_shot->kitsauto 64.63; balloom:gas_bump->kitsauto 49.98; forkrook:fork_split->deghoul 204.07; forkrook:baseline_shot->deghoul 47.64; cinislug:cinder_burst->deghoul 92.96; cinislug:baseline_shot->deghoul 53.1; crackett:seam_cut->deghoul 100.08; kitsauto:blade_flurry->cinislug 37.69; deghoul:gas_bump->validape 47.69; deghoul:transaction_tax->validape 0.0 |
| C51 | PASS | turn-1 cover: only Ember Wall is 2 AP; the other four covers are 3 AP and miss turn 1. barrier_wall AP3 Barrier Wall None line 42, blockade AP3 Blockade Block line 83, ember_wall AP2 Ember Wall Burned line 106, ice_wall AP3 Ice Wall Frozen line 111, compound_guard AP3 Compound Guard Staked line 140 |
| C52 | PASS | Memory Leak AP 2 rarity Legendary stages 'Speed:2:1.0:true' utility 'ArmUtilityWindow:OwnerTurnStart:1:1:-2:0%:MeleeAtk:0:SingleEnemy:0%:false:0:memory_leak' text 'Target loses 2 AP next turn; user gains Speed +2.' |
| C53 | PASS | Transaction Tax AP 1 Epic utility 'StealAP:1:100%:SingleEnemy:transaction_tax' text 'Steal 1 AP from the target.' |
| C54 | PASS | Gas War utility 'NextMoveAPSurcharge:1:100%:Self:gas_war' text 'Heavy ranged hit; your next move costs +1 AP.' |
| C55 | PASS | Hammer Sync text 'Heavy hit; +20% damage if your partner is buffed.' bonus 0.2 cap 1.2 scaling 'Self:BuffedPartnerCountAtLeast:0:None:MeleeAtk:MeleeAtk' |
| C56 | PASS | Blink Lunge (quick_lunge) learn-pool holders=none power 26 AP 2 |
| C57 | PASS | Fee Collector learn-pool holders=none AP 0 cat Passive |
| C58 | PASS | Bot denial not in a learn pool: mev_extract AP2 Rare holders=none, thread_lock AP2 Legendary holders=none |
| C59 | PASS | learnable damaging moves that cannot be paid on turn 1 (regen +2 from 0): hammer_sync AP4 turn 2, cryo_ray AP4 turn 2, cinder_burst AP3 turn 2, cipher_burst AP3 turn 2, volatile_shot AP4 turn 2, fork_split AP4 turn 2, bulwark_blow AP3 turn 2, pickaxe_flurry AP3 turn 2, deep_dig AP4 turn 2, recoil_strike AP4 turn 2 |

Re-run: `python3 move_cost_curve_v2_check.py <snapshot> MOVE_COST_CURVE_V2_ANALYSIS.md` then `python3 move_cost_curve_v2_sim.py <snapshot> 320 sim.json`. The sim is the round-13 engine plus the four fixes named in its header, and the P wall design from `walls_pvp_v2_sim.py`.
