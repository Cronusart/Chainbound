import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet, JsonMap, jsonResponse, readJsonBody, requirePost, resolveAuthenticatedProfile,
  str, supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  fetchAndVerifyStampedAsset, loadRollConfig, loadOwnershipConfig, OwnershipConfigurationError,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req); if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  try {
    loadRollConfig();
    const { data: knownRows, error: knownError } = await supabase.from("ownership_known_moves").select("*")
      .eq("player_profile_id", profile.player_profile_id).is("consumed_at", null).order("created_at", { ascending: true });
    if (knownError) throw knownError;
    const known = (knownRows ?? []).map((row: JsonMap) => ({ instanceId: str(row.known_id), moveId: str(row.move_id),
      modifierCount: 0, generatorVersion: Number(row.generator_version), poolHash: str(row.pool_hash) }));
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
    const wallet = activeWallet ? str(activeWallet.wallet_public_key) : "";
    if (requestedWallet && requestedWallet !== wallet) return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    if (!wallet) return jsonResponse({ success: true, known, stamped: [] });

    const config = loadOwnershipConfig();
    const { data: operations, error: operationError } = await supabase.from("ownership_stamp_operations")
      .select("*").eq("player_profile_id", profile.player_profile_id).eq("wallet_address", wallet).eq("status", "confirmed");
    if (operationError) throw operationError;
    const stamped: JsonMap[] = [];
    for (const operation of operations ?? []) {
      const verified = await fetchAndVerifyStampedAsset(config, str(operation.stamped_asset_address), wallet);
      if (!verified || verified.knownOnchainInstanceId !== String(operation.onchain_instance_id)) continue;
      const cached = operation.verified_asset_json as JsonMap | null;
      const item: JsonMap = { ...verified, knownInstanceId: str(operation.known_id), moveId: str(cached?.moveId) || verified.moveId };
      delete item.knownOnchainInstanceId;
      stamped.push(item);
    }
    return jsonResponse({ success: true, known, stamped });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_get_inventory]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_get_inventory_failed" }, 500);
  }
});

