-- Chainbound verified wallet auth challenges.
-- Used by mobile wallet adapter login/link flows before profile resolution.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE OR REPLACE FUNCTION public.chainbound_touch_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
CREATE TABLE IF NOT EXISTS public.wallet_auth_challenges (
    nonce_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_public_key TEXT NOT NULL,
    wallet_provider TEXT NOT NULL DEFAULT 'other',
    cluster TEXT NOT NULL DEFAULT 'devnet',
    nonce_hash TEXT NOT NULL,
    message_hash TEXT NOT NULL,
    message TEXT NOT NULL,
    active_platform_build TEXT NOT NULL DEFAULT 'unknown',
    client_version TEXT NULL,
    device_id_hash TEXT NULL,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    consumed_at TIMESTAMPTZ NULL,
    verification_status TEXT NOT NULL DEFAULT 'issued',
    verification_error TEXT NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT wallet_auth_challenge_wallet_nonempty CHECK (length(trim(wallet_public_key)) > 0),
    CONSTRAINT wallet_auth_challenge_status_known CHECK (
        verification_status IN ('issued', 'consumed', 'expired', 'rejected')
    )
);
CREATE INDEX IF NOT EXISTS idx_wallet_auth_challenges_wallet_created
    ON public.wallet_auth_challenges (wallet_public_key, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_wallet_auth_challenges_profile_created
    ON public.wallet_auth_challenges (player_profile_id, created_at DESC)
    WHERE player_profile_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_wallet_auth_challenges_expires
    ON public.wallet_auth_challenges (expires_at)
    WHERE consumed_at IS NULL AND verification_status = 'issued';
DROP TRIGGER IF EXISTS trg_wallet_auth_challenges_touch_updated_at
    ON public.wallet_auth_challenges;
CREATE TRIGGER trg_wallet_auth_challenges_touch_updated_at
    BEFORE UPDATE ON public.wallet_auth_challenges
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
ALTER TABLE public.wallet_auth_challenges ENABLE ROW LEVEL SECURITY;
