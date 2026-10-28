-- RAM Shop R2 Task 2: a settled purchase writes its own ledger row.
--
-- ram_shop_settle_purchase used to add ram_amount to player_profiles.ram_balance
-- with no ram_ledger_operations row, so ram_v2_audit_service (balance = sum of
-- settled ledger rows) reported every buyer as unbalanced. Settlement now writes
-- one settled 'shop_purchase' credit keyed by operation_id = order_id and moves
-- the balance in the same transaction.
--
-- Purchases stay outside the daily earn cap: this function never reads or
-- writes ram_daily_earnings.
--
-- Lock order follows the RAM_00 profile-first rule: player_profiles, then the
-- order, then the ledger account. Idempotency, the u32 guard and the stored
-- balance/version pair are unchanged.
--
-- No backfill: on 2026-10-28 the hosted Devnet had no settled purchase orders.
-- The guard below refuses to apply if a settled post-v2 order without a ledger
-- row ever appears, so it gets operator review instead of a silent gap.

DO $shop_ledger_gap_guard$
BEGIN
    IF EXISTS (
        SELECT 1
          FROM public.ram_purchase_orders AS o
          LEFT JOIN public.ram_v2_migration_audit AS a
            ON a.player_profile_id = o.player_profile_id
         WHERE o.status = 'settled'
           AND (a.captured_at IS NULL OR o.settled_at > a.captured_at)
           AND NOT EXISTS (
               SELECT 1 FROM public.ram_ledger_operations AS l
                WHERE l.cluster = o.cluster AND l.operation_id = o.order_id
           )
    ) THEN
        RAISE EXCEPTION 'ram_shop_settled_orders_without_ledger_rows_require_operator_review';
    END IF;
END
$shop_ledger_gap_guard$;

CREATE OR REPLACE FUNCTION public.ram_shop_settle_purchase(
    p_order_id UUID,
    p_credit_signature TEXT,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE
    v_profile_id UUID;
    v_order public.ram_purchase_orders%ROWTYPE;
    v_balance BIGINT;
    v_balance_version BIGINT;
BEGIN
    SELECT player_profile_id INTO v_profile_id
      FROM public.ram_purchase_orders WHERE order_id = p_order_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;

    -- Profile first, then the order, then the ledger account.
    PERFORM 1 FROM public.player_profiles
     WHERE player_profile_id = v_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'player_profile_not_found'; END IF;

    SELECT * INTO v_order FROM public.ram_purchase_orders
     WHERE order_id = p_order_id FOR UPDATE;
    IF v_order.status = 'settled' THEN
        RETURN jsonb_build_object(
            'replayed', TRUE, 'status', 'settled',
            'balance_after', v_order.balance_after,
            'balance_version', v_order.balance_version
        );
    END IF;
    IF v_order.status NOT IN ('payment_verified', 'credit_submitted') THEN
        RAISE EXCEPTION 'payment_not_verified';
    END IF;
    PERFORM 1 FROM public.ram_ledger_accounts
     WHERE player_profile_id = v_order.player_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_not_active'; END IF;

    -- An unsettled order must not already own a ledger row.
    PERFORM 1 FROM public.ram_ledger_operations
     WHERE cluster = v_order.cluster AND operation_id = v_order.order_id;
    IF FOUND THEN RAISE EXCEPTION 'shop_purchase_operation_conflict'; END IF;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, state, metadata_json, settled_at
    ) VALUES (
        v_order.order_id, v_order.player_profile_id, v_order.wallet_address, v_order.cluster,
        'shop_purchase', 'credit', v_order.ram_amount, 'settled',
        jsonb_build_object(
            'source', 'ram_shop',
            'order_id', v_order.order_id,
            'sku', v_order.sku,
            'catalog_version', v_order.catalog_version,
            'tender', v_order.tender,
            'payment_atomic_amount', v_order.payment_atomic_amount::TEXT,
            'payment_signature', v_order.payment_signature,
            'reference_address', v_order.reference_address,
            'credit_signature', p_credit_signature,
            'credit_receipt_address', p_receipt_address
        ),
        now()
    );

    UPDATE public.player_profiles
       SET ram_balance = ram_balance + v_order.ram_amount
     WHERE player_profile_id = v_order.player_profile_id
       AND ram_balance + v_order.ram_amount <= 4294967295
     RETURNING ram_balance, ram_balance_version INTO v_balance, v_balance_version;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_balance_overflow'; END IF;

    UPDATE public.ram_purchase_orders SET
        status = 'settled', credit_signature = p_credit_signature,
        credit_receipt_address = p_receipt_address,
        balance_after = v_balance, balance_version = v_balance_version,
        settled_at = now(), lease_owner = NULL, lease_expires_at = NULL, updated_at = now()
     WHERE id = v_order.id;
    RETURN jsonb_build_object(
        'replayed', FALSE, 'status', 'settled',
        'balance_after', v_balance, 'balance_version', v_balance_version
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ram_shop_settle_purchase(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_shop_settle_purchase(UUID, TEXT, TEXT) TO service_role;
