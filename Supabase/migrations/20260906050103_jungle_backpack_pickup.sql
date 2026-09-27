-- Jungle unique world pickup. Unity sends spawn_id only; this row is the bag reward.
-- Canonical remote migration version: 20260906050103.
-- Scene presence still uses world.pickup.jungle.backpack_01 on WorldPickupGrant.

INSERT INTO public.chainbound_world_pickup_spawns (spawn_id, item_id, quantity, enabled, notes)
VALUES (
    'world.pickup.jungle.backpack_01',
    'chainling_patch',
    1,
    true,
    'Jungle backpack unique pickup'
)
ON CONFLICT (spawn_id) DO UPDATE SET
    item_id = EXCLUDED.item_id,
    quantity = EXCLUDED.quantity,
    enabled = EXCLUDED.enabled,
    notes = EXCLUDED.notes,
    updated_at = now();
