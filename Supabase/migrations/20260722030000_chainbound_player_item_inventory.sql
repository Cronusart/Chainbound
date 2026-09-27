-- Chainbound player bag items + Metaplex Core collectible ownership cache.
-- Additive. Profile stacks are off-chain; Core collectibles are verified via DAS server-side.

CREATE TABLE IF NOT EXISTS public.chainbound_item_catalog (
    item_id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL,
    effect_family TEXT NOT NULL DEFAULT 'none',
    ownership_kind TEXT NOT NULL DEFAULT 'profile_stack',
    core_kind_attribute TEXT NULL,
    rarity TEXT NULL,
    max_stack INT NOT NULL DEFAULT 99,
    usable_in_menu BOOLEAN NOT NULL DEFAULT true,
    usable_in_battle BOOLEAN NOT NULL DEFAULT false,
    bindery_only BOOLEAN NOT NULL DEFAULT false,
    use_ram_cost BIGINT NOT NULL DEFAULT 0,
    schema_version INT NOT NULL DEFAULT 1,
    enabled BOOLEAN NOT NULL DEFAULT true,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_item_catalog_id_nonempty CHECK (length(trim(item_id)) > 0),
    CONSTRAINT chainbound_item_catalog_category_nonempty CHECK (length(trim(category)) > 0),
    CONSTRAINT chainbound_item_catalog_max_stack_positive CHECK (max_stack > 0),
    CONSTRAINT chainbound_item_catalog_ownership_kind_valid CHECK (
        ownership_kind IN ('profile_stack', 'core_collectible', 'core_cosmetic')
    )
);
CREATE INDEX IF NOT EXISTS idx_chainbound_item_catalog_category
    ON public.chainbound_item_catalog (category)
    WHERE enabled = true;
DROP TRIGGER IF EXISTS trg_chainbound_item_catalog_touch_updated_at
    ON public.chainbound_item_catalog;
CREATE TRIGGER trg_chainbound_item_catalog_touch_updated_at
    BEFORE UPDATE ON public.chainbound_item_catalog
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
INSERT INTO public.chainbound_item_catalog (
    item_id, display_name, description, category, effect_family, ownership_kind, core_kind_attribute, max_stack, bindery_only, use_ram_cost
) VALUES
    ('chainling_patch', 'Chainling Patch', 'Applies a small HP patch to one Chainling.', 'field', 'heal_single', 'profile_stack', NULL, 99, false, 0),
    ('chainling_update', 'Chainling Update', 'Applies a moderate HP update to one Chainling.', 'field', 'heal_single', 'profile_stack', NULL, 99, false, 0),
    ('chainling_reformat', 'Chainling Reformat', 'Fully restores one Chainling''s HP.', 'field', 'heal_single', 'profile_stack', NULL, 99, false, 0),
    ('system_flush', 'System Flush', 'Heals and clears common statuses.', 'field', 'status_clear', 'profile_stack', NULL, 99, false, 0),
    ('chainling_reboot', 'Chainling Reboot', 'Reboots a fainted Chainling.', 'field', 'revive', 'profile_stack', NULL, 99, false, 0),
    ('party_patch', 'Party Patch', 'Applies a small HP patch to the whole party.', 'field', 'heal_party', 'profile_stack', NULL, 99, false, 0),
    ('signal_orb', 'Signal Orb', 'Basic capture tool.', 'capture', 'capture_tool', 'profile_stack', NULL, 99, false, 0),
    ('lattice_orb', 'Lattice Orb', 'Improved capture tool.', 'capture', 'capture_tool', 'profile_stack', NULL, 99, false, 0),
    ('entangle_orb', 'Entangle Orb', 'Capture tool with a soft entangled bias.', 'capture', 'capture_tool', 'profile_stack', NULL, 99, false, 0),
    ('quick_orb', 'Quick Orb', 'Slightly stronger capture when weakened.', 'capture', 'capture_tool', 'profile_stack', NULL, 99, false, 0),
    ('clarity_ampoule_s', 'Clarity Ampoule', 'Primary Signal Clarity (short).', 'clarity', 'clarity_ampoule', 'profile_stack', NULL, 99, false, 0),
    ('clarity_ampoule_m', 'Focused Clarity Ampoule', 'Primary Signal Clarity (medium).', 'clarity', 'clarity_ampoule', 'profile_stack', NULL, 99, false, 0),
    ('clarity_ampoule_l', 'Lattice Clarity Ampoule', 'Primary Signal Clarity (long).', 'clarity', 'clarity_ampoule', 'profile_stack', NULL, 99, false, 0),
    -- One-way compatibility row only. Retired by Ownership Spine 2026-09-15 and unobtainable.
    ('press_charge', 'Retired Catalyst', 'Legacy compatibility row; no active recipe.', 'bindery', 'retired', 'profile_stack', NULL, 99, false, 0),
    ('stamp_solvent', 'Stamp Solvent', 'Erases a pressed stamp (Stamped → Known).', 'bindery', 'stamp_erase', 'profile_stack', NULL, 99, true, 25),
    ('seal_breaker', 'Seal Breaker', 'Rerolls a Stamped modifier in place.', 'bindery', 'modifier_reroll', 'profile_stack', NULL, 99, true, 40),
    ('case_binding_standard', 'Case Binding', 'Cover consumed at Archive Binding.', 'archive', 'case_binding', 'core_collectible', 'case_binding', 1, true, 0),
    ('binding_mutation_roll', 'Binding Mutation Roll', 'Display-only binding mutation.', 'archive', 'binding_mutation_roll', 'core_collectible', 'binding_mutation', 1, false, 0),
    ('archive_key', 'Archive Key', 'Unbinds a Bound move from loadout.', 'archive', 'unbind_bound_move', 'core_collectible', 'archive_key', 1, false, 50),
    ('signature_extract', 'Signature Extract', 'Removes a move from a bound encyclopedia.', 'archive', 'extract_book_move', 'core_collectible', 'signature_extract', 1, false, 75),
    ('archive_severance', 'Archive Severance', 'Fully unbinds a bound encyclopedia.', 'archive', 'unbind_archive_book', 'core_collectible', 'archive_severance', 1, false, 200)
