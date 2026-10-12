-- Explicit finalized-chain transition shared by receipt-backed RAM spends.
-- Canonical remote migration version: 20261006110728.

CREATE OR REPLACE FUNCTION public.ram_ledger_mark_chain_confirmed(
    p_operation_id UUID,
    p_cluster TEXT,
    p_chain_signature TEXT,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_operation public.ram_ledger_operations%ROWTYPE;
BEGIN
    IF NULLIF(BTRIM(p_chain_signature), '') IS NULL
       OR NULLIF(BTRIM(p_receipt_address), '') IS NULL THEN
        RAISE EXCEPTION 'finalized_chain_evidence_required';
    END IF;
    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE operation_id = p_operation_id
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'operation_not_found'; END IF;
    IF v_operation.state = 'settled' THEN
        IF v_operation.chain_signature IS DISTINCT FROM p_chain_signature
           OR v_operation.receipt_address IS DISTINCT FROM p_receipt_address THEN
            RAISE EXCEPTION 'settled_operation_evidence_conflict';
        END IF;
        RETURN jsonb_build_object('state', 'settled', 'replayed', TRUE);
    END IF;
    IF v_operation.state NOT IN ('reserved', 'chain_submitted', 'chain_confirmed') THEN
        RAISE EXCEPTION 'operation_not_confirmable';
    END IF;
    IF v_operation.receipt_address IS NOT NULL
       AND v_operation.receipt_address IS DISTINCT FROM p_receipt_address THEN
        RAISE EXCEPTION 'operation_receipt_conflict';
    END IF;
    UPDATE public.ram_ledger_operations
       SET state = 'chain_confirmed',
           chain_signature = p_chain_signature,
           receipt_address = p_receipt_address,
           updated_at = now()
     WHERE id = v_operation.id;
    RETURN jsonb_build_object(
        'state', 'chain_confirmed',
        'replayed', v_operation.state = 'chain_confirmed'
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ram_ledger_mark_chain_confirmed(UUID, TEXT, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_ledger_mark_chain_confirmed(UUID, TEXT, TEXT, TEXT)
    TO service_role;
