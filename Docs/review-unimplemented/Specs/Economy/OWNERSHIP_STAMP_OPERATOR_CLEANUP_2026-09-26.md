> **Parked for review.** Probably scrap for the public presentation. One-off operator session log, not a mechanic spec. Review list: [../../README.md](../../README.md).

﻿# Operator cleanup — Seeker Stamp session 2026-09-26

Status: **operator runbook** (no code deploy).  
Profile: `38ac2f9a-5b94-4037-9bc6-fdd205632018`  
Wallet: `9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj`

## Chain facts (verified)

| Item | Value |
|------|--------|
| Finalized Stamp tx | `4QTPy1v6r9D6VypaMdiLorwvWechTQzrBhny1fdg23VMoPvGSHPecg5ZYpKdbfk8k4KrpSj1LRuHS5nG3as4QTcd` |
| Explorer | https://explorer.solana.com/tx/4QTPy1v6r9D6VypaMdiLorwvWechTQzrBhny1fdg23VMoPvGSHPecg5ZYpKdbfk8k4KrpSj1LRuHS5nG3as4QTcd?cluster=devnet |
| Core asset | `AYXk8fkDKKPV4nXyhhFYzBPqLU2GZkBVBn6C8cmLrRYW` |
| Slot / time (UTC) | 504378958 / 2026-09-26 10:38:50 |
| Ticket in that tx | `7eAvg8WAoEgpW8LpwwipKhMHpEutyejvpRyFWEBdUkow` |
| Matching DB op | `17999ff0-46fb-4572-ba59-43732d755a7d` |
| Idempotency key | `de784d9f02754f71bea32c9022e869fe` |
| Idempotency hash | `6de5fcecbafe6736e811c513a089b109ec6741a3df29f2fd6417337957da1a60` |
| Known row | `4205f8a6-7419-439d-be90-1cfdff60c013` |
| On-chain instance | `1` |
| Modifier | `10.1-a` |
| On-chain RAM after Stamp | ~20 (do not wipe profile) |

All seven exported rows were still `prepared` / `signature null` — settlement never finished.

## What you can delete

**Safe to delete (or mark expired):** the six non-success prepares. Prefer `status = 'expired'` first; hard-delete is fine afterward if you want a clean table.

| operation_id | stamped_asset_address | known_id | Why safe |
|--------------|----------------------|----------|----------|
| `59d2348d-a586-4058-bc40-8e939609e62b` | 4J4R7HES… | 4205f8a6… (retry) | Never landed; new key after UI drop |
| `b973ed03-8508-4e87-8615-e2eac399b4f3` | 9BRBN3jo… | c9eca3e5… | Prepare only |
| `1293eb5a-603b-4b34-8faf-bd8d367b6b5a` | 7joohJST… | c9eca3e5… | Prepare only |
| `c807ee11-ef22-4788-8712-9c19f1676f4c` | 3NxZA1iQ… | 939ee24d… | Prepare only |
| `9d42be09-acd9-4131-9562-95b03ea899ea` | DiK4cnE2… | 939ee24d… | Prepare only |
| `3a93b8e4-1bb1-4cc2-97aa-8e8b5e168a88` | FSjStYYH… | f47d705a… | Prepare only |

**Do NOT delete:**

- `17999ff0-46fb-4572-ba59-43732d755a7d` (AYXk… success) until it is `confirmed`
- Known row `4205f8a6-7419-439d-be90-1cfdff60c013` until it is `consumed_at` set

---

## Step 0 — Preflight (read-only)

```sql
-- Profile RAM mirror
SELECT player_profile_id, ram_balance
FROM public.player_profiles
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018';

-- Known row that was stamped on-chain
SELECT known_id, move_id, onchain_instance_id, consumed_at, created_at
FROM public.ownership_known_moves
WHERE known_id = '4205f8a6-7419-439d-be90-1cfdff60c013';

-- Stamp ops for this session
SELECT operation_id, known_id, status, signature, stamped_asset_address,
       idempotency_key, onchain_instance_id, confirmed_at, expires_at
FROM public.ownership_stamp_operations
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
ORDER BY created_at;
```

