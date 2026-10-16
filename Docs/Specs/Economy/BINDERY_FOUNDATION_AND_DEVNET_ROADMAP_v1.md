# Bindery Foundation and Devnet Roadmap v1

**Status:** Phase 1 local foundation complete / deployment and live-proof gates closed / no player enablement  
**Date:** 2026-10-16  
**Scope:** Local hardening plus read-only Devnet/Supabase assessment. This document authorizes no
program upgrade, remote migration, Edge Function deployment, feature-flag change, asset mint, or
user transaction.

## 1. Executive decision

The Bindery belongs in the existing `chainbound_game` program. Its canonical operation,
`bind_chainling`, is already present in that program and its current Bind-capable artifact is
already live on Devnet.

| Question | Decision |
|---|---|
| Does Bindery need a new Devnet program? | **No.** Use the existing Devnet `chainbound_game` program: `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ`. |
| Does a future Bindery contract fix need a deployment? | **Only if Rust/IDL behavior changes.** Upgrade the same program ID after the normal artifact/provenance/size gates. |
| Does Mainnet reuse the Devnet program account? | **No.** A first Mainnet release deploys the same reviewed program architecture under a separate Mainnet program ID because Solana clusters do not share accounts. It is not a Bindery-specific second program. |
| Is Bindery ready for player use? | **No.** The instruction and significant coordination scaffolding exist, but trusted source provisioning, complete on/off-chain state alignment, end-to-end proof, player orchestration, and archive projection are incomplete or unverified. |

The useful next move is therefore to harden and prove a **single Bindery vertical slice on the
existing Devnet program**, while keeping all player-facing Bind gates closed. Creating a separate
program now would add cross-program atomicity, migration, and audit costs without solving a known
problem.

## 2. Canonical Bindery contract

The authoritative ownership model is defined by
[`OWNERSHIP_SPINE_v1.md`](OWNERSHIP_SPINE_v1.md),
[`BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md`](BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md), and
[`CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`](../Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md).

```text
Unbound Chainling dossier
  + trusted Metaplex Core book cover
  + trusted Stamped move OR trusted cosmetic
  + 250 RAM
  + owner signature
          |
          v
one atomic bind_chainling transaction
  - debits RAM
  - burns the cover and selected component
  - marks the Chainling bound
  - mints a Chainling Archive Metaplex Core asset
  - writes an idempotent BindReceipt
```

Non-negotiable scope boundaries:

- The Bindery publishes **Chainlings into archive books only**.
- `Bound` is a Chainling/archive state, never a move tier.
- A Stamped move is the final move tier; the Bindery does not stamp or upgrade moves.
- No Press Charge is required or consumed.
- No Bubblegum/cNFT Bind path exists.
- A cover and a component are consumed permanently; a completed Bind must be atomic and idempotent.

The historical filename `BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md` is retained for links, but its
current content explicitly rejects the earlier Press Charge and Bound-move concepts.

## 3. Verified Devnet snapshot

The following was established through a read-only Devnet audit on 2026-10-15 and a local artifact
hash comparison. It is stronger evidence than the older Phase 1 planning record, but it is not a
player-launch approval.

| Item | Observed / configured value |
|---|---|
| Devnet program | `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` |
| Program account | Exists, executable, and owned by the upgradeable loader |
| ProgramData last deployment | Slot `511286785`, observed timestamp `2026-10-15T08:20:47Z` |
| Live payload | `899,312` bytes |
| Live SHA-256 | `a79a218bcfd88b5430893de135d0ffea1306f2082e2beebe466e98237852c8fe` |
| Hardened local candidate | 899,376 bytes; SHA-256 `25e88efb33b77e07a74ef51f1226e2fea1fb55b44e3f65e656caad2c600e7e64` |
| Artifact comparison | Candidate intentionally differs from live: it fixes canonical archive metadata. |
| Upgrade headroom | Current capacity is 899,312 bytes; the candidate requires a deliberate 64-byte ProgramData extension before an approved upgrade. |
| Core program | `CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d` |
| Core authority PDA | `FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys` |
| Stamp RAM cost | `75` |
| Bind RAM cost | `250` |

