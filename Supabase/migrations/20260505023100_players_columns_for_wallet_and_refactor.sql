-- Signal Jam 3D - players compatibility columns for wallet connect and account refactor.
-- Assumes the MVP public.players table already exists.

ALTER TABLE public.players ADD COLUMN IF NOT EXISTS wallet_lower TEXT;
ALTER TABLE public.players ADD COLUMN IF NOT EXISTS login_count INTEGER NOT NULL DEFAULT 0;
ALTER TABLE public.players ADD COLUMN IF NOT EXISTS intro_complete BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE public.players ADD COLUMN IF NOT EXISTS ram_balance INTEGER NOT NULL DEFAULT 0;
ALTER TABLE public.players ADD COLUMN IF NOT EXISTS player_profile_id UUID NULL;
UPDATE public.players
SET wallet_lower = lower(wallet)
WHERE wallet IS NOT NULL
  AND (wallet_lower IS NULL OR wallet_lower <> lower(wallet));
CREATE OR REPLACE FUNCTION public.players_set_wallet_lower()
RETURNS TRIGGER
LANGUAGE plpgsql
SET search_path = public
AS $$
BEGIN
    NEW.wallet_lower = lower(NEW.wallet);
    RETURN NEW;
END;
$$;
DROP TRIGGER IF EXISTS trg_players_wallet_lower ON public.players;
CREATE TRIGGER trg_players_wallet_lower
    BEFORE INSERT OR UPDATE OF wallet ON public.players
    FOR EACH ROW
    EXECUTE FUNCTION public.players_set_wallet_lower();
CREATE UNIQUE INDEX IF NOT EXISTS idx_players_wallet_lower_unique
    ON public.players (wallet_lower);
CREATE INDEX IF NOT EXISTS idx_players_player_profile_id
    ON public.players (player_profile_id);
