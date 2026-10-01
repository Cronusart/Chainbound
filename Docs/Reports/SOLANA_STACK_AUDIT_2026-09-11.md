# Chainbound Solana Stack Audit

**Date:** 2026-09-11  
**Scope:** `backend/chainbound_game`, Unity Solana and transaction services, BattleService/Supabase boundaries, Metaplex Core, Solana legacy/v0/v1 transaction readiness, MagicBlock VRF and planned Ephemeral Rollups.  
**Deployment posture:** devnet/localnet only until the P0 items below are resolved and tested. No mainnet or real-value escrow readiness is claimed.

## Executive result

The current stack is useful prototype scaffolding, not a production Solana custody/mint stack.

- The taxonomy is now consistent in the inspected runtime and primary Solana docs: **Ordinals are game PDAs; Inscriptions are Bubblegum cNFTs; Runes and trusted cosmetics are Metaplex Core assets.** The legacy Core Inscription route is fail-closed.
- The Anchor program has 15 implemented/no-op instructions and 14 fail-closed declarations. There is no `mpl-core`, Bubblegum, MagicBlock VRF, escrow, or Ephemeral Rollups dependency/CPI in the program.
- Every project-owned Unity transaction builder emits **legacy** transactions. The C# parser/orchestrator rejects v0 and v1. The desktop bridge can pass a generic versioned payload to web3.js v1, but that is not end-to-end v0/v1 support.
- The largest security gap is authorization: wallet signatures protect each wallet's own PDA, but several economically authoritative mutations (`add_ordinal`, `add_chainling_from_capture`, wins/losses, RAM spend) trust client-supplied outcomes. `ProgramAuthorityOnly` is misleading: it accepts any signer.
- VRF is not live despite production-oriented comments. The program entrypoint always returns `ContractNotImplemented`, there is no `EncounterRequest` account, and the Unity account order/layout has no corresponding Anchor context.
- Ranked escrow remains a Unity in-memory stub and design document. No Core custody exists on-chain.
- Ephemeral Rollups are absent. The correct target is to delegate program-owned hot session/state PDAs only; Core asset ownership, minting, transfer, vault, escrow, and final settlement stay on the base layer.

## Scoped hardening applied in this audit

1. `credit_ram_from_burn(amount)` now fails closed on-chain. Any wallet could previously credit arbitrary RAM to its own profile.
2. `MoveVaultService.BurnOrdinalAsync` no longer submits a second RAM-credit transaction after `burn_ordinal_for_ram`, which already burns and credits atomically. It reloads the profile instead.
3. The Unity compatibility method `CreditRamFromBurnAsync` now fails locally and no longer constructs the unsafe transaction.
4. Core cosmetic DAS validation now requires an explicit `MplCoreAsset` or `CoreAsset` interface and no longer treats `ownership.delegate` as update authority.
5. The manual contract manifest marks standalone RAM credit as disabled.

These changes do not implement Core, VRF, escrow, or ER stubs.

## Architecture map

### Anchor program instructions

