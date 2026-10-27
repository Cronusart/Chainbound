# Rarity-weighted Known rolls with on-chain VRF tiers — draft v1

> **Status:** DRAFT for approval, 2026-10-26. The Known curve is decided (**Ultra**, see "Decisions needed" 1); decisions 2–5 are still open. Nothing here is implemented. It extends [ROLL_REQUEST_VRF_DESIGN_v1.md](ROLL_REQUEST_VRF_DESIGN_v1.md) and answers its decision 1 (Known tier weights and membership). The VRF latency and cost numbers come from [MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md](../Solana/MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md).

## What changes for the player

Today a Candy Machine Known roll is a flat 1-in-10 pick from ten typeless Common/Uncommon baselines. After this change it works in two steps:

1. **Tier draw.** The VRF bytes pick a rarity tier using published odds, with Mythic and Legendary very rare.
2. **Move draw.** The bytes then pick one move, uniformly, from that tier's published list.

Both lists, and the odds, are public. Anyone can recompute a roll from its on-chain bytes.

## Two rarity systems (don't mix them up)

| | Known roll rarity | Stamp rarity |
|---|---|---|
| What it decides | Which **move** you get. Uses the move's own `Rarity` from `Moves.csv` | How strong the **modifier** is on a Stamped move |
| Today | Flat pick from 10 baselines | Server cut-offs 45 / 30 / 17 / 5 / 2 / **1**% |
| ECONOMY_01 baseline | Not defined | 45 / 30 / 17.5 / 5 / 2 / **0.5**% |

There's an existing drift: the server gives Stamp Mythic **1%**, while ECONOMY_01 and `VendingConfigSO` say **0.5%**. Decision 4 settles it.

## Proposed Known tier weights

Weights are integers in **parts per million** (1,000,000 total), so odds as low as 1 in 1,000,000 can be written exactly. Today's percent floats get rounded to basis points, which can't go below 0.01%.

Assumptions for the "time to get one" columns: a roll costs 5 RAM, and the vending cap is 10 per day.

### Recommended: "Steep"

| Tier | ppm | Chance | 1 in | Rolls on average | Days at 10/day | RAM on average |
|---|---:|---:|---:|---:|---:|---:|
| Common | 620,000 | 62% | 1.6 | — | — | — |
| Uncommon | 250,000 | 25% | 4 | 4 | <1 | 20 |
| Rare | 100,000 | 10% | 10 | 10 | 1 | 50 |
| Epic | 25,000 | 2.5% | 40 | 40 | 4 | 200 |
| Legendary | 4,500 | 0.45% | 222 | 222 | 22 | 1,111 |
| **Mythic** | **500** | **0.05%** | **2,000** | **2,000** | **200** | **10,000** |

What this means for a player who rolls the full 10 per day:

- **One month (300 rolls):** about a 14% chance of seeing any Mythic, and a 74% chance of any Legendary.
- **One year (3,650 rolls):** about an 84% chance of at least one Mythic.
- **A specific Mythic move** (one of the 8 in the pool): 1 in 16,000 per roll, about 4.4 years at the cap.

Mythic stays something most players hear about rather than own, but a dedicated player should get one within a year.

### Alternatives

| Preset | Common | Uncommon | Rare | Epic | Legendary | Mythic | Mythic odds | Mythic at the cap |
|---|---:|---:|---:|---:|---:|---:|---|---|
| ECONOMY_01 (as written) | 45% | 30% | 17.5% | 5% | 2% | 0.5% | 1 in 200 | ~20 days |
| **Steep (recommended)** | 62% | 25% | 10% | 2.5% | 0.45% | 0.05% | 1 in 2,000 | ~200 days |
| Ultra | 64% | 25% | 9% | 1.7% | 0.28% | 0.02% | 1 in 5,000 | ~500 days |

ECONOMY_01's curve was written for Stamp modifiers. For Known moves, where Mythic moves are run-defining (team cap 4, per-Chainling cap 2), it hands out a Mythic about every three weeks, which doesn't feel rare.

