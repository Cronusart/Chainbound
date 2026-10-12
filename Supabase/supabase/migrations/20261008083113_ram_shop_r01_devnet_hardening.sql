-- RAM Shop R0.1: Devnet-only quote scope, verified-only payment evidence,
-- Canonical remote migration version: 20261008083113.
-- terminal mismatch audit, and expired-quote recovery.

ALTER TABLE public.ram_shop_settings
    ADD COLUMN IF NOT EXISTS checkout_cluster TEXT NULL
        CHECK (checkout_cluster IN ('devnet', 'mainnet-beta'));

-- Mainnet safety (edited 2026-10-08, after this version was applied to Devnet;
-- Devnet does not re-run it): the Devnet-only statements in this file run only
-- when the database is explicitly identified as Devnet. Otherwise they are
-- skipped with a NOTICE (fail closed). Accepted Devnet markers, either one:
--   1. Explicit opt-in: chainbound.cluster = 'devnet' in the session applying
--      this file, e.g. SET LOCAL chainbound.cluster = 'devnet'; or, for a
--      CLI-driven Devnet rebuild, ALTER DATABASE postgres SET chainbound.cluster = 'devnet';
--   2. public.hub_settings.cluster = 'devnet'. That column defaults to '' and is
--      operator-set; no migration ever sets it to 'devnet'.
DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
           INTO v_is_devnet;
    END IF;
    IF NOT v_is_devnet THEN
        RAISE NOTICE '20261008083113_ram_shop_r01_devnet_hardening checkout_cluster skipped: database is not marked as Devnet';
        RETURN;
    END IF;

    UPDATE public.ram_shop_settings
       SET checkout_cluster = 'devnet',
           quote_ttl_seconds = 60,
           updated_at = now()
     WHERE singleton;
END
$devnet_guard$;

CREATE OR REPLACE FUNCTION public.ram_shop_record_submission(
    p_order_id UUID,
    p_signature TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE v_order public.ram_purchase_orders;
BEGIN
    SELECT * INTO v_order
      FROM public.ram_purchase_orders
     WHERE order_id = p_order_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.status NOT IN ('quoted', 'submitted') THEN
        RAISE EXCEPTION 'invalid_order_state';
    END IF;

    INSERT INTO public.ram_payment_attempts(order_id, signature, status)
    VALUES (v_order.id, p_signature, 'submitted')
    ON CONFLICT (order_id, signature) DO UPDATE
       SET observed_at = now();

    UPDATE public.ram_purchase_orders
       SET status = 'submitted', reconcile_after = now(), updated_at = now()
     WHERE id = v_order.id;
    RETURN jsonb_build_object('status', 'submitted');
END;
$$;

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
    v_consuming_order BIGINT;
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

    SELECT order_id INTO v_consuming_order
      FROM public.ram_payment_attempts
     WHERE signature = p_signature
     FOR UPDATE;
    IF FOUND AND v_consuming_order <> v_order.id THEN
        RAISE EXCEPTION 'signature_already_consumed';
    END IF;

    IF v_order.payment_signature IS NOT NULL AND v_order.payment_signature <> p_signature THEN
        UPDATE public.ram_payment_attempts
           SET status = 'failed', error_code = 'replaced_unverified_legacy_signature', observed_at = now()
         WHERE order_id = v_order.id
           AND signature = v_order.payment_signature
           AND status <> 'verified';
    END IF;

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

    IF p_observed_signature IS NOT NULL THEN
        INSERT INTO public.ram_payment_attempts(order_id, signature, status, error_code)
        VALUES (v_order.id, p_observed_signature, 'failed', p_failure_code)
        ON CONFLICT (signature) DO UPDATE
           SET status = 'failed', error_code = EXCLUDED.error_code, observed_at = now()
         WHERE public.ram_payment_attempts.order_id = EXCLUDED.order_id;
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

CREATE OR REPLACE FUNCTION public.ram_shop_claim_reconcile_batch(
    p_worker_id TEXT,
    p_lease_seconds INTEGER DEFAULT 30,
    p_limit INTEGER DEFAULT 20
) RETURNS SETOF public.ram_purchase_orders
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
BEGIN
    IF p_worker_id IS NULL OR btrim(p_worker_id) = '' OR p_lease_seconds < 1 OR p_limit < 1 OR p_limit > 100 THEN
        RAISE EXCEPTION 'invalid_worker_request';
    END IF;
    RETURN QUERY
    UPDATE public.ram_purchase_orders o
       SET lease_owner = p_worker_id,
           lease_expires_at = now() + make_interval(secs => p_lease_seconds),
           updated_at = now()
     WHERE o.id IN (
        SELECT q.id
          FROM public.ram_purchase_orders q
         WHERE (
                   q.status IN ('submitted', 'payment_verified', 'credit_submitted')
                OR (q.status = 'quoted' AND q.quote_expires_at <= now())
               )
           AND q.reconcile_after <= now()
           AND (q.lease_expires_at IS NULL OR q.lease_expires_at < now())
         ORDER BY q.reconcile_after, q.id
         LIMIT p_limit
         FOR UPDATE SKIP LOCKED
     )
    RETURNING o.*;
END;
$$;

DROP INDEX IF EXISTS public.ram_purchase_orders_reconcile_idx;
CREATE INDEX ram_purchase_orders_reconcile_idx
    ON public.ram_purchase_orders (status, reconcile_after, id)
    WHERE status IN ('quoted', 'submitted', 'payment_verified', 'credit_submitted');

REVOKE ALL ON FUNCTION public.ram_shop_record_submission(UUID, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_mark_payment_verified(UUID, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_mark_order_terminal(UUID, TEXT, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_claim_reconcile_batch(TEXT, INTEGER, INTEGER)
    FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.ram_shop_record_submission(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_mark_payment_verified(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_mark_order_terminal(UUID, TEXT, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_claim_reconcile_batch(TEXT, INTEGER, INTEGER) TO service_role;

COMMENT ON COLUMN public.ram_shop_settings.checkout_cluster IS
'Explicit database-side checkout cluster guard; R0.1 is configured for Devnet only.';
