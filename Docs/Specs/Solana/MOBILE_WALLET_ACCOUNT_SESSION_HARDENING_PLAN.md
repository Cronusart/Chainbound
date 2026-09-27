# Chainbound Mobile Wallet, Account, and Transaction Hardening Plan

**Status:** Client dependency-removal phase implemented; device security validation pending  
**Updated:** 2026-08-01  
**Scope:** Unity Android client, Solana Mobile Wallet Adapter (MWA), Chainbound Anchor program, Supabase account/indexing services

## 1. What the existing document covered

The previous version was a June 2026 implementation checklist centered on:

- a Unity wallet session cache and `IWalletService` seam;
- Supabase wallet challenge and verified-connect calls;
- a `WalletOperationGuard` for sensitive operations;
- a production MWA provider to be added around the vendored Solana Unity SDK;
- rewiring the main menu and preserving Core/DAS verification; and
- compatibility with the legacy `player_connect` flow.

That plan was useful for identifying the account/session seams, but it left the most consequential decisions open. In particular, it still assumed the vendored Solana Unity SDK/Web3Auth path, did not define who constructs or submits transactions, did not bound gameplay write frequency, and did not provide a secure migration model for the existing program accounts.

This revision is the implementation source of truth for the replacement architecture.

## 2. Current findings that drive the update

### Unity and Android

- The project is on Unity `6000.0.24f1`.
- The former `Packages/Solana.Unity-SDK-main` embedded package included Web3Auth code and vendored AndroidX AAR/JAR files. It is now outside Unity's active import graph in `Quarantine/RemovedPackages`.
- Those local Android artifacts collided with Gradle/Maven AndroidX dependencies and caused the duplicate-class build failures seen during Android builds.
- `Web3` eagerly creates a `Web3AuthWallet`; `Web3AuthWallet` accepts an `ed25519PrivKey` and signs locally. This is incompatible with a non-custodial mobile-wallet design.
- `MainMenuWalletServiceSolana` uses the project `IWalletService` seam. `MainMenuWalletServiceInGame`, direct `Web3.*` references, copied wallet prefabs, and legacy scene components have been removed from the active runtime.
- `SolanaServiceBridge` now creates the project-owned wallet and RPC services without `Web3.Instance`.

### Account and backend

- `AccountSessionService` already has the right high-level flow (connect, challenge, sign proof, verified connect), and `WalletOperationGuard` should be retained and expanded.
- The current Supabase helpers accept client-supplied profile/auth identifiers in several paths. A wallet public key and a verified proof must be the authority for account resolution; body identifiers may only be consistency checks.
- `wallet_auth_challenge` must be bound to an authenticated app session (or a deliberately rate-limited anonymous bootstrap), and `wallet_connect_verified` must consume a one-time challenge with domain, chain, wallet, expiry, and nonce binding.
- Core/DAS verification remains server-side. The current placeholder consume path that finalizes from a client signature (including `deferred_no_tx`) is not acceptable and must be replaced by observed, confirmed chain state.

### On-chain program

- The current program ID is `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ`; the selected migration is an in-place upgrade under this ID.
- Legacy account layouts and seeds are not safe to extend indefinitely. Several current instructions allow a player signer to forge wins, losses, RAM, captures, ordinals, chainlings, or levels; `ProgramAuthorityOnly` is currently only a generic signer check.
- Gameplay-heavy state is desired, but per-frame and per-action transactions are not. State must be checkpointed in bounded batches and signed by the player.

## 3. Locked architecture decisions

1. **MWA is the only signing authority on Android.** The Unity client never receives, stores, imports, or derives a private key.
2. **The client constructs typed transactions.** A backend may provide data, quotes, blockhashes, and attestations, but it does not silently substitute opaque transaction bytes.
3. **Unity validates and simulates before asking MWA to sign and send.** MWA submits the approved transaction; Unity then confirms and reconciles it through the selected RPC endpoint.
4. **Use MWA 2.0 `signAndSendTransactions` on Android.** `signTransactions` is deprecated in MWA 2.0. Transaction construction, decoding, policy checks, fee display, and preflight simulation remain under game control; the wallet owns the final sign-and-send boundary.
5. **Legacy transaction format first.** Versioned transactions and address lookup tables are deferred until the validator, bridge, and recovery telemetry are proven on device.
6. **Players pay transaction fees initially.** The UI must show a fee estimate, required accounts/rent, and the exact action before the wallet prompt. Sponsored fees can be added later behind the same policy interface.
7. **Gameplay-heavy on-chain storage is batched.** A post-battle checkpoint may contain wins/losses, XP/level, capture result, RAM delta, progression, and a bounded position update. No per-frame RPC or transaction is allowed.
8. **The existing program ID is upgraded in place.** V3 PDAs, authority checks, caps, pause controls, scoped attestations, and an explicit migration/close path are required before client rollout.
9. **Web3Auth and the vendored Solana Unity SDK are removed from Unity's active import graph.** Recoverable copies remain under `Quarantine/`; PlaySolana input support is retained and unrelated Unity dependencies are not removed as part of this plan.