The current `OwnershipConfig` also points to four existing Devnet Core collections:

| Asset family | Devnet Core collection |
|---|---|
| Stamped moves | `AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC` |
| Chainling archives | `DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U` |
| Book covers | `6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs` |
| Cosmetics | `76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV` |

### What this proves—and what it does not

It proves that the original Bind-capable program remains on Devnet and that the hardened candidate
is reproducibly distinct. Read-only hardening also proved exact local/remote migration-version parity
and pinned the deployed Bind Edge revisions. It does **not** prove server-only secret values, source
records, or that the newer local Edge closure has been deployed. The working tree is also dirty, so
the candidate must be tied to a clean commit/tag before any upgrade.

## 4. Implementation inventory

| Layer | Current state | Evidence and implication |
|---|---|---|
| On-chain instruction | **Substantial, live on Devnet** | `bind_chainling` in `backend/chainbound_game/programs/chainbound_game/src/lib.rs` validates RAM reservation, Chainling state, owned Core inputs, configured collections, and Core authority; it burns inputs, mints the archive, marks the Chainling bound, and creates a receipt in one transaction. |
| Generated client contract | **Present** | `ChainboundGameProgramContract.Generated.cs` exposes the `bind_chainling` discriminator and all required account slots. This is a generated contract surface, not a full user flow. |
| Bind coordination schema | **Present with remote version parity verified** | Remote migration history exactly matches local versions through `20261015042331`; `20261006110715_ownership_bind_coordination.sql` supplies the fail-closed coordination schema. |
| Edge coordination | **Deployed revision pinned; local drift measured** | All three Bind functions are active at version 6. Six of eight normalized downloaded inputs match; local modifier-pool and Ownership Spine shared sources are newer and need a reviewed deploy/rebase decision. |
| Bind source inventory | **Phase 1 lifecycle implemented locally; not deployed** | `20261016045210_bindery_phase1_foundation.sql` adds service-only creation, inspection evidence, enable/disable, revocation, expiry, immutable inputs, and audit history. Creation is Devnet-only and disabled by default; the migration inserts no source. |
| Wallet/client UI | **Absent** | No Unity service or screen invokes Prepare → sign → confirm/reconcile for Bind. Existing ownership services cover Known/Stamp paths only. |
| Roster/archive projection | **Implemented locally; not deployed** | One receipt-backed projection now feeds profile Chainlings, party, and ownership inventory. Unbound rows fail closed to `is_bound=false`; the read functions must not deploy before the migration that creates the projection RPC. |
| Book presentation | **Specified, not built** | The Bound encyclopedia/book presentation has a UI spec, but no actual book asset or Bindery reveal flow is wired. |
| Player exposure | **Closed by design** | Release/feature policy still keeps Bindery/Core consumption unavailable to players. This is the correct current state. |

### On-chain behavior in detail

`bind_chainling` accepts the owner, player profile, RAM reservation, Chainling account,
`OwnershipConfig`, Core authority, cover, component, all four configured collections, a
deterministic archive asset address, and a deterministic receipt address. It:

1. verifies an owner-authorized Bind reservation at the configured 250 RAM cost;
2. requires enough profile RAM and an unbound Chainling owned by the caller;
3. permits component kind `Stamped move` or `cosmetic` only;
4. verifies that the selected Core assets are owned by the caller and attached to the expected,
   authority-controlled collections;
5. rejects duplicate cover/component assets and invalid collection or ownership data;
6. deducts RAM, marks the Chainling bound, and stores the archive asset address;
7. burns the cover and component through Metaplex Core;
8. creates the archive Core asset, writes an idempotent `BindReceipt`, and emits a bound event.

Solana transaction semantics make the state changes and Core CPIs atomic: an error during a burn or
archive creation rolls the whole transaction back rather than leaving a partial RAM debit or
consumed item.