Expect: success op still `prepared`; Known still `consumed_at IS NULL`; `ram_balance` likely around **80** (4× Known rolls from a 100 grant, Stamp debit never settled). If `ram_balance` is already &lt; 75, stop and message before settling.

---

## Step 1 — Settle the real Stamp (keep this row)

Prefer the existing atomic RPC (debits **75** server RAM, consumes Known, marks op confirmed). Run as **service_role** / SQL editor.

```sql
BEGIN;

SELECT public.ownership_confirm_stamp_atomic(
  '38ac2f9a-5b94-4037-9bc6-fdd205632018'::uuid,
  'de784d9f02754f71bea32c9022e869fe',
  '4205f8a6-7419-439d-be90-1cfdff60c013'::uuid,
  '4QTPy1v6r9D6VypaMdiLorwvWechTQzrBhny1fdg23VMoPvGSHPecg5ZYpKdbfk8k4KrpSj1LRuHS5nG3as4QTcd',
  jsonb_build_object(
    'assetAddress', 'AYXk8fkDKKPV4nXyhhFYzBPqLU2GZkBVBn6C8cmLrRYW',
    'ownerAddress', '9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj',
    'knownInstanceId', '4205f8a6-7419-439d-be90-1cfdff60c013',
    'knownOnchainInstanceId', '1',
    'moveId', (
      SELECT move_id FROM public.ownership_known_moves
      WHERE known_id = '4205f8a6-7419-439d-be90-1cfdff60c013'
    ),
    'rarity', '0',
    'modifier1Id', '10.1-a',
    'modifierCount', 1,
    'isCoreAsset', true,
    'reconciled_by', 'operator_2026-09-26',
    'reconciled_note', 'manual settle after confirm DAS lag; on-chain tx finalized'
  ),
  1,
  75
);

COMMIT;
```

If the RPC raises `insufficient_ram`, do **not** force it. Snapshot `ram_balance` and stop.

If the RPC raises `known_not_found_or_consumed` because something already consumed the Known, only update the stamp op:

```sql
UPDATE public.ownership_stamp_operations
SET status = 'confirmed',
    signature = '4QTPy1v6r9D6VypaMdiLorwvWechTQzrBhny1fdg23VMoPvGSHPecg5ZYpKdbfk8k4KrpSj1LRuHS5nG3as4QTcd',
    confirmed_at = now(),
    updated_at = now(),
    verified_asset_json = jsonb_build_object(
      'assetAddress', 'AYXk8fkDKKPV4nXyhhFYzBPqLU2GZkBVBn6C8cmLrRYW',
      'reconciled_by', 'operator_2026-09-26'
    )
WHERE operation_id = '17999ff0-46fb-4572-ba59-43732d755a7d'
  AND status = 'prepared';
```

### Manual fallback (only if RPC cannot run)

Mirrors `ownership_confirm_stamp_atomic` in one transaction:

```sql
BEGIN;

UPDATE public.player_profiles
SET ram_balance = ram_balance - 75
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
  AND ram_balance >= 75;

UPDATE public.ownership_known_moves
SET consumed_at = now(),
    onchain_instance_id = 1
WHERE known_id = '4205f8a6-7419-439d-be90-1cfdff60c013'
  AND player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
  AND consumed_at IS NULL;

UPDATE public.ownership_stamp_operations
SET status = 'confirmed',
    signature = '4QTPy1v6r9D6VypaMdiLorwvWechTQzrBhny1fdg23VMoPvGSHPecg5ZYpKdbfk8k4KrpSj1LRuHS5nG3as4QTcd',
    verified_asset_json = jsonb_build_object(
      'assetAddress', 'AYXk8fkDKKPV4nXyhhFYzBPqLU2GZkBVBn6C8cmLrRYW',
      'ownerAddress', '9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj',
      'knownInstanceId', '4205f8a6-7419-439d-be90-1cfdff60c013',
      'modifier1Id', '10.1-a',
      'reconciled_by', 'operator_2026-09-26'
    ),
    confirmed_at = now(),
    updated_at = now()
WHERE operation_id = '17999ff0-46fb-4572-ba59-43732d755a7d'
  AND status = 'prepared';

-- Sanity: exactly one row each
SELECT ram_balance FROM public.player_profiles
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018';

SELECT known_id, consumed_at, onchain_instance_id
FROM public.ownership_known_moves
WHERE known_id = '4205f8a6-7419-439d-be90-1cfdff60c013';

SELECT operation_id, status, signature
FROM public.ownership_stamp_operations
WHERE operation_id = '17999ff0-46fb-4572-ba59-43732d755a7d';

COMMIT;
```

