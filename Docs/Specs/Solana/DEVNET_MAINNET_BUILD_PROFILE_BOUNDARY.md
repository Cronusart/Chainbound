# Devnet / Mainnet Build-Profile Boundary

**Status:** Boundary implemented; isolated Mainnet Supabase and Helius secrets provisioned; on-chain Mainnet disabled pending funded deployment  
**Last updated:** 2026-10-01

> **Current operational handoff:** Mainnet RPC/DAS secrets are installed server-side, but the
> designated Mainnet program payer has `0 SOL`. No Mainnet program, collection, or ownership
> state has been created. The authoritative resume checklist is
> [Mainnet Smoke Deployment Runbook](MAINNET_SMOKE_DEPLOYMENT_RUNBOOK.md).

## Purpose

Chainbound selects its ownership environment at build time. Players cannot switch networks at runtime, and an Android build without an explicit environment selection fails closed.

## Profiles

| Profile | Compile define | Backend scope | Dev tools | Current state |
|---|---|---|---|---|
| `DevnetOwnership` | `CHAINBOUND_DEVNET_OWNERSHIP` | `devnet` | Allowed | Complete and used by the development APK command |
| `MainnetSmoke` | `CHAINBOUND_MAINNET_SMOKE` | `mainnet-smoke` | Disabled | Backend isolated; server-side RPC/DAS set; program, authority, and collection intentionally unset |

Profile assets live under `Assets/Resources/DeploymentProfiles/`. Each profile owns references to one Solana config and one Supabase config plus its metadata base URL. Mainnet and Devnet may not share those config assets, Supabase project, game program, or Core Moves collection.

## Safety contract

- The Android build command embeds exactly one deployment define.
- `PlatformContext` resolves the compiled profile and supplies its Solana/Supabase configs to runtime services.
- A player build with neither deployment define disables backend and ownership access.
- The build validator checks cluster, expected genesis hash, RPC/DAS URLs, program, collection, authority, Supabase project, metadata host, MWA setup, and environment isolation.
- The validator calls `getGenesisHash` against the selected RPC before producing an APK.
- Wallet connect and sign-and-send repeat the genesis check at runtime before opening MWA.
- MWA authorization, Supabase refresh credentials, and offline profile snapshots are environment-scoped. Legacy unscoped credentials migrate only into Devnet.
- `DEVELOPMENT_BUILD` no longer enables the Ownership Spine. Mainnet Smoke never exposes `DevGrantStrip`.
- Runtime RPC overrides are Editor-only; Android players use the config embedded by the selected profile.

## Build commands

- `build_development_android` selects `DevnetOwnership`.
- `build_mainnet_smoke_android` selects `MainnetSmoke` and is expected to fail validation until the fields below are provisioned.

## Provisioned Mainnet backend

The following resources are isolated from Devnet and ready for a controlled smoke test:

- Supabase project: `Chainbound Mainnet Smoke` (`vznasgdkcrfwupsayybn`)
- Project URL: `https://vznasgdkcrfwupsayybn.supabase.co`
- Metadata bucket: `chainbound-ownership-metadata`
  - public reads
  - Edge/service-role writes only
  - `application/json` and `image/png`
  - 10 MiB object limit
- 50 current source-backed Edge Functions, all deployed with JWT verification
- Devnet grant/top-up and metadata-repair operator functions are not deployed
- One approved `reviewer` allowlist entry for the Seeker smoke wallet
- `CHAINBOUND_MAINNET_SMOKE_ENABLED=false`
- Mainnet cluster, wallet allowlist, distinct-program, HTTPS RPC, Helius DAS, and genesis-hash guards are enforced server-side
- Dedicated Mainnet Helius RPC and DAS secrets are present server-side; their values are never stored in Unity assets or this repository

No Solana Mainnet transaction has been signed or submitted.

## Mainnet fields still required

Before `MainnetSmoke` can build or any ownership endpoint can mutate state, supply:

1. Deployed Mainnet game program ID.
2. Mainnet Core Moves collection and expected update-authority PDA.
3. Dedicated Mainnet result-authority secret with an external recovery backup.
4. Funded Mainnet payer and an explicitly reviewed deployment budget.
5. Final activation of the server-side kill switch only after all reads and simulations pass.


## Historical Mainnet RPC + DAS provisioning notes (2026-09-30)

> **Superseded as a gate on 2026-10-01:** `HELIUS_DAS_URL` and
> `CHAINBOUND_SOLANA_RPC_ENDPOINT` are now present in the isolated Mainnet
> Supabase project. The endpoint values remain server-only. Program deployment
> funding is now the blocking gate; see the runbook linked at the top.

**Status:** Blocked — dedicated Mainnet credentials are not present. No endpoints were wired. Devnet `SolanaConfig.asset` was not modified. No deploy and no mutating txs.

### Current Unity MainnetSmoke fields (`Assets/Data/Solana/SolanaConfig_MainnetSmoke.asset`)

| Field | Status |
|---|---|
| `ownershipCluster` | Filled: `mainnet-beta` |
| `expectedGenesisHash` | Filled: `5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d` (verified read-only against public `https://api.mainnet-beta.solana.com`) |
| `metaplexCoreProgramId` | Filled: `CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d` |
| `rpcEndpoint` | **Blank** (fail-closed) |
| `wsEndpoint` | **Blank** (fail-closed) |
| `dasRpcEndpoint` | **Blank** (fail-closed) |
| `gameProgramId` | **Blank** (leave until deploy gate; built ID `7oBf57qGZRZvNNcRjKiLMdxQ2TeMeA9ue8LtiwG5M5Pj` is not live yet) |
| `coreMovesCollectionId` / `coreMovesUpdateAuthority` / cosmetics | **Blank** (leave until collection gate) |

