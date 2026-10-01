-- Persist the Stamp-locked modifier amplitudes that Unity already substitutes
-- into candy/library copy. Existing rows stay nullable until prepare/backfill.
ALTER TABLE public.ownership_stamp_operations
    ADD COLUMN IF NOT EXISTS modifier_effect_values NUMERIC(12,4)[];

ALTER TABLE public.ownership_stamp_operations
    DROP CONSTRAINT IF EXISTS ownership_stamp_operations_effect_values_shape;

ALTER TABLE public.ownership_stamp_operations
    ADD CONSTRAINT ownership_stamp_operations_effect_values_shape CHECK (
        modifier_effect_values IS NULL
        OR (
            cardinality(modifier_effect_values) = modifier_count
            AND modifier_effect_values[1] IS NOT NULL
            AND (
                modifier_count = 1
                OR modifier_effect_values[2] IS NOT NULL
            )
        )
    );
