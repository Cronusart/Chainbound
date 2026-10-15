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

interface AtomicLoadoutResult {
  success: boolean;
  error?: string;
  chainling_id?: string;
  slot?: number;
  move_item_id?: string;
  max_slots?: number;
  max_passives?: number;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const chainlingId = str(body.chainling_id);
  const moveItemId = str(body.move_item_id);
  const slot = Number(body.slot);
  if (!chainlingId) return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  if (!Number.isInteger(slot) || slot < 0 || slot >= 16) {
    return jsonResponse({ success: false, error: "invalid_loadout_slot" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    // This is deliberately the only loadout write. The RPC locks the Chainling
    // and the move row, so last-slot, seal, escrow, target-slot, and cross-
    // Chainling checks cannot race across client requests.
    const { data, error } = await supabase.rpc("profile_set_loadout_atomic", {
      p_profile_id: profile.player_profile_id,
      p_chainling_id: chainlingId,
      p_slot: slot,
      p_move_item_id: moveItemId || null,
    });
    if (error) throw error;

    const result = (data ?? { success: false, error: "profile_set_loadout_failed" }) as AtomicLoadoutResult;
    const status = result.success ? 200 : statusFor(result.error);
    return jsonResponse({
      ...result,
      player_profile_id: profile.player_profile_id,
      chainling_id: result.chainling_id ?? chainlingId,
      slot: result.slot ?? slot,
      move_item_id: result.move_item_id ?? moveItemId,
    }, status);
  } catch (error) {
    console.error("[profile_set_loadout]", error);
    return jsonResponse({ success: false, error: "profile_set_loadout_failed" }, 500);
  }
});

function statusFor(error?: string): number {
  switch (error) {
    case "chainling_not_found_or_not_owned":
    case "move_item_not_found":
      return 404;
    case "invalid_loadout_slot":
    case "slot_exceeds_chainling_capacity":
    case "passive_cap_exceeded":
      return 400;
    default:
      return 409;
  }
}