## 5. Bindery data flow and current breakpoints

The intended durable flow is:

```text
verified source + player selection
        |
        v
Edge preparation: auth, source policy, RAM hold, account derivation, simulation
        |
        v
wallet signs one bind_chainling transaction on Devnet
        |
        v
program atomically burns inputs + mints archive + writes receipt
        |
        v
Edge confirm/reconcile verifies receipt, settles RAM/source, writes binding projection
        |
        v
Unity refreshes verified inventory and Chainling/party archive state
```

| Stage | Current assessment | Required foundation |
|---|---|---|
| Input discovery | Missing trusted player inventory for covers, cosmetics, and archives | Add server-verified inventory/catalog paths that enforce collection, Core authority, `cb.kind`, and source eligibility. |
| Source authorization | Schema is fail-closed but empty | Define an operator-only source lifecycle: create, enable, expire, revoke, consume, and audit. |
| Transaction preparation | Present in the canonical top-level Edge source | Verify the exact target Supabase project deployment, service secrets, RPC/DAS settings, and cluster/genesis allowlist. |
| Wallet signing | No Unity Bind flow | Add a reusable ownership domain operation; do not write a scene-specific transaction manager. |
| Confirmation/recovery | Local receipt verification/reconciliation exists | Test timeout, blockhash expiration, wallet disconnect, repeated confirmation, and finality delay against Devnet. |
| Read-model projection | UI DTOs anticipate it but read APIs omit it | Persist and return archive asset ID and binding time via a canonical binding projection. |

### Supabase source-tree release risk

`Supabase/functions` is the documented top-level deployment source. A separate
`Supabase/supabase/functions` tree also contains endpoint directories, while its `_shared` directory
is a junction to the top-level shared code. The two `ownership_prepare_bind` endpoint files differ
in content. Before any Bind deployment, select and document one canonical CLI working directory,
then verify the deployed function hashes/revisions. Accidentally deploying the divergent nested
endpoint is an avoidable release-integrity risk.

## 6. Stamping posture and its impact on Bindery

The stamping system has materially stronger primitives than a raw client-side mint: Core ticket and
collection checks, deterministic/receipt-oriented flow, RAM economics, and server-side
prepare/confirm/reconcile paths. The live program artifact also includes the current
profile-native Stamp work.

That hardening is useful, but it is not automatically a Bindery foundation. The existing on-chain
Bind instruction requires an on-chain `PlayerProfile`, RAM reservation, and `ChainlingAccount`.
The profile-native Stamp path can mint a valid Stamped Core input while its state and settlement
live primarily in the profile/Edge plane. It does not by itself establish all on-chain prerequisites
that `bind_chainling` reads.

Before a real Bind smoke, choose and document one of these compatible models:

| Model | Meaning | Bindery consequence |
|---|---|---|
| On-chain profile/Chainling authority | Bind continues to use the existing Anchor profile, reservation, and Chainling accounts as its authoritative inputs. | Build/maintain an explicit bridge from profile-owned data into those accounts before Bind. |
| Profile-native Bind | A future program/service contract takes authoritative profile state in a deliberately designed way. | Requires a reviewed program and protocol change; it is not available merely by wiring the current Stamp flow to the UI. |
| Hybrid with explicit reconciliation | Both planes are used, with an unambiguous canonical owner for every field and audited synchronization. | Only acceptable with an idempotent bridge, drift detection, and recovery rules. |

The preferred decision is not assumed here; the first phase must select it. Until then, a Stamped
asset may be a valid **component**, but that alone is not evidence that the selected Chainling and
RAM state can safely Bind.

There is also a cross-plane release risk: program bytecode, Edge Functions, SQL migrations,
generated Unity C#, and asset/configuration values deploy independently. Exact program-byte equality
is excellent on-chain evidence, but a Bindery release requires a captured compatibility matrix for
all five planes.