| Instruction | Reality | Authority/data notes |
| --- | --- | --- |
| `initialize` | Implemented no-op | No config or program-authority state is created. |
| `create_profile` | Implemented | Wallet-owned PDA: `player_profile + wallet`. |
| `update_wins_losses` | Implemented, unsafe as authoritative ladder input | Any profile owner can choose win/loss. Must be result-authority/session-settlement controlled. |
| `update_position` | Implemented | Wallet-controlled cache/state; suitable for low-value state if intentionally non-authoritative. |
| `spend_ram` | Implemented | Owner can burn its own balance, but economic action and spend are separate transactions, so failures can charge without delivering. |
| `credit_ram_from_burn` | **Disabled in this audit** | Standalone arbitrary credit was unsafe and duplicated atomic burn credit. |
| `create_move_vault` | Implemented | Wallet-owned PDA. |
| `add_ordinal` | Implemented, authority gap | Any owner can mint arbitrary ordinal fields into its vault. Use a trusted grant/capture authority or proof-bearing atomic flow. |
| `burn_ordinal_for_ram` | Implemented | Atomic removal + fixed credit. Still lacks on-chain UTC reset/daily-cap enforcement. |
| `initialize_chainling_vault` | Implemented | Wallet-owned PDA. |
| `add_chainling_from_capture` | Implemented, authority gap | Any owner chooses species/level/IV seed. Must consume an authoritative capture receipt/session result. |
| `remove_chainling` | Implemented | Owner-controlled destructive mutation. |
| `initialize_equipped_registry` | Implemented | Registry is not constrained to an existing owned chainling. |
| `set_equipped_slot` | Implemented, validation gap | Accepts arbitrary tier/source/asset/move/cache; no vault/Core/cNFT ownership or lock proof. Never use as the authority for paid assets. |
| `create_chainling` | Implemented, authority gap | Wallet chooses species, IVs, level, flags. Also duplicates the vault representation without a linking invariant. |
| `request_encounter` | Declared, fail-closed | No VRF CPI, request PDA, authority, callback, replay state, or account context. |
| `mint_inscription_cnft` | Declared, fail-closed | No Bubblegum/state-compression CPI or Merkle-tree accounts. |
| `upgrade_inscription_to_rune` | Declared, fail-closed | No cNFT proof/burn policy or Core create CPI. |
| `reroll_rune_modifier` | Declared, fail-closed | No Core asset/plugin validation or ticket consumption. |
| `create/fulfill/consume_upgrade_ticket` | Declared, fail-closed | No ticket account exists; no VRF authority, nonce, expiry, binding, or one-shot state. |
| `vending_*` (4) | Declared, fail-closed | Unity builders exist but account orders do not match the one-signer stub context. |
| `apply_xp_and_maybe_level_up` | Declared, fail-closed | Unity builder exists; no authoritative outcome proof. |
| `claim_level_reward_ordinal` | Declared, fail-closed | Unity builder exists; no one-shot reward state enforcement. |
| `mint_inscription` | Legacy disabled | Correctly rejects the obsolete Core-as-Inscription model. |

The checked-in `idl/chainbound_game_contract.json` is a manual manifest, not an Anchor IDL. The generated C# file contains only constants. Builders manually duplicate Borsh layouts and account order, creating silent drift risk. Regenerate the Anchor IDL and generate clients (Codama where supported, or a project generator for C#) before enabling another builder.

### Unity services

| Surface | Role | Current boundary |
| --- | --- | --- |
| `SolanaWalletService`, desktop/MWA bridges | Wallet connection and signing | Wallet-owned keys; MWA uses sign-and-send. Desktop page loads web3.js v1 from a CDN and contains an editor test-wallet route that must not ship in production. |
| `SolanaRpcService` | JSON-RPC send, simulate, status, account reads | Base64 send/simulate is good; account APIs discard owner/lamports/executable metadata, so consumers cannot validate account owner. Confirmation returns a boolean and reports confirmed as `Finalized`. |
| `SolanaChainTxOrchestrator` | Validate, simulate, sign/send, confirm, resume | Legacy-only parser; no transaction review DTO containing cluster/program/action/assets/cost; timestamp-based idempotency keys do not provide durable business idempotency. |
| `ProfileService` | Profile builder/cache | Legacy-only; exposes client-authoritative win/loss/position/spend paths. Standalone RAM credit now disabled. |
| Move/Chainling vault and equipped services | PDA CRUD | Legacy-only; handwritten account layouts; local caches refresh only around explicit calls. |
| `MoveMintService`, `RuneMintService`, vending/ticket/level services | Declared upgrade/mint flows | They spend RAM and request VRF in separate transactions before calling program stubs. This can charge users without delivery. Local objects can be created only after success, but the on-chain success is currently impossible. |
| `CoreMoveService` | Legacy Core Inscription compatibility | Public mint/read methods fail closed. A private dead raw builder remains migration-only and should be deleted after downstream references are confirmed absent. |
| `CoreAssetConsumeService` | Supabase prepare → wallet tx → finalize seam | Correct shape, but prepare currently returns no burn transaction until the Core builder ships. |
| `SolanaServiceBridge` | Dependency composition | Uses configured devnet program/RPC/VRF IDs; does not make missing on-chain functionality real. |

### Supabase and BattleService

