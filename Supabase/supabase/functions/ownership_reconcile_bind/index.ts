import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import { advanceBindOperation } from "../_shared/ownership_bind.ts";
import { OwnershipConfigurationError } from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req) ?? {};
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const walletAddress = activeWallet ? str(activeWallet.wallet_public_key) : "";
    const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
    if (!walletAddress || (requestedWallet && requestedWallet !== walletAddress)) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    let query = supabase.from("ownership_bind_operations").select("*")
      .eq("player_profile_id", profile.player_profile_id).eq("wallet_address", walletAddress);
    const operationId = str(body.operation_id);
    const idempotencyKey = str(body.idempotency_key);
    if (operationId) query = query.eq("operation_id", operationId);
    else if (idempotencyKey) query = query.eq("idempotency_key", idempotencyKey);
    else query = query.in("status", ["prepared", "submitted", "chain_confirmed"]);
    const { data: operation, error } = await query.order("created_at", { ascending: true }).limit(1).maybeSingle();
    if (error) throw error;
    if (!operation) return jsonResponse({ success: true, pending: false, operation_status: "none" });
    const result = await advanceBindOperation(
      supabase,
      profile.player_profile_id,
      operation as JsonMap,
      str(body.signature),
    );
    return jsonResponse(result, result.pending === true ? 202 : 200);
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    const message = error instanceof Error ? error.message : "unknown_error";
    if (message === "bind_signature_transaction_mismatch") {
      return jsonResponse({ success: false, error: message }, 409);
    }
    console.error("[ownership_reconcile_bind]", message);
    return jsonResponse({ success: false, error: "ownership_reconcile_bind_failed" }, 500);
  }
});