No pity timer. This matches ECONOMY_01 §Stamp ("no pity that guarantees a specific rarity"). A pity timer would also make a roll depend on history, which breaks "recompute from one PDA".

## Pool membership: which moves can come out of the Candy Machine

Proposed rule: a move is candy-eligible when it is player-obtainable (not `BOSS_ONLY`), not tagged `NO_CANDY`, and not a species signature (`isSignature` in any species' progression pool). Species signatures stay tied to their species, so a Chainling's own move remains special.

Applied to `Moves.csv` and the 19 species assets today:

| Tier | Eligible | Excluded as species signatures |
|---|---:|---|
| Common | 62 | — |
| Uncommon | 42 | (`civet_beam` is `NO_CANDY`) |
| Rare | 42 | `ore_strike` |
| Epic | 27 | `transaction_tax`, `shard_partition`, `fox_feint`, `bean_sip` |
| Legendary | 10 | `immutable_ledger`, `memory_leak`, `ember_wall`, `fork_split`, `deep_reap`, `ap_reserve`, `yield_spike`, `streamlined_flow`, `field_split`, `hammer_sync`, `cryo_ray` |
| Mythic | 8 | `fuel_surge`, `chaos_strike` |

Eligible Legendary moves: `defensive_reflex`, `overflow_reboot`, `combustive_shift`, `finish_burn`, `conductive_counter`, `thread_lock`, `crash_routine`, `shard_scatter`, `hype_train`, `gecko_guard`.

Eligible Mythic moves: `voltage_rush`, `risk_spike`, `data_scramble`, `glitch_barrage`, `viral_surge`, `jackpot_shot`, `double_or_nothing`, `meme_momentum`.

`all_in` (Epic, Meme) would become eligible under this rule. That's the "rarity-weighted Known rolls" condition from the `COMBAT_ISSUES_MASTER` availability note, so approving this rule also approves All-In in the Candy Machine.

How it's authored: `VendingConfigSO.knownPoolMoveIds` is replaced by the rule above, plus a `knownPoolExcludeIds` list for hand-picked exceptions. `CombatServerTablesExporter` generates one list per tier, and it fails the export if any tier ends up empty. That way every tier always has a move and nothing needs a roll-down rule.

Within a tier, every move is equally likely. Per-move weights can come later; they would just be another versioned table.

## Making the tier itself verifiable on-chain

The roll_request design has the server apply all the rules from the VRF bytes. This draft moves **the tier draw into the program**, so the rarity a player got is a fact on-chain. It can't be re-derived or disputed, even by Chainbound.

### New account: `RollOdds` (one per kind)

PDA `["roll_odds", kind]`, written by the program's admin authority.

| Field | Type | Notes |
|---|---|---|
| `kind` | u8 | 1 = Known, 2 = Stamp |
| `odds_version` | u32 | Bumped on every change |
| `cumulative_ppm` | [u32; 6] | Common…Mythic, strictly increasing, last = 1,000,000 |
| `pool_hash` | [u8; 32] | sha256 of the generated per-tier move lists (Known) or modifier pool (Stamp) |

`set_roll_odds(kind, cumulative_ppm, pool_hash)` validates the thresholds (strictly increasing, last exactly 1,000,000) and bumps `odds_version`. It emits `RollOddsChanged`, so every past change is public.

### Changes to `RollRequest`

- **At `request_roll`:** copy `odds_version`, `cumulative_ppm` and `pool_hash` from `RollOdds` into the request. The odds are then frozen for that roll: changing them mid-flight can't touch a pending request.
- **At `fulfill_roll` (the VRF callback):** compute
  `draw = u64_le(randomness[0..8]) % 1_000_000`, then `tier = first i where draw < cumulative_ppm[i]`.
  Store `tier: u8` on the PDA and emit `RollFulfilled { kind, operation_hash, tier, odds_version }`. That's six comparisons, so the oracle's callback stays cheap.
- **At `consume_roll(result_hash)`:** unchanged. The server settles the move pick, and `result_hash` covers it.

**Modulo bias.** `u64 % 1,000,000` is biased by at most 1,000,000 / 2⁶⁴ ≈ 5 × 10⁻¹⁴ per outcome, about a billion times smaller than the Mythic odds. That's simpler than rejection sampling, and the roll_request doc's `uniform(n)` can use the same bound. Move draws within a tier (n ≤ 62) use `u64_le(randomness[8..16]) % n`, with an even smaller bias.

### What the server still does

1. Reads the fulfilled PDA. It refuses to settle if the PDA's `pool_hash` doesn't match the generated table for its `odds_version`.
2. Picks the move: `TIER_POOLS[tier][u64_le(bytes 8..16) % len]`.
3. Writes the Known and debits RAM in one transaction, then calls `consume_roll(sha256(move_id ‖ tier ‖ operation_id))`.

An audit is three checks:

- recompute the tier from the bytes and the PDA's frozen thresholds; it must equal the on-chain `tier`;
- recompute the move from the published list for `pool_hash`;
- compare against `result_hash`.

### Stamp

Stamp uses the same mechanism with kind 2 and its own `RollOdds` row. The callback writes the Stamp rarity tier, and modifier selection and effect locks stay off-chain as in roll_request. The Stamp weights are decision 4.

## Where the numbers live

| Layer | Source |
|---|---|
| Authoring | `VendingConfigSO`: `knownTierWeightsPpm[6]` and `stampTierWeightsPpm[6]`, replacing `rarityTierWeightsPercent` |
| Server | Generated `roll_odds.generated.ts` (thresholds plus per-tier lists plus `pool_hash`) and the C# twin, with shared test vectors |
| Chain | `RollOdds` accounts, set by an operator script from the generated file; the script diffs the chain against the file before writing |
| Client | The Candy Machine shows the odds read from `RollOdds`, so the odds shown are the odds enforced |

`get_vending_config`'s weight override and `eventFavorBoost` can't change v2 odds, because the chain is authoritative. An event with boosted odds becomes an explicit new `odds_version` that is set, announced, and reverted on-chain.

## Rollout

1. Approve this draft: the decisions below.
2. Program: `RollOdds`, plus tier computation in `fulfill_roll` on top of the roll_request instructions; unit tests for the threshold edges (draw = 0, draw = boundary − 1, boundary, 999,999); autofixer; Devnet upgrade (separate approval).
3. Data: the ppm fields, the eligibility rule, the exporter per-tier lists and empty-tier check, and the generated tables with test vectors.
4. Server and Unity: settlement, audit endpoint, the Candy Machine odds display, and tier-coloured reveals sized to the 2.5 s latency budget.
5. Devnet: a 1,000-draw distribution check against the simulator (`OddsSimulatorWindow`, switched to ppm), plus one on-chain round trip per kind.

## Decisions needed

1. **Known curve:** **Decided 2026-10-26 (Cronus): Ultra.** Common 640,000 / Uncommon 250,000 / Rare 90,000 / Epic 17,000 / Legendary 2,800 / Mythic 200 ppm. At 10 rolls a day, that means:
   - **Mythic:** 1 in 5,000, about 500 days on average, about a 5.8% chance in a month, and about 52% in a year.
   - **Legendary:** 1 in 357, about 36 days.
   - **Epic:** 1 in 59, about 6 days.
   - **A specific Mythic move:** 1 in 40,000.
2. **Pool rule:** the rule above (all eligible minus signatures, `NO_CANDY` and `BOSS_ONLY`), or a hand-picked shorter list. This also decides All-In.
3. **Typed moves:** the rule puts typed moves of every element in the Candy Machine. ECONOMY_01 positions the market as "mostly typeless Common and Uncommon". Fine for Candy, or keep Common/Uncommon candy typeless?
4. **Stamp curve:** fix the drift to ECONOMY_01 (0.5% Mythic), or move Stamps to the same Steep curve.
5. **Tier on-chain** (recommended, this draft) or **server-only** (the original roll_request design).
