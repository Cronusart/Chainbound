-- Signal Jam 3D - canonical account, identity, wallet, and entitlement model.
-- Additive migration: preserves the legacy `players` table and wallet-keyed
-- Edge Functions while introducing server-issued player_profile_id.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE OR REPLACE FUNCTION public.signaljam_touch_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
CREATE TABLE IF NOT EXISTS public.player_profiles (
    player_profile_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    legacy_player_id UUID UNIQUE NULL,
    display_name TEXT NULL,
    has_started_game BOOLEAN NOT NULL DEFAULT false,
    intro_complete BOOLEAN NOT NULL DEFAULT false,
    platform_origin TEXT NOT NULL DEFAULT 'unknown',
    last_platform_seen TEXT NOT NULL DEFAULT 'unknown',
    data_residency TEXT NULL,
    progression_state JSONB NOT NULL DEFAULT '{}'::jsonb,
    privacy_metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
UPDATE public.player_profiles
SET player_profile_id = gen_random_uuid()
WHERE player_profile_id IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_player_profiles_profile_id
    ON public.player_profiles (player_profile_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_player_profiles_legacy_player_id
    ON public.player_profiles (legacy_player_id);
DROP TRIGGER IF EXISTS trg_player_profiles_touch_updated_at ON public.player_profiles;
CREATE TRIGGER trg_player_profiles_touch_updated_at
    BEFORE UPDATE ON public.player_profiles
    FOR EACH ROW
    EXECUTE FUNCTION public.signaljam_touch_updated_at();
CREATE TABLE IF NOT EXISTS public.profile_identities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    identity_type TEXT NOT NULL,
    provider TEXT NOT NULL,
    provider_subject TEXT NOT NULL,
    auth_user_id TEXT NULL,
    is_primary BOOLEAN NOT NULL DEFAULT false,
    linked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    revoked_at TIMESTAMPTZ NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_profile_identities_active_subject
    ON public.profile_identities (identity_type, provider, provider_subject)
    WHERE revoked_at IS NULL AND provider_subject <> '';
CREATE INDEX IF NOT EXISTS idx_profile_identities_profile
    ON public.profile_identities (player_profile_id);
CREATE TABLE IF NOT EXISTS public.profile_wallets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_provider TEXT NOT NULL,
    wallet_public_key TEXT NOT NULL,
    linked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    unlinked_at TIMESTAMPTZ NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_profile_wallets_active_public_key
    ON public.profile_wallets (wallet_public_key)
    WHERE is_active = true AND unlinked_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_profile_wallets_profile
    ON public.profile_wallets (player_profile_id);
CREATE TABLE IF NOT EXISTS public.entitlement_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    source_type TEXT NOT NULL,
    source_platform TEXT NOT NULL,
    source_tx_id TEXT NULL,
    sku_or_product_id TEXT NULL,
    amount BIGINT NOT NULL DEFAULT 0,
    currency_type TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending_validation',
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_entitlement_ledger_source_tx
    ON public.entitlement_ledger (source_type, source_platform, source_tx_id)
    WHERE source_tx_id IS NOT NULL AND source_tx_id <> '';
CREATE INDEX IF NOT EXISTS idx_entitlement_ledger_profile
    ON public.entitlement_ledger (player_profile_id, created_at DESC);
DROP TRIGGER IF EXISTS trg_entitlement_ledger_touch_updated_at ON public.entitlement_ledger;
CREATE TRIGGER trg_entitlement_ledger_touch_updated_at
    BEFORE UPDATE ON public.entitlement_ledger
    FOR EACH ROW
    EXECUTE FUNCTION public.signaljam_touch_updated_at();
CREATE TABLE IF NOT EXISTS public.account_link_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE SET NULL,
    event_type TEXT NOT NULL,
    actor_type TEXT NOT NULL DEFAULT 'player',
    provider TEXT NULL,
    result TEXT NOT NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_account_link_events_profile_created
    ON public.account_link_events (player_profile_id, created_at DESC);
ALTER TABLE public.player_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_identities ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_wallets ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.entitlement_ledger ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.account_link_events ENABLE ROW LEVEL SECURITY;
DO $$
BEGIN
    IF to_regclass('public.chainlings') IS NOT NULL THEN
        EXECUTE 'ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS player_profile_id UUID NULL REFERENCES public.player_profiles(player_profile_id)';
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'chainlings'
              AND column_name = 'player_id'
        ) THEN
            EXECUTE 'ALTER TABLE public.chainlings ALTER COLUMN player_id DROP NOT NULL';
        END IF;
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_chainlings_player_profile_id ON public.chainlings (player_profile_id)';
    END IF;
END $$;
DO $$
BEGIN
    IF to_regclass('public.players') IS NOT NULL THEN
        EXECUTE 'ALTER TABLE public.players ADD COLUMN IF NOT EXISTS player_profile_id UUID NULL';

        EXECUTE $sql$
            INSERT INTO public.player_profiles (
                legacy_player_id,
                display_name,
                has_started_game,
                intro_complete,
                platform_origin,
                last_platform_seen,
                created_at,
                updated_at
            )
            SELECT
                p.id,
                p.display_name,
                COALESCE(p.login_count, 0) > 0,
                COALESCE(p.intro_complete, false),
                'legacy_wallet',
                'legacy_wallet',
                COALESCE(p.created_at, now()),
                COALESCE(p.last_seen_at, now())
            FROM public.players p
            ON CONFLICT (legacy_player_id) DO UPDATE SET
                display_name = EXCLUDED.display_name,
                has_started_game = EXCLUDED.has_started_game,
                intro_complete = EXCLUDED.intro_complete,
                updated_at = now()
        $sql$;

        EXECUTE $sql$
            UPDATE public.players p
            SET player_profile_id = pp.player_profile_id
            FROM public.player_profiles pp
            WHERE pp.legacy_player_id = p.id
              AND p.player_profile_id IS NULL
        $sql$;

        EXECUTE $sql$
            INSERT INTO public.profile_wallets (
                player_profile_id,
                wallet_provider,
                wallet_public_key,
                linked_at,
                is_active,
                metadata_json
            )
            SELECT
                pp.player_profile_id,
                'other',
                p.wallet,
                COALESCE(p.created_at, now()),
                true,
                jsonb_build_object('source', 'legacy_players')
            FROM public.players p
            JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
            WHERE p.wallet IS NOT NULL AND p.wallet <> ''
            ON CONFLICT DO NOTHING
        $sql$;

        IF to_regclass('public.chainlings') IS NOT NULL THEN
            EXECUTE $sql$
                UPDATE public.chainlings c
                SET player_profile_id = pp.player_profile_id
                FROM public.player_profiles pp
                WHERE pp.legacy_player_id = c.player_id
                  AND c.player_profile_id IS NULL
            $sql$;
        END IF;
    END IF;
END $$;
