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

const encoder = new TextEncoder();
const ticketTtlSeconds = 600;

export async function handleHub(
  req: Request,
  op: "config" | "unlock" | "link" | "card" | "verify",
): Promise<Response> {
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
  if (op === "verify") {
    return verifyTicket(supabase, str(body.ticket));
  }
  if (op === "link") {
    const authenticated = await requireAuthenticatedUser(req);
    if (!authenticated) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    const linked = await callRpc(supabase, "hub_link_identity_service", {
      p_profile_id: profile.player_profile_id,
      p_supabase_user_id: authenticated.userId,
      p_ugs_player_id: str(body.ugs_player_id),
    });
    if (linked.error) return linked.error;
    const payload = linked.data;
    if (payload.error === "identity_in_use") return jsonResponse(payload, 409);
    if (payload.success === true) {
      try {
        payload.ticket = await signPresenceTicket(str(body.ugs_player_id), profile.player_profile_id);
      } catch (error) {
        console.error("[Hub] ticket", error instanceof Error ? error.message : "unavailable");
        return jsonResponse({ success: false, error: "ticket_unavailable" }, 500);
      }
    }
    return jsonResponse(payload);
  }

  return rpc(supabase, "hub_presence_card_service", {
    p_ugs_player_id: str(body.ugs_player_id),
  });
}

async function verifyTicket(supabase: SupabaseClient, ticket: string): Promise<Response> {
  const checked = await readPresenceTicket(ticket);
  if (!checked) return jsonResponse({ success: false, error: "ticket_rejected" }, 401);
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(checked.profile)) {
    return jsonResponse({ success: false, error: "ticket_rejected" }, 401);
  }
  const { data, error } = await supabase
    .from("hub_identities")
    .select("ugs_player_id")
    .eq("ugs_player_id", checked.ugs)
    .eq("player_profile_id", checked.profile)
    .maybeSingle();
  if (error || !data) return jsonResponse({ success: false, error: "ticket_rejected" }, 401);
  return jsonResponse({ success: true, error: "", ugs_player_id: checked.ugs });
}

async function signPresenceTicket(ugs: string, profile: string): Promise<string> {
  const exp = Math.floor(Date.now() / 1000) + ticketTtlSeconds;
  const payload = `${ugs}|${exp}|${profile}`;
  const key = await ticketKey(["sign"]);
  const signature = new Uint8Array(await crypto.subtle.sign("HMAC", key, encoder.encode(payload)));
  return `${b64url(encoder.encode(payload))}.${b64url(signature)}`;
}

async function readPresenceTicket(ticket: string): Promise<{ ugs: string; profile: string } | null> {
  const parts = ticket.split(".");
  if (parts.length !== 2 || !parts[0] || !parts[1]) return null;
  let payload = "";
  try {
    payload = new TextDecoder().decode(b64urlDecode(parts[0]));
  } catch {
    return null;
  }
  const fields = payload.split("|");
  if (fields.length !== 3) return null;
  const [ugs, expText, profile] = fields;
  const exp = Number(expText);
  if (!ugs || !profile || !Number.isFinite(exp) || exp < Math.floor(Date.now() / 1000)) return null;
  try {
    const key = await ticketKey(["verify"]);
    const ok = await crypto.subtle.verify("HMAC", key, b64urlDecode(parts[1]), encoder.encode(payload));
    return ok ? { ugs, profile } : null;
  } catch {
    return null;
  }
}

async function ticketKey(usages: KeyUsage[]): Promise<CryptoKey> {
  const secret = Deno.env.get("HUB_TICKET_SECRET") ?? "";
  if (!secret) throw new Error("ticket_secret_missing");
  return crypto.subtle.importKey(
    "raw",
    encoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    usages,
  );
}

function b64url(bytes: Uint8Array): string {
  let binary = "";
  for (const value of bytes) binary += String.fromCharCode(value);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
}

function b64urlDecode(text: string): Uint8Array {
  const padded = text + "=".repeat((4 - (text.length % 4)) % 4);
  const binary = atob(padded.replaceAll("-", "+").replaceAll("_", "/"));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

async function rpc(supabase: SupabaseClient, fn: string, args: JsonMap): Promise<Response> {
  const result = await callRpc(supabase, fn, args);
  if (result.error) return result.error;
  if (result.data.error === "identity_in_use") return jsonResponse(result.data, 409);
  return jsonResponse(result.data);
}

async function callRpc(
  supabase: SupabaseClient,
  fn: string,
  args: JsonMap,
): Promise<{ data: JsonMap; error: Response | null }> {
  const { data, error } = await supabase.rpc(fn, args);
  if (error) {
    console.error("[Hub]", fn, error.message);
    return { data: {}, error: jsonResponse({ success: false, error: "hub_failed" }, 500) };
  }
  if (!data || typeof data !== "object") {
    return { data: {}, error: jsonResponse({ success: false, error: "empty_rpc" }) };
  }
  return { data: data as JsonMap, error: null };
}
