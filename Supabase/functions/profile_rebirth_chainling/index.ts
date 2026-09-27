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
import {
  PROGRESSION_CONFIG,
  getRebirthRamCost,
} from "../_shared/vending_config.ts";

const REBIRTH_RAM_COSTS = PROGRESSION_CONFIG.rebirthRamCosts;
const STAT_INDEX: Record<string, number> = {
  max_hp: 0,
  melee_atk: 1,
  melee_def: 2,
  ranged_atk: 3,
  ranged_def: 4,
  speed: 5,
};

interface ChainlingProgressionRow {
  rebirth_count: number | null;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const chainlingId = str(body.chainling_id);
  const stat = str(body.stat).toLowerCase();
  const statIndex = STAT_INDEX[stat];

  if (!chainlingId) {
    return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  }

  if (!Number.isInteger(statIndex)) {
    return jsonResponse({ success: false, error: "invalid_rebirth_stat" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data: chainling, error: chainlingError } = await supabase
      .from("chainlings")
      .select("rebirth_count")
      .eq("id", chainlingId)
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    if (chainlingError) throw chainlingError;
    if (!chainling) {
      return jsonResponse({ success: false, error: "chainling_not_found_or_not_owned" }, 404);
    }

    const rebirthCount = Math.max(0, Number((chainling as ChainlingProgressionRow).rebirth_count ?? 0));
    if (rebirthCount >= REBIRTH_RAM_COSTS.length) {
      return jsonResponse({ success: false, error: "rebirth_max_reached" }, 400);
    }

    const cost = getRebirthRamCost(rebirthCount);
    const { data, error } = await supabase.rpc("profile_rebirth_chainling_tx", {
      p_profile_id: profile.player_profile_id,
      p_chainling_id: chainlingId,
      p_stat_index: statIndex,
      p_cost: cost,
    });

    if (error) {
      return jsonResponse({
        success: false,
        error: normalizeRpcError(error.message),
      }, 400);
    }

    const payload = (data ?? {}) as Record<string, unknown>;
    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      ram_balance: Number(payload.ram_balance ?? 0),
      chainling: payload.chainling ?? {},
    });
  } catch (error) {
    console.error("[profile_rebirth_chainling]", error);
    return jsonResponse({ success: false, error: "profile_rebirth_chainling_failed" }, 500);
  }
});

function normalizeRpcError(message: string): string {
  const normalized = (message ?? "").trim();
  if (!normalized) return "profile_rebirth_chainling_failed";
  if (normalized.includes("chainling_not_found_or_not_owned")) return "chainling_not_found_or_not_owned";
  if (normalized.includes("rebirth_requires_star_5")) return "rebirth_requires_star_5";
  if (normalized.includes("rebirth_max_reached")) return "rebirth_max_reached";
  if (normalized.includes("insufficient_ram")) return "insufficient_ram";
  if (normalized.includes("invalid_stat")) return "invalid_rebirth_stat";
  return normalized;
}
