# Bindery Phase 2 Devnet Proof Plan v1

**Status:** Groundwork only — not executed, not complete  
**Target program:** `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` on Devnet  
**Supabase project:** `jhzqcbrzkausrrtcavuu`  
**Phase 1 migration parity:** `107/107`, through `20261016045210_bindery_phase1_foundation.sql`

## Non-negotiable gate

The Phase 1 live inspection has **not** been run. The deployed database baseline still contains zero Chainlings, zero Bind sources, zero trusted sources, zero enabled sources, and zero bindings. No Chainling, Cover, Stamped move, or Cosmetic is created by this groundwork.

Every live case in this plan is `BLOCKED_PHASE1_INSPECTION`. Before any Phase 2 live case may start, a later clean test cycle must:

1. provision one disposable, disabled, expiring Bind source using separately approved operations;
2. call `ownership_inspect_bind_source`; and
3. retain evidence that `selection_trusted == true`, `source.enabled == false`, and `prepare_eligible == false`.

That inspection remains queued. This document does not call it. `prepare_eligible` remains `false` in every groundwork artifact. Enabling a source, authorizing Devnet writes, and implementing a transaction runner are separate future decisions.

## Scope and safety boundary

This plan covers both supported `bind_chainling` component paths:

- Stamped move: `component_kind = 0`
- Cosmetic: `component_kind = 1`

The checked-in runner is inventory-only. It reads local JSON, validates the safety gate, lists cases, and exits. It contains no RPC client, signer, deploy command, transaction builder, Supabase client, or environment-secret access. Passing `--execute-live` is required to request live mode, but live mode is intentionally unimplemented and always refuses.

This groundwork does not call `ownership_create_bind_source_service`, `ownership_set_bind_source_enabled_service`, `ownership_prepare_bind`, `ownership_confirm_bind`, or `ownership_reconcile_bind`. It does not mint, sign, submit, deploy, upgrade, alter Solana configuration or collections, expose secrets, enable a player-facing Bind gate, or modify Unity/mobile/APK files.

## Contract path under proof

The eventual successful path is:

1. `ownership_prepare_bind`
   - re-inspects source trust;
   - requires trusted and eligible selection;
   - reserves 250 RAM in Postgres and mirrors the reservation on chain;
   - builds and simulates `bind_chainling`.
2. `bind_chainling`
   - validates the RAM reservation, component kind, owner, Core authority, collections, distinct inputs, Chainling state, and RAM balance;
   - debits 250 on-chain RAM;
   - marks the Chainling bound and creates a completed `BindReceipt`;
   - burns Cover and component through Core;
   - creates the archive through Core `CreateV2` with the wallet owner and program Core-authority PDA as update authority.
3. `ownership_confirm_bind` or `ownership_reconcile_bind`
   - verifies finalized receipt/archive state;
   - advances the operation through `prepared`, `submitted`, `chain_confirmed`, and `settled`, or releases it as `failed`;
   - settles Postgres RAM/source/binding state exactly once.

Solana transaction atomicity is a required assertion: any failed burn or archive creation must roll back the reservation consumption, RAM debit, Chainling mutation, receipt initialization, and all prior Core CPIs in that transaction.

## Evidence vocabulary

Every case captures the common evidence envelope plus its case-specific fields.

**Common identity and baseline**

- case id, component path, expected result, observed result, start/end UTC, operator approval reference;
- cluster genesis hash, program id, executable data hash/slot, relevant IDL hash;
- Supabase project ref, migration version, deployed function versions/hashes;
- game config, ownership config, collection addresses, and pre-case account/DAS snapshots.

**Disposable inputs**

- wallet/profile/Chainling/source identifiers;
- Cover and component addresses and component kind;
- idempotency key hash, operation id, RAM reservation id/PDA, archive PDA, receipt PDA;
- archive name, URI, and metadata hash, without seed phrases, private keys, service-role keys, JWTs, or raw authorization headers.

**Execution and terminal evidence**

