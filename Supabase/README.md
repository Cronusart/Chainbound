# Supabase Edge Layer

This folder contains the lightweight backend pieces used for profile-oriented game flows.

## Contents
- `functions/profile_set_party/`
  - Persists the ordered combat party roster (`party_chainling_ids`) for a resolved player profile (`chainlings.party_slot` + `is_party_member`).
- `functions/account_*/`
  - Authenticated profile bootstrap, session, identity, and wallet-link flows.
- `functions/profile_*/`
  - Authenticated profile reads and gameplay/narrative mutations.
- `migrations/`
  - SQL migrations for the project's Supabase-side data model.

## Environment
The edge functions expect server-side secrets from environment variables:
- `SUPABASE_URL`
- `SUPABASE_SERVICE_ROLE_KEY`
- `CHAINBOUND_SOLANA_RPC_ENDPOINT` (required for authoritative passive recovery and future offline timers; use a finalized-capable Solana RPC)

Use `.env.example` at the repo root as the public-safe reference. Do not place real secrets in source files or Unity assets.

## Role In The Stack
- Unity is the primary client.
- Supabase handles trusted profile mutations that should not be driven directly by client-side credentials.
- Gameplay ownership is profile-native. Wallets are optional linked identities;
  they never own roster, inventory, economy, or narrative state. The pre-release
  `players` table and wallet-only Edge Functions were retired in the 20260824 cutover.
