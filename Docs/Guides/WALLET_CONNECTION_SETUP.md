# Wallet connection setup

Signal Jam uses the project-owned Solana client under `Assets/Scripts/Solana` and the official Solana Mobile Wallet Adapter (MWA) 2.0 Android library. The vendored Solana Unity SDK, its Web3 singleton, Web3Auth, copied wallet prefabs, and local-password wallet path are not part of the runtime.

## Required configuration

Open the `SolanaConfigSO` asset assigned to `SolanaServiceBridge` and set:

- `rpcEndpoint` to the intended cluster RPC endpoint. Use devnet until the program and mobile flow pass their security gates.
- `mwaIdentityUri` to an HTTPS URL controlled by the project. Android wallet connection fails closed while this is empty or non-HTTPS.
- `mwaIdentityName` to the player-facing application name.
- `mwaIconUri` to an icon URI relative to the identity URI, normally `favicon.ico`.
- `gameProgramId` to the reviewed Chainbound program for the selected cluster.
- `vrfProgramId` to MagicBlock's reviewed base-layer program
  `Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz`.
- `vrfOracleQueueId` to the reviewed base-layer queue
  `Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh`.
- `useDeterministicVrfInEditor` may be enabled only for Editor/tests. Player builds ignore it and
  compose the real `VrfService`.

Do not add a private key, seed phrase, Web3Auth client secret, or local signing wallet to Unity assets, source, environment configuration, or `PlayerPrefs`.

The current Devnet/Seeker identity is saved in `Assets/Data/Solana/SolanaConfig.asset`:

- identity URI: `https://chainbound-pub.chainboundsolana.workers.dev`
- identity name: `Chainbound`
- relative icon URI: `favicon.ico`

Both the identity page and icon returned HTTP 200 during the Milestone 1 preflight on 2026-09-23.
The physical Seeker acceptance pass completed the same day: a fresh install entered the main-menu
New Game wallet prompt, established MWA encrypted sessions for authorization and challenge signing,
received HTTP 200 from `wallet_connect_verified`, and created a linked account session whose active
wallet matched the connected public key. This pass does not cover transaction signing, the negative
path matrix, or the required second-device Mock MWA Wallet pass.

## Android flow

1. Unity constructs and decodes a legacy Solana transaction.
2. The shared transaction boundary verifies that the connected wallet is the fee payer.
3. Unity simulates the transaction through the configured RPC and fails closed on an error.
4. The Android bridge opens the wallet and calls MWA 2.0 `signAndSendTransactions`.
5. Unity validates the returned signature and independently confirms/reconciles it through RPC.

The bridge is in `Assets/Plugins/Android/SignalJamSolanaMwa.androidlib`. Gradle resolves `com.solanamobile:mobile-wallet-adapter-clientlib-ktx:2.0.7`; do not copy its AARs or AndroidX dependencies into the project. That artifact carries Kotlin 2.1 metadata, so the Android library pins the Kotlin Android Gradle plugin to `2.1.10`; an older 1.9 compiler cannot consume it. MWA 2.0 requires Android API 24 or newer, so the project's minimum Android API level is 24. The editor-only `Assets/Scripts/Editor/SolanaAndroidBuildSettings.cs` guard preserves that minimum if Player Settings are changed later.

The official MWA result sender requires an AndroidX `ComponentActivity`. The plugin therefore opens a private, non-exported, translucent `SignalJamMwaActivity` for the wallet handoff and returns to Unity afterward. Authorization tokens, messages, and transaction bytes remain in process memory and are not placed in the Activity intent.

## Main-menu account proof

The main-menu Connect Wallet action is not a connect-only operation. The required path is:

1. authorize the selected account through MWA;
2. request a one-time challenge from `wallet_auth_challenge`;
3. sign the exact challenge message through MWA;
4. submit the proof to `wallet_connect_verified`;
5. accept the result only when it contains an active wallet identity matching the connected public key.

`WalletConnectionPromptService` enters this path through
`MainMenuFlowOrchestrator.ConnectWalletToCurrentProfileAsync()`. Do not route the fresh-connect
button through `LinkWalletToCurrentProfile`; that API is only valid after a wallet connection has
already been established. `wallet_connect_verified` resolves both valid outcomes after proof:

- if the wallet is new, it is linked to the current fresh profile;
- if the wallet already owns a profile, the caller's current Supabase auth identity is moved to that
  profile through the service-role-only `profile_recover_by_wallet_tx` transaction.

Recovery revokes stale profile-identity mappings, preserves both profile rows, and never copies or
merges guest progression. The Game Menu therefore exposes only **Play Game** and **Connect Wallet**;
there is no separate Restore Profile action.

## Desktop editor behavior

The editor-only desktop bridge exists for devnet testing. It is isolated from Android and must never become a production fallback. If an Android wallet cannot authorize, the operation fails; the game does not generate or import a local player key.

## MagicBlock VRF efficacy

The Devnet efficacy path is request → poll → owner consume. It is independent of ownership fees.
The game program derives an EncounterRequest PDA from the wallet and a hashed 32-byte request
context, requests from the pinned queue, and accepts fulfillment only through MagicBlock's
program-scoped VRF identity.

For a live Editor proof, temporarily turn off `useDeterministicVrfInEditor`, enter Play Mode with a
funded Devnet wallet, then select **Chainbound → Solana → Run MagicBlock VRF Devnet Efficacy**. The harness
logs the public request PDA, transaction signature, 32-byte result, latency, and consume result. It
does not log wallet secrets. Restore the deterministic Editor switch afterward.

Polling defaults to 2 seconds with a 30-second timeout. Treat failed simulation, wallet
cancellation, rejected request accounts, invalid PDA owner/discriminator/status, delayed callback,
RPC timeout, and consume replay as distinct failures. A live proof requires the pinned Chainbound
program upgrade; the old Devnet binary has no request instruction.

## Build verification

Before accepting an Android build:

- confirm no active `Solana.Unity`, `Web3Auth`, or `com.solana.unity_sdk` references remain;
- compile Unity scripts with zero errors;
- build an Android development APK and inspect Gradle for dependency-resolution or duplicate-class failures;
- inspect the APK/Dex output for `SignalJamMwaBridge`, `SignalJamMwaActivity`, and the official Mobile Wallet Adapter client classes;
- connect with a real MWA-compatible wallet on devnet;
- test cancellation, authorization expiry, wallet switching, failed simulation, stale blockhash, confirmation timeout, and app resume;
- inspect the requested transaction in the wallet before approval.

### Milestone 1 Seeker evidence — 2026-09-23

- APK: `Builds/Android/Chainbound-Milestone1-Dev.apk`
- SHA-256: `1AEE5123C3D73294F1CB48FBD9F3AFEF191AD80E8FBA6C162695EBDDC9D288F3`
- package: `com.signaljam.chainbound`, version `0.0.1` (`versionCode=1`)
- Android: ARM64 IL2CPP, minimum API 24, target API 35, development build
- APK inspection: `SignalJamMwaBridge`, `SignalJamMwaActivity`, and official MWA client classes present
- device result: clean install succeeded; Unity reached `main_menu` without a fatal exception
- wallet result: two encrypted MWA sessions completed, `wallet_connect_verified` returned HTTP 200,
  and `GameSessionContext` received a non-empty linked wallet identity
- validation: Android release validator passed; `SolanaMobileReleaseSecurityTests` passed 12/12
- scene wiring: stale `EditorMockIdentityProvider` and `EditorMockWalletProvider` references were
  cleared from `main_menu`; player builds resolve the production providers from Bootstrap

The MWA client emitted transient localhost WebSocket connection-refused warnings while waiting for
the wallet-side association server. Both encrypted sessions subsequently established and completed,
so these warnings were non-fatal in this pass; retain them in the later retry/timeout matrix.

The old SDK and generated wallet assets are kept only in `Quarantine/` as a recoverable migration backup. Unity does not import content from that directory.

Canonical security decisions and remaining policy work are tracked in `Docs/Specs/Solana/MOBILE_WALLET_ACCOUNT_SESSION_HARDENING_PLAN.md`.
