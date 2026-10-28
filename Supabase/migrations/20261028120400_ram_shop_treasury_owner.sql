-- RAM Shop R2 Task 7 (decision D5): the shop payee is a configured treasury
-- owner per cluster, not whatever each ram_shop_tender_prices row says.
--
-- The payee is the configured treasury owner; devnet currently
-- DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG, a hot wallet. (This corrects the
-- 20261004045000_devnet_seeker_ram_shop_catalog header, which still calls it
-- "the devnet upgrade authority"; the upgrade authority has been the Squads
-- vault since 2026-10-27.)
--
-- The Edge functions read this row and also require the Edge secret
-- RAM_SHOP_TREASURY_OWNER to match it; a missing or different value closes
-- checkout with a typed 503. At prepare:
--   SOL:       the price row's destination must equal the owner.
--   USDC/SKR:  the destination must equal ATA(mint, owner, allowOwnerOffCurve,
--              token program), and the live token account must be owned by the
--              treasury owner, on that mint, initialized and not frozen.
-- A mismatch disables that tender. Orders keep the destination they were quoted
-- with, so an order created before a treasury change still verifies.
--
-- Moving the devnet payee to the treasury multisig vault
-- (Hkn8eVNs11wFKvGKGk3GZqo7WA29bYn3mroWNDj8gdaL, created 2026-10-28) is a later
-- migration plus a RAM_SHOP_TREASURY_OWNER change, after its ATAs exist.

CREATE TABLE IF NOT EXISTS public.ram_shop_treasuries (
    cluster TEXT PRIMARY KEY CHECK (cluster IN ('devnet', 'mainnet-beta')),
    owner TEXT NOT NULL CHECK (owner ~ '^[1-9A-HJ-NP-Za-km-z]{32,44}$'),
    notes TEXT NOT NULL DEFAULT '',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE public.ram_shop_treasuries ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.ram_shop_treasuries FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.ram_shop_treasuries TO service_role;

DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
           INTO v_is_devnet;
    END IF;
    IF NOT v_is_devnet THEN
        RAISE NOTICE '20261028120400_ram_shop_treasury_owner seed skipped: database is not marked as Devnet';
        RETURN;
    END IF;

    INSERT INTO public.ram_shop_treasuries(cluster, owner, notes)
    VALUES (
        'devnet',
        'DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG',
        'Hot wallet. Replace with the treasury multisig vault once its ATAs exist.'
    )
    ON CONFLICT (cluster) DO NOTHING;
END
$devnet_guard$;
