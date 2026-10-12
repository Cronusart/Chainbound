import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  bool,
  JsonMap,
  jsonResponse,
  readJsonBody,
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

    const progression = { ...(profile.progression_state ?? {}) } as JsonMap;
    // This checkpoint is intentionally monotonic: a completed/skipped intro
    // should never regress because of a stale client retry.
    progression.intro_cinematic_complete = bool(progression.intro_cinematic_complete) ||
      bool(body.intro_cinematic_complete);
    const { data: updated, error } = await supabase
      .from("player_profiles")
      .update({ progression_state: progression })
      .eq("player_profile_id", profile.player_profile_id)
      .select("*")
      .single();
    if (error) throw error;

    return jsonResponse(await sessionResponse(supabase, updated, "guest"));
  } catch (error) {
    console.error("[account_set_onboarding_state]", error);
    return jsonResponse({ success: false, error: "account_set_onboarding_state_failed" }, 500);
  }
});