## 7. Gaps and release blockers

| Priority | Gap | Why it matters | Closure evidence |
|---|---|---|---|
| Phase 1/2 gate | Chainling capture authority implementation | The fail-closed authority policy is resolved, but provenance bridge implementation and adversarial proof remain. | Server/VRF-authoritative capture rules, migration path, and adversarial tests pass before any source can be enabled. |
| Resolved in Phase 0 | State-authority decision | The hybrid model now assigns one canonical owner to every fact and transition. | [`BINDERY_PROTOCOL_CONTRACT_v1.md`](BINDERY_PROTOCOL_CONTRACT_v1.md) section 2 and v2 manifest policy checks. |
| Phase 1 deployment gate | Source provisioning | The audited operator lifecycle is implemented locally but is not present in the linked project, and no test source has been created. | Review and apply the migration, deploy the canonical Edge bundle, then create one disabled, expiring source through the service RPC. |
| Phase 2 gate | End-to-end proof | Existing coverage is metadata/unit/structural evidence, not a full Bind transaction matrix. | Controlled Devnet tests prove success, all negative cases, atomic rollback, receipt, settlement, and read-model refresh. |
| Deployment gate; local fix complete | Archive metadata vocabulary | Rust source/tests and the candidate emit `chainling_archive` / `cosmetic`; the live v1 artifact still emits retired values. | Explicit same-program upgrade after clean provenance, capacity extension, review, and approval. |
| Deployment gate; remote state pinned | Edge source parity | Deployed version-6 Bind entrypoints are known; two shared inputs are newer locally. | Review the two deltas and deploy the chosen canonical top-level bundle together, then recapture all three hashes. |
| High | Core update-authority regression risk | Stamp changed `CreateV2` to use no explicit update authority after a Core `ConflictingAuthority` issue. Bind still passes the Core authority explicitly when creating the archive. | A Devnet smoke explicitly exercises archive `CreateV2`; reconcile the patterns if it fails. |
| Deployment gate; measured | Program capacity | The 899,376-byte candidate exceeds the current capacity by exactly 64 bytes. | Explicitly extend ProgramData by at least 64 bytes before the separately approved upgrade. |
| Deployment review | SBF stack-frame warning | The pinned build reports an mpl-core helper at an estimated 4,224-byte frame. | Review dependency/compiler options and prove the exercised archive path on Devnet before approving the candidate. |
| Phase 1 deployment gate | Archive projection | The receipt-backed projection is wired locally but is not yet deployed or exercised against a settled Bind. | Deploy migration before the three read functions, then verify profile/party/inventory return the same binding after controlled settlement. |
| Resolved in Phase 0 | Configuration parity | Central Unity configuration and release validation now carry all four on-chain collection IDs. | C# compilation, config asset assertions, and v2 manifest validation pass. |
| Medium | Legacy terminology | Historical catalog/docs still mention Press Charges and Bound-move behavior. | Ensure current UI, active recipe data, and new docs use only the canonical Bind-only model. |
| Deployment gate | Release provenance | The live v1 and local v2 candidate are separately hashed, while relevant source/IDL/client files remain dirty. | Clean release commit/tag, v2 hashes, build log, slot/capacity plan, and operator sign-off are captured. |

## 8. Phased path forward

Every phase below keeps player Bindery gates disabled until its exit criteria are met. This document
also records local implementation evidence. It does not authorize a remote migration, Edge deploy,
asset mint, signing request, transaction, source enablement, or player feature-gate change.

### Phase 0 — Freeze the contract and release baseline

**Goal:** Establish one canonical Bindery protocol before more scaffolding is built.

- Ratify archive metadata names and component vocabulary against the asset taxonomy.
- Choose the authoritative state model for Chainling, RAM, and the relationship between
  profile-native Stamp and Bind.
- Resolve the Chainling capture/IV/entanglement authority issue that would otherwise be frozen into
  archives.
