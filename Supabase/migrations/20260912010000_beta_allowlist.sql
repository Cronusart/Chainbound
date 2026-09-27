-- Closed-beta access is granted only to cryptographically proven wallet addresses.
-- No client-facing RLS policy is intentional: Edge Functions use service_role.
CREATE TABLE IF NOT EXISTS public.beta_allowlist (
    wallet_public_key TEXT PRIMARY KEY,
    ring TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'invited',
    notes TEXT NULL,
    approved_at TIMESTAMPTZ NULL,
    revoked_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT beta_allowlist_wallet_nonempty CHECK (length(trim(wallet_public_key)) BETWEEN 32 AND 44),
    CONSTRAINT beta_allowlist_ring_known CHECK (ring IN ('A', 'B', 'reviewer')),
    CONSTRAINT beta_allowlist_status_known CHECK (status IN ('invited', 'approved', 'revoked')),
    CONSTRAINT beta_allowlist_revocation_consistent CHECK (
        (status = 'revoked' AND revoked_at IS NOT NULL) OR
        (status <> 'revoked' AND revoked_at IS NULL)
    )
);

DROP TRIGGER IF EXISTS trg_beta_allowlist_touch_updated_at ON public.beta_allowlist;
CREATE TRIGGER trg_beta_allowlist_touch_updated_at
    BEFORE UPDATE ON public.beta_allowlist
    FOR EACH ROW EXECUTE FUNCTION public.chainbound_touch_updated_at();

ALTER TABLE public.beta_allowlist ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.beta_allowlist FROM anon, authenticated;

COMMENT ON TABLE public.beta_allowlist IS
    'Server-only closed-beta membership. Approval follows wallet signature proof; device and cookies never grant access.';
