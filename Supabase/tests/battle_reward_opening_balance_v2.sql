BEGIN;

SELECT plan(6);

SELECT is((SELECT opening_delta::TEXT FROM public.ram_v2_migration_audit
            WHERE player_profile_id = '34000000-0000-4000-8000-000000000001'), '90',
          'Devnet migration snapshots the pre-v2 canonical balance');
SELECT is((SELECT amount::TEXT FROM public.ram_ledger_operations
            WHERE player_profile_id = '34000000-0000-4000-8000-000000000001'
              AND operation_kind = 'opening_balance'), '90',
          'Devnet migration books the exact opening-balance credit');
SELECT is((SELECT cluster FROM public.ram_ledger_operations
            WHERE player_profile_id = '34000000-0000-4000-8000-000000000001'
              AND operation_kind = 'opening_balance'), 'devnet',
          'opening-balance ledger key uses the guarded settings cluster');
SELECT is((SELECT status FROM public.ram_ledger_accounts
            WHERE player_profile_id = '34000000-0000-4000-8000-000000000001'), 'active',
          'pre-v2 frozen account is reactivated');
SELECT is((SELECT status FROM public.ram_reconciliation_cases
            WHERE player_profile_id = '34000000-0000-4000-8000-000000000001'), 'superseded_v2',
          'legacy chain-mirror mismatch is explicitly superseded');
SELECT ok((SELECT balanced FROM public.ram_v2_audit_service(
              '34000000-0000-4000-8000-000000000001')),
          'opening credit reconciles the canonical profile balance');

SELECT * FROM finish();
ROLLBACK;
