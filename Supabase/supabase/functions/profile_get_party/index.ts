import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import { loadChainlingLoadouts } from "../_shared/chainling_loadouts.ts";
import { applyChainlingPassiveRecovery } from "../_shared/chainling_passive_recovery.ts";

interface ChainlingRow {
  id: string;
  species_id: string;
  stars: number | null;
  mastery_xp: number | null;
  rebirth_count: number | null;
  evolution_stage: number | null;
  display_name: string | null;
  ivs: number[] | null;
  rebirth_iv_bonuses: number[] | null;
  entangled_flag: boolean | null;
  entangled_type: string | null;
  mutation_id: string | null;
  visual_variant_id: string | null;
  visual_identity: Record<string, unknown> | null;
  visual_identity_asset_ids: Record<string, unknown> | null;
  current_hp: number | null;
}

const DEFAULT_VISUAL_IDENTITY = {
  aura_id: "none",
  hud_card_variant_id: "default",
  swap_in_fx_id: "default",
  swap_out_fx_id: "default",
  ko_fx_id: "none",
  victory_pose_id: "default",
  idle_style_id: "none",
  idle_flourish_id: "none",
  melee_attack_style_id: "none",
  ranged_attack_style_id: "none",
  hit_reaction_style_id: "none",
  attacker_ko_style_id: "none",
};

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    await applyChainlingPassiveRecovery(supabase, profile.player_profile_id);

    const { data: chainlings, error } = await supabase
      .from("chainlings")
      .select(
        "id, species_id, stars, mastery_xp, rebirth_count, evolution_stage, display_name, ivs, rebirth_iv_bonuses, entangled_flag, entangled_type, mutation_id, visual_variant_id, visual_identity, visual_identity_asset_ids, party_slot, current_hp",
      )
      .eq("player_profile_id", profile.player_profile_id)
      .eq("is_party_member", true)
      .order("party_slot", { ascending: true });

    if (error) throw error;

    const rows = (chainlings ?? []) as ChainlingRow[];
    const loadouts = await loadChainlingLoadouts(supabase, rows.map((row) => row.id));
    const party = rows.map((row) => ({
      backend_chainling_id: row.id,
      species_id: row.species_id,
      stars: row.stars ?? 0,
      mastery_xp: row.mastery_xp ?? 0,
      rebirth_count: row.rebirth_count ?? 0,
      evolution_stage: row.evolution_stage ?? 1,
      display_name: row.display_name ?? "",
      ivs: Array.isArray(row.ivs) ? row.ivs : [],
      rebirth_iv_bonuses: Array.isArray(row.rebirth_iv_bonuses) ? row.rebirth_iv_bonuses : [],
      entangled_flag: row.entangled_flag ?? false,
      entangled_type: resolveEntangledType(row.entangled_type, row.entangled_flag),
      mutation_id: resolveMutationId(row.mutation_id),
      visual_variant_id: resolveVisualVariantId(row.visual_variant_id),
      visual_identity: normalizeVisualIdentity(row.visual_identity, row.mutation_id),
      visual_identity_asset_ids: normalizeStringRecord(row.visual_identity_asset_ids),
      current_hp: row.current_hp ?? -1,
      move_ids: loadouts.get(row.id)?.move_ids ?? [],
      modifier1_section_ids: loadouts.get(row.id)?.modifier1_section_ids ?? [],
      modifier2_section_ids: loadouts.get(row.id)?.modifier2_section_ids ?? [],
    }));

    return jsonResponse({ player_profile_id: profile.player_profile_id, party });
  } catch (error) {
    console.error("[profile_get_party]", error);
    return jsonResponse({ success: false, error: "profile_get_party_failed" }, 500);
  }
});

function resolveVisualVariantId(value: string | null): string {
  const normalized = typeof value === "string" ? value.trim() : "";
  if (!normalized || normalized.toLowerCase() === "entangled") return "base";
  return normalized;
}

function resolveEntangledType(value: string | null, entangledFlag: boolean | null): string {
  if (!entangledFlag) return "none";
  const normalized = typeof value === "string" ? value.trim() : "";
  return normalized || "none";
}

function resolveMutationId(value: string | null): string {
  const normalized = typeof value === "string" ? value.trim() : "";
  return normalized || "none";
}

function normalizeVisualIdentity(
  value: Record<string, unknown> | null,
  mutationId: string | null = null,
): Record<string, string> {
  const source: Record<string, unknown> = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  const hudCardVariantId = hudCardSlot(source.hud_card_variant_id, source.nameplate_frame_id);
  const resolvedMutation = resolveMutationId(mutationId);
  const auraFallback = resolvedMutation !== "none" ? resolvedMutation : DEFAULT_VISUAL_IDENTITY.aura_id;

  return {
    aura_id: stringSlot(source.aura_id, auraFallback),
    hud_card_variant_id: hudCardVariantId,
    swap_in_fx_id: stringSlot(source.swap_in_fx_id ?? source.spawn_fx_id, DEFAULT_VISUAL_IDENTITY.swap_in_fx_id),
    swap_out_fx_id: stringSlot(source.swap_out_fx_id ?? source.ko_swap_fx_id, DEFAULT_VISUAL_IDENTITY.swap_out_fx_id),
    ko_fx_id: stringSlot(source.ko_fx_id ?? source.defender_ko_fx_id, DEFAULT_VISUAL_IDENTITY.ko_fx_id),
    victory_pose_id: stringSlot(source.victory_pose_id, DEFAULT_VISUAL_IDENTITY.victory_pose_id),
    idle_style_id: stringSlot(source.idle_style_id, DEFAULT_VISUAL_IDENTITY.idle_style_id),
    idle_flourish_id: stringSlot(source.idle_flourish_id, DEFAULT_VISUAL_IDENTITY.idle_flourish_id),
    melee_attack_style_id: stringSlot(source.melee_attack_style_id, DEFAULT_VISUAL_IDENTITY.melee_attack_style_id),
    ranged_attack_style_id: stringSlot(source.ranged_attack_style_id, DEFAULT_VISUAL_IDENTITY.ranged_attack_style_id),
    hit_reaction_style_id: stringSlot(source.hit_reaction_style_id, DEFAULT_VISUAL_IDENTITY.hit_reaction_style_id),
    attacker_ko_style_id: stringSlot(
      source.attacker_ko_style_id ?? source.enemy_ko_style_id,
      DEFAULT_VISUAL_IDENTITY.attacker_ko_style_id,
    ),
  };
}

function normalizeStringRecord(value: Record<string, unknown> | null): Record<string, string> {
  const source: Record<string, unknown> = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  const result: Record<string, string> = {};
  for (const [key, rawValue] of Object.entries(source)) {
    const normalized = stringSlot(rawValue, "");
    if (normalized) result[key] = normalized;
  }
  return result;
}

function hudCardSlot(primary: unknown, legacy: unknown): string {
  const primaryValue = stringSlot(primary, "");
  if (primaryValue && primaryValue !== "none") return primaryValue;

  const legacyValue = stringSlot(legacy, "");
  if (legacyValue && legacyValue !== "none") return legacyValue;

  return DEFAULT_VISUAL_IDENTITY.hud_card_variant_id;
}

function stringSlot(value: unknown, fallback: string): string {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : fallback;
}