- Record a release manifest: program ID, ProgramData slot, artifact hash/size, IDL hash, generated
  C# hash, config/collection addresses, Edge revision hashes, and migration versions.
- Classify old Press Charge/Bound-move artifacts as historical only; ensure they are not selected by
  any new Bindery work.

**Exit criteria:** One signed-off contract table and a reproducible Devnet baseline. No program
upgrade is expected unless the contract decision changes Rust behavior.

**Implemented 2026-10-15:**

- The signed-off vocabulary, state-authority, capture-provenance, and legacy-quarantine decisions
  are recorded in [`BINDERY_PROTOCOL_CONTRACT_v1.md`](BINDERY_PROTOCOL_CONTRACT_v1.md).
- The immutable live snapshot remains
  [`manifests/bindery-devnet-baseline-v1.json`](manifests/bindery-devnet-baseline-v1.json). The
  hardened local candidate and pinned Supabase evidence are in
  [`manifests/bindery-devnet-baseline-v2.json`](manifests/bindery-devnet-baseline-v2.json); both are
  preserved rather than rewriting history.
- The authority decision is **hybrid with explicit reconciliation**: BattleService/Postgres owns
  mutable capture and RAM truth, result authority attests the on-chain Bind inputs, and Core plus the
  Bind receipt owns the irreversible result.
- Rust now emits and tests the canonical `chainling_archive`, `stamped_move`, and `cosmetic` values.
  The pinned candidate is 899,376 bytes with SHA-256
  `25e88efb33b77e07a74ef51f1226e2fea1fb55b44e3f65e656caad2c600e7e64`; no deployment occurred.
- Unity's central Devnet configuration now contains Moves, Archives, Covers, and Cosmetics collection
  IDs, and complete deployment validation requires every one of them.
- Read-only Supabase verification pinned all three active Bind functions at version 6 and proved
  exact migration-version parity through `20261015042331`. Two newer local shared Edge inputs are
  explicitly recorded instead of being mistaken for deployed code.
- `check`, `verify-live`, and `verify-supabase` provide repeatable local, Solana, and Supabase checks.
  Player Bindery and source defaults remain disabled, and neither verifier can deploy or transact.

```powershell
node backend/chainbound_game/scripts/bindery-phase0-baseline.cjs check
node backend/chainbound_game/scripts/bindery-phase0-baseline.cjs verify-live
node backend/chainbound_game/scripts/bindery-phase0-baseline.cjs verify-supabase
```

### Phase 1 — Complete non-player Devnet foundations

**Goal:** Make the service and data layer capable of preparing one tightly controlled Bind without
exposing it to users.

- Starting from the pinned v2 remote evidence, verify server-only secrets, expected RPC/DAS
  endpoints, and cluster/genesis guards without exposing secret values.
- Review the two local/deployed shared-source differences, then deploy only from the canonical
  `Supabase/functions` root under a separately approved operation and recapture all three hashes.
- Specify an operator-only Bind source registry: eligible owner/profile/Chainling/cover/component,
  metadata URI/hash, expiry, disable/revoke behavior, and audit record.
- Implement or define a server-verified inventory path for Covers, Stamped moves, and Cosmetics.
- Design the archive-binding read projection consumed by profile, party, and inventory reads.

**Exit criteria:** A service account can create one disabled-by-default, expiring test source; a
read-only inspection can prove every selected asset is trusted before a transaction is prepared.

**Implemented locally 2026-10-16 (not deployed):**

- Added `20261016045210_bindery_phase1_foundation.sql`. Its service-role RPCs create Devnet-only,
  maximum-seven-day sources disabled by default; require an active wallet, owned Chainling, approved
  dossier authority, versioned provenance, dossier and archive hashes; and provide explicit
  enable/disable and irreversible revoke operations. Source selection fields are immutable and every
  creation, inspection, state transition, revocation, and consumption is audited. Table triggers
  independently block direct enablement without fresh trust evidence and make revocation/consumption
  irreversible. `anon` and `authenticated` receive no table or RPC access.
