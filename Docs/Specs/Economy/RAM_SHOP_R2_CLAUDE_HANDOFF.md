# Claude Code Handoff: RAM Shop R2 (harden, then USD-priced SOL/USDC/SKR on devnet)

> Paste everything below the line into Claude Code, opened at the repo root
> `C:\Users\mando\Downloads\UNITYGAMING\Signal Jam\Signal Jam 3D` (branch `sync/local-20261020`).
>
> **Provenance:** the repo docs plan only part of this work. `Docs/review-unimplemented/Guides/RAM_00_Immediate_Battle_Reward.md` plans the shop credit switch to Postgres (§0, §2(c), §5 L194/L203, §6 step 4). No doc plans USDC/SKR, USD pricing or the exploit fixes. This handoff is based mainly on a read-only audit dated 2026-10-28 (CT), and is a proposal for Cronus to approve.
>
> **Revision 2026-10-28 (CT):** Cronus answered the six open questions. His answers are recorded in "Decisions" below, and the tasks were updated to match. Items marked **Proposal** are recommendations Cronus hasn't approved yet; build them as written unless a task says to stop.

---

You are continuing the Chainbound RAM shop. SOL checkout works on devnet. The goal is to close the integrity and exploit holes **first**, then move to fixed USD bundle prices paid in SOL, USDC or SKR, still **devnet only**.

## Decisions (Cronus, 2026-10-28)
These answer the former open questions. Don't stop to ask about them again.

**D1. Shop credit: Postgres only, no on-chain `credit_purchased_ram` (Proposal: Cronus may veto before merge).**
- Cronus asked to keep it on-chain only if that helps production readiness or security. The code says it doesn't:
  - `credit_purchased_ram` (`lib.rs:321-347`, accounts :1683-1710) is signed by `GameConfig.result_authority`, a **server hot key** held by the Edge function, not by Squads. The amount is unbounded and goes through `apply_uncapped_ram_credit` (:3257). An on-chain credit therefore proves only that the hot key said so. It is not independent verification.
  - It costs a fee-paid tx per purchase and adds a second failure point after the player has paid.
- Postgres is already canonical (RAM_00 §0), and the real proof of a purchase is the player's own finalized payment tx. That tx carries the per-order reference key and the exact amount, mint and payee, and its signature is stored on the order.
- The `RamPurchaseReceipt` PDA exists only because of the credit instruction, so it goes away with it. No memo or other on-chain write is needed.
- **Coupling you must handle:** on-chain RAM is still *spent*.
  - Stamp and bind call `ensureRamReservationOnChain` (`ownership_prepare_stamp/index.ts:211`, `ownership_prepare_bind/index.ts:143`).
  - Rebirth calls `ensureRamReservationOnChain` + `debitReservedRamOnChain` (`profile_rebirth_chainling/index.ts:150-165`).
  - The program checks `profile.ram_balance` in `create_ram_reservation`, `debit_reserved_ram`, stamp and bind.
  - v2 battle and quest credits are already Postgres-only, so the on-chain mirror already lags Postgres. The spend switch in RAM_00 §5 is needed either way.
  - Stamp/bind changes that touch the program need an upgrade, which is a Squads step.
  - See Task 3 for the safe sequencing.

**D2. Purchased RAM stays outside the daily earn cap.** A purchase never reads or writes `ram_daily_earnings`. After buying N RAM, the player can still earn the full 100/day cap (Task 2 tests).

**D3. Quest rewards count toward the daily earn cap.**
- Today they don't. `profile_apply_quest_reward_tx` (`20261014030000_fisher_ape_lucky_crayon.sql` :91, credit :207-213) adds 15 RAM without touching `ram_daily_earnings`, and no later migration redefines it.
- Task 4 fixes this, using the RAM_00 §3 rule: applied = `min(requested, 100 - earned)`, with a zero result `daily_cap` at 0.

**D4. Fixed USD price per bundle; the token amount comes from USD price feeds at quote time.**
- Each bundle has one USD price. At quote time the server converts it to SOL, USDC or SKR using a server-side feed, locks that amount in the order, and settles only against the locked amount.
- Missing, stale or low-confidence price data fails closed. Final USD prices are a Cronus input (Task 10).

