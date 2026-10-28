# RAM Shop R2: report for Tasks 1-9 (integrity, exploit and cap fixes)

**Date:** 2026-10-28 (CT). **Branch:** `sync/local-20261020`. Nothing committed, deployed, signed or pushed. Devnet only; no mainnet path added or enabled.
**Scope:** Tasks 1-9 of `RAM_SHOP_R2_CLAUDE_HANDOFF.md`. Tasks 10-13 (USD pricing, USDC/SKR catalog rows, client polish, ToU wording) are not started.

## 1. Files

New migrations (all CRLF; none edits an applied migration):

| File | Purpose |
|---|---|
| `Supabase/migrations/20261028120000_ram_shop_ledger_credit.sql` | Task 2. Settle writes a settled `shop_purchase` credit (operation_id = order_id) with the balance, profile-first locks, no cap writes. Refuses to apply if a settled post-v2 order lacks a ledger row (hosted Devnet had 0 settled orders). |
| `Supabase/migrations/20261028120100_ram_shop_credit_mode.sql` | Task 3. `ram_shop_settings.credit_mode` (`chain_mirror` default, `postgres_only`). Settle requires the credit signature and receipt in `chain_mirror`, accepts NULLs in `postgres_only`, and records the mode in ledger metadata. |
| `Supabase/migrations/20261028120200_quest_reward_daily_cap.sql` | Task 4. `profile_apply_quest_reward_tx` routes quest RAM through the 100/UTC-day cap; new `profile_quest_reward_replay` helper (not client-callable). |
| `Supabase/migrations/20261028120300_ram_shop_signature_poisoning.sql` | Task 5. `UNIQUE (signature)` becomes a partial unique index on verified attempts; `mark_payment_verified` and `mark_order_terminal` updated. |
| `Supabase/migrations/20261028120400_ram_shop_treasury_owner.sql` | Task 7. `ram_shop_treasuries(cluster, owner)`; Devnet-guarded seed with DkFm; corrected payee comment. |
| `Supabase/migrations/20261028120500_ram_shop_tender_mint_pins.sql` | Task 8. `ram_shop_tender_mints` pin table (classic Token, 6 decimals, known mints in CHECKs), composite FK from every token price row, devnet USDC pin seeded. |

Edge (CRLF kept):

| File | Change |
|---|---|
| `Supabase/functions/_shared/ram_shop.ts` | Tasks 3, 5-9: `loadCreditMode` + `settleInPostgres`; fee-payer guard `requireOrderWalletFeePayer` (before anything is recorded); `requiresManualReview`; semantic-verification proposal as comments; treasury helpers (`resolveTreasuryOwner`, `loadTreasuryOwner`, `requireTreasuryDestination`, `checkTreasuryTokenAccount`), treasury-driven prepare and catalog; mint pins (`PINNED_TENDER_MINTS`, `requirePinnedTenderMint`, `checkTenderMintAccount`, `checkPayerTokenAccount`); classic-Token-only verifier; `secretsMatch`. |
| `Supabase/functions/ram_shop_status/index.ts` | Signature lookup reads only the verified attempt. |
| `Supabase/functions/ram_shop_reconcile/index.ts` | Constant-time secret check. It already didn't call `ram_ledger_register_audit` (handoff Task 3 item was already done). |
| `Supabase/functions/profile_apply_quest_reward/index.ts` | Returns `requested_ram`, `applied_ram`, `reason`. |
| `Supabase/functions/_shared/ram_ledger_guard_test.ts` | Known-roll bundles, both shop settle migrations and the quest cap migration added to the reviewed-writer list, each with a content test. |
| `Supabase/functions/_shared/ram_shop_test.ts` | 27 new tests (37 total). |

Note: `Supabase/supabase/functions/_shared/*` are hard links to `Supabase/functions/_shared/*`, so git shows the same diff twice. The canonical folders are `Supabase/migrations` and `Supabase/functions`.

Tests and fixtures: `Supabase/tests/ram_shop_settlement_v1.sql`, `ram_shop_settlement_concurrency_v1.sql`, `ram_shop_credit_mode_v1.sql`, `quest_reward_daily_cap_v1.sql`, `ram_shop_signature_poisoning_v1.sql`, `ram_shop_treasury_v1.sql`, `ram_shop_tender_mints_v1.sql`; fixtures `tests/fixtures/ram_shop_minimal_prerequisites.sql` (real shop tables from migrations) and `tests/fixtures/quest_reward_cap_prerequisites.sql` (item tables plus a stubbed item-delta function).

Unity: `Assets/Scripts/Combat/Runtime/Services/QuestRewardService.cs` gains `requestedRam`, `appliedRam`, `reason` (additive; the existing `Ok(...)` call shape still compiles through optional parameters). `SupabaseRamShopService.cs` needed no change: the credit fields are plain strings and nothing shows a credit receipt.

