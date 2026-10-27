-- Unified move inventory: one durable move_items row is the playable asset.
-- Known rows and ownership operations are immutable provenance for that row.

ALTER TABLE public.move_items
    ADD COLUMN IF NOT EXISTS ownership_known_id UUID
        REFERENCES public.ownership_known_moves(known_id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS stamp_operation_id UUID
        REFERENCES public.ownership_stamp_operations(operation_id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS bind_operation_id UUID
        REFERENCES public.ownership_bind_operations(operation_id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS bind_sealed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS modifier1_effect_value NUMERIC(12, 4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS modifier1_effect_value_locked BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS modifier2_effect_value NUMERIC(12, 4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS modifier2_effect_value_locked BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE public.move_items
    DROP CONSTRAINT IF EXISTS move_items_ownership_known_id_key,
    ADD CONSTRAINT move_items_ownership_known_id_key UNIQUE (ownership_known_id),
    DROP CONSTRAINT IF EXISTS move_items_stamp_operation_id_key,
    ADD CONSTRAINT move_items_stamp_operation_id_key UNIQUE (stamp_operation_id),
    DROP CONSTRAINT IF EXISTS move_items_bind_operation_id_key,
    ADD CONSTRAINT move_items_bind_operation_id_key UNIQUE (bind_operation_id);

CREATE INDEX IF NOT EXISTS idx_move_items_inventory_projection
    ON public.move_items (player_profile_id, created_at, id);
CREATE INDEX IF NOT EXISTS idx_move_items_bound_assets
    ON public.move_items (player_profile_id, exported_asset_id)
    WHERE exported_asset_id IS NOT NULL AND bind_sealed_at IS NULL;

-- Fail closed during history repair. A new canonical item is created for a
-- Known only from that Known's own id; no legacy move_items row is paired by
-- name, timestamp, or source. Operators can repair those ambiguous rows later.
INSERT INTO public.move_items (
    player_profile_id,
    tier,
    move_id,
    modifier1_id,
    modifier1_effect_value,
    modifier1_effect_value_locked,
    modifier2_id,
    modifier2_effect_value,
    modifier2_effect_value_locked,
    locked,
    exported_asset_id,
    source,
    ownership_known_id,
    stamp_operation_id
)
SELECT
    known.player_profile_id,
    CASE WHEN stamp.operation_id IS NULL THEN 'ordinal'::public.move_tier ELSE 'inscription'::public.move_tier END,
    known.move_id,
    CASE WHEN stamp.operation_id IS NULL THEN NULL ELSE stamp.modifier_ids[1] END,
    COALESCE(stamp.modifier_effect_values[1], 0),
    stamp.operation_id IS NOT NULL AND stamp.modifier_effect_values[1] IS NOT NULL,
    CASE WHEN stamp.modifier_count = 2 THEN stamp.modifier_ids[2] ELSE NULL END,
    COALESCE(stamp.modifier_effect_values[2], 0),
    stamp.operation_id IS NOT NULL AND stamp.modifier_count = 2 AND stamp.modifier_effect_values[2] IS NOT NULL,
    FALSE,
    CASE WHEN stamp.operation_id IS NULL THEN NULL ELSE stamp.stamped_asset_address END,
    'ownership_known_backfill',
    known.known_id,
    stamp.operation_id
FROM public.ownership_known_moves AS known
LEFT JOIN LATERAL (
    SELECT operation_id, modifier_count, modifier_ids, modifier_effect_values, stamped_asset_address
    FROM public.ownership_stamp_operations
    WHERE known_id = known.known_id
      AND player_profile_id = known.player_profile_id
      AND status = 'confirmed'
    ORDER BY confirmed_at DESC NULLS LAST, created_at DESC
    LIMIT 1
) AS stamp ON TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM public.move_items existing
    WHERE existing.ownership_known_id = known.known_id
)
ON CONFLICT (ownership_known_id) DO NOTHING;

-- The on-chain program defines kind 0 as COMPONENT_STAMPED_MOVE. Only that
-- component kind, and only an exact stamped asset match, seals a move.
UPDATE public.move_items AS item
SET bind_operation_id = binding.operation_id,
    bind_sealed_at = binding.bound_at
FROM public.ownership_chainling_bindings AS binding
WHERE binding.component_kind = 0
  AND item.player_profile_id = binding.player_profile_id
  AND item.exported_asset_id = binding.component_asset_address
  AND item.bind_sealed_at IS NULL;

CREATE OR REPLACE FUNCTION public.seal_bound_move_item()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
    IF NEW.component_kind = 0 THEN
        UPDATE public.move_items
           SET bind_operation_id = NEW.operation_id,
               bind_sealed_at = NEW.bound_at
         WHERE player_profile_id = NEW.player_profile_id
           AND exported_asset_id = NEW.component_asset_address
           AND bind_sealed_at IS NULL;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_seal_bound_move_item ON public.ownership_chainling_bindings;
CREATE TRIGGER trg_seal_bound_move_item
    AFTER INSERT ON public.ownership_chainling_bindings
    FOR EACH ROW EXECUTE FUNCTION public.seal_bound_move_item();

REVOKE ALL ON FUNCTION public.seal_bound_move_item() FROM PUBLIC, anon, authenticated;

-- Preserve the existing settlement implementations as private callees, then
-- make their legacy result update the exact move item in the same transaction.
ALTER FUNCTION public.ownership_settle_known_reserved(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, NUMERIC, INTEGER, INTEGER, INTEGER, TEXT
) RENAME TO ownership_settle_known_reserved_legacy;

CREATE FUNCTION public.ownership_settle_known_reserved(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_chain_signature TEXT,
    p_receipt_address TEXT,
    p_idempotency_key TEXT,
    p_move_id TEXT,
    p_onchain_move_id INTEGER,
    p_onchain_instance_id NUMERIC,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_result JSONB;
    v_known_id UUID;
    v_move_item_id UUID;
BEGIN
    v_result := public.ownership_settle_known_reserved_legacy(
        p_operation_id, p_player_profile_id, p_cluster, p_chain_signature,
        p_receipt_address, p_idempotency_key, p_move_id, p_onchain_move_id,
        p_onchain_instance_id, p_element, p_base_power, p_generator_version, p_pool_hash
    );
    v_known_id := NULLIF(v_result -> 'known' ->> 'known_id', '')::UUID;

    INSERT INTO public.move_items (
        player_profile_id, tier, move_id, modifier1_id, modifier2_id,
        locked, exported_asset_id, source, ownership_known_id
    ) VALUES (
        p_player_profile_id, 'ordinal'::public.move_tier, p_move_id, NULL, NULL,
        FALSE, NULL, 'candy_roll', v_known_id
    )
    ON CONFLICT (ownership_known_id) DO UPDATE
       SET move_id = EXCLUDED.move_id
    RETURNING id INTO v_move_item_id;

    RETURN v_result || jsonb_build_object('move_item_id', v_move_item_id);
END;
$$;

ALTER FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) RENAME TO ownership_confirm_stamp_reserved_legacy;

CREATE FUNCTION public.ownership_confirm_stamp_reserved(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_idempotency_key TEXT,
    p_known_id UUID,
    p_signature TEXT,
    p_verified_asset_json JSONB,
    p_onchain_instance_id NUMERIC,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_result JSONB;
    v_operation public.ownership_stamp_operations%ROWTYPE;
    v_move_item_id UUID;
BEGIN
    v_result := public.ownership_confirm_stamp_reserved_legacy(
        p_operation_id, p_player_profile_id, p_cluster, p_idempotency_key,
        p_known_id, p_signature, p_verified_asset_json, p_onchain_instance_id, p_receipt_address
    );

    SELECT * INTO v_operation
      FROM public.ownership_stamp_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
       AND known_id = p_known_id
       AND status = 'confirmed'
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'confirmed_stamp_operation_not_found'; END IF;

    UPDATE public.move_items
       SET tier = 'inscription'::public.move_tier,
           modifier1_id = v_operation.modifier_ids[1],
           modifier1_effect_value = COALESCE(v_operation.modifier_effect_values[1], 0),
           modifier1_effect_value_locked = v_operation.modifier_effect_values[1] IS NOT NULL,
           modifier2_id = CASE WHEN v_operation.modifier_count = 2 THEN v_operation.modifier_ids[2] ELSE NULL END,
           modifier2_effect_value = COALESCE(v_operation.modifier_effect_values[2], 0),
           modifier2_effect_value_locked = v_operation.modifier_count = 2 AND v_operation.modifier_effect_values[2] IS NOT NULL,
           exported_asset_id = v_operation.stamped_asset_address,
           stamp_operation_id = v_operation.operation_id
     WHERE player_profile_id = p_player_profile_id
       AND ownership_known_id = p_known_id
    RETURNING id INTO v_move_item_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'move_item_not_found_for_known'; END IF;

    RETURN v_result || jsonb_build_object('move_item_id', v_move_item_id);
END;
$$;

ALTER FUNCTION public.ownership_grant_event_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, TEXT
) RENAME TO ownership_grant_event_known_atomic_legacy;

CREATE FUNCTION public.ownership_grant_event_known_atomic(
    p_player_profile_id UUID,
    p_idempotency_key TEXT,
    p_move_id TEXT,
    p_onchain_move_id INTEGER,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT,
    p_source TEXT DEFAULT 'event_grant'
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_result JSONB;
    v_known_id UUID;
    v_move_item_id UUID;
BEGIN
    v_result := public.ownership_grant_event_known_atomic_legacy(
        p_player_profile_id, p_idempotency_key, p_move_id, p_onchain_move_id,
        p_element, p_base_power, p_generator_version, p_pool_hash, p_source
    );
    v_known_id := NULLIF(v_result -> 'known' ->> 'known_id', '')::UUID;
    v_move_item_id := NULLIF(v_result ->> 'move_item_id', '')::UUID;
    UPDATE public.move_items
       SET ownership_known_id = v_known_id
     WHERE id = v_move_item_id
       AND player_profile_id = p_player_profile_id
       AND ownership_known_id IS NULL;
    IF NOT FOUND AND NOT EXISTS (
        SELECT 1 FROM public.move_items
         WHERE id = v_move_item_id AND ownership_known_id = v_known_id
    ) THEN RAISE EXCEPTION 'event_known_move_item_link_failed'; END IF;
    RETURN v_result;
END;
$$;

ALTER FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, TEXT
) RENAME TO profile_grant_progression_ordinal_tx_legacy;

CREATE FUNCTION public.profile_grant_progression_ordinal_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_move_id TEXT,
    p_source_event_id TEXT,
    p_idempotency_key TEXT,
    p_event_type TEXT,
    p_star_index INTEGER,
    p_mastery_cycle_index INTEGER,
    p_battle_seed INTEGER,
    p_onchain_move_id INTEGER,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_result JSONB;
    v_known_id UUID;
    v_move_item_id UUID;
BEGIN
    v_result := public.profile_grant_progression_ordinal_tx_legacy(
        p_profile_id, p_chainling_id, p_move_id, p_source_event_id,
        p_idempotency_key, p_event_type, p_star_index, p_mastery_cycle_index,
        p_battle_seed, p_onchain_move_id, p_element, p_base_power,
        p_generator_version, p_pool_hash
    );
    v_known_id := NULLIF(v_result ->> 'known_id', '')::UUID;
    v_move_item_id := NULLIF(v_result ->> 'move_item_id', '')::UUID;
    UPDATE public.move_items
       SET ownership_known_id = v_known_id
     WHERE id = v_move_item_id
       AND player_profile_id = p_profile_id
       AND ownership_known_id IS NULL;
    IF NOT FOUND AND NOT EXISTS (
        SELECT 1 FROM public.move_items
         WHERE id = v_move_item_id AND ownership_known_id = v_known_id
    ) THEN RAISE EXCEPTION 'progression_known_move_item_link_failed'; END IF;
    RETURN v_result;
END;
$$;

-- One lock per Chainling makes capacity, last-move protection, and slot writes
-- serializable. Locking the move item also prevents two Chainlings claiming it.
CREATE FUNCTION public.profile_set_loadout_atomic(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_slot INTEGER,
    p_move_item_id UUID DEFAULT NULL
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_rebirth_count INTEGER;
    v_max_slots INTEGER;
    v_equipped_count INTEGER;
    v_current_move_item_id UUID;
    v_target_slot_move_item_id UUID;
    v_move public.move_items%ROWTYPE;
    v_assigned_chainling_id UUID;
    v_target_move_id TEXT;
    v_passive_count INTEGER;
BEGIN
    IF p_chainling_id IS NULL OR p_slot IS NULL OR p_slot < 0 OR p_slot >= 16 THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'invalid_loadout_slot');
    END IF;

    PERFORM pg_advisory_xact_lock(hashtext(p_chainling_id::TEXT));
    SELECT rebirth_count INTO v_rebirth_count
      FROM public.chainlings
     WHERE id = p_chainling_id AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'chainling_not_found_or_not_owned');
    END IF;
    v_max_slots := LEAST(9, GREATEST(4, 4 + LEAST(5, GREATEST(0, COALESCE(v_rebirth_count, 0)))));
    IF p_slot >= v_max_slots THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'slot_exceeds_chainling_capacity', 'max_slots', v_max_slots);
    END IF;

    SELECT move_item_id INTO v_current_move_item_id
      FROM public.loadouts
     WHERE chainling_id = p_chainling_id AND slot = p_slot
     FOR UPDATE;

    IF p_move_item_id IS NULL THEN
        IF v_current_move_item_id IS NULL THEN
            RETURN jsonb_build_object('success', TRUE, 'chainling_id', p_chainling_id, 'slot', p_slot, 'move_item_id', '');
        END IF;
        SELECT COUNT(*)::INTEGER INTO v_equipped_count
          FROM public.loadouts
         WHERE chainling_id = p_chainling_id AND move_item_id IS NOT NULL;
        IF v_equipped_count <= 1 THEN
            RETURN jsonb_build_object('success', FALSE, 'error', 'last_move_required');
        END IF;
        SELECT * INTO v_move FROM public.move_items WHERE id = v_current_move_item_id FOR UPDATE;
        IF v_move.bind_sealed_at IS NOT NULL THEN
            RETURN jsonb_build_object('success', FALSE, 'error', 'bind_sealed');
        END IF;
        IF COALESCE(v_move.locked, FALSE) THEN
            RETURN jsonb_build_object('success', FALSE, 'error', 'move_escrowed');
        END IF;
        DELETE FROM public.loadouts WHERE chainling_id = p_chainling_id AND slot = p_slot;
        RETURN jsonb_build_object('success', TRUE, 'chainling_id', p_chainling_id, 'slot', p_slot, 'move_item_id', '');
    END IF;

    SELECT * INTO v_move
      FROM public.move_items
     WHERE id = p_move_item_id AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'move_item_not_found');
    END IF;
    IF v_move.bind_sealed_at IS NOT NULL THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'bind_sealed');
    END IF;
    IF COALESCE(v_move.locked, FALSE) THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'move_escrowed');
    END IF;

    IF v_current_move_item_id IS NOT NULL AND v_current_move_item_id <> p_move_item_id THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'target_slot_occupied');
    END IF;
    IF v_current_move_item_id = p_move_item_id THEN
        RETURN jsonb_build_object('success', TRUE, 'chainling_id', p_chainling_id, 'slot', p_slot, 'move_item_id', p_move_item_id);
    END IF;

    SELECT chainling_id INTO v_assigned_chainling_id
      FROM public.loadouts
     WHERE move_item_id = p_move_item_id
     LIMIT 1
     FOR UPDATE;
    IF FOUND THEN
        IF v_assigned_chainling_id <> p_chainling_id THEN
            RETURN jsonb_build_object('success', FALSE, 'error', 'move_equipped_elsewhere');
        END IF;
        RETURN jsonb_build_object('success', FALSE, 'error', 'move_already_equipped');
    END IF;

    v_target_move_id := LOWER(BTRIM(COALESCE(v_move.move_id, '')));
    IF EXISTS (
        SELECT 1
          FROM public.loadouts l
          JOIN public.move_items equipped ON equipped.id = l.move_item_id
         WHERE l.chainling_id = p_chainling_id
           AND LOWER(BTRIM(COALESCE(equipped.move_id, ''))) = v_target_move_id
    ) THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'duplicate_move_id');
    END IF;

    SELECT COUNT(*)::INTEGER INTO v_passive_count
      FROM public.loadouts l
      JOIN public.move_items equipped ON equipped.id = l.move_item_id
     WHERE l.chainling_id = p_chainling_id
       AND LOWER(BTRIM(COALESCE(equipped.move_id, ''))) = ANY (ARRAY[
           'turn_start_ap_gain', 'on_hit_momentum', 'on_crit_bonus', 'defensive_reflex',
           'sustain_regen', 'status_resistance', 'emergency_recovery', 'precision_focus',
           'reactive_ping', 'low_ap_surge', 'spite_counter', 'desperation_surge',
           'fee_collector', 'immutable_ledger', 'firewall_passive', 'conductive_counter',
           'extraction_hit', 'patience_passive', 'stake_refund', 'streamlined_flow',
           'automated_pressure', 'unstable_code', 'parallel_passive', 'shattered_armor',
           'double_or_nothing', 'meme_momentum', 'gecko_guard'
       ]);
    IF v_target_move_id = ANY (ARRAY[
        'turn_start_ap_gain', 'on_hit_momentum', 'on_crit_bonus', 'defensive_reflex',
        'sustain_regen', 'status_resistance', 'emergency_recovery', 'precision_focus',
        'reactive_ping', 'low_ap_surge', 'spite_counter', 'desperation_surge',
        'fee_collector', 'immutable_ledger', 'firewall_passive', 'conductive_counter',
        'extraction_hit', 'patience_passive', 'stake_refund', 'streamlined_flow',
        'automated_pressure', 'unstable_code', 'parallel_passive', 'shattered_armor',
        'double_or_nothing', 'meme_momentum', 'gecko_guard'
    ]) AND v_passive_count >= 2 THEN
        RETURN jsonb_build_object('success', FALSE, 'error', 'passive_cap_exceeded', 'max_passives', 2);
    END IF;

    INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
    VALUES (p_chainling_id, p_slot, p_move_item_id);
    RETURN jsonb_build_object('success', TRUE, 'chainling_id', p_chainling_id, 'slot', p_slot, 'move_item_id', p_move_item_id);
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_settle_known_reserved_legacy(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, NUMERIC, INTEGER, INTEGER, INTEGER, TEXT
) FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON FUNCTION public.ownership_confirm_stamp_reserved_legacy(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON FUNCTION public.ownership_grant_event_known_atomic_legacy(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, TEXT
) FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON FUNCTION public.profile_grant_progression_ordinal_tx_legacy(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, TEXT
) FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON FUNCTION public.ownership_settle_known_reserved(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, NUMERIC, INTEGER, INTEGER, INTEGER, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_grant_event_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.profile_set_loadout_atomic(UUID, UUID, INTEGER, UUID)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_settle_known_reserved(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, NUMERIC, INTEGER, INTEGER, INTEGER, TEXT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_grant_event_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, TEXT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, TEXT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_set_loadout_atomic(UUID, UUID, INTEGER, UUID)
    TO service_role;
