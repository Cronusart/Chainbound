-- Daily login gift v1: eligibility, one claim per UTC day, outside the earn cap.
-- Run on the RAM shop chain plus tests/fixtures/daily_login_prerequisites.sql
-- (see its header for the migration order). now() is fixed for the whole
-- transaction, so the suite cannot straddle a UTC midnight.
BEGIN;

SELECT plan(36);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance, progression_state)
VALUES
  ('46000000-0000-4000-8000-000000000001', 'test', 'test', 0, '{}'),
  ('46000000-0000-4000-8000-000000000002', 'test', 'test', 0, '{"world.coffee.toby_delivery_accepted": false}'),
  ('46000000-0000-4000-8000-000000000003', 'test', 'test', 0, '{}'),
  ('46000000-0000-4000-8000-000000000004', 'test', 'test', 0, '{"world.coffee.toby_delivery_accepted": true}'),
  ('46000000-0000-4000-8000-000000000005', 'test', 'test', 100, '{}'),
  ('46000000-0000-4000-8000-000000000006', 'test', 'test', 0, '{}'),
  ('46000000-0000-4000-8000-000000000007', 'test', 'test', 0, '{}');

-- Profile 2 finished fetch_coffee only; the quest is still active.
INSERT INTO public.profile_quest_states(player_profile_id, quest_id, lifecycle, objective_progress)
VALUES ('46000000-0000-4000-8000-000000000002', 'coffee_and_beer', 'active', '{"fetch_coffee": 1}');
INSERT INTO public.profile_quest_states(player_profile_id, quest_id, lifecycle, resolution_id)
SELECT p::UUID, 'coffee_and_beer', 'completed', 'deliver_to_toby'
  FROM unnest(ARRAY[
    '46000000-0000-4000-8000-000000000003',
    '46000000-0000-4000-8000-000000000005',
    '46000000-0000-4000-8000-000000000006',
    '46000000-0000-4000-8000-000000000007'
  ]) AS p;

-- Profile 5 already earned the full 100 today.
INSERT INTO public.ram_ledger_operations(
  operation_id, player_profile_id, wallet_address, cluster,
  operation_kind, direction, amount, state, settled_at
) VALUES (
  '46000000-0000-5000-8000-000000000005', '46000000-0000-4000-8000-000000000005', NULL,
  'devnet', 'battle_reward', 'credit', 100, 'settled', now()
);
INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
VALUES ('46000000-0000-4000-8000-000000000005', (now() AT TIME ZONE 'UTC')::DATE, 100);

-- Profile 6 holds a full orb stack.
INSERT INTO public.chainbound_profile_item_stacks(player_profile_id, item_id, quantity)
VALUES ('46000000-0000-4000-8000-000000000006', 'signal_orb', 99);

-- Profile 7 claimed yesterday on a 3-day streak.
INSERT INTO public.profile_daily_login(player_profile_id, last_claim_utc_day, current_streak, longest_streak, total_claims)
VALUES ('46000000-0000-4000-8000-000000000007', (now() AT TIME ZONE 'UTC')::DATE - 1, 3, 3, 3);

UPDATE public.battle_reward_settings SET cluster = 'devnet' WHERE singleton;

CREATE FUNCTION pg_temp.balance(p_profile TEXT) RETURNS TEXT LANGUAGE sql AS $$
  SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id = p_profile::UUID;
$$;
CREATE FUNCTION pg_temp.orbs(p_profile TEXT) RETURNS TEXT LANGUAGE sql AS $$
  SELECT COALESCE((SELECT quantity::TEXT FROM public.chainbound_profile_item_stacks
                    WHERE player_profile_id = p_profile::UUID AND item_id = 'signal_orb'), '0');
$$;
CREATE FUNCTION pg_temp.claims(p_profile TEXT) RETURNS TEXT LANGUAGE sql AS $$
  SELECT count(*)::TEXT FROM public.profile_daily_login_claims WHERE player_profile_id = p_profile::UUID;
