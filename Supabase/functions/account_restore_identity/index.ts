import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  identityVerificationAllowed,
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  recordLinkEvent,
  requirePost,
  sessionResponse,
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

  if (!identityVerificationAllowed(provider, identityType)) {
    return jsonResponse({
      success: false,
      error: "invalid_proof",
      message: "Server-side identity verification is not wired for this provider.",
    }, 401);
  }

  const supabase = supabaseAdmin();
  try {
    const { data: existingIdentity, error: existingError } = await supabase
      .from("profile_identities")
      .select("player_profile_id")
      .eq("identity_type", identityType)
      .eq("provider", provider)
      .eq("provider_subject", providerSubject)
      .is("revoked_at", null)
      .maybeSingle();

    if (existingError) throw existingError;

    if (existingIdentity?.player_profile_id) {
      const { data: profile, error: profileError } = await supabase
        .from("player_profiles")
        .update({ last_platform_seen: activePlatform, updated_at: new Date().toISOString() })
        .eq("player_profile_id", existingIdentity.player_profile_id)
        .select("*")
        .single();

      if (profileError) throw profileError;
      await recordLinkEvent(supabase, profile.player_profile_id, "identity_restore_requested", provider, "success");
      return jsonResponse(await sessionResponse(supabase, profile, identityType, activePlatform, false));
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
      identity_type: identityType,
      provider,
      provider_subject: providerSubject,
      auth_user_id: str(body.auth_user_id) || null,
      is_primary: true,
    });

    if (identityError) throw identityError;
    await recordLinkEvent(supabase, profile.player_profile_id, "identity_restore_requested", provider, "created");

    return jsonResponse(await sessionResponse(supabase, profile, identityType, activePlatform, true));
  } catch (error) {
    console.error("[account_restore_identity]", error);
    return jsonResponse({ success: false, error: "account_restore_identity_failed" }, 500);
  }
});
