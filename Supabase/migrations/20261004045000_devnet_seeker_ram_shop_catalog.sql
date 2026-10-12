-- Devnet Seeker purchase fixture. SOL amounts are small exercise prices so a
-- faucet-funded wallet can sign them. They are not the dollar shelf.
-- Destination is the devnet upgrade authority. No mainnet rows.

-- Mainnet safety (edited 2026-10-08, after this version was applied to Devnet;
-- Devnet does not re-run it): the Devnet-only statements in this file run only
-- when the database is explicitly identified as Devnet. Otherwise they are
-- skipped with a NOTICE (fail closed). Accepted Devnet markers, either one:
--   1. Explicit opt-in: chainbound.cluster = 'devnet' in the session applying
--      this file, e.g. SET LOCAL chainbound.cluster = 'devnet'; or, for a
--      CLI-driven Devnet rebuild, ALTER DATABASE postgres SET chainbound.cluster = 'devnet';
--   2. public.hub_settings.cluster = 'devnet'. That column defaults to '' and is
--      operator-set; no migration ever sets it to 'devnet'.
-- hub_settings does not exist yet when this version replays on a fresh
-- database, so a fresh Devnet rebuild must use the explicit opt-in (1).

DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
           INTO v_is_devnet;
    END IF;
    IF NOT v_is_devnet THEN
        RAISE NOTICE '20261004045000_devnet_seeker_ram_shop_catalog skipped: database is not marked as Devnet';
        RETURN;
    END IF;

    UPDATE public.ram_shop_settings
    SET checkout_cluster = 'devnet', updated_at = now()
    WHERE singleton;

    -- The former post-UPDATE checkout_cluster check could never fail (it ran right
    -- after setting the value); the Devnet marker check above replaces it.

    UPDATE public.ram_shop_catalog_versions
    SET enabled = FALSE
    WHERE enabled
      AND version <> 'devnet-seeker-20261004';

    INSERT INTO public.ram_shop_catalog_versions (version, enabled)
    VALUES ('devnet-seeker-20261004', TRUE)
    ON CONFLICT (version) DO UPDATE
    SET enabled = TRUE;

    INSERT INTO public.ram_shop_bundles (
        catalog_version_id, sku, display_name, ram_amount, sort_order, enabled
    )
    SELECT catalog.id, bundle.sku, bundle.display_name, bundle.ram_amount, bundle.sort_order, TRUE
    FROM public.ram_shop_catalog_versions AS catalog
    CROSS JOIN (
        VALUES
            ('ram_5', '100 RAM', 100::BIGINT, 0),
            ('ram_20', '500 RAM', 500::BIGINT, 1),
            ('ram_100', '3000 RAM', 3000::BIGINT, 2)
    ) AS bundle(sku, display_name, ram_amount, sort_order)
    WHERE catalog.version = 'devnet-seeker-20261004'
    ON CONFLICT (catalog_version_id, sku) DO UPDATE
    SET display_name = EXCLUDED.display_name,
        ram_amount = EXCLUDED.ram_amount,
        sort_order = EXCLUDED.sort_order,
        enabled = TRUE;

    INSERT INTO public.ram_shop_tender_prices (
        bundle_id, cluster, tender, atomic_amount, mint_address, decimals,
        token_program, destination, enabled, verified_at
    )
    SELECT
        bundle.id,
        'devnet',
        'SOL',
        price.atomic_amount,
        NULL,
        9,
        NULL,
        'DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG',
        TRUE,
        now()
    FROM public.ram_shop_bundles AS bundle
    JOIN public.ram_shop_catalog_versions AS catalog
        ON catalog.id = bundle.catalog_version_id
    JOIN (
        VALUES
            ('ram_5', 5000000::NUMERIC),
            ('ram_20', 20000000::NUMERIC),
            ('ram_100', 100000000::NUMERIC)
    ) AS price(sku, atomic_amount)
        ON price.sku = bundle.sku
    WHERE catalog.version = 'devnet-seeker-20261004'
    ON CONFLICT (bundle_id, cluster, tender) DO UPDATE
    SET atomic_amount = EXCLUDED.atomic_amount,
        mint_address = NULL,
        decimals = 9,
        token_program = NULL,
        destination = EXCLUDED.destination,
        enabled = TRUE,
        verified_at = now();

    UPDATE public.ram_shop_settings
    SET checkout_enabled = TRUE,
        checkout_cluster = 'devnet',
        quote_ttl_seconds = 60,
        updated_at = now()
    WHERE singleton
      AND checkout_cluster = 'devnet';
END
$devnet_guard$;
