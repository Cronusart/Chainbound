# Profile-native ownership conformance audit

As of the 20260824 cutover, `player_profile_id` is the only account and gameplay
owner identifier.

## Invariants

- `public.players` does not exist.
- `player_profiles.legacy_player_id` does not exist.
- Roster, move inventory, lifetime stats, event/session records, vending tickets,
  achievements, and move upgrade events use `player_profile_id`.
- Wallets live only in `profile_wallets` and are optional linked identities.
- Edge Functions resolve the authenticated JWT subject to one profile before any
  profile read or mutation.
- Unity starter, name, intro, party, roster, battle, capture, and progression
  services call only `account_*` or `profile_*` endpoints.

## Retired endpoints

The following wallet-keyed functions were removed from source and deployment:

- `player_connect`
- `player_commit_starter`
- `get_player_party`
- `player_set_display_name`
- `player_set_intro_complete`

Wallet connection must happen after profile authentication through the verified
wallet-link flow. A wallet address never grants profile authority.

## Verification

- Migration: `20260824020000_remove_legacy_players_model.sql`
- SQL regression: `supabase/tests/profile_native_ownership_v1.sql`
- Architecture: `Docs/Architecture/Account_Platform_Refactor.md`

