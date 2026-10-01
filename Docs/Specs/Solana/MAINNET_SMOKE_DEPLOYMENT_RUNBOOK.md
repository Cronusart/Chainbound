# Mainnet Smoke Deployment Runbook

**Status:** Paused at the funded-payer gate.  
**Last verified:** 2026-10-01 (America/Chicago)  
**Scope:** Controlled Chainbound Mainnet smoke only; this is not a production launch runbook.

## Non-negotiable safety boundary

- Do not reuse Devnet program IDs, Supabase project data, Helius credentials, ownership rows, or collections.
- Do not place Helius keys, result-authority material, program keypairs, seed phrases, or private keys in this repository, Unity assets, chat, logs, or screenshots.
- The Windows Solana CLI remains configured for Devnet. A Mainnet command must pass its dedicated HTTPS RPC explicitly; never change the global Devnet CLI configuration as a shortcut.
- Do not use `--skip-preflight`, do not make the program final during smoke, and do not enable `CHAINBOUND_MAINNET_SMOKE_ENABLED` until the controlled read/simulation checks are approved.
- Every Mainnet transaction requires a fresh, explicit operator approval at the time it is sent.

## Verified deployment record

| Item | Verified value / state |
|---|---|
| Mainnet cluster | `mainnet-beta` |
| Expected genesis hash | `5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d` |
| Mainnet program ID | `7oBf57qGZRZvNNcRjKiLMdxQ2TeMeA9ue8LtiwG5M5Pj` |
| Program on Mainnet | Not deployed; public account lookup returned `AccountNotFound` |
| Upgrade authority / deployment payer | `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG` |
| Payer Mainnet balance | `0 SOL` at last check |
| Isolated artifact | `chainbound_game.so`, 741,248 bytes |
| Artifact SHA-256 | `ec25f9521554f0930cd7e20f29058807865ed8ac80d5431bdc55a8d236ac1b90` |
| Pinned build | `cargo build-sbf --tools-version v1.52 --arch v0 -- --locked` |
| Build manifest | `.utmp/mainnet-program-build/2026-10-01T01-16-34-944Z-7oBf57qG/mainnet-build-manifest.json` |
| Devnet program ID (must not use) | `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` |

The Mainnet program keypair is intentionally stored outside the repository. Its public key was verified to derive `7oBf…M5Pj`. The authority keypair is also outside the repository and was verified to derive `DkFm…gJmG`.

## Backend and client state

| Surface | Current state |
|---|---|
| Mainnet Supabase project | `vznasgdkcrfwupsayybn` (`Chainbound Mainnet Smoke`) |
| Security migration history | Local and remote aligned at `20261001005844_mainnet_backend_security_hardening` |
| Security advisor | No findings at last check |
| Helius secrets | `HELIUS_DAS_URL` and `CHAINBOUND_SOLANA_RPC_ENDPOINT` present; values redacted and server-only |
| Smoke kill switch | Secret defined; must remain disabled through read/simulation review |
| Unity `MainnetSmoke` config | Mainnet cluster and genesis filled; keyed RPC/DAS, program, collection, and authorities intentionally blank |
| Devnet | Untouched; its config and ownership environment remain separate |

Still unprovisioned: `CHAINBOUND_GAME_PROGRAM_ID`, Mainnet Core Moves collection and update-authority data, `CHAINBOUND_RESULT_AUTHORITY_SECRET`, and Mainnet metadata publication configuration.

## Current blocker: fund the deployment payer

The compiled artifact's rent estimate is `3.76619008 SOL`. That estimate does not include program-account metadata or deployment transaction fees.

Before deployment, fund the public payer/authority address below with at least **4.25 SOL**. **5 SOL** is the recommended amount to leave a practical buffer for deployment confirmation/retry costs and immediate initialization planning:

```text
DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG
```

This funding transfer is a separate Mainnet transaction owned and approved by the operator. The unused balance remains in the authority wallet.

## Resume checklist after funding

### 1. Read-only preflight

1. Confirm the payer balance is at least `4.25 SOL` against the dedicated Mainnet Helius RPC.
2. Confirm the RPC `getGenesisHash` result is exactly `5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d`.
3. Confirm the program keypair still derives `7oBf57qGZRZvNNcRjKiLMdxQ2TeMeA9ue8LtiwG5M5Pj`.
4. Confirm the authority keypair still derives `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG`.
5. Re-hash the artifact and require an exact match with the SHA-256 recorded above.
6. Confirm the Mainnet program account remains absent. If it exists, stop and inspect it as an untrusted account before considering an upgrade path.
7. Confirm the deployment environment has a Linux/WSL Solana CLI available. Do not deploy from the Windows default configuration, which has no authority keypair and remains pointed to Devnet.

### 2. Deployment review

Before a transaction is sent, present and confirm all of the following:

| Field | Required value |
|---|---|
| Cluster | Mainnet Beta |
| RPC | Dedicated Mainnet Helius HTTPS URL, supplied privately at runtime |
| Fee payer | `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG` |
| Program ID | `7oBf57qGZRZvNNcRjKiLMdxQ2TeMeA9ue8LtiwG5M5Pj` |
| Artifact | Hash-verified 741,248-byte `chainbound_game.so` |
| Upgrade authority | `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG` |
| Loader mode | Upgradeable; no `--final` during smoke |
| Preflight | Enabled; no `--skip-preflight` |

The deployment command must use explicit `--url`, `--fee-payer`, `--keypair`, `--program-id`, and `--upgrade-authority` arguments. The Helius URL must be provided through a private runtime environment variable, never typed into a tracked script.

### 3. Post-deploy verification

After confirmation, perform only read-only checks before any initialization:

1. `solana program show` reports the expected program ID and upgrade authority.
2. The program account is executable and owned by the upgradeable loader.
3. The deployed data length accommodates the verified artifact.
4. A second read after the next slot still reports the expected program.
5. Record the deployment signature, slot, deployer, program ID, artifact hash, and observed upgrade authority in the operator handoff.

If any check fails, stop. Preserve and inspect a possible buffer account rather than blindly retrying; the deployment command uploads large binaries through a temporary buffer.

## Gates after program deployment

Program deployment does **not** authorize the following actions. Each needs its own review and explicit approval:

1. Create and configure the Mainnet Core Moves collection and its update authority.
2. Create/back up the dedicated result authority and set its secret.
3. Set `CHAINBOUND_GAME_PROGRAM_ID` and collection/authority values in the isolated Mainnet backend, then redeploy the affected Edge Functions.
4. Fill only keyless Unity Mainnet endpoints where justified, run the `MainnetSmoke` validator, and keep the kill switch disabled.
5. Initialize Mainnet PDAs/ownership state after reviewing the exact transaction.
6. Enable the smoke kill switch only after successful read-only and simulated checks.
7. Build a MainnetSmoke APK and conduct one allowlisted Seeker smoke action.

## Related specifications

- [Devnet / Mainnet Build-Profile Boundary](DEVNET_MAINNET_BUILD_PROFILE_BOUNDARY.md)
- [Chainbound Program Registry](CHAINBOUND_PROGRAM_REGISTRY.md)
- [Chainbound Solana Beta Build Sequence](CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md)
