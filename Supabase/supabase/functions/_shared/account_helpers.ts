import { createClient, SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

export const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
export const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
export const SOLANA_BASE58_RE = /^[1-9A-HJ-NP-Za-km-z]{32,44}$/;

export type JsonMap = Record<string, unknown>;

export interface PlayerProfileRow {
  player_profile_id: string;
  display_name?: string | null;
  has_started_game?: boolean | null;
  intro_complete?: boolean | null;
  platform_origin?: string | null;
  last_platform_seen?: string | null;
  data_residency?: string | null;
  progression_state?: JsonMap | null;
}

export interface AuthenticatedUserContext {
  userId: string;
  isAnonymous: boolean;
}

export function supabaseAdmin(): SupabaseClient {
  return createClient(SUPABASE_URL, SERVICE_ROLE_KEY);
}

export function corsResponse(): Response {
  return new Response("ok", {
    headers: {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type, Authorization, apikey",
    },
  });
}

export function jsonResponse(body: JsonMap, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Access-Control-Allow-Origin": "*",
    },
  });
}

export async function readJsonBody<T>(req: Request): Promise<T | null> {
  try {
    return await req.json() as T;
  } catch {
    return null;
  }
}

export function str(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

export function bool(value: unknown): boolean {
  return value === true || value === "true";
}

export function normalizePlatform(value: unknown): string {
  const raw = str(value).toLowerCase();
  switch (raw) {
    case "googleplay":
    case "google_play":
      return "google_play";
    case "iosappstore":
    case "ios_app_store":
      return "ios_app_store";
    case "solanadappstore":
    case "solana_dapp_store":
      return "solana_dapp_store";
    case "editor":
      return "editor";
    default:
      return "unknown";
  }
}

export async function sha256Hex(value: string): Promise<string> {
  const bytes = new TextEncoder().encode(value);
  const hash = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(hash)).map((b) => b.toString(16).padStart(2, "0")).join("");
}

export async function recordLinkEvent(
  supabase: SupabaseClient,
  playerProfileId: string | null,
  eventType: string,
  provider: string | null,
  result: string,
  metadata: JsonMap = {},
): Promise<void> {
  const { error } = await supabase.from("account_link_events").insert({
    player_profile_id: playerProfileId,
    event_type: eventType,
    actor_type: "player",
    provider,
    result,
    metadata_json: metadata,
  });

  if (error) {
    console.warn("[account_helpers] Failed to record account_link_event:", error.message);
  }
}

export async function getProfileById(
  supabase: SupabaseClient,
  playerProfileId: string,
): Promise<PlayerProfileRow | null> {
  if (!playerProfileId) return null;

  const { data, error } = await supabase
    .from("player_profiles")
    .select("*")
    .eq("player_profile_id", playerProfileId)
    .maybeSingle();

  if (error) throw new Error(`Failed to resolve player profile: ${error.message}`);
  return data as PlayerProfileRow | null;
}

export async function isApprovedBetaWallet(
  supabase: SupabaseClient,
  walletPublicKey: string,
): Promise<boolean> {
  if (!walletPublicKey) return false;

  const { data, error } = await supabase
    .from("beta_allowlist")
    .select("status, revoked_at")
    .eq("wallet_public_key", walletPublicKey)
    .maybeSingle();

  if (error) throw new Error(`Failed to check beta allowlist: ${error.message}`);
  return data?.status === "approved" && !data?.revoked_at;
}

export async function resolveProfileByWallet(
  supabase: SupabaseClient,
  walletPublicKey: string,
): Promise<PlayerProfileRow | null> {
  if (!walletPublicKey) return null;

  const { data: walletLink, error: walletError } = await supabase
    .from("profile_wallets")
    .select("player_profile_id")
    .eq("wallet_public_key", walletPublicKey)
    .eq("is_active", true)
    .is("unlinked_at", null)
    .maybeSingle();

  if (walletError) throw new Error(`Failed to resolve wallet link: ${walletError.message}`);
  if (walletLink?.player_profile_id) {
    return await getProfileById(supabase, walletLink.player_profile_id as string);
  }
  return null;
}

export async function resolveProfileByAuthUser(
  supabase: SupabaseClient,
  authUserId: string,
): Promise<PlayerProfileRow | null> {
  if (!authUserId) return null;

  const { data: identity, error } = await supabase
    .from("profile_identities")
    .select("player_profile_id")
    .eq("auth_user_id", authUserId)
    .is("revoked_at", null)
    .maybeSingle();

  if (error) throw new Error(`Failed to resolve auth identity: ${error.message}`);
  if (!identity?.player_profile_id) return null;
  return await getProfileById(supabase, identity.player_profile_id as string);
}

export async function getPrimaryIdentity(
  supabase: SupabaseClient,
  playerProfileId: string,
): Promise<JsonMap | null> {
  const { data, error } = await supabase
    .from("profile_identities")
    .select("*")
    .eq("player_profile_id", playerProfileId)
    .is("revoked_at", null)
    .order("is_primary", { ascending: false })
    .order("linked_at", { ascending: true })
    .limit(1)
    .maybeSingle();

  if (error) throw new Error(`Failed to resolve identity: ${error.message}`);
  return data as JsonMap | null;
}

