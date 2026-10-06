-- Server-side throttling for the authenticated Solana JSON-RPC Edge proxy.
-- The table is exposed through no client role and is only mutated by the
-- service_role-only function below.

CREATE TABLE IF NOT EXISTS public.solana_rpc_rate_limits (
    scope TEXT PRIMARY KEY,
    window_started_at TIMESTAMPTZ NOT NULL,
    request_count INTEGER NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT solana_rpc_rate_limits_scope_nonempty CHECK (length(btrim(scope)) > 0),
    CONSTRAINT solana_rpc_rate_limits_request_count_nonnegative CHECK (request_count >= 0)
);

ALTER TABLE public.solana_rpc_rate_limits ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.solana_rpc_rate_limits FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON TABLE public.solana_rpc_rate_limits TO service_role;

CREATE OR REPLACE FUNCTION public.solana_rpc_consume_rate_limit(
    p_auth_user_id UUID,
    p_user_limit INTEGER,
    p_global_limit INTEGER,
    p_window_seconds INTEGER
)
RETURNS TABLE (
    allowed BOOLEAN,
    user_remaining INTEGER,
    global_remaining INTEGER,
    retry_after_seconds INTEGER
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $function$
DECLARE
    v_now TIMESTAMPTZ := clock_timestamp();
    v_window INTERVAL;
    v_user_count INTEGER;
    v_global_count INTEGER;
    v_user_started_at TIMESTAMPTZ;
    v_global_started_at TIMESTAMPTZ;
BEGIN
    IF p_auth_user_id IS NULL THEN
        RAISE EXCEPTION 'auth user id is required' USING ERRCODE = '22023';
    END IF;
    IF p_user_limit < 1 OR p_user_limit > 1000 THEN
        RAISE EXCEPTION 'user limit is outside the supported range' USING ERRCODE = '22023';
    END IF;
    IF p_global_limit < 1 OR p_global_limit > 10000 THEN
        RAISE EXCEPTION 'global limit is outside the supported range' USING ERRCODE = '22023';
    END IF;
    IF p_window_seconds < 1 OR p_window_seconds > 3600 THEN
        RAISE EXCEPTION 'window seconds is outside the supported range' USING ERRCODE = '22023';
    END IF;

    v_window := pg_catalog.make_interval(secs => p_window_seconds);

    INSERT INTO public.solana_rpc_rate_limits AS current_limit (
        scope,
        window_started_at,
        request_count,
        updated_at
    )
    VALUES ('global', v_now, 1, v_now)
    ON CONFLICT (scope) DO UPDATE
    SET
        window_started_at = CASE
            WHEN current_limit.window_started_at <= v_now - v_window THEN v_now
            ELSE current_limit.window_started_at
        END,
        request_count = CASE
            WHEN current_limit.window_started_at <= v_now - v_window THEN 1
            ELSE least(current_limit.request_count + 1, p_global_limit + 1)
        END,
        updated_at = v_now
    RETURNING request_count, window_started_at
    INTO v_global_count, v_global_started_at;

    INSERT INTO public.solana_rpc_rate_limits AS current_limit (
        scope,
        window_started_at,
        request_count,
        updated_at
    )
    VALUES ('user:' || p_auth_user_id::TEXT, v_now, 1, v_now)
    ON CONFLICT (scope) DO UPDATE
    SET
        window_started_at = CASE
            WHEN current_limit.window_started_at <= v_now - v_window THEN v_now
            ELSE current_limit.window_started_at
        END,
        request_count = CASE
            WHEN current_limit.window_started_at <= v_now - v_window THEN 1
            ELSE least(current_limit.request_count + 1, p_user_limit + 1)
        END,
        updated_at = v_now
    RETURNING request_count, window_started_at
    INTO v_user_count, v_user_started_at;

    allowed := v_global_count <= p_global_limit AND v_user_count <= p_user_limit;
    user_remaining := greatest(0, p_user_limit - v_user_count);
    global_remaining := greatest(0, p_global_limit - v_global_count);
    retry_after_seconds := CASE
        WHEN allowed THEN 0
        ELSE greatest(
            1,
            pg_catalog.ceil(
                extract(
                    EPOCH FROM (
                        greatest(v_user_started_at, v_global_started_at) +
                        v_window -
                        v_now
                    )
                )
            )::INTEGER
        )
    END;

    RETURN NEXT;
END;
$function$;

REVOKE ALL ON FUNCTION public.solana_rpc_consume_rate_limit(UUID, INTEGER, INTEGER, INTEGER)
FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.solana_rpc_consume_rate_limit(UUID, INTEGER, INTEGER, INTEGER)
TO service_role;
