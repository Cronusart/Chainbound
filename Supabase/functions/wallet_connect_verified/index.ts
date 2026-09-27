import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import * as bs58Module from "https://esm.sh/bs58@5.0.0";
import * as naclModule from "https://esm.sh/tweetnacl@1.0.3";
import {
  editorMockWalletAllowed,
  getProfileById,
  isApprovedBetaWallet,
  JsonMap,
  jsonResponse,
  normalizePlatform,
  PlayerProfileRow,
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

const DEFAULT_PROOF_TTL_SECONDS = 15 * 60;
const EDITOR_MOCK_SIGNATURE_PREFIX = "editor_mock_signature:";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const walletPublicKey = str(body.wallet_public_key) || str(body.wallet_address);
  const nonceId = str(body.nonce_id);
  const signedMessage = str(body.signed_message) || str(body.message);
  const signature = str(body.signature);

  if (!SOLANA_BASE58_RE.test(walletPublicKey)) {
    return jsonResponse({ success: false, error: "invalid_wallet_public_key" }, 400);
  }
  if (!nonceId || !signedMessage || !signature) {
    return jsonResponse({ success: false, error: "missing_wallet_proof" }, 400);
  }

  const supabase = supabaseAdmin();
  const authenticated = await requireAuthenticatedUser(req);
  const authenticatedProfile = await resolveAuthenticatedProfile(req, supabase, body, true);
  if (!authenticated || !authenticatedProfile) {
    return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  }
  const activePlatform = normalizePlatform(body.active_platform_build);
  const walletProvider = normalizeWalletProvider(body.wallet_provider);
  const now = new Date();
  const nowIso = now.toISOString();
  const proofTtlSeconds = positiveInt(Deno.env.get("CHAINBOUND_WALLET_PROOF_TTL_SECONDS"), DEFAULT_PROOF_TTL_SECONDS);
  const proofExpiresAt = new Date(now.getTime() + proofTtlSeconds * 1000).toISOString();

  try {
    const { data: challenge, error: challengeError } = await supabase
      .from("wallet_auth_challenges")
      .select("*")
      .eq("nonce_id", nonceId)
      .maybeSingle();

    if (challengeError) throw challengeError;
    if (!challenge) return jsonResponse({ success: false, error: "challenge_not_found" }, 404);

    const challengeStatus = str(challenge.verification_status);
    const challengeWallet = str(challenge.wallet_public_key);
    const challengeMessage = str(challenge.message);
    const requestedProfileId = authenticatedProfile.player_profile_id;
    if (str(challenge.player_profile_id) !== requestedProfileId) {
      await markChallenge(supabase, nonceId, "rejected", "challenge_profile_mismatch");
      return jsonResponse({ success: false, error: "challenge_profile_mismatch" }, 401);
    }
    const expiresAt = new Date(str(challenge.expires_at));

    if (challengeStatus !== "issued" || challenge.consumed_at) {
      return jsonResponse({ success: false, error: "challenge_already_used" }, 409);
    }
    if (!Number.isFinite(expiresAt.getTime()) || expiresAt.getTime() < now.getTime()) {
      await markChallenge(supabase, nonceId, "expired", "challenge_expired");
      return jsonResponse({ success: false, error: "challenge_expired" }, 401);
    }
    if (challengeWallet !== walletPublicKey) {
      await markChallenge(supabase, nonceId, "rejected", "challenge_wallet_mismatch");
      return jsonResponse({ success: false, error: "challenge_wallet_mismatch" }, 401);
    }
    if (challengeMessage !== signedMessage || str(challenge.message_hash) !== await sha256Hex(signedMessage)) {
      await markChallenge(supabase, nonceId, "rejected", "challenge_message_mismatch");
      return jsonResponse({ success: false, error: "challenge_message_mismatch" }, 401);
    }

    const verification = verifyEd25519Signature(walletPublicKey, signedMessage, signature);
    const verified = verification.verified;
    const devBypass = editorMockWalletAllowed() && isEditorMockSignature(signature);
    if (!verified && !devBypass) {
      await markChallenge(supabase, nonceId, "rejected", verification.error);
      return jsonResponse({ success: false, error: "invalid_proof" }, 401);
    }

    if (!await isApprovedBetaWallet(supabase, walletPublicKey)) {
      return jsonResponse({ success: false, error: "beta_not_allowlisted" }, 403);
    }

    // Proof is established before profile-link invariants are inspected. A
    // profile that already has another wallet still fails closed. An unlinked
    // profile may, however, recover the profile already owned by this proven
    // wallet; that authority handoff is performed transactionally below.
    const preflight = await inspectCurrentProfileWalletInvariant(
      supabase,
      walletPublicKey,
      requestedProfileId,
    );
    if (!preflight.ok) {
      await markChallenge(supabase, nonceId, "rejected", preflight.error);
      return jsonResponse({ success: false, error: preflight.error }, preflight.status);
    }

    const { data: consumedChallenge, error: consumeError } = await supabase
      .from("wallet_auth_challenges")
      .update({
        consumed_at: nowIso,
        verification_status: "consumed",
        verification_error: null,
        metadata_json: {
          source: "wallet_connect_verified",
          signature_verified: verified,
          dev_bypass: devBypass,
        },
      })
      .eq("nonce_id", nonceId)
      .eq("verification_status", "issued")
      .is("consumed_at", null)
      .select("nonce_id")
      .maybeSingle();
    if (consumeError) throw consumeError;
    if (!consumedChallenge) {
      return jsonResponse({ success: false, error: "challenge_already_used" }, 409);
    }

    const resolved = await resolveOrCreateProfileForWallet(
      supabase,
      authenticated.userId,
      walletPublicKey,
      walletProvider,
      requestedProfileId,
      activePlatform,
    );

    if (!resolved.ok) {
      return jsonResponse(
        { success: false, error: resolved.error ?? "wallet_profile_resolution_failed" },
        resolved.status ?? 400,
      );
    }

    await recordLinkEvent(
      supabase,
      resolved.profile.player_profile_id,
      resolved.recovered ? "wallet_profile_restored" : "wallet_connect_requested",
      walletProvider,
      resolved.recovered ? "restored" : resolved.created ? "created" : "success",
      {
        wallet_public_key: walletPublicKey,
        challenge_nonce_id: nonceId,
        signature_verified: verified,
        dev_bypass: devBypass,
        replaced_profile_id: resolved.recovered ? requestedProfileId : null,
      },
    );

    const session = await sessionResponse(supabase, resolved.profile, "wallet", activePlatform, resolved.created);
    return jsonResponse({
      ...session,
      wallet_proof_verified_at: nowIso,
      wallet_proof_expires_at: proofExpiresAt,
      wallet_profile_restored: resolved.recovered,
    });
  } catch (error) {
    console.error("[wallet_connect_verified]", error);
    return jsonResponse({ success: false, error: "wallet_connect_verified_failed" }, 500);
  }
});