Docs: `Docs/Specs/Economy/RAM_SHOP_MANUAL_REVIEW_RUNBOOK.md` (Task 6) and this report.

## 2. Test results

**pgTAP** (isolated local `supabase/postgres:17.6.1.063` container, never the hosted project):

| Suite | Result |
|---|---|
| R2 suites on the full chain in timestamp order | settlement 26/26, concurrency 5/5, credit mode 11/11, quest cap 22/22, signature poisoning 12/12, treasury 7/7, tender mints 10/10 (**93/93**) |
| R2 migrations applied a second time | clean (idempotent) |
| Existing RAM v2 suites, with and without the new settle | opening 6, settlement 22, security 20, hardening 26, phase 1.6 21, phase 1.7 25, concurrency 5 (**125/125 both ways**) |
| `known_roll_bundles_v1` (Task 1 migration) | 29/29 |

**Deno** (`--no-prompt`, no network, env access only to named variables, none of which are set):
- `ram_shop_test.ts` 37/37 (previously listed as "needs env": it only needs permission to *read* unset names, so no value is supplied).
- `ram_ledger_guard_test.ts` 9/9.
- Also passing: `known_roll_bundle` 9, `wallet_binding` 4, `solana_pda` 7, `siws` 12, `bindery_foundation` 7, `ownership_bind` 3, `ownership_das_proxy` 9, `pvp_matchmaking` 11, `stamp_host_rules` 9, `solana_rpc_proxy` 6, `ownership_spine` 33, `battle_reward` 4, plus `display_name_charset`, `hub_presence`, `player_display_name`, `known_pool_boss_only` and `pvp_party_boss_only`.
- **Failing, not from R2:** `known_pool_parity` (basePower 100 vs 98), `move_ap_parity` (baseline_barrage AP 3 vs 2) and `ownership_metadata` (`metadata_move_card_missing:baseline_jab`). All three compare against files with uncommitted local changes (`move_ap.ts`, `known_pool.generated.ts`, `ownership_card_catalog.generated.ts`) that R2 didn't touch.
- `deno check` passes for every changed endpoint.

**Not run:**
- **Unity recompile:** the Editor is connected but its main thread didn't answer commands (busy importing RAM bundle sprites), and the last compile in `Logs/Editor.log` predates the `QuestRewardService.cs` edit.
- **Unity EditMode tests:** not run.

**How to re-run pgTAP:** apply, in order, `tests/fixtures/ram_shop_minimal_prerequisites.sql`, `20261003113101`, `20261004045000`, `20261008083113`, the six RAM v2 migrations (`20261008231904` … `20261009143000`), `tests/fixtures/quest_reward_cap_prerequisites.sql`, `20261014030000`, `20261015031400`, then `20261028120000` … `20261028120500`. Apply them as `supabase_admin` with `chainbound.cluster = 'devnet'` set on the database. Then run each suite.

## 3. Exploit and cap cases → proving tests

Each case fails before its fix and passes after (checked by running the suite without, then with, the migration or code change).

| Case | Tests |
|---|---|
| T1 bundle debit backed by ledger | `ram_ledger_guard_test`: "known roll bundles debit RAM through one settled ledger operation per pull" |
| T2 purchase writes ledger row; audit balanced | `ram_shop_settlement_v1` 3-5, 13, 19 (8 failures before the fix) |
| T2 double / concurrent settle credits once | `ram_shop_settlement_v1` 6-9; `ram_shop_settlement_concurrency_v1` 4-5 |
| T2/D2 purchases never touch the cap; buy N then earn 100 | `ram_shop_settlement_v1` 10-12, 14-18 |
| T3 chain_mirror can't settle without the chain credit | `ram_shop_credit_mode_v1` 3-5; Deno "chain_mirror never settles in Postgres before the on-chain credit" |
| T3 postgres_only: no chain write, settles once under confirm + status + reconcile | Deno "postgres_only settles from the verified payment with no chain write" and "postgres_only: concurrent confirm, status and reconcile settle exactly once"; `ram_shop_credit_mode_v1` 7-10 |
| T4 quest cap 0/90/100, retry, items still processed | `quest_reward_daily_cap_v1` 1-15 (17 failures before the fix) |
| T4 quest + battle ≤ 100; same-day purchase changes neither | `quest_reward_daily_cap_v1` 16-20 |
| T5 signature poisoning (terminal and submission paths) | `ram_shop_signature_poisoning_v1` 2-3 and 7 (before the fix: `signature_already_consumed`); Deno "signature poisoning: another wallet's payment is refused before it touches the order" |
| T5 a signature can't verify two orders | `ram_shop_signature_poisoning_v1` 8-10 |
| T6 wallet-edited tx → manual_review (documents current policy) | Deno "wallet-modified tx: …" (3 tests) |
| T7 SOL to non-treasury, wrong-owner ATA, PDA owner ATA, env/DB mismatch 503, old orders still verify | Deno "treasury: …" (7 tests); `ram_shop_treasury_v1` |
| T8 wrong or lookalike mint, wrong decimals, Token-2022, missing/short ATA, correct USDC accepted | Deno "spl: …" (8 tests); `ram_shop_tender_mints_v1` 3-9 |
| T9 constant-time reconciler secret | Deno "reconciler secret: …" (2 tests) |

