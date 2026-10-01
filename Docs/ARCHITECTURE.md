# Architecture (High Level)

## Responsibility Split
- Unity Client (`Assets/`)
  - Combat systems, UI, world logic, and player interaction.
  - Wallet session lifecycle and gameplay service orchestration via `SolanaServiceBridge`.
  - Reads local runtime config (`StreamingAssets/Config.json`) for non-secret endpoint overrides.
- On-Chain (Solana programs)
  - Program-owned state and transaction execution for chain-integrated gameplay features.
  - Program IDs are public identifiers, not private credentials.
- Backend / Edge Layer (`Supabase/`, optional `backend/chainbound_game`)
  - Trusted operations requiring server-side credentials (service-role key).
  - Profile initialization/update endpoints and other privileged flows.

## Data Flow
1. Player starts game and opens combat/gameplay scene.
2. Unity wallet/service layer initializes (`SolanaServiceBridge`).
3. Client reads config override:
   - `CHAINBOUND_SOLANA_RPC_ENDPOINT` env var or `Assets/StreamingAssets/Config.json`.
4. Client performs read/write operations through wallet + RPC abstractions.
5. For privileged profile operations, client calls Supabase Edge Functions.
6. Edge Functions use server-side service-role credentials to update Supabase safely.

## Security Boundaries
- Client is untrusted: never place service-role keys or private signing keys in Unity assets.
- Edge Functions/backend are trusted: keep sensitive secrets in env/secret managers only.
- Repo-safe defaults use devnet public endpoint and sample config placeholders.

Mechanic paths and implementation status: [INDEX.md](INDEX.md).
