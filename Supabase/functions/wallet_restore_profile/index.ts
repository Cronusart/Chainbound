import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import * as bs58Module from "https://esm.sh/bs58@5.0.0";
import * as naclModule from "https://esm.sh/tweetnacl@1.0.3";
import {
  editorMockWalletAllowed,
  isApprovedBetaWallet,
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  recordLinkEvent,
  requireAuthenticatedUser,
  requirePost,
  resolveAuthenticatedProfile,
  sessionResponse,
  sha256Hex,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

const EDITOR_MOCK_SIGNATURE_PREFIX = "editor_mock_signature:";
const DEFAULT_PROOF_TTL_SECONDS = 15 * 60;

// Explicit recovery path. A proven wallet may move the caller's anonymous-auth
// identity to the existing wallet profile, but never copies or merges the guest
// profile's game data into it.
serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const walletPublicKey = str(body.wallet_public_key);
  const nonceId = str(body.nonce_id);
  const message = str(body.signed_message);
  const signature = str(body.signature);
  if (!SOLANA_BASE58_RE.test(walletPublicKey) || !nonceId || !message || !signature)
    return jsonResponse({ success: false, error: "missing_wallet_proof" }, 400);

  const supabase = supabaseAdmin();
  const authenticated = await requireAuthenticatedUser(req);
  const current = await resolveAuthenticatedProfile(req, supabase, body, true);
  if (!authenticated || !current) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const { data: challenge, error: challengeError } = await supabase
      .from("wallet_auth_challenges")
      .select("*")
      .eq("nonce_id", nonceId)
      .maybeSingle();
    if (challengeError) throw challengeError;
    if (!challenge || str(challenge.verification_status) !== "issued" || challenge.consumed_at)
      return jsonResponse({ success: false, error: "challenge_not_available" }, 409);
    if (str(challenge.player_profile_id) !== current.player_profile_id ||
        str(challenge.wallet_public_key) !== walletPublicKey ||
        str(challenge.message) !== message ||
        str(challenge.message_hash) !== await sha256Hex(message) ||
        new Date(str(challenge.expires_at)).getTime() < Date.now())
      return jsonResponse({ success: false, error: "challenge_mismatch_or_expired" }, 401);

    const verified = verifySignature(walletPublicKey, message, signature);
    const devBypass = editorMockWalletAllowed() && isEditorMockSignature(signature);
    if (!verified && !devBypass) return jsonResponse({ success: false, error: "invalid_proof" }, 401);

    if (!await isApprovedBetaWallet(supabase, walletPublicKey)) {
      return jsonResponse({ success: false, error: "beta_not_allowlisted" }, 403);
    }

    const now = new Date();
    const nowIso = now.toISOString();
    const proofTtlSeconds = positiveInt(Deno.env.get("CHAINBOUND_WALLET_PROOF_TTL_SECONDS"), DEFAULT_PROOF_TTL_SECONDS);
    const proofExpiresAt = new Date(now.getTime() + proofTtlSeconds * 1000).toISOString();
    const { data: consumedChallenge, error: consumeError } = await supabase.from("wallet_auth_challenges").update({
      consumed_at: nowIso, verification_status: "consumed", verification_error: null,
      metadata_json: { source: "wallet_restore_profile", signature_verified: verified, dev_bypass: devBypass },
    })
      .eq("nonce_id", nonceId)
      .eq("verification_status", "issued")
      .is("consumed_at", null)
      .select("nonce_id")
      .maybeSingle();
    if (consumeError) throw consumeError;
    if (!consumedChallenge) return jsonResponse({ success: false, error: "challenge_already_used" }, 409);

    const { data: wallet, error: walletError } = await supabase
      .from("profile_wallets")
      .select("player_profile_id")
      .eq("wallet_public_key", walletPublicKey)
      .eq("is_active", true)
      .is("unlinked_at", null)
      .maybeSingle();
    if (walletError) throw walletError;
    const targetProfileId = str(wallet?.player_profile_id);
    if (!targetProfileId) return jsonResponse({ success: false, error: "wallet_profile_not_found" }, 404);

    const { data: recovery, error: recoveryError } = await supabase.rpc(
      "profile_recover_by_wallet_tx",
      {
        p_auth_user_id: authenticated.userId,
        p_current_profile_id: current.player_profile_id,
        p_wallet_public_key: walletPublicKey,
      },
    );
    if (recoveryError) throw recoveryError;
    const recoveryPayload = (recovery ?? {}) as JsonMap;
    if (recoveryPayload.success !== true) {
      return jsonResponse(
        { success: false, error: str(recoveryPayload.error) || "wallet_profile_recovery_failed" },
        str(recoveryPayload.error) === "authenticated_profile_mismatch" ? 409 : 400,
      );
    }
    if (str(recoveryPayload.player_profile_id) !== targetProfileId) {
      return jsonResponse({ success: false, error: "wallet_profile_recovery_target_mismatch" }, 409);
    }

    const { data: target, error: targetError } = await supabase
      .from("player_profiles")
      .update({ last_platform_seen: normalizePlatform(body.active_platform_build) })
      .eq("player_profile_id", targetProfileId)
      .select("*")
      .single();
    if (targetError) throw targetError;

    await recordLinkEvent(supabase, targetProfileId, "wallet_profile_restored", str(challenge.wallet_provider), "success", {
      replaced_profile_id: current.player_profile_id, wallet_public_key: walletPublicKey,
    });
    return jsonResponse({
      ...await sessionResponse(supabase, target, "wallet", normalizePlatform(body.active_platform_build)),
      wallet_proof_verified_at: nowIso,
      wallet_proof_expires_at: proofExpiresAt,
      wallet_profile_restored: recoveryPayload.recovered === true,
    });
  } catch (error) {
    console.error("[wallet_restore_profile]", error);
    return jsonResponse({ success: false, error: "wallet_restore_profile_failed" }, 500);
  }
});

function verifySignature(wallet: string, message: string, signatureText: string): boolean {
  try {
    const moduleShape = bs58Module as unknown as { decode?: (value: string) => Uint8Array; default?: { decode?: (value: string) => Uint8Array } };
    const decode = moduleShape.decode ?? moduleShape.default?.decode;
    const publicKey = decode?.(wallet);
    let signature: Uint8Array;
    try { signature = Uint8Array.from(atob(signatureText), (character) => character.charCodeAt(0)); }
    catch { signature = decode?.(signatureText) ?? new Uint8Array(); }
    const naclShape = naclModule as unknown as { sign?: { detached?: { verify?: (m: Uint8Array, s: Uint8Array, p: Uint8Array) => boolean } }; default?: { sign?: { detached?: { verify?: (m: Uint8Array, s: Uint8Array, p: Uint8Array) => boolean } } } };
    const verify = naclShape.sign?.detached?.verify ?? naclShape.default?.sign?.detached?.verify;
    return !!publicKey && publicKey.length === 32 && signature.length === 64 && !!verify && verify(new TextEncoder().encode(message), signature, publicKey);
  } catch { return false; }
}

function isEditorMockSignature(value: string): boolean {
  if (value.startsWith(EDITOR_MOCK_SIGNATURE_PREFIX)) return true;
  try { return new TextDecoder().decode(Uint8Array.from(atob(value), (character) => character.charCodeAt(0))).startsWith(EDITOR_MOCK_SIGNATURE_PREFIX); }
  catch { return false; }
}

function positiveInt(value: unknown, fallback: number): number {
  const parsed = Number(str(value));
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