**D5. Treasury: use a separate Squads treasury multisig (Proposal).**
- Recommended setup: two of the existing member wallets plus a new cold wallet, owning SOL and the USDC/SKR ATAs.
- A second vault index on the current upgrade multisig is weaker. It shares the same members, threshold and config authority, so one compromise (or a bad approved proposal) reaches both program upgrades and revenue.
- A separate multisig also allows a different threshold or time lock later.
- The payee becomes config-driven (Task 7). It stays `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG` on devnet until Cronus provides the new vault address. Creating the multisig and ATAs is a Cronus step.
- **Devnet treasury multisig created 2026-10-28** with `tools/vrf-efficacy/squads.cjs create-multisig` (memo "chainbound devnet ram shop treasury"). The throwaway `Ei1HGT9c…` paid and has no role. Read back from chain:

  | Field | Value |
  |---|---|
  | Multisig account | `BLrr7egH9EDsUL1kufZpr8o9q2jy5D9DSCfrAbmfSCap` |
  | Vault (index 0), the treasury owner / payee | `Hkn8eVNs11wFKvGKGk3GZqo7WA29bYn3mroWNDj8gdaL` |
  | Create tx (finalized, slot 516,133,476) | `62raoDAdkJA4aNmEJthSqwtYi5Td6WqVXqBLp9QQCi5EcveDH84jQvsmKJKn4qURwkPZ5zygEjtN9o17dF4DXPPX` |
  | Settings | 2 of 3, time lock 0, config authority none |
  | Members (all Initiate, Vote, Execute) | Seeker Seed Vault `9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj` (shared with the upgrade multisig), treasury Phantom `FVpLF14N4n98NbKCZQr1NUhXX223tqHZv6y5WTNfYefv`, treasury offline wallet `DNyvuzdA9QXSoJr68EWadc39X3M47ctL1aeBgskpMhTw` (recovery only) |

  Still to do: the vault's devnet USDC and SKR-test ATAs, then the Task 7 migration moving the devnet payee from DkFm to `Hkn8…gdaL`. Mainnet needs its own treasury multisig (Squads app), with a time lock decided then.

**D6. Update the Terms of Use for USD pricing and token tenders.** Draft the wording only (Task 13). Don't edit `Docs/Legal/`; legal review and the edit are Cronus's.

## Read first (treat as the plan of record where they apply)
1. `Docs/review-unimplemented/Guides/RAM_00_Immediate_Battle_Reward.md`. Key sections:
   - the decision record in §0;
   - "Buy RAM" in §2(c);
   - the daily cap in §3, where purchases are excluded and battle and quest count;
   - the Edge changes for the shop, stamp, bind and rebirth in §5;
   - the migration order in §6.

   Note §11: "RAM shop catalog/pricing changes" was out of scope for RAM_00. The USD pricing and USDC/SKR rows here are new scope that Cronus has requested.
2. `Supabase/functions/_shared/ram_shop.ts`. It covers prepare :216-370, confirm :384-421, settlement :476-581, the verifier :583-722 and the receipt reader :724-748.
3. Migrations:
   - `Supabase/migrations/20261003113101_ram_shop_unified_ledger.sql` (bundles, tender prices, orders)
   - `20261008083113_ram_shop_r01_devnet_hardening.sql`
   - `20261009010622_ram_v2_postgres_canonical_earn.sql` (cap table + `ram_v2_audit_service` :285-315)
   - `20261009143000_ram_v2_pre_rollout_hardening.sql` (current `ram_shop_settle_purchase` :568-617; battle capped path ~:994/:1141)
   - `20261004045000_devnet_seeker_ram_shop_catalog.sql`
   - `20261014030000_fisher_ape_lucky_crayon.sql` (quest reward)
4. Client: `Assets/Scripts/UI/MenuNavigation/RamShop/RamShopScreen.cs`, `RamBundleCardPresenter.cs`, `Assets/Scripts/Combat/Runtime/Services/RamShop/*`.
5. Program (read only, no changes): `backend/chainbound_game/programs/chainbound_game/src/lib.rs` `credit_purchased_ram` :321-347.

