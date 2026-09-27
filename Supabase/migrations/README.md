# Supabase migrations

These migrations are ordered by their timestamp prefixes and should be applied sequentially. Historical migrations introduce the account model additively; the 20260824 cutover makes `player_profile_id` the sole gameplay owner and removes the pre-release wallet-first model.

## Apply Order

1. `20260505023000_drop_legacy_wallet_keyed_player_profiles_if_present.sql`
   - Removes only the old wallet-keyed `public.player_profiles` table shape if it exists.
   - Leaves the canonical account table untouched if it already exists.
2. `20260505023100_players_columns_for_wallet_and_refactor.sql`
   - Adds `players` compatibility columns used by `player_connect` and the account backfill.
   - Adds the `wallet_lower` trigger and unique index required by `.upsert(..., { onConflict: "wallet_lower" })`.
3. `20260505023200_chainlings_progression_and_stats.sql`
   - Adds progression, player-facing, analytics, update trigger, and RLS support to `chainlings`.
4. `20260505023300_player_lifetime_stats.sql`
   - Adds `player_lifetime_stats` for player analytics and progression totals.
5. `20260505023400_account_identity_model.sql`
   - Adds canonical account/profile tables and maps legacy `players` / `chainlings` rows to `player_profile_id`.
6. Later Chainlings gameplay migrations through `20260619000100_chainling_progression_grants.sql`
   - Add visual identity, loadouts, service records, HP sync, and progression grants.
7. `20260625000100_chainbound_core_cosmetics.sql`
   - Adds the Chainbound Core cosmetic catalog, ownership verification cache, profile chain-state read model, and `chainlings.visual_identity_asset_ids`.
8. `20260824010000` through `20260824010400`
   - Add profile-native economy and ownership columns and replace starter, capture, progression, rebirth, move-upgrade, lifetime-stat, and narrative transactions.
9. `20260824020000_remove_legacy_players_model.sql`
   - Verifies the pre-release bridge is empty, removes `public.players`, `legacy_player_id`, and gameplay `player_id` ownership columns, and enforces profile foreign keys.
10. `20260824020100_profile_owned_loadout_delete_cascade.sql`
   - Makes profile deletion cascade deterministically through equipped loadout rows.
11. `20260829000100_battle_defeat_ram_penalty.sql`
   - Records the PvE defeat RAM cost in lifetime spending and deducts it from the canonical profile balance without allowing a negative balance.
12. `20260829071718_chainling_passive_recovery.sql`
   - Adds server-time passive HP recovery checkpoints and the profile recovery transaction used for offline healing.
13. `20260903010000_authoritative_trading_trace_capture.sql`
   - Adds authoritative PvE sessions and idempotent commands, trading-trace capture profiles/events, utility equipment and real-time boosts, the capture ledger/stats/achievements, atomic capture transactions, and idempotent abandoned-battle defeat consequences.

## Prerequisites

- Historical migrations through `20260505023400` expect the original MVP tables to exist. The final hosted schema does not contain `public.players`.
- `public.chainlings` must already exist before `20260505023200_chainlings_progression_and_stats.sql` runs.
- These migrations do not recreate the full MVP table set shown in the dashboard (`moves_catalog`, `loadouts`, `achievements`, etc.). Capture that baseline separately if you need a totally empty database rebuild.

## Operational Notes

- Prefer `supabase db push`, Supabase MCP `apply_migration`, or a controlled CI pipeline over ad-hoc SQL Editor runs.
- Confirm hosted migration history with `supabase migration list` or Supabase MCP `list_migrations`.
- After schema migration, deploy the Edge Functions under `Supabase/functions/` and verify required secrets before testing Unity account flows.
- Required cosmetic verification secrets: `CHAINBOUND_SOLANA_CLUSTER`, `CHAINBOUND_GAME_PROGRAM_ID`, `CHAINBOUND_DAS_RPC_URL`, `CHAINBOUND_CORE_COSMETICS_COLLECTION`, and `CHAINBOUND_CORE_COSMETICS_UPDATE_AUTHORITIES`.
