# Chainbound Program Registry

Status: repo source of truth for local/dev program IDs and generated contract artifacts.
Date: 2026-09-12

## Game Program

The checked-in Anchor workspace is the authoritative source for the Chainbound game program contract.

| Environment | Program ID | Source |
| --- | --- | --- |
| localnet/devnet working ID | `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` | `backend/chainbound_game/Anchor.toml` and `declare_id!` |

Unity dev config should use this ID while the repo backend is the authoritative contract. Any deployed program with a different ID must be documented here before Unity or Supabase is pointed at it.

The singleton `GameConfig` PDA uses seed `game_config`. Deployment initializes it once with the BattleService/server signer as `result_authority`; that private key is never committed or distributed to player builds.

## Generated contract artifacts

- Checked-in Anchor IDL: `backend/chainbound_game/idl/chainbound_game.json`
- Generated Unity contract: `Assets/Scripts/Solana/Programs/Generated/ChainboundGameProgramContract.Generated.cs`
- Generator: `backend/chainbound_game/scripts/generate-unity-client.ps1`

Generation order:

1. `anchor build` (Anchor CLI 0.32.1) produces `target/idl/chainbound_game.json`.
2. Run `scripts/generate-unity-client.ps1` from `backend/chainbound_game` to copy the canonical IDL and regenerate C# instruction/account metadata.
3. Run Anchor authority/account-order tests and Unity `ChainboundGameIdlParityTests` before committing.

## Metaplex Core

| Environment | Program ID |
| --- | --- |
| devnet/mainnet | `CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d` |

## Required Deployment Secrets

Supabase Edge Functions that verify Core cosmetics / bag collectibles require:

- `CHAINBOUND_SOLANA_CLUSTER`
- `CHAINBOUND_SOLANA_RPC_ENDPOINT`
- `CHAINBOUND_DAS_RPC_URL`
- `CHAINBOUND_GAME_PROGRAM_ID`
- `CHAINBOUND_RESULT_AUTHORITY_PUBKEY` *(BattleService/server public key stored in `GameConfig`; the private key stays in the server secret store)*
- `CHAINBOUND_CORE_COSMETICS_COLLECTION`
- `CHAINBOUND_CORE_COSMETICS_UPDATE_AUTHORITIES`
- `CHAINBOUND_CORE_ITEMS_COLLECTION` *(bag Core collectibles — Archive tools / case bindings; DAS refresh TBD)*
- `CHAINBOUND_CORE_ITEMS_UPDATE_AUTHORITIES` *(comma-separated; fail closed when unset for paid consumes)*

Paid/tradable cosmetics and Core bag items must fail closed when collection or trusted update authority configuration is missing.