export async function getActiveWallet(
  supabase: SupabaseClient,
  playerProfileId: string,
): Promise<JsonMap | null> {
  const { data, error } = await supabase
    .from("profile_wallets")
    .select("*")
    .eq("player_profile_id", playerProfileId)
    .eq("is_active", true)
    .is("unlinked_at", null)
    .order("linked_at", { ascending: false })
    .limit(1)
    .maybeSingle();

  if (error) throw new Error(`Failed to resolve wallet: ${error.message}`);
  return data as JsonMap | null;
}

export async function sessionResponse(
  supabase: SupabaseClient,
  profile: PlayerProfileRow,
  lastSignInMethod = "none",
  activePlatformBuild = "unknown",
  created = false,
): Promise<JsonMap> {
  const identity = await getPrimaryIdentity(supabase, profile.player_profile_id);
  const wallet = await getActiveWallet(supabase, profile.player_profile_id);
  const progressionState = (profile.progression_state ?? {}) as JsonMap;

  return {
    success: true,
    player_profile_id: profile.player_profile_id,
    auth_user_id: str(identity?.auth_user_id),
    display_name: profile.display_name ?? "",
    platform_origin: profile.platform_origin ?? "unknown",
    last_platform_seen: profile.last_platform_seen ?? activePlatformBuild,
    data_residency: profile.data_residency ?? "",
    has_started_game: profile.has_started_game === true,
    intro_cinematic_complete: bool(progressionState.intro_cinematic_complete),
    intro_complete: profile.intro_complete === true,
    created,
    is_guest: str(identity?.identity_type) === "guest",
    is_recoverable: str(identity?.identity_type) !== "" && str(identity?.identity_type) !== "guest",
    identity_type: str(identity?.identity_type),
    identity_provider: str(identity?.provider),
    identity_subject: str(identity?.provider_subject),
    platform_identity_type: str(identity?.identity_type),
    platform_identity_provider: str(identity?.provider),
    platform_identity_subject: str(identity?.provider_subject),
    wallet_provider: str(wallet?.wallet_provider),
    wallet_public_key: str(wallet?.wallet_public_key),
    account_link_state: wallet ? "linked" : "unlinked",
    last_sign_in_method: lastSignInMethod,
    active_platform_build: activePlatformBuild,
  };
}

/**
 * Validates the caller's Supabase JWT with the service-role client. Edge
 * functions use this only to establish the subject; all database work remains
 * server-side. No client-supplied profile, auth, or wallet value grants access.
 */
export async function requireAuthenticatedUser(req: Request): Promise<AuthenticatedUserContext | null> {
  const authorization = req.headers.get("Authorization") ?? "";
  const match = authorization.match(/^Bearer\s+(.+)$/i);
  if (!match || !match[1]) return null;

  const { data, error } = await supabaseAdmin().auth.getUser(match[1].trim());
  if (error || !data.user?.id) return null;

  return {
    userId: data.user.id,
    isAnonymous: data.user.is_anonymous === true,
  };
}

/**
 * Resolves exactly the profile owned by the authenticated JWT subject. Body
 * identifiers are optional consistency checks to catch stale or spoofed client
 * state, never alternate lookup keys.
 */
export async function resolveAuthenticatedProfile(
  req: Request,
  supabase: SupabaseClient,
  body: JsonMap,
  allowWalletProfileConflict = false,
): Promise<PlayerProfileRow | null> {
  const authenticated = await requireAuthenticatedUser(req);
  if (!authenticated) return null;

  const profile = await resolveProfileByAuthUser(supabase, authenticated.userId);
  if (!profile) return null;

  const suppliedProfileId = str(body.player_profile_id);
  const suppliedAuthUserId = str(body.auth_user_id);
  if ((suppliedProfileId && suppliedProfileId !== profile.player_profile_id) ||
      (suppliedAuthUserId && suppliedAuthUserId !== authenticated.userId)) {
    return null;
  }

  const suppliedWallet = str(body.wallet_public_key) || str(body.wallet_address);
  if (suppliedWallet) {
    const walletProfile = await resolveProfileByWallet(supabase, suppliedWallet);
    if (!allowWalletProfileConflict && walletProfile && walletProfile.player_profile_id !== profile.player_profile_id) return null;
  }

  return profile;
}

export async function resolveProfileFromBody(
  supabase: SupabaseClient,
  body: JsonMap,
): Promise<PlayerProfileRow | null> {
  const playerProfileId = str(body.player_profile_id);
  const walletPublicKey = str(body.wallet_public_key) || str(body.wallet_address);
  const authUserId = str(body.auth_user_id);

  return await getProfileById(supabase, playerProfileId) ??
    await resolveProfileByAuthUser(supabase, authUserId) ??
    await resolveProfileByWallet(supabase, walletPublicKey);
}

export function identityVerificationAllowed(provider: string, identityType: string): boolean {
  if (provider === "editor_mock" || provider === "local_install" || identityType === "guest") {
    return true;
  }

  return Deno.env.get("ALLOW_UNVERIFIED_IDENTITY_LINKS") === "true";
}

export function walletVerificationAllowed(): boolean {
  return Deno.env.get("ALLOW_UNVERIFIED_WALLET_LINKS") === "true";
}

export function editorMockWalletAllowed(): boolean {
  return Deno.env.get("ALLOW_EDITOR_MOCK_WALLET_LINKS") === "true";
}

export function requirePost(req: Request): Response | null {
  if (req.method === "OPTIONS") return corsResponse();
  if (req.method !== "POST") return jsonResponse({ error: "Method not allowed" }, 405);
  return null;
}
