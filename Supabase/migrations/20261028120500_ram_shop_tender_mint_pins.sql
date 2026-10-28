-- RAM Shop R2 Task 8: pin USDC/SKR mints before any token tender is enabled.
--
-- ram_shop_tender_mints holds the one allowed (mint, token program, decimals)
-- per cluster and tender. Every token row in ram_shop_tender_prices must match a
-- pin through a composite foreign key; SOL rows (mint NULL) are untouched, so
-- the live devnet SOL catalog keeps working. CHECKs hard-code the known mints
-- (verified on chain 2026-10-28, all classic SPL Token, 6 decimals):
--   USDC mainnet  EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v
--   USDC devnet   4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU (Circle)
--   SKR  mainnet  SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3
-- SKR doesn't exist on devnet: Cronus adds the project test mint as a devnet
-- 'SKR' row in a later migration. Only classic SPL Token is allowed
-- (TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA), so Token-2022 transfer-fee
-- and transfer-hook mints are rejected outright. Only the devnet USDC pin is
-- seeded; no mainnet rows.

CREATE TABLE IF NOT EXISTS public.ram_shop_tender_mints (
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    tender TEXT NOT NULL CHECK (tender IN ('USDC', 'SKR')),
    mint TEXT NOT NULL CHECK (mint ~ '^[1-9A-HJ-NP-Za-km-z]{32,44}$'),
    token_program TEXT NOT NULL
        CHECK (token_program = 'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA'),
    decimals SMALLINT NOT NULL CHECK (decimals = 6),
    notes TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (cluster, tender),
    UNIQUE (cluster, tender, mint, token_program, decimals),
    CONSTRAINT ram_shop_tender_mints_known_mints CHECK (
        (tender <> 'USDC' OR cluster <> 'mainnet-beta' OR mint = 'EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v')
        AND (tender <> 'USDC' OR cluster <> 'devnet' OR mint = '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU')
        AND (tender <> 'SKR' OR cluster <> 'mainnet-beta' OR mint = 'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3')
    )
);

ALTER TABLE public.ram_shop_tender_mints ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.ram_shop_tender_mints FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.ram_shop_tender_mints TO service_role;

-- Refuse to apply over an existing unpinned token row; on 2026-10-28 the hosted
-- devnet had SOL rows only.
DO $tender_pin_gap_guard$
BEGIN
    IF EXISTS (SELECT 1 FROM public.ram_shop_tender_prices WHERE tender <> 'SOL') THEN
        RAISE EXCEPTION 'ram_shop_token_price_rows_require_review_before_pinning';
    END IF;
END
$tender_pin_gap_guard$;

ALTER TABLE public.ram_shop_tender_prices
    DROP CONSTRAINT IF EXISTS ram_shop_tender_prices_pinned_mint_fkey;
ALTER TABLE public.ram_shop_tender_prices
    ADD CONSTRAINT ram_shop_tender_prices_pinned_mint_fkey
    FOREIGN KEY (cluster, tender, mint_address, token_program, decimals)
    REFERENCES public.ram_shop_tender_mints (cluster, tender, mint, token_program, decimals);

DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
           INTO v_is_devnet;
    END IF;
    IF NOT v_is_devnet THEN
        RAISE NOTICE '20261028120500_ram_shop_tender_mint_pins seed skipped: database is not marked as Devnet';
        RETURN;
    END IF;

    INSERT INTO public.ram_shop_tender_mints(cluster, tender, mint, token_program, decimals, notes)
    VALUES (
        'devnet', 'USDC', '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU',
        'TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA', 6, 'Circle devnet USDC'
    )
    ON CONFLICT (cluster, tender) DO NOTHING;
END
$devnet_guard$;