- prepare/confirm/reconcile response envelopes with secrets redacted;
- simulation error/log codes, transaction message hash, signature, submitted/finalized slots, confirmation status, `meta.err`, and program logs;
- decoded pre/post `PlayerProfile`, Chainling, RAM reservation, and `BindReceipt` accounts;
- pre/post Core/DAS asset owner, update authority, collection, plugins/attributes, and data hashes;
- bind operation, source, inspection, binding, RAM ledger, and audit rows;
- assertion results for atomic rollback, exactly-once settlement, no duplicate binding, and expected error identity;
- artifact filenames and SHA-256 hashes.

Observed fields remain `null` or empty until a separately authorized run. The schema and empty bundle live at:

- `manifests/bindery-phase2-case-catalog-v1.json`
- `manifests/bindery-phase2-evidence-bundle.schema.json`
- `manifests/bindery-phase2-evidence-bundle.empty.json`

## Phase 2 case matrix

All rows have live status `BLOCKED_PHASE1_INSPECTION`. “Both” means the case is repeated once with `component_kind = 0` and once with `component_kind = 1`, using fresh disposable assets unless the case explicitly proves replay/reuse.

| ID | Path | Existing function / instruction hit | Required disposable inputs | Expected terminal state | Case-specific evidence |
|---|---|---|---|---|---|
| P2-BIND-001A | Stamped move (0) | `ownership_prepare_bind` → `bind_chainling` → `ownership_confirm_bind`/`ownership_reconcile_bind` | Fresh trusted/eligible source, unbound Chainling, wallet-owned Cover and Stamped move in configured collections, exact live RAM reservation, unused idempotency key and archive/receipt PDAs | One finalized archive and completed receipt; both inputs burned; Chainling bound; operation settled once; source consumed; exactly 250 RAM debited in both ledgers | Receipt fields, archive owner/collection/update authority/attributes, burned input state, Chainling archive address, reservation consumption, RAM deltas, operation/source/binding rows |
| P2-BIND-001B | Cosmetic (1) | Same as 001A | Same shape, with wallet-owned Cosmetic in configured Cosmetics collection | Same as 001A, with component kind and metadata identifying Cosmetic | Same as 001A plus exact live component vocabulary; compare observed archive attributes with the protocol’s canonical vocabulary |
| P2-BIND-002 | Both | `ownership_prepare_bind` inspection/preflight and direct `bind_chainling` negative proof | Fresh source plus one input owned by a different disposable wallet; no shared production assets | Rejected with owner mismatch; no receipt/archive/burn/bind/RAM debit; reservation/source recoverable and not settled | Expected/actual owner, `AssetOwnerMismatch` or Edge rejection, pre/post asset hashes, zero state deltas |
| P2-BIND-003 | Both | `ownership_prepare_bind` trust gate plus `bind_chainling` Anchor account constraints and collection-authority validation | Two disposable subcases: supply a non-PDA Core-authority account; use a collection fixture whose update authority is not the program Core-authority PDA | Rejected by the Core-authority seed constraint or as untrusted/`InvalidCollectionAuthority`; no mutation or settlement | Supplied/expected Core-authority account, expected/observed collection authority, constraint/error or inspection reason, full pre/post hashes |
| P2-BIND-004 | Both | `ownership_prepare_bind` trust gate and `bind_chainling` collection membership checks | Disposable wallet-owned asset assigned to the wrong configured collection | Rejected with wrong collection; no mutation or settlement | Expected/observed collections, `WrongCollection` or inspection reason, source/operation outcome, zero state deltas |
| P2-BIND-005 | Invalid kind | Direct `bind_chainling` negative proof | Fresh otherwise-valid fixtures and a transaction argument with `component_kind` outside `{0,1}` | `InvalidComponentKind`; no receipt/archive/burn/bind/RAM debit | Encoded argument, simulation/transaction error, logs, all pre/post account hashes |
| P2-BIND-006 | Both | `ownership_prepare_bind` preflight and `bind_chainling` distinct-input/Core-account checks | Two disposable subcases: one Core asset supplied as both Cover and component; an input already consumed by a prior successful fixture supplied again | Rejected as duplicate or unavailable/reused input; no additional asset consumption, binding, settlement, or RAM debit | Cover/component addresses, prior burn/receipt reference, `DuplicateComponent` or exact account/preflight error, account existence/hash and aggregate counts before/after |
| P2-BIND-007 | Both | `ownership_prepare_bind` and `bind_chainling` profile RAM check | Fresh fixtures with profile RAM below configured bind cost; no unrelated RAM grant | Rejected for low RAM; no reservation settlement, burn, archive, receipt, or binding | Configured cost, profile/Postgres balances, `InsufficientRam` or Edge error, unchanged ledgers |
| P2-BIND-008 | Both | `ownership_prepare_bind` reservation handling and `bind_chainling::consume_ram_reservation` | Fresh fixtures and an exact reservation allowed to expire before simulation/submission | `ReservationExpired`; operation released/failed through recovery; RAM unchanged; source reusable if policy allows | Reservation expiry/current chain time, error, reservation/operation transitions, release audit, zero asset deltas |
| P2-BIND-009 | Both | `bind_chainling::consume_ram_reservation` | Fresh fixtures and a disposable reservation already marked consumed by its successful original operation | `ReservationAlreadyConsumed`; no second bind/debit/burn/archive/receipt | Original and replay signatures, reservation state, error, counts proving one receipt/binding/debit only |
| P2-BIND-010 | Both | `bind_chainling::consume_ram_reservation` | Fresh fixtures with one controlled mismatch per subcase: operation hash, amount, or reservation kind | `IdempotencyMismatch`, `ReservationAmountMismatch`, or `InvalidReservationKind`; no mutation | Expected/observed hashes, amount/kind, exact error/log, unchanged accounts and ledgers |
| P2-BIND-011 | Both | `ownership_prepare_bind` existing-binding checks and direct `bind_chainling` `AlreadyBound` proof | Disposable Chainling already bound by a completed fixture plus fresh unused inputs/reservation | Rejected; original binding/archive intact; new inputs and RAM untouched | Original/new ids, Chainling state, `bind_already_exists` or `AlreadyBound`, binding/receipt/debit counts |
| P2-BIND-012 | Both | `ownership_prepare_bind` idempotency lookup; `ownership_confirm_bind`/`ownership_reconcile_bind` | One existing operation and its identical idempotency key; conflict subcase changes source or wallet | Identical replay returns the same operation/result; conflict returns 409; no second reservation, signature, binding, or RAM debit | Request fingerprint, operation ids, response codes, row/PDA counts, ledger totals, signature equality |
| P2-BIND-013 | Both | `bind_chainling` Core `BurnV1` CPIs | Fresh valid fixtures; a reviewed disposable second input that deterministically fails Core burn after the first burn CPI would execute (for example a locally proven Core lock/freeze fixture) | Transaction fails and atomically rolls back: both assets remain, receipt absent, Chainling unbound, reservation unconsumed, RAM unchanged | CPI order/logs and Core error, both asset snapshots, receipt/Chainling/reservation/profile hashes, ledger and operation recovery |
| P2-BIND-014 | Both | `bind_chainling` Core `CreateV2` CPI | Fresh valid fixtures plus a reviewed disposable condition that reaches `CreateV2` and fails without changing canonical config/collections; fixture method requires separate approval | Transaction fails and atomically restores both would-be burns and every program write; operation eventually released/failed once | Proof validation and both burns were reached, CreateV2 error/logs, all account/DAS pre/post hashes, no receipt/archive/binding/debit |
| P2-BIND-015 | Both | `fetchAndVerifyBindReceipt` and `ownership_archive_binding_projection_service` read path | Reuse each 001 success artifact; no new transaction | Read model agrees with finalized chain state; exactly one binding; receipt/account/PDA fields and all economic deltas match | Full decoded receipt, archive metadata/collection/owner/authorities, Chainling state, source/operation/binding rows, RAM debit reconciliation |
| P2-BIND-016 | Both | `bind_chainling` Core `CreateV2` authority path | Fresh success fixture per path using the currently deployed program and configured Archives collection | Record the actual deployed behavior. Pass only if creation succeeds and archive owner is wallet while update authority is program Core-authority PDA; otherwise preserve exact failure as a contract finding | CreateV2 accounts/logs, Archives collection authority, archive owner/update authority/plugin authorities, live metadata vocabulary, program slot/hash |
| P2-BIND-017 | Both | `ownership_prepare_bind`, then `ownership_confirm_bind`/`ownership_reconcile_bind` via `advanceBindOperation` | Fresh success fixture; controlled client drops response after submission without resubmitting | Eventual single settled result or single released failure; no duplicate transaction, binding, source consumption, or RAM debit | Drop boundary, request/idempotency fingerprint, discovered signature, operation transitions, final receipt and count assertions |
| P2-BIND-018 | Both | `ownership_confirm_bind`/`ownership_reconcile_bind` finality branch in `advanceBindOperation` | Fresh success fixture; controlled observation before finality, then after finality | Initially pending/submitted without premature settlement; later settles once after finalized verified receipt, or releases once after definitive failure/expiry | Commitment/slots over time, response sequence, signature status, receipt availability, ledger/binding counts |
| P2-BIND-019 | Both | `ownership_reconcile_bind` and `advanceBindOperation` after process restart | Fresh prepared/submitted operation; controlled Edge/server restart with durable database state | Reconciliation discovers chain truth and reaches one terminal state; no reliance on process memory and no double settlement | Restart boundary/instance ids, durable row snapshots, signature discovery, transition timestamps, final chain/database reconciliation |