## Hard rules
- **Devnet only.** Don't add or enable any mainnet path. Keep `RAM_SHOP_CLUSTER=devnet`, the genesis-hash pin and the program-id pin as they are.
- **No signing, deploying, committing or pushing.**
  - Don't run `supabase db push`, `supabase functions deploy`, `anchor deploy`, `solana program …`, or `git commit/push`.
  - Call no mutating RPC. Read-only JSON-RPC GETs against devnet are fine.
- **Never read** `.env*`, keypair, wallet or `*.json` key files, and don't print secrets.
  - If a test needs `SUPABASE_URL` or keys, **don't** supply them. List the test as "needs env" in your report.
  - The same goes for `PYTH_API_KEY`: never supply, request or hard-code it. Tests use mocks.
- **Never touch** `Docs/GameTheory/`, `Docs/Legal/`, `AgentScripts/`, `Supabase/supabase/.temp/`, or `.claude/worktrees/`.
- **On-chain config:** any change to on-chain config or authority (program config, treasury, ATAs, authorities) goes to Cronus as a **Squads proposal step** via `tools/vrf-efficacy/squads-signer` (localhost:8787). Write the exact step for him; don't run it. The program/upgrade authority is the Squads 2-of-3 vault.
- **Keep CRLF line endings** in every file you edit or create. Check with `git diff --stat` and make sure there are no whole-file churn diffs.
- Every DB change goes in a **new** migration file with a devnet guard, following the existing pattern. Never edit an applied migration.
- **Order matters:** finish Tasks 1-9 (integrity, exploit and cap fixes) and get their tests green before Task 10 (USD pricing) and Task 11 (new currencies).
- **Every exploit case needs a test that fails before your fix and passes after.**
- Don't invent scope. The former open questions are decided above; follow the Decisions. Stop and report only if:
  - a task needs one of the "Cronus inputs still needed" listed below, or
  - the code contradicts a Decision.

  For a missing input, build with the labelled placeholder and keep the feature disabled.

## Tasks (in order)

### Task 1: fix the red ledger guard test
`deno test --no-prompt --allow-read=.. _shared/ram_ledger_guard_test.ts`, run from `Supabase/functions`, currently fails. `20261027120000_known_roll_bundles.sql` (~L245, `SET ram_balance = ram_balance - …`) isn't in the allowed-writer list (`ram_ledger_guard_test.ts:7-13`).
- Confirm that the debit is backed by a settled `ram_ledger_operations` row in the same transaction. If it is, add the migration to the reviewed list with a comment. If it isn't, **don't whitelist it**; report it as a finding.

### Task 2: purchases must write a `shop_purchase` ledger op (High) (D2)
`ram_shop_settle_purchase` (v2 hardening :568-617) increments `player_profiles.ram_balance` without a ledger row, so `ram_v2_audit_service` reports buyers as unbalanced.
- New migration: CREATE OR REPLACE `ram_shop_settle_purchase`.
  - In one transaction, insert a settled credit op (kind `shop_purchase`, idempotency key = order_id) and update the balance. First confirm that any `operation_kind` constraint accepts `shop_purchase`.
  - Keep the `FOR UPDATE` locks, idempotency, and the u32 guard.
  - Never read or write `ram_daily_earnings` (D2).
- Tests: new pgTAP `Supabase/tests/ram_shop_settlement_v1.sql` (reuse `tests/fixtures/ram_v2_minimal_prerequisites.sql`). It should cover:
  - The audit is balanced after a purchase.
  - A double settle is a no-op.
  - A concurrent double settle credits once.
  - The daily cap row is unchanged: no row created and `earned` unchanged.
  - A purchase on a profile already at 100 earned still credits.
  - **Buy N, then earn (D2):** after a purchase of N, battle settlements (and quest rewards once Task 4 lands) still apply the full 100 for that UTC day. The final balance = N + 100.
  - **Purchases never touch the cap counter (D2):** several purchases in one day leave `ram_daily_earnings.earned` exactly as it was.

