-- Loomville Mart leftover crates: unique world pickups after leftover permission.
-- Canonical remote migration version: 20260904075913.
-- Scene presence still uses world.pickup.* flags; SQL claims are the bag fact.

INSERT INTO public.chainbound_world_pickup_spawns (spawn_id, item_id, quantity, enabled, notes)
VALUES
    (
        'world.pickup.loomville_mart.open_crate_patch',
        'chainling_patch',
        1,
        true,
        'Loomville Mart leftover crate: Chainling Patch'
    ),
    (
        'world.pickup.loomville_mart.open_crate_orb',
        'signal_orb',
        1,
        true,
        'Loomville Mart leftover crate: Signal Orb'
    )
ON CONFLICT (spawn_id) DO UPDATE SET
    item_id = EXCLUDED.item_id,
    quantity = EXCLUDED.quantity,
    enabled = EXCLUDED.enabled,
    notes = EXCLUDED.notes,
    updated_at = now();
