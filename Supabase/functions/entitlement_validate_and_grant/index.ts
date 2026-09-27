import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getProfileById,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const playerProfileId = str(body.player_profile_id);
  if (!playerProfileId) return jsonResponse({ success: false, error: "player_profile_id is required" }, 400);

  const sourceType = str(body.source_type);
  const allowUnvalidated = Deno.env.get("ALLOW_UNVALIDATED_ENTITLEMENT_GRANTS") === "true";
  const providerValidated = sourceType === "manual_grant" || sourceType === "test" || allowUnvalidated;

  if (!providerValidated) {
    return jsonResponse({
      success: false,
      error: "validation_provider_not_configured",
      message: "Storefront, ad, and wallet commerce validation must be provider-specific.",
    }, 501);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await getProfileById(supabase, playerProfileId);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase
      .from("entitlement_ledger")
      .insert({
        player_profile_id: playerProfileId,
        source_type: sourceType,
        source_platform: str(body.source_platform),
        source_tx_id: str(body.source_tx_id) || null,
        sku_or_product_id: str(body.sku_or_product_id) || null,
        amount: Number(body.amount ?? 0),
        currency_type: str(body.currency_type),
        status: "granted",
        metadata_json: typeof body.metadata_json === "object" && body.metadata_json !== null ? body.metadata_json : {},
      })
      .select("id, status")
      .single();

    if (error) throw error;
    return jsonResponse({ success: true, ledger_id: data.id, status: data.status });
  } catch (error) {
    console.error("[entitlement_validate_and_grant]", error);
    return jsonResponse({ success: false, error: "entitlement_validate_and_grant_failed" }, 500);
  }
});
