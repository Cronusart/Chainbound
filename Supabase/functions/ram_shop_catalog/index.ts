import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import { catalogResponse } from "../_shared/ram_shop.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req) ?? {};
  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }
    return jsonResponse(await catalogResponse(supabase, profile.player_profile_id));
  } catch (error) {
    console.error("[ram_shop_catalog]", error);
    return jsonResponse({
      success: false,
      error: "ram_shop_catalog_failed",
      checkout_available: false,
      bundles: [],
      tenders: [],
    }, 500);
  }
});
