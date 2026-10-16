-- Bindery Phase 1: service-only source lifecycle, inspection evidence, and
-- canonical archive projection. No source is inserted or enabled here.

ALTER TABLE public.ownership_bind_sources
    ADD COLUMN source_version SMALLINT NOT NULL DEFAULT 1,
    ADD COLUMN dossier_provenance JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN dossier_snapshot_hash TEXT,
    ADD COLUMN archive_metadata_hash TEXT,
    ADD COLUMN expires_at TIMESTAMPTZ,
    ADD COLUMN revoked_at TIMESTAMPTZ,
    ADD COLUMN revoked_reason TEXT,
    ADD COLUMN created_by TEXT NOT NULL DEFAULT 'legacy',
    ADD COLUMN last_state_reason TEXT,
    ADD COLUMN last_inspected_at TIMESTAMPTZ,
    ADD COLUMN last_inspection_digest TEXT,
    ADD COLUMN last_inspection_trusted BOOLEAN;

ALTER TABLE public.ownership_bind_sources
    ADD CONSTRAINT ownership_bind_source_version_positive CHECK (source_version > 0),
    ADD CONSTRAINT ownership_bind_source_provenance_object CHECK (jsonb_typeof(dossier_provenance) = 'object'),
    ADD CONSTRAINT ownership_bind_source_dossier_hash CHECK (
        dossier_snapshot_hash IS NULL OR dossier_snapshot_hash ~ '^[0-9a-f]{64}$'
    ),
    ADD CONSTRAINT ownership_bind_source_metadata_hash CHECK (
        archive_metadata_hash IS NULL OR archive_metadata_hash ~ '^[0-9a-f]{64}$'
    ),
    ADD CONSTRAINT ownership_bind_source_expiry_after_creation CHECK (
        expires_at IS NULL OR expires_at > created_at
    ),
    ADD CONSTRAINT ownership_bind_source_revoke_reason CHECK (
        revoked_at IS NULL OR NULLIF(BTRIM(revoked_reason), '') IS NOT NULL
    ),
    ADD CONSTRAINT ownership_bind_source_inspection_digest CHECK (
        last_inspection_digest IS NULL OR last_inspection_digest ~ '^[0-9a-f]{64}$'
    );

CREATE INDEX ownership_bind_source_active_expiry
    ON public.ownership_bind_sources(cluster, expires_at)
    WHERE enabled = TRUE AND consumed_at IS NULL AND revoked_at IS NULL;

CREATE INDEX ownership_chainling_binding_profile_projection
    ON public.ownership_chainling_bindings(player_profile_id, bound_at DESC);

CREATE TABLE public.ownership_bind_source_audit (
    audit_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_id UUID NOT NULL REFERENCES public.ownership_bind_sources(source_id) ON DELETE RESTRICT,
    event_type TEXT NOT NULL CHECK (
        event_type IN ('created', 'enabled', 'disabled', 'inspected_trusted', 'inspected_untrusted', 'revoked', 'consumed')
    ),
    actor_role TEXT NOT NULL,
    reason TEXT,
    details JSONB NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(details) = 'object'),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ownership_bind_source_audit_source_created
    ON public.ownership_bind_source_audit(source_id, created_at DESC);

ALTER TABLE public.ownership_bind_source_audit ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.ownership_bind_source_audit FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT ON TABLE public.ownership_bind_source_audit TO service_role;
GRANT USAGE, SELECT ON SEQUENCE public.ownership_bind_source_audit_audit_id_seq TO service_role;

