# Chainbound RAM Shop: Status Audit (read-only)

**Date:** 2026-10-28 (CT)
**Repo:** `C:\Users\mando\Downloads\UNITYGAMING\Signal Jam\Signal Jam 3D`, branch `sync/local-20261020`, HEAD `fd1a395a` ("Known roll bundles: atomic multi-pull ownership_roll_known"), with many uncommitted asset changes.
**Method:** Static review with `git grep`/`Get-Content`, safe Deno tests run with no network and no secrets, and read-only public JSON-RPC calls (devnet + mainnet `getAccountInfo`/`getSignaturesForAddress`). I didn't edit any repo file, sign anything, deploy anything, or read any env or keypair file. I didn't touch `Docs/GameTheory/`. `.claude/worktrees/` exists; I didn't look inside it.

> Everything under "Proposed" is a **proposal** for Cronus to approve. Nothing in it has been decided.
>
> **Update 2026-10-28 (CT):** Cronus has answered the open questions in §7. The decisions (D1-D6) and the revised, renumbered task list are in `Docs/Specs/Economy/RAM_SHOP_R2_CLAUDE_HANDOFF.md` under "Decisions (Cronus, 2026-10-28)". Where this audit and the handoff differ, the handoff wins. This audit's own findings are unchanged.

---

## 1. Current state (summary)