async function resolveOrCreateProfileForWallet(
  supabase: ReturnType<typeof supabaseAdmin>,
  authUserId: string,
  walletPublicKey: string,
  walletProvider: string,
  requestedProfileId: string,
  activePlatform: string,
): Promise<{ ok: true; profile: PlayerProfileRow; created: boolean; recovered: boolean } | { ok: false; error: string; status: number }> {
  const invariant = await inspectCurrentProfileWalletInvariant(supabase, walletPublicKey, requestedProfileId);
  if (!invariant.ok) return invariant;

  const { data: existingWallet, error: existingError } = await supabase
    .from("profile_wallets")
    .select("player_profile_id")
    .eq("wallet_public_key", walletPublicKey)
    .eq("is_active", true)
    .is("unlinked_at", null)
    .maybeSingle();

  if (existingError) throw existingError;

  if (existingWallet?.player_profile_id) {
    const existingProfileId = str(existingWallet.player_profile_id);
    if (requestedProfileId && requestedProfileId !== existingProfileId) {
      const { data: recovery, error: recoveryError } = await supabase.rpc(
        "profile_recover_by_wallet_tx",
        {
          p_auth_user_id: authUserId,
          p_current_profile_id: requestedProfileId,
          p_wallet_public_key: walletPublicKey,
        },
      );
      if (recoveryError) {
        console.error("[wallet_connect_verified] profile recovery RPC failed", recoveryError.code);
        return { ok: false, error: "wallet_profile_recovery_failed", status: 500 };
      }

      const recoveryPayload = (recovery ?? {}) as JsonMap;
      if (recoveryPayload.success !== true) {
        return {
          ok: false,
          error: str(recoveryPayload.error) || "wallet_profile_recovery_failed",
          status: str(recoveryPayload.error) === "authenticated_profile_mismatch" ? 409 : 400,
        };
      }
      if (str(recoveryPayload.player_profile_id) !== existingProfileId) {
        return { ok: false, error: "wallet_profile_recovery_target_mismatch", status: 409 };
      }

      const recoveredProfile = await getProfileById(supabase, existingProfileId);
      if (!recoveredProfile) return { ok: false, error: "profile_not_found", status: 404 };
      await touchProfilePlatform(supabase, existingProfileId, activePlatform);
      return { ok: true, profile: recoveredProfile as PlayerProfileRow, created: false, recovered: true };
    }

    const profile = await getProfileById(supabase, existingProfileId);
    if (!profile) return { ok: false, error: "profile_not_found", status: 404 };

    await touchProfilePlatform(supabase, existingProfileId, activePlatform);
    return { ok: true, profile: profile as PlayerProfileRow, created: false, recovered: false };
  }

  const profile = await getProfileById(supabase, requestedProfileId);
  if (!profile) return { ok: false, error: "profile_not_found", status: 404 };

  const insertError = await insertWalletLink(supabase, requestedProfileId, walletProvider, walletPublicKey);
  if (insertError) {
    return {
      ok: false,
      error: insertError === "unique_violation" ? "wallet_link_race_conflict" : "wallet_link_insert_failed",
      status: insertError === "unique_violation" ? 409 : 500,
    };
  }
  await touchProfilePlatform(supabase, requestedProfileId, activePlatform);
  return { ok: true, profile: profile as PlayerProfileRow, created: false, recovered: false };
}

