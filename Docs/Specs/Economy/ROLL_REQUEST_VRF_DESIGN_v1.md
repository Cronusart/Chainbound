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

**Shared test vectors.** A checked-in JSON file lists bytes, inputs and the expected result. The Deno tests and the Unity EditMode tests both assert against it, so the TS and C# derivations can't drift apart.

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
| Oracle doesn't fulfill within T | Stays pending; the client shows "still rolling" and resumes later. T is set from the efficacy latency sample |
| Oracle never fulfills | **Open question:** does MagicBlock support re-requesting for an existing PDA? If not, after a long deadline cancel the operation and release the RAM reservation. Never fall back to HMAC entropy for the same operation |
| Settle fails after fulfill | Reconcile retries; settlement is idempotent on the operation ID |

## Cost (to be filled from the efficacy run)

Per roll: the `request_roll` and `consume_roll` fees (result authority), plus `RollRequest` rent (191 bytes with the discriminator, about 0.0022 SOL, refunded by `close_roll`), plus any MagicBlock fee, which isn't documented yet. The fulfill fee is the oracle's.

## Work breakdown (after approval)

1. Program: `RollRequest`, the four instructions, unit tests for the lifecycle and seed binding; autofixer; Devnet upgrade (separate approval).
2. Shared derivation v2 in TS and C# with the shared test vectors.
3. Migration: operation columns (`generator_version`, `vrf_seed`, `roll_request_pda`, `vrf_status`), plus status RPCs.
4. Edge Functions: two-step roll and prepare, status endpoint, reconcile cron, audit endpoint.
5. Unity: pending and resume flow in the Candy Machine screen and the Stamp flow.
6. Devnet matrix: normal, timeout, replay, forged callback, mismatched version.

## Decisions needed

1. Known tier weights and tier membership (design the rarity-weighted Known pool).
2. Whether to keep v1 HMAC as a closed-beta fallback, or switch outright once v2 ships.
3. Oracle-never-fulfills policy: cancel and release, or wait indefinitely.
4. `close_roll` retention window.