- Added the canonical top-level `ownership_inspect_bind_source` Edge function and shared
  `bindery_foundation` verifier. Inspection checks the live Chainling PDA/discriminator/owner/state,
  absence of archive/receipt outputs, DAS identity/owner/collection/update-authority/metadata locks,
  independent raw-account ownership by the Metaplex Core program for both selected assets, and an
  exact SHA-256 match for archive metadata fetched only beneath the configured metadata origin.
- `ownership_prepare_bind` now runs and records that same inspection before operation creation, RAM
  reservation, or transaction construction. An allowlist row alone cannot authorize preparation;
  an untrusted source returns before any economic or on-chain side effect. The database separately
  requires a trusted inspection no more than ten minutes old when enabling a source and inserting a
  Bind operation.
- Added a single receipt-backed archive projection and consumed it from `profile_get_chainlings`,
  `profile_get_party`, and `ownership_get_inventory`. This avoids inventing a second archive-state
  authority and makes all three reads agree on the settled binding.
- Read-only secret-name audit of linked Devnet project `jhzqcbrzkausrrtcavuu` found all required Bind
  configuration names present: cluster, server RPC, DAS, program, result/Core authorities, four Core
  collections, metadata base, schema/generator/pool pins, costs, and ticket TTL. Values were neither
  printed nor changed. Local configuration code derives the expected Devnet genesis hash and rejects
  wrong-cluster/uncredentialed RPC or DAS configuration before inspection.
- `deno check` passes for all changed/new Edge modules. The focused Bind suite passes 10/10 and the
  expanded Ownership Spine suite passes 42/42,
  including forged metadata, missing asset identity, projection fail-closed behavior, shared
  inspection wiring, and migration policy structure. A 17-assertion pgTAP test covers source
  creation, expiry, immutability, audit, inspection gating, enable/disable/revoke, privilege denial,
  and empty pre-settlement projection. The existing coordination migration, new Phase 1 migration,
  and pgTAP suite all execute successfully in the disposable Supabase Postgres image using the
  test-only `bindery_phase1_minimal_prerequisites.sql` baseline; all 17 assertions pass.
- `bindery-phase1-readonly.cjs` reverified that Devnet genesis, live program slot/size/hash, and four
  ownership collections are unchanged. It also proved the three deployed Bind functions remain
  active at version 6 with their pinned bundle hashes, remote migrations remain at
  `20261015042331`, the Phase 1 migration is the only pending local version, and all 18 required
  secret names are present. Both verifier modes are read-only and report zero mutations.

**Still required to satisfy the operational exit criteria:**

- Review and apply the migration, then deploy only the canonical top-level `Supabase/functions`
  bundle in this order: inspection dependency/migration first, then inspection/prepare/read functions.
  Deployment remains a separately approved operation; the duplicate nested function tree is not a
  release source.
- After deployment, create one disabled, expiring test source through the service RPC and run the
  read-only inspection against real disposable Devnet Cover and Stamped/Cosmetic assets. Recapture
  deployed function hashes and migration parity. Do not enable the source or prepare a transaction
  until that evidence is reviewed.
**Local environment note:** A full `supabase start` still stops before migrations because generated
`.temp/storage-version` pins nonexistent image tag `optimize-existing-functions-again`. The focused
SQL fixture now removes this as a Phase 1 migration-test blocker without silently rewriting generated
CLI state or pretending the historical dashboard-created schema is reproducible from migrations.

### Phase 2 — Prove the on-chain contract on Devnet

**Goal:** Convert the current on-chain implementation from source/live-artifact evidence into a
reproducible integration proof.

Use isolated funded wallets and disposable Devnet assets. The matrix must cover both Stamped-move
and Cosmetic component paths:

- happy-path archive creation;
- wrong owner, wrong Core authority, and wrong collection;
- invalid component kind and cover/component reuse;
- low, expired, replayed, or mismatched RAM reservation;
- already-bound Chainling and replayed idempotency key;
- Core burn or archive-creation failure with full atomic rollback;
- correct receipt contents, archive metadata, collection, owner, Chainling state, and RAM debit;
- the current archive `CreateV2` authority behavior;
- retry/reconcile behavior after client timeout, finality delay, and server restart.

**Exit criteria:** Tests are automated where practical, produce a per-case Devnet evidence bundle,
and show no partial consumption or double settlement.

**Deployment rule:** Use the existing program unchanged for the initial proof. If Phase 0/2 exposes
a Rust or metadata fix, upgrade the **same** Devnet program ID only after a clean release manifest,
size/headroom preflight, and explicit approval.

### Phase 3 — Close the service, ledger, and projection loop

**Goal:** Prove that a successful or failed wallet interaction leaves both Solana and the profile
system coherent.

- Validate prepare → simulate → sign → confirm → reconcile against the real Devnet backend.
- Confirm exact idempotency behavior for duplicate requests, repeated signatures, and deferred
  confirmation.
- Settle or release the RAM hold exactly once and consume/release the Bind source exactly once.
- Write a canonical archive-binding projection and return it from profile/party reads.
- Verify that a bind-sealed Stamped move cannot remain in an equipable gameplay loadout.
- Add operational reporting for prepared, submitted, confirmed, released, failed, and reconciled
  operations.

**Exit criteria:** A deliberately interrupted test can be reconciled to one correct terminal state
without operator data repair.

### Phase 4 — Build the Unity vertical slice behind a disabled gate

**Goal:** Integrate a maintainable client surface without introducing a one-off Bindery scene
controller.

- Extend the reusable ownership service/gateway domain with Bind request, review, result, and
  recovery DTOs.
- Present only server-verified Chainlings, Covers, and Components.
- Implement review/simulation, wallet signing, confirmation/reconcile polling, and explicit failure
  states.
- Refresh inventory and Chainling/party data after terminal success.
- Project the established `Anchored` state first; defer premium book presentation until the data
  contract is proven.
- Keep interaction and feature allowlist flags false outside the controlled test audience.

**Exit criteria:** An internal Devnet build can complete, cancel, recover, and render the correct
archive state using the same service contracts as the backend tests.

### Phase 5 — Controlled Devnet rehearsal

**Goal:** Run a narrow, observable operational proof before any broader beta consideration.

- Provision a small allowlisted source set and isolated operator/test wallets.
- Perform one or more reviewed end-to-end Binds only after simulation preflight succeeds.
- Reconcile every operation from the receipt, archive asset, and database records.
- Run failure/retry drills; review source consumption, RAM accounting, and roster projection.
- Re-audit capture authority, metadata, Core collection verification, and post-bind gameplay
  restrictions.

**Exit criteria:** The team has repeatable evidence, a recovery runbook, alerts/queries for stuck
operations, and a formal decision to remain gated or proceed to a limited player beta.

### Phase 6 — Mainnet construction and rehearsal

**Goal:** Recreate the proven architecture in an isolated Mainnet boundary, not copy Devnet state.

- Build from the approved clean release using
  `backend/chainbound_game/scripts/prepare-mainnet-program-build.cjs`.
- Deploy the reviewed program under the separate planned Mainnet program ID
  `7oBf57qGZRZvNNcRjKiLMdxQ2TeMeA9ue8LtiwG5M5Pj` only after fresh operator approval and funded-payer
  preflight.
- Create a separate Mainnet Core authority, four separate collections, `GameConfig`,
  `OwnershipConfig`, server secrets, RPC/DAS configuration, metadata location, source registry, and
  rollout telemetry.
- Never reuse Devnet program accounts, collection IDs, Supabase data, or secrets.
- Re-run the full integration/reconciliation matrix with a tightly limited Mainnet smoke before any
  player enablement.

