// New claims are rejected when any other non-blank player display name matches
// ignoring case, including grandfathered rows (rule a). The partial unique
// index only covers rows stamped by profile_set_display_name_tx. The same
// profile may re-set its own name. Chainling names are not unique.
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
import {
  isPlayerDisplayNameLengthValid,
  isPlayerDisplayNameUniqueViolation,
  normalizePlayerDisplayName,
  PLAYER_DISPLAY_NAME_TAKEN,
} from "../_shared/player_display_name.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);

  const displayName = normalizePlayerDisplayName(str(body.display_name));
  if (!isPlayerDisplayNameLengthValid(displayName)) {
    return jsonResponse({ success: false, error: "invalid_display_name" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_set_display_name_tx", {
      p_profile_id: profile.player_profile_id,
      p_display_name: displayName,
    });

    if (error) {
      if (isPlayerDisplayNameUniqueViolation(error)) {
        return jsonResponse({ success: false, error: PLAYER_DISPLAY_NAME_TAKEN }, 409);
      }
      throw error;
    }

    const result = (data ?? {}) as JsonMap;
    if (result.success !== true) {
      const errorCode = str(result.error) || "profile_set_display_name_failed";
      const status = errorCode === PLAYER_DISPLAY_NAME_TAKEN
        ? 409
        : errorCode === "invalid_display_name"
        ? 400
        : errorCode === "profile_not_found"
        ? 404
        : 500;
      return jsonResponse({ success: false, error: errorCode }, status);
    }

    return jsonResponse({
      success: true,
      player_profile_id: str(result.player_profile_id) || profile.player_profile_id,
      display_name: str(result.display_name) || displayName,
    });
  } catch (error) {
    console.error("[profile_set_display_name]", error);
    if (isPlayerDisplayNameUniqueViolation(error as { code?: string; message?: string; details?: string })) {
      return jsonResponse({ success: false, error: PLAYER_DISPLAY_NAME_TAKEN }, 409);
    }
    return jsonResponse({ success: false, error: "profile_set_display_name_failed" }, 500);
  }
});