## Failure-fixture rule

Cases 013 and 014 must use disposable, reviewed fixtures that are first proven locally or in a non-writing simulation. They may not alter the canonical Devnet configuration, collections, program, or production-like assets. If a deterministic post-first-burn or `CreateV2` failure cannot be induced safely, the case stays blocked; it is not replaced with a weaker preflight failure and is not marked passed.

## Archive `CreateV2` compatibility checkpoint

The deployed program is the subject of proof. The current source requests the wallet as archive owner and the program Core-authority PDA as both Core update authority and Attributes plugin authority. Phase 2 must capture what the deployed binary actually accepts and produces.

The roadmap/protocol also record a known vocabulary risk: earlier live-v1 artifacts may emit legacy archive values such as `bound_chainling` or `cosmetic_skin`, while current source expects canonical `chainling_archive`, `stamped_move`, and `cosmetic`. Evidence must preserve the observed live values and report a mismatch; it must not rewrite observations to make them pass.

## Execution sequence for the later authorized cycle

1. Complete and preserve the disabled-source Phase 1 inspection checkpoint (`selection_trusted=true`, `prepare_eligible=false`).
2. Review disposable asset provenance, operator authorization, funding cap, program/config/collection hashes, and the two failure-fixture methods.
3. Enable only the approved disposable source through a separately authorized action; capture that transition independently from the Phase 1 checkpoint.
4. Run one case at a time, beginning with both happy paths and the `CreateV2` authority checkpoint.
5. Freeze each evidence bundle before provisioning the next case; use fresh inputs except where replay/reuse is the behavior under test.
6. Reconcile every operation to a single terminal state and prove aggregate asset, source, binding, and RAM invariants.
7. Keep player-facing Bind disabled until all required evidence is reviewed. Phase 2 is complete only after that review; this groundwork is not completion evidence.

## Local non-executing harness

Run from the repository root:

```powershell
node backend/chainbound_game/scripts/bindery-phase2-groundwork.cjs
node backend/chainbound_game/scripts/bindery-phase2-groundwork.cjs --format json
node backend/chainbound_game/scripts/bindery-phase2-groundwork.cjs --assert-safe
```

The first two commands list the catalog. `--assert-safe` validates the case/evidence invariants. All are local file reads only.

`--execute-live` is the reserved future opt-in flag. Today it always exits non-zero before any network or transaction behavior because no live adapter exists and the Phase 1 inspection gate is unsatisfied.
