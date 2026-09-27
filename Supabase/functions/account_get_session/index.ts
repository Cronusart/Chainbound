import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
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
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
    }

    return jsonResponse(await sessionResponse(supabase, profile));
  } catch (error) {
    console.error("[account_get_session]", error);
    return jsonResponse({ success: false, error: "account_get_session_failed" }, 500);
  }
});
