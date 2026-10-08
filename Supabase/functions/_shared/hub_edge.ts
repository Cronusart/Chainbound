import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requireAuthenticatedUser,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "./account_helpers.ts";

export async function handleHub(req: Request, op: "config" | "unlock" | "link" | "card"): Promise<Response> {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);

  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

  if (op === "config") {
    return rpc(supabase, "hub_config_service", { p_profile_id: profile.player_profile_id });
  }
  if (op === "unlock") {
    return rpc(supabase, "hub_unlock_service", { p_profile_id: profile.player_profile_id });
  }
  if (op === "link") {
    const authenticated = await requireAuthenticatedUser(req);
    if (!authenticated) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    return rpc(supabase, "hub_link_identity_service", {
      p_profile_id: profile.player_profile_id,
      p_supabase_user_id: authenticated.userId,
      p_ugs_player_id: str(body.ugs_player_id),
    });
  }

  return rpc(supabase, "hub_presence_card_service", {
    p_ugs_player_id: str(body.ugs_player_id),
  });
}

async function rpc(supabase: SupabaseClient, fn: string, args: JsonMap): Promise<Response> {
  const { data, error } = await supabase.rpc(fn, args);
  if (error) {
    console.error("[Hub]", fn, error.message);
    return jsonResponse({ success: false, error: "hub_failed" }, 500);
  }
  if (!data || typeof data !== "object") return jsonResponse({ success: false, error: "empty_rpc" });
  return jsonResponse(data as JsonMap);
}
