-- Snapshot the authoritative Known acquisition on every Stamp operation.
-- This preserves the selected card face after the Known leaves active inventory
-- and lets reconciliation/metadata repair reproduce the original NFT bake.

ALTER TABLE public.ownership_stamp_operations
    ADD COLUMN IF NOT EXISTS acquisition TEXT;

UPDATE public.ownership_stamp_operations AS operation
   SET acquisition = known.acquisition
  FROM public.ownership_known_moves AS known
 WHERE known.known_id = operation.known_id
   AND operation.acquisition IS DISTINCT FROM known.acquisition;

UPDATE public.ownership_stamp_operations
   SET acquisition = 'candy_roll'
 WHERE acquisition IS NULL
    OR acquisition NOT IN ('candy_roll', 'progression_learn', 'event_grant');

ALTER TABLE public.ownership_stamp_operations
    ALTER COLUMN acquisition SET DEFAULT 'candy_roll',
    ALTER COLUMN acquisition SET NOT NULL;

ALTER TABLE public.ownership_stamp_operations
    DROP CONSTRAINT IF EXISTS ownership_stamp_operations_acquisition_valid;

ALTER TABLE public.ownership_stamp_operations
    ADD CONSTRAINT ownership_stamp_operations_acquisition_valid
    CHECK (acquisition IN ('candy_roll', 'progression_learn', 'event_grant'));