async function inspectCurrentProfileWalletInvariant(
  supabase: ReturnType<typeof supabaseAdmin>,
  walletPublicKey: string,
  requestedProfileId: string,
): Promise<{ ok: true } | { ok: false; error: string; status: number }> {
  const { data: profileWallet, error: profileWalletError } = await supabase
    .from("profile_wallets")
    .select("wallet_public_key")
    .eq("player_profile_id", requestedProfileId)
    .eq("is_active", true)
    .is("unlinked_at", null)
    .maybeSingle();
  if (profileWalletError) throw profileWalletError;

  if (profileWallet?.wallet_public_key && str(profileWallet.wallet_public_key) !== walletPublicKey) {
    return { ok: false, error: "profile_wallet_mismatch", status: 409 };
  }

  return { ok: true };
}

async function insertWalletLink(
  supabase: ReturnType<typeof supabaseAdmin>,
  playerProfileId: string,
  walletProvider: string,
  walletPublicKey: string,
): Promise<"unique_violation" | "insert_failed" | null> {
  const { error } = await supabase
    .from("profile_wallets")
    .insert({
      player_profile_id: playerProfileId,
      wallet_provider: walletProvider,
      wallet_public_key: walletPublicKey,
      is_active: true,
      metadata_json: { source: "wallet_connect_verified" },
    });

  if (!error) return null;
  if (error.code === "23505") return "unique_violation";
  console.error("[wallet_connect_verified] profile_wallet insert failed", error.code);
  return "insert_failed";
}

