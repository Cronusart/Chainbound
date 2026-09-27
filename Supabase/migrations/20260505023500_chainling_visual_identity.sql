-- Signal Jam 3D - Chainling visual identity payload.
-- Stores intrinsic visual variants and equipped cosmetic/effect IDs as stable IDs.

DO $$
BEGIN
    IF to_regclass('public.chainlings') IS NULL THEN
        RETURN;
    END IF;

    ALTER TABLE public.chainlings
        ADD COLUMN IF NOT EXISTS visual_variant_id TEXT NOT NULL DEFAULT 'base';

    ALTER TABLE public.chainlings
        ADD COLUMN IF NOT EXISTS visual_identity JSONB NOT NULL DEFAULT '{}'::jsonb;

    UPDATE public.chainlings
    SET visual_variant_id = 'entangled'
    WHERE COALESCE(entangled_flag, false) = true
      AND (visual_variant_id IS NULL OR visual_variant_id = '' OR visual_variant_id = 'base');

    UPDATE public.chainlings
    SET visual_variant_id = 'base'
    WHERE visual_variant_id IS NULL OR visual_variant_id = '';

    UPDATE public.chainlings
    SET visual_identity = '{}'::jsonb
    WHERE visual_identity IS NULL OR jsonb_typeof(visual_identity) <> 'object';

    ALTER TABLE public.chainlings
        ALTER COLUMN visual_variant_id SET DEFAULT 'base',
        ALTER COLUMN visual_variant_id SET NOT NULL,
        ALTER COLUMN visual_identity SET DEFAULT '{}'::jsonb,
        ALTER COLUMN visual_identity SET NOT NULL;

    CREATE INDEX IF NOT EXISTS idx_chainlings_species_visual_variant
        ON public.chainlings (species_id, visual_variant_id);

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'chainlings_visual_identity_is_object'
          AND conrelid = 'public.chainlings'::regclass
    ) THEN
        ALTER TABLE public.chainlings
            ADD CONSTRAINT chainlings_visual_identity_is_object
            CHECK (jsonb_typeof(visual_identity) = 'object');
    END IF;
END $$;