### Task 3: drop on-chain `credit_purchased_ram` from shop settlement (D1; Proposal, Cronus may veto before merge)
Not gated: build it now. Mark it in your report as "Proposal D1: Cronus may veto before merge".
- **Sequencing guard.** While stamp/bind/rebirth still require on-chain `profile.ram_balance`, a buyer whose purchase isn't mirrored on-chain could hit `InsufficientRam` there. To keep that from happening:
  - Add a shop setting in a new migration, `credit_mode` (`'chain_mirror'` | `'postgres_only'`).
  - Default it to `'chain_mirror'` (today's behavior). `'postgres_only'` skips the chain write.
  - Cronus flips the setting after the spend switch below. Don't flip it yourself.
- In `ram_shop.ts` `advancePurchaseSettlement` (:476-581), when `credit_mode='postgres_only'`:
  - go from verified payment straight to the Task 2 settle, with no chain write, no `config.resultAuthority` signing and no receipt read;
  - in a new migration, let `ram_shop_settle_purchase` accept NULL `p_credit_signature` / `p_receipt_address` in that mode;
  - keep the payment signature and the reference address on the order as the purchase proof, and return them in status.
- Client: `SupabaseRamShopService.cs:100-101,224-225` reads `credit_signature` and `credit_receipt_address`. Make sure NULLs are handled. If the UI shows a receipt link, show the payment signature instead.
- In `ram_shop_reconcile/index.ts`, stop calling `ram_ledger_register_audit` and keep order recovery. Remove the now-dead receipt code only if nothing else imports it.
- **Spend switch (dependency, scope it but don't over-build):** list exactly what RAM_00 §5 requires so stamp, bind and rebirth gate on Postgres instead of on-chain RAM.
  - Rebirth fully off-chain: drop `ensureRamReservationOnChain` / `debitReservedRamOnChain` in `profile_rebirth_chainling/index.ts:150-165` and use a Postgres debit.
  - Stamp and bind: use a Postgres hold and no on-chain reservation. Where the program itself checks `profile.ram_balance` (stamp ~L874, bind ~L1093), that's a program upgrade, which is a Squads step for Cronus.
  - Implement the Edge-only parts if they fit RAM_00 §5 exactly; otherwise write them up as the next task. Either way, report the order to flip `credit_mode`.
- Tests (Deno, mocked RPC/DB):
  - Concurrent confirm + status + reconcile settle exactly once in both modes.
  - In `'postgres_only'` mode, no transaction is built or signed with the result authority.

### Task 4: quest rewards must go through the daily cap (D3, new)
- New migration: CREATE OR REPLACE `profile_apply_quest_reward_tx(UUID, TEXT)`. Keep its signature, idempotency per profile+reward_id, and the item/orb handling. Route the RAM through the same capped logic as `ram_battle_reward_settle_service`, sharing a helper if that's clean:
  - Lock order: `player_profiles` → quest reward row → `ram_daily_earnings` → `ram_ledger_operations` (RAM_00 §3 profile-first rule).
  - Use the UTC day from the DB clock.
  - applied = `min(requested, 100 - earned)`. When applied > 0, write the ledger op `quest_reward` with requested/applied in metadata, update `ram_daily_earnings`, then update the balance.
  - When applied = 0, write no ledger row (the ledger CHECK requires amount > 0) and return reason `daily_cap`.
  - Return `requestedRam`, `appliedRam`, `reason` and `balanceAfter`. Update `Supabase/functions/profile_apply_quest_reward/index.ts:29` and any Unity reader of the response to show the applied amount.
  - Proposal (RAM_00 §8 "revisit if quests feel punished"): a capped one-time quest reward is reduced, not deferred. If you think deferral is better, note it in your report; don't build it.
- Add the new migration to the ledger guard's reviewed list.
- Tests (pgTAP):
  - quest at 0 earned → 15 applied, cap row = 15;
  - at 90 → 10 applied;
  - at 100 → 0 with `daily_cap`, items still processed;
  - a retry is idempotent;
  - quest + battle together never exceed 100;
  - a purchase on the same day changes neither (D2);
  - the audit stays balanced.
- Other earn paths found uncapped (report them; don't change them unless listed here):
  - shop purchase (uncapped by design, D2);
  - `opening_balance` migration ops (one-time v2 migration);
  - operator `ram_reconciliation_settle` (`20261006110545` ~:116, service_role; RAM_00 retires operator reconcile);
  - on-chain `credit_purchased_ram` (uncapped, result authority; removed from the shop by Task 3).
  - The devnet smoke-grant and top-up DB writers are revoked from service_role or return 410. Confirm they're unreachable.
  - On-chain `burn_known_move_for_ram` (lib.rs:489) is capped on-chain and has no Edge caller.

### Task 5: block signature poisoning (Medium)
**Attack:**
1. The attacker creates their own quoted order and calls `ram_shop_confirm` with a victim's public finalized payment signature.
2. The mismatch goes to `ram_shop_mark_order_terminal` (r01 :165-171), which stores `(attacker_order, victim_sig)` in `ram_payment_attempts` (UNIQUE signature).
3. The victim's `ram_shop_mark_payment_verified` then raises `signature_already_consumed` (r01 :98-104). The victim paid and is never credited.

- Fix options; pick one and justify it:
  - (a) Make signature uniqueness apply only to `status='verified'` (partial unique index). Failed observations can't block. Also update the `ON CONFLICT` targets.
  - (b) Only record an observed signature on an order when the tx fee payer equals that order's wallet.
  - Prefer (a)+(b) together.
- Tests: pgTAP and Deno. The victim settles after an attacker's poison attempt. The same signature still can't be **verified** for two orders.

### Task 6: wallet-modified transactions (Medium)
The verifier requires exact message bytes (`ram_shop.ts:615-626`). A wallet that adds priority-fee or guard instructions sends the order to `manual_review` after the player has paid, and no refund tool exists.
- Don't loosen anything silently. Write tests that document current behavior: extra ix, changed CU price.
- Propose a semantic-verification option in code comments, not merged logic unless trivial and safe:
  - the transfer and the reference are each present exactly once;
  - only ComputeBudget or allow-listed extra instructions;
  - the same fee payer.
- Draft `Docs/Specs/Economy/RAM_SHOP_MANUAL_REVIEW_RUNBOOK.md`, covering how an operator verifies and settles or refunds a `manual_review` order. Refunds are Cronus-signed steps.

### Task 7: config-driven treasury payee (D5)
The payee is currently the per-row `destination` in `ram_shop_tender_prices` (seeded with DkFm at `20261004045000_devnet_seeker_ram_shop_catalog.sql` L77). `verifyMintAndDestination` (`ram_shop.ts:701-722`) checks the token program and mint but not the owner.
- New migration: a per-cluster treasury owner setting (for example `ram_shop_settings.treasury_owner`, or a small `ram_shop_treasuries(cluster, owner)` table). Set the devnet value to `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG` (unchanged until Cronus provides the new vault).
- Edge: read it from config, not code.
  - Also pin it with an Edge env var `RAM_SHOP_TREASURY_OWNER`. If the env value and the DB value disagree, checkout returns a typed 503.
  - Don't set the env var yourself; list it as a Cronus step.
- At prepare and in the verifier:
  - **SOL:** the destination must equal the treasury owner. A Squads vault PDA is a system-owned account, so SOL is sent to the vault address itself.
  - **USDC/SKR:** the destination must equal `getAssociatedTokenAddressSync(mint, treasuryOwner, /*allowOwnerOffCurve*/ true, tokenProgram)`. Also check that the parsed token account's **owner** equals the treasury owner, the mint matches, and the account is initialized and not frozen.
  - Any mismatch disables that tender (typed error). Never fall back to the row's destination.
- Fix the stale payee comment ("Destination is the devnet upgrade authority", catalog seed L3) in the new migration's header comment, not by editing the applied migration. Correct text: "the payee is the configured treasury owner; devnet currently DkFm…, a hot wallet".
- Tests (Deno + pgTAP):
  - SOL to a non-treasury destination is rejected;
  - an ATA for the wrong owner is rejected;
  - an off-curve (PDA) owner ATA is accepted;
  - an env/DB mismatch returns 503;
  - an order created before the treasury changes still verifies against its stored destination.
- Cronus steps (write them out; don't run them):
  1. Create the treasury multisig in Squads (two existing members + a new cold wallet; threshold Cronus's choice, 2-of-3 proposed).
  2. Read its vault PDA (index 0).
  3. Create the vault's devnet ATAs for the USDC devnet mint and the SKR test mint. ATA creation is permissionless (any fee payer, owner = vault PDA), but Cronus runs it.
  4. Provide the address. You then write a new migration that updates the devnet treasury owner and destinations, and Cronus sets `RAM_SHOP_TREASURY_OWNER`.

### Task 8: SPL safety before enabling tokens (Medium)
In `ram_shop.ts` (:299-319, :681-722) and a new CHECK migration on `ram_shop_tender_prices`:
- Pin mints per cluster:
  - USDC mainnet `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v`
  - USDC devnet (Circle) `4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU`
  - SKR mainnet `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3` (already pinned)
  - SKR devnet: a config value Cronus supplies, since SKR doesn't exist on devnet
- The destination and owner checks come from Task 7.
- For USDC/SKR, allow only the classic SPL Token program (`Tokenkeg…`). Both are classic SPL Token with 6 decimals, verified on-chain 2026-10-28. Reject Token-2022 mints that have transfer-fee or transfer-hook extensions.
- A missing source ATA or insufficient balance returns a clean, typed error (no 500).
- Tests (Deno, in `_shared/ram_shop_test.ts` style): wrong mint, lookalike mint, wrong decimals, destination owned by another wallet, Token-2022 fee mint rejected, missing ATA error, correct USDC transfer accepted.

### Task 9: small hardening
- Constant-time compare for `x-reconciler-secret` (`ram_shop_reconcile/index.ts:11`), plus a test.
- (The stale payee comment fix moved to Task 7.)

### Task 10: fixed USD bundle prices + server-side price feeds (D4; after 1-9 are green)
**Schema (new devnet-guarded migration):**
- Add `ram_shop_bundles.usd_price_cents INTEGER` (CHECK > 0).
  - **Placeholders:** `ram_5` = 50, `ram_20` = 200, `ram_100` = 1000.
  - Mark them `TODO(Cronus): final prices`. They are the USD equivalents of today's 0.005/0.02/0.1 SOL at ≈$100.9/SOL on 2026-10-28 (CoinGecko/Jupiter).
  - $0.49/$1.99/$9.99 is an alternative. Don't present any of these as final.
- Add a shop setting `pricing_mode` (`'fixed_atomic'` | `'usd_feed'`), default `'fixed_atomic'`. The existing SOL rows keep working until Cronus switches.
  - In `'usd_feed'` mode, `ram_shop_tender_prices` still decides which tenders are enabled, plus their mint, decimals, program and destination. The stored `atomic_amount` is not used for the quote.
- Add a feed config table `ram_shop_price_feeds(cluster, tender, source, feed_id, max_age_seconds, max_conf_bps, min_price, max_price, enabled)`. Proposed devnet values:
  - SOL: `pyth`, feed `ef0d8b6fda2ceba41da15d4095d1da392a0d2f8ed0c6c7bc0f4cfac8c280b56d`, max age 60 s, conf ≤ 100 bps (Proposal).
  - USDC: `pyth`, feed `eaa020c61cc479712813461ce153894a96a6c00b21ed0cfc2798d1f9a9e9c94a`, max age 60 s, conf ≤ 100 bps, depeg bound 0.98-1.02 USD; outside it, fail closed (Proposal).
  - SKR on devnet: `mock_fixed` (the SKR test mint has no market), with a fixed test price configured in the row. Mainnet later: `pyth` feed `38846ec4d0dbe808091817f5c0d6ab8058e25422348ddf97db52b6c378a93bf9` (Crypto.SKR/USD), conf ≤ 200 bps (Proposal); no mainnet rows now.
- Add snapshot columns to `ram_purchase_orders`: `usd_price_cents`, `price_source`, `price_feed_id`, `feed_price`, `feed_expo`, `feed_conf`, `feed_publish_time`, `priced_at`.

**Edge (`_shared/price_feeds.ts`, server-side only, never client-supplied):**
- Interface: `getUsdPrice(cluster, tender) → { price: bigint, expo, conf: bigint, publishTime, source, feedId }`.
- Adapters:
  - `PythHermesAdapter` (`GET /v2/updates/price/latest?ids[]=…`). Hermes needs an API key since 2026-08-26: send `Authorization: Bearer ${PYTH_API_KEY}` to `hermes.pyth.network` or `pyth.dourolabs.app/hermes`. The key is an Edge secret Cronus sets.
  - Optional `PythPushAccountAdapter` for SOL/USDC. It reads Pyth's sponsored price-feed accounts over RPC, so it needs no key. Shard-0 accounts, verified on devnet and mainnet on 2026-10-28: SOL `7UVimffxr9ow1uXYxsr4LHAcV58mLzhmwaeKvJ1pjLiE`, USDC `Dpw1EAVrSB1ibxiDQyTAW6Zip3J4Btk2x4SgApQCeFbX`. Check the account owner is the Pyth Receiver `rec5EKMGg6MxZYaMdyBfgwp4d5rB9T1VQH5pJv5LtFJ` and the feed id matches. SKR has **no** sponsored account (its PDA is missing on both clusters), so SKR is Hermes-only.
  - `MockFixedAdapter`, allowed only when `cluster='devnet'`, for the SKR test mint and for tests.
- **Fail closed.** Return a typed 503 `price_unavailable` and create no order when any of these is true:
  - the adapter or network errors;
  - the feed id doesn't match the configured one;
  - price ≤ 0;
  - `now - publishTime > max_age_seconds`;
  - `conf/price > max_conf_bps`;
  - the price is outside min/max (the USDC depeg bound);
  - the setting is missing.
- **SKR fallback (Proposal):**
  - Pyth SKR/USD is primary.
  - Jupiter Price API v3 (`lite-api.jup.ag/price/v3?ids=SKRbvo…`, ≈$0.0143, liquidity ≈$612k on 2026-10-28) is only a sanity cross-check. If it deviates from Pyth by more than 3%, fail closed.
  - Never price from Jupiter alone. If Pyth SKR is unavailable, the SKR tender is disabled.
- **Quote math:** `atomic = ceil(usd_price_cents × 10^decimals × 10^(-expo) / (price × 100))` in BigInt, rounding **up**. Store the snapshot on the order.
- **Quote lock:** in `'usd_feed'` mode, the quote expires at `min(quote_ttl_seconds, 120)` and before the blockhash expiry (Proposal: 60-120 s). After expiry, the client must request a new quote.

**Settlement:** verify only against the order's stored `payment_atomic_amount`, destination and message. It already does this; keep it that way. Confirm, status and reconcile must **never** call a price adapter.

**Tests (Deno, mocked):**
- stale, missing, wide-conf, depegged and zero prices each create no order;
- the feed id mismatch is rejected;
- the rounding is correct per decimals (9 for SOL, 6 for USDC/SKR);
- the mock adapter is refused off devnet;
- the snapshot is stored;
- an expired quote is rejected;
- **settlement with a mock adapter that throws if it's called still settles at the locked amount**;
- `'fixed_atomic'` mode is unchanged.

### Task 11: devnet USDC/SKR catalog rows (after 1-10 are green)
- New devnet-guarded migration: USDC and SKR tender rows for ram_5/ram_20/ram_100, **inserted disabled with `verified_at` NULL**.
  - The destination is the ATA of the configured treasury (Task 7).
  - The SKR mint is a `TODO(Cronus)` config value.
  - In `'fixed_atomic'` mode, `atomic_amount` is a placeholder marked `TODO(Cronus)`.
- Write the Cronus steps:
  - Create the treasury ATAs on devnet (Task 7).
  - Supply the SKR devnet test mint.
  - Set final USD prices.
  - Set `PYTH_API_KEY` (if Hermes is used) and `RAM_SHOP_TREASURY_OWNER`.
  - Switch `pricing_mode` and enable/verify the rows.
  - Any treasury or authority change goes as a Squads proposal via `tools/vrf-efficacy/squads-signer` (localhost:8787).

### Task 12: client
`RamShopScreen.cs`:
- Make the tender-aware copy replace the SOL-only strings at :212 and :237.
- Show the bundle's USD price, plus the locked token amount after quoting, with a "quote expires" state.
- Add errors for "no USDC/SKR token account", "insufficient balance", "price unavailable" and "quote expired".
- Format token amounts with decimals and the token symbol (not only the mint, :561).
- Handle NULL credit signature and receipt fields (Task 3).
- Add or extend EditMode tests in `Assets/Scripts/UI/MenuNavigation/Editor/Tests/` (tender switching, price display, missing price → disabled, quote expiry).

### Task 13: Terms of Use wording, for Cronus/legal review (D6, doc only)
Don't edit `Docs/Legal/TERMS_OF_USE.md`. Put the wording below in your report as a "Cronus/legal step". It is a **Proposal** for Cronus and legal review, not final text. Current lines: L107 "What RAM is", L117 "Paying with SOL", L119 the prices line, L122 the SOL value line; L111 and L180 also mention SOL.
- L107 proposed: "**What RAM is.** "**RAM**" is Chainbound's in-game currency. You may earn RAM by playing (subject to a daily earn cap; RAM you buy does not count toward or reduce that cap) or buy RAM bundles in the in-game RAM shop. Bundles are priced in US dollars and paid in SOL, USDC or SKR through a wallet transaction. Other in-game purchases and Solana transactions or mints may also be offered."
- L117 heading proposed: "**Paying with SOL, USDC or SKR.**"
- L119 proposed: "Each RAM bundle has a price in US dollars, shown in the Game before you buy. You pay in the token you choose (SOL, USDC or SKR), not in US dollars. At checkout we convert the US dollar price into a token amount using third-party price data and lock that amount for a short time. Your wallet shows the exact token amount before you approve the Solana transaction. If the quote expires, or price data is unavailable, the purchase won't go through and you can request a new quote."
- L122 proposed: "Token prices change constantly. The locked token amount is final for that purchase. We don't adjust it for price changes before or after your purchase, and we are not responsible for those changes or for errors or delays in third-party price data."
- For legal review: L111 and L180 (other SOL mentions); whether to add a clause about clearly mispriced quotes; and stablecoin wording for USDC. `Docs/Legal/PRIVACY_POLICY.md` L80 ("recorded on-chain") stays true, because the payment tx is on-chain even without the on-chain credit.

## Cronus inputs still needed (don't stop; use placeholders and keep features disabled)
1. Final USD price per bundle (placeholders: 50/200/1000 cents).
2. The SKR devnet test mint, and its mock USD price.
3. ~~The treasury multisig vault address~~ (done: `Hkn8eVNs11wFKvGKGk3GZqo7WA29bYn3mroWNDj8gdaL`) and its devnet ATAs (DkFm stays until the Task 7 migration).
4. `PYTH_API_KEY` as an Edge secret, if Hermes is used.
5. Legal sign-off on the Task 13 wording.
6. Whether to veto Proposal D1 or D5 before merge, and when to flip `credit_mode` (after the spend switch).

## How to run tests (no secrets)
- Deno, from `Supabase/functions`: `deno test --no-prompt --allow-read=.. <test files>`. If a test asks for `SUPABASE_URL`, `PYTH_API_KEY` or other keys, don't grant it; list it.
- pgTAP: only if Docker is running, against an isolated local container as in RAM_00 §9. Never against the hosted project.
- Unity EditMode: only if the Editor is available. Otherwise list the tests as not run.

## Report back (required)
1. Files changed or created, with a one-line purpose each, and confirmation of CRLF.
2. Test results per suite (pass/fail counts), plus the list of tests not run and why.
3. For each exploit or cap case (Tasks 2, 4, 5-8, 10), the test name that proves it.
4. **Steps left for Cronus:**
   - migrations to apply and functions to deploy;
   - the treasury multisig and ATAs to create, and mints to supply;
   - Squads proposals with exact parameters;
   - USD prices to set;
   - Edge secrets/env to set (`PYTH_API_KEY`, `RAM_SHOP_TREASURY_OWNER`);
   - when to flip `credit_mode` and `pricing_mode`;
   - the Task 13 ToU wording for legal review;
   - whether he vetoes Proposal D1 or D5.
5. Anything you found that contradicts this handoff, the Decisions, or RAM_00.
