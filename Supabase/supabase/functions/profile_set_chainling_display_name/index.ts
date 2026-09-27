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

const NAME_RE = /^[\p{L}][\p{L} '-]*$/u;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);

  const chainlingId = str(body.chainling_id);
  const displayName = str(body.display_name);
  if (!chainlingId) {
    return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  }
  if (displayName.length < 1 || displayName.length > 16 || !NAME_RE.test(displayName) || displayName.includes("  ")) {
    return jsonResponse({ success: false, error: "invalid_display_name" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_set_chainling_display_name_tx", {
      p_profile_id: profile.player_profile_id,
      p_chainling_id: chainlingId,
      p_display_name: displayName,
    });
    if (error) throw error;

    const result = (data ?? {}) as JsonMap;
    return jsonResponse(result, result.success === true ? 200 : 400);
  } catch (error) {
    console.error("[profile_set_chainling_display_name]", error);
    return jsonResponse({ success: false, error: "profile_set_chainling_display_name_failed" }, 500);
  }
});