ON CONFLICT (item_id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    category = EXCLUDED.category,
    effect_family = EXCLUDED.effect_family,
    ownership_kind = EXCLUDED.ownership_kind,
    core_kind_attribute = EXCLUDED.core_kind_attribute,
    max_stack = EXCLUDED.max_stack,
    bindery_only = EXCLUDED.bindery_only,
    use_ram_cost = EXCLUDED.use_ram_cost,
    updated_at = now();
CREATE TABLE IF NOT EXISTS public.chainbound_profile_item_stacks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    quantity INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_profile_item_stacks_qty_nonneg CHECK (quantity >= 0),
    CONSTRAINT chainbound_profile_item_stacks_unique UNIQUE (player_profile_id, item_id)
);
CREATE INDEX IF NOT EXISTS idx_chainbound_profile_item_stacks_profile
    ON public.chainbound_profile_item_stacks (player_profile_id);
DROP TRIGGER IF EXISTS trg_chainbound_profile_item_stacks_touch_updated_at
    ON public.chainbound_profile_item_stacks;
CREATE TRIGGER trg_chainbound_profile_item_stacks_touch_updated_at
    BEFORE UPDATE ON public.chainbound_profile_item_stacks
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
CREATE TABLE IF NOT EXISTS public.chainbound_core_item_ownership_cache (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_public_key TEXT NOT NULL,
    cluster TEXT NOT NULL DEFAULT 'devnet',
    asset_id TEXT NOT NULL,
    item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    core_kind TEXT NOT NULL,
    collection TEXT NOT NULL,
    update_authority TEXT NULL,
    schema_version INT NOT NULL DEFAULT 1,
    verification_status TEXT NOT NULL DEFAULT 'verified',
    verification_error TEXT NULL,
    last_verified_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_core_item_wallet_nonempty CHECK (length(trim(wallet_public_key)) > 0),
    CONSTRAINT chainbound_core_item_asset_nonempty CHECK (length(trim(asset_id)) > 0),
    CONSTRAINT chainbound_core_item_unique UNIQUE (player_profile_id, wallet_public_key, cluster, asset_id)
);
CREATE INDEX IF NOT EXISTS idx_chainbound_core_item_ownership_profile
    ON public.chainbound_core_item_ownership_cache (player_profile_id, wallet_public_key, cluster);
DROP TRIGGER IF EXISTS trg_chainbound_core_item_ownership_touch_updated_at
    ON public.chainbound_core_item_ownership_cache;
CREATE TRIGGER trg_chainbound_core_item_ownership_touch_updated_at
    BEFORE UPDATE ON public.chainbound_core_item_ownership_cache
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
CREATE TABLE IF NOT EXISTS public.chainbound_core_item_consume_intents (
    consume_intent_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_public_key TEXT NOT NULL,
    cluster TEXT NOT NULL DEFAULT 'devnet',
    item_id TEXT NOT NULL,
    asset_id TEXT NOT NULL,
    reason TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'prepared',
    unsigned_tx_base64 TEXT NULL,
    tx_signature TEXT NULL,
    action_summary TEXT NOT NULL DEFAULT '',
    estimated_fee_lamports BIGINT NOT NULL DEFAULT 0,
    error TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    finalized_at TIMESTAMPTZ NULL,
    CONSTRAINT chainbound_core_item_consume_status_valid CHECK (
        status IN ('prepared', 'submitted', 'finalized', 'failed', 'expired')
    )
);
CREATE INDEX IF NOT EXISTS idx_chainbound_core_item_consume_intents_profile
    ON public.chainbound_core_item_consume_intents (player_profile_id, status);
