import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  bool,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import { isProgressionFlagKeyValid } from "../_shared/progression_flag_key.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const flagKey = str(body.flag_key);
  if (!isProgressionFlagKeyValid(flagKey)) {
    return jsonResponse({ success: false, error: "invalid_flag_key" }, 400);
  }

  const flagValue = bool(body.flag_value);
  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data: profileRow, error: readError } = await supabase
      .from("player_profiles")
      .select("progression_state")
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    if (readError) throw readError;

    const current = asObject((profileRow as JsonMap | null)?.progression_state);
    const next = { ...current, [flagKey]: flagValue };

    const { error: updateError } = await supabase
      .from("player_profiles")
      .update({ progression_state: next })
      .eq("player_profile_id", profile.player_profile_id);

    if (updateError) throw updateError;

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      progression_state: next,
    });
  } catch (error) {
    console.error("[profile_set_progression_flag]", error);
    return jsonResponse({ success: false, error: "profile_set_progression_flag_failed" }, 500);
  }
});

function asObject(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}
