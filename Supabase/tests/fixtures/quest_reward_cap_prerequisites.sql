-- Test-only relations for tests/quest_reward_daily_cap_v1.sql. Apply after the
-- RAM shop chain (ram_shop_minimal_prerequisites.sql header), then
--   20261014030000_fisher_ape_lucky_crayon.sql
--   20261028120200_quest_reward_daily_cap.sql
-- The item catalog and stacks predate the repository's migration history, so
-- profile_apply_profile_item_delta_tx is a stub here: it records the reward's
-- mutation key once, which is all the RAM cap path depends on. Item delta
-- behavior itself is covered by tests/profile_apply_profile_item_delta_v1.sql.
-- Never push this fixture to a hosted project.

CREATE TABLE public.chainbound_item_catalog (
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
INSERT INTO public.chainbound_item_catalog(item_id, display_name, category)
VALUES ('signal_orb', 'Signal Orb', 'capture');

CREATE TABLE public.chainbound_world_pickup_spawns (
    spawn_id TEXT PRIMARY KEY,
    item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    quantity INTEGER NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT true,
    notes TEXT NOT NULL DEFAULT '',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE public.chainbound_profile_item_mutations (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    idempotency_key TEXT NOT NULL,
    grants JSONB NOT NULL,
    consumes JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (player_profile_id, idempotency_key)
);

CREATE FUNCTION public.profile_apply_profile_item_delta_tx(
    p_profile_id UUID, p_idempotency_key TEXT, p_grants JSONB, p_consumes JSONB
) RETURNS JSONB LANGUAGE plpgsql SET search_path = public AS $$
BEGIN
    INSERT INTO public.chainbound_profile_item_mutations(player_profile_id, idempotency_key, grants, consumes)
    VALUES (p_profile_id, p_idempotency_key, p_grants, p_consumes)
    ON CONFLICT (player_profile_id, idempotency_key) DO NOTHING;
    RETURN jsonb_build_object('ok', true, 'already_applied', NOT FOUND);
END;
$$;
