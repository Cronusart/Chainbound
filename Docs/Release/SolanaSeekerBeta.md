# Solana Seeker Beta Release Gate

This checklist is the release contract for the Solana dApp Store Android APK. A green Unity compile alone is not release approval.

Closed-beta **what the binary may do** and **who may run it**: [`BETA_FEATURE_ALLOWLIST.md`](BETA_FEATURE_ALLOWLIST.md).  
Build order for the program and client: [`CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md`](../Specs/Solana/CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md).

## Build configuration

- Run `Tools > Chainbound > Solana Mobile > Validate Android Release`.
- Configure `Assets/Data/Solana/SolanaConfig.asset` with an owned HTTPS identity URI, a relative hosted icon path, and the public game name.
- Keep `useStubServices` disabled and confirm the intended Solana cluster. Beta defaults to devnet until mainnet is explicitly approved.
- Build ARM64 with IL2CPP, API 24 or newer, landscape-only autorotation, and package id `com.signaljam.chainbound`.
- Produce a signed APK with the dedicated dApp Store keystore. Back up that key securely; every update must use the same key and a higher version code.
- Never commit keystore files, passwords, private keys, seed phrases, service-role keys, or production RPC secrets.

## Wallet acceptance matrix

Run every case on a physical Seeker and once on a second Android device with the official Mock MWA Wallet.

1. Fresh install: Main Menu > Connect Wallet opens a compatible wallet and identifies Chainbound with the hosted icon.
2. Approve authorization: the app returns to landscape, displays the expected public address, requests the server nonce signature, and resolves the correct profile.
3. Reject authorization and reject message signing: the app returns to an interactive menu with a cancellation message and no profile mutation.
4. Press the in-game Cancel button while the wallet request is open: the host activity closes, input unlocks, and a second connection attempt succeeds immediately.
5. Leave the request open past two minutes: it times out, closes cleanly, and a retry succeeds without `REQUEST_IN_PROGRESS`.
6. No compatible wallet installed: show an actionable wallet-unavailable message; never spin indefinitely.
7. Existing wallet linked to this profile: reconnect idempotently without duplicate connection events.
8. Wallet linked to another profile: show the explicit restore/continue conflict; never silently merge profiles.
9. Switch wallet account between authorization and signing: reject the mismatched challenge.
10. Background/foreground and rotate the phone during each wallet step: no duplicated prompt, black screen, lost input lock, or portrait Unity surface.
11. Verify the backend rejects expired, reused, wrong-profile, wrong-wallet, modified-message, malformed, and invalid Ed25519 proofs.

## Transaction gate

Before enabling any paid/on-chain action in the beta, its confirmation UI must show the cluster, fee payer, program/action, asset or cost summary, simulation result, wallet handoff, submitted signature, and final confirmation/failure state. Unity must not perform ownership-sensitive writes from client-asserted ownership data.

## Store artifacts

- Archive the signed APK, version name/code, mapping/symbol files, commit identifier, Unity version, MWA dependency version, RPC cluster, and device-test results.
- Validate install/update behavior using the same signing key before submission.
- Complete the Solana dApp Store publisher and release metadata separately from the Unity build.
