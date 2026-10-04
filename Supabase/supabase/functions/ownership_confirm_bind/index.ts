import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import { advanceBindOperation } from "../_shared/ownership_bind.ts";
import { isSolanaSignature, OwnershipConfigurationError } from "../_shared/ownership_spine.ts";

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const operationId = str(body.operation_id);
  const signature = str(body.signature);
  const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
  if (!UUID_RE.test(operationId) || !isSolanaSignature(signature) || !SOLANA_BASE58_RE.test(requestedWallet)) {
    return jsonResponse({ success: false, error: "invalid_bind_confirm_request" }, 400);
  }
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== requestedWallet) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    const { data: operation, error } = await supabase.from("ownership_bind_operations").select("*")
      .eq("operation_id", operationId).eq("player_profile_id", profile.player_profile_id).maybeSingle();
    if (error) throw error;
    if (!operation || str(operation.wallet_address) !== requestedWallet) {
      return jsonResponse({ success: false, error: "bind_operation_not_found" }, 404);
    }
    const result = await advanceBindOperation(supabase, profile.player_profile_id, operation as JsonMap, signature);
    return jsonResponse(result, result.pending === true ? 202 : 200);
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    const message = error instanceof Error ? error.message : "unknown_error";
    if (message === "bind_signature_transaction_mismatch") {
      return jsonResponse({ success: false, error: message }, 409);
    }
    console.error("[ownership_confirm_bind]", message);
    return jsonResponse({ success: false, error: "ownership_confirm_bind_failed" }, 500);
  }
});
