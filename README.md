# Chainbound

Chainbound is a creature-collector battler from **SunOrc Games** (Texas). You explore a stylized overworld, catch Chainlings, and fight turn-based 2v2 battles. The Unity client is the game. Supabase holds profiles and trusted server actions, a .NET battle service on Cloud Run resolves authoritative PvE capture, and a Solana program on devnet holds wallet accounts, the ownership spine, and MagicBlock verifiable randomness.

The Android build is aimed at the [Solana dApp Store](Docs/Release/SolanaSeekerBeta.md) (Seeker and Mobile Wallet Adapter). Public contact: **chainboundgame@gmail.com**.

## What you can play

- Turn-based 2v2 combat: action select, targeting, swaps, and a presentation-driven battle scene.
- `Assets/Scenes/Combat2_TestHarness.unity` — combat UX, timing, VFX, and camera without a full profile.
- `Assets/Scenes/main_menu.unity` — menu and account entry. `Assets/Scenes/Bootstrap.unity` brings up services.
- Overworld scenes such as `Loomville.unity`, `Jungle.unity`, and `player_room.unity`, with narrative sequences and wild encounters.
- Profile-backed party, inventory, and capture. Authoritative capture talks to the Cloud Run battle service when that rollout flag is on.

## Tech stack

| Piece | What this repo uses |
|---|---|
| Client | Unity **6000.6.6f1** (target editor), C#, Universal Render Pipeline |
| Mobile | Solana Mobile Wallet Adapter, embedded PlaySolana Unity SDK under `Packages/PlaySolana.Unity-SDK-main` |
| Chain client | Project-owned Solana client in `Assets/Scripts/Solana` (not the retired Solana Unity SDK) |
| Profiles | Supabase Postgres, Auth, and Edge Functions in `Supabase/` |
| Battles | .NET 8 `backend/Chainbound.BattleService`, image in `Dockerfile`, Cloud Run port 8080 |
| Program | Anchor 0.32.1 / Rust 1.89 program `backend/chainbound_game` |
| Randomness | MagicBlock base-layer VRF via `ephemeral-rollups-sdk` |

`ProjectSettings/ProjectVersion.txt` in this commit still records Unity `6000.0.24f1` (`11fa355cd605`). Open the project in **6000.6.6f1**. That file is updated when the editor upgrade is committed.

Devnet battle-service URL and the capture rollout flag: `Assets/Data/Combat/Capture/AuthoritativeCombatServiceConfig.asset` (Cloud Run, `us-central1`).

## MagicBlock VRF

MagicBlock supplies 32 bytes of verifiable randomness for one encounter request. Chainbound does not draw those bytes itself. The program creates an `EncounterRequest` account, asks MagicBlock's base-layer queue, and accepts the callback only from MagicBlock's program-scoped VRF identity.

| Instruction | Role |
|---|---|
| `request_encounter` | Player-funded PDA. Seeds: `encounter_request`, wallet, 32-byte request seed. CPI to the pinned queue. |
| `fulfill_encounter` | MagicBlock callback. Writes randomness and moves Pending → Fulfilled. |
| `consume_encounter_randomness` | Owner-signed, exactly once. Fulfilled → Consumed. |

Pinned addresses (the same constants the Unity client refuses to start without):

- VRF program `Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz`
- Oracle queue `Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh`

The devnet game program is `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` (`Anchor.toml`, `declare_id!`). That devnet binary includes the `encounter_request` path. This repo does not record a completed Devnet efficacy signature (request, callback, and consume). The open checklist is [Docs/Specs/Economy/OWNERSHIP_SPINE_PHASE1_OPERATOR_CHECKLIST.md](Docs/Specs/Economy/OWNERSHIP_SPINE_PHASE1_OPERATOR_CHECKLIST.md) section 8.

Where to read it:

- Program: `backend/chainbound_game/programs/chainbound_game/src/lib.rs` (`request_encounter`, `EncounterRequest`)
- SDK pin: `backend/chainbound_game/programs/chainbound_game/Cargo.toml`
- IDL check: `backend/chainbound_game/tests/vrf_idl.ts`
- Client: `Assets/Scripts/Solana/VRF/VrfService.cs` and `Assets/Scripts/Solana/Config/SolanaConfigSO.cs`
- Editor proof: **Chainbound → Solana → Run MagicBlock VRF Devnet Efficacy** in `Assets/Scripts/Editor/MagicBlockVrfEfficacyMenu.cs`

Scope, so the integration is not overstated:

- Overworld wild encounters roll in `EncounterRollService` (local seeded tables). `EncounterTriggerZone` calls that service.
- Player builds compose the real `VrfService`. Editor and test builds use `DeterministicVrfService` while `useDeterministicVrfInEditor` is on.
- `VrfService` is what the efficacy menu calls. Stamp modifier draws and capture IVs do not read this `EncounterRequest`.
- `ClosedBetaFeaturePolicy` does not list `request_encounter`. `VrfService` does not consult that allowlist.