- `onchain_cosmetics.ts` is the strongest production-shaped ownership boundary: linked wallet, owner, exact Core interface, configured collection, trusted update authority, `cb.*` attributes, enabled catalog row, schema/slot match, short cache TTL. It refreshes on explicit inventory/selection flows rather than per frame.
- The verifier still depends on provider DAS schema stability and should have fixtures for missing interface, false collection grouping, untrusted authorities/delegates, frozen/plugin states, duplicate cosmetic IDs, pagination limits, and malformed attributes.
- Core bag-item consumption is explicitly scaffolded: prepare/finalize exists, but no authoritative Core burn builder/proof is returned yet.
- `Chainbound.BattleService` is authoritative for current battle/capture persistence, but it has no Solana transaction builder, Core escrow, ER routing, or on-chain settlement adapter.
- `StubEscrowWagerService` is local/in-memory. Its older “inscription wager” naming conflicts with the current v1 escrow policy of **Core Runes only** and must be gated out of production composition.

## Metaplex Core conformance checklist

### Standards and separation

- [x] Program ID configured as `CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d`.
- [x] Canonical taxonomy is documented; legacy Core Inscription entrypoint fails closed.
- [x] Cosmetics use server-side DAS verification rather than trusting Unity ownership lists.
- [ ] Runes are not actually minted as Core assets. `RuneMintService` derives a supposed “rune PDA,” while the program has no `mpl-core` dependency or CPI.
- [ ] Inscriptions are not actually minted as Bubblegum cNFTs. The client merely includes the Bubblegum program account against a fail-closed game instruction.
- [ ] Escrow v1 does not exist. It must reject Bubblegum/cNFT Inscriptions and accept only configured Core Rune collections.

### Core create/update/plugin requirements

- [ ] Add and pin a compatible `mpl-core` crate; use official current CPI builders/helpers rather than hand-encoded Core internals.
- [ ] Create the Core Asset account using the correct signer/PDA authority model and current CreateV2/CreateV1 CPI guidance. Do not model Core as mint + metadata + ATA.
- [ ] Require and validate the Rune collection account, collection membership/update-authority form, and configured trusted authority on every create/transfer/deposit.
- [ ] Define plugin policy at creation: royalties, transfer/freeze delegate, and any permanent delegate. Permanent plugins are security policy, not decoration.
- [ ] Before transfer/deposit/burn, deserialize the Core asset and collection under the Core program, verify owner/delegate authority, collection, and lifecycle/plugin permission (`canTransfer`/`canBurn` semantics).
- [ ] Pass the collection account on collection-member transfers.
- [ ] Bound names/URIs/attributes and define canonical `cb.*` schema/version fields on-chain or in immutable referenced metadata.
- [ ] Test frozen assets, transfer delegates, permanent delegates, wrong collection, wrong owner, spoof program owner, missing collection, and duplicate asset accounts.

### Escrow readiness

- [ ] Prefer the dedicated escrow program proposed in `PVP_02` to isolate custody risk.
- [ ] Encode terms, players, allowed Core collection, result authority, expiry, fee cap, asset arrays, and terminal state in PDAs.
- [ ] Deposit verifies Core program owner, depositor ownership/authorized delegate, collection/update policy, and transferability before Core transfer to escrow PDA.
- [ ] Settlement transitions one-shot state before CPIs, uses checked fee math, rejects duplicate mutable assets, transfers before close, and has timeout/refund/forfeit paths.
- [ ] Do not start ranked combat before confirmed `Locked`; do not accept client winner claims.
- [ ] External audit before mainnet or real funds.

Metaplex distinguishes its instruction names such as `CreateV1`, `CreateV2`, and `TransferV1` from the account key `AssetV1`. None of those names denotes the Solana wire transaction version.

## Transaction version matrix

**Critical distinction:** Metaplex Core instruction versions are program APIs. Solana transaction v1 is the outer message/wire format (`0x81`). Core instructions themselves do not change when carried by legacy, v0, or v1 messages.

