import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { JsonMap, jsonResponse, requirePost, str, supabaseAdmin } from "../_shared/account_helpers.ts";
import { publishStampedMetadata } from "../_shared/ownership_metadata.ts";
import {
  authoritySecretBytes,
  deriveStampLockForModifiers,
  loadRollConfig,
  normalizeAcquisition,
  parseModifierEffectValues,
  stampEffectFieldsFromOperation,
} from "../_shared/ownership_spine.ts";

// One-shot Devnet repair for the confirmed Hot Rod asset whose v6 metadata
// predates locked Stamp amplitudes. The target and render version cannot be
// redirected by request input. Revoking the token disables the endpoint.
const TARGET_ASSET = "BQHbysVnyYS8xPaxft2aHFHiVD9JgHNmVcR7GCi12aAs";
const TARGET_PROFILE = "38ac2f9a-5b94-4037-9bc6-fdd205632018";
const TARGET_RENDER_VERSION = 6;
const OPERATOR_TOKEN_ENV = "CHAINBOUND_METADATA_REPAIR_OPERATOR_TOKEN";
const OPERATOR_ENABLED = false;

function operatorAuthorized(req: Request): boolean {
  const expected = str(Deno.env.get(OPERATOR_TOKEN_ENV));
  const actual = str(req.headers.get("x-chainbound-operator-token"));
  return expected.length >= 32 && actual.length === expected.length && actual === expected;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  if (!OPERATOR_ENABLED) return jsonResponse({ success: false, error: "operator_disabled" }, 410);
  if (!operatorAuthorized(req)) return jsonResponse({ success: false, error: "operator_unauthorized" }, 401);

  try {
    const supabase = supabaseAdmin();
    const { data: operation, error: operationError } = await supabase
      .from("ownership_stamp_operations")
      .select("*")
      .eq("stamped_asset_address", TARGET_ASSET)
      .eq("player_profile_id", TARGET_PROFILE)
      .eq("status", "confirmed")
      .maybeSingle();
    if (operationError) throw operationError;
    if (!operation) return jsonResponse({ success: false, error: "confirmed_stamp_operation_not_found" }, 404);

    const knownId = str(operation.known_id);
    const { data: known, error: knownError } = await supabase
      .from("ownership_known_moves")
      .select("move_id,base_power,acquisition")
      .eq("known_id", knownId)
      .maybeSingle();
    if (knownError) throw knownError;
    const verifiedAsset = operation.verified_asset_json && typeof operation.verified_asset_json === "object"
      ? operation.verified_asset_json as JsonMap
      : {};
    const moveId = str(known?.move_id) || str(verifiedAsset.moveId);
    if (!moveId) throw new Error("stamp_move_id_not_found");

    const metadataUriBase = str(Deno.env.get("CHAINBOUND_OWNERSHIP_METADATA_URI_BASE"));
    if (!metadataUriBase.startsWith("https://")) throw new Error("metadata_uri_base_missing");
    const modifierIds = Array.isArray(operation.modifier_ids)
      ? operation.modifier_ids.map((value: unknown) => str(value)).filter(Boolean)
      : [];
    if (modifierIds.length !== 1 && modifierIds.length !== 2) {
      throw new Error("invalid_modifier_count");
    }

    const roll = loadRollConfig();
    let modifierEffectValues = parseModifierEffectValues(operation.modifier_effect_values);
    if (!modifierEffectValues) {
      modifierEffectValues = await deriveStampLockForModifiers(
        authoritySecretBytes(roll.resultAuthority),
        str(operation.player_profile_id),
        knownId,
        modifierIds,
        Number(operation.generator_version ?? roll.generatorVersion),
        str(operation.pool_hash) || roll.modifierPoolHash,
      );
    }
    if (modifierEffectValues.length !== modifierIds.length) {
      throw new Error("modifier_effect_values_shape_mismatch");
    }

    const published = await publishStampedMetadata(
      supabase,
      metadataUriBase,
      {
        assetAddress: TARGET_ASSET,
        walletAddress: str(operation.wallet_address),
        knownId,
        onchainInstanceId: str(operation.onchain_instance_id),
        moveId,
        basePower: Number(known?.base_power ?? 0),
        modifierIds,
        rarity: Number(operation.rarity ?? 0),
        generatorVersion: Number(operation.generator_version ?? 0),
        poolHash: str(operation.pool_hash),
        acquisition: normalizeAcquisition(str(operation.acquisition) || str(known?.acquisition)),
        modifierEffectValues,
        cluster: roll.cluster,
      },
      {
        renderVersion: TARGET_RENDER_VERSION,
        replaceExisting: true,
        mediaCacheNonce: "repair-v6-20260929",
      },
    );

    const lockFields = stampEffectFieldsFromOperation({
      modifier_count: modifierIds.length,
      modifier_effect_values: modifierEffectValues,
    });
    const nextVerifiedAsset = {
      ...verifiedAsset,
      ...lockFields,
    };
    const { data: persisted, error: persistError } = await supabase
      .from("ownership_stamp_operations")
      .update({
        modifier_effect_values: modifierEffectValues,
        verified_asset_json: nextVerifiedAsset,
        updated_at: new Date().toISOString(),
      })
      .eq("stamped_asset_address", TARGET_ASSET)
      .eq("player_profile_id", TARGET_PROFILE)
      .eq("status", "confirmed")
      .select("modifier_effect_values")
      .single();
    if (persistError) throw persistError;
    const persistedValues = parseModifierEffectValues(persisted?.modifier_effect_values);
    if (!persistedValues || persistedValues.length !== modifierIds.length) {
      throw new Error("modifier_effect_values_persist_failed");
    }

    return jsonResponse({
      success: true,
      repaired: true,
      asset_address: TARGET_ASSET,
      render_version: TARGET_RENDER_VERSION,
      modifier_effect_values: persistedValues,
      ...published,
    });
  } catch (error) {
    console.error(
      "[ownership_operator_repair_stamp_metadata]",
      error instanceof Error ? error.message : "unknown_error",
    );
    return jsonResponse({ success: false, error: "operator_metadata_repair_failed" }, 500);
  }
});