**Current Mainnet status:** The planned address is not deployed in the existing Mainnet smoke
record, its payer had `0 SOL` at the last recorded check, and the Mainnet build artifact in that
record predates the current 899,312-byte Devnet artifact. It must be rebuilt and re-hashed from the
approved Bind-capable release; it is not a deploy-ready carryover.

## 9. Deployment decision matrix

| Planned activity | New Bindery program? | Existing Devnet program upgrade? | Separate Mainnet deployment? |
|---|---:|---:|---:|
| Document contract, inventory, operator policy, or test plan | No | No | No |
| Provision Bind source records or deploy Edge/SQL/UI changes | No | No, unless the protocol changes | No |
| Run a controlled test against current `bind_chainling` | No | No | No |
| Fix Bind Rust logic, metadata schema, or account contract | No | **Yes, same ID** | Later, from the approved release |
| Establish Mainnet | No | No | **Yes, distinct cluster program ID** |

## 10. Enablement gate

Bindery remains disabled until every item below has evidence attached to the release record:

- [ ] Capture and immutable Chainling fields are server/VRF-authoritative.
- [ ] One authoritative state model connects Chainling, RAM, Stamp, and Bind.
- [ ] Archive metadata and taxonomy vocabulary are consistent.
- [ ] Devnet program, IDL, generated client, Edge bundles, migrations, config, and collections have
  a recorded compatibility manifest.
- [ ] Program upgrade capacity has been checked for the candidate artifact.
- [ ] Source registry and verified inventory are service-controlled and auditable.
- [ ] Full positive/negative/rollback/reconciliation Devnet matrix passes for both Bind component
  types.
- [ ] The profile/party/inventory read model shows the settled archive state correctly.
- [ ] Unity has cancellation, recovery, duplicate-submit, and wallet-disconnect UX.
- [ ] Operational owners have a dashboard/query path and an incident/reconciliation runbook.
- [ ] A security/economy review approves a deliberately limited rollout gate.

## 11. Primary evidence and related specifications

- [`OWNERSHIP_SPINE_v1.md`](OWNERSHIP_SPINE_v1.md)
- [`BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md`](BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md)
- [`OWNERSHIP_SPINE_PHASE1_DEVNET.md`](OWNERSHIP_SPINE_PHASE1_DEVNET.md): historical Phase 1
  record; retains the correct gate posture but is not the current artifact source of truth.
- [`CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`](../Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md)
- [`CASE_BINDING_COVER_ART_DROPS_v1.md`](../Solana/CASE_BINDING_COVER_ART_DROPS_v1.md)
- [`BOUND_ENCYCLOPEDIA_BOOK_PRESENTATION_v1.md`](../UI/BOUND_ENCYCLOPEDIA_BOOK_PRESENTATION_v1.md)
- [`08_Bind_archive.md`](../Security/EconomyAudits/08_Bind_archive.md): needs reconciliation
  because its Edge-function inventory predates the local Bind endpoints.
- [`06_Chainling_IV_and_progression.md`](../Security/EconomyAudits/06_Chainling_IV_and_progression.md)
- [`DEVNET_MAINNET_BUILD_PROFILE_BOUNDARY.md`](../Solana/DEVNET_MAINNET_BUILD_PROFILE_BOUNDARY.md)
- [`MAINNET_SMOKE_DEPLOYMENT_RUNBOOK.md`](../Solana/MAINNET_SMOKE_DEPLOYMENT_RUNBOOK.md)

## 12. Change record

- **2026-10-16:** Phase 1 service/data foundations implemented locally and documented above. Secret
  presence was audited read-only; TypeScript, focused unit/structural tests, migration application,
  and all 17 pgTAP assertions pass locally. Read-only live/Supabase revalidation also passed. No remote migration,
  Edge deployment, source creation, source enablement, mint, signature request, or transaction was
  performed. Operational exit criteria remain open pending reviewed deployment and live inspection.
- **2026-10-15:** Initial planning specification created from a read-only repository and Devnet
  audit. No implementation, deployment, migration, asset, configuration, or feature-gate change was
  made as part of this document.