## 4. Transaction and wallet security model

### 4.1 Wallet session

The Android native bridge wraps the official Solana Mobile Wallet Adapter client library. It exposes only:

- authorize/deauthorize;
- the wallet public key and wallet identity metadata;
- `signAndSendTransactions`; and
- `signMessages` for account proof.

Authorization state is cached only as a resumable session reference. Treat it as a UX optimization, not proof of ownership. On resume, reconnect or reauthorize and refresh the session when the wallet reports an invalid/expired authorization.

Every account proof includes a domain/app identifier, cluster, wallet public key, issued-at time, expiry, random nonce, and purpose. Never sign an arbitrary server string or a transaction hash presented without context.

### 4.2 Client transaction policy

Introduce a single policy/validator used by every write path (`SolanaTransactionPolicy` or equivalent). Before signing it must:

- decode the transaction and require the expected cluster and recent blockhash;
- require the connected wallet as the fee payer and expected signer;
- allow only the program IDs and instruction discriminators for the requested action;
- validate PDA seeds, account ownership, writable/signer flags, and account count;
- enforce action-specific caps (RAM, XP, item count, checkpoint size, and fee/rent ceiling);
- reject unexpected CPIs, executable accounts, duplicate writable accounts, and arbitrary system/token transfers;
- display the action, fee payer, estimated fee/rent, and material state deltas; and
- simulate with the same commitment used for submission, failing closed on simulation errors or unexpected logs.

The policy must be deterministic and unit-testable without Unity scenes. A transaction that was built by a backend is still untrusted input until it passes this policy.

### 4.3 Submission and confirmation

Use direct RPC from the Unity client for simulation and confirmation. On Android, the MWA wallet performs final submission through `signAndSendTransactions`; desktop development adapters may use an explicitly isolated sign-then-send path. Retry only with a fresh blockhash and a freshly rebuilt transaction; never blindly resend an expired payload. Treat a timeout as `unknown`, then reconcile by signature and account state before offering retry.

The backend is an indexer/attestation service, not a custody or signing service. It may return canonical account data, blockhash metadata, fee estimates, and a short-lived attestation, but it must not hold player keys or make a client-supplied signature sufficient for finalization.

## 5. Official MWA bridge and dependency policy

The native Android bridge uses the MCP-verified official Mobile Wallet Adapter dependency as of this plan:

```text
com.solanamobile:mobile-wallet-adapter-clientlib-ktx:2.0.3
```

The Android library also pins the bridge compile boundary to `androidx.activity:activity-ktx:1.8.2` and `kotlinx-coroutines-android:1.7.3`. MWA 2.0 requires a minimum Android API level of 24. Add `com.solanamobile:web3-solana:0.2.5`, `com.solanamobile:rpc-core:0.2.7`, or `io.github.funkatronics:multimult:0.2.3` only if the bridge later needs those primitives; serialized transaction construction remains in Unity, so they are not currently needed.

The official result sender requires `ComponentActivity`. `SignalJamMwaActivity` is therefore a private, non-exported, translucent proxy that exists only for the wallet handoff. The bridge permits one active operation, validates payload sizes and identity URIs before launch, never serializes transaction bytes or authorization tokens into Activity intents, and clears mutable payload buffers when each operation completes.

Do not copy AAR/JAR files into `Assets/Plugins/Android`. Let Gradle resolve Maven artifacts so there is one AndroidX graph. After migration, run an Android dependency report and fail CI on duplicate classes or more than one version of a required AndroidX module.

The bridge protocol must be versioned and small (JSON or a stable binary envelope). It must return structured errors such as user-cancelled, authorization-expired, unsupported-wallet, simulation-failed, blockhash-expired, and rejected-by-policy. Do not expose SDK types to gameplay assemblies.

## 6. Unity architecture changes

### 6.1 Interfaces and ownership

Keep the existing `IWalletService` and `IWalletProvider` seams, but make transaction intent explicit:

- `ConnectAsync` returns wallet identity and cluster;
- `SignMessageAsync` accepts a typed proof purpose and returns the signature plus payload metadata;
- `SignAndSendTransactionAsync` accepts a validated transaction envelope, not an arbitrary opaque byte array from gameplay code;
- a separate RPC client owns get-latest-blockhash, simulation, confirmation, and the isolated desktop-development send path; and
- `WalletOperationGuard` requires a connected wallet, fresh verified proof, and an action-specific capability before sensitive writes.

