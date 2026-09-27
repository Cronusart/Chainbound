import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet, JsonMap, jsonResponse, readJsonBody, requirePost, resolveAuthenticatedProfile,
  SOLANA_BASE58_RE, str, supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  fetchAndVerifyStampedAsset, loadOwnershipConfig, OwnershipConfigurationError, signatureIsConfirmed,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req); if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const knownId = str(body.known_instance_id); const idempotencyKey = str(body.idempotency_key);
  const walletAddress = str(body.wallet_address) || str(body.wallet_public_key); const signature = str(body.signature);
  if (!knownId || !idempotencyKey || !SOLANA_BASE58_RE.test(walletAddress) || !SOLANA_BASE58_RE.test(signature))
    return jsonResponse({ success: false, error: "invalid_confirm_request" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== walletAddress)
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    const config = loadOwnershipConfig();
    const { data: operation, error } = await supabase.from("ownership_stamp_operations").select("*")
      .eq("player_profile_id", profile.player_profile_id).eq("idempotency_key", idempotencyKey).maybeSingle();
    if (error) throw error;
    if (!operation || str(operation.known_id) !== knownId || str(operation.wallet_address) !== walletAddress)
      return jsonResponse({ success: false, error: "stamp_operation_not_found" }, 404);
    if (str(operation.status) === "confirmed") return jsonResponse({ success: true, confirmed: true,
      idempotent: true, signature: str(operation.signature), asset: operation.verified_asset_json ?? null });
    if (new Date(str(operation.expires_at)).getTime() < Date.now()) {
      await supabase.from("ownership_stamp_operations").update({ status: "expired", updated_at: new Date().toISOString() })
        .eq("operation_id", operation.operation_id);
      return jsonResponse({ success: false, error: "stamp_ticket_expired" }, 410);
    }
    if (!await signatureIsConfirmed(config.rpcUrl, signature))
      return jsonResponse({ success: false, error: "transaction_not_confirmed" }, 409);
    const verified = await fetchAndVerifyStampedAsset(config, str(operation.stamped_asset_address), walletAddress);
    if (!verified || verified.knownOnchainInstanceId !== String(operation.onchain_instance_id))
      return jsonResponse({ success: false, error: "stamped_asset_not_verified" }, 409);
    const publicAsset = { ...verified, knownInstanceId: knownId, moveId: str((await supabase.from("ownership_known_moves")
      .select("move_id").eq("known_id", knownId).single()).data?.move_id) };
    delete (publicAsset as Record<string, unknown>).knownOnchainInstanceId;
    const now = new Date().toISOString();
    const opUpdate = await supabase.from("ownership_stamp_operations").update({ status: "confirmed", signature,
      verified_asset_json: publicAsset, confirmed_at: now, updated_at: now }).eq("operation_id", operation.operation_id).eq("status", "prepared");
    if (opUpdate.error) throw opUpdate.error;
    const knownUpdate = await supabase.from("ownership_known_moves").update({ consumed_at: now,
      onchain_instance_id: String(operation.onchain_instance_id) }).eq("known_id", knownId).is("consumed_at", null);
    if (knownUpdate.error) throw knownUpdate.error;
    return jsonResponse({ success: true, confirmed: true, idempotent: false, signature, asset: publicAsset });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_confirm_stamp]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_confirm_stamp_failed" }, 500);
  }
});

