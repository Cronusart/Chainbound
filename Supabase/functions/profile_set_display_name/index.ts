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
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const displayName = str(body.display_name);
  if (displayName.length < 1 || displayName.length > 16) {
    return jsonResponse({ success: false, error: "display_name must be between 1 and 16 characters" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data: updatedProfile, error: profileError } = await supabase
      .from("player_profiles")
      .update({ display_name: displayName })
      .eq("player_profile_id", profile.player_profile_id)
      .select("display_name")
      .single();

    if (profileError) throw profileError;

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      display_name: updatedProfile?.display_name ?? displayName,
    });
  } catch (error) {
    console.error("[profile_set_display_name]", error);
    return jsonResponse({ success: false, error: "profile_set_display_name_failed" }, 500);
  }
});
