-- Test-only relations for tests/daily_login_reward_v1.sql and
-- tests/daily_login_reward_concurrency_v1.sql. Apply after the RAM shop chain
-- (ram_shop_minimal_prerequisites.sql header), then in order:
--   this fixture
--   20260930120000_profile_item_delta.sql   (the real item delta, not a stub)
--   20261030120000_daily_login_reward.sql
-- It also layers on top of quest_reward_cap_prerequisites.sql, but the real item
-- delta replaces that fixture's stub, so run quest_reward_daily_cap_v1.sql first
-- or use a fresh container.
-- Never push this fixture to a hosted project.

ALTER TABLE public.player_profiles
    ADD COLUMN IF NOT EXISTS progression_state JSONB NOT NULL DEFAULT '{}'::jsonb;

CREATE TABLE IF NOT EXISTS public.chainbound_item_catalog (
    item_id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL,
    effect_family TEXT NOT NULL DEFAULT 'none',
    ownership_kind TEXT NOT NULL DEFAULT 'profile_stack',
    max_stack INTEGER NOT NULL DEFAULT 99,
    usable_in_menu BOOLEAN NOT NULL DEFAULT false,
    usable_in_battle BOOLEAN NOT NULL DEFAULT false,
    bindery_only BOOLEAN NOT NULL DEFAULT false,
    use_ram_cost INTEGER NOT NULL DEFAULT 0,
    enabled BOOLEAN NOT NULL DEFAULT true,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
INSERT INTO public.chainbound_item_catalog(item_id, display_name, category, max_stack)
VALUES ('signal_orb', 'Signal Orb', 'capture', 99)
ON CONFLICT (item_id) DO NOTHING;

-- Same shape as 20260722030000_chainbound_player_item_inventory.sql.
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

-- Same columns and lifecycle checks as 20260822010000_profile_narrative_state_v1.sql.
CREATE TABLE IF NOT EXISTS public.profile_quest_states (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    quest_id TEXT NOT NULL,
    lifecycle TEXT NOT NULL,
    objective_progress JSONB NOT NULL DEFAULT '{}'::jsonb,
    resolution_id TEXT NULL,
    resolution_context JSONB NOT NULL DEFAULT '{}'::jsonb,
    revision BIGINT NOT NULL DEFAULT 0,
    content_version INTEGER NOT NULL DEFAULT 1,
    resolved_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, quest_id),
    CONSTRAINT profile_quest_states_lifecycle
        CHECK (lifecycle IN ('locked', 'available', 'active', 'completed', 'failed')),
    CONSTRAINT profile_quest_states_completed_resolution
        CHECK (lifecycle <> 'completed' OR NULLIF(BTRIM(COALESCE(resolution_id, '')), '') IS NOT NULL)
);
