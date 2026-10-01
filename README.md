# Chainbound

Chainbound is a Unity-built creature battler that combines party-based combat, stylized world exploration, and optional Solana-backed progression. This repo is structured as a public hackathon monorepo: the Unity client is the center of gravity, with Supabase edge functions and an optional Anchor workspace included for the backend and on-chain side of the stack.

## Demo Highlights
- Turn-based 2v2 combat with action selection, targeting, swap flow, and presentation-driven battle scenes
- Combat test harness for iterating on move timing, VFX, camera behavior, and UX without full progression setup
- Wallet-aware service layer with devnet-safe defaults and local config overrides
- Supabase edge functions for profile bootstrap and progression flags
- Optional Solana/Anchor program for the ownership spine (Known / Stamped moves). The closed-beta client does not expose mint, stamp, or wager instructions.

## Tech Stack
- Unity `6000.0.24f1`
- C# gameplay and runtime systems
- Supabase Edge Functions
- Solana Unity SDK packages under `Packages/`
- Optional Rust + Anchor workspace under `backend/chainbound_game`

## Repo Map
- `Assets/`: Unity scenes, scripts, prefabs, art, and runtime content
- `Assets/Scripts/Combat/`: combat runtime, presentation, AI, data, and tests
- `Assets/Scenes/`: playable scenes including `main_menu.unity` and `Combat2_TestHarness.unity`
- `Supabase/`: edge functions and SQL migrations for server-side profile flows
- `backend/chainbound_game/`: optional Anchor workspace for on-chain development
- `Docs/`: curated architecture, setup, and gameplay docs
- `Packages/`: Unity package manifest plus embedded package sources required by the project

## Quickstart
1. Install Unity `6000.0.24f1`.
2. Open this folder in Unity Hub.
3. Copy `Assets/StreamingAssets/Config.sample.json` to `Assets/StreamingAssets/Config.json`.
4. Set `solanaRpcEndpoint` to a devnet-safe RPC endpoint, or use `CHAINBOUND_SOLANA_RPC_ENDPOINT`.
5. If you want backend profile flows, create a local `.env` from `.env.example` for Supabase secrets.
6. Open one of the main demo scenes and press Play.

## Best Demo Paths
- `Assets/Scenes/main_menu.unity`
  - Best high-level entry point for a judge or demo walkthrough.
- `Assets/Scenes/Combat2_TestHarness.unity`
  - Best engineering/demo scene for validating combat UX, camera work, targeting, and battle presentation changes.

## Architecture Snapshot
- Unity client:
  - Owns combat flow, scene presentation, UI, exploration systems, and wallet/service orchestration.
- Supabase edge layer:
  - Handles trusted profile mutations that should not run with client-side credentials.
- Optional on-chain workspace:
  - `backend/chainbound_game` contains the Anchor program (profile, vault, Known roll, Stamp, Bind, and gated encounter instructions). Local Unity combat does not require it. The player beta policy only allows profile and vault setup instructions.

## Config And Security
- No service-role keys, wallet seed phrases, or private RPC credentials should be committed.
- Public-safe samples live in `.env.example` and `Assets/StreamingAssets/Config.sample.json`.
- Real runtime values come from ignored local config or environment variables.
- See [SECURITY_NOTES.md](SECURITY_NOTES.md) for the repo's secret-handling policy.

## Current State And Limitations
- The Unity client is the primary deliverable: combat, overworld narrative, menus, and profile-backed capture.
- Supabase edge functions cover account, party, inventory, capture, and ownership calls. They are not a full live-service platform.
- The Anchor program implements the ownership spine. A devnet report in `Docs/Specs/Economy/` still marks player-facing ownership flags NO-GO, and `ClosedBetaFeaturePolicy` blocks stamp, bind, vending, and wager instructions in the beta client.
- PvP lobby, ranked escrow, tournaments, and a competitive ladder are not playable modes. Client wager types exist and stay off the closed-beta path. The design notes are parked under `Docs/review-unimplemented/`.
- The combat harness is the fastest way to evaluate battle presentation without full progression setup.

## Documentation
- [Docs/INDEX.md](Docs/INDEX.md) — mechanics map and what is actually implemented
- [Docs/README.md](Docs/README.md)
- [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)
- [Docs/HOW_TO_PLAY.md](Docs/HOW_TO_PLAY.md)
- [Assets/Scripts/Combat/README.md](Assets/Scripts/Combat/README.md)
- [Supabase/README.md](Supabase/README.md)
- [backend/chainbound_game/README.md](backend/chainbound_game/README.md)