- **SOL checkout on devnet is built end to end.** The flow is Unity `RamShopScreen` → `ram_shop_prepare` (server quote plus a server-built tx) → Seeker wallet sign-and-send (MWA) → `ram_shop_confirm` (verification at finalized commitment) → on-chain `credit_purchased_ram` receipt → Postgres `ram_shop_settle_purchase`. `ram_shop_status` and the `ram_shop_reconcile` cron handle recovery.
- **The catalog seeds SOL prices only** (`20261004045000_devnet_seeker_ram_shop_catalog.sql`). The bundles are ram_5 = 100 RAM for 0.005 SOL, ram_20 = 500 RAM for 0.02 SOL, and ram_100 = 3000 RAM for 0.1 SOL. The payee is `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG`.
- **USDC/SKR are mostly plumbed but turned off.** The tender enum, the SPL `transferChecked` builder and verifier, the Token/Token-2022 allowance, the SKR mainnet mint pin and the USDC/SKR UI buttons all exist. There are **zero USDC/SKR price rows**, so the UI falls back to "Pay with SOL" (`RamShopScreen.cs:237`).
- **Daily cap:** earned RAM is capped at 100 per profile per UTC day in Postgres (`ram_daily_earnings`, CHECK 0..100, row lock). Purchases are deliberately **excluded** from the cap. That was decided in RAM_00 §0/§8 ("purchases excluded"), and the code matches it. The daily gift's 10 RAM is excluded the same way ([DAILY_LOGIN_REWARDS_v1](DAILY_LOGIN_REWARDS_v1.md)).
- **Architecture drift:** RAM_00 (approved) says shop purchases should credit Postgres directly as a `shop_purchase` ledger op, without `credit_purchased_ram`. The code still runs the on-chain credit. The Postgres settle also adds to `ram_balance` **without writing a `ram_ledger_operations` row**, so `ram_v2_audit_service` (balance = ledger sum) will flag every buyer as unbalanced.
- **Tests:** 15/17 of the Deno tests I could run safely pass. One guard test fails (a new Known-roll-bundles migration isn't on the approved RAM-writer list). `ram_shop_test.ts` can't run without `SUPABASE_URL`, so I didn't run it. No pgTAP suite covers the shop.
- **Docs:** there's **no doc planning USDC/SKR**. The only doc that plans shop work is `Docs/review-unimplemented/Guides/RAM_00_Immediate_Battle_Reward.md` (switch shop credit to Postgres). It also lists "RAM shop catalog/pricing changes" as **out of scope** (§11).

## 2. Docs that cover this area

| Doc | What it covers | Phase state |
|---|---|---|
| `Docs/review-unimplemented/Guides/RAM_00_Immediate_Battle_Reward.md` | RAM v2: Postgres-canonical RAM, cap 100 per UTC day with purchases excluded (§0 L15-18, §3 L140-151, §8 L365). Shop path: "Player signs SOL/token transfer → verify at finalized → Postgres credit `shop_purchase`, key order_id" (L71, L115-121). Edge change list (L194, L203). Migration order: "Switch the shop, then spends, then earn" (§6 L212). | The header says Phase 1 was implemented locally on 2026-10-09 but not deployed. Phases 2-4 are gated. I can't tell from the repo whether Phase 1 is now deployed. Phase 3 (retire the mirror) is where the shop switch lands. |
| `Docs/Specs/Security/EconomyAudits/01_RAM_ledger_and_balances.md` + `README.md` (2026-09-25) | The older dual-ledger audit, which predates v2. Its critical client-trusted battle faucet finding was addressed by RAM v2. | Historical |
| `Docs/Specs/Solana/CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md`, `Docs/Reports/SOLANA_STACK_AUDIT_2026-09-11.md` | GameConfig/daily cap on-chain, devnet-only posture | Historical; superseded by RAM_00 for RAM |
| `Docs/Specs/Solana/MAINNET_VRF_READINESS_PLAN.md` L253 | Mentions only the `ram_shop_reconcile` lease cron as a pattern | n/a |
| `Docs/Legal/TERMS_OF_USE.md` L107 | Says RAM is bought with SOL and mentions the daily earn cap | It will need a wording update if USDC/SKR ship (flag for Cronus) |

**Conclusion:** the docs plan the shop credit-path switch (RAM_00) but **not** the USDC/SKR expansion or the exploit fixes below. The handoff combines RAM_00's shop items with this audit's findings.

## 3. SOL flow trace (file:line)

1. **Catalog:** `ram_shop_catalog` → `_shared/ram_shop.ts:117-214`. It only returns prices that are enabled, have `verified_at` set and match the cluster.
2. **Prepare:** `ram_shop_prepare` (allowed body keys: sku, tender, wallet_address, wallet_public_key) → `preparePurchase` `ram_shop.ts:216-370`
   - Checks settings: checkout enabled, cluster, TTL (:227-238). Requires the active bound wallet; a mismatch is an error (:240-249). Ensures the ledger account and scope (:251-262).
   - **The price comes from the DB, never the client** (:264-278).
   - Reference = a PDA from sha256("ram-purchase:"+orderId) (:280-285).
   - Builds the transfer: SOL `SystemProgram.transfer` (:291-295); tokens use `transferChecked` with token-program, mint and destination checks (:299-319). The reference key is appended read-only (:323), followed by 2 compute-budget ixs (:327-328).
   - Stores the expected message bytes and hash (:331-338) and inserts the order (:343-367).
3. **Client:** `RamShopScreen.cs:292` PrepareAsync → simulate (:297) → `transactions.SubmitAsync` sign-and-send on device (:326; Editor uses non-device mode, :546-555) → ConfirmAsync (:343), or GetStatusAsync by reference if there's no signature (:344) → polling (:395) and restore of the saved order (:444).
4. **Confirm:** `confirmPurchase` `ram_shop.ts:384-421`
   - Checks the signature format, and that an already-verified order is being confirmed with the same signature (:391-396). Requires runtime cluster = order cluster (:400).
   - `verifyFinalizedPayment` (:583-600): `getTransaction` at finalized, and `meta.err` must be null (:595).
   - `verifyPaymentTransaction` (:602-628): exactly one signature, matching (:607-612); **message bytes equal the stored expected bytes** (:615); hash (:618); 3 ixs (:621); fee payer = wallet (:622).
   - `verifyPaymentInstruction` (:656-699): checks reference, program, keys, amount and decimals.
   - Results: an execution failure → `failed` (:404-405); a mismatch → `manual_review` (:407-408); success → `ram_shop_mark_payment_verified` (r01 migration :70-129).
5. **Credit:** `advancePurchaseSettlement` `ram_shop.ts:476-581`
   - Receipt PDA = ["ram_purchase_receipt", owner, orderHash] (:492-496). If the receipt is absent, it builds and sends `credit_purchased_ram` signed by the result-authority service key and confirms at finalized (:515-560).
   - Then `mark_credit_submitted` (:562) → `ram_shop_settle_purchase` (:569; final version in `20261009143000_ram_v2_pre_rollout_hardening.sql:568-617`). The settle locks the order and the ledger account, is idempotent, and does `ram_balance += ram_amount` with a u32 guard. It does not write a ledger op.
   - On-chain: `lib.rs:321-347` `credit_purchased_ram` (uncapped, `init` receipt, result_authority signer, accounts :1683-1710). The program doesn't bound the amount; the result authority is trusted.
6. **Recovery:** `ram_shop_status` reconciles quoted/submitted orders by reference (`reconcileQuotedPurchase` :443-474, with expiry by block height :469-471). `ram_shop_reconcile` claims batches with a 60s lease (`claim_reconcile_batch` r01 :184, SKIP LOCKED :213), authenticated by the `x-reconciler-secret` header (`ram_shop_reconcile/index.ts:11`).
7. **DB security:** RLS is enabled with **no policies**. Everything is `REVOKE`d from PUBLIC/anon/authenticated (`20261003113101…:246-266`), and functions are granted to service_role only (:621-639; v2 :1165-1176). The client can't write RAM tables or `player_profiles` directly.

## 4. Exploit / risk findings

| # | Sev | Finding | Where | Suggested fix (proposal) |
|---|---|---|---|---|
| E1 | **High** (integrity) | Purchase settle credits `player_profiles.ram_balance` with **no `ram_ledger_operations` row**. RAM v2's audit (balance = ledger sum) breaks for every buyer, and purchased RAM isn't traceable in the canonical ledger. RAM_00 L71/L121/L194 expects a `shop_purchase` credit op keyed by order_id. | `20261009143000…sql:568-617`; audit `20261009010622…sql:285-315` | Settle in one transaction: insert a settled `shop_purchase` credit op (idempotency key = order_id) and update the balance. Test that the audit stays balanced after a purchase. |
| E2 | **Medium** (griefing / stuck funds) | **Signature poisoning.** Payment signatures are public. An attacker with their own quoted order calls `ram_shop_confirm` with the victim's finalized signature. The mismatch goes to `mark_order_terminal`, which inserts `ram_payment_attempts(attacker_order, victim_sig)` (UNIQUE on signature). When the victim then confirms or reconciles, `mark_payment_verified` raises `signature_already_consumed`. The victim has paid and is never credited. No RAM is stolen. | `ram_shop.ts:404-408` → r01 `:165-171`, `:98-104` | Record a failed attempt against an order only when the tx fee payer equals that order's wallet, **or** make signature uniqueness apply only to `verified` attempts (partial unique index), so failed observations don't block. Add a regression test. |
| E3 | **Medium** (stuck funds) | **Exact-message match vs wallet modification.** Seeker/MWA wallets (or Phantom/Lighthouse-style guards) may add instructions or change priority fees. The paid tx then fails `message_mismatch` / ix count, the order goes to `manual_review`, and the player has been charged with **no refund or recovery tool**. | `ram_shop.ts:615-626`, `:407-408` | Decide the policy: keep exact match and build an operator review/refund runbook, **or** verify semantically (the transfer + reference ix present exactly once, ignoring compute-budget and known guard ixs). Add tests either way. Needs a device test on a Seeker. |
| E4 | **Medium** (needed before USDC/SKR) | Mint pinning: SKR's mainnet mint is pinned in code and SQL, but **USDC isn't pinned** for any cluster and devnet mints aren't pinned. A bad `ram_shop_tender_prices` row (admin error) could price in a lookalike mint. The destination check verifies the token account's mint and program but **not its owner** (the treasury wallet). | `ram_shop.ts:302-308`, `:701-722`; `20261003113101…sql:63-88` | Pin mints per cluster in code and in a CHECK; require the destination = the ATA(treasury, mint, program) and check `owner == treasury`. |
| E5 | **Medium** (needed before Token-2022) | Token-2022 is allowed but its extensions aren't inspected. A transfer-fee, transfer-hook or confidential mint would change the amount actually received while the ix amount still matches. Verified on mainnet: SKR and USDC are both classic SPL Token, so this is moot for them today. | `ram_shop.ts:299`, `:701-722` | Allow only the classic Token program for USDC/SKR, or reject Token-2022 mints that have fee/hook extensions. |
| E6 | Low | Concurrent confirm/status/reconcile calls for the same order can each send a `credit_purchased_ram` tx. The on-chain `init` receipt and the `FOR UPDATE` settle prevent double credit, but fees are wasted and losers get noisy errors. confirm/status take no lease. | `ram_shop.ts:476-560` | Moot if E1/RAM_00 removes the on-chain credit. Otherwise take the order lease before sending. |
| E7 | Low | The treasury is `DkFmzzz…`, an active devnet system wallet: ~9.6 SOL and dozens of txs on 2026-10-27, last at 22:39 CT. The seed comment calls it the "devnet upgrade authority", which is now stale because upgrade authority is the Squads vault. Fine for devnet, but it's a hot key as payee. | `20261004045000…sql` | Cronus decides the treasury (Squads vault or a dedicated treasury) before anything leaves devnet. Fix the comment. |
| E8 | Low | `x-reconciler-secret` is compared with `!==`, which isn't constant-time. | `ram_shop_reconcile/index.ts:11` | Use a timing-safe compare. |
| E9 | Low / info | Uncapped credit paths besides purchases: the quest reward of 15 RAM (`20261014030000_fisher_ape_lucky_crayon.sql`, ledger-backed) and the opening-balance ops. RAM_00 §8 L366 says **quest rewards count toward the cap**, but this migration credits outside `ram_daily_earnings`. `ownership_operator_devnet_ram_topup` is retired (returns 410). | fisher_ape migration | Ask Cronus: should the quest 15 RAM count toward the cap per RAM_00 §8? If so, route it through the capped path. **Decided 2026-10-28: yes. This is handoff Task 4 (D3).** |
| E10 | Low / info | On-chain `grant_ram` caps per wallet PlayerProfile PDA (`lib.rs:231-255`), while the Postgres cap is per profile. Wallets are permanently 1:1 with profiles, so there's no multi-wallet bypass in v2. | `lib.rs:231-255` | Retire per RAM_00 Phase 3. |

**Checked and OK:**
- **Replay:** UNIQUE signature, a per-order reference PDA, exact message bytes, a single fee payer.
- **Price and amount:** the client never supplies the price or amount (only sku/tender).
- **Failed or unconfirmed txs:** finalized commitment plus `meta.err == null` are required.
- **Devnet/mainnet mix-ups:** `RAM_SHOP_CLUSTER` is forced to devnet, with a genesis-hash pin and a program-id pin (`ram_shop.ts:99-115`), plus devnet guards in the migrations.
- **Client access:** there's no client RLS write path.
- **Daily cap:** the earned cap is enforced by a DB CHECK plus a row lock, with the day boundary on the UTC DB clock (`v2 :1016-1017, :1105-1111`).
- **Refunds:** no refund flow exists; `manual_review` is the only route (see E2/E3).

## 5. Test status

| Suite | Result |
|---|---|
| Deno `_shared/ram_ledger_guard_test.ts` | **4/5 pass. FAIL:** "post-activation migrations reject unapproved direct RAM balance writes". The cause is `20261027120000_known_roll_bundles.sql` (`SET ram_balance = ram_balance - …` ~L245), which isn't in the allowed-writer list. Either the list needs the new reviewed migration added, or the migration needs review. |
| Deno `_shared/wallet_binding_test.ts` | 4/4 pass |
| Deno `_shared/solana_pda_test.ts` | 7/7 pass |
| Deno `_shared/ram_shop_test.ts` (10 tests: verifier, receipt, settle signature) | **Not run.** It needs env `SUPABASE_URL` at import, and I didn't grant env access. |
| Other Deno tests needing Supabase env | Probably any test that imports `account_helpers`/supabase-js. I didn't enumerate them. |
| pgTAP `Supabase/tests/*.sql` | Not run (needs Docker + a local Supabase). `ram_v2_phase_1_6/1_7` and `battle_reward_*` cover the cap and "purchase exclusion". **No pgTAP suite covers `ram_shop_*` functions.** |
| Unity EditMode `RamShopSelectOpenTests`, `RamShopViewAuthoringTests`, `PlayerRamBalanceStoreTests`, etc. | Not run (needs the Unity Editor/batchmode). These are UI/authoring tests, not payment tests. |
| .NET BattleService xUnit | Not run; not shop-related. |

Command used: `deno test --no-prompt --allow-read=.. <files>` from `Supabase/functions` (deno 2.9.0).

## 6. USDC / SKR readiness gaps

Facts I verified this session via public mainnet/devnet RPC and solanamobile.com:
- **SKR:** mint `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`. It's classic **SPL Token** (`Tokenkeg…`), 6 decimals, no freeze authority, mint authority `FMNn5sor…`. Solana Mobile docs call it an "SPL Token". **It doesn't exist on devnet**, so devnet testing needs a project-owned test mint.
- **USDC mainnet:** `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v`, SPL Token, 6 decimals.
- **USDC devnet (Circle):** `4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU`, SPL Token, 6 decimals.

| Area | Ready | Gap |
|---|---|---|
| Server tx build (transferChecked + reference + CU) | Yes (`ram_shop.ts:299-323`) | The source ATA must already exist. The destination ATA must exist, because prepare checks it (:701-722). There's no create-ATA step, which is fine for the payee if pre-created. |
| Verifier for tokens | Yes (`:681-698`): opcode 12, amount, decimals, mint, owner signer | E4 (owner of the destination ATA), E5 (Token-2022 extensions) |
| DB schema | Yes (`tender_prices` CHECKs: non-SOL needs mint+program; SKR mainnet pinned) | No USDC pin; no devnet mint pins |
| Catalog data | No | No USDC/SKR rows. Needs a devnet-guarded migration with atomic prices + `verified_at`, plus treasury ATAs created on devnet (a **Cronus on-chain step**). |
| Pricing model | Fixed per-tender atomic prices in DB | There's no oracle. Fixed USDC is straightforward. SKR is volatile, so either fixed prices with manual re-pricing or a price oracle (Pyth/Jupiter) is a **Cronus decision**. Not in scope unless he asks. |
| Client UI | Buttons `Tender_SOL/USDC/SKR` exist (`RamShopScreen.cs:728-730, 765-767`); card shows tender price (`RamBundleCardPresenter.cs:103-117`) | Copy is SOL-centric (`:212`, `:237`), and a no-token-balance or no-ATA message is needed. The confirmation shows the mint (`:561`), but there's no friendly token name or decimals formatting. |
| Tests | SOL verifier tests exist | No SPL verifier tests (wrong mint, wrong decimals, wrong destination owner, Token-2022 mint, missing ATA) |
| Legal | — | TERMS_OF_USE L107 says "bought with SOL" |

## 7. Proposed next build phase: "RAM Shop R2: harden, then add USDC/SKR (devnet)"

*Proposal only. The ordering puts cap/exploit fixes before new currencies, as requested.*

**Task 1: fix the failing ledger guard test**
- Files: `Supabase/functions/_shared/ram_ledger_guard_test.ts`; review `Supabase/migrations/20261027120000_known_roll_bundles.sql` (~L245).
- Accept when: the guard passes, and the known-roll debit is confirmed to be backed by a settled ledger op in the same transaction. If it isn't, report it and don't whitelist it.
- Test: `deno test` on the guard file.

**Task 2 (E1): purchases write a `shop_purchase` ledger op**
- Files: a new migration `Supabase/migrations/2026102xxxxxxx_ram_shop_ledger_credit.sql` (CREATE OR REPLACE `ram_shop_settle_purchase`); `_shared/ram_shop.ts` (settle call, if the signature changes).
- Accept when: the balance and a settled credit op (idempotency key = order_id, kind `shop_purchase`) are written atomically; a replayed settle is a no-op; `ram_v2_audit_service` is balanced after a purchase; `ram_daily_earnings` is unchanged.
- Tests: a new pgTAP `Supabase/tests/ram_shop_settlement_v1.sql`, plus a Deno ledger-guard update.

**Task 3 (RAM_00 §5 L194/L203): retire the on-chain `credit_purchased_ram` step**
- Only if Cronus confirms RAM_00's shop switch still stands.
- Files: `_shared/ram_shop.ts` (`advancePurchaseSettlement` :476-581, receipt reader :724-748), `ram_shop_reconcile/index.ts` (drop `ram_ledger_register_audit`).
- Accept when: verified payment → Postgres settle with no chain write.
- Tests: Deno tests that a verified order settles once under concurrent confirm + status + reconcile.

**Task 4 (E2): stop signature poisoning**
- Files: a new migration (partial unique index on `ram_payment_attempts(signature) WHERE status='verified'`, or a fee-payer guard), and `ram_shop.ts:404-408` (only record attempts whose fee payer equals the order wallet).
- Accept when: attacker order + victim signature can't block the victim's settle.
- Tests: pgTAP + Deno regression.

**Task 5 (E3): define handling for wallet-modified txs**
- Files: `ram_shop.ts:602-699`, `ram_shop_test.ts`.
- Accept when: there's an explicit policy (exact match + manual-review runbook doc, or a semantic verifier) and tests for added priority-fee/guard instructions.
- Includes a device test on a Seeker (Cronus).

**Task 6 (E4/E5): lock down SPL before enabling it**
- Files: `ram_shop.ts` (mint pins per cluster for USDC/SKR, destination = ATA(treasury), owner check, allow only classic Token for these tenders), and a new CHECK migration on `ram_shop_tender_prices`.
- Tests: wrong mint, lookalike mint, wrong decimals, destination owned by someone else, Token-2022 mint rejected, missing source ATA → clean error.

**Task 7: devnet USDC/SKR catalog**
- Files: a new devnet-guarded migration adding USDC (Circle devnet mint) and SKR (devnet test mint; Cronus provides the address) price rows **disabled/unverified**.
- **Cronus steps:** create the treasury ATAs, mint the test SKR, decide prices, and set `verified_at`/enable via a Squads or operator step.

**Task 8: client polish**
- Files: `RamShopScreen.cs` (tender copy at :212/:237, "insufficient USDC/SKR or no token account" errors, token amount formatting with decimals); EditMode tests in `Assets/Scripts/UI/MenuNavigation/Editor/Tests/`.

**Task 9: small items**
- E8 constant-time compare (`ram_shop_reconcile/index.ts:11`), E7 seed comment, E9 quest-cap question (decided 2026-10-28: capped; now handoff Task 4).

**Open questions for Cronus:** answered 2026-10-28. See the handoff's "Decisions" section:
- D1: the shop credits Postgres only, with no on-chain credit. This is a proposal, behind a `credit_mode` flag, and it depends on the stamp/bind/rebirth spend switch.
- D2: purchases stay outside the cap.
- D3: quest rewards count toward the cap (new handoff Task 4).
- D4: fixed USD prices with server-side feeds.
- D5: a separate treasury multisig (proposal) with a config-driven payee.
- D6: ToU wording drafted for legal review.

## 8. Not verified
- Whether RAM v2 Phase 1 migrations and the shop functions are **deployed** to devnet Supabase, and whether any real purchases exist. I didn't query the DB.
- `ram_shop_test.ts` results (blocked by env); pgTAP and Unity tests weren't run.
- Whether Seeker/MWA wallets modify the server-built tx in practice (E3). Needs a device test.
- The exact behavior of `resolveAuthenticatedProfile`/`requireBoundWallet` in `_shared/account_helpers.ts`; I didn't read it fully.
- The legacy `public.players.ram_balance` column's grants (RLS on `player_profiles` confirmed; `players` not re-checked).
