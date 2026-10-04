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
import { advanceBindOperation } from "../_shared/ownership_bind.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body || Object.keys(body).some((key) => key !== "operation_id")) {
    return jsonResponse({ success: false, error: "operation_id_only" }, 400);
  }
  const operationId = str(body.operation_id);
  if (!operationId) return jsonResponse({ success: false, error: "operation_id_required" }, 400);
  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, {});
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    const { data, error } = await supabase.from("ram_ledger_operations")
      .select(
        "operation_id, operation_kind, direction, amount, state, cluster, wallet_address, chain_signature, receipt_address, metadata_json, created_at, updated_at, settled_at",
      )
      .eq("player_profile_id", profile.player_profile_id).eq("operation_id", operationId).maybeSingle();
    if (error) throw error;
    if (!data) return jsonResponse({ success: false, error: "operation_not_found" }, 404);
    let recovery: JsonMap | null = null;
    if (str(data.operation_kind) === "bind" && !["settled", "released", "failed"].includes(str(data.state))) {
      const { data: bind, error: bindError } = await supabase.from("ownership_bind_operations").select("*")
        .eq("operation_id", operationId).eq("player_profile_id", profile.player_profile_id).maybeSingle();
      if (bindError) throw bindError;
      if (bind) recovery = await advanceBindOperation(supabase, profile.player_profile_id, bind as JsonMap);
    }
    return jsonResponse({
      success: true,
      operation: { ...data, amount: String(data.amount) },
      recovery,
    }, recovery?.pending === true ? 202 : 200);
  } catch (error) {
    console.error("[ram_operation_status]", error);
    return jsonResponse({ success: false, error: "ram_operation_status_failed" }, 500);
  }
});
