# NEW MOVE AND MECHANIC CONCEPTS

## Post-reconciliation revisit (2026-10-19)

Every line here is a **PROPOSAL** or a status note. No gameplay code, data, pool, or test was changed.

Checked against the local working tree at about 23:30 CDT on 2026-10-19, after Codex marked every todo in `audit_reconciliation_v1.plan.md` complete. Since the 14:07 snapshot this note was written from, `Moves.csv`, `SpeciesMovePools.csv`, `Statuses.csv`, every species and move asset, `CombatResolver.cs`, `DamageCalculator.cs`, `StatusProcessor.cs`, and `UtilityEffectExecutor.cs` have not changed at all. Only three files changed: `AuditReconciliationV1Tests.cs` (now 27 tests, all green per `audit_reconciliation_v1_tests.md`), `audit_reconciliation_v1_tests.md`, and `COMBAT_ISSUES_MASTER.md`. In other words, the reconciliation data and code were already in the 14:07 tree. What came after it was tests and docs.

Checks: `new_concepts_check.py` still gives **PASS 142, FAIL 0** on the current tree. A new check, `/workspace/balance/out/post_recon_revisit_check.py`, gives **PASS 44, FAIL 0**.

**Recount.** 211 moves. **17 empty** type × rarity cells and 20 thin cells, the same as before. The empty cells are None Mythic; Node Common and Mythic; Block, Encrypted, Burned, Mined, Staked, Bot, and Shard Mythic; Frozen Epic and Mythic; Staked Common; Compressed Uncommon, Epic, and Mythic; and Meme Rare. **Staked Common and Node Common are still empty.** There are still 77 learnable distinct moves.

**Tally of the 46 items below:** Still applies 36 · Already done 4 · Changed 5 · Obsolete 1.

| # | Item (section) | Status | Evidence now |
|---:|---|---|---|
| 1 | Headline counts and per-type grid (a) | Still applies | Recounted and unchanged: 211 / 17 empty / 20 thin / 77 learnable |
| 2 | "Earlier concepts" status table, all 19 rows | Still applies | No data or engine change since 14:07. Uplink is on Validape and `ap_router` is off. Pulse Shot is 26. Bean Sip is 1 AP with recharge 2. Grind Tax is 6 / `BOSS_ONLY` / `NO_CANDY` |
| 3 | Slot-gap map and thin-variety bullets (b) | Still applies | The pools are byte-identical |
| 4 | System Reset build contract (c) | Already done | `StripAllStatuses:100%:SingleEnemy`, 3 AP, recharge 2, still in Keyper. The new test `SystemReset_StripsAllGeneralStatusesAndRechargesOneTurn` passes |
| 5 | System Reset recharge locks only that move | Already done (it is the engine behavior) | `CombatActionEligibilityService.IsMoveRecharging` matches `rechargingMoveId` to `move.instanceId`. The user's other moves stay selectable |
| 6 | System Reset card text ("skip your next action") | Still applies | Not fixed. `Moves.csv` still says "Strip all target buffs and debuffs; skip your next action." Plan P2.6 kept it unchanged on purpose. `UNIMPLEMENTED_MOVE_EFFECTS.md` still says "Spends 2 AP" |
| 7 | Test `SystemReset_…LeavesStagesChemistryWalls_AndLocksOnlyItself` | Changed | Partly covered. The new test checks that Anchored (chemistry) survives and that `rechargeTurns == 2`. Stages, walls, and "another move is still selectable" are still untested |
| 8 | Hype Burst build contract (c) | Already done | `StripBuffsThenStage:2:…:MeleeAtk:1`, 2 AP, still in Cinislug. `HypeBurst_StripsUpToTwoBuffsAndGainsAtkPerRemoved` passes (2 buffs give Melee Atk +2, and the debuff stays) |
| 9 | Hype Burst should grant Ranged Atk, not Melee Atk | Still applies | It still writes `MeleeAtk`. Cinislug is Melee Atk 31 / Ranged Atk 63. Both of its damaging moves (Pulse Shot, Cinder Burst) are `DamagingRanged`, and `DamageCalculator.cs:243-244` reads Ranged Atk for those. The +1/+2 cannot raise any Cinislug hit |
| 10 | Test `HypeBurst_ZeroBuffs_GrantsNothing_AndDoesNotStripDebuffs` | Changed | The "debuffs are not stripped" half is now covered by the new test. The zero-buff → no-stage half is still untested |
| 11 | Crystal Extraction build contract (c) | Already done | `HealSelfPerTargetDebuff:5` then `Cleanse:99:SingleEnemy`, with no `HealPercentMaxHP`, still in Kodiax. Both tests pass. This matches Cronus's restated intent: heal the user 5% per foe debuff, then cleanse the foe |
| 12 | Test `CrystalExtraction_NoDebuffOrFullHp_…` | Still applies | Not added |
| 13 | Strips ignore stat stages | Still applies (confirmed) | `StripBuffs` and `StripAllGeneralStatuses` remove only General statuses and revert stages *owned by* those statuses. Stages from move stage columns (Pack Rally, Guard Stance, Bean Sip Speed, Slipstream) are never touched |
| 14 | Crystal Extraction can cash out a partner's Kindled / Cipher Lock (conflict 5) | Still applies | Accepted side effect of the approved cleanse. Kindled and Cipher Lock are General debuffs |
| 15–27 | The 13 move concepts (d): Yield Jab, Pair Ping, Seam Brace, Tine Strike, Pane Cut, Backdraft, Spoil Strike, Glitch Jab, Brace Line, Whiteout, Lag Script, Cover Call, Ember Line | Still applies (13 rows) | Every id and display name is still absent. Every named swap still passes the band test. `AnyFoeAPGreaterThanSelf` (Backdraft) is still unused in the CSV. Uplink, which Pair Ping and Cover Call rely on, is on Validape |
| 28 | M1 Pair Window | Still applies | The Uplink ledger hooks are at `CombatResolver.cs:3884`, `:4170` and `UtilityEffectExecutor.cs:261`, `:365` |
| 29 | M2 Stockpile Echo | Changed | Lower priority. The AP v2 revisit (tempo section) shows that a bank already costs a whole action, and that Drained or a skim on a banked bar already delays a 4 AP cast by a turn. An extra point on top punishes banking twice. Hold it until a boss-only case (Grind Tax) needs it |
| 30 | M3 Bench Relay | Still applies | The swap AP copy and `TickBenchedRecharge` are unchanged |
| 31 | M4 Boss Heat | Still applies | Grind Tax is still 6 AP. The scorer +60 is unchanged |
| 32 | M5 Overleveraged with no HP cost | Still applies | Status 208 is still `Shelved; not applied by any move.` No move or modifier names it. Also, the comment in `StatusId.cs:54` still says "Next heavy hit causes recoil", which is a stale comment, not a gameplay issue |
| 33 | (f) What works / doesn't / better | Still applies | Hype Burst Melee Atk, stage-blind strips, and 17 empty cells all remain |
| 34 | Rec 1: section (c) as contract + Ranged Atk + tooltips | Changed | The contract part is done (rows 4, 8, 11). Ranged Atk and the two tooltip rewrites are still open |
| 35 | Rec 2: Yield Jab on Rattlestake, Pair Ping on Validape | Still applies | Staked Common and Node Common are still empty |
| 36 | Rec 3: six typed-common swaps | Still applies | — |
| 37 | Rec 4: Pair Window + Stockpile Echo | Changed | Pair Window stands. Stockpile Echo is deferred (row 29) |
| 38 | Rec 5: Overleveraged +50% / +2 surcharge | Still applies | — |
| 39 | Rec 6: later adds (Seam Brace, Brace Line, Lag Script, Cover Call, Whiteout) | Still applies | — |
| 40 | Rec 7: leave Script Loop, Viral Surge, Wildcard Swap, Ghost, Cofi, Meme Rare alone | Still applies | All three utility columns are still empty, as deferred |
| 41 | Rec 8: no Gas Rares on Deghoul or Balloom | Still applies | — |
| 42 | Q1 Crystal Extraction: cleanse foe or self | Obsolete | Resolved. Cronus confirmed "heal the user 5% per foe debuff, then cleanse the foe", and the code, CSV, master row, and tests all match |
| 43 | Q2 Hype Burst Ranged Atk | Still applies | Default unchanged: author Ranged Atk |
| 44 | Q3 strips and stages | Still applies | Default unchanged: keep stages, and say "status buffs" in the tooltips |
| 45 | Q4 System Reset text | Still applies | The mechanic is done (row 5). The text is still open. Default: "Strip all target buffs and debuffs. Can't use System Reset next turn." |
| 46 | Q5–Q8 (Overleveraged producer, Pair Ping / Cover Call, Boss Heat, Bench Relay) | Still applies | Defaults unchanged |