## How the pieces fit

1. Unity owns combat presentation, overworld, UI, wallet session, and local prediction.
2. Supabase Edge Functions perform trusted profile, inventory, narrative, and ownership calls. The client never ships the service-role key. Android RPC goes through `solana_rpc_proxy`, which keeps the provider URL server-side.
3. `Chainbound.BattleService` is the authority for resumable PvE capture. It links the Unity-free rules in `Assets/Scripts/Combat/Core`, so the client and the service share the same fixed-point simulation. Battle RAM settlement is profile-native in Postgres (`battle_reward_settle`).
4. The Anchor program is the on-chain ownership spine: player profile, per-wallet move vault, per-wallet chainling vault, Known moves, Stamp (Metaplex Core), Bind, and the VRF encounter request above. Local combat does not require a deploy.

Closed-beta player transactions are limited to `create_profile`, `create_move_vault`, `initialize_chainling_vault`, `initialize_equipped_registry`, and `update_position` (`Assets/Scripts/Combat/Runtime/Services/ClosedBetaFeaturePolicy.cs`). Known roll, Stamp, Bind, and encounter VRF stay off that list. The devnet ownership report is still NO-GO for turning player ownership flags on: [Docs/Specs/Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md](Docs/Specs/Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md).

A casual Colosseum queue and Supabase matchmaking functions are in the tree. Ranked escrow, tournaments, and a competitive ladder are not playable modes. Notes for those sit under `Docs/review-unimplemented/`.

## Repo map

- `Assets/` — scenes, scripts, prefabs, and content
- `Assets/Scripts/Combat/` — battle runtime, presentation, AI, and tests
- `Assets/Scripts/Solana/` — wallet, RPC, program client, VRF
- `Assets/Scenes/` — `main_menu.unity`, `Bootstrap.unity`, `Combat2_TestHarness.unity`, overworld scenes
- `Supabase/` — Edge Functions and SQL migrations
- `backend/Chainbound.BattleService/` — .NET 8 battle and capture authority, plus `Dockerfile`
- `backend/chainbound_game/` — Anchor workspace, IDL, and TypeScript tests
- `Docs/` — start at [Docs/INDEX.md](Docs/INDEX.md)
- `Packages/` — Unity manifest, PlaySolana SDK, embedded Newtonsoft.Json
- `tools/` — capture verification, library-card render, ownership smoke scripts

## Quickstart

1. Install Unity **6000.6.6f1** and open this folder in Unity Hub.
2. For Editor RPC overrides, copy `Assets/StreamingAssets/Config.sample.json` to `Assets/StreamingAssets/Config.json`. Leave `ownershipCluster` on `devnet`.
3. For Android, copy that sample to `Assets/Resources/ChainboundLocalConfig.json` (gitignored). Point `solanaRpcEndpoint` at the authenticated `solana_rpc_proxy` function URL. The provider credential stays in Supabase secrets. `CHAINBOUND_SOLANA_RPC_ENDPOINT` is an Editor-only direct-RPC override.
4. For Edge Functions locally, copy `.env.example` to `.env` and fill it outside git. Do not commit secrets, keypairs, or keystores.
5. Press Play in `main_menu.unity` or `Combat2_TestHarness.unity`.

Building the program is separate from opening the client. `backend/chainbound_game/Anchor.toml` targets devnet. Deploy keys stay outside the repo. Regenerate the IDL and the Unity client with `anchor build`, then `backend/chainbound_game/scripts/generate-unity-client.ps1`. Do not hand-edit either artifact.

## Config and security

No service-role keys, wallet seeds, or private RPC credentials belong in git. Samples are `.env.example` and `Assets/StreamingAssets/Config.sample.json`. See [SECURITY_NOTES.md](SECURITY_NOTES.md).

The game is for adults 18+ (or the local age of majority, if higher). Terms and privacy: [Docs/Legal/TERMS_OF_USE.md](Docs/Legal/TERMS_OF_USE.md), [Docs/Legal/PRIVACY_POLICY.md](Docs/Legal/PRIVACY_POLICY.md).

## Documentation

- [Docs/INDEX.md](Docs/INDEX.md) — mechanic, status, and code path
- [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)
- [Docs/HOW_TO_PLAY.md](Docs/HOW_TO_PLAY.md)
- [Docs/Release/BETA_FEATURE_ALLOWLIST.md](Docs/Release/BETA_FEATURE_ALLOWLIST.md)
- [Docs/Release/SolanaSeekerBeta.md](Docs/Release/SolanaSeekerBeta.md)
- [Assets/Scripts/Combat/README.md](Assets/Scripts/Combat/README.md)
- [Supabase/README.md](Supabase/README.md)
- [backend/chainbound_game/README.md](backend/chainbound_game/README.md)
- [backend/Chainbound.BattleService/README.md](backend/Chainbound.BattleService/README.md)

## Contact

SunOrc Games · Texas · **chainboundgame@gmail.com**
