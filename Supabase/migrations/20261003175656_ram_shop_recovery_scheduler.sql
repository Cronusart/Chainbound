-- Scheduled purchase recovery. Secrets are never embedded in cron.job:
-- create Vault entries named project_url, service_role_key, and
-- ram_reconciler_secret, then call ram_shop_configure_reconciler_schedule().

CREATE EXTENSION IF NOT EXISTS pg_cron;
CREATE EXTENSION IF NOT EXISTS pg_net WITH SCHEMA extensions;
CREATE EXTENSION IF NOT EXISTS supabase_vault WITH SCHEMA vault;

CREATE OR REPLACE FUNCTION public.ram_shop_configure_reconciler_schedule(
    p_schedule TEXT DEFAULT '* * * * *'
) RETURNS BIGINT
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_job_id BIGINT;
    v_secret_count INTEGER;
BEGIN
    IF NULLIF(BTRIM(p_schedule), '') IS NULL THEN
        RAISE EXCEPTION 'cron_schedule_required';
    END IF;

    SELECT COUNT(DISTINCT name)
      INTO v_secret_count
      FROM vault.decrypted_secrets
     WHERE name IN ('project_url', 'service_role_key', 'ram_reconciler_secret')
       AND NULLIF(BTRIM(decrypted_secret), '') IS NOT NULL;
    IF v_secret_count <> 3 THEN
        RAISE EXCEPTION 'ram_reconciler_vault_secrets_missing';
    END IF;

    IF EXISTS (SELECT 1 FROM cron.job WHERE jobname = 'ram-shop-reconcile-every-minute') THEN
        PERFORM cron.unschedule('ram-shop-reconcile-every-minute');
    END IF;

    SELECT cron.schedule(
        'ram-shop-reconcile-every-minute',
        p_schedule,
        $job$
        SELECT net.http_post(
            url := (
                SELECT decrypted_secret
                  FROM vault.decrypted_secrets
                 WHERE name = 'project_url'
                 LIMIT 1
            ) || '/functions/v1/ram_shop_reconcile',
            headers := jsonb_build_object(
                'Content-Type', 'application/json',
                'Authorization', 'Bearer ' || (
                    SELECT decrypted_secret
                      FROM vault.decrypted_secrets
                     WHERE name = 'service_role_key'
                     LIMIT 1
                ),
                'apikey', (
                    SELECT decrypted_secret
                      FROM vault.decrypted_secrets
                     WHERE name = 'service_role_key'
                     LIMIT 1
                ),
                'x-reconciler-secret', (
                    SELECT decrypted_secret
                      FROM vault.decrypted_secrets
                     WHERE name = 'ram_reconciler_secret'
                     LIMIT 1
                )
            ),
            body := '{}'::jsonb,
            timeout_milliseconds := 10000
        ) AS request_id
        $job$
    ) INTO v_job_id;

    RETURN v_job_id;
END;
$$;

REVOKE ALL ON FUNCTION public.ram_shop_configure_reconciler_schedule(TEXT)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_shop_configure_reconciler_schedule(TEXT)
    TO service_role;

DO $$
DECLARE v_secret_count INTEGER;
BEGIN
    SELECT COUNT(DISTINCT name)
      INTO v_secret_count
      FROM vault.decrypted_secrets
     WHERE name IN ('project_url', 'service_role_key', 'ram_reconciler_secret')
       AND NULLIF(BTRIM(decrypted_secret), '') IS NOT NULL;
    IF v_secret_count = 3 THEN
        PERFORM public.ram_shop_configure_reconciler_schedule();
    END IF;
END;
$$;

COMMENT ON FUNCTION public.ram_shop_configure_reconciler_schedule(TEXT) IS
'Configures the leased RAM purchase reconciler via pg_cron/pg_net. Requires Vault secrets project_url, service_role_key, and ram_reconciler_secret.';