Doc defect: the headline says "6 conflicts", and the text refers to conflicts 1, 4, and 5, but the note has no separate conflicts list. Read them as (h) Q1 (Crystal Extraction, now obsolete), Q4 (System Reset text), and row 14 (Crystal Extraction cashing out partner debuffs).

---

Snapshot: `chainbound_combat_snapshot_20261019_1407` (local tree, 2026-10-19 14:07 CDT). Facts below are from that tree only. Every suggestion is a **PROPOSAL**. Nothing here edits the repo, opens a pull request, or changes a pool.

The learn-pool band is a hard constraint. `PlayableLearnPoolsFollowTheBodyBand` is the rule that actually runs: a wild pool is five moves; the flagship is strictly rarer than every other move in the pool; at most one non-signature move may sit exactly one step above the body; nothing else may sit above the body. Starters (Validape, Gilarime, Kitsauto) only have to keep a single move at the top rarity. Ghost and Cofi Lootwak stay outside the rule. Boss-only moves stay out of learn pools and candy. A proposed move is legal only when the swap or add below passes that test. The current wild pools and the three starters pass it today.

Commons are aimed at power 30–40. A typed move should beat the typeless move of the same job. A rare typeless move is about a typed common. AP cost is by battle impact. Any lower-rarity card that can match a higher-rarity one is flagged in the move. STAB is undecided and is not set here. Self-damage stays shelved. Meme has no chemistry. Bot is the AP-denial type. Node is the ally type.

## (a) Headline counts

| Metric | Count |
|---|---:|
| Catalog moves (`Moves.csv`) | 211 |
| Type × rarity cells (15 types × 6 rarities) | 90 |
| Empty catalog cells | 17 |
| Thin catalog cells (exactly one move) | 20 |
| Distinct moves sitting in any learn, exclusive, or off-type column | 77 |
| New move concepts in this note | 13 |
| New mechanic concepts in this note | 5 |
| Conflicts with current data or with the green light | 6 |
| Questions only Cronus can answer | 8 |

Catalog counts, Common through Mythic:

| Type | Com | Unc | Rare | Epic | Leg | Myth |
|---|---:|---:|---:|---:|---:|---:|
| None | 26 | 14 | 14 | 4 | 1 | 0 |
| Node | 0 | 6 | 1 | 3 | 2 | 0 |
| Gas | 2 | 2 | 2 | 2 | 1 | 1 |
| Block | 3 | 1 | 2 | 3 | 1 | 0 |
| Encrypted | 4 | 2 | 1 | 2 | 1 | 0 |
| Burned | 2 | 3 | 2 | 2 | 2 | 0 |
| Frozen | 5 | 1 | 3 | 0 | 2 | 0 |
| Forked | 1 | 2 | 1 | 2 | 2 | 2 |
| Mined | 2 | 2 | 4 | 2 | 1 | 0 |
| Staked | 0 | 3 | 3 | 4 | 3 | 0 |
| Compressed | 6 | 0 | 2 | 0 | 1 | 0 |
| Bot | 5 | 3 | 2 | 2 | 1 | 0 |
| Glitched | 2 | 1 | 2 | 2 | 1 | 3 |
| Shard | 1 | 2 | 4 | 2 | 2 | 0 |
| Meme | 3 | 1 | 0 | 1 | 1 | 4 |

Empty cells: None Mythic; Node Common and Mythic; Block, Encrypted, Burned, Mined, Staked, Bot, and Shard Mythic; Frozen Epic and Mythic; Staked Common; Compressed Uncommon, Epic, and Mythic; Meme Rare.

Checker tally: **PASS 142, FAIL 0** (`new_concepts_check.py` against this snapshot).

## Earlier concepts, against this snapshot

These rows are status, not new designs. This note does not rebuild them.

| Earlier concept | Status now |
|---|---|
| Design-ideas moves `fee_tap`, `cipher_skim`, `relay_cut`, `brace_yield`, `fork_line`, `press_shot`, `aim_lock`, `slip_step`, `mempool_mark`, `node_beacon`, `cinder_idle`, `rampart`, `stockpile_shot`, `gas_vent`, `anchor_tap`, `quiet_ledger`, `toll_gate`, `confirmation_seal`, `reorg_pulse`, `partner_float` | Still open. None of the ids are in `Moves.csv`. Not repeated below. |
| Design-ideas M1 (slot AP, bench recharge, paused buffs) | Shipped behavior. Swap copies slot AP (`CombatResolver.cs:6687`). Bench recharge ticks (`CombatResolver.cs:504`). |
| M2 swap toll | Still open. The swap copies AP and does not spend 1. Not repeated. |
| M3 Finality, M4 Reorg, M6 Mempool Mark | Still open. The move ids are absent. `TurnStartPendingStatus` has no CSV row. |
| M5 chemistry catalyst | Still open. Hit chemistry takes the attack type (`CombatResolver.cs:2165`) and no chosen second type. |
| Boss beams Espresso Shot, Double Bean, Cream Top | Obsolete as new beams. Grind Tax is the decided boss card: Staked, Legendary, AllEnemies, power 30, 3 hits, AP 6, recharge 2, tags `NO_CANDY` and `BOSS_ONLY`. |
| Type-gap Node passive | Implemented as Uplink on Validape. `ap_router` is off that pool. Partner Beacon stays withdrawn. |
| Type-gap Kindling | In Cinislug's five. The CSV utility column is empty; the 6% body is the passive hook, not a new opcode. |
| Type-gap C1–C7 and D1–D7 (`brace_yield` through `loaded_ping`, including `ping_tax`) | Still open. Ids absent. Packet Skim is a different live card and is not those proposals. |
| Type-gap Meme chemistry | Still the rule. No Meme move is in any pool. Ghost's learn list is empty. |
| AP-economy Shielded AP and fat-pool steal | Still open as engine rules. Not rebuilt here. Stockpile Echo below is a different, smaller rule. |
| Move-cost concepts `cut_ledger`, `batch_debit`, `rime_jab`, `vested_strike` | Still open. Ids absent. Not repeated. |
| Status Resistance | Retired. Description is `Retired. No effect.` |
| Script Loop, Viral Surge, Wildcard Swap | Deferred. All three utility columns are empty. Left empty. |
| Accuracy Disrupt, Power Drain, Ore Restoration as pool swaps | Dropped, as decided. The rows remain in the catalog and in no species column. |
| Pulse Shot 26, Crystal Bash 44, Data Scramble 1 AP, Jammed Accuracy −1, passives before the status tick, Bean Sip recharge 2 with +1 AP | Holding. Bean Sip is Epic, Staked, self, AP 1, `NO_CANDY`. End step runs passives, then `ProcessEndOfTurn` (`CombatResolver.cs:468`). |
| Enemy-AI one-brain redesign | Still open. `UtilityStatusAI` is still its own scorer. It subtracts AP cost and adds 60 when a boss-only move is payable (`UtilityStatusAI.cs:135`). |
| Overleveraged as HP recoil | Shelved, and that shelf holds. Status 208's summary is `Shelved; not applied by any move.` No move row names it. A non-HP rework is **PROPOSAL** M5 below, because this round's brief asked for a version that matches the name. |

## (b) Slot-gap map

Learnable counts below are distinct move ids in any species' learn, exclusive, or off-type column. Bean Sip is the only Staked Epic in that set, and it belongs to Cofi. Off-type rows are included once (Cold Snap on Crackett, Deep Reap on Hopoch, Precision Strike and Solid Strike on their exception rows).

Role tags are a reading of the live payload: damage, buff, debuff, ap_boost, ap_denial, heal, cleanse, wall, status, passive, self_tax. Four cells are corrected by hand because a window opcode does not carry those words: Chilling Retort is a one-turn debuff reflect, Fox Feint is a once-per-battle counter, Isolation Tool applies Anchored, Field Split drops Evasion on entry. Memory Leak both drains 2 AP on the foe's next turn and gives the user Speed +2. Gas War's surcharge hits the user, so it is a self-tax, not Bot-style denial. Pickaxe Flurry raises the user's Melee Atk. Immovable Stance and Shard Partition are buff statuses.

