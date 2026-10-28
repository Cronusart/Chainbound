-- RAM Shop R2 Task 8: token price rows must match a pinned classic-SPL mint.
-- Run on the RAM shop chain plus 20261004045000_devnet_seeker_ram_shop_catalog.sql
-- and the R2 migrations.
BEGIN;

SELECT plan(10);

CREATE FUNCTION pg_temp.add_price(p_cluster TEXT, p_tender TEXT, p_mint TEXT, p_program TEXT, p_decimals INTEGER, p_sku TEXT DEFAULT 'ram_20')
RETURNS VOID LANGUAGE sql AS $$
  INSERT INTO public.ram_shop_tender_prices(
    bundle_id, cluster, tender, atomic_amount, mint_address, decimals, token_program, destination
  )
  SELECT id, p_cluster, p_tender, 500000, p_mint, p_decimals, p_program, 'treasury-ata'
    FROM public.ram_shop_bundles WHERE sku = p_sku;
$$;

SELECT is(
  (SELECT mint || ':' || token_program || ':' || decimals FROM public.ram_shop_tender_mints
    WHERE cluster = 'devnet' AND tender = 'USDC'),
  '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU:TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA:6',
  'devnet USDC is pinned to Circle devnet USDC, classic Token, 6 decimals'
);
SELECT is((SELECT count(*)::TEXT FROM public.ram_shop_tender_mints WHERE cluster = 'mainnet-beta'), '0',
          'no mainnet pins are seeded');

SELECT lives_ok(
  $$SELECT pg_temp.add_price('devnet', 'USDC', '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU',
                             'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA', 6, 'ram_5')$$,
  'a pinned devnet USDC price row is accepted'
);
SELECT throws_ok(
  $$SELECT pg_temp.add_price('devnet', 'USDC', '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDV',
                             'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA', 6)$$,
  '23503', NULL, 'a lookalike USDC mint is rejected'
);
SELECT throws_ok(
  $$SELECT pg_temp.add_price('devnet', 'USDC', '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU',
                             'TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb', 6)$$,
  '23503', NULL, 'a Token-2022 program on a USDC row is rejected'
);
SELECT throws_ok(
  $$SELECT pg_temp.add_price('devnet', 'USDC', '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU',
                             'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA', 9)$$,
  '23503', NULL, 'wrong decimals are rejected'
);
SELECT throws_ok(
  $$SELECT pg_temp.add_price('devnet', 'SKR', 'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3',
                             'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA', 6)$$,
  '23503', NULL, 'devnet SKR is refused until Cronus pins a test mint'
);
SELECT throws_ok(
  $$INSERT INTO public.ram_shop_tender_mints(cluster, tender, mint, token_program, decimals)
    VALUES ('mainnet-beta', 'USDC', '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU',
            'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA', 6)$$,
  '23514', NULL, 'the mainnet USDC pin can only be the real USDC mint'
);
SELECT throws_ok(
  $$INSERT INTO public.ram_shop_tender_mints(cluster, tender, mint, token_program, decimals)
    VALUES ('devnet', 'SKR', '9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin',
            'TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb', 6)$$,
  '23514', NULL, 'a pin cannot name Token-2022'
);
SELECT ok(NOT has_table_privilege('service_role', 'public.ram_shop_tender_mints', 'INSERT'),
          'pins change only through migrations');

SELECT * FROM finish();
ROLLBACK;
