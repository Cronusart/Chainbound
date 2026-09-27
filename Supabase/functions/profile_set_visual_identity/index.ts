import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  buildAssetHintsByCosmeticId,
  collectSelectedCosmeticIds,
  verifyCosmeticSelectionOwnership,
} from "../_shared/onchain_cosmetics.ts";

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

  const chainlingId = str(body.chainling_id);
  if (!chainlingId) {
    return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const visualIdentitySource = asObject(body.visual_identity);
    if (!("hud_card_variant_id" in visualIdentitySource) && "hud_card_variant_id" in body) {
      visualIdentitySource.hud_card_variant_id = body.hud_card_variant_id;
    }
    if (!("nameplate_frame_id" in visualIdentitySource) && "nameplate_frame_id" in body) {
      visualIdentitySource.nameplate_frame_id = body.nameplate_frame_id;
    }

    const visualIdentity = normalizeVisualIdentity(visualIdentitySource);
    const visualVariantId = normalizeVariant(str(body.visual_variant_id));
    const visualIdentityForOwnership = {
      visual_variant_id: visualVariantId,
      ...visualIdentity,
    };
    const visualIdentityAssetIdSource = asObject(body.visual_identity_asset_ids);
    const selectedCosmeticIds = collectSelectedCosmeticIds(visualIdentityForOwnership);
    const assetHintsByCosmeticId = buildAssetHintsByCosmeticId(
      visualIdentityForOwnership,
      visualIdentityAssetIdSource,
    );

    const ownership = await verifyCosmeticSelectionOwnership(
      supabase,
      profile.player_profile_id,
      str(body.wallet_address) || str(body.wallet_public_key),
      selectedCosmeticIds,
      assetHintsByCosmeticId,
    );
    if (!ownership.ok) {
      return jsonResponse({
        success: false,
        error: ownership.error ?? "cosmetic_ownership_verification_failed",
        missing_cosmetic_ids: ownership.missing_cosmetic_ids ?? [],
      }, ownership.status ?? 403);
    }

    const visualIdentityAssetIds = normalizeVisualIdentityAssetIds(
      visualIdentity,
      visualVariantId,
      ownership.asset_ids_by_cosmetic_id ?? {},
    );

    const { data, error } = await supabase
      .from("chainlings")
      .update({
        visual_variant_id: visualVariantId,
        visual_identity: visualIdentity,
        visual_identity_asset_ids: visualIdentityAssetIds,
      })
      .eq("id", chainlingId)
      .eq("player_profile_id", profile.player_profile_id)
      .select("id")
      .maybeSingle();

    if (error) throw error;
    if (!data) {
      return jsonResponse({ success: false, error: "chainling_not_found_or_not_owned" }, 404);
    }

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      chainling_id: chainlingId,
      visual_variant_id: visualVariantId,
      visual_identity: visualIdentity,
      visual_identity_asset_ids: visualIdentityAssetIds,
    });
  } catch (error) {
    console.error("[profile_set_visual_identity]", error);
    return jsonResponse({ success: false, error: "profile_set_visual_identity_failed" }, 500);
  }
});

function asObject(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function normalizeVariant(value: string): string {
  return value && value !== "none" ? value : "base";
}

function normalizeVisualIdentity(source: Record<string, unknown>): Record<string, string> {
  const hudCardVariantId = hudCardSlot(source.hud_card_variant_id, source.nameplate_frame_id);

  return {
    aura_id: stringSlot(source.aura_id, DEFAULT_VISUAL_IDENTITY.aura_id),
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

function normalizeVisualIdentityAssetIds(
  visualIdentity: Record<string, string>,
  visualVariantId: string,
  verifiedAssetIdsByCosmeticId: Record<string, string>,
): Record<string, string> {
  const result: Record<string, string> = {};
  assignAssetId(result, "visual_variant_id", visualVariantId, verifiedAssetIdsByCosmeticId);
  for (const [slotKey, cosmeticId] of Object.entries(visualIdentity)) {
    assignAssetId(result, slotKey, cosmeticId, verifiedAssetIdsByCosmeticId);
  }
  return result;
}

function assignAssetId(
  target: Record<string, string>,
  slotKey: string,
  cosmeticId: string,
  verifiedAssetIdsByCosmeticId: Record<string, string>,
): void {
  const assetId = verifiedAssetIdsByCosmeticId[cosmeticId];
  if (assetId) target[slotKey] = assetId;
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
