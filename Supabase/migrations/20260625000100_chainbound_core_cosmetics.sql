-- Chainbound Core cosmetic ownership read model and provenance storage.
-- This is additive and safe to re-run.

CREATE OR REPLACE FUNCTION public.chainbound_touch_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
ALTER TABLE public.chainlings
    ADD COLUMN IF NOT EXISTS visual_identity_asset_ids JSONB NOT NULL DEFAULT '{}'::jsonb;
CREATE TABLE IF NOT EXISTS public.chainbound_cosmetic_catalog (
    cosmetic_id TEXT PRIMARY KEY,
    slot TEXT NOT NULL,
    schema_version INT NOT NULL DEFAULT 1,
    enabled BOOLEAN NOT NULL DEFAULT true,
    rarity TEXT NULL,
    species_scope TEXT[] NULL,
    visual_variant_scope TEXT[] NULL,
    free_default BOOLEAN NOT NULL DEFAULT false,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_cosmetic_catalog_id_nonempty CHECK (length(trim(cosmetic_id)) > 0),
    CONSTRAINT chainbound_cosmetic_catalog_slot_nonempty CHECK (length(trim(slot)) > 0),
    CONSTRAINT chainbound_cosmetic_catalog_schema_positive CHECK (schema_version > 0)
);
CREATE INDEX IF NOT EXISTS idx_chainbound_cosmetic_catalog_slot
    ON public.chainbound_cosmetic_catalog (slot)
    WHERE enabled = true;
DROP TRIGGER IF EXISTS trg_chainbound_cosmetic_catalog_touch_updated_at
    ON public.chainbound_cosmetic_catalog;
CREATE TRIGGER trg_chainbound_cosmetic_catalog_touch_updated_at
    BEFORE UPDATE ON public.chainbound_cosmetic_catalog
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
INSERT INTO public.chainbound_cosmetic_catalog (
    cosmetic_id,
    slot,
    schema_version,
    enabled,
    rarity,
    free_default,
    metadata_json
)
VALUES
    ('none', 'free_default', 1, true, 'free', true, '{"source":"default"}'::jsonb),
    ('default', 'free_default', 1, true, 'free', true, '{"source":"default"}'::jsonb),
    ('base', 'visual_variant', 1, true, 'free', true, '{"source":"default"}'::jsonb),
    ('entangled', 'visual_variant', 1, true, 'free', true, '{"source":"default"}'::jsonb)
ON CONFLICT (cosmetic_id) DO UPDATE SET
    enabled = EXCLUDED.enabled,
    free_default = EXCLUDED.free_default,
    updated_at = now();
CREATE TABLE IF NOT EXISTS public.chainbound_core_cosmetic_ownership_cache (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_public_key TEXT NOT NULL,
    cluster TEXT NOT NULL DEFAULT 'devnet',
    asset_id TEXT NOT NULL,
    cosmetic_id TEXT NOT NULL,
    slot TEXT NOT NULL,
    collection TEXT NOT NULL,
    update_authority TEXT NULL,
    schema_version INT NOT NULL DEFAULT 1,
    verification_status TEXT NOT NULL DEFAULT 'verified',
    verification_error TEXT NULL,
    last_verified_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_verified_slot BIGINT NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_core_cosmetic_wallet_nonempty CHECK (length(trim(wallet_public_key)) > 0),
    CONSTRAINT chainbound_core_cosmetic_asset_nonempty CHECK (length(trim(asset_id)) > 0),
    CONSTRAINT chainbound_core_cosmetic_status_known CHECK (verification_status IN ('verified', 'rejected', 'stale', 'error'))
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_chainbound_core_cosmetic_asset_cluster
    ON public.chainbound_core_cosmetic_ownership_cache (cluster, asset_id);
CREATE INDEX IF NOT EXISTS idx_chainbound_core_cosmetic_owner
    ON public.chainbound_core_cosmetic_ownership_cache (
        player_profile_id,
        wallet_public_key,
        cluster,
        cosmetic_id,
        verification_status,
        last_verified_at DESC
    );
DROP TRIGGER IF EXISTS trg_chainbound_core_cosmetic_cache_touch_updated_at
    ON public.chainbound_core_cosmetic_ownership_cache;
CREATE TRIGGER trg_chainbound_core_cosmetic_cache_touch_updated_at
    BEFORE UPDATE ON public.chainbound_core_cosmetic_ownership_cache
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
CREATE TABLE IF NOT EXISTS public.chainbound_profile_chain_state (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_public_key TEXT NOT NULL,
    cluster TEXT NOT NULL DEFAULT 'devnet',
    game_program_id TEXT NOT NULL,
    player_profile_pda TEXT NULL,
    verification_status TEXT NOT NULL DEFAULT 'unknown',
    verification_error TEXT NULL,
    last_verified_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_profile_chain_state_status_known CHECK (
        verification_status IN ('unknown', 'verified', 'missing_on_chain', 'mismatch', 'rpc_unavailable')
    )
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_chainbound_profile_chain_state
    ON public.chainbound_profile_chain_state (player_profile_id, wallet_public_key, cluster, game_program_id);
DROP TRIGGER IF EXISTS trg_chainbound_profile_chain_state_touch_updated_at
    ON public.chainbound_profile_chain_state;
CREATE TRIGGER trg_chainbound_profile_chain_state_touch_updated_at
    BEFORE UPDATE ON public.chainbound_profile_chain_state
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
ALTER TABLE public.chainbound_cosmetic_catalog ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.chainbound_core_cosmetic_ownership_cache ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.chainbound_profile_chain_state ENABLE ROW LEVEL SECURITY;
