import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  recordLinkEvent,
  requireAuthenticatedUser,
  requirePost,
  resolveProfileByAuthUser,
  sessionResponse,
  sha256Hex,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const authenticated = await requireAuthenticatedUser(req);
  if (!authenticated) return jsonResponse({ success: false, error: "invalid_or_missing_jwt" }, 401);
  if (!authenticated.isAnonymous) return jsonResponse({ success: false, error: "anonymous_jwt_required" }, 403);

  const installId = str(body.guest_install_id);
  const activePlatform = normalizePlatform(body.active_platform_build);
  const installHash = installId ? await sha256Hex(`install:${installId}`) : "";
  const nowIso = new Date().toISOString();
  const supabase = supabaseAdmin();

  try {
    const existingProfile = await resolveProfileByAuthUser(supabase, authenticated.userId);
    if (existingProfile) {
      const { data: profile, error: profileError } = await supabase
        .from("player_profiles")
        .update({ last_platform_seen: activePlatform, updated_at: nowIso })
        .eq("player_profile_id", existingProfile.player_profile_id)
        .select("*")
        .single();

      if (profileError) throw profileError;
      return jsonResponse(await sessionResponse(supabase, profile, "guest", activePlatform, false));
    }

    const { data: profile, error: profileError } = await supabase
      .from("player_profiles")
      .insert({
        platform_origin: activePlatform,
        last_platform_seen: activePlatform,
      })
      .select("*")
      .single();

    if (profileError) throw profileError;

    const { error: identityError } = await supabase.from("profile_identities").insert({
      player_profile_id: profile.player_profile_id,
      identity_type: "guest",
      provider: "supabase_anonymous",
      provider_subject: authenticated.userId,
      auth_user_id: authenticated.userId,
      is_primary: true,
      metadata_json: {
        client_version: str(body.client_version),
        install_id_hash: installHash || null,
      },
    });

    if (identityError) {
      // A second launch may win the auth-subject uniqueness race after both
      // requests observed no identity. Resolve the winner instead of making
      // the caller retry a non-idempotent bootstrap.
      if (identityError.code === "23505") {
        const winner = await resolveProfileByAuthUser(supabase, authenticated.userId);
        if (winner) {
          await supabase
            .from("player_profiles")
            .delete()
            .eq("player_profile_id", profile.player_profile_id);
          return jsonResponse(await sessionResponse(supabase, winner, "guest", activePlatform, false));
        }
      }
      throw identityError;
    }

    await recordLinkEvent(supabase, profile.player_profile_id, "guest_started", "supabase_anonymous", "success");
    return jsonResponse(await sessionResponse(supabase, profile, "guest", activePlatform, true));
  } catch (error) {
    console.error("[account_start_guest]", error);
    return jsonResponse({ success: false, error: "account_start_guest_failed" }, 500);
  }
});