Abort (`ROLLBACK`) if any of those updates touch 0 rows unexpectedly.

---

## Step 2 — Clear the six junk prepares (your offer)

### Safer: mark expired

```sql
UPDATE public.ownership_stamp_operations
SET status = 'expired',
    updated_at = now()
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
  AND status = 'prepared'
  AND operation_id IN (
    '59d2348d-a586-4058-bc40-8e939609e62b',
    'b973ed03-8508-4e87-8615-e2eac399b4f3',
    '1293eb5a-603b-4b34-8faf-bd8d367b6b5a',
    'c807ee11-ef22-4788-8712-9c19f1676f4c',
    '9d42be09-acd9-4131-9562-95b03ea899ea',
    '3a93b8e4-1bb1-4cc2-97aa-8e8b5e168a88'
  );
-- Expect: UPDATE 6
```

### Or hard-delete those six only

```sql
DELETE FROM public.ownership_stamp_operations
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
  AND status IN ('prepared', 'expired', 'failed')
  AND operation_id IN (
    '59d2348d-a586-4058-bc40-8e939609e62b',
    'b973ed03-8508-4e87-8615-e2eac399b4f3',
    '1293eb5a-603b-4b34-8faf-bd8d367b6b5a',
    'c807ee11-ef22-4788-8712-9c19f1676f4c',
    '9d42be09-acd9-4131-9562-95b03ea899ea',
    '3a93b8e4-1bb1-4cc2-97aa-8e8b5e168a88'
  );
-- Expect: DELETE 6
-- Must NOT include 17999ff0-…
```

---

## Step 3 — Verify

```sql
SELECT operation_id, status, signature, stamped_asset_address
FROM public.ownership_stamp_operations
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
ORDER BY created_at;

SELECT known_id, move_id, consumed_at, onchain_instance_id
FROM public.ownership_known_moves
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018'
ORDER BY created_at;

SELECT ram_balance
FROM public.player_profiles
WHERE player_profile_id = '38ac2f9a-5b94-4037-9bc6-fdd205632018';
```

Pass criteria:

1. AYXk op is `confirmed` with the signature above.
2. Known `4205f8a6…` has `consumed_at` set and `onchain_instance_id = 1`.
3. The six junk ops are gone or `expired`.
4. Server `ram_balance` ≈ prior − 75 (likely ~5 if you started near 80).
5. On-chain RAM still ~20 (unchanged by SQL).

---

## Step 4 — RAM top-up for more candy tests (after settle)

On-chain ~20 is below the next Stamp need (~80 if materialize+stamp). Use the **approved Devnet smoke grant** path (100 RAM, allowlisted wallet), not a random SQL bump of on-chain state.

After grant + confirm:

- Server and chain should both allow at least one more Known (5) + Stamp (75).
- Do **not** use Candy Machine GRANT RAM strip for Ownership Spine tests.

---

## Out of scope here (next session)

1. Create public bucket `chainbound-ownership-metadata`.
2. Upload JSON (+ library-card PNG) to  
   `…/stamped/AYXk8fkDKKPV4nXyhhFYzBPqLU2GZkBVBn6C8cmLrRYW.json`  
   so Explorer shows metadata without reminting.
3. Settlement lifecycle code (DAS retry, persist signature before DAS).

## Operator log

| When | Who | Result |
|------|-----|--------|
| | | Step 0 preflight ram_balance = |
| | | Step 1 settle OK / error = |
| | | Step 2 expired or deleted count = |
| | | Step 3 verify pass? |