$$;
CREATE FUNCTION pg_temp.earned(p_profile TEXT) RETURNS TEXT LANGUAGE sql AS $$
  SELECT COALESCE((SELECT earned::TEXT FROM public.ram_daily_earnings
                    WHERE player_profile_id = p_profile::UUID
                      AND utc_day = (now() AT TIME ZONE 'UTC')::DATE), 'none');
$$;
CREATE FUNCTION pg_temp.next_midnight() RETURNS TIMESTAMPTZ LANGUAGE sql AS $$
  SELECT ((now() AT TIME ZONE 'UTC')::DATE + 1)::timestamp AT TIME ZONE 'UTC';
$$;

-- Locked profiles.
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000001')->>'error',
          'not_unlocked', 'no quest and no flag is not_unlocked');
SELECT is(pg_temp.claims('46000000-0000-4000-8000-000000000001') || ':' || pg_temp.balance('46000000-0000-4000-8000-000000000001'),
          '0:0', 'a locked claim writes no row and no RAM');
SELECT is(public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000001')->>'unlocked',
          'false', 'status reports locked');
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000002')->>'error',
          'not_unlocked', 'fetch_coffee alone with the quest active stays locked');

-- Unlock by quest or by flag.
SELECT is(public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000003')->>'canClaim',
          'true', 'a completed coffee_and_beer can claim');
SELECT is(public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000003')->>'nextClaimAt',
          NULL::TEXT, 'nextClaimAt is null while the gift is claimable');
SELECT is(public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000004')->>'canClaim',
          'true', 'the Toby flag alone can claim');
