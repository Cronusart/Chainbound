import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

const PROGRESSION_MOVES_GRANTED = "pve.progression_moves_granted";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const chainlingId = str(body.chainling_id);
  const metricKey = str(body.metric_key);
  const source = str(body.source) || "unknown";
  const moveId = str(body.move_id);
  const amount = nonNegativeInteger(body.amount ?? 1);

  if (!chainlingId) {
    return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  }

  if (metricKey !== PROGRESSION_MOVES_GRANTED) {
    return jsonResponse({ success: false, error: "unsupported_metric_key" }, 400);
  }

  if (amount <= 0) {
    return jsonResponse({ success: false, error: "invalid_metric_amount" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const idempotencyKey = str(body.idempotency_key) ||
      `metric:${profile.player_profile_id}:${chainlingId}:${metricKey}:${source}:${moveId}:${Date.now()}`;

    const { data, error } = await supabase.rpc("profile_increment_chainling_metric_tx", {
      p_profile_id: profile.player_profile_id,
      p_chainling_id: chainlingId,
      p_metric_key: metricKey,
      p_amount: amount,
      p_idempotency_key: idempotencyKey,
    });

    if (error) {
      return jsonResponse({
        success: false,
        error: normalizeRpcError(error.message),
      }, 400);
    }

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      result: data ?? {},
    });
  } catch (error) {
    console.error("[profile_increment_chainling_metric]", error);
    return jsonResponse({ success: false, error: "profile_increment_chainling_metric_failed" }, 500);
  }
});

function nonNegativeInteger(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value)) return Math.max(0, Math.trunc(value));
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return Math.max(0, Math.trunc(parsed));
  }
  return 0;
}

function normalizeRpcError(message: string): string {
  const normalized = (message ?? "").trim();
  if (!normalized) return "profile_increment_chainling_metric_failed";
  if (normalized.includes("chainling_not_found_or_not_owned")) return "chainling_not_found_or_not_owned";
  if (normalized.includes("unsupported_metric_key")) return "unsupported_metric_key";
  if (normalized.includes("invalid_metric_amount")) return "invalid_metric_amount";
  return normalized;
}