| Builder/reader path | Legacy | v0 + ALT | tx v1 | CU/resource behavior | Action |
| --- | --- | --- | --- | --- | --- |
| `SolanaPrimitives.Transaction` serialize | Yes | No | No | No ComputeBudget helpers | Replace with version-aware transaction abstraction; keep legacy path for current clusters. |
| `Transaction.Deserialize` + orchestrator validation | Yes | Explicitly rejects | Explicitly rejects | Cannot inspect v1 `transactionConfig` | Parse version prefix; return a review model for all supported versions. |
| Profile builders (create, win/loss, position, spend) | Yes | No | No | Default cluster limits | Generate from IDL; choose envelope separately. |
| Move vault builders (create/add/burn) | Yes | No | No | Default limits | Same. |
| Chainling vault builders (create/add/remove) | Yes | No | No | Default limits | Same. |
| Equipped registry builders (create/set) | Yes | No | No | Default limits | Same. |
| VRF request builder | Builds legacy bytes but program/account contract absent | No | No | Default limits | Do not enable until real VRF Anchor accounts/CPI exist. |
| Upgrade-ticket builders (create/fulfill/consume) | Builds legacy bytes against stubs | No | No | Three separate txs | Replace with real one-shot ticket design; server/VRF authority must fulfill. |
| Move/cNFT builders + vending cNFT builders | Builds legacy bytes against stubs | No | No | Separate pre-spend/VRF/mint | Atomicize or use durable receipt/refund state before enablement. |
| Rune create/reroll + vending Rune builders | Builds legacy bytes against stubs | No | No | No Core helper or collection/plugin handling | Build official Core CPI on-chain first; client wraps resulting game ix. |
| Level XP/reward builders | Builds legacy bytes against stubs | No | No | Separate state changes | Require authoritative match result and one-shot claim. |
| `CoreMoveService` legacy builder | Private/dead legacy only | No | No | Raw obsolete account assumptions | Delete after migration audit. Never reactivate for Inscriptions. |
| `SolanaRpcService.send/simulate` | Base64 transport works | Transport could work if bytes supplied | Transport could work when cluster activates | Simulation response only records consumed units | Keep base64; add version metadata and explicit v1 config estimator pipeline. |
| `getSignatureStatuses` confirmation | Version-neutral | Version-neutral | Version-neutral | Does not fetch message | Preserve, but return actual confirmed/finalized status and last-valid-block-height/expiry handling. |
| `getAccountInfo/getMultipleAccounts` | N/A | N/A | N/A | Drops account owner metadata | Return validated account envelopes, not bytes only. |
| Desktop wallet bridge | Signs legacy | Can deserialize generic versioned tx (`0x80`) | web3.js v1 boundary is read/sign dependent, not a builder | No review extraction | Move to a pinned local Wallet Standard/Kit-capable bridge; feature-detect wallet v1 support. |
| Mobile Wallet Adapter bridge | Opaque byte sign-and-send | Wallet-dependent | Wallet-dependent | Client simulates first | Advertise required tx version/capabilities and fail before prompt if unsupported. |
| BattleService/Supabase | No transaction/block readers found | N/A | N/A | N/A | When readers/indexers are added, always set `maxSupportedTransactionVersion: 1`. |

### v1 preparation sequence

1. Introduce `TransactionEnvelopeVersion { Legacy, V0, V1 }` and a parsed review DTO: cluster, fee payer, programs, writable/signers, action, assets/cost, recent-blockhash lifetime, resource configuration.
2. Keep legacy as the default on public clusters until v1 activation and wallet capability are both confirmed. Add v0 only where ALT account compression is actually needed.
3. Add version-aware simulation: simulate a provisional message, estimate CU and loaded-account data, then rebuild.
4. For legacy/v0, express compute limit/price and loaded-account cap through ComputeBudget instructions. For v1, remove those instructions and set explicit message `transactionConfig` fields; v1 requires explicit compute-unit and loaded-account-data-size limits.
5. Do not carry ALT strategy into v1. Inline at most the supported address set and split large Core/game bundles when needed.
6. Use base64 transport for large messages and set `maxSupportedTransactionVersion: 1` on every future `getTransaction`/`getBlock` reader/indexer.
7. Parse v1 `transactionConfig` for fee/CU review; scanning ComputeBudget instructions produces wrong answers for v1.
8. Add fixture tests for `0x00` legacy, `0x80` v0, `0x81` v1, malformed prefixes, large base64 payloads, unsupported wallet capability, and v1 messages containing forbidden ALT/legacy ComputeBudget assumptions.

## MagicBlock plan

### VRF current state and hardening

