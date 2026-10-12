import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { JsonMap, jsonResponse, readJsonBody, requirePost, str, supabaseAdmin } from "../_shared/account_helpers.ts";
import { advanceBattleReward } from "../_shared/battle_reward.ts";

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  if (!isAuthorized(req)) return jsonResponse({ success: false, error: "unauthorized" }, 401);
  const body = await readJsonBody<JsonMap>(req);
  if (!body || Object.keys(body).length !== 1 || !UUID_RE.test(str(body.battle_id))) {
    return jsonResponse({ success: false, error: "invalid_request" }, 400);
  }
  const battleId = str(body.battle_id).toLowerCase();
  try {
    const result = await advanceBattleReward(supabaseAdmin(), battleId);
    return jsonResponse({ success: true, ...result });
  } catch (error) {
    const code = error instanceof Error ? error.message : "battle_reward_failed";
    const status = code === "battle_not_found" ? 404 : code.includes("invalid_") ? 400 : 503;
    console.error("[battle_reward_settle]", { battleId, code });
    return jsonResponse({ success: false, error: code }, status);
  }
});

function isAuthorized(req: Request): boolean {
  const serviceRole = str(Deno.env.get("SUPABASE_SERVICE_ROLE_KEY"));
  const privateSecret = str(Deno.env.get("BATTLE_REWARD_SERVICE_SECRET"));
  const apikey = str(req.headers.get("apikey"));
  const suppliedSecret = str(req.headers.get("x-battle-reward-secret"));
  return !!serviceRole && !!privateSecret && constantTimeEqual(apikey, serviceRole) &&
    constantTimeEqual(suppliedSecret, privateSecret);
}

function constantTimeEqual(left: string, right: string): boolean {
  const encoder = new TextEncoder();
  const a = encoder.encode(left);
  const b = encoder.encode(right);
  let mismatch = a.length ^ b.length;
  const length = Math.max(a.length, b.length);
  for (let i = 0; i < length; i++) {
    mismatch |= (a[i % Math.max(1, a.length)] ?? 0) ^ (b[i % Math.max(1, b.length)] ?? 0);
  }
  return mismatch === 0;
}
