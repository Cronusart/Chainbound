# Chainbound Ownership / Economy Security Audit — Index

**Title:** Chainbound Ownership/Economy Security Audit  
**Date:** 2026-09-25  
**Scope:** RAM, Known moves, Stamped Core Moves, Chainlings (IVs/progression), items, Bind (config/program), encounter/combat reward paths, admin/debug grants  
**Method:** Static review of Unity (`Assets/Scripts`), Supabase Edge Functions + migrations, Anchor `chainbound_game` program, and existing Economy/Ownership specs. No runtime code changes; no feature-flag flips; no commits.

## Executive summary

Chainbound runs a **hybrid** economy: soft currency and most progression live in **Postgres profile rows** mutated by **Edge Functions (service role + JWT)**, while **Stamped Moves / Bind / on-chain RAM grants / encounter VRF** live in the Solana program. Unity ownership spine flags (`enableDevnetOwnershipSpine`) are documented **OFF** for the live player spine; stamp/mint is therefore mostly a Devnet matrix path, but **profile-native faucets remain fully live** for any authenticated client that can call Edge Functions.

The highest-severity pattern is **client-authored grant payloads** accepted by JWT-authenticated endpoints (battle RAM/XP/stars, capture IVs/entangled, progression Known `move_id`, event Known grants with caller-chosen idempotency keys). Ownership Phase 1 paths (Known roll, Stamp prepare/confirm) are comparatively stronger: pinned costs, atomic SQL, ticket idempotency, DAS verification, and result-authority secrets server-side only.

## Mechanism reviews

| # | File | Mechanism | Live? |
|---|---|---|---|
| 01 | [01_RAM_ledger_and_balances.md](01_RAM_ledger_and_balances.md) | Profile + on-chain RAM dual ledger, daily cap, battle faucet/sink | Live (profile); on-chain grant Devnet/smoke |
| 02 | [02_Known_roll_and_grants.md](02_Known_roll_and_grants.md) | Candy Known roll (5 RAM), event Known grant, progression ordinals | Live |
| 03 | [03_Stamp_prepare_confirm_mint.md](03_Stamp_prepare_confirm_mint.md) | Prepare ticket → wallet Stamp → confirm + Known consume | Live Edge/program; Unity flags OFF |
| 04 | [04_Second_stamp_deferred.md](04_Second_stamp_deferred.md) | Add-mod / second Stamp @ 150 RAM | Deferred / not implemented |
| 05 | [05_Encounter_rewards_and_VRF.md](05_Encounter_rewards_and_VRF.md) | PvE battle outcome, MagicBlock encounter VRF, victory Known | Live (profile battle); VRF on-chain |
| 06 | [06_Chainling_IV_and_progression.md](06_Chainling_IV_and_progression.md) | Capture, IVs, stars/XP, rebirth RAM sinks | Live |
| 07 | [07_Items_inventory.md](07_Items_inventory.md) | Catalog stacks, world pickups, field use, core consume | Live |
| 08 | [08_Bind_archive.md](08_Bind_archive.md) | Bindery archive Bind @ 250 RAM pin | Program present; Unity/live spine OFF / config-only |
| 09 | [09_Admin_debug_grant_paths.md](09_Admin_debug_grant_paths.md) | Dev candy grants, smoke RAM grant, entitlement stub | Mixed |

## Mechanisms searched but not found as live player faucets/sinks

| Candidate | Status |
|---|---|
| Market Known buy (90/150 RAM) | Spec only (`ECONOMY_01`); no Edge Function found |
| Salvage Known/Stamped → RAM | Spec guardrails only; no live Edge sink/faucet found |
| Press Charge | Retired by Ownership Spine 2026-09-15 |
| Bubblegum / Inscription move mints | Quarantined / disabled per spine |
| Second Stamp / add-mod | Pin only — see `04` |
| Mainnet ownership mint | Cluster locked to `devnet` in Edge config loader |

## Top 10 risks (ranked)