SELECT is(
  (SELECT s->>'ramAmount' || '/' || (s->>'itemId') || '/' || (s->>'itemQuantity')
     FROM (SELECT public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000004') AS s) x),
  '10/signal_orb/1', 'status labels the gift from the settings row'
);

-- Disabled settings refuse before any write.
UPDATE public.daily_login_settings SET enabled = false WHERE singleton;
SELECT is(public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000004')->>'canClaim',
          'false', 'a disabled gift is not claimable');
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000004')->>'error',
          'disabled', 'a disabled gift refuses the claim');
UPDATE public.daily_login_settings SET enabled = true WHERE singleton;

-- First claim.
CREATE TEMP TABLE claim_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO claim_results VALUES
  ('first', public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000003'));
INSERT INTO claim_results VALUES
  ('again', public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000003'));

SELECT is((SELECT payload->>'ok' || ':' || (payload->>'already_claimed') FROM claim_results WHERE name = 'first'),
          'true:false', 'first claim succeeds');
SELECT is(pg_temp.balance('46000000-0000-4000-8000-000000000003'), '10', 'first claim credits 10 RAM');
SELECT is((SELECT payload->>'ram_balance' FROM claim_results WHERE name = 'first'), '10', 'claim returns the new balance');
SELECT ok((SELECT (payload->>'ram_balance_version')::BIGINT > 0 FROM claim_results WHERE name = 'first'),
          'claim returns a bumped balance version');
SELECT is(pg_temp.orbs('46000000-0000-4000-8000-000000000003'), '1', 'first claim grants one signal_orb');
SELECT is(
  (SELECT count(*)::TEXT || ':' || min(amount)::TEXT || ':' || min(state) || ':' || min(metadata_json->>'reward')
     FROM public.ram_ledger_operations
    WHERE player_profile_id = '46000000-0000-4000-8000-000000000003'
      AND operation_kind = 'daily_login' AND direction = 'credit'),
  '1:10:settled:daily_login', 'one settled daily_login credit'
);
SELECT is(pg_temp.earned('46000000-0000-4000-8000-000000000003'), 'none', 'the gift opens no earn-cap row');
SELECT ok((SELECT (payload->>'next_claim_at')::TIMESTAMPTZ = pg_temp.next_midnight() FROM claim_results WHERE name = 'first'),
          'next_claim_at is the next 00:00 UTC');
SELECT ok(
  (SELECT (c.payload->>'next_claim_at')::TIMESTAMPTZ <> d.claimed_at + interval '24 hours'
     FROM claim_results c, public.profile_daily_login_claims d
    WHERE c.name = 'first' AND d.player_profile_id = '46000000-0000-4000-8000-000000000003'),
  'next_claim_at is not claimed_at + 24 hours'
);

-- Same UTC day again.
SELECT is((SELECT payload->>'ok' || ':' || (payload->>'already_claimed') FROM claim_results WHERE name = 'again'),
          'true:true', 'second claim the same UTC day is already_claimed');
SELECT is(pg_temp.balance('46000000-0000-4000-8000-000000000003') || ':' || pg_temp.orbs('46000000-0000-4000-8000-000000000003'),
          '10:1', 'second claim grants nothing');
SELECT is((SELECT payload->>'ram_amount' FROM claim_results WHERE name = 'again'), '10', 'replay reports the stored amount');
SELECT is(
  (SELECT s->>'canClaim' || ':' || ((s->>'nextClaimAt')::TIMESTAMPTZ = pg_temp.next_midnight())::TEXT
     FROM (SELECT public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000003') AS s) x),
  'false:true', 'status after the claim counts down to the next 00:00 UTC'
);

-- Outside the earn cap.
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000005')->>'ok',
          'true', 'a profile at the 100 cap can still claim');
SELECT is(pg_temp.balance('46000000-0000-4000-8000-000000000005') || ':' || pg_temp.earned('46000000-0000-4000-8000-000000000005'),
          '110:100', 'the gift adds 10 and earned stays 100');

-- Full orb stack.
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000006')->>'error',
          'stack_full', 'a full orb stack refuses the claim');
SELECT is(pg_temp.claims('46000000-0000-4000-8000-000000000006') || ':' || pg_temp.balance('46000000-0000-4000-8000-000000000006'),
          '0:0', 'stack_full writes no claim and no RAM');
SELECT is(public.profile_get_daily_login_status_tx('46000000-0000-4000-8000-000000000006')->>'canClaim',
          'true', 'stack_full does not spend the day');
SELECT is(
  public.profile_apply_profile_item_delta_tx(
    '46000000-0000-4000-8000-000000000006', 'test:use-orb',
    '[]'::jsonb, '[{"item_id":"signal_orb","quantity":1}]'::jsonb
  )->>'ok',
  'true', 'the player uses one orb'
);
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000006')->>'already_claimed',
          'false', 'a later claim the same UTC day works once the stack has room');
SELECT is(pg_temp.orbs('46000000-0000-4000-8000-000000000006') || ':' || pg_temp.balance('46000000-0000-4000-8000-000000000006'),
          '99:10', 'the late claim grants the orb and the RAM');

-- Streak (stored, not shown).
SELECT is(public.profile_claim_daily_login_tx('46000000-0000-4000-8000-000000000007')->>'streak_after',
          '4', 'claiming the day after yesterday extends the streak');
SELECT is(
  (SELECT current_streak || ':' || longest_streak || ':' || total_claims FROM public.profile_daily_login
    WHERE player_profile_id = '46000000-0000-4000-8000-000000000007'),
  '4:4:4', 'the summary row tracks the streak'
);
SELECT is(
  (SELECT current_streak || ':' || total_claims FROM public.profile_daily_login
    WHERE player_profile_id = '46000000-0000-4000-8000-000000000003'),
  '1:1', 'a first claim starts the streak at 1'
);

SELECT ok((SELECT bool_and(balanced) FROM public.ram_v2_audit_service(NULL)
            WHERE player_profile_id::TEXT LIKE '46000000-%'),
          'audit is balanced for every daily login profile');
SELECT ok(
  NOT has_function_privilege('authenticated', 'public.profile_claim_daily_login_tx(uuid)', 'EXECUTE')
  AND NOT has_function_privilege('authenticated', 'public.profile_get_daily_login_status_tx(uuid)', 'EXECUTE')
  AND NOT has_function_privilege('anon', 'public.profile_claim_daily_login_tx(uuid)', 'EXECUTE'),
  'clients cannot call the daily login functions'
);

SELECT * FROM finish();
ROLLBACK;