## 4. Steps left for Cronus

1. **Before deploying the new Edge code**, set the Edge secret `RAM_SHOP_TREASURY_OWNER=DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG`. Without it, catalog and prepare fail closed with `treasury_owner_not_pinned`, which turns off Devnet SOL checkout.
2. **Apply migrations** `20261028120000` … `20261028120500` to hosted Devnet, in order. Each has a guard or is idempotent; `120000` and `120500` refuse to apply if settled orders without ledger rows, or token price rows, appeared since 2026-10-28.
3. **Deploy functions** `ram_shop_catalog`, `ram_shop_prepare`, `ram_shop_confirm`, `ram_shop_status`, `ram_shop_reconcile` and `profile_apply_quest_reward`. They share `_shared/ram_shop.ts`.
4. **Treasury move (D5):**
   - Create the vault `Hkn8eVNs11wFKvGKGk3GZqo7WA29bYn3mroWNDj8gdaL`'s Devnet USDC ATA (any fee payer, owner = vault, `allowOwnerOffCurve`).
   - Supply the SKR test mint (a new `ram_shop_tender_mints` devnet SKR row) and create its ATA.
   - A follow-up migration then sets `ram_shop_treasuries.devnet` and the SOL price destinations to the vault, and `RAM_SHOP_TREASURY_OWNER` changes in the same window.
   - Orders quoted before the switch still verify against their stored DkFm destination.
5. **Flip `credit_mode` to `postgres_only` only after the spend switch** below. Until then keep `chain_mirror`: stamp, bind and rebirth still read on-chain `profile.ram_balance`, so a Postgres-only purchase would leave a buyer unable to spend it there.
6. **Veto window** for Proposal D1 (credit_mode) and D5 (separate treasury) before merge.
7. **Seeker device test** for wallet-modified transactions (E3), to decide between exact match plus the runbook and the semantic verifier sketched in `ram_shop.ts`.

### Spend switch (Task 3 dependency; written up, not built)

These go in order (RAM_00 §5):

1. **Rebirth** (`profile_rebirth_chainling/index.ts:150-165`): drop `ensureRamReservationOnChain` and `debitReservedRamOnChain`, and debit through the Postgres reserve/settle-spend functions (`ram_ledger_reserve_operation` + `ram_ledger_settle_spend`). Edge only.
2. **Stamp** (`ownership_prepare_stamp/index.ts:211`) and **bind** (`ownership_prepare_bind/index.ts:143`): replace the on-chain reservation with a Postgres hold; confirm/reconcile settle the hold on the receipt and release it on an expired blockhash. The program itself checks `profile.ram_balance` in stamp (~lib.rs:874) and bind (~lib.rs:1093), so this half needs a **program upgrade (Squads proposal)** that removes or bypasses those checks.
3. Then flip `credit_mode`.

I didn't build step 1 here: it changes a live spend path outside the shop, so it deserves its own task and device test.

## 5. Contradictions and judgment calls

- **Treasury in the verifier (Task 7).** The handoff asks the verifier to require destination = treasury owner, *and* for old orders to keep verifying against their stored destination. Those conflict once the treasury moves. I enforce the treasury at prepare and catalog time, and the verifier checks the order's stored destination. That destination was itself treasury-checked at quote time.
- **Foreign-fee-payer transactions (Task 5).** These are now refused with `payment_not_from_order_wallet` and leave the order untouched, instead of moving the attacker's order to `manual_review`. That's option (b) plus (a), as the handoff preferred.
- **Quest reason codes (Task 4).** Beyond `daily_cap`, the response reports `applied`, `daily_cap_partial` and `no_ram`. A replay reports what the first application actually credited (read back from its ledger row). I think a capped one-time quest should stay "reduced, not deferred", as built. Deferral would need a pending-credit table and isn't worth it for a 15-RAM side quest.
- **Manual-review settle gap (Task 6).** No operator RPC can settle a `manual_review` order today, because `mark_payment_verified` only accepts quoted or submitted orders. The runbook gives that RPC's contract; until it exists, refund is the only remedy. Worth building before real money flows.
- **Supabase/supabase mirror.** It holds a stale partial migration copy (24 files vs 117) and hard-linked `_shared` files. It isn't the deploy source for these migrations; confirm the CLI deploys from `Supabase/` before applying.