async function touchProfilePlatform(
  supabase: ReturnType<typeof supabaseAdmin>,
  playerProfileId: string,
  activePlatform: string,
): Promise<void> {
  const { error } = await supabase
    .from("player_profiles")
    .update({ last_platform_seen: activePlatform })
    .eq("player_profile_id", playerProfileId);
  if (error) throw error;
}

async function markChallenge(
  supabase: ReturnType<typeof supabaseAdmin>,
  nonceId: string,
  status: string,
  error: string,
): Promise<void> {
  await supabase
    .from("wallet_auth_challenges")
    .update({
      verification_status: status,
      verification_error: error,
    })
    .eq("nonce_id", nonceId);
}

type SignatureVerificationResult = {
  verified: boolean;
  error: string;
};

function verifyEd25519Signature(
  walletPublicKey: string,
  message: string,
  signatureText: string,
): SignatureVerificationResult {
  let publicKey: Uint8Array;
  let signature: Uint8Array;

  try {
    publicKey = bs58Decode(walletPublicKey);
  } catch {
    return { verified: false, error: "invalid_wallet_public_key_decode" };
  }

  try {
    signature = decodeSignature(signatureText);
  } catch {
    return { verified: false, error: "invalid_signature_decode" };
  }

  if (publicKey.length !== 32) {
    return { verified: false, error: `invalid_wallet_public_key_length:${publicKey.length}` };
  }
  if (signature.length !== 64) {
    return { verified: false, error: `invalid_signature_length:${signature.length}` };
  }

  const detached = naclDetachedVerify();
  if (!detached) {
    return { verified: false, error: "nacl_detached_verify_unavailable" };
  }

  try {
    const verified = detached(new TextEncoder().encode(message), signature, publicKey);
    return { verified, error: verified ? "" : "invalid_wallet_signature" };
  } catch {
    return { verified: false, error: "nacl_detached_verify_failed" };
  }
}

function decodeSignature(value: string): Uint8Array {
  try {
    const bytes = Uint8Array.from(atob(value), (c) => c.charCodeAt(0));
    if (bytes.length === 64) return bytes;
  } catch {
    // Try base58 below.
  }

  try {
    const bytes = bs58Decode(value);
    if (bytes.length === 64) return bytes;
  } catch {
    // Fall through.
  }

  throw new Error("signature decode unavailable");
}

function bs58Decode(value: string): Uint8Array {
  const moduleShape = bs58Module as unknown as {
    decode?: (input: string) => Uint8Array;
    default?: { decode?: (input: string) => Uint8Array };
  };
  const decode = moduleShape.decode ?? moduleShape.default?.decode;
  if (!decode) throw new Error("bs58 decode unavailable");
  return decode(value);
}

function naclDetachedVerify(): ((message: Uint8Array, signature: Uint8Array, publicKey: Uint8Array) => boolean) | null {
  const moduleShape = naclModule as unknown as {
    sign?: { detached?: { verify?: (message: Uint8Array, signature: Uint8Array, publicKey: Uint8Array) => boolean } };
    default?: {
      sign?: { detached?: { verify?: (message: Uint8Array, signature: Uint8Array, publicKey: Uint8Array) => boolean } };
    };
  };
  return moduleShape.sign?.detached?.verify ?? moduleShape.default?.sign?.detached?.verify ?? null;
}

function isEditorMockSignature(value: string): boolean {
  if (value.startsWith(EDITOR_MOCK_SIGNATURE_PREFIX)) return true;

  try {
    const decoded = new TextDecoder().decode(Uint8Array.from(atob(value), (c) => c.charCodeAt(0)));
    return decoded.startsWith(EDITOR_MOCK_SIGNATURE_PREFIX);
  } catch {
    return false;
  }
}

function positiveInt(value: unknown, fallback: number): number {
  const parsed = Number(str(value));
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function normalizeWalletProvider(value: unknown): string {
  const raw = str(value).toLowerCase();
  switch (raw) {
    case "phantom":
    case "solflare":
    case "seed_vault":
    case "other":
      return raw;
    default:
      return "other";
  }
}
