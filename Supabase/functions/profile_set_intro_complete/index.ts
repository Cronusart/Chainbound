import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { error: profileError } = await supabase
      .from("player_profiles")
      .update({ intro_complete: true, has_started_game: true })
      .eq("player_profile_id", profile.player_profile_id);

    if (profileError) throw profileError;

    return jsonResponse({ success: true, player_profile_id: profile.player_profile_id });
  } catch (error) {
    console.error("[profile_set_intro_complete]", error);
    return jsonResponse({ success: false, error: "profile_set_intro_complete_failed" }, 500);
  }
});