CREATE OR REPLACE FUNCTION public.ownership_bind_source_immutable_guard()
RETURNS TRIGGER
LANGUAGE plpgsql
SET search_path = ''
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.enabled
           OR NEW.expires_at IS NULL OR NEW.expires_at <= now()
           OR NEW.dossier_snapshot_hash IS NULL OR NEW.archive_metadata_hash IS NULL
           OR NEW.revoked_at IS NOT NULL OR NEW.consumed_at IS NOT NULL THEN
            RAISE EXCEPTION 'bind_source_must_start_disabled';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.player_profile_id IS DISTINCT FROM OLD.player_profile_id
       OR NEW.chainling_id IS DISTINCT FROM OLD.chainling_id
       OR NEW.wallet_address IS DISTINCT FROM OLD.wallet_address
       OR NEW.cluster IS DISTINCT FROM OLD.cluster
       OR NEW.onchain_chainling_id IS DISTINCT FROM OLD.onchain_chainling_id
       OR NEW.cover_asset_address IS DISTINCT FROM OLD.cover_asset_address
       OR NEW.component_asset_address IS DISTINCT FROM OLD.component_asset_address
       OR NEW.component_kind IS DISTINCT FROM OLD.component_kind
       OR NEW.archive_name IS DISTINCT FROM OLD.archive_name
       OR NEW.archive_uri IS DISTINCT FROM OLD.archive_uri
       OR NEW.source_version IS DISTINCT FROM OLD.source_version
       OR NEW.dossier_provenance IS DISTINCT FROM OLD.dossier_provenance
       OR NEW.dossier_snapshot_hash IS DISTINCT FROM OLD.dossier_snapshot_hash
       OR NEW.archive_metadata_hash IS DISTINCT FROM OLD.archive_metadata_hash
       OR NEW.expires_at IS DISTINCT FROM OLD.expires_at
       OR NEW.created_by IS DISTINCT FROM OLD.created_by THEN
        RAISE EXCEPTION 'bind_source_inputs_immutable';
    END IF;
    IF OLD.revoked_at IS NOT NULL AND (
        NEW.revoked_at IS DISTINCT FROM OLD.revoked_at
        OR NEW.revoked_reason IS DISTINCT FROM OLD.revoked_reason
    ) THEN
        RAISE EXCEPTION 'bind_source_revocation_immutable';
    END IF;
    IF OLD.consumed_at IS NOT NULL AND (
        NEW.consumed_at IS DISTINCT FROM OLD.consumed_at
        OR NEW.consumed_operation_id IS DISTINCT FROM OLD.consumed_operation_id
    ) THEN
        RAISE EXCEPTION 'bind_source_consumption_immutable';
    END IF;
    IF NEW.consumed_at IS NOT NULL THEN NEW.enabled := FALSE; END IF;
    IF NEW.enabled AND (
        NEW.consumed_at IS NOT NULL OR NEW.revoked_at IS NOT NULL
        OR NEW.expires_at IS NULL OR NEW.expires_at <= now()
        OR NEW.last_inspection_trusted IS DISTINCT FROM TRUE
        OR NEW.last_inspected_at IS NULL OR NEW.last_inspected_at < now() - interval '10 minutes'
    ) THEN
        RAISE EXCEPTION 'bind_source_not_enableable';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_ownership_bind_source_immutable
    BEFORE INSERT OR UPDATE ON public.ownership_bind_sources
    FOR EACH ROW EXECUTE FUNCTION public.ownership_bind_source_immutable_guard();

CREATE OR REPLACE FUNCTION public.ownership_bind_source_audit_trigger()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_event TEXT;
    v_reason TEXT;
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_event := 'created';
    ELSIF NEW.consumed_at IS DISTINCT FROM OLD.consumed_at AND NEW.consumed_at IS NOT NULL THEN
        v_event := 'consumed';
    ELSIF NEW.revoked_at IS DISTINCT FROM OLD.revoked_at AND NEW.revoked_at IS NOT NULL THEN
        v_event := 'revoked';
        v_reason := NEW.revoked_reason;
    ELSIF NEW.enabled IS DISTINCT FROM OLD.enabled THEN
        v_event := CASE WHEN NEW.enabled THEN 'enabled' ELSE 'disabled' END;
        v_reason := NEW.last_state_reason;
    ELSIF NEW.last_inspected_at IS DISTINCT FROM OLD.last_inspected_at THEN
        v_event := CASE WHEN NEW.last_inspection_trusted THEN 'inspected_trusted' ELSE 'inspected_untrusted' END;
    ELSE
        RETURN NEW;
    END IF;

    INSERT INTO public.ownership_bind_source_audit(source_id, event_type, actor_role, reason, details)
    VALUES (
        NEW.source_id,
        v_event,
        COALESCE(NULLIF(current_setting('request.jwt.claim.role', TRUE), ''), session_user),
        v_reason,
        jsonb_build_object(
            'enabled', NEW.enabled,
            'expires_at', NEW.expires_at,
            'inspection_digest', NEW.last_inspection_digest,
            'consumed_operation_id', NEW.consumed_operation_id
        )
    );
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_ownership_bind_source_audit
    AFTER INSERT OR UPDATE ON public.ownership_bind_sources
    FOR EACH ROW EXECUTE FUNCTION public.ownership_bind_source_audit_trigger();