| Type | Rarity | Learnable | Moves and roles |
|---|---|---:|---|
| None | Common | 7 | Quick Jab (damage) · Pulse Shot (damage) · Body Check (damage) · Focus Shot (damage) · Snap Strike (damage) · Pressure Point (damage, debuff) · Scatter Shot (damage) |
| None | Uncommon | 2 | Barrier Wall (wall) · Vector Shot (damage) |
| None | Rare–Mythic | 0 | — |
| Node | Common | 0 | — |
| Node | Uncommon | 3 | Hub Heal (heal) · Pack Rally (buff) · Uplink (passive) |
| Node | Rare | 0 | — |
| Node | Epic | 0 | — |
| Node | Legendary | 1 | Hammer Sync (damage) |
| Node | Mythic | 0 | — |
| Gas | Common | 2 | Gas Bump (damage) · Slipstream (buff, self_tax) |
| Gas | Uncommon | 1 | Gas War (damage, self_tax) |
| Gas | Rare | 0 | — |
| Gas | Epic | 1 | Transaction Tax (ap_denial) |
| Gas | Legendary | 0 | — |
| Gas | Mythic | 1 | Fuel Surge (ap_boost, status) |
| Block | Common | 1 | Stone Barrage (damage, debuff) |
| Block | Uncommon | 1 | Blockade (wall) |
| Block | Rare | 2 | Immovable Stance (buff, status) · Solid Strike (damage, debuff) |
| Block | Epic | 1 | Bulwark Blow (damage) |
| Block | Legendary | 1 | Immutable Ledger (passive) |
| Block | Mythic | 0 | — |
| Encrypted | Common | 3 | Cipher Burst (damage) · Key Rotation (buff) · System Reset (debuff) |
| Encrypted | Uncommon | 1 | Cipher Shot (damage, status) |
| Encrypted | Rare–Epic | 0 | — |
| Encrypted | Legendary | 1 | Memory Leak (ap_denial, buff) |
| Encrypted | Mythic | 0 | — |
| Burned | Common | 2 | Cinder Burst (damage, debuff) · Hype Burst (buff, debuff) |
| Burned | Uncommon | 1 | Kindling (passive) |
| Burned | Rare–Epic | 0 | — |
| Burned | Legendary | 1 | Ember Wall (wall) |
| Burned | Mythic | 0 | — |
| Frozen | Common | 3 | Chilling Retort (reflect) · Cold Snap (debuff) · Frost Shards (damage, debuff) |
| Frozen | Uncommon | 1 | Ice Wall (wall) |
| Frozen | Rare | 1 | Frost Shot (damage, debuff) |
| Frozen | Epic | 0 | — |
| Frozen | Legendary | 1 | Cryo Ray (damage, debuff) |
| Frozen | Mythic | 0 | — |
| Forked | Common | 1 | Chain Fork (damage) |
| Forked | Uncommon | 1 | Hard Fork (cleanse, damage) |
| Forked | Rare | 1 | Rapid Strike (damage) |
| Forked | Epic–Mythic | 0 | — |
| Forked | Legendary | 1 | Fork Split (damage) |
| Mined | Common | 2 | Crystal Extraction (cleanse, heal) · Pickaxe Flurry (buff, damage) |
| Mined | Uncommon | 1 | Deep Dig (damage) |
| Mined | Rare | 2 | Exploit Window (status) · Ore Strike (damage) |
| Mined | Epic | 0 | — |
| Mined | Legendary | 1 | Deep Reap (damage, heal) |
| Mined | Mythic | 0 | — |
| Staked | Common | 0 | — |
| Staked | Uncommon | 3 | Bean Beam (damage, debuff) · Compound Guard (buff, wall) · Stake Heal (heal) |
| Staked | Rare | 2 | Patience Passive (buff, passive) · Stake Strike (damage) |
| Staked | Epic | 1 | Bean Sip (ap_boost, buff, heal) — Cofi only |
| Staked | Legendary | 2 | AP Reserve (ap_boost) · Yield Spike (damage) |
| Staked | Mythic | 0 | — |
| Compressed | Common | 3 | Compression Trade (buff, self_tax) · Precision Strike (damage) · Zipped Strikes (buff, damage) |
| Compressed | Uncommon | 0 | — |
| Compressed | Rare | 1 | Lean Hit (damage) |
| Compressed | Epic | 0 | — |
| Compressed | Legendary | 1 | Streamlined Flow (passive, self_tax) |
| Compressed | Mythic | 0 | — |
| Bot | Common | 3 | Blade Flurry (damage) · Debuff Script (debuff) · Packet Skim (ap_denial) |
| Bot | Uncommon | 1 | Multi-Thread Shot (ap_boost, damage) |
| Bot | Rare | 0 | — |
| Bot | Epic | 1 | Fox Feint (counter) |
| Bot | Legendary–Mythic | 0 | — |
| Glitched | Common | 2 | Bug Fix (cleanse, heal, status) · Defense Corrupt (debuff) |
| Glitched | Uncommon | 1 | Error Correction (cleanse, heal) |
| Glitched | Rare–Legendary | 0 | — |
| Glitched | Mythic | 1 | Chaos Strike (damage) |
| Shard | Common | 1 | Isolation Tool (status) |
| Shard | Uncommon | 0 | — |
| Shard | Rare | 3 | Parallel Passive (buff, passive) · Partition Shot (damage, status) · Seam Cut (damage) |
| Shard | Epic | 1 | Shard Partition (buff, status) |
| Shard | Legendary | 1 | Field Split (debuff) |
| Shard | Mythic | 0 | — |
| Meme | all | 0 | — |

Where variety is thin, in the pools a player can actually roll:

- **Staked Common is an empty catalog cell.** Rattlestake's three on-band slots are typeless (Pulse Shot, Quick Jab, Pressure Point). That is why the learn-pool plan said there was no Staked Common.
- **Node Common is an empty catalog cell.** The only Node body is starter Validape. Its live Node cards are Hub Heal, Pack Rally, Uplink, and Hammer Sync. No Common, no Rare, no wall, no cleanse.
- **Compressed Uncommon is an empty catalog cell.** Humpacked's Uncommon slot is typeless Barrier Wall. There is no Compressed heal.
- **Forked Common is only Chain Fork**, a multi-hit. Forkrook's other two commons are typeless shots.
- **Shard Common is only Isolation Tool.** Crackett and Marrowhead lean on typeless jabs and snipes for damage.
- **Glitched Common has no damage.** Kreechank's damage common is typeless Snap Strike. Chaos Strike is the Mythic signature.
- **Burned Common has no single-target hit.** Cinislug's damage common is multi-hit Cinder Burst; the clean ranged click is typeless Pulse Shot.
- **Mined Common has a heal and a multi-hit, and no single Def break.** Kodiax's clean hit is typeless Body Check.
- **Gas Common has a melee hit and a self speed trade, and no catch-up.** Both Gas bodies are Common, so a Gas Rare (Fee Collector, AP Siphon) cannot enter those fives: it would be a second move above the body, or it would have to beat Transaction Tax / Fuel Surge as signature. Those Rares stay ungranted until a higher Gas body exists.
- **Block Common has no ally buff.** Blockade is the Uncommon wall. Stone Barrage is the only common, and it is damage.
- **Frozen Epic is an empty catalog cell.** Gilarime is the only Frozen body, and the signature is Legendary Cryo Ray, so an Epic can sit under it.
- **Bot's live denial is Packet Skim.** The Uncommon is a damage click that sometimes refunds AP. Thread Lock is Legendary and in no pool. Kitsauto's signature is Epic Fox Feint, so an Uncommon add does not tie it.
- **Meme Rare is empty, and every Meme learnable cell is empty.** Ghost stays unchanged. No proposal below puts a move on Ghost or Cofi, and none is boss-only.

Shard Uncommon already has Team Buffer and Lattice Guard in the catalog, ungranted. This note does not invent a second Shard Uncommon. Granting Team Buffer to Marrowhead, replacing Focus Shot or the typeless snipe, is legal (Uncommon is within an Uncommon body, Partition Shot stays the one step-up, Field Split stays the signature). That is a grant, not a new card.

## (c) Build specs: System Reset, Hype Burst, Crystal Extraction

Cronus green-lit these three to be built and kept, instead of pulled. In this snapshot the rows, opcodes, and happy-path tests are already present. The spec below is the contract those tests should keep, plus the edge cases they do not cover yet. Deviations from the authored row are marked **PROPOSAL**.

What "buff" and "debuff" mean in the opcodes: a General status with `IsBuff` or `IsDebuff`. Chemistry statuses stay. Stat stages from the move stage column stay. Walls are mitigation sources, not statuses. Recharging is General and is neither buff nor debuff, so a strip leaves it.

General buffs the strip can see: HODLed, Shard Partition, Encrypted Seal, Broadcast, Yield, Immovable, Surging, Unsealed, Relay, Damage Intercept. General debuffs include Cipher Lock, Kindled, Thread Lock, FUD, Sear Field, Staggered, Exploit Mark, MEV Extracted, Frozen Assets, Bot Swarm. Pack Rally, Power Surge, Guard Stance, and Bean Sip's Speed +1 are stages. They survive both strips.

