-- Forward fix: 20260722030000_chainbound_player_item_inventory was edited after it
-- was applied, retiring press_charge (Ownership Spine 2026-09-15). That edit never
-- re-runs, so apply exactly the same catalog change here. Idempotent and
-- cluster-agnostic. `enabled` was not part of that edit and is left unchanged.
UPDATE public.chainbound_item_catalog
   SET display_name = 'Retired Catalyst',
       description = 'Legacy compatibility row; no active recipe.',
       effect_family = 'retired',
       bindery_only = false
 WHERE item_id = 'press_charge'
   AND (display_name, description, effect_family, bindery_only)
       IS DISTINCT FROM ('Retired Catalyst'::text, 'Legacy compatibility row; no active recipe.'::text, 'retired'::text, false);
