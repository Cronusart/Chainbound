BEGIN;

SELECT plan(26);

SELECT has_column(
    'public', 'move_items', 'ownership_known_id',
    'move items retain the Known provenance identifier'
);

SELECT has_column(
    'public', 'move_items', 'stamp_operation_id',
    'move items retain the confirmed Stamp operation identifier'
);

SELECT has_column(
    'public', 'move_items', 'bind_sealed_at',
    'Bind sealing is explicit state on the move item'
);

SELECT ok(
    to_regprocedure('public.normalize_modifier_id(text)') IS NOT NULL,
    'immutable move-item modifier normalizer exists'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.normalize_modifier_id(text)',
        'EXECUTE'
    ),
    'modifier normalization remains an internal database helper'
);

SELECT ok(
    EXISTS (
        SELECT 1
        FROM public.modifiers_catalog
        WHERE modifier_id = '§10.1-a'
    ),
    'modifiers_catalog retains the §-prefixed canonical ID format'
);

SELECT is(
    (
        SELECT provolatile::TEXT
        FROM pg_proc
        WHERE oid = 'public.normalize_modifier_id(text)'::regprocedure
    ),
    'i',
    'modifier normalization is immutable because it performs formatting only'
);

SELECT is(
    public.normalize_modifier_id('10.1-a'),
    '§10.1-a',
    'un-prefixed Stamp modifier IDs map to the catalog format'
);

SELECT is(
    public.normalize_modifier_id('§10.1-a'),
    '§10.1-a',
    'already-prefixed catalog modifier IDs are unchanged'
);

SELECT is(
    public.normalize_modifier_id(NULL),
    NULL::TEXT,
    'a null modifier ID remains null'
);

SELECT is(
    COALESCE((
        SELECT catalog.modifier_id
        FROM public.modifiers_catalog AS catalog
        WHERE catalog.modifier_id = public.normalize_modifier_id('not-a-real-modifier')
    ), ''),
    '',
    'unknown modifier IDs are omitted from the move-item projection'
);

SELECT is(
    (SELECT COUNT(*) FROM public.move_items WHERE ownership_known_id IS NOT NULL),
    (SELECT COUNT(*) FROM public.ownership_known_moves),
    'backfill leaves exactly one provenance-linked move item for every Known move'
);

SELECT alike(
    pg_get_functiondef(
        'public.ownership_confirm_stamp_reserved(uuid,uuid,text,text,uuid,text,jsonb,numeric,text)'::regprocedure
    ),
    '%normalize_modifier_id(v_operation.modifier_ids[1])%',
    'Stamp confirmation normalizes the first modifier before projecting it to move_items'
);

SELECT ok(
    to_regprocedure('public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)') IS NOT NULL,
    'service-only atomic loadout mutation exists'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)',
        'EXECUTE'
    ),
    'authenticated callers cannot bypass the loadout Edge service'
);

SELECT alike(
    pg_get_functiondef('public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)'::regprocedure),
    '%last_move_required%',
    'atomic mutation rejects removing the final equipped move'
);

SELECT alike(
    pg_get_functiondef('public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)'::regprocedure),
    '%bind_sealed%',
    'atomic mutation returns a distinct Bind-sealed error'
);

SELECT alike(
    pg_get_functiondef('public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)'::regprocedure),
    '%move_escrowed%',
    'atomic mutation retains a distinct escrow error'
);

SELECT alike(
    pg_get_functiondef('public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)'::regprocedure),
    '%move_equipped_elsewhere%',
    'atomic mutation rejects equipping a move assigned to another Chainling'
);

SELECT alike(
    pg_get_functiondef('public.profile_set_loadout_atomic(uuid,uuid,integer,uuid)'::regprocedure),
    '%target_slot_occupied%',
    'atomic mutation rejects overwriting an occupied target slot'
);

SELECT ok(
    to_regprocedure('public.ownership_confirm_stamp_reserved(uuid,uuid,text,text,uuid,text,jsonb,numeric,text)') IS NOT NULL,
    'Stamp confirmation wrapper exists'
);

SELECT alike(
    pg_get_functiondef(
        'public.ownership_confirm_stamp_reserved(uuid,uuid,text,text,uuid,text,jsonb,numeric,text)'::regprocedure
    ),
    '%UPDATE public.move_items%',
    'confirmed Stamp updates the existing playable move item'
);

SELECT unalike(
    pg_get_functiondef(
        'public.ownership_confirm_stamp_reserved(uuid,uuid,text,text,uuid,text,jsonb,numeric,text)'::regprocedure
    ),
    '%INSERT INTO public.move_items%',
    'confirmed Stamp never creates an unequipped replacement move item'
);

SELECT ok(
    to_regprocedure('public.seal_bound_move_item()') IS NOT NULL,
    'Bind-to-move sealing trigger function exists'
);

SELECT alike(
    pg_get_functiondef('public.seal_bound_move_item()'::regprocedure),
    '%NEW.component_kind = 0%',
    'Bind sealing applies only to the stamped-move component kind'
);

SELECT alike(
    pg_get_functiondef('public.seal_bound_move_item()'::regprocedure),
    '%exported_asset_id = NEW.component_asset_address%',
    'Bind sealing requires an exact proven stamped-asset match'
);

SELECT * FROM finish();

ROLLBACK;
