-- RAM Shop R2 Task 3 (Proposal D1, Cronus may veto before merge): shop credit mode.
--
-- 'chain_mirror' (default, today's behavior): after the payment verifies, the
-- Edge function sends credit_purchased_ram signed by the result authority and
-- settles with that tx's signature and the RamPurchaseReceipt PDA. Settlement
-- now refuses to run in this mode without both, so Postgres can't credit a
-- purchase whose on-chain mirror was skipped.
--
-- 'postgres_only': no chain write. The player's finalized payment tx (signature
-- and per-order reference key, both stored on the order and in the ledger row
-- metadata) is the purchase proof, and settle takes NULL credit fields.
--
-- Cronus flips this only after the stamp/bind/rebirth spend switch (RAM_00 section 5),
-- because those paths still check on-chain profile.ram_balance. Don't flip it in
-- a migration.

ALTER TABLE public.ram_shop_settings
    ADD COLUMN IF NOT EXISTS credit_mode TEXT NOT NULL DEFAULT 'chain_mirror';

DO $credit_mode_check$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ram_shop_settings_credit_mode_check'
           AND conrelid = 'public.ram_shop_settings'::regclass
    ) THEN
        ALTER TABLE public.ram_shop_settings
            ADD CONSTRAINT ram_shop_settings_credit_mode_check
            CHECK (credit_mode IN ('chain_mirror', 'postgres_only'));
    END IF;
END
$credit_mode_check$;

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
    v_credit_mode TEXT;
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
    SELECT credit_mode INTO v_credit_mode FROM public.ram_shop_settings WHERE singleton;
    IF v_credit_mode IS NULL THEN RAISE EXCEPTION 'ram_shop_settings_missing'; END IF;
    IF v_credit_mode = 'chain_mirror'
       AND (NULLIF(BTRIM(p_credit_signature), '') IS NULL OR NULLIF(BTRIM(p_receipt_address), '') IS NULL) THEN
        RAISE EXCEPTION 'credit_receipt_required';
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
            'credit_receipt_address', p_receipt_address,
            'credit_mode', v_credit_mode
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