Current Unity intent is request → poll PDA → feed randomness into a ticket. Current program reality is a fail-closed `request_encounter` with only an arbitrary signer account. Therefore VRF is **not integrated**.

Required design before enabling:

- Add `EncounterRequest`/randomness-receipt PDA keyed by player plus a bounded unique nonce/session identifier; never use arbitrary seed bytes directly as an unbounded PDA seed.
- Store requester, purpose/action, target ticket/session, request slot, expiry, status, VRF request identity, randomness/commitment, and consumed flag.
- Pin the MagicBlock VRF program and validate every CPI/callback program/account owner; callback/fulfillment authority must not be the player.
- Bind fulfillment to the original request and ticket, verify pending state, reject replay/duplicate fulfillment, and mark consumed atomically with the economic result.
- Do not derive rarity/modifiers client-side as authority. Unity may preview, but the program/server must derive and persist the authoritative result from verified randomness.
- Replace timestamp idempotency with persisted business IDs/nonces. Validate request PDA discriminator, owner, wallet/purpose, and exact data length before Unity consumes it.
- Add timeout/cancel/refund rules so separate spend/request/mint transactions cannot strand value.

### Ephemeral Rollup target boundary

| Delegate to ER | Keep on base layer |
| --- | --- |
| A purpose-built `MatchSession` PDA containing turn/phase/hash/sequence state | Metaplex Core Rune and cosmetic Asset/Collection accounts |
| Optional session-scoped player/input PDAs with bounded authority | Bubblegum trees/proofs and cNFT mint/burn ownership transitions |
| High-frequency deterministic combat commands and acknowledgements | Move/Chainling vault ownership, Bind-to-Chain/Core minting |
| Periodic committed state hashes/checkpoints | SOL/Core escrow deposits, custody, refunds, forfeit payouts, final settlement |
| Potentially a minimal delegated profile/session projection, not the canonical full profile | Canonical long-lived profile/economy unless a specific delegated field model is audited |

Integration points when explicitly scheduled:

1. Add compatible `ephemeral_rollups_sdk` and apply `#[ephemeral]` to the Anchor program only as part of a complete integration.
2. Create all session PDAs on base layer. Add explicit `delegate_match_session` (and any paired session input PDA delegates) using the Delegation Program `DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh` and SDK `DelegateConfig`.
3. Route hot gameplay transactions to the ER RPC/Magic Router. The client must know whether an account is delegated and must not mix base-only Core/escrow CPIs into ER gameplay transactions.
4. Add periodic `commit_match_session` checkpoints on ER, with monotonically increasing sequence/state hash and idempotent BattleService reconciliation.
5. End with `commit_and_undelegate`; support the SDK-injected undelegation callback so base ownership returns to the game program.
6. Only after the base account is restored and final state verified should the result authority call base-layer escrow settlement.
7. Test disconnect, duplicate commit, stale sequence, invalid result authority, ER outage, base reconciliation, timeout/forfeit, and retry after a commit whose response was lost.

Do not hand-wire only the CPI account list or macros. Delegation, routing, commit, callback, ownership restoration, and reconciliation must ship together.

## Prioritized fix plan

### P0 — block production/value paths

1. **Establish real authorities and receipts** in `backend/chainbound_game/programs/chainbound_game/src/lib.rs`: replace misleading `ProgramAuthorityOnly`; gate win/loss, capture grants, ordinal grants, XP/rewards, ticket fulfillment, and settlement to stored trusted authorities or verified one-shot receipts. Separate harmless owner preferences from economic state.
2. **Enforce RAM rules on-chain:** add UTC window reset and daily cap to atomic `burn_ordinal_for_ram`; never rely on Unity `PlayerPrefs`/clock/cache for the cap. Make spend + promised action atomic or receipt/refund-based.
3. **Keep all contract stubs unreachable from production UX.** Gate `MoveMintService`, `RuneMintService`, `VendingMachineService`, `UpgradeTicketService`, `LevelUpMoveGrantService`, and VRF composition when the deployed program/IDL reports `declared_fail_closed`.
4. **Do not ship local escrow as custody.** Remove `StubEscrowWagerService` from production composition and rename Inscription wager UI/contracts to Core Rune semantics before implementing v1 escrow.
5. **Implement Core only from a generated IDL and official mpl-core CPI:** exact collection/update policy, owner/delegate, freeze/transferability, plugin policy, program ID, account order, and adversarial tests.
6. **Make account reads verifiable:** change `IRpcService` account reads to include owner, executable, lamports, data, context slot; each parser verifies expected program owner, discriminator, exact/minimum length, embedded owner, seeds, and schema.
7. **Remove editor test-wallet capability from production builds** and vendor/pin browser dependencies instead of loading the signing library dynamically from a public CDN.

