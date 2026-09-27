import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  recordLinkEvent,
  requirePost,
  resolveAuthenticatedProfile,
  sessionResponse,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

    const activePlatform = normalizePlatform(body.active_platform_build);
    const { data: updated, error } = await supabase
      .from("player_profiles")
      .update({ has_started_game: true, last_platform_seen: activePlatform })
      .eq("player_profile_id", profile.player_profile_id)
      .select("*")
      .single();
    if (error) throw error;

    await recordLinkEvent(supabase, profile.player_profile_id, "game_started", "supabase_anonymous", "success");
    return jsonResponse(await sessionResponse(supabase, updated, "guest", activePlatform));
  } catch (error) {
    console.error("[account_begin_game]", error);
    return jsonResponse({ success: false, error: "account_begin_game_failed" }, 500);
  }
});
