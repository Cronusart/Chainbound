import { publishStampedMetadata } from "../../Supabase/functions/_shared/ownership_metadata.ts";
import { supabaseAdmin } from "../../Supabase/functions/_shared/account_helpers.ts";
import {
  authoritySecretBytes,
  deriveStampLockForModifiers,
  loadRollConfig,
  normalizeAcquisition,
  parseModifierEffectValues,
  stampEffectFieldsFromOperation,
} from "../../Supabase/functions/_shared/ownership_spine.ts";

function requiredArg(name: string): string {
  const index = Deno.args.indexOf(name);
  const value = index >= 0 ? Deno.args[index + 1]?.trim() : "";
  if (!value) throw new Error(`missing_${name.replace(/^--/, "")}`);
  return value;
}

function optionalArg(name: string): string {
  const index = Deno.args.indexOf(name);
  return index >= 0 ? Deno.args[index + 1]?.trim() ?? "" : "";
}

const assetAddress = requiredArg("--asset");
const renderVersionText = optionalArg("--render-version");
const renderVersion = renderVersionText ? Number(renderVersionText) : undefined;
if (
  renderVersion !== undefined &&
  (!Number.isInteger(renderVersion) || renderVersion < 1)
) {
  throw new Error("invalid_render_version");
}
const replaceExisting = Deno.args.includes("--replace-existing");
if (replaceExisting && renderVersion === undefined) {
  throw new Error("replace_existing_requires_explicit_render_version");
}
const supabaseUrl = (Deno.env.get("SUPABASE_URL") ?? "").replace(/\/+$/, "");
if (!supabaseUrl) throw new Error("SUPABASE_URL is required");
const metadataUriBase =
  (Deno.env.get("CHAINBOUND_OWNERSHIP_METADATA_URI_BASE") ??
    `${supabaseUrl}/storage/v1/object/public/chainbound-ownership-metadata`)
    .replace(/\/+$/, "");

const supabase = supabaseAdmin();
const { data: operation, error: operationError } = await supabase
  .from("ownership_stamp_operations")
  .select("*")
  .eq("stamped_asset_address", assetAddress)
  .maybeSingle();
if (operationError) throw operationError;
if (!operation) throw new Error("stamp_operation_not_found");

const knownId = String(operation.known_id ?? "");
const { data: known, error: knownError } = await supabase
  .from("ownership_known_moves")
  .select("move_id,base_power,acquisition")
  .eq("known_id", knownId)
  .maybeSingle();
if (knownError) throw knownError;
const verifiedAsset = operation.verified_asset_json &&
    typeof operation.verified_asset_json === "object"
  ? operation.verified_asset_json as Record<string, unknown>
  : {};
const moveId = String(known?.move_id ?? verifiedAsset.moveId ?? "");
if (!moveId) throw new Error("stamp_move_id_not_found");

const modifierIds = Array.isArray(operation.modifier_ids)
  ? operation.modifier_ids.map((value: unknown) => String(value)).filter(Boolean)
  : [];
const roll = loadRollConfig();
let modifierEffectValues = parseModifierEffectValues(operation.modifier_effect_values);
if (!modifierEffectValues && modifierIds.length > 0) {
  modifierEffectValues = await deriveStampLockForModifiers(
    authoritySecretBytes(roll.resultAuthority),
    String(operation.player_profile_id ?? ""),
    knownId,
    modifierIds,
    Number(operation.generator_version ?? roll.generatorVersion),
    String(operation.pool_hash ?? roll.modifierPoolHash),
  );
}

const published = await publishStampedMetadata(
  supabase,
  metadataUriBase,
  {
    assetAddress,
    walletAddress: String(operation.wallet_address ?? ""),
    knownId,
    onchainInstanceId: String(operation.onchain_instance_id ?? ""),
    moveId,
    basePower: Number(known?.base_power ?? 0),
    modifierIds,
    rarity: Number(operation.rarity ?? 0),
    generatorVersion: Number(operation.generator_version ?? 0),
    poolHash: String(operation.pool_hash ?? ""),
    acquisition: normalizeAcquisition(String(operation.acquisition ?? known?.acquisition ?? "")),
    modifierEffectValues,
    cluster: roll.cluster,
  },
  {
    renderVersion,
    replaceExisting,
    mediaCacheNonce: replaceExisting ? `repair-v${renderVersion}` : undefined,
  },
);

if (modifierEffectValues && modifierEffectValues.length === modifierIds.length) {
  const lockFields = stampEffectFieldsFromOperation({
    modifier_count: modifierIds.length,
    modifier_effect_values: modifierEffectValues,
  });
  const nextVerified = {
    ...verifiedAsset,
    ...lockFields,
  };
  const { error: persistError } = await supabase.from("ownership_stamp_operations").update({
    modifier_effect_values: modifierEffectValues,
    verified_asset_json: Object.keys(verifiedAsset).length > 0 ? nextVerified : operation.verified_asset_json,
    updated_at: new Date().toISOString(),
  }).eq("stamped_asset_address", assetAddress);
  if (persistError) throw persistError;
}

console.log(
  JSON.stringify({ success: true, assetAddress, ...published }, null, 2),
);
