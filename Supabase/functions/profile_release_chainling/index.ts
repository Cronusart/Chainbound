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

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const chainlingId = str(body.chainling_id);
  if (!chainlingId) return jsonResponse({ success: false, error: "chainling_id_required" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_release_chainling", {
      p_profile_id: profile.player_profile_id,
      p_chainling_id: chainlingId,
    });

    if (error) {
      console.error("[profile_release_chainling] rpc", error);
      return jsonResponse(
        { success: false, error: error.message || "release_failed" },
        400,
      );
    }

    const result = (data ?? {}) as JsonMap;
    if (result.ok !== true) {
      return jsonResponse({
        success: false,
        error: str(result.error) || "release_failed",
      }, 409);
    }

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      chainling_id: chainlingId,
    });
  } catch (error) {
    console.error("[profile_release_chainling]", error);
    return jsonResponse({ success: false, error: "release_failed" }, 500);
  }
});
