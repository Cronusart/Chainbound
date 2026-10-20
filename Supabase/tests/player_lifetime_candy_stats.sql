BEGIN;

SELECT plan(6);

SELECT ok(
    EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'player_lifetime_stats'
           AND column_name = 'candy_machine_rolls'
           AND is_nullable = 'NO'
    ),
    'player_lifetime_stats.candy_machine_rolls exists'
);

SELECT ok(
    EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'player_lifetime_stats'
           AND column_name = 'candy_machine_stamps'
           AND is_nullable = 'NO'
    ),
    'player_lifetime_stats.candy_machine_stamps exists'
);

SELECT ok(
    EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgname = 'ownership_known_moves_count_candy_roll'
           AND NOT tgisinternal
    ),
    'candy roll insert trigger exists'
);

SELECT ok(
    EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgname = 'ownership_stamp_operations_count_candy_stamp'
           AND NOT tgisinternal
    ),
    'candy stamp status trigger exists'
);

SELECT ok(
    NOT has_function_privilege('anon', 'public.player_lifetime_stats_count_candy_roll()', 'EXECUTE')
    AND NOT has_function_privilege('authenticated', 'public.player_lifetime_stats_count_candy_roll()', 'EXECUTE'),
    'candy roll counter is not executable by client roles'
);

SELECT ok(
    NOT has_function_privilege('anon', 'public.player_lifetime_stats_count_candy_stamp()', 'EXECUTE')
    AND NOT has_function_privilege('authenticated', 'public.player_lifetime_stats_count_candy_stamp()', 'EXECUTE'),
    'candy stamp counter is not executable by client roles'
);

SELECT * FROM finish();

ROLLBACK;
