import { publishStampedMetadata } from "../../Supabase/functions/_shared/ownership_metadata.ts";
import { supabaseAdmin } from "../../Supabase/functions/_shared/account_helpers.ts";

function requiredArg(name: string): string {
  const index = Deno.args.indexOf(name);
  const value = index >= 0 ? Deno.args[index + 1]?.trim() : "";
  if (!value) throw new Error(`missing_${name.replace(/^--/, "")}`);
  return value;
}

const assetAddress = requiredArg("--asset");
const supabaseUrl = (Deno.env.get("SUPABASE_URL") ?? "").replace(/\/+$/, "");
if (!supabaseUrl) throw new Error("SUPABASE_URL is required");
const metadataUriBase = (Deno.env.get("CHAINBOUND_OWNERSHIP_METADATA_URI_BASE") ??
  `${supabaseUrl}/storage/v1/object/public/chainbound-ownership-metadata`).replace(/\/+$/, "");

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
  .select("move_id,base_power")
  .eq("known_id", knownId)
  .single();
if (knownError) throw knownError;

const published = await publishStampedMetadata(
  supabase,
  metadataUriBase,
  {
    assetAddress,
    walletAddress: String(operation.wallet_address ?? ""),
    knownId,
    onchainInstanceId: String(operation.onchain_instance_id ?? ""),
    moveId: String(known.move_id ?? ""),
    basePower: Number(known.base_power ?? 0),
    modifierIds: Array.isArray(operation.modifier_ids)
      ? operation.modifier_ids.map((value: unknown) => String(value))
      : [],
    rarity: Number(operation.rarity ?? 0),
    generatorVersion: Number(operation.generator_version ?? 0),
    poolHash: String(operation.pool_hash ?? ""),
  },
);

console.log(JSON.stringify({ success: true, assetAddress, ...published }, null, 2));
