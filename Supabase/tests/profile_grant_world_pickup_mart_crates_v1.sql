BEGIN;

SELECT plan(4);

SELECT is(
    (SELECT item_id FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.loomville_mart.open_crate_patch'),
    'chainling_patch',
    'mart leftover crate patch spawn is seeded as chainling_patch'
);

SELECT is(
    (SELECT item_id FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.loomville_mart.open_crate_orb'),
    'signal_orb',
    'mart leftover crate orb spawn is seeded as signal_orb'
);

SELECT is(
    (SELECT quantity FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.loomville_mart.open_crate_patch'),
    1,
    'mart leftover crate patch grants quantity 1'
);

SELECT is(
    (SELECT quantity FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.loomville_mart.open_crate_orb'),
    1,
    'mart leftover crate orb grants quantity 1'
);

SELECT * FROM finish();

ROLLBACK;