`SolanaServiceBridge` becomes a narrow Solana composition root. Encounter/return/time services move to their existing non-Solana owners. No service may access `Web3.Instance`.

All Solana code remains in the existing `Chainbound.Combat.Runtime` assembly unless a new assembly is justified. If an assembly reference changes, update `Docs/Architecture/AssemblyModules.md` and `.cursor/rules/assembly-modules.mdc` in the same change.

### 6.2 Required removal targets

After the native bridge and replacement services pass the gates in Section 10, remove:

- `Packages/Solana.Unity-SDK-main` and its `com.solana.unity_sdk` lock entry;
- the SDK `Web3`, `Web3AuthWallet`, and Web3Auth Android plugin sources/artifacts;
- `MainMenuWalletServiceInGame` and all direct `Web3.*` call sites;
- the `Web3` component and serialized Web3Auth configuration from `Assets/Scenes/demo_scenes/lab_demo.unity`; and
- any duplicate AndroidX libraries previously vendored by the SDK.

Retain `PlaySolana.Unity-SDK-main` for PSG1 input until an independent replacement is tested. Retain Unity’s supported Newtonsoft package if required by the new client; remove `Packages/Newtonsoft.Json-master` only after a repository-wide reference scan confirms it is an unused source checkout.

Because the repository currently has no baseline commit, create a reviewed backup/commit and run a secret scan before deleting embedded packages.

## 7. Bounded gameplay state and fee model

Use one canonical `GameplayCheckpoint` action after a battle or other meaningful session boundary. It may include:

- battle result and win/loss counters;
- XP/level/progression delta;
- capture/chainling result and bounded inventory changes;
- RAM delta and a bounded position update; and
- a client sequence number and previous-checkpoint hash.

The program enforces maximum deltas, maximum items, monotonic sequence numbers, and replay protection. Loadout/equipped-move changes use a separate bounded batch. Cosmetic or analytics-only data remains off-chain.

The client coalesces multiple local changes, prompts once, and records the pending checkpoint locally until confirmed. A failed or unknown transaction is reconciled from chain state before retry. Fee/rent estimates and a user setting for “ask before each checkpoint” are required for player-paid fees.

## 8. Anchor V3 program and migration

### 8.1 V3 account namespace

Add a versioned configuration and PDA namespace while preserving the deployed program ID:

```text
config_v3
player_profile_v3/<wallet>
move_vault_v3/<wallet>
chainling_vault_v3/<wallet>
loadout_v3/<wallet>/<chainling_instance>
```

`ProgramConfigV3` stores schema version, cluster/config hash, pause flags, domain authorities, attestation authority, caps, fee policy, and migration status. Authority roles must be explicit PDAs or configured public keys; a generic `Signer` is not an authority check.

### 8.2 Instruction rules

- Replace generic mutation instructions with action-specific instructions.
- Player-controlled choices (for example, loadout selection) require the player signer and bounded validation.
- Earned rewards, captures, VRF outcomes, mint entitlements, and progression results require a short-lived, domain-separated attestation verified on-chain.
- Use checked arithmetic, explicit ownership/discriminator checks, PDA bump validation, and replay-protected nonces.
- Make RAM spending atomic with the purchase/action that consumes it; do not expose a generic spend primitive.
- Add pause controls and per-action caps with a documented emergency authority and audit trail.
- Disable unsafe legacy handlers after migration; do not leave the old forgeable paths callable “for compatibility.”

### 8.3 Migration

1. Freeze writes to the legacy namespace.
2. Verify program authority, upgrade authority, and deployed binary hash from an operator-controlled environment. This is a hard gate; local Solana CLI is not currently available in the workspace.
3. Create `ProgramConfigV3` and enable only V3 instructions.
4. Migrate identity and non-economic progress with deterministic, idempotent instructions.
5. Import economic/inventory state only from trusted provenance or an attestation; never trust a client-provided snapshot.
6. Verify migrated state, then close legacy accounts atomically to return rent where safe.
7. Keep a read-only legacy decoder and rollback pause procedure until post-release reconciliation is complete.

When Rust changes are implemented, run `program_autofixer` before returning code, apply every suggested fix, and repeat until `require_another_tool_call_after_fixing` is `false`. Then run Anchor tests and the security checklist before deployment.

## 9. Supabase and indexing hardening

