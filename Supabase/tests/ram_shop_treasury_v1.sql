-- RAM Shop R2 Task 7: per-cluster treasury owner. Run on the RAM shop chain plus
-- 20261004045000_devnet_seeker_ram_shop_catalog.sql and the R2 migrations.
BEGIN;

SELECT plan(7);

SELECT is((SELECT owner FROM public.ram_shop_treasuries WHERE cluster = 'devnet'),
          'DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG',
          'devnet treasury owner is seeded unchanged (DkFm until the vault move)');
SELECT is((SELECT count(*)::TEXT FROM public.ram_shop_treasuries WHERE cluster = 'mainnet-beta'), '0',
          'no mainnet treasury row');
SELECT is(
  (SELECT count(*)::TEXT FROM public.ram_shop_tender_prices p
     JOIN public.ram_shop_treasuries t ON t.cluster = p.cluster
    WHERE p.enabled AND p.tender = 'SOL' AND p.destination <> t.owner),
  '0', 'every enabled SOL price already pays the configured owner'
);
SELECT ok(
  (SELECT count(*) > 0 FROM public.ram_shop_tender_prices WHERE enabled AND cluster = 'devnet'),
  'the devnet catalog has live SOL rows to keep working'
);
SELECT throws_ok(
  $$INSERT INTO public.ram_shop_treasuries(cluster, owner) VALUES ('mainnet-beta', 'not a key')$$,
  '23514', NULL, 'owner must look like a base58 public key'
);
SELECT ok(NOT has_table_privilege('authenticated', 'public.ram_shop_treasuries', 'SELECT'),
          'clients cannot read the treasury table');
SELECT ok(NOT has_table_privilege('service_role', 'public.ram_shop_treasuries', 'UPDATE'),
          'service_role cannot change the treasury (migration-only)');

SELECT * FROM finish();
ROLLBACK;
