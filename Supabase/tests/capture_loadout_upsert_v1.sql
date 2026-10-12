BEGIN;

SELECT plan(4);

SELECT ok(
    to_regprocedure('public.authoritative_finalize_capture_attempt_service(uuid,uuid,text,integer,integer,jsonb,boolean,text,text,integer,jsonb)') IS NOT NULL,
    'capture capacity finalizer exists'
);

SELECT ok(
    NOT (
        pg_get_functiondef(
            'public.authoritative_finalize_capture_attempt_service(uuid,uuid,text,integer,integer,jsonb,boolean,text,text,integer,jsonb)'::regprocedure
        ) ILIKE '%updated_at = now()%'
    ),
    'capture loadout upsert does not assume loadouts.updated_at'
);

SELECT like(
    pg_get_functiondef(
        'public.authoritative_finalize_capture_attempt_service(uuid,uuid,text,integer,integer,jsonb,boolean,text,text,integer,jsonb)'::regprocedure
    ),
    '%SET move_item_id = EXCLUDED.move_item_id%',
    'locked capture writes the authoritative ordinal to the first loadout slot'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.authoritative_finalize_capture_attempt_service(uuid,uuid,text,integer,integer,jsonb,boolean,text,text,integer,jsonb)',
        'EXECUTE'
    ),
    'authenticated callers cannot finalize captures'
);

SELECT * FROM finish();

ROLLBACK;