### P1 — client correctness and forward compatibility

1. Replace handwritten instruction serializers/account arrays with build-generated IDL/Codama-derived artifacts and account-order parity tests.
2. Introduce version-aware legacy/v0/v1 message parsing, review, wallet capability negotiation, and RPC transport. Preserve legacy current-cluster behavior.
3. Add durable idempotency records keyed by business action; retain pending transactions on timeout because a timed-out transaction may later land.
4. Return `Confirmed` versus `Finalized` accurately. Track blockhash plus `lastValidBlockHeight`, expiration, rebroadcast, and safe rebuild semantics.
5. Add the full pre-sign review contract required by wallet UX: cluster, fee payer, program IDs, decoded action, affected assets, RAM/SOL cost, simulated logs/units, and confirmation state.
6. Add v1 reader defaults (`maxSupportedTransactionVersion: 1`) before any `getTransaction`, `getBlock`, indexing, or BattleService verification path ships.
7. Harden VRF using the request/ticket design above; then implement ER as a separate scoped milestone.

### P2 — quality, observability, and cleanup

1. Delete the dead private Core Inscription builder and old Inscription escrow naming once migration callers are proven absent.
2. Add LiteSVM or Mollusk instruction/security tests as the fast gate; retain Anchor TS account-order/IDL tests; use Surfpool for Core/Bubblegum/MagicBlock integration and realistic cloned state.
3. Add CU and transaction-size budgets for Core/game bundles; test split plans for v1 without ALTs.
4. Add metrics for simulation failure class, wallet rejection, blockhash expiry, confirmation latency, VRF age/replay rejection, ER commit lag, and reconciliation state.
5. Resolve doc drift: `Docs/Guides/SETUP_NOTES.md` exists, but generated/manual-IDL status and every fail-closed feature should be surfaced in a single deployment readiness checklist.

## Verification and test gates

- Rust: `cargo fmt --check`, `cargo test`, `cargo clippy --all-targets --all-features -- -D warnings`; `anchor build` when the matching CLI/platform tools are installed.
- Program security: wrong signer/owner/PDA/bump, duplicate mutable accounts, arbitrary CPI target, overflow, replay, reinit, stale receipt, double settle, wrong collection, frozen Core asset.
- Unity: EditMode fixture tests for legacy/v0/v1 parser/review and malicious RPC/DAS payloads; `dotnet build "Signal Jam 3D.sln" -v minimal`; live Editor compilation with zero errors.
- Supabase: Deno fixtures for exact Core interface, owner, collection, update authority, `cb.*`, catalog/schema, stale cache, pagination, malformed DAS, and delegate non-substitution.
- Integration: Surfpool/base program + real Core/Bubblegum programs; MagicBlock local/dev environment for VRF and eventual delegate/commit/undelegate lifecycle.

## Primary references

- Solana RPC transaction JSON and v1 `transactionConfig`: <https://solana.com/docs/rpc/json-structures>
- Solana Foundation transaction-v1 examples: <https://github.com/solana-foundation/transaction-v1-examples>
- Metaplex Core single-account asset model: <https://www.metaplex.com/docs/smart-contracts/core/what-is-an-asset>
- Metaplex Core collection transfer and freeze/delegate behavior: <https://www.metaplex.com/docs/smart-contracts/core/transfer>
- MagicBlock ER delegate/commit/undelegate quickstart: <https://docs.magicblock.gg/pages/ephemeral-rollups-ers/how-to-guide/quickstart>
- Repository escrow design: `Docs/review-unimplemented/Guides/PVP_02_Ranked_Escrow_And_Ladder.md`
- Foundation security/testing guidance: installed `solana-dev` skill v1.1.0 (`references/security.md`, `references/testing.md`).

