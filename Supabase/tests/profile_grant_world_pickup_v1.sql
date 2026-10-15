BEGIN;

SELECT plan(10);

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen)
VALUES ('20000000-0000-0000-0000-000000000001', 'test', 'test');

SELECT is(
    (SELECT item_id FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.link1.backpack_01'),
    'chainling_patch',
    'link1 backpack spawn is seeded as chainling_patch'
);

SELECT is(
    (SELECT quantity FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.link1.backpack_01'),
    1,
    'link1 backpack spawn grants quantity 1'
);

SELECT is(
    (SELECT item_id FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.link1.backpack_02'),
    'signal_orb',
    'link1 backpack_02 spawn is seeded as signal_orb'
);

SELECT is(
    (SELECT quantity FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.link1.backpack_02'),
    1,
    'link1 backpack_02 spawn grants quantity 1'
);

CREATE TEMP TABLE grant_results (name TEXT PRIMARY KEY, payload JSONB);

INSERT INTO grant_results VALUES (
    'first',
    public.profile_grant_world_pickup_tx(
        '20000000-0000-0000-0000-000000000001',
        'world.pickup.link1.backpack_01',
        'test-grant-backpack-001'
    )
);

INSERT INTO grant_results VALUES (
    'retry',
    public.profile_grant_world_pickup_tx(
        '20000000-0000-0000-0000-000000000001',
        'world.pickup.link1.backpack_01',
        'test-grant-backpack-001'
    )
);

INSERT INTO grant_results VALUES (
    'unknown',
    public.profile_grant_world_pickup_tx(
        '20000000-0000-0000-0000-000000000001',
        'world.pickup.does_not_exist',
        NULL
    )
);

SELECT is((SELECT payload->>'ok' FROM grant_results WHERE name = 'first'), 'true', 'first grant succeeds');
SELECT is((SELECT payload->>'already_claimed' FROM grant_results WHERE name = 'first'), 'false', 'first grant is not already claimed');
SELECT is(
    (SELECT quantity FROM public.chainbound_profile_item_stacks
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'
       AND item_id = 'chainling_patch'),
    1,
    'first grant inserts chainling_patch x1'
);
SELECT is((SELECT payload->>'already_claimed' FROM grant_results WHERE name = 'retry'), 'true', 'retry is already_claimed');
SELECT is(
    (SELECT quantity FROM public.chainbound_profile_item_stacks
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'
       AND item_id = 'chainling_patch'),
    1,
    'retry does not increment stack quantity'
);
SELECT is((SELECT payload->>'error' FROM grant_results WHERE name = 'unknown'), 'unknown_spawn', 'unknown spawn is rejected');

SELECT * FROM finish();

ROLLBACK;
