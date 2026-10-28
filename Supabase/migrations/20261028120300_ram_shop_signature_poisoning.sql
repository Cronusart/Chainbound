-- RAM Shop R2 Task 5: block payment-signature poisoning.
--
-- Attack: payment signatures are public. An attacker with their own quoted
-- order calls ram_shop_confirm with a victim's finalized signature. The
-- mismatch reached ram_shop_mark_order_terminal, which stored
-- (attacker_order, victim_signature) in ram_payment_attempts, and that table's
-- UNIQUE (signature) then made the victim's ram_shop_mark_payment_verified
-- raise signature_already_consumed. The victim paid and was never credited.
--
-- Fix (a), here: only a *verified* attempt consumes a signature. UNIQUE
-- (signature) becomes a partial unique index on verified rows, so failed or
-- submitted observations on other orders can't block anyone, and one signature
-- still can't be verified for two orders (also held by
-- ram_purchase_orders UNIQUE (cluster, payment_signature)).
-- Fix (b), in _shared/ram_shop.ts: confirm refuses a transaction whose fee payer
-- isn't the order's wallet before it records anything against the order.

ALTER TABLE public.ram_payment_attempts
    DROP CONSTRAINT IF EXISTS ram_payment_attempts_signature_key;

CREATE UNIQUE INDEX IF NOT EXISTS ram_payment_attempts_verified_signature_idx
    ON public.ram_payment_attempts (signature)
    WHERE status = 'verified';

CREATE INDEX IF NOT EXISTS ram_payment_attempts_signature_idx
    ON public.ram_payment_attempts (signature);

CREATE OR REPLACE FUNCTION public.ram_shop_mark_payment_verified(
    p_order_id UUID,
    p_signature TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE
    v_order public.ram_purchase_orders;
BEGIN
    SELECT * INTO v_order
      FROM public.ram_purchase_orders
     WHERE order_id = p_order_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;

    IF v_order.status IN ('payment_verified', 'credit_submitted', 'settled') THEN
        IF v_order.payment_signature IS DISTINCT FROM p_signature THEN
            RAISE EXCEPTION 'order_signature_conflict';
        END IF;
        RETURN jsonb_build_object('replayed', TRUE, 'status', v_order.status);
    END IF;
    IF v_order.status NOT IN ('quoted', 'submitted') THEN
        RAISE EXCEPTION 'invalid_payment_state';
    END IF;

    -- Only another order's verified attempt consumes the signature.
    PERFORM 1
      FROM public.ram_payment_attempts
     WHERE signature = p_signature
       AND status = 'verified'
       AND order_id <> v_order.id
     FOR UPDATE;
    IF FOUND THEN RAISE EXCEPTION 'signature_already_consumed'; END IF;

    IF v_order.payment_signature IS NOT NULL AND v_order.payment_signature <> p_signature THEN
        UPDATE public.ram_payment_attempts
           SET status = 'failed', error_code = 'replaced_unverified_legacy_signature', observed_at = now()
         WHERE order_id = v_order.id
           AND signature = v_order.payment_signature
           AND status <> 'verified';
    END IF;

    BEGIN
        INSERT INTO public.ram_payment_attempts(order_id, signature, status, error_code)
        VALUES (v_order.id, p_signature, 'verified', NULL)
        ON CONFLICT (order_id, signature) DO UPDATE
           SET status = 'verified', error_code = NULL, observed_at = now();

        UPDATE public.ram_purchase_orders
           SET status = 'payment_verified',
               payment_signature = p_signature,
               payment_verified_at = now(),
               failure_code = NULL,
               reconcile_after = now(),
               updated_at = now()
         WHERE id = v_order.id;
    EXCEPTION WHEN unique_violation THEN
        -- A concurrent verification of the same signature for another order won.
        RAISE EXCEPTION 'signature_already_consumed';
    END;
    RETURN jsonb_build_object('replayed', FALSE, 'status', 'payment_verified');
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_shop_mark_order_terminal(
    p_order_id UUID,
    p_state TEXT,
    p_failure_code TEXT,
    p_observed_signature TEXT DEFAULT NULL
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE v_order public.ram_purchase_orders;
BEGIN
    IF p_state NOT IN ('expired_unpaid', 'failed', 'manual_review') THEN
        RAISE EXCEPTION 'invalid_terminal_state';
    END IF;
    IF NULLIF(BTRIM(p_failure_code), '') IS NULL THEN
        RAISE EXCEPTION 'failure_code_required';
    END IF;

    SELECT * INTO v_order
      FROM public.ram_purchase_orders
     WHERE order_id = p_order_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.status IN ('payment_verified', 'credit_submitted', 'settled') THEN
        RAISE EXCEPTION 'verified_payment_cannot_be_failed';
    END IF;
    IF v_order.status IN ('expired_unpaid', 'failed', 'manual_review') THEN
        RETURN jsonb_build_object('replayed', TRUE, 'status', v_order.status);
    END IF;
    IF v_order.status NOT IN ('quoted', 'submitted') THEN
        RAISE EXCEPTION 'invalid_order_state';
    END IF;

    -- A failed observation is scoped to this order and never consumes the signature.
    IF p_observed_signature IS NOT NULL THEN
        INSERT INTO public.ram_payment_attempts(order_id, signature, status, error_code)
        VALUES (v_order.id, p_observed_signature, 'failed', p_failure_code)
        ON CONFLICT (order_id, signature) DO UPDATE
           SET status = 'failed', error_code = EXCLUDED.error_code, observed_at = now();
    END IF;

    UPDATE public.ram_purchase_orders
       SET status = p_state,
           failure_code = p_failure_code,
           lease_owner = NULL,
           lease_expires_at = NULL,
           updated_at = now()
     WHERE id = v_order.id;
    RETURN jsonb_build_object('replayed', FALSE, 'status', p_state, 'failure_code', p_failure_code);
END;
$$;

REVOKE ALL ON FUNCTION public.ram_shop_mark_payment_verified(UUID, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_mark_order_terminal(UUID, TEXT, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_shop_mark_payment_verified(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_mark_order_terminal(UUID, TEXT, TEXT, TEXT) TO service_role;
