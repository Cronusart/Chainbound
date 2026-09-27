BEGIN;

SELECT plan(9);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.profile_recover_by_wallet_tx(text,uuid,text)',
        'EXECUTE'
    ),
    'authenticated clients cannot invoke wallet recovery directly'
);

INSERT INTO public.player_profiles (player_profile_id, display_name, platform_origin, last_platform_seen)
VALUES
    ('25000000-0000-0000-0000-000000000001', 'Fresh Install', 'test', 'test'),
    ('25000000-0000-0000-0000-000000000002', 'Wallet Profile', 'test', 'test');

INSERT INTO public.profile_identities (
    player_profile_id, identity_type, provider, provider_subject, auth_user_id, is_primary
) VALUES
    ('25000000-0000-0000-0000-000000000001', 'guest', 'supabase_anonymous', 'new-auth-user', 'new-auth-user', true),
    ('25000000-0000-0000-0000-000000000002', 'guest', 'supabase_anonymous', 'stale-auth-user', 'stale-auth-user', true);

INSERT INTO public.profile_wallets (
    player_profile_id, wallet_provider, wallet_public_key, is_active
) VALUES (
    '25000000-0000-0000-0000-000000000002',
    'seed_vault',
    'WalletRecovery111111111111111111111111111',
    true
);

CREATE TEMP TABLE wallet_recovery_results (payload JSONB);
INSERT INTO wallet_recovery_results
SELECT public.profile_recover_by_wallet_tx(
    'new-auth-user',
    '25000000-0000-0000-0000-000000000001',
    'WalletRecovery111111111111111111111111111'
);

SELECT is(
    (SELECT payload->>'success' FROM wallet_recovery_results),
    'true',
    'verified-wallet recovery succeeds'
);

SELECT is(
    (SELECT payload->>'player_profile_id' FROM wallet_recovery_results),
    '25000000-0000-0000-0000-000000000002',
    'recovery resolves the profile already linked to the wallet'
);

SELECT is(
    (SELECT player_profile_id::TEXT FROM public.profile_identities
     WHERE auth_user_id = 'new-auth-user' AND revoked_at IS NULL),
    '25000000-0000-0000-0000-000000000002',
    'the current auth identity moves to the wallet profile'
);

SELECT ok(
    EXISTS (
        SELECT 1 FROM public.profile_identities
        WHERE auth_user_id = 'stale-auth-user' AND revoked_at IS NOT NULL
    ),
    'the stale auth identity loses profile authority'
);

SELECT is(
    (SELECT COUNT(*)::TEXT FROM public.profile_identities
     WHERE player_profile_id = '25000000-0000-0000-0000-000000000002'
       AND revoked_at IS NULL AND is_primary = true),
    '1',
    'the recovered profile has exactly one active primary identity'
);

SELECT is(
    (SELECT wallet_public_key FROM public.profile_wallets
     WHERE player_profile_id = '25000000-0000-0000-0000-000000000002'
       AND is_active = true AND unlinked_at IS NULL),
    'WalletRecovery111111111111111111111111111',
    'wallet ownership remains unchanged'
);

SELECT is(
    (SELECT display_name FROM public.player_profiles
     WHERE player_profile_id = '25000000-0000-0000-0000-000000000001'),
    'Fresh Install',
    'the displaced fresh profile remains intact and is never merged'
);

SELECT is(
    (public.profile_recover_by_wallet_tx(
        'new-auth-user',
        '25000000-0000-0000-0000-000000000001',
        'WalletRecovery111111111111111111111111111'
    )->>'error'),
    'authenticated_profile_mismatch',
    'a stale current-profile claim fails closed'
);

SELECT * FROM finish();
ROLLBACK;
