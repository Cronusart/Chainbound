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
import { isDisplayNameCharsetValid } from "../_shared/display_name_charset.ts";

interface CommitStarterResult extends JsonMap {
  success: boolean;
  error?: string;
  player_profile_id?: string;
  chainling_id?: string;
  species_id?: string;
  move_ids?: string[];
  created?: boolean;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const starterSpeciesId = str(body.starter_species_id);
  const displayName = str(body.display_name).trim();
  if (!starterSpeciesId) {
    return jsonResponse({ success: false, error: "starter_species_id is required" }, 400);
  }
  if (displayName.length > 0 && (displayName.length > 16 || !isDisplayNameCharsetValid(displayName))) {
    return jsonResponse({ success: false, error: "invalid_display_name" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_commit_starter_tx", {
      p_profile_id: profile.player_profile_id,
      p_species_id: starterSpeciesId,
      p_display_name: displayName || null,
    });

    if (error) throw error;

    const result = data as CommitStarterResult | null;
    if (!result?.success) {
      const errorCode = result?.error ?? "starter_commit_rejected";
      const status = errorCode === "invalid_starter_species" || errorCode === "invalid_display_name" ? 400 : 409;
      return jsonResponse({ success: false, error: errorCode }, status);
    }

    return jsonResponse(result);
  } catch (error) {
    console.error("[profile_commit_starter]", error);
    return jsonResponse({ success: false, error: "profile_commit_starter_failed" }, 500);
  }
});