CREATE OR REPLACE FUNCTION public.ownership_create_bind_source_service(
    p_player_profile_id UUID,
    p_chainling_id UUID,
    p_wallet_address TEXT,
    p_cluster TEXT,
    p_onchain_chainling_id NUMERIC,
    p_cover_asset_address TEXT,
    p_component_asset_address TEXT,
    p_component_kind SMALLINT,
    p_archive_name TEXT,
    p_archive_uri TEXT,
    p_archive_metadata_hash TEXT,
    p_dossier_provenance JSONB,
    p_dossier_snapshot_hash TEXT,
    p_expires_at TIMESTAMPTZ,
    p_operator_reference TEXT
) RETURNS public.ownership_bind_sources
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_source public.ownership_bind_sources%ROWTYPE;
BEGIN
    IF p_player_profile_id IS NULL OR p_chainling_id IS NULL THEN
        RAISE EXCEPTION 'invalid_bind_source_identity';
    END IF;
    IF p_cluster IS DISTINCT FROM 'devnet' THEN RAISE EXCEPTION 'bind_source_devnet_only'; END IF;
    IF p_expires_at IS NULL OR p_expires_at <= now() OR p_expires_at > now() + interval '7 days' THEN
        RAISE EXCEPTION 'bind_source_expiry_out_of_range';
    END IF;
    IF p_wallet_address IS NULL OR p_wallet_address !~ '^[1-9A-HJ-NP-Za-km-z]{32,44}$'
       OR p_cover_asset_address IS NULL OR p_cover_asset_address !~ '^[1-9A-HJ-NP-Za-km-z]{32,44}$'
       OR p_component_asset_address IS NULL OR p_component_asset_address !~ '^[1-9A-HJ-NP-Za-km-z]{32,44}$'
       OR p_cover_asset_address = p_component_asset_address THEN
        RAISE EXCEPTION 'invalid_bind_source_asset';
    END IF;
    IF p_component_kind IS NULL OR p_component_kind NOT IN (0, 1) THEN
        RAISE EXCEPTION 'invalid_component_kind';
    END IF;
    IF p_onchain_chainling_id IS NULL OR p_onchain_chainling_id < 0
       OR p_onchain_chainling_id > 18446744073709551615 THEN
        RAISE EXCEPTION 'invalid_onchain_chainling_id';
    END IF;
    IF NULLIF(BTRIM(p_archive_name), '') IS NULL OR octet_length(p_archive_name) > 64
       OR p_archive_uri IS NULL OR p_archive_uri NOT LIKE 'https://%' OR octet_length(p_archive_uri) > 200
       OR p_archive_metadata_hash IS NULL OR p_archive_metadata_hash !~ '^[0-9a-f]{64}$'
       OR p_dossier_snapshot_hash IS NULL OR p_dossier_snapshot_hash !~ '^[0-9a-f]{64}$'
       OR p_dossier_provenance IS NULL OR jsonb_typeof(p_dossier_provenance) <> 'object'
       OR p_dossier_provenance->>'authority' NOT IN (
           'authoritative_capture', 'authoritative_starter', 'operator_reattestation'
       )
       OR NULLIF(BTRIM(p_dossier_provenance->>'authority'), '') IS NULL
       OR NULLIF(BTRIM(p_dossier_provenance->>'authority_record_id'), '') IS NULL
       OR p_dossier_provenance->>'snapshot_version' !~ '^[1-9][0-9]*$'
       OR pg_column_size(p_dossier_provenance) > 4096
       OR octet_length(p_dossier_provenance->>'authority_record_id') > 256
       OR NULLIF(BTRIM(p_operator_reference), '') IS NULL
       OR octet_length(p_operator_reference) > 256 THEN
        RAISE EXCEPTION 'invalid_bind_source_provenance';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.chainlings c
        WHERE c.id = p_chainling_id AND c.player_profile_id = p_player_profile_id
    ) THEN RAISE EXCEPTION 'bind_source_chainling_not_owned'; END IF;
    IF EXISTS (
        SELECT 1 FROM public.ownership_chainling_bindings b WHERE b.chainling_id = p_chainling_id
    ) THEN RAISE EXCEPTION 'bind_source_chainling_already_bound'; END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.profile_wallets w
        WHERE w.player_profile_id = p_player_profile_id
          AND w.wallet_public_key = p_wallet_address
          AND w.is_active = TRUE AND w.unlinked_at IS NULL
    ) THEN RAISE EXCEPTION 'bind_source_wallet_not_active'; END IF;

    INSERT INTO public.ownership_bind_sources(
        player_profile_id, chainling_id, wallet_address, cluster, onchain_chainling_id,
        cover_asset_address, component_asset_address, component_kind, archive_name, archive_uri,
        enabled, source_version, dossier_provenance, dossier_snapshot_hash,
        archive_metadata_hash, expires_at, created_by
    ) VALUES (
        p_player_profile_id, p_chainling_id, p_wallet_address, p_cluster, p_onchain_chainling_id,
        p_cover_asset_address, p_component_asset_address, p_component_kind, p_archive_name, p_archive_uri,
        FALSE, 1, p_dossier_provenance, p_dossier_snapshot_hash,
        p_archive_metadata_hash, p_expires_at, p_operator_reference
    ) RETURNING * INTO v_source;
    RETURN v_source;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_record_bind_source_inspection_service(
    p_source_id UUID,
    p_inspection_digest TEXT,
    p_trusted BOOLEAN
) RETURNS public.ownership_bind_sources
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_source public.ownership_bind_sources%ROWTYPE;
BEGIN
    IF p_inspection_digest IS NULL OR p_inspection_digest !~ '^[0-9a-f]{64}$'
       OR p_trusted IS NULL THEN
        RAISE EXCEPTION 'invalid_inspection_result';
    END IF;
    UPDATE public.ownership_bind_sources
       SET last_inspected_at = now(), last_inspection_digest = p_inspection_digest,
           last_inspection_trusted = p_trusted, updated_at = now()
     WHERE source_id = p_source_id AND consumed_at IS NULL
     RETURNING * INTO v_source;
    IF NOT FOUND THEN RAISE EXCEPTION 'bind_source_not_available'; END IF;
    RETURN v_source;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_set_bind_source_enabled_service(
    p_source_id UUID,
    p_enabled BOOLEAN,
    p_reason TEXT
) RETURNS public.ownership_bind_sources
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_source public.ownership_bind_sources%ROWTYPE;
BEGIN
    SELECT * INTO v_source FROM public.ownership_bind_sources WHERE source_id = p_source_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'bind_source_not_found'; END IF;
    IF p_enabled IS NULL THEN RAISE EXCEPTION 'bind_source_enabled_required'; END IF;
    IF NULLIF(BTRIM(p_reason), '') IS NULL OR octet_length(p_reason) > 256 THEN
        RAISE EXCEPTION 'bind_source_reason_required';
    END IF;
    IF p_enabled AND (
        v_source.consumed_at IS NOT NULL OR v_source.revoked_at IS NOT NULL
        OR v_source.expires_at IS NULL OR v_source.expires_at <= now()
        OR v_source.last_inspection_trusted IS DISTINCT FROM TRUE
        OR v_source.last_inspected_at IS NULL OR v_source.last_inspected_at < now() - interval '10 minutes'
    ) THEN RAISE EXCEPTION 'bind_source_not_enableable'; END IF;
    UPDATE public.ownership_bind_sources
       SET enabled = p_enabled, last_state_reason = p_reason, updated_at = now()
     WHERE source_id = p_source_id RETURNING * INTO v_source;
    RETURN v_source;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_revoke_bind_source_service(
    p_source_id UUID,
    p_reason TEXT
) RETURNS public.ownership_bind_sources
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_source public.ownership_bind_sources%ROWTYPE;
BEGIN
    IF NULLIF(BTRIM(p_reason), '') IS NULL OR octet_length(p_reason) > 256 THEN
        RAISE EXCEPTION 'bind_source_reason_required';
    END IF;
    UPDATE public.ownership_bind_sources
       SET enabled = FALSE, revoked_at = COALESCE(revoked_at, now()),
           revoked_reason = COALESCE(revoked_reason, p_reason),
           last_state_reason = p_reason, updated_at = now()
     WHERE source_id = p_source_id AND consumed_at IS NULL
     RETURNING * INTO v_source;
    IF NOT FOUND THEN RAISE EXCEPTION 'bind_source_not_revocable'; END IF;
    RETURN v_source;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_enforce_bind_operation_source()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_source public.ownership_bind_sources%ROWTYPE;
