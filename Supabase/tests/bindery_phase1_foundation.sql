BEGIN;

SELECT plan(17);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES ('41000000-0000-4000-8000-000000000001', 'test', 'test', 500);

INSERT INTO public.profile_wallets(player_profile_id, wallet_provider, wallet_public_key, is_active)
VALUES ('41000000-0000-4000-8000-000000000001', 'test', '77777777777777777777777777777777', TRUE);

INSERT INTO public.chainlings(
    id, player_profile_id, species_id, is_party_member, party_slot, level, xp, ivs,
    rebirth_iv_bonuses, stars, rebirth_count, evolution_stage, visual_variant_id, visual_identity
) VALUES (
    '41000000-0000-4000-8000-000000000002',
    '41000000-0000-4000-8000-000000000001',
    'test_species', FALSE, NULL, 1, 0, '[1,1,1,1,1,1]'::jsonb,
    '[0,0,0,0,0,0]'::jsonb, 0, 0, 1, 'base', '{}'::jsonb
);

CREATE TEMP TABLE bind_source_result(payload public.ownership_bind_sources);
INSERT INTO bind_source_result
SELECT public.ownership_create_bind_source_service(
    '41000000-0000-4000-8000-000000000001',
    '41000000-0000-4000-8000-000000000002',
    '77777777777777777777777777777777', 'devnet', 1::NUMERIC,
    '88888888888888888888888888888888',
    '99999999999999999999999999999999', 0::SMALLINT,
    'Phase 1 Test Archive', 'https://example.invalid/archive.json', repeat('a', 64),
    '{"authority":"authoritative_capture","authority_record_id":"phase1-test","snapshot_version":"1"}'::jsonb,
    repeat('b', 64), now() + interval '1 hour', 'phase1-test-service'
);

SELECT is(((SELECT payload FROM bind_source_result)).enabled, FALSE,
          'service-created source is disabled by default');
SELECT ok(((SELECT payload FROM bind_source_result)).expires_at > now(),
          'service-created source expires');
SELECT is((SELECT count(*) FROM public.ownership_bind_source_audit WHERE event_type = 'created'), 1::BIGINT,
          'source creation is audited');
SELECT throws_ok(
    $$UPDATE public.ownership_bind_sources SET cover_asset_address='AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'
      WHERE source_id=((SELECT payload FROM bind_source_result)).source_id$$,
    'bind_source_inputs_immutable', 'source inputs are immutable');
SELECT throws_ok(
    $$SELECT public.ownership_set_bind_source_enabled_service(
      ((SELECT payload FROM bind_source_result)).source_id, TRUE, 'premature')$$,
    'bind_source_not_enableable', 'source cannot enable before trusted inspection');
SELECT throws_ok(
    $$UPDATE public.ownership_bind_sources SET enabled=TRUE
      WHERE source_id=((SELECT payload FROM bind_source_result)).source_id$$,
    'bind_source_not_enableable', 'direct table write cannot bypass inspection gate');

SELECT lives_ok(
    $$SELECT public.ownership_record_bind_source_inspection_service(
      ((SELECT payload FROM bind_source_result)).source_id, repeat('c',64), FALSE)$$,
    'untrusted inspection is recorded');
SELECT throws_ok(
    $$SELECT public.ownership_set_bind_source_enabled_service(
      ((SELECT payload FROM bind_source_result)).source_id, TRUE, 'still untrusted')$$,
    'bind_source_not_enableable', 'untrusted source remains disabled');
SELECT lives_ok(
    $$SELECT public.ownership_record_bind_source_inspection_service(
      ((SELECT payload FROM bind_source_result)).source_id, repeat('d',64), TRUE)$$,
    'trusted inspection is recorded');
SELECT is((public.ownership_set_bind_source_enabled_service(
      ((SELECT payload FROM bind_source_result)).source_id, TRUE, 'reviewed test enable')).enabled,
    TRUE, 'fresh trusted inspection permits explicit service enable');
SELECT is((SELECT count(*) FROM public.ownership_bind_source_audit WHERE event_type = 'enabled'), 1::BIGINT,
          'enable transition is audited');
SELECT is((public.ownership_set_bind_source_enabled_service(
      ((SELECT payload FROM bind_source_result)).source_id, FALSE, 'test complete')).enabled,
    FALSE, 'service can disable a source');
SELECT ok((public.ownership_revoke_bind_source_service(
      ((SELECT payload FROM bind_source_result)).source_id, 'retired test source')).revoked_at IS NOT NULL,
    'service can permanently revoke a source');
SELECT throws_ok(
    $$UPDATE public.ownership_bind_sources SET revoked_at=NULL, revoked_reason=NULL
      WHERE source_id=((SELECT payload FROM bind_source_result)).source_id$$,
    'bind_source_revocation_immutable', 'direct table write cannot reverse revocation');
SELECT throws_ok(
    $$SELECT public.ownership_set_bind_source_enabled_service(
      ((SELECT payload FROM bind_source_result)).source_id, TRUE, 'must stay revoked')$$,
    'bind_source_not_enableable', 'revoked source cannot be re-enabled');
SELECT ok(NOT has_function_privilege(
    'authenticated',
    'public.ownership_create_bind_source_service(uuid,uuid,text,text,numeric,text,text,smallint,text,text,text,jsonb,text,timestamp with time zone,text)',
    'EXECUTE'), 'authenticated callers cannot create Bind sources');
SELECT is((SELECT count(*) FROM public.ownership_archive_binding_projection_service(
    '41000000-0000-4000-8000-000000000001')), 0::BIGINT,
    'projection is empty until receipt-backed settlement writes a binding');

SELECT * FROM finish();
ROLLBACK;
