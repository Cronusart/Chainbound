# Supabase Edge Layer

This folder contains the lightweight backend pieces used for profile-oriented game flows.

## Contents
- `functions/profile_set_party/`
  - Persists the ordered combat party roster (`party_chainling_ids`) for a resolved player profile (`chainlings.party_slot` + `is_party_member`).
- `functions/account_*/`
  - Authenticated profile bootstrap, session, identity, and wallet-link flows.
- `functions/profile_*/`
  - Authenticated profile reads and gameplay/narrative mutations.
- `functions/solana_rpc_proxy/`
  - Authenticated, rate-limited JSON-RPC proxy for the Unity client. It exposes only the reviewed read/simulation/confirmation methods and keeps the provider URL server-side. Mobile Wallet Adapter remains responsible for player transaction submission.
- `migrations/`
  - SQL migrations for the project's Supabase-side data model.

## Environment
The edge functions expect server-side secrets from environment variables:
- `SUPABASE_URL`
- `SUPABASE_SERVICE_ROLE_KEY`
- `CHAINBOUND_SOLANA_CLUSTER` (`devnet` or `mainnet-beta`)
- `CHAINBOUND_SOLANA_RPC_DEVNET_ENDPOINT` / `CHAINBOUND_SOLANA_RPC_MAINNET_ENDPOINT` (optional preferred cluster-specific Helius RPC URLs)
- `CHAINBOUND_SOLANA_RPC_ENDPOINT` (legacy/shared HTTPS RPC fallback, finalized-capable and cluster-verified with `getGenesisHash`)
- `HELIUS_DAS_URL` (server-only DAS endpoint whose host and cluster must match the selected environment)
- `CHAINBOUND_GAME_PROGRAM_ID`
- `CHAINBOUND_CORE_MOVES_COLLECTION`
- `CHAINBOUND_CORE_MOVES_UPDATE_AUTHORITY`
- `CHAINBOUND_RESULT_AUTHORITY_SECRET`
- `CHAINBOUND_OWNERSHIP_METADATA_URI_BASE`
- `CHAINBOUND_OWNERSHIP_GENERATOR_VERSION`
- `CHAINBOUND_OWNERSHIP_MODIFIER_POOL_HASH`
- `CHAINBOUND_OWNERSHIP_SCHEMA_VERSION`
- `CHAINBOUND_OWNERSHIP_STAMP_RAM_COST`
- `CHAINBOUND_OWNERSHIP_BIND_RAM_COST`
- `CHAINBOUND_OWNERSHIP_TICKET_TTL_SECONDS`

Mainnet additionally requires:

- `CHAINBOUND_MAINNET_SMOKE_ENABLED=true` only during an approved smoke window
- `CHAINBOUND_MAINNET_SMOKE_WALLETS` as a JSON array of exact wallet addresses

Mainnet fails closed when the kill switch is off, the wallet is not allowlisted, the Mainnet program equals the Devnet program, or the RPC genesis hash does not match Mainnet. Do not deploy Devnet grant/top-up/operator functions to the Mainnet project.

Use `.env.example` at the repo root as the public-safe reference. Do not place real secrets in source files or Unity assets.

`solana_rpc_proxy` validates every candidate against `CHAINBOUND_SOLANA_CLUSTER`. It prefers the matching cluster-specific RPC secret, then the shared RPC secret, and finally the matching `HELIUS_DAS_URL`. The last fallback lets older Devnet projects keep a public shared transaction RPC while the authenticated mobile proxy still uses the existing server-only Helius endpoint. A wrong-cluster or uncredentialed provider URL is skipped and the proxy fails closed when no valid candidate remains.

## Role In The Stack
- Unity is the primary client.
- Supabase handles trusted profile mutations that should not be driven directly by client-side credentials.
- Gameplay ownership is profile-native. Wallets are optional linked identities;
  they never own roster, inventory, economy, or narrative state. The pre-release
  `players` table and wallet-only Edge Functions were retired in the 20260824 cutover.