BEGIN
    SELECT * INTO v_source FROM public.ownership_bind_sources WHERE source_id = NEW.source_id;
    IF NOT FOUND OR NOT v_source.enabled OR v_source.consumed_at IS NOT NULL
       OR v_source.revoked_at IS NOT NULL OR v_source.expires_at IS NULL OR v_source.expires_at <= now()
       OR v_source.dossier_snapshot_hash IS NULL OR v_source.archive_metadata_hash IS NULL
       OR v_source.last_inspection_trusted IS DISTINCT FROM TRUE
       OR v_source.last_inspected_at IS NULL OR v_source.last_inspected_at < now() - interval '10 minutes' THEN
        RAISE EXCEPTION 'bind_source_not_available';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_ownership_bind_operation_source
    BEFORE INSERT ON public.ownership_bind_operations
    FOR EACH ROW EXECUTE FUNCTION public.ownership_enforce_bind_operation_source();

CREATE OR REPLACE FUNCTION public.ownership_archive_binding_projection_service(p_player_profile_id UUID)
RETURNS TABLE (
    chainling_id UUID,
    archive_asset_id TEXT,
    archived_at_unix BIGINT,
    bind_operation_id UUID,
    bind_source_id UUID,
    cover_asset_address TEXT,
    component_asset_address TEXT,
    component_kind SMALLINT,
    receipt_address TEXT,
    signature TEXT,
    cluster TEXT
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
    SELECT b.chainling_id,
           b.archive_asset_address,
           FLOOR(EXTRACT(EPOCH FROM b.bound_at))::BIGINT,
           b.operation_id,
           b.source_id,
           b.cover_asset_address,
           b.component_asset_address,
           b.component_kind,
           b.receipt_address,
           b.signature,
           b.cluster
      FROM public.ownership_chainling_bindings b
     WHERE b.player_profile_id = p_player_profile_id
     ORDER BY b.bound_at, b.binding_id
$$;

REVOKE ALL ON FUNCTION public.ownership_bind_source_immutable_guard() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_bind_source_audit_trigger() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_enforce_bind_operation_source() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_create_bind_source_service(
    UUID, UUID, TEXT, TEXT, NUMERIC, TEXT, TEXT, SMALLINT, TEXT, TEXT, TEXT, JSONB, TEXT, TIMESTAMPTZ, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_record_bind_source_inspection_service(UUID, TEXT, BOOLEAN)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_set_bind_source_enabled_service(UUID, BOOLEAN, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_revoke_bind_source_service(UUID, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_archive_binding_projection_service(UUID)
    FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.ownership_create_bind_source_service(
    UUID, UUID, TEXT, TEXT, NUMERIC, TEXT, TEXT, SMALLINT, TEXT, TEXT, TEXT, JSONB, TEXT, TIMESTAMPTZ, TEXT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_record_bind_source_inspection_service(UUID, TEXT, BOOLEAN)
    TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_set_bind_source_enabled_service(UUID, BOOLEAN, TEXT)
    TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_revoke_bind_source_service(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_archive_binding_projection_service(UUID) TO service_role;
