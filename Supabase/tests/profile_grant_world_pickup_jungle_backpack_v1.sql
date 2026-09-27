BEGIN;

SELECT plan(2);

SELECT is(
    (SELECT item_id FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.jungle.backpack_01'),
    'chainling_patch',
    'jungle backpack spawn is seeded as chainling_patch'
);

SELECT is(
    (SELECT quantity FROM public.chainbound_world_pickup_spawns
     WHERE spawn_id = 'world.pickup.jungle.backpack_01'),
    1,
    'jungle backpack spawn grants quantity 1'
);

SELECT * FROM finish();

ROLLBACK;
