# roll_request: MagicBlock VRF for Known rolls and Stamps — design v1

> **Status:** DRAFT for review, 2026-10-26. Nothing here is implemented. It depends on a passing Devnet efficacy run ([MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md](../Solana/MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md)). Closed-beta policy still blocks Stamp and vending in the client, so this ships behind that gate.

## Problem

Known rolls and Stamps get their entropy from an HMAC of a server secret:

- **Known:** `deriveKnownResult` hashes `known:{profile}:{idempotency}` with the secret, then `ownership_roll_known_profile_atomic` debits RAM and writes the Known in one Postgres call. Today that's a flat pick from 10 baselines.
- **Stamp:** `deriveStampResult` hashes the profile, Known, modifier count, `generator_version` and `pool_hash` with the secret. Its bytes pick rarity (cut-offs 4500/7500/9200/9700/9900 of 10,000), modifiers from the host-filtered pool, and the effect values. `prepare_profile_stamp_ticket` then records the result on-chain.

Whoever holds the secret can predict or choose results. Economy audit 05 item 4 flags this ("VRF unused by Stamp").

## Goals and non-goals

Goals:

- Entropy that no one, including Chainbound, can predict or choose, with an on-chain proof per result.
- Anyone can recompute any result from public bytes and public rule tables.
- One result per paid operation; no rerolling.
- Keep the existing selection logic: host filter, second-modifier pairing, effect locks, and the pinned pool and its on-chain hash.

Non-goals: fee or RAM-price changes, selection logic on-chain, Ephemeral Rollups, PvP.

## Architecture: VRF supplies the bytes, the server applies the rules

```
client ──► Edge fn (roll / prepare_stamp)
             1. create operation row: seed, status=vrf_pending, RAM reserved
             2. result authority signs request_roll ──► Chainbound program ──CPI──► MagicBlock VRF
                                                                                      │ oracle + proof
             3. reconcile cron reads fulfilled roll_request ◄── fulfill_roll callback ◄┘
             4. derive result from bytes (same rules as today)
             5. settle: write Known / prepare stamp ticket, then consume_roll(result_hash)
client ◄── status endpoint (pending → settled)
```

The server keeps applying the rules because the rule tables (host compatibility, pools, effect bounds) are large, versioned, and already generated for TS and C#. Putting them on-chain would cost compute and make every rules change a program upgrade.

## On-chain: the `RollRequest` account

A generalized `EncounterRequest`. Encounter randomness keeps its own path untouched.

| Field | Type | Notes |
|---|---|---|
| `status` | u8 | Pending, Fulfilled, Consumed (first, so polling reads a short prefix) |
| `randomness` | [u8; 32] | Set only by the callback |
| `kind` | u8 | 1 = Known roll, 2 = Stamp |
| `operation_hash` | [u8; 32] | `sha256(operation_id)`; operation IDs are the existing RAM ledger UUIDs |
| `subject_hash` | [u8; 32] | `sha256(profile_id)`, or for a Stamp `sha256(profile_id ‖ known_id ‖ host_move_id)` |
| `generator_version` | u32 | VRF results start at a new version |
| `pool_hash` | [u8; 32] | Copied from `OwnershipConfig` at request time |
| `requested_at_slot`, `fulfilled_at_slot` | u64 | |
| `result_hash` | [u8; 32] | Written by `consume_roll`: hash of the settled result |
| `bump` | u8 | |

PDA seeds are `["roll_request", kind, operation_hash]`. That gives one account per operation whoever pays, so requesting twice for the same operation fails at `init`.

The VRF `caller_seed` is
`sha256("chainbound:roll:v1" ‖ kind ‖ operation_hash ‖ subject_hash ‖ generator_version ‖ pool_hash)`.
This binds the oracle's input to the operation, the player, and the rule version.

Instructions:

| Instruction | Signer | Effect |
|---|---|---|
| `request_roll(kind, operation_hash, subject_hash)` | `GameConfig.result_authority` (also the payer) | Init the PDA as Pending, CPI `create_request_scoped_randomness_ix` with the PDA as the writable callback account. Uses the `#[vrf]` context, like `RequestEncounter`. |
| `fulfill_roll(randomness)` | VRF program identity (`#[vrf_callback]`) | Pending → Fulfilled; store the bytes and the slot |
| `consume_roll(result_hash)` | result authority | Fulfilled → Consumed; store `result_hash`; emit `RollConsumed { kind, operation_hash, result_hash }` |
| `close_roll()` | result authority | Consumed only, at least N slots after consume; returns the rent to the result authority |

Only the result authority can request. Players have no reason to, and it keeps guest (walletless) profiles working, since the server pays.

`EncounterRequest` has no close path, so its rent is locked for good. `close_roll` fixes that for rolls, and the emitted events keep the audit trail after an account closes.

The Rust goes through `program_autofixer` per CLAUDE.md, and the IDL and Unity contract are regenerated, before review.

## Off-chain flow

### Known roll (becomes two steps)

1. `ownership_roll_known` validates, reserves RAM through the existing ledger, and creates the operation with `status = vrf_pending`, `generator_version = 2`, the seed, and the roll PDA. It returns `{ operation_id, status: "pending" }`.
2. The function signs and sends `request_roll` from the result authority. Request failures stay on the operation for reconcile; the RAM reservation stays put.
3. A reconcile job runs on the same cron pattern as `ram_shop_reconcile`. It finds fulfilled roll PDAs for pending operations and derives the Known (next section). In one Postgres transaction it debits RAM and writes the Known, then it sends `consume_roll(result_hash)`.
4. The client polls a status endpoint (new `ownership_roll_status`, or the existing function called again with the same operation ID). The Candy Machine reveal plays while it waits.

### Stamp

`ownership_prepare_stamp` follows the same steps. The only change in the derivation is that `deriveStampResult` takes the VRF bytes instead of the HMAC digest. Settling then runs `prepare_profile_stamp_ticket` exactly as today. Already prepared stamps keep their stored v1 results.

### Generator versions

- `generator_version = 1`: HMAC entropy, the current behavior; it stays readable for old operations and reconcile.
- `generator_version = 2`: VRF entropy. A result from one version is never re-derived under the other.

## Turning 32 bytes into results

All derivation is pure and has the same signature in TS and C#: `(randomness, kind, inputs) → result`.

**Byte slices.** Each draw reads its own slice:

| Bytes | Known | Stamp |
|---|---|---|
| 0–7 | tier draw | rarity draw |
| 8–15 | move within tier | first modifier |
| 16–23 | — | second modifier |
| 24–31 | — | effect value locks (one u32 per modifier) |

**No modulo bias.** `uniform(n)` uses rejection sampling on a u64. When a draw has to resample, it extends the stream with `sha512(randomness ‖ "ext" ‖ counter)`. That's deterministic, so recomputation still works. Today's `readU32 % length` is replaced in v2.

**Rarity weights.** Stamp keeps the current cut-offs, applied to `uniform(10_000)`.

Known needs a tiered pool, which doesn't exist yet. It needs a decision:

- a versioned `KNOWN_TIER_WEIGHTS` (for example Common/Uncommon/Rare/Epic/Legendary/Mythic out of 10,000);
- one list of eligible moves per tier, generated by `CombatServerTablesExporter` from `VendingConfigSO` the way `KNOWN_POOL` is today;
- an empty tier rolls down to the nearest lower non-empty tier.

All-In and other Meme moves join only when that's decided (`COMBAT_ISSUES_MASTER` availability note).

**Stamp modifiers.** These are exactly today's rules: `eligibleModifierPoolForMove(host)` for the first pick, then `eligibleSecondModifiersForMove(host, first)`, then `lockModifierEffectValues` reading from bytes 24–31.

