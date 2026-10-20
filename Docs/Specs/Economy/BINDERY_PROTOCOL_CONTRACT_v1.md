# Bindery Protocol Contract v1

**Status:** Phase 0 frozen and locally hardened; approved for non-player Devnet foundations  
**Decision date:** 2026-10-15  
**Player release:** Not approved; every Bindery player gate remains closed  
**Canonical roadmap:** [`BINDERY_FOUNDATION_AND_DEVNET_ROADMAP_v1.md`](BINDERY_FOUNDATION_AND_DEVNET_ROADMAP_v1.md)

This document is the signed-off Phase 0 contract table. It resolves the vocabulary and authority
questions that must not be left to individual Unity screens, Edge Functions, or transactions. The
Ownership Spine lock remains authoritative. This contract narrows that lock into one Bind flow; it
does not authorize a deployment, source enablement, asset mint, burn, or player transaction.

## 1. Frozen recipe and vocabulary

```text
authoritative unbound Chainling dossier
  + verified Core book cover
  + verified Core Stamped move OR verified Core cosmetic
  + 250 Postgres-canonical RAM held for the operation
  + owner wallet signature
    -> one atomic bind_chainling transaction
    -> one verified Chainling Archive Core asset and BindReceipt
    -> one idempotent off-chain settlement/projection
```

| Concept | Canonical wire/metadata value | Player term | Rejected aliases |
|---|---|---|---|
| Archive asset kind | `cb.kind=chainling_archive` | Bound Chainling / archive book | `bound_chainling` |
| Stamped component | instruction enum `0`; `cb.bind_component_kind=stamped_move` | Stamped Move | Bound move, Rune |
| Cosmetic component | instruction enum `1`; `cb.bind_component_kind=cosmetic` | Cosmetic Skin | `cosmetic_skin` as a metadata value |
| Cover | `cb.kind=book_cover` | Book Cover | folio charge |
| Bind result | Chainling Archive in the configured archives collection | Bound | Bound as a move tier |
| Resource cost | `250 RAM` | RAM | Press Charge |

Display copy may call a cosmetic a "Cosmetic Skin". The stable machine value is `cosmetic`.
`Bound` is exclusively the state of a published Chainling archive. A Stamped move is the final move
tier even when it is consumed by Bind.

## 2. Signed-off authority table

| Fact / transition | Canonical authority | Bind rule | Failure posture |
|---|---|---|---|
| Wallet ownership of the profile | Supabase wallet-proof/binding records | The authenticated profile, active proven wallet, transaction owner, and Core owners must agree. | Reject before preparation and again on chain where applicable. |
| Wild capture outcome | `Chainbound.BattleService` plus its service-only capture RPCs | Species, base IVs/seed, entangled state/type, base visual variant, mutations, and initial Known grants come only from the authoritative battle target and finalized capture attempt. Unity prediction is presentation only. | Client-authored or unproven captures are never Bind-eligible. |
| Unbound Chainling dossier | Postgres `chainlings` row created by the authoritative capture/starter path | Postgres is the mutable pre-Bind source of truth. Every Bind source must retain provenance to the authority result that created or approved the row. | Rows without acceptable provenance remain usable only in their existing non-Bind scope. |
| On-chain `ChainlingAccount` | Result-authority-attested projection of the canonical dossier | It is a sealed Bind input, created by the `GameConfig.result_authority`; it is not a second independently editable profile. Snapshot fields must exactly match the source hash/version. | No owner/client-created or drifted account may enter a Bind source. |
| RAM balance | Postgres `player_profiles.ram_balance` and its ledger | Postgres is canonical. Prepare holds exactly 250 RAM once. The current v1 on-chain profile/reservation is an execution guard/mirror until a reviewed result-authority spend-receipt instruction replaces it. Confirm/reconcile settles or releases the Postgres hold exactly once. | Any ledger/mirror mismatch blocks preparation and enters reconciliation; the client never chooses a balance or cost. |
| Known / Stamp relationship | Profile-owned Known inventory; result-authority Stamp coordination; verified Core Stamped asset after mint | Bind never reads a client claim that a move is Stamped. It accepts the Core asset only after owner, collection, authority, and `cb.kind=stamped_move` verification. Settlement seals the consumed exported move item. | Missing or ambiguous profile/Core linkage blocks Bind. No new move tier is created. |
| Cover and cosmetic ownership | Metaplex Core accounts in the configured authority-controlled collections | Program checks exact owner, collection, Core program, and distinct input accounts. Server inventory applies the same policy before signing. | Free-form metadata, creator strings, or Unity inventory are insufficient. |
| Bind source eligibility | Service-only `ownership_bind_sources` record | A source is immutable for its operation inputs, disabled by default, expiring, auditable, and single-use. Its dossier provenance and snapshot hash are mandatory before it can be enabled. | No generic player insert/update policy; revoked, expired, consumed, or unproven sources fail closed. |
| Atomic Bind result | Existing `chainbound_game` program and deterministic `BindReceipt` | RAM guard/debit, both Core burns, Chainling bound state, archive create, and receipt succeed atomically. | Any CPI or validation failure rolls back the transaction. |
| Archive ownership and metadata | Finalized Core archive account plus `BindReceipt` | The archive must be in the configured archives collection and use the vocabulary in section 1. | A finalized but nonconforming asset is quarantined from the read model and investigated. |
| Profile/party/archive projection | Idempotent Supabase confirm/reconcile settlement | Receipt verification is the only writer of `archive_asset_id`, `bound_at`, source consumption, and RAM settlement. | Timeouts remain pending; retries discover the receipt and never double-settle. |