### System Reset

Quoted intent:

> Strip all target buffs and debuffs; skip your next action. — `Moves.csv` description, and `UNIMPLEMENTED_MOVE_EFFECTS.md` (that note's "Spends 2 AP" is stale; the row is 3).

> BUILD System Reset. New StripAll/StripBuffs opcode + recharge. Keep 3 AP. Stays in Keyper. — `audit_reconciliation_v1.plan.md`.

The green light in this round's brief is "full foe buff strip plus a recharge." The authored contract strips General debuffs as well as buffs. That is the spec.

| Field | Value |
|---|---|
| Id | `system_reset` |
| Type / rarity / category | Encrypted / Common / StatusUtility |
| Target / contact | SingleEnemy / None |
| Power / AP / accuracy / hits | 0 / 3 / 0 / 1 |
| Recharge | 2 (the granting end tick counts, so the move itself skips one upcoming turn) |
| Payload | `StripAllStatuses:100%:SingleEnemy:system_reset` |
| Pool | Keyper. Signature stays Memory Leak (Legendary). This Common sits in the body band beside Cipher Burst and Key Rotation. |

Resolver hook, already present: `UtilityEffectExecutor.ApplyStripAllStatuses` calls `StatusProcessor.StripAllGeneralStatuses` (`UtilityEffectExecutor.cs:1061`, `StatusProcessor.cs:865`). Recharge is applied to the user after the action (`CombatResolver.cs:1816`). Accuracy 0 follows the utility hit rule (`DamageCalculator` treats `baseAccuracy <= 0` as a hit).

Edge cases:

- No General buff or debuff: nothing is removed, AP 3 is still spent, recharge still starts.
- Chemistry (Pumped, Jammed, Anchored, Amplified) stays.
- Stages stay. Bean Sip's Speed and Pack Rally's Atk do not fall off.
- Walls and other mitigation stay.
- The foe's own Recharging timer stays.
- Bench fighters are not a legal SingleEnemy target. A buff parked on the bench is safe.
- Bosses are legal targets. HODLed and Immovable drop, and owned stages on those statuses revert (`StatusProcessor.RevertChemistryStageEffects`). A boss-only move is not a status and is not stripped.
- The user's other moves stay selectable. **PROPOSAL:** keep that. The old sentence "skip your next action" reads like a full turn skip. Recharge 2 is the green-lit lock, and it is move-only. See conflict 4.

Test already in tree: `StripAllStatuses_ClearsBuffsAndDebuffs` (Yield and Thread Lock go, Anchored stays). `ReconciledMoveAssets_MatchApprovedNumbersAndPayloads` asserts recharge 2 and the opcode.

Test to add: `SystemReset_StripsGeneralStatuses_LeavesStagesChemistryWalls_AndLocksOnlyItself`.

Assertion: foe has Yield, Thread Lock, Anchored, a Speed stage of +1, and a live mitigation source. After System Reset, Yield and Thread Lock are gone, Anchored remains, Speed is still +1, mitigation remains, the user's System Reset is recharging, and Quick Jab is still selectable.

### Hype Burst

Quoted intent:

> Remove up to 2 target buffs; gain Atk +1 for each. — `Moves.csv`.

> BUILD Hype Burst. StripBuffs:2, then Atk +1 per buff removed. Keep 2 AP. Shares strip opcode. Stays in Cinislug. — `audit_reconciliation_v1.plan.md`.

| Field | Value |
|---|---|
| Id | `hype_burst` |
| Type / rarity / category | Burned / Common / StatusUtility |
| Target / contact | SingleEnemy / None |
| Power / AP / accuracy / hits | 0 / 2 / 0 / 1 |
| Recharge | 0 |
| Payload | `StripBuffsThenStage:2:100%:SingleEnemy:MeleeAtk:1:hype_burst` |
| Pool | Cinislug. Ember Wall stays the Legendary signature. Kindling is the one Uncommon step-up. This Common stays in the body band. |

Resolver hook: `ApplyStripBuffsThenStage` (`UtilityEffectExecutor.cs:1080`) removes up to `magnitude` General buffs, shortest duration first (`StatusProcessor.StripBuffs`, `StatusProcessor.cs:828`), then adds `stageAmount * removed` to the source. The stage cap on that path is 6, the table clamp (`ChainlingInstance.SetCombatStage`). Two removals grant +2, which matches `STAGE_ACTIVE_CHANGE_CAP` (`CombatConstants.cs:36`) for a single cast from 0. The opcode does not consult that constant; the cap of 2 removals is what keeps the cast inside it.

Edge cases:

- Zero buffs: no stage is granted, AP 2 is still spent. Debuffs are not removed.
- One buff: Melee Atk +1, and only that buff goes. The longer-duration buff stays when two are present and the cap is 2.
- Three buffs: the two shortest go. The third stays.
- Atk already at +6: the buffs still go; the stage does not rise.
- Cipher Lock on the user: `BuffGate` can refuse the Atk gain. The strip still happens. The card should say so if that lock is up.
- Chemistry buffs, stages, walls, bench, and Recharging behave as on System Reset.
- A boss with HODLed or Surging loses those statuses. A boss whose only "buff" is a stage loses nothing and gives no Atk.

**PROPOSAL:** change the authored stat from Melee Atk to Ranged Atk. Cinislug's live damage is Cinder Burst and Pulse Shot, and `DamageCalculator` uses Ranged Atk for ranged hits (`DamageCalculator.cs:244`). Melee Atk +1 does not raise those hits. Ranged Atk +1 per buff actually removed, still capped at two, is the payoff the pool can spend. A later melee Burned learner would want a second look. Until that swap, the existing test below is the melee contract.

Test already in tree: `StripBuffsThenStage_GrantsAtkEqualToBuffsRemoved` asserts source Melee Atk +2 and an empty foe status list when both seeds were buffs.

Test to add: `HypeBurst_ZeroBuffs_GrantsNothing_AndDoesNotStripDebuffs`. Assertion: foe has only Thread Lock; after the move, Thread Lock remains and the user's Melee Atk (or Ranged Atk, if the proposal is taken) is unchanged.

Test to add if the proposal is taken: `HypeBurst_GrantsRangedAtkEqualToBuffsRemoved`. Assertion: two General buffs removed, source Ranged Atk +2, Melee Atk unchanged.

### Crystal Extraction

Quoted intent:

> Heal 5% MaxHP per target debuff; then remove them. — `Moves.csv`.

> BUILD Crystal Extraction. Heal self 5% per foe debuff, then cleanse those off the foe. Keep SingleEnemy, 2 AP. Fix foe-heal bug. Stays in Kodiax. — `audit_reconciliation_v1.plan.md`.

The parenthetical in this round's brief says "then cleanse self." The CSV, the plan, and the test cleanse the foe. The spec follows those three. Conflict 1 asks Cronus which sentence wins.

| Field | Value |
|---|---|
| Id | `crystal_extraction` |
| Type / rarity / category | Mined / Common / StatusUtility |
| Target / contact | SingleEnemy / None |
| Power / AP / accuracy / hits | 0 / 2 / 0 / 1 |
| Recharge | 0 |
| Payload | `HealSelfPerTargetDebuff:5:100%:crystal_extraction` then `Cleanse:99:100%:SingleEnemy:crystal_extraction` |
| Pool | Kodiax. Ore Strike stays the Rare signature. Deep Dig is Uncommon, inside an Uncommon body, so it is not a step-up violation. This Common stays in the body band. |

`5` imports as 0.05 (`ParsePercentOrFloat` divides values above 1). Heal is `floor(source MaxHP × 0.05 × debuff count)`, and at least 1 when the count is positive and the user is not already full (`UtilityEffectExecutor.HealPercent`). The count is taken before the cleanse (`CountGeneralDebuffs`, `StatusProcessor.cs:808`). Cleanse removes General debuffs only, up to 99, which is the whole set that was counted.

Edge cases:

- No General debuff: heal 0, cleanse removes 0, AP 2 is still spent. Stages such as Def −1 do not count.
- User already at full HP: heal lands 0, and the foe's General debuffs still drop.
- User is the one healed. The foe's HP does not change. That is the foe-heal bugfix.
- The user's own debuffs stay. This card does not cleanse self under the green-lit payload.
- Chemistry debuffs (Jammed, Anchored, Rugged) stay. Cipher Lock, Kindled, Thread Lock, and Exploit Mark do not. Cashing those out for 5% each can delete a partner's setup. That is conflict 5, and the green-lit cleanse does it on purpose.
- Walls, bench, and boss targeting match System Reset. A benched foe's debuffs are not cashed out.

Test already in tree: `CrystalExtraction_HealsSelfFivePercentPerFoeDebuffThenCleansesFoe` (three debuffs, 100 MaxHP, 40 current HP becomes 55, foe HP stays 50, foe statuses empty). `CrystalExtraction_DoesNotHealTheFoe` asserts `healPercentMaxHP` is 0 and the opcode order is heal-self then cleanse.

Test to add: `CrystalExtraction_NoDebuffOrFullHp_DoesNotHealAndDoesNotCleanseSelf`. Assertion: with zero foe debuffs, both HP totals stay put and the user's own Thread Lock stays; with three foe debuffs and the user already at max HP, user HP stays at max, foe debuffs are gone, user's debuff stays.

## (d) New move concepts

Thirteen **PROPOSAL** cards. Each one names the pool swap that passes the band test. None of these ids or display names are in the catalog. None is boss-only, none carries `NO_CANDY`, and none goes on Ghost or Cofi. They do not reuse the twenty design-idea ids or the type-gap C/D ids.

Damage index used below is power × accuracy, the same sketch the design notes used. Pulse Shot, the typeless common benchmark that stays at 26 / 95, sketches to 24.7.

### 1. Yield Jab (`yield_jab`) — **PROPOSAL**

Staked, Common, DamagingMelee, SingleEnemy, Contact. Power 36, AP 2, accuracy 95, recharge 0. No rider. One contact hit.

Index 34.2, inside the common band, above Pulse Shot, below Stake Strike (Rare, 52) and Yield Spike (Legendary, 56). There is no other Staked Common, so there is no same-cell overlap.

Pools: Rattlestake replaces Pressure Point (`baseline_thrust`). Quick Jab and Pulse Shot stay, so the five keeps a priority click and a ranged click. Hopoch can also take it by replacing Snap Strike; the recommended home is Rattlestake, because that five has no typed damage at all. Cofi does not gain it.

Test: `YieldJab_IsLegalOnRattlestake_AndDoesNotTieApReserve`. Assertion: after the swap the pool is still five, AP Reserve is the only Legendary, Compound Guard is the only move above Common, and Yield Jab's power is 36.

### 2. Pair Ping (`pair_ping`) — **PROPOSAL**

Node, Common, Support, SingleAlly, contact None. Power 0, AP 2, accuracy 0, recharge 0. The ally gains Speed +1. Stage column `Speed:1:1.0:false`.

Pack Rally is Uncommon, all allies, Atk +1 and Speed +1, AP 3. Pair Ping is one ally and one stage at AP 2. It does not copy Hub Heal. The stage-column path already notifies Uplink (`CombatResolver.cs:4169`), so a Validape who pings an ally also pays that ally 1 AP once this turn if Uplink is equipped. That is the Node payoff, and typeless Speed Boost does not do it.

Pool: Validape adds it. Hammer Sync stays the only Legendary. Starters are not locked at five. No wild Node body exists, so no wild five can take it.

Rival flag: with Uplink, the ally gets Speed +1 and +1 AP for 2 AP. That is a strong Common. It still does not match Pack Rally's two stages on the whole team. If that pairing is too much, the card stays Speed +1 and Uplink remains a separate equip choice.

Test: `PairPing_RaisesAllySpeed_AndUplinkPaysOnce`. Assertion: ally Speed increases by 1, a second Pair Ping on the same ally in the same turn does not pay a second Uplink point, and Hammer Sync is still the unique top rarity.

### 3. Seam Brace (`seam_brace`) — **PROPOSAL**

Compressed, Uncommon, Support, Self, contact None. Power 0, AP 2, accuracy 0, recharge 0. Heal 8% MaxHP.

The Compressed Uncommon cell is empty. Humpacked has no heal. Self Repair Protocol is Bot, Uncommon, 15% at AP 1, so Seam Brace stays under that heal on purpose: Compressed is the small efficient brace, not the Bot repair. Typeless Heal Ally is 15% on an ally. This is a self heal, a different job, and it does not compete with Lean Hit's 1 AP jab.

Pool: Humpacked replaces Zipped Strikes. Barrier Wall stays. Lean Hit stays the only move one step above Uncommon. Streamlined Flow stays the Legendary signature.

Test: `SeamBrace_HealsEightPercent_AndHumpackedBandHolds`. Assertion: a 100 MaxHP user at 50 heals to 58, and the swapped pool still has one Rare (Lean Hit) and one Legendary (Streamlined Flow).

### 4. Tine Strike (`tine_strike`) — **PROPOSAL**

Forked, Common, DamagingMelee, SingleEnemy, Contact. Power 36, AP 2, accuracy 95, recharge 0. One hit. No priority tag.

Chain Fork is the other Forked Common, and it is the 2–4 hit button (power 12). A two-hit floor sketches to 22.8 and a three-hit sketch ties Tine at 34.2. Chain Fork is the wall-shredder. Tine is the single hit Forkrook does not have. Rapid Strike stays the Rare 1 AP priority finisher and is not copied (no `PRIOR` tag, power stays 36 at 2 AP rather than Rapid Strike's 26 at 1 AP).

Rival flag: damage per AP favors Rapid Strike. That is accepted. Tine is the turn-1 hit; Rapid Strike is the finisher. Tine must not grow a priority tag or it rivals the Rare.

Pool: Forkrook replaces Scatter Shot. Pulse Shot can stay or go in a later pass; the recommended drop is the typeless spread, because Fork Split is already the Legendary spread. Rapid Strike stays the only step-up. Fork Split stays the signature.

Test: `TineStrike_SingleHit36_DoesNotCarryPriority`. Assertion: hit count is 1, power 36, tags do not contain `PRIOR`, and Forkrook's pool still has exactly one Rare.

### 5. Pane Cut (`pane_cut`) — **PROPOSAL**

Shard, Common, DamagingMelee, SingleEnemy, Contact. Power 34, AP 2, accuracy 95, recharge 0. One hit.

Isolation Tool is the only Shard Common, and it is a status. Seam Cut is Rare at 46. Crystal Bash is Epic at 44 with the Def scaling. Pane Cut is the plain common hit and does not take either rider.

Pools: Crackett replaces Quick Jab. Marrowhead replaces the typeless snipe (`baseline_snipe`). Isolation Tool stays on both. Crackett's signature stays Shard Partition (Epic), and a Common sits under an Epic body. Marrowhead's signature stays Field Split; Partition Shot stays the only step-up.

Test: `PaneCut_LegalOnCrackettAndMarrowhead`. Assertion: both swapped pools pass the band test, power is 34, and Isolation Tool is still present on each.

### 6. Backdraft (`backdraft`) — **PROPOSAL**

Gas, Common, DamagingRanged, SingleEnemy, Projectile. Power 32, AP 2, accuracy 90, recharge 0. The hit always resolves. After a confirmed hit, if any foe has more AP than the user, the user gains 1 AP.

Payload: `GainAP:1:100%:Self:backdraft`, utility timing `AfterEachConfirmedHit`, policy `ResolveConditionsOnly`, condition `Self:AnyFoeAPGreaterThanSelf`. That condition already exists on the resolver and is unused in the CSV. A miss does not grant AP.

Index 28.8, under Gas Bump's 34.2, so the melee stays the better hit when you are ahead. The AP is the catch-up, which fits Gas tempo and leaves denial to Bot. Transaction Tax (Epic steal) and Fuel Surge (Mythic +3) stay above it. Fee Tap from the design notes is a different card (a 1 AP foe drain) and is not this one.

Rival flag: on the turn a foe is ahead, Backdraft deals a modest hit and gains 1 AP, which can be worth more than Gas Bump's cleaner damage. That is the card. When nobody is ahead, it is a worse Gas Bump and should lose the click.

Pool: Deghoul replaces Snap Strike. Gas Bump and Slipstream stay. Gas War stays the only Uncommon step-up. Transaction Tax stays the signature. Balloom can legally take the same swap for Body Check; the recommended home is Deghoul, so Balloom keeps a distinct self-trade kit. A Gas Rare still cannot enter either five.

Test: `Backdraft_GainsApOnlyWhenAFoeIsAhead_AndOnlyOnHit`. Assertion: foe AP 6 and user AP 2 grants +1 after a hit; foe AP 2 grants nothing; a forced miss grants nothing; Deghoul's signature is still Transaction Tax.

### 7. Spoil Strike (`spoil_strike`) — **PROPOSAL**

Mined, Common, DamagingMelee, SingleEnemy, Contact. Power 32, AP 2, accuracy 90, recharge 0. On hit, the foe's Melee Def stage drops by 1 (`MeleeDef:-1:1.0:false`).

Index 28.8 plus a guaranteed Def drop. Pickaxe Flurry is the multi-hit self-Atk button at 3 AP. Deep Dig is 72 power at 4 AP. Ore Strike is 52 at 2 AP and stays the signature, so this Common cannot match it. Crystal Extraction does not count this Def drop, because a stage is not a General debuff. The card should not promise an extraction setup.

Pool: Kodiax replaces Body Check. Crystal Extraction and Pickaxe Flurry stay. Ore Strike stays the only move above Uncommon.

Test: `SpoilStrike_DropsDefOnHit_AndMissDropsNothing`. Assertion: a hit sets foe Melee Def to −1, a miss does not, power is 32, and Ore Strike remains Kodiax's signature.

### 8. Glitch Jab (`glitch_jab`) — **PROPOSAL**

Glitched, Common, DamagingMelee, SingleEnemy, Contact. Power 36, AP 2, accuracy 85, recharge 0. One hit. The low accuracy is the chaos tell. No random self-hit (self-damage stays shelved, and Bit Rot already owns the random foe-stat click).

Index 30.6, just above Snap Strike's 28.5, so the typed common beats the typeless common it replaces. Chaos Strike is Mythic at 58 and stays the signature. Defense Corrupt and Bug Fix stay the utility commons. An Uncommon damage card cannot be added beside Error Correction: Kreechank is a Common body and already has its one step-up.

Pool: Kreechank replaces Snap Strike.

Test: `GlitchJab_Accuracy85Power36_ReplacesSnapStrikeOnly`. Assertion: accuracy is 85, power is 36, the pool still contains Error Correction as the only Uncommon, and Chaos Strike is the only Mythic.

### 9. Brace Line (`brace_line`) — **PROPOSAL**

Block, Common, Support, SingleAlly, contact None. Power 0, AP 2, accuracy 0, recharge 0. The ally gains Melee Def +1. Stage column `MeleeDef:1:1.0:false`.

Guard Stance is self Def +2 at AP 1. Brace Line does not beat that on the user's own body. It is the ally click Block's common band does not have. Blockade remains the Uncommon 25% wall at AP 3. Bulwark Blow remains the Epic Def-based hit.

Pool: Blockwyrm replaces Stone Barrage. That drops the only common damage from the five. Stone Barrage stays in the catalog and can still be equipped if it was learned elsewhere; the learn five would not roll it. Immutable Ledger stays the Legendary signature. Nothing in the five needs to be Mythic, because the body is already Legendary and the signature is the top.

Rival flag: losing Stone Barrage's multi-hit from the learn five is the cost. If that cost is wrong, do not ship the swap; the card can exist in the catalog without entering the five. The band allows the swap. It does not require it.

Test: `BraceLine_GivesAllyDefPlusOne_NotAWall`. Assertion: ally Melee Def increases by 1, the ally gains no mitigation source, and Blockwyrm's signature is still Immutable Ledger.

### 10. Whiteout (`whiteout`) — **PROPOSAL**

Frozen, Epic, StatusUtility, AllEnemies, contact None. Power 0, AP 2, accuracy 0, recharge 2. Every active foe's Speed stage drops by 1. Stage column `Speed:-1:1.0:false`, the same shape Bean Beam uses, with no damage and a recharge so it cannot be clicked every turn.

The Frozen Epic cell is empty. Frost Shot is Rare damage with a 25% Speed drop. Cryo Ray is the Legendary 80-power hit. Whiteout is the control Epic, not a damage upgrade, and it must not be read as a better Frost Shot. Recharge 2 means one skipped upcoming turn, and that recharge ticks on the bench.

Pool: Gilarime adds it. Cryo Ray stays the only Legendary. Starters are not size-locked. No wild Frozen body exists.

Test: `Whiteout_DropsEveryFoeSpeed_ThenRechargesOneTurn`. Assertion: both active foes lose 1 Speed, the bench foe does not, the move is recharging next turn, and Cryo Ray is still the unique top rarity.

### 11. Lag Script (`lag_script`) — **PROPOSAL**

Bot, Uncommon, DamagingRanged, SingleEnemy, Projectile. Power 36, AP 2, accuracy 95, recharge 0. On a confirmed hit, the foe's next move costs +1 AP. Payload `NextMoveAPSurcharge:1:100%:SingleEnemy:lag_script` at `AfterEachConfirmedHit`. A miss does not surcharge.

Index 34.2 plus a one-move tax. Packet Skim stays the Common 1 AP drain and is the better pure tax (1 AP, no hit required). Multi-Thread Shot stays the multi-hit that sometimes gains AP. Thread Lock is Legendary, blocks AP gain rather than adding a cost, and is not in this pool. Lag Script is how Bot taxes a stockpiled 4 AP swing without deleting the regen. It drains nothing, so it does not stack a second drain on top of Packet Skim in the same click.

Rival flag: power 36 at Uncommon sits on the typed-common index and then adds a tax, so it is deliberately ahead of a naked common hit. It does not reach Thread Lock, and it does not drain. If the tax plus the hit feels like a Rare, drop the power to 30 and keep the surcharge.

Pool: Kitsauto adds it. Fox Feint stays the only Epic. No wild Bot body exists.

Test: `LagScript_SurchargesNextMoveOnlyOnHit`. Assertion: a hit sets the foe's next-move surcharge to +1, a miss does not, Packet Skim is still in the pool, and Fox Feint is still the unique top rarity.

### 12. Cover Call (`cover_call`) — **PROPOSAL**

Node, Rare, Support, SingleAlly, contact None. Power 0, AP 2, accuracy 0, recharge 0. Heal the ally 10% MaxHP and give Speed +1.

Coordinated Shot is the only Node Rare in the catalog, and it is a 56-power hit at 3 AP with a buffed-partner rider. It is also in no pool. Cover Call is the support Rare, so the same type and rarity do not share a job. Hub Heal is 15% to all allies at 3 AP. Cover Call is a smaller single-ally heal plus a stage, at 2 AP. The Speed stage is what notifies Uplink, same as Pair Ping.

Pool: Validape adds it beside Pair Ping. Hammer Sync stays the only Legendary. A Rare under a Legendary signature is legal on a starter.

Rival flag: Pair Ping plus Cover Call plus Uplink is a lot of ally Speed. They stack as two separate clicks, each spending 2 AP, so a single turn cannot do both. That is the brake.

Test: `CoverCall_HealsTenPercentAndRaisesSpeed_WithoutMatchingHubHeal`. Assertion: one ally heals 10 on a 100 MaxHP body and gains Speed +1, the other ally is unchanged, and the pool's only Legendary is still Hammer Sync.

### 13. Ember Line (`ember_line`) — **PROPOSAL**

Burned, Common, DamagingRanged, SingleEnemy, Projectile. Power 30, AP 2, accuracy 95, recharge 0. One hit. No Atk drop.

Index 28.5. Cinder Burst's guaranteed two hits of 15 sketch to the same 28.5, cost 3 AP, and can drop Atk. Extra hits and the Atk drop are why Cinder Burst stays the button when a wall needs chewing. Ember Line is the single ranged hit so Cinislug can drop typeless Pulse Shot. Hype Burst stays the buff answer. Ember Wall stays the signature.

Rival flag: Ember Line is cheaper than Cinder Burst's floor. If Cinislug stops clicking Cinder Burst, this card is too efficient and should be cut or moved to 3 AP. The recommendation is to ship it and watch that click, not to pre-nerf the only typed single hit in the five.

Pool: Cinislug replaces Pulse Shot. Kindling stays the only step-up.

Test: `EmberLine_OneHitPower30_CinislugKeepsCinderBurst`. Assertion: hit count is 1, power is 30, Cinder Burst and Hype Burst remain, Ember Wall remains the only Legendary.

## (e) New mechanic concepts

Five **PROPOSAL** mechanics. They are not the design-notes M1–M6, not Shielded AP, and not a new Cofi beam.

### M1. Pair Window — **PROPOSAL**

Rule: once per turn, when this fighter has just raised an ally's stage (the Uplink ledger already records that ally), that fighter's next Node move this turn costs 1 less AP. The cost cannot fall below 1. The discount does not stack with Streamlined Flow; if both would apply, only one point comes off, and damaging moves still pay at least 1.

Why it is Node: Uplink pays the ally. Pair Window pays the coordinator back, on the next Node click, and only after they actually helped. Turn 1 with no ally stage pays nothing, which is the failure mode that killed Partner Beacon. Pair Ping and Cover Call are the Common and Rare ways to open the window. Pack Rally already opens it.

Engine: read the same ledger `NoteUplinkAlly` writes (`UtilityEffectExecutor.cs:365`, `CombatResolver.cs:4169`) inside the AP spend, before the move resolves. `SettleUplink` (`CombatResolver.cs:5445`) stays the ally's +1 and does not also grant this discount.

Risk: Validape with Uplink, Pair Ping, and a 4 AP Hammer Sync could start the discount and then swing. Hammer Sync is 4 AP, so −1 makes it 3, which is still a stockpile button on turn 1 (regen is +2). The floor of 1 stops a free hit. Do not let the discount apply to typeless moves, or Snap Strike becomes the best Node button.

Smallest version: the discount applies only to a move whose `BaseType` is Node, only on the same turn as the ally stage, only once, and it is a logged "Pair Window −1 AP". No new status.

### M2. Stockpile Echo — **PROPOSAL**

Rule: the first AP drain or steal that lands on a fighter who currently has 6 or more AP removes 1 extra AP, once per turn for that fighter. At 3 AP or below, drains and steals remove their authored amount and never the extra. Spending down before the tax is the counterplay. High-damage clicks that need a stockpile (Hammer Sync at 4, All-In, Grind Tax at 6) are what the extra point hunts. A fighter who spent to 2 AP loses only the base nick.

This is not Shielded AP, and it does not change Fee Collector's number. Bot stays the denial type: Packet Skim and Lag Script are the player-facing tools. Gas Transaction Tax is a steal and would also take the extra point at 6+, which is the right punishment for a stockpile and is still one extra, not a second system.

Engine: the drain and steal path in the AP service, called from `UtilityEffectExecutor` (`LogDrain` at `UtilityEffectExecutor.cs:1127` is the log, not the spend). `UtilityStatusAI` should score a drain at 0 when the foe is already at 0, so the bot does not tax an empty bar. The boss +60 gate stays.

Risk: Packet Skim plus a chemistry Drained tick plus the extra point can pile up. The extra is limited to the first removal of the turn, and Drained's later end-of-turn tick does not also take it. Cap of 10 and floor of 0 stay. Thread Lock still blocks gains and is not itself a drain, so it does not consume the extra-point flag.

Smallest version: one integer on the fighter, `stockpileEchoTurn`, set when the extra point is taken. No new status and no CSV change.

### M3. Bench Relay — **PROPOSAL**

Rule: on a voluntary swap, the player may move one General buff from the outgoing fighter onto the incoming fighter. Remaining duration is kept, not refreshed. Debuffs stay on the body that earned them. Recharging stays on its move and keeps ticking on the bench. KO auto-swaps do not relay. Mitigation and walls do not relay, because they are not statuses. Stages do not relay.

This is the bench interaction that M1 describes and does not sell. M2's swap toll is a different proposal and is not part of this rule. The swap still costs 0 AP.

Engine: the swap block that already copies slot AP (`CombatResolver.cs:6687`) is the place to move one `StatusInstance` after the AP copy. `TickBenchedRecharge` (`CombatResolver.cs:504`) is unchanged.

Risk: relaying Immovable or HODLed onto a fresh body is strong. The limit of one buff, and the choice being explicit, is the brake. Relaying Yield moves an end-of-turn AP buff onto the fighter who can spend it. That is the point, and it is one buff, not the whole bank.

Smallest version: a swap request carries an optional status instance id. Empty means today's swap. One EditMode test moves Yield and leaves a debuff behind.

### M4. Boss Heat — **PROPOSAL**

Rule: after a boss has started three of its own turns on the field, its boss-only moves cost 1 less AP, once, floored at 1. Grind Tax goes from 6 to 5. It is still a stockpile button. It is not a new beam, and Bean Beam is untouched. Leaving the field resets the count. The discount does not apply to Bean Sip or Bean Beam, because those are not `BOSS_ONLY`.

From an empty bar, Bean Sip's +1 and the +2 regen still do not put Grind Tax on turn 1 or turn 2. The discount matters on the turn Cofi has 5 AP and would otherwise wait for 6. That is the pressure: the player who chipped Cofi's AP down to 5 is no longer safe from the spread.

Engine: a turn counter on the boss, incremented from the same start-of-turn pass that grants `apPerTurn` (2) under the cap of 10. Eligibility and `UtilityStatusAI.cs:135` use the discounted cost, so the +60 bonus follows the real price. `MoveAvailability.IsBossOnly` is the gate (`CombatResolver.cs:1746`).

Risk: stacking with a future AP buff could pull Grind Tax forward by a full turn. The smallest version discounts only `BOSS_ONLY` moves, by 1, after three turns, and never below 1. If a playtest shows turn-3 Grind Tax every fight, the trigger moves to four turns. It does not move to a new attack.

Smallest version: Grind Tax at 5 AP after three boss turns is legal and is selected when the scorer's +60 applies; at 4 AP it is not.

### M5. Overleveraged, no HP — **PROPOSAL**

Status 208 stays. The shelved summary is replaced with a body that matches the name and does not spend HP.

Rule: Overleveraged is a General consumed debuff, duration 2. The owner's next damaging hit that lands deals +50% damage, then the status is consumed and that owner gains a +2 next-move AP surcharge. A miss does not consume it. No HP loss, no self-KO, no recoil. Voltage Rush and Risk Marker stay on `NextMoveAPSurcharge` and do not re-apply this status.

Risk and reward: one hit is half again as hard, and the following action is expensive. A 2 AP move becomes 4. That is the overlever. It does not delete the fighter.

Engine: a consumed flag read by the damage multiplier, then `NextMoveAPSurcharge` of 2 on the owner, in the same hit path that already applies Surging-style next-hit bonuses. No learn-pool producer in this version, so no band changes. `risk_spike` (Leverage Up) is ungranted and is the optional producer if Cronus wants a card later. It is not added to Forkrook: a Mythic on an Uncommon body cannot sit beside Fork Split unless Fork Split leaves the five.

Smallest version: a test applies the status directly, lands one hit for 1.5×, asserts the follow-up surcharge is +2, and asserts a miss leaves the status up. No species row changes.

## (f) What works, what doesn't, what could be better

What works. The band test matches the live fives, including the starters' unique signatures. Typed commons that exist, such as Gas Bump at 36, sit above Pulse Shot at 26. The three green-lit opcodes exist, Keyper, Cinislug, and Kodiax still hold them, and the happy-path tests match the plan. Packet Skim is real Bot denial in a starter five. Uplink is real Node payoff. Slot AP and bench recharge already do what the design note said they do. Grind Tax is the 6 AP boss spread, tagged so it cannot enter a candy roll.

What doesn't. Seventeen catalog cells are empty, and the wild fives hide several of them behind typeless commons. Rattlestake has no Staked move below Legendary except Compound Guard. Hype Burst writes Melee Atk for a ranged five, so the Atk it grants does not raise Cinislug's clicks. The strip cards ignore the buffs players actually click, because those are stages, and they do catch the rare status buffs (HODLed, Surging, Immovable), which is a narrower card than the tooltip says. Crystal Extraction will delete a partner's Kindled or Cipher Lock to heal 5% a stack. Gas Rare denial cannot be learned by Deghoul or Balloom without breaking the band. Meme has nowhere to go while Ghost is frozen. Script Loop and the Meme cards are still blank, as deferred.

What could be better. Put one typed common into each wild five that is currently borrowing Pulse Shot or Snap Strike, using the swaps in section (d), before anyone adds another Epic. Say "status buffs" on System Reset and Hype Burst until a stage strip is a deliberate rarity decision. Point Hype Burst at Ranged Atk so the live pool can spend it. Add Pair Window and Stockpile Echo as rules, not as another row of 1 AP drains. Leave the Meme Rare cell empty.

## (g) Ranked recommendations

1. **PROPOSAL.** Treat section (c) as the build contract for the three green-lit cards. Keep the authored numbers. Add the edge tests. Change Hype Burst's stage to Ranged Atk, and change both strip tooltips so they say status buffs and a move-only recharge.
2. **PROPOSAL.** Fill the two empty cells a live body can legally learn: Yield Jab on Rattlestake (replace Pressure Point) and Pair Ping on Validape (add, Hammer Sync stays the only Legendary).
3. **PROPOSAL.** Replace typeless commons with the typed commons in section (d): Tine Strike, Pane Cut, Glitch Jab, Spoil Strike, Ember Line, Backdraft. One swap each. Do not add a second step-up.
4. **PROPOSAL.** Ship Pair Window and Stockpile Echo as the smallest Node and AP-counterplay rules. Do not ship the design-note card pack in the same pass.
5. **PROPOSAL.** Give Overleveraged the +50% hit and the +2 surcharge, with no HP and no learn-pool producer.
6. **PROPOSAL.** Add Seam Brace, Brace Line, Lag Script, Cover Call, and Whiteout only after the common swaps. Each one is legal on the named body. Brace Line is the one that removes a damage common from a five, so it is the easiest to hold back.
7. **PROPOSAL.** Leave Script Loop, Viral Surge, Wildcard Swap, Ghost, Cofi's learn list, and the Meme Rare cell untouched.
8. **PROPOSAL.** Do not grant Gas Rares to Deghoul or Balloom. The band forbids it.

## (h) Questions only Cronus can answer

1. Crystal Extraction cleanses the foe in the CSV, the plan, and the test. This brief's short parenthetical says cleanse self. **Default:** cleanse the foe, and leave the user's debuffs in place.
2. Hype Burst grants Melee Atk. Cinislug's live hits use Ranged Atk. **Default:** author Ranged Atk on this card.
3. Strip cards do not remove stat stages. Pack Rally and Power Surge survive them. **Default:** keep that, and rewrite the tooltips to say status buffs, so a Common does not erase an Uncommon team buff.
4. System Reset's description says the user skips their next action. Recharge 2 only locks System Reset. **Default:** keep the move-only recharge, and fix the sentence.
5. Overleveraged's new body has no producer. **Default:** ship the body with a direct test, and add no learn-pool card until a later pass. Do not hang it on Forkrook.
6. Validape's list is not size-locked, and the learn-pool plan said to leave that membership. Pair Ping and Cover Call are adds. **Default:** allow Pair Ping now, and hold Cover Call until Pair Ping has been seen in a fight.
7. Boss Heat makes Grind Tax cost 5 after three turns on the field. **Default:** yes. It stays a stockpile click, and it does not touch Bean Sip or Bean Beam.
8. Bench Relay moves one General buff on a voluntary swap and leaves KO swaps alone. **Default:** yes, duration unchanged, stages and walls stay put.

## Machine claims

The checker reads every line that starts with `CLAIM`.

CLAIM move_count 211
CLAIM empty_count 17
CLAIM thin_count 20
CLAIM learnable_count 77
CLAIM row None 26 14 14 4 1 0
CLAIM row Node 0 6 1 3 2 0
CLAIM row Gas 2 2 2 2 1 1
CLAIM row Block 3 1 2 3 1 0
CLAIM row Encrypted 4 2 1 2 1 0
CLAIM row Burned 2 3 2 2 2 0
CLAIM row Frozen 5 1 3 0 2 0
CLAIM row Forked 1 2 1 2 2 2
CLAIM row Mined 2 2 4 2 1 0
CLAIM row Staked 0 3 3 4 3 0
CLAIM row Compressed 6 0 2 0 1 0
CLAIM row Bot 5 3 2 2 1 0
CLAIM row Glitched 2 1 2 2 1 3
CLAIM row Shard 1 2 4 2 2 0
CLAIM row Meme 3 1 0 1 1 4
CLAIM learnrow None 7 2 0 0 0 0
CLAIM learnrow Node 0 3 0 0 1 0
CLAIM learnrow Gas 2 1 0 1 0 1
CLAIM learnrow Block 1 1 2 1 1 0
CLAIM learnrow Encrypted 3 1 0 0 1 0
CLAIM learnrow Burned 2 1 0 0 1 0
CLAIM learnrow Frozen 3 1 1 0 1 0
CLAIM learnrow Forked 1 1 1 0 1 0
CLAIM learnrow Mined 2 1 2 0 1 0
CLAIM learnrow Staked 0 3 2 1 2 0
CLAIM learnrow Compressed 3 0 1 0 1 0
CLAIM learnrow Bot 3 1 0 1 0 0
CLAIM learnrow Glitched 2 1 0 0 0 1
CLAIM learnrow Shard 1 0 3 1 1 0
CLAIM learnrow Meme 0 0 0 0 0 0
CLAIM pool_band
CLAIM move system_reset BaseType Encrypted
CLAIM move system_reset Rarity Common
CLAIM move system_reset Category StatusUtility
CLAIM move system_reset Targeting SingleEnemy
CLAIM move system_reset Power 0
CLAIM move system_reset APCost 3
CLAIM move system_reset Accuracy 0
CLAIM move system_reset RechargeTurns 2
CLAIM move system_reset UtilityEffects StripAllStatuses:100%:SingleEnemy:system_reset
CLAIM desc system_reset Strip all target buffs and debuffs; skip your next action.
CLAIM move hype_burst BaseType Burned
CLAIM move hype_burst Rarity Common
CLAIM move hype_burst APCost 2
CLAIM move hype_burst RechargeTurns 0
CLAIM move hype_burst UtilityEffects StripBuffsThenStage:2:100%:SingleEnemy:MeleeAtk:1:hype_burst
CLAIM desc hype_burst Remove up to 2 target buffs; gain Atk +1 for each.
CLAIM move crystal_extraction BaseType Mined
CLAIM move crystal_extraction Rarity Common
CLAIM move crystal_extraction Targeting SingleEnemy
CLAIM move crystal_extraction APCost 2
CLAIM move crystal_extraction UtilityEffects HealSelfPerTargetDebuff:5:100%:crystal_extraction|Cleanse:99:100%:SingleEnemy:crystal_extraction
CLAIM desc crystal_extraction Heal 5% MaxHP per target debuff; then remove them.
CLAIM pool_has keyper system_reset
CLAIM pool_has cinislug hype_burst
CLAIM pool_has kodiax crystal_extraction
CLAIM pool_has validape uplink
CLAIM pool_lacks validape ap_router
CLAIM move baseline_shot Power 26
CLAIM move crystal_bash Power 44
CLAIM move data_scramble APCost 1
CLAIM move bean_sip APCost 1
CLAIM move bean_sip RechargeTurns 2
CLAIM move grind_tax APCost 6
CLAIM tags grind_tax BOSS_ONLY
CLAIM tags grind_tax NO_CANDY
CLAIM tags bean_sip NO_CANDY
CLAIM desc status_resistance Retired. No effect.
CLAIM utility_empty script_loop
CLAIM utility_empty viral_surge
CLAIM utility_empty wildcard_swap
CLAIM absent fee_tap
CLAIM absent cipher_skim
CLAIM absent relay_cut
CLAIM absent brace_yield
CLAIM absent fork_line
CLAIM absent press_shot
CLAIM absent aim_lock
CLAIM absent slip_step
CLAIM absent mempool_mark
CLAIM absent node_beacon
CLAIM absent cinder_idle
CLAIM absent rampart
CLAIM absent stockpile_shot
CLAIM absent gas_vent
CLAIM absent anchor_tap
CLAIM absent quiet_ledger
CLAIM absent toll_gate
CLAIM absent confirmation_seal
CLAIM absent reorg_pulse
CLAIM absent partner_float
CLAIM absent ping_tax
CLAIM absent cut_ledger
CLAIM absent batch_debit
CLAIM absent rime_jab
CLAIM absent vested_strike
CLAIM absent yield_jab
CLAIM absent pair_ping
CLAIM absent seam_brace
CLAIM absent tine_strike
CLAIM absent pane_cut
CLAIM absent backdraft
CLAIM absent spoil_strike
CLAIM absent glitch_jab
CLAIM absent brace_line
CLAIM absent whiteout
CLAIM absent lag_script
CLAIM absent cover_call
CLAIM absent ember_line
CLAIM display_absent Yield Jab
CLAIM display_absent Pair Ping
CLAIM display_absent Seam Brace
CLAIM display_absent Tine Strike
CLAIM display_absent Pane Cut
CLAIM display_absent Backdraft
CLAIM display_absent Spoil Strike
CLAIM display_absent Glitch Jab
CLAIM display_absent Brace Line
CLAIM display_absent Whiteout
CLAIM display_absent Lag Script
CLAIM display_absent Cover Call
CLAIM display_absent Ember Line
CLAIM legal_swap rattlestake baseline_thrust Common
CLAIM legal_swap hopoch baseline_striker Common
CLAIM legal_swap validape - Common
CLAIM legal_swap validape - Rare
CLAIM legal_swap humpacked zipped_strikes Uncommon
CLAIM legal_swap forkrook baseline_volley Common
CLAIM legal_swap crackett baseline_jab Common
CLAIM legal_swap marrowhead baseline_snipe Common
CLAIM legal_swap deghoul baseline_striker Common
CLAIM legal_swap balloom baseline_slam Common
CLAIM legal_swap kodiax baseline_slam Common
CLAIM legal_swap kreechank baseline_striker Common
CLAIM legal_swap blockwyrm stone_barrage Common
CLAIM legal_swap gilarime - Epic
CLAIM legal_swap kitsauto - Uncommon
CLAIM legal_swap cinislug baseline_shot Common
