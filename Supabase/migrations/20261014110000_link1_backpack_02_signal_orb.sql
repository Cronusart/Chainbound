-- Link 1 second backpack. Unity sends spawn_id only; this row is the bag reward.
-- Scene presence uses world.pickup.link1.backpack_02 on WorldPickupGrant.

INSERT INTO public.chainbound_world_pickup_spawns (spawn_id, item_id, quantity, enabled, notes)
VALUES (
    'world.pickup.link1.backpack_02',
    'signal_orb',
    1,
    true,
    'Link 1 backpack unique pickup. Grants one Signal Orb.'
)
ON CONFLICT (spawn_id) DO UPDATE SET
    item_id = EXCLUDED.item_id,
    quantity = EXCLUDED.quantity,
    enabled = EXCLUDED.enabled,
    notes = EXCLUDED.notes,
    updated_at = now();
