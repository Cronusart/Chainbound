-- Chainbound - profile deletion must clean the full owned loadout graph.
-- A loadout cannot outlive either its Chainling or its profile-owned move item.

ALTER TABLE public.loadouts
    DROP CONSTRAINT IF EXISTS loadouts_move_item_id_fkey;
ALTER TABLE public.loadouts
    ADD CONSTRAINT loadouts_move_item_id_fkey
    FOREIGN KEY (move_item_id)
    REFERENCES public.move_items(id)
    ON DELETE CASCADE;
