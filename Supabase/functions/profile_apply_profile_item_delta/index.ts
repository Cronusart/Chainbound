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

interface ItemDeltaLine {
  item_id?: string;
  quantity?: number;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const idempotencyKey = str(body.idempotency_key);
  if (!idempotencyKey) {
    return jsonResponse({ success: false, error: "idempotency_key_required" }, 400);
  }

  const grants = normalizeLines(body.grants);
  const consumes = normalizeLines(body.consumes);
  if (!grants || !consumes) {
    return jsonResponse({ success: false, error: "delta_must_be_arrays" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_apply_profile_item_delta_tx", {
      p_profile_id: profile.player_profile_id,
      p_idempotency_key: idempotencyKey,
      p_grants: grants,
      p_consumes: consumes,
    });

    if (error) {
      console.error("[profile_apply_profile_item_delta] rpc", error);
      return jsonResponse(
        { success: false, ok: false, error: error.message || "profile_item_delta_failed" },
        400,
      );
    }

    const result = (data ?? {}) as JsonMap;
    if (result.ok === false) {
      return jsonResponse({
        success: false,
        ok: false,
        already_applied: false,
        error: str(result.error) || "not_applied",
        item_id: str(result.item_id),
      }, 409);
    }

    return jsonResponse({
      success: true,
      ok: true,
      already_applied: result.already_applied === true,
      player_profile_id: profile.player_profile_id,
      error: "",
    });
  } catch (error) {
    console.error("[profile_apply_profile_item_delta]", error);
    return jsonResponse({ success: false, ok: false, error: "profile_apply_profile_item_delta_failed" }, 500);
  }
});

function normalizeLines(value: unknown): ItemDeltaLine[] | null {
  if (value == null) return [];
  if (!Array.isArray(value)) return null;

  const lines: ItemDeltaLine[] = [];
  for (const entry of value) {
    if (!entry || typeof entry !== "object") return null;
    const row = entry as JsonMap;
    const itemId = str(row.item_id);
    const quantity = typeof row.quantity === "number" ? Math.trunc(row.quantity) : Number(str(row.quantity));
    if (!itemId || !Number.isFinite(quantity) || quantity <= 0) return null;
    lines.push({ item_id: itemId, quantity });
  }
  return lines;
}
