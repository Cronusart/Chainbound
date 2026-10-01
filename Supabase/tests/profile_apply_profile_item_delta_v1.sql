BEGIN;

SELECT plan(8);

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen)
VALUES ('20000000-0000-0000-0000-000000000041', 'test', 'test');

SELECT is(
    (SELECT category FROM public.chainbound_item_catalog WHERE item_id = 'coffee_beans'),
    'quest',
    'coffee_beans is a quest catalog row'
);

CREATE TEMP TABLE delta_results (name TEXT PRIMARY KEY, payload JSONB);

INSERT INTO delta_results VALUES (
    'beans',
    public.profile_apply_profile_item_delta_tx(
        '20000000-0000-0000-0000-000000000041',
        'quest:coffee_and_beer:coffee_beans',
        '[{"item_id":"coffee_beans","quantity":1}]'::jsonb,
        '[]'::jsonb
    )
);

INSERT INTO delta_results VALUES (
    'beans_retry',
    public.profile_apply_profile_item_delta_tx(
        '20000000-0000-0000-0000-000000000041',
        'quest:coffee_and_beer:coffee_beans',
        '[{"item_id":"coffee_beans","quantity":1}]'::jsonb,
        '[]'::jsonb
    )
);

INSERT INTO delta_results VALUES (
    'beer',
    public.profile_apply_profile_item_delta_tx(
        '20000000-0000-0000-0000-000000000041',
        'quest:coffee_and_beer:cold_beer',
        '[{"item_id":"cold_beer","quantity":1}]'::jsonb,
        '[]'::jsonb
    )
);

INSERT INTO delta_results VALUES (
    'short',
    public.profile_apply_profile_item_delta_tx(
        '20000000-0000-0000-0000-000000000041',
        'quest:coffee_and_beer:reynard_brew_short',
        '[{"item_id":"tobys_delivery","quantity":1}]'::jsonb,
        '[{"item_id":"coffee_beans","quantity":2}]'::jsonb
    )
);

INSERT INTO delta_results VALUES (
    'brew',
    public.profile_apply_profile_item_delta_tx(
        '20000000-0000-0000-0000-000000000041',
        'quest:coffee_and_beer:reynard_brew',
        '[{"item_id":"tobys_delivery","quantity":1}]'::jsonb,
        '[{"item_id":"coffee_beans","quantity":1},{"item_id":"cold_beer","quantity":1}]'::jsonb
    )
);

SELECT is((SELECT payload->>'ok' FROM delta_results WHERE name = 'beans'), 'true', 'bean grant succeeds');
SELECT is((SELECT payload->>'already_applied' FROM delta_results WHERE name = 'beans_retry'), 'true', 'bean grant replay is already applied');
SELECT is(
    (SELECT quantity FROM public.chainbound_profile_item_stacks
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000041'
       AND item_id = 'coffee_beans'),
    1,
    'bean replay does not stack a second bag'
);
SELECT is((SELECT payload->>'error' FROM delta_results WHERE name = 'short'), 'insufficient_quantity', 'over-consume is rejected');
SELECT is((SELECT payload->>'ok' FROM delta_results WHERE name = 'brew'), 'true', 'brew exchange succeeds');
SELECT is(
    (SELECT quantity FROM public.chainbound_profile_item_stacks
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000041'
       AND item_id = 'tobys_delivery'),
    1,
    'brew grants tobys_delivery'
);
SELECT is(
    (SELECT COUNT(*)::int FROM public.chainbound_profile_item_stacks
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000041'
       AND item_id IN ('coffee_beans', 'cold_beer')),
    0,
    'brew consumes the beans and the beer'
);

SELECT * FROM finish();

ROLLBACK;
