import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  bool,
  identityVerificationAllowed,
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  recordLinkEvent,
  requireAuthenticatedUser,
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

  const identityType = str(body.identity_type);
  const provider = str(body.provider);
  const providerSubject = str(body.provider_subject);
  const activePlatform = normalizePlatform(body.active_platform_build);

  if (!identityType || !provider || !providerSubject) {
    return jsonResponse({ success: false, error: "missing_identity" }, 400);
  }

  if (!bool(body.explicit_consent)) {
    return jsonResponse({ success: false, error: "explicit_consent_required" }, 400);
  }

  if (!identityVerificationAllowed(provider, identityType)) {
    return jsonResponse({
      success: false,
      error: "invalid_proof",
      message: "Server-side identity verification is not wired for this provider.",
    }, 401);
  }

  const supabase = supabaseAdmin();
  try {
    const authenticated = await requireAuthenticatedUser(req);
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!authenticated || !profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
    const playerProfileId = profile.player_profile_id;

    const { data: existingIdentity, error: existingError } = await supabase
      .from("profile_identities")
      .select("player_profile_id")
      .eq("identity_type", identityType)
      .eq("provider", provider)
      .eq("provider_subject", providerSubject)
      .is("revoked_at", null)
      .maybeSingle();

    if (existingError) throw existingError;

    if (existingIdentity?.player_profile_id === playerProfileId) {
      return jsonResponse({
        success: true,
        player_profile_id: playerProfileId,
        account_link_state: "linked",
        message: "Identity is already linked to this profile.",
      });
    }

    if (existingIdentity?.player_profile_id) {
      await recordLinkEvent(supabase, playerProfileId, "identity_link_requested", provider, "conflict", {
        existing_player_profile_id: existingIdentity.player_profile_id,
      });
      return jsonResponse({ success: false, error: "identity_linked_to_different_profile" }, 409);
    }

    const { error: insertError } = await supabase.from("profile_identities").insert({
      player_profile_id: playerProfileId,
      identity_type: identityType,
      provider,
      provider_subject: providerSubject,
      // The JWT subject is the sole auth identity authority. A body value may
      // only survive the resolver's consistency check; it never becomes a
      // profile-binding credential.
      auth_user_id: authenticated.userId,
      is_primary: true,
    });

    if (insertError) throw insertError;

    await supabase
      .from("player_profiles")
      .update({ last_platform_seen: activePlatform })
      .eq("player_profile_id", playerProfileId);

    await recordLinkEvent(supabase, playerProfileId, "identity_link_requested", provider, "success");
    return jsonResponse({
      success: true,
      player_profile_id: playerProfileId,
      account_link_state: "linked",
      message: "Identity linked.",
    });
  } catch (error) {
    console.error("[account_link_identity]", error);
    return jsonResponse({ success: false, error: "account_link_identity_failed" }, 500);
  }
});
