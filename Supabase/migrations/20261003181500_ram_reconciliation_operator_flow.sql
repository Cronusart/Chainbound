-- Explicit, replay-safe operator recovery for legacy RAM mismatches.
-- Reconciliation moves the on-chain balance by an audited delta; it never
-- overwrites either source of truth.

CREATE UNIQUE INDEX IF NOT EXISTS ram_reconciliation_signature_unique
    ON public.ram_reconciliation_cases(cluster, chain_signature)
    WHERE chain_signature IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ram_reconciliation_receipt_unique
    ON public.ram_reconciliation_cases(cluster, receipt_address)
    WHERE receipt_address IS NOT NULL;

CREATE OR REPLACE FUNCTION public.ram_reconciliation_approve(
    p_case_id UUID,
    p_direction TEXT,
    p_amount BIGINT,
    p_reason TEXT,
    p_approved_by TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_case public.ram_reconciliation_cases%ROWTYPE;
    v_profile_balance BIGINT;
    v_expected_direction TEXT;
    v_expected_amount BIGINT;
BEGIN
    IF NULLIF(BTRIM(p_reason), '') IS NULL OR NULLIF(BTRIM(p_approved_by), '') IS NULL THEN
        RAISE EXCEPTION 'reconciliation_approval_details_required';
    END IF;
    SELECT * INTO v_case FROM public.ram_reconciliation_cases
     WHERE case_id = p_case_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'reconciliation_case_not_found'; END IF;
    IF v_case.status = 'settled' THEN
        RETURN jsonb_build_object('state', 'settled', 'replayed', TRUE);
    END IF;
    IF v_case.status NOT IN ('open', 'approved', 'chain_submitted') THEN
        RAISE EXCEPTION 'reconciliation_case_not_approvable';
    END IF;

    SELECT ram_balance INTO v_profile_balance FROM public.player_profiles
     WHERE player_profile_id = v_case.player_profile_id FOR UPDATE;
    IF v_profile_balance IS DISTINCT FROM v_case.profile_balance THEN
        RAISE EXCEPTION 'profile_balance_changed_since_audit';
    END IF;
    PERFORM 1 FROM public.ram_ledger_accounts
     WHERE player_profile_id = v_case.player_profile_id
       AND status = 'frozen_reconciliation' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_not_frozen'; END IF;

    v_expected_direction := CASE WHEN v_case.profile_balance > v_case.chain_balance
        THEN 'credit_chain' ELSE 'debit_chain' END;
    v_expected_amount := ABS(v_case.profile_balance - v_case.chain_balance);
    IF v_expected_amount = 0 THEN RAISE EXCEPTION 'reconciliation_delta_not_required'; END IF;
    IF p_direction IS DISTINCT FROM v_expected_direction OR p_amount IS DISTINCT FROM v_expected_amount THEN
        RAISE EXCEPTION 'reconciliation_delta_mismatch';
    END IF;
    IF v_case.status <> 'open' AND (
        v_case.direction IS DISTINCT FROM p_direction OR
        v_case.amount IS DISTINCT FROM p_amount OR
        v_case.reason IS DISTINCT FROM BTRIM(p_reason) OR
        v_case.approved_by IS DISTINCT FROM BTRIM(p_approved_by)
    ) THEN RAISE EXCEPTION 'reconciliation_approval_conflict'; END IF;

    UPDATE public.ram_reconciliation_cases
       SET status = CASE WHEN status = 'open' THEN 'approved' ELSE status END,
           direction = p_direction,
           amount = p_amount,
           reason = BTRIM(p_reason),
           approved_by = BTRIM(p_approved_by),
           updated_at = now()
     WHERE id = v_case.id;
    RETURN jsonb_build_object('state', 'approved', 'replayed', v_case.status <> 'open');
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_reconciliation_mark_submitted(
    p_case_id UUID,
    p_signature TEXT,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_case public.ram_reconciliation_cases%ROWTYPE;
BEGIN
    IF NULLIF(BTRIM(p_signature), '') IS NULL OR NULLIF(BTRIM(p_receipt_address), '') IS NULL THEN
        RAISE EXCEPTION 'reconciliation_chain_evidence_required';
    END IF;
    SELECT * INTO v_case FROM public.ram_reconciliation_cases
     WHERE case_id = p_case_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'reconciliation_case_not_found'; END IF;
    IF v_case.status = 'settled' THEN
        RETURN jsonb_build_object('state', 'settled', 'replayed', TRUE);
    END IF;
    IF v_case.status NOT IN ('approved', 'chain_submitted') THEN
        RAISE EXCEPTION 'reconciliation_case_not_submittable';
    END IF;
    IF (v_case.chain_signature IS NOT NULL AND v_case.chain_signature <> p_signature)
       OR (v_case.receipt_address IS NOT NULL AND v_case.receipt_address <> p_receipt_address) THEN
        RAISE EXCEPTION 'reconciliation_chain_evidence_conflict';
    END IF;
    UPDATE public.ram_reconciliation_cases
       SET status = 'chain_submitted',
           chain_signature = COALESCE(chain_signature, p_signature),
           receipt_address = COALESCE(receipt_address, p_receipt_address),
           updated_at = now()
     WHERE id = v_case.id;
    RETURN jsonb_build_object('state', 'chain_submitted', 'replayed', v_case.status = 'chain_submitted');
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_reconciliation_settle(
    p_case_id UUID,
    p_signature TEXT,
    p_receipt_address TEXT,
    p_balance_before BIGINT,
    p_balance_after BIGINT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_case public.ram_reconciliation_cases%ROWTYPE;
    v_profile_balance BIGINT;
BEGIN
    SELECT * INTO v_case FROM public.ram_reconciliation_cases
     WHERE case_id = p_case_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'reconciliation_case_not_found'; END IF;
    IF v_case.status = 'settled' THEN
        IF v_case.chain_signature IS DISTINCT FROM p_signature OR
           v_case.receipt_address IS DISTINCT FROM p_receipt_address THEN
            RAISE EXCEPTION 'reconciliation_settlement_conflict';
        END IF;
        RETURN jsonb_build_object('state', 'settled', 'replayed', TRUE);
    END IF;
    IF v_case.status <> 'chain_submitted' OR
       v_case.chain_signature IS DISTINCT FROM p_signature OR
       v_case.receipt_address IS DISTINCT FROM p_receipt_address THEN
        RAISE EXCEPTION 'reconciliation_not_submitted';
    END IF;
    SELECT ram_balance INTO v_profile_balance FROM public.player_profiles
     WHERE player_profile_id = v_case.player_profile_id FOR UPDATE;
    IF v_profile_balance IS DISTINCT FROM v_case.profile_balance THEN
        RAISE EXCEPTION 'profile_balance_changed_since_audit';
    END IF;
    IF p_balance_before IS DISTINCT FROM v_case.chain_balance OR
       p_balance_after IS DISTINCT FROM v_case.profile_balance THEN
        RAISE EXCEPTION 'reconciliation_receipt_balance_mismatch';
    END IF;

    UPDATE public.ram_reconciliation_cases
       SET status = 'settled', settled_at = now(), updated_at = now()
     WHERE id = v_case.id;
    UPDATE public.ram_ledger_accounts
       SET status = 'active',
           profile_balance_snapshot = v_case.profile_balance,
           chain_balance_snapshot = p_balance_after,
           last_audited_at = now(), updated_at = now()
     WHERE player_profile_id = v_case.player_profile_id;
    RETURN jsonb_build_object('state', 'settled', 'replayed', FALSE, 'balance', p_balance_after);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_reconciliation_reject(
    p_case_id UUID,
    p_reason TEXT,
    p_approved_by TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_case public.ram_reconciliation_cases%ROWTYPE;
BEGIN
    IF NULLIF(BTRIM(p_reason), '') IS NULL OR NULLIF(BTRIM(p_approved_by), '') IS NULL THEN
        RAISE EXCEPTION 'reconciliation_rejection_details_required';
    END IF;
    SELECT * INTO v_case FROM public.ram_reconciliation_cases
     WHERE case_id = p_case_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'reconciliation_case_not_found'; END IF;
    IF v_case.status = 'rejected' THEN
        RETURN jsonb_build_object('state', 'rejected', 'replayed', TRUE);
    END IF;
    IF v_case.status <> 'open' THEN RAISE EXCEPTION 'reconciliation_case_not_rejectable'; END IF;
    UPDATE public.ram_reconciliation_cases
       SET status = 'rejected', reason = BTRIM(p_reason), approved_by = BTRIM(p_approved_by), updated_at = now()
     WHERE id = v_case.id;
    RETURN jsonb_build_object('state', 'rejected', 'replayed', FALSE);
END;
$$;

REVOKE ALL ON FUNCTION public.ram_reconciliation_approve(UUID, TEXT, BIGINT, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_reconciliation_mark_submitted(UUID, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_reconciliation_settle(UUID, TEXT, TEXT, BIGINT, BIGINT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_reconciliation_reject(UUID, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_reconciliation_approve(UUID, TEXT, BIGINT, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_reconciliation_mark_submitted(UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_reconciliation_settle(UUID, TEXT, TEXT, BIGINT, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_reconciliation_reject(UUID, TEXT, TEXT) TO service_role;

-- Direct legacy RAM writers remain in schema only for migration history. They
-- are deliberately unreachable from production services.
REVOKE ALL ON FUNCTION public.profile_upgrade_move_item_tx(
    UUID, UUID, public.move_tier, TEXT, TEXT, INTEGER, TEXT
) FROM service_role;
REVOKE ALL ON FUNCTION public.ownership_confirm_devnet_smoke_grant_atomic(
    UUID, UUID, TEXT, INTEGER, INTEGER, NUMERIC, NUMERIC
) FROM service_role;
REVOKE ALL ON FUNCTION public.ownership_reconcile_operator_devnet_ram_topup_atomic(
    UUID, UUID, TEXT, INTEGER, INTEGER, INTEGER, NUMERIC, NUMERIC
) FROM service_role;

ALTER TABLE public.ram_ledger_accounts
    ADD COLUMN IF NOT EXISTS audit_lease_owner TEXT NULL,
    ADD COLUMN IF NOT EXISTS audit_lease_expires_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS audit_after TIMESTAMPTZ NOT NULL DEFAULT now();

CREATE OR REPLACE FUNCTION public.ram_ledger_claim_audit_batch(
    p_worker_id TEXT,
    p_limit INTEGER DEFAULT 20,
    p_lease_seconds INTEGER DEFAULT 60
) RETURNS SETOF public.ram_ledger_accounts
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
    IF NULLIF(BTRIM(p_worker_id), '') IS NULL OR p_limit < 1 OR p_limit > 100 OR p_lease_seconds < 1 THEN
        RAISE EXCEPTION 'invalid_audit_lease_request';
    END IF;
    RETURN QUERY
    UPDATE public.ram_ledger_accounts a
       SET audit_lease_owner = p_worker_id,
           audit_lease_expires_at = now() + make_interval(secs => p_lease_seconds),
           updated_at = now()
     WHERE a.player_profile_id IN (
        SELECT q.player_profile_id
          FROM public.ram_ledger_accounts q
         WHERE q.status = 'pending_audit'
           AND q.audit_after <= now()
           AND (q.audit_lease_expires_at IS NULL OR q.audit_lease_expires_at < now())
         ORDER BY q.updated_at, q.player_profile_id
         LIMIT p_limit
         FOR UPDATE SKIP LOCKED
     )
    RETURNING a.*;
END;
$$;

REVOKE ALL ON FUNCTION public.ram_ledger_claim_audit_batch(TEXT, INTEGER, INTEGER)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_ledger_claim_audit_batch(TEXT, INTEGER, INTEGER) TO service_role;
