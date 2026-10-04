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
import { preparePurchase, RamShopError } from "../_shared/ram_shop.ts";

const ALLOWED_KEYS = new Set(["sku", "tender"]);

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);
  if (Object.keys(body).some((key) => !ALLOWED_KEYS.has(key))) {
    return jsonResponse({ success: false, error: "unexpected_prepare_field" }, 400);
  }
  const sku = str(body.sku);
  const tender = str(body.tender);
  if (!sku || !tender) return jsonResponse({ success: false, error: "sku_and_tender_required" }, 400);
  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, {});
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    return jsonResponse(await preparePurchase(supabase, profile.player_profile_id, sku, tender));
  } catch (error) {
    const status = error instanceof RamShopError ? error.httpStatus : 500;
    const code = error instanceof RamShopError ? error.code : "ram_shop_prepare_failed";
    console.error("[ram_shop_prepare]", code, error);
    return jsonResponse({ success: false, error: code }, status);
  }
});
