# RAM Shop: manual-review runbook (Devnet)

**Status:** draft, 2026-10-28 (RAM Shop R2 Task 6). Devnet only. Refunds and any treasury movement are Cronus-signed steps.

## When an order lands in `manual_review`

`ram_shop_confirm` (and the status/reconcile paths) verify the player's finalized payment against the exact message the server quoted. Any difference parks the order in `manual_review` with a `failure_code`. The player may already have paid, so treat every `manual_review` order as "money may be in the treasury".

| `failure_code` | Usual cause | Player paid? |
|---|---|---|
| `payment_message_mismatch`, `payment_message_hash_mismatch` | The wallet edited the transaction (priority fee, Lighthouse-style guard, reordering) before signing | Usually yes |
| `payment_instruction_count_mismatch` | The wallet added or removed an instruction | Usually yes |
| `payment_compute_limit_mismatch`, `payment_compute_price_mismatch` | The wallet changed compute-budget values | Usually yes |
| `payment_amount_mismatch`, `payment_accounts_mismatch`, `payment_reference_mismatch` | Wrong amount, payee or reference | Maybe; check the amount and payee |
| `payment_signature_mismatch`, `payment_signature_shape_mismatch` | A signature that isn't this transaction's, or multiple signers | Check the fee payer |

A transaction paid by a different wallet than the order's (`payment_not_from_order_wallet`) is refused without touching the order (R2 Task 5), so it never reaches this queue. `payment_execution_failed` goes to `failed`, not here: the transfer didn't happen.

## 1. Find the orders

Read-only, service role, hosted Devnet:

```sql
SELECT order_id, player_profile_id, wallet_address, sku, ram_amount, tender,
       payment_atomic_amount, mint_address, destination, reference_address,
       failure_code, created_at, updated_at
  FROM public.ram_purchase_orders
 WHERE status = 'manual_review'
 ORDER BY updated_at;

-- The signature the player submitted is the failed attempt on that order.
SELECT a.signature, a.status, a.error_code, a.observed_at
  FROM public.ram_payment_attempts a
  JOIN public.ram_purchase_orders o ON o.id = a.order_id
 WHERE o.order_id = '<order_id>';
```

## 2. Verify the payment on chain

Use `getTransaction <signature>` at `finalized` commitment on a Devnet RPC, plus `getSignaturesForAddress <reference_address>` to find the transaction if no signature was stored. Record in the review note:

1. `meta.err` is `null`.
2. The fee payer (first account key) equals the order's `wallet_address`.
3. Exactly one transfer to the order's `destination`:
   - **SOL:** a System transfer of exactly `payment_atomic_amount` lamports, confirmed by `postBalances - preBalances` on the destination.
   - **USDC/SKR:** `transferChecked` on the order's `mint_address` and decimals into the treasury ATA, confirmed by `postTokenBalances - preTokenBalances`.
4. The order's `reference_address` appears in the transfer's keys.
5. No other instruction moves funds out of the treasury or names the reference key.
6. No other order is already `settled` with this signature (`ram_purchase_orders.payment_signature`).

## 3. Decide

| Finding | Action |
|---|---|
| Checks 1-6 pass (typical wallet edit) | **Settle**: the player paid exactly the quote. |
| Paid, but a different amount or to the wrong payee | **Refund** what the treasury received; don't settle. |
| Not paid, or paid by another wallet | **Reject**: leave the order in `manual_review` and note why. No refund. |

## 4. Settle (gap: no operator RPC yet)

`ram_shop_mark_payment_verified` only accepts `quoted`/`submitted` orders, so a `manual_review` order can't currently be settled through the normal functions, and **nothing should be hand-edited with raw SQL**. A follow-up should add a service-only operator RPC with this contract:

- `ram_shop_operator_settle_manual_review(p_order_id UUID, p_signature TEXT, p_operator TEXT, p_note TEXT)`
- Requires status `manual_review`; records the operator, note and timestamp on the order.
- Runs the same steps as a normal settle: a verified attempt (respecting the partial unique index from `20261028120300_ram_shop_signature_poisoning.sql`), `payment_verified`, then `ram_shop_settle_purchase`, which writes the `shop_purchase` ledger row.
- In `chain_mirror` credit mode the settle requires the on-chain `credit_purchased_ram` receipt, so the operator path also runs the Edge credit step. In `postgres_only` it doesn't.

Until that RPC exists, the only operator remedy is a refund.

## 5. Refund (Cronus-signed)

- **Today (Devnet):** the payee is `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG`, a hot wallet. A refund is a plain transfer from it to the order's `wallet_address` for the amount the treasury actually received, signed by Cronus.
- **After the treasury move (R2 Task 7):** the payee is the treasury vault `Hkn8eVNs11wFKvGKGk3GZqo7WA29bYn3mroWNDj8gdaL` (multisig `BLrr7egH9EDsUL1kufZpr8o9q2jy5D9DSCfrAbmfSCap`, 2-of-3). A refund is a Squads vault transaction (System transfer, or SPL `transferChecked` from the vault's ATA) created and approved by two members. The local signing page (`tools/vrf-efficacy/squads-signer`) only targets the upgrade multisig today and needs a treasury mode first.
- Record the refund signature in the review note and leave the order in `manual_review` with that note. A refunded order must never settle.

## 6. Record

Keep one note per order: order id, signature, which checks passed, the decision, the operator, and the settle or refund signature. Until the operator RPC adds columns for this, keep the notes in the ops log and link them from the order's `failure_code` investigation.
