-- Chainbound - promote HUD card cosmetics to hud_card_variant_id.
-- Backfills legacy nameplate_frame_id values into the forward visual_identity key.

DO $$
BEGIN
    IF to_regclass('public.chainlings') IS NULL THEN
        RETURN;
    END IF;

    UPDATE public.chainlings
    SET visual_identity = jsonb_set(
        CASE
            WHEN visual_identity IS NULL OR jsonb_typeof(visual_identity) <> 'object'
                THEN '{}'::jsonb
            ELSE visual_identity
        END,
        '{hud_card_variant_id}',
        to_jsonb(
            CASE
                WHEN NULLIF(BTRIM(COALESCE(visual_identity->>'hud_card_variant_id', '')), '') IS NOT NULL
                     AND BTRIM(COALESCE(visual_identity->>'hud_card_variant_id', '')) <> 'none'
                    THEN BTRIM(visual_identity->>'hud_card_variant_id')
                WHEN NULLIF(BTRIM(COALESCE(visual_identity->>'nameplate_frame_id', '')), '') IS NOT NULL
                     AND BTRIM(COALESCE(visual_identity->>'nameplate_frame_id', '')) <> 'none'
                    THEN BTRIM(visual_identity->>'nameplate_frame_id')
                ELSE 'default'
            END
        ),
        true
    )
    WHERE visual_identity IS NULL
       OR jsonb_typeof(visual_identity) <> 'object'
       OR NOT (visual_identity ? 'hud_card_variant_id')
       OR NULLIF(BTRIM(COALESCE(visual_identity->>'hud_card_variant_id', '')), '') IS NULL
       OR BTRIM(COALESCE(visual_identity->>'hud_card_variant_id', '')) = 'none';
END $$;
