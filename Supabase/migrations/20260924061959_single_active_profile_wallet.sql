BEGIN;

-- Retain the newest active wallet link for each profile and deactivate any
-- older duplicates before enforcing the one-active-wallet-per-profile rule.
WITH ranked_active_wallets AS (
  SELECT
    id,
    row_number() OVER (
      PARTITION BY player_profile_id
      ORDER BY linked_at DESC, id DESC
    ) AS active_rank
  FROM public.profile_wallets
  WHERE is_active = true
    AND unlinked_at IS NULL
)
UPDATE public.profile_wallets AS wallet
SET
  is_active = false,
  unlinked_at = now(),
  metadata_json = coalesce(wallet.metadata_json, '{}'::jsonb) || jsonb_build_object(
    'deactivated_by', '20260924061959_single_active_profile_wallet',
    'deactivated_reason', 'older_duplicate_profile_wallet',
    'deactivated_at', now()
  )
FROM ranked_active_wallets AS ranked
WHERE wallet.id = ranked.id
  AND ranked.active_rank > 1;

CREATE UNIQUE INDEX IF NOT EXISTS ux_profile_wallets_active_profile
  ON public.profile_wallets (player_profile_id)
  WHERE is_active = true
    AND unlinked_at IS NULL;

COMMENT ON INDEX public.ux_profile_wallets_active_profile IS
  'Allows at most one active wallet link per player profile.';

COMMIT;
