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

interface ChainlingContribution {
  chainling_id?: string;
  instance_id?: string;
  display_name?: string;
  total_victories?: number;
  xp_earned_lifetime?: number;
  progression_moves_granted?: number;
  damage_dealt?: number;
  damage_taken?: number;
  damage_mitigated?: number;
  healing_total?: number;
  enemy_kos?: number;
  legacy_times_kod?: number;
  previous_stars?: number;
  new_stars?: number;
  gained_star?: boolean;
  sync_current_hp?: boolean;
  current_hp?: number;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const battleType = str(body.battle_type).toLowerCase();
    if (battleType && battleType !== "pve") {
      return jsonResponse({ success: true, skipped: true, reason: "not_pve" });
    }

    const chainlings = Array.isArray(body.chainlings) ? body.chainlings as ChainlingContribution[] : [];

    if (chainlings.length === 0) {
      return jsonResponse({ success: true, applied_count: 0, chainlings: [] });
    }

    const battleSeed = integer(body.battle_seed);
    const forensicHash = str(body.forensic_hash);
    const configHash = str(body.config_hash);
    // PvE outcomes are still client-resolved. RAM is therefore never mutated here;
    // retain reward configuration for a future authoritative verifier only.
    const ramPenalty = 0;
    const applied: unknown[] = [];
    let appliedCount = 0;
    let totalEnemyKos = 0;
    let totalTimesKod = 0;
    let totalStarUps = 0;

    for (const contribution of chainlings) {
      const chainlingId = str(contribution.chainling_id);
      if (!chainlingId) {
        return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
      }

      const delta: Record<string, number | string> = {
        total_victories: nonNegativeInteger(contribution.total_victories),
        xp_earned_lifetime: nonNegativeInteger(contribution.xp_earned_lifetime),
        progression_moves_granted: nonNegativeInteger(contribution.progression_moves_granted),
        damage_dealt: nonNegativeInteger(contribution.damage_dealt),
        damage_taken: nonNegativeInteger(contribution.damage_taken),
        damage_mitigated: nonNegativeInteger(contribution.damage_mitigated),
        healing_total: nonNegativeInteger(contribution.healing_total),
        enemy_kos: nonNegativeInteger(contribution.enemy_kos),
        legacy_times_kod: nonNegativeInteger(contribution.legacy_times_kod),
        previous_stars: nonNegativeInteger(contribution.previous_stars),
        new_stars: nonNegativeInteger(contribution.new_stars),
      };

      if (contribution.sync_current_hp === true) {
        delta.current_hp = nonNegativeInteger(contribution.current_hp);
      }

      if (configHash) {
        delta.config_hash = configHash;
      }

      const idempotencyKey = buildIdempotencyKey(
        "pve_battle",
        profile.player_profile_id,
        chainlingId,
        forensicHash,
        battleSeed,
      );

      const { data, error } = await supabase.rpc("profile_apply_pve_battle_outcome_tx", {
        p_profile_id: profile.player_profile_id,
        p_chainling_id: chainlingId,
        p_delta_json: delta,
        p_idempotency_key: idempotencyKey,
      });

      if (error) {
        return jsonResponse({
          success: false,
          error: normalizeRpcError(error.message),
          chainling_id: chainlingId,
        }, 400);
      }

      const payload = (data ?? {}) as Record<string, unknown>;
      if (payload.applied === true) {
        appliedCount += 1;
        totalEnemyKos += nonNegativeInteger(contribution.enemy_kos);
        totalTimesKod += nonNegativeInteger(contribution.legacy_times_kod);
        totalStarUps += contribution.gained_star === true ? 1 : 0;
      }
      applied.push(payload);
    }

    if (appliedCount > 0) {
      const { error: lifetimeError } = await supabase.rpc("profile_apply_pve_profile_lifetime_stats_tx", {
        p_profile_id: profile.player_profile_id,
        p_battles_won: bool(body.player_won) && !bool(body.was_fled) ? 1 : 0,
        p_battles_lost: !bool(body.player_won) && !bool(body.is_draw) && !bool(body.was_fled) ? 1 : 0,
        p_battles_drawn: bool(body.is_draw) && !bool(body.was_fled) ? 1 : 0,
        p_battles_fled: bool(body.was_fled) ? 1 : 0,
        p_total_kos_scored: totalEnemyKos,
        p_total_kos_taken: totalTimesKod,
        p_total_star_ups: totalStarUps,
        p_total_ram_earned: 0,
        p_total_ram_spent: ramPenalty,
      });

      if (lifetimeError) {
        console.warn("[profile_apply_pve_battle_outcome] player lifetime stat update failed:", lifetimeError.message);
      }
    }

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      applied_count: appliedCount,
      ram_delta_applied: 0,
      chainlings: applied,
    });
  } catch (error) {
    console.error("[profile_apply_pve_battle_outcome]", error);
    return jsonResponse({ success: false, error: "profile_apply_pve_battle_outcome_failed" }, 500);
  }
});

function buildIdempotencyKey(
  prefix: string,
  profileId: string,
  chainlingId: string,
  forensicHash: string,
  battleSeed: number,
): string {
  const battlePart = forensicHash || `seed_${battleSeed}`;
  return `${prefix}:${profileId}:${chainlingId}:${battlePart}`;
}

function nonNegativeInteger(value: unknown): number {
  const parsed = integer(value);
  return parsed > 0 ? parsed : 0;
}

function integer(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value)) return Math.trunc(value);
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return Math.trunc(parsed);
  }
  return 0;
}

function bool(value: unknown): boolean {
  return value === true || value === "true";
}

function normalizeRpcError(message: string): string {
  const normalized = (message ?? "").trim();
  if (!normalized) return "profile_apply_pve_battle_outcome_failed";
  if (normalized.includes("chainling_not_found_or_not_owned")) return "chainling_not_found_or_not_owned";
  return normalized;
}