| Rank | Sev | Finding | Where |
|---:|---|---|---|
| 1 | **Critical** | Client-submitted `ram_earned` (and loss penalty) credited to `player_profiles.ram_balance` with no server battle simulation | `profile_apply_pve_battle_outcome` → `profile_apply_pve_profile_lifetime_stats_tx` |
| 2 | **Critical** | Capture accepts client IVs, entangled, stars, HP, capture modifiers — server persists as truth | `profile_capture_wild` / `profile_capture_wild_tx` |
| 3 | **High** | `ownership_grant_event_known` grants allowlisted Known for any JWT caller; only catalog + idempotency — **new keys = unlimited Known** | `ownership_grant_event_known` + SQL atomic |
| 4 | **High** | Progression Known grant accepts client `move_id` / indices with weak server validation of star/mastery entitlement | `profile_grant_progression_ordinal` |
| 5 | **High** | Dual RAM ledgers (Postgres vs on-chain `PlayerProfile`) can diverge; Stamp confirms debit off-chain after on-chain debit | stamp confirm + `grant_ram` / battle path |
| 6 | **High** | PvE battle also trusts client XP/stars/`progression_moves_granted` / forensic fields for progression | same battle outcome path |
| 7 | **Medium** | Stamp modifier entropy is **authority SHA-256 digest**, not MagicBlock VRF (encounter path differs) | `deriveStampResult` |
| 8 | **Medium** | `entitlement_validate_and_grant` lacks JWT profile binding; `manual_grant`/`test`/`ALLOW_UNVALIDATED_*` can write ledger | entitlement function |
| 9 | **Medium** | Ticket/mint desync if on-chain Stamp succeeds but confirm RPC fails (Known may remain unconsumed off-chain until retry) | prepare/confirm + ticket TTL |
| 10 | **Low/Info** | Dev candy `GrantRam` / `GrantKnown` in Editor + `Debug.isDebugBuild` stub inventory — must not ship as authority | `DevCandyMachineInventory`, Editor grants window |

## Hybrid vs on-chain guidance (this project)

| Asset | Recommendation | Why |
|---|---|---|
| **RAM (soft)** | Keep hybrid short-term; **server-author authoritative payouts** before any on-chain migration | Daily farming volume; on-chain every battle is fee-heavy. Cap + receipt model already exists on-chain for grants. |
| **Known (non-NFT)** | Stay off-chain/profile; harden grant auth | Spine intentionally non-NFT until Stamp |
| **Stamped Core** | Keep on-chain (Metaplex Core) | Already atomic consume+mint; migrate entropy to VRF when ready |
| **Bind archives** | On-chain when live | Burns + mint must be atomic; program already models this |
| **Chainling IVs / capture** | **Must leave client** for server/VRF-seeded rolls before Bind | Bound books would mint client-forged IVs otherwise |
| **Items (field)** | Profile + spawn catalog OK | World pickup model is sound if spawn table stays service-role |

**Bottom line:** Do not “put everything on-chain.” Fix **client-trusted faucets** first. On-chain migration helps most for **tradable Core assets and Bind**, and for **result-authority RAM grants with daily caps** — not for every PvE RAM tick.

## Evidence roots

- `Supabase/functions/_shared/ownership_spine.ts` — pins: Known 5, Stamp 75, Second 150, Bind 250, daily cap 100
- `Supabase/migrations/20260919064343_ownership_spine_phase1_authority.sql`
- `Supabase/migrations/20260920010000_ownership_bootstrap_ram_grant.sql`
- `backend/chainbound_game/programs/chainbound_game/src/lib.rs`
- `Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md`, `ECONOMY_01_EconomyAndProgression_v1.md`, `Docs/review-unimplemented/Specs/Economy/OWNERSHIP_SPINE_LIVE_MATRIX_PLAN.md`

## Reviewer notes / access limits

- Static code/SQL/docs only; **no live RPC, no secret material, no production DB queries**.
- Did not execute Solana transactions or Edge Functions.
- Prefab YAML flag values referenced from docs/matrix plan (reported OFF); not re-grepped in this pass beyond gateway serialize field.
- Nested `backend/chainbound_game` program reviewed; BattleService C# backend not exhaustively inventoried for alternate reward paths.

---
*Audit date 2026-09-25. Docs-only deliverable under `Docs/Specs/Security/EconomyAudits/`.*