**Bundles change the byte source** (2026-10-27, see [Multi-roll bundles](#multi-roll-bundles)). Each pull now reads its own bytes, `b_i = sha256("chainbound:roll-pull:v1" ‖ randomness ‖ i)`, using the slices above; bytes 0–7 are drawn on-chain. A single roll is simply pull 0. Bytes 16–31 of `b_i` remain available for Stamp.

**Shared test vectors.** A checked-in JSON file lists bytes, inputs and the expected result. The Deno tests and the Unity EditMode tests both assert against it, so the TS and C# derivations can't drift apart.

## Multi-roll bundles

**Decided 2026-10-27 (Cronus):** the Candy Machine sells Known rolls as bundles of **1, 10 and 20 pulls**. One VRF request settles the whole bundle, so the oracle cost per pull drops tenfold or twentyfold.

| Bundle | RAM (5 per pull, no discount yet) | VRF requests | VRF + fees spent | Per pull |
|---|---:|---:|---:|---:|
| 1 | 5 | 1 | ~0.00052 SOL | ~0.00052 SOL |
| 10 | 50 | 1 | ~0.00052 SOL | ~0.000052 SOL |
| 20 | 100 | 1 | ~0.00052 SOL | ~0.000026 SOL |

### On-chain (in the Phase 1 build; not deployed yet)

- `request_roll(kind, operation_hash, subject_hash, pull_count)`. The program accepts 1–20; the server only offers 1, 10 and 20.
- `pull_count` is part of the caller seed, so the bundle size can't be changed after the request.
- `RollRequest.tier` becomes `pull_count` (same offset), and `tiers: [u8; 20]` is added after `result_hash`. `fulfill_roll` draws `tiers[i]` from `b_i[0..8]` for every pull; unused entries stay 255. `RollFulfilled` carries the full `tiers` array.
- **Pulls are independent:** each has the same published odds, and duplicates are possible. There is no pity or guarantee, and none is implied in the odds disclosure.

### Settlement

- **All or nothing.** One operation, one RAM reservation of `5 × pull_count`, and one `result_hash` over every pull. A cancel or refund covers the whole bundle; there's no partial delivery.
- **Move within tier:** from `b_i[8..16]`, using the per-tier pool for the request's `odds_version` and `pool_hash`.

### Storage capacity

**Rule: a roll never destroys or silently drops a move.**

- **The limit:** the Known inventory caps at 50 (`MAX_KNOWN_MOVES` in the program's `MoveVault`, `CombatConstants.MAX_KNOWN_MOVES` in Unity).
- **The check:** before reserving RAM, the server refuses a bundle when free slots are fewer than `pull_count`. It returns `inventory_full` with `freeSlots` and `required`. The check runs inside the same Postgres transaction as the reservation, so two parallel requests can't both pass it.
- **Gap today:** the profile-owned Known roll (`ownership_roll_known_atomic`, migration `20261015042331`) does **not** check capacity. This must be added with bundles.
- **Rejected alternatives:**
  - auto-burning overflow for RAM (destroys a possible Mythic without consent);
  - an overflow mailbox (more state, and it just defers the same choice). It can be revisited if players ask.

### Candy Machine UI

- **Three buttons:** ×1, ×10, ×20, each showing its RAM price and "needs N free slots".
- **Disabled state:** when RAM or free slots are short, the button is disabled and says why ("Free 7 slots"), with a shortcut to the Known list, where moves can be burned for RAM (`burn_known_move_for_ram`).
- **Odds panel:** per-pull odds from `RollOdds`, plus an optional "chance of at least one Epic or better" line for 10 and 20 pulls. For example, with Ultra and 10 pulls, `1 − 0.98^10` is about 18%.
- **Reveal:**
  - ×1 keeps the single reveal.
  - ×10 and ×20 use a grid: cards flip in pull order with a tap-to-skip, the highest tier gets a final spotlight, and a summary row closes it out.
  - The reveal waits for the whole bundle, so it fits inside the same 2.5 s latency budget.
- **Resume:** a bundle left mid-reveal (app killed or interrupted) replays its summary from the stored operation via the resume coordinator. It never re-rolls.

### Stamps

Stamps stay single (`pull_count = 1`), since a Stamp targets one specific Known.

## Anti-grinding and audit

| Risk | Control |
|---|---|
| Player rerolls by retrying | One PDA per operation; retries return the same operation |
| Server requests several times and keeps the best | Seed and PDA are stored before the request; one PDA per operation hash; audit query: every settled operation has exactly one roll PDA and a matching `result_hash` |
| Server sees bytes and declines to settle | Settlement is automatic in the reconcile job; RAM is debited at settle only from the reservation; any operation left Fulfilled-but-unsettled past a deadline is an alert |
| Forged fulfillment | `#[vrf_callback]` checks the scoped VRF identity signer (same as encounters) |
| Rule tables changed after the request | `generator_version` and `pool_hash` are part of the seed and stored on the PDA; settlement refuses a mismatch |

Public audit tool: given an operation ID, it fetches the PDA, recomputes the result from the bytes and the tables for that version, and compares it with the settled row and `result_hash`.

## Failure handling

| Case | Behavior |
|---|---|
| Request transaction fails | Operation stays `vrf_pending` with no PDA; reconcile retries the request with the same seed (the PDA doesn't exist yet) |
| Oracle doesn't fulfill within T | Stays pending; the client shows "still rolling" and resumes later. T = 10 s, from the efficacy latency sample (confirmed p90 1,994 ms) |
| Oracle never fulfills | `cancel_roll` (result authority, Pending only, at least 150 slots after request) marks the PDA Cancelled, so a late callback fails. Reconcile retries once under a new operation hash (a new PDA, so MagicBlock never needs a re-request), then releases the RAM reservation. Never fall back to HMAC entropy for the same operation. See [MAINNET_VRF_READINESS_PLAN.md §4](../Solana/MAINNET_VRF_READINESS_PLAN.md#4-roll-timeouts-retry-and-refund) |
| Settle fails after fulfill | Reconcile retries; settlement is idempotent on the operation ID |

## Cost (measured in efficacy run 1)

Per roll: the `request_roll` and `consume_roll` fees (result authority), plus `RollRequest` rent (191 bytes with the discriminator, about 1.6M lamports at the measured Devnet rate, refunded by `close_roll`), plus the MagicBlock request charge (500,000 lamports, measured on Devnet). The fulfill fee is the oracle's. The 10-run sample measured request → fulfilled at a median of 1.85 s (p90 1.99 s, 2–3 slots), with finalized about 130 ms behind on Devnet. The oracle is fast; most of the wait the player sees is client polling.

## Work breakdown (after approval)

1. Program: `RollRequest`, the four instructions, unit tests for the lifecycle and seed binding; autofixer; Devnet upgrade (separate approval).
2. Shared derivation v2 in TS and C# with the shared test vectors.
3. Migration: operation columns (`generator_version`, `vrf_seed`, `roll_request_pda`, `vrf_status`), plus status RPCs.
4. Edge Functions: two-step roll and prepare, status endpoint, reconcile cron, audit endpoint.
5. Unity: pending and resume flow in the Candy Machine screen and the Stamp flow.
6. Devnet matrix: normal, timeout, replay, forged callback, mismatched version.

## Decisions needed

1. Known tier weights and tier membership: drafted in [KNOWN_RARITY_WEIGHTS_v1.md](KNOWN_RARITY_WEIGHTS_v1.md), awaiting approval.
2. Whether to keep v1 HMAC as a closed-beta fallback, or switch outright once v2 ships.
3. Oracle-never-fulfills policy: cancel and release, or wait indefinitely. **Proposed** in [MAINNET_VRF_READINESS_PLAN.md §4](../Solana/MAINNET_VRF_READINESS_PLAN.md#4-roll-timeouts-retry-and-refund): `cancel_roll` (Pending only, after 150 slots), one automatic retry under a new PDA, then release the RAM reservation; no HMAC fallback.
4. `close_roll` retention window.