Never put a Helius `api-key` into Unity assets. Client DAS inventory uses Edge `ownership_das_proxy`; Unity may later hold keyless HTTPS RPC/DAS hosts only.

### Devnet inventory (provider names only; keys redacted)

| Surface | Provider / shape |
|---|---|
| Unity `SolanaConfig.asset` `rpcEndpoint` / `dasRpcEndpoint` | Public Solana Devnet (`api.devnet.solana.com`) — no API key in client |
| Supabase Devnet (`jhzqcbrzkausrrtcavuu`) secrets | `HELIUS_DAS_URL` present (Helius Devnet host), plus `CHAINBOUND_SOLANA_RPC_ENDPOINT` and legacy `CHAINBOUND_DAS_RPC_URL` |
| Edge DAS validation | Requires `https://devnet.helius-rpc.com/?api-key=<KEY>` when `CHAINBOUND_SOLANA_CLUSTER=devnet` |

Do **not** copy the Devnet Helius key into Mainnet.

### Mainnet Supabase (`vznasgdkcrfwupsayybn`) secret presence (names only)

Present (non-RPC): ownership RAM/TTL/schema/pool/metadata-base, `CHAINBOUND_SOLANA_CLUSTER`, `CHAINBOUND_MAINNET_SMOKE_ENABLED`, `CHAINBOUND_MAINNET_SMOKE_WALLETS`, platform Supabase keys.

**Historical missing state (resolved 2026-10-01):**

1. `HELIUS_DAS_URL`
2. `CHAINBOUND_SOLANA_RPC_ENDPOINT`

Also still missing for later gates (not filled here): `CHAINBOUND_GAME_PROGRAM_ID`, Core collection/authority secrets, `CHAINBOUND_RESULT_AUTHORITY_SECRET`. Legacy `CHAINBOUND_DAS_RPC_URL` is optional fallback in some shared helpers; ownership spine / DAS proxy require `HELIUS_DAS_URL`.

### Historical secret templates (already provisioned)

1. **Provider:** Helius (required by `requireHeliusDasUrl` in `ownership_das_proxy.ts` / `ownership_spine.ts`).
2. **Signup / key:** Create a **separate Mainnet** API key in the Helius dashboard (do not reuse Devnet key material). Suggested dashboard name: `chainbound-mainnet-smoke`.
3. **Secret `HELIUS_DAS_URL` template (server-only):**
   `https://mainnet.helius-rpc.com/?api-key=<MAINNET_API_KEY>`
   Host must be exactly `mainnet.helius-rpc.com`, HTTPS, with non-empty `api-key` query param.
4. **Secret `CHAINBOUND_SOLANA_RPC_ENDPOINT` template (server-only, HTTPS):**
   Prefer the same dedicated Helius Mainnet URL (RPC + DAS share the host), e.g. `https://mainnet.helius-rpc.com/?api-key=<MAINNET_API_KEY>`,
   **or** another dedicated HTTPS Mainnet RPC. Public `https://api.mainnet-beta.solana.com` is acceptable only for temporary read-only genesis checks, not as the dedicated smoke RPC.
5. **Set secrets** (local operator machine; never paste keys into chat/git):
   `supabase secrets set --project-ref vznasgdkcrfwupsayybn HELIUS_DAS_URL="https://mainnet.helius-rpc.com/?api-key=..." CHAINBOUND_SOLANA_RPC_ENDPOINT="https://mainnet.helius-rpc.com/?api-key=..."`
6. **Unity after keyless endpoints exist (optional, still no program ID):**
   - `rpcEndpoint`: dedicated keyless HTTPS Mainnet RPC **or** public Mainnet for bring-up
   - `wsEndpoint`: matching `wss://...`
   - `dasRpcEndpoint`: keyless HTTPS host for any direct client probes; production inventory stays on Edge proxy
7. **Confirm cluster secret** remains `mainnet-beta` and kill switch `CHAINBOUND_MAINNET_SMOKE_ENABLED=false` until read/sim GO.

### Historical next step after this gate

This gate is complete. Do not fill Unity with a keyed Helius URL. Resume from
the program deployment funding gate in
[Mainnet Smoke Deployment Runbook](MAINNET_SMOKE_DEPLOYMENT_RUNBOOK.md).

## Program artifact boundary

Devnet uses program ID `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ`. Mainnet must use a different program ID. Anchor requires the program keypair, `declare_id!`, generated IDL address, and cluster mapping to agree.

`backend/chainbound_game/scripts/prepare-mainnet-program-build.cjs` creates an isolated copy under `.utmp/`, replaces only the copied program identity, and can produce a pinned `cargo build-sbf` artifact plus a hash manifest. It never copies a program keypair and never deploys. The canonical Devnet workspace is hash-checked before and after preparation.

The first Mainnet deployment must additionally verify that the securely held program keypair derives the same public key recorded in the manifest. A second clean build should reproduce the artifact hash before deployment review.

After provisioning, run the Mainnet validator first. The first Mainnet smoke transaction still requires explicit transaction review, simulation, and user approval.