- Bind challenge creation to the authenticated app session where possible; rate-limit anonymous bootstrap and never treat a challenge as authentication.
- Derive the profile from the verified wallet/JWT. Body `player_profile_id` and `auth_user_id` are consistency checks only and must be rejected on mismatch.
- Enforce one-time challenge consumption, strict expiry, domain/cluster/purpose binding, and replay protection.
- Remove or gate editor mock-wallet bypasses outside local development; production functions must fail closed if the bypass flag is present.
- Replace `prepareCoreItemConsume`’s empty transaction and `deferred_no_tx` path with a canonical intent containing expected accounts/instructions. `finalize` must verify the observed transaction and resulting on-chain state through RPC before mutating the cache.
- Keep Core/DAS owner, collection, discriminator, and data-length checks server-side. Use Solana Kit/Codama-generated clients for new RPC/IDL boundaries; isolate legacy `@solana/web3.js` validation behind a compatibility adapter until migrated.
- Store only the minimum indexable data, redact signatures/PII from logs, and make idempotency keys and audit events explicit.

## 10. Implementation order and acceptance gates

### Phase A — baseline and inventory

- Create a repository baseline commit/backup and secret scan.
- Inventory every `Web3`, `Web3Auth`, `Solana.Unity`, and Android native dependency reference.
- Add dependency and scene checks that prevent new references to the removed SDK.

**Gate:** no unknown private-key or Web3Auth path remains in the planned runtime graph.

### Phase B — native bridge and client policy

- Add the small official MWA bridge and versioned protocol.
- Implement wallet session refresh, proof purposes, transaction decoding/policy, simulation, MWA sign-and-send, direct RPC confirmation, and recovery states.
- Add device tests for authorization expiry, cancellation, malformed transactions, stale blockhashes, unexpected CPIs, and fee display.

**Gate:** a real MWA wallet can connect, sign a message, sign and send a policy-approved legacy transaction, and recover from each expected failure without a private key in the app.

### Phase C — backend/auth and V3 program

- Harden Supabase challenge/connect and intent/finalize flows.
- Implement and test `ProgramConfigV3`, V3 PDAs, scoped attestations, caps, pause controls, atomic actions, and migration instructions.
- Run Anchor tests, property/fuzz tests for caps/replay/authority, and devnet shadow migration.

**Gate:** authority and upgrade checks are independently verified; unsafe legacy handlers are disabled; migrated state reconciles with the source snapshot.

### Phase D — gameplay integration and package removal

- Replace each direct SDK consumer with typed action builders and the shared wallet/RPC/policy services.
- Integrate post-battle checkpoints and bounded loadout batches with player-paid fee UX.
- Remove Web3Auth/Solana Unity SDK sources, scene components, lock entries, and duplicate Android artifacts.

**Gate:** clean Android build, no duplicate classes, no forbidden assembly/reference scan hits, and no scene has a missing provider/component.

### Phase E — staged devnet release

- Deploy the in-place upgrade only after authority/hash verification.
- Roll out to internal wallets, then a small devnet cohort with telemetry for simulation failures, rejected policies, confirmation latency, retries, and reconciliation mismatches.
- Keep the pause switch and read-only legacy decoder available through the observation window.

## 11. Required tests and security checks

- C# unit tests for transaction policy, PDA derivation, instruction allow-lists, caps, and checkpoint coalescing.
- Android instrumentation tests for MWA authorization, signing, cancellation, wallet switching, and bridge protocol compatibility.
- RPC integration tests for simulation, blockhash expiry, dropped transactions, duplicate submissions, and confirmation reconciliation.
- Supabase tests for challenge replay, JWT/profile mismatch, expired attestations, mock bypass rejection, and idempotent finalization.
- Anchor tests for unauthorized authority attempts, overflow, duplicate/non-monotonic sequence numbers, stale attestations, paused actions, account substitution, and legacy instruction rejection.
- Dependency scans for private-key symbols, Web3Auth classes, duplicate AndroidX classes, vulnerable transitive packages, and accidental secrets.

No production rollout is complete until all gates pass and the deployed program authority/configuration is documented.

## 12. Rollback and incident response

The client can disable checkpoint submission and fall back to read-only state without uninstalling the app. The program pause authority can stop V3 writes while preserving reads. Backend finalization is idempotent and can be paused independently. Any incident runbook must record the affected cluster, program/config hash, transaction signatures, attestation IDs, and reconciliation result; never ask a player to export a private key.

## 13. Documentation and source-of-truth rule

This plan was updated using the Solana Developer MCP workflow: call `list_sections` first for non-trivial Solana questions, then use canonical `get_documentation` sources (including `solana-mobile-docs`) and targeted Solana search/expert tools for narrow API or error questions. If this plan conflicts with model memory, current MCP documentation and the deployed program/IDL win. Any future Solana program Rust change must follow the `program_autofixer` loop described above.