This selects the roadmap's **hybrid with explicit reconciliation** model. It is not an equal-authority
dual write: Postgres/BattleService owns mutable profile facts, the result authority attests the exact
on-chain execution inputs, and Solana/Core owns the irreversible Bind result.

## 3. Capture and snapshot policy

The capture authority issue is resolved by policy as follows:

1. New wild captures are Bind-eligible only when created by
   `authoritative_finalize_capture_attempt_service` from a BattleService-owned target snapshot.
2. Unity never supplies authoritative IVs, entanglement, mutations, stars, progression, species, or
   capture-rate inputs. Prediction and display DTOs cannot be promoted to Bind source data.
3. Base IVs stay distinct from rebirth allocations and other progression. The archive copies the
   canonical base IV vector/seed and any separately named progression fields; it never folds bonuses
   back into base IVs.
4. The Bind source records a versioned canonical serialization hash of every archived dossier field.
   The result-authority projection must match that hash before the source can be enabled.
5. Historical rows created through retired client-controlled capture paths are ineligible by default.
   They require an explicit, reviewed migration or server re-attestation with an audit record. There
   is no blanket grandfathering and no operator toggle that treats missing provenance as trusted.
6. Starter Chainlings require equivalent server-authored provenance before Bind; being a starter is
   not itself proof of immutable snapshot fields.

The policy closes the design ambiguity, but implementation and adversarial tests of the provenance
bridge remain Phase 1/2 work. Until those tests pass, no Bind source may be enabled.

## 4. Profile-native Stamp compatibility

- Known inventory and available RAM remain profile-native and wallet-optional until Stamp.
- Stamp creates the final tradable move tier as a verified Core asset. Its result-authority ticket,
  idempotency key, and profile settlement are the bridge between the profile and Core planes.
- Bind consumes the verified Core Stamped asset, not the old Known row and not a Unity DTO.
- A successful Bind writes `bind_operation_id` / `bind_sealed_at` to the linked exported move item so
  it cannot remain equipable or tradable in the profile projection after its Core account is burned.
- Cosmetic components follow the same Core owner/collection/authority verification but do not use a
  move-tier transition.

## 5. Legacy quarantine

Historical names and paths may remain only for migration reads, audit evidence, or explicit cleanup:

- `press_charge` is retired and unobtainable; it is never a Bind input, reward, requirement, or UI
  counter.
- "Bound move" means a retired design, never a current state. The database helper name
  `seal_bound_move_item` describes sealing a consumed Stamped asset and does not create a Bound tier.
- Bubblegum/cNFT, Inscription, Rune, move-press, and folio-stamp paths are not selectable by new
  Bindery work.
- The canonical Edge deployment root is `Supabase/functions`. The divergent
  `Supabase/supabase/functions` endpoint tree is excluded from Bindery deployment inputs.

## 6. Baseline, candidate, and deployment deltas

The immutable live snapshot remains
[`manifests/bindery-devnet-baseline-v1.json`](manifests/bindery-devnet-baseline-v1.json). The hardened
local candidate and remote Supabase evidence are recorded in
[`manifests/bindery-devnet-baseline-v2.json`](manifests/bindery-devnet-baseline-v2.json). Generate or
verify v2 with `backend/chainbound_game/scripts/bindery-phase0-baseline.cjs`.

Phase 0 hardening resolves local contract/configuration drift and converts the remaining differences
into explicit deployment gates:

1. The live Devnet program remains the immutable v1 baseline and emits
   `cb.kind=bound_chainling` / `cb.bind_component_kind=cosmetic_skin`. The local v2 candidate now
   emits the canonical `chainling_archive` / `cosmetic` values and has passing Rust metadata tests.
   It is 899,376 bytes, 64 bytes larger than the current 899,312-byte ProgramData capacity. A
   separately approved same-program upgrade must therefore include a deliberate capacity extension.
2. The checked-in Unity Devnet configuration now carries and validates all four collection IDs
   (Moves, Archives, Covers, Cosmetics) against the read-only on-chain `OwnershipConfig` evidence.
3. The target Supabase project has exact local/remote migration-version parity through
   `20261015042331`. All three Bind functions are active at version 6 with pinned deployment hashes.
   Six of eight normalized downloaded source inputs match the local closure; local
   `modifier_pool.generated.ts` and `ownership_spine.ts` are newer and require a reviewed
   deployment/rebase decision before treating the local Edge bundle as deployed.
4. The candidate SBF build succeeds on the pinned WSL toolchain but reports the existing mpl-core
   estimated 4,224-byte frame warning (128 bytes over the 4,096-byte warning threshold). This must be
   reviewed and the archive `CreateV2` path exercised on Devnet before upgrade approval.
5. The captured source tree has relevant dirty inputs. Their byte hashes are exact, but this is not a
   clean release tag or player-release provenance record.

These deltas do not prevent the contract from being frozen. They do prevent Phase 2 durable asset
creation and every player-facing release gate from opening.

## 7. Sign-off and change control

| Area | Decision state | Basis |
|---|---|---|
| Ownership/economy terminology | Locked | Ownership Spine 2026-09-15 plus section 1 |
| State authority and Stamp/Bind bridge | Locked for foundation implementation | Section 2 |
| Capture/IV/entanglement authority | Locked fail-closed policy | Section 3 |
| Devnet live baseline and hardened candidate | Recorded and mechanically verifiable | v1/v2 checked-in manifests and verifier |
| Program/Supabase deployment | Not authorized | Requires later phase evidence and explicit approval |
| Player Bindery | Not authorized | Feature allowlist remains `Gate` |

Any change to the recipe, authority ownership, metadata values, or legacy quarantine requires a new
contract version and regenerated baseline. Updating an address or artifact hash requires a new
baseline ID; it must never overwrite the evidence for this snapshot.
