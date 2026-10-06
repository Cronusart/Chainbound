-- Devnet Seeker purchase fixture. SOL amounts are small exercise prices so a
-- faucet-funded wallet can sign them. They are not the dollar shelf.
-- Destination is the devnet upgrade authority. No mainnet rows.

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
    quote_ttl_seconds = 180,
    updated_at = now()
WHERE singleton;
