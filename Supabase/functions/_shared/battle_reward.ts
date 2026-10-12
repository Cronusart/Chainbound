import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { JsonMap, str } from "./account_helpers.ts";

export type BattleRewardMode = "off" | "shadow" | "grant";

export interface BattleRewardResult extends JsonMap {
  status: string;
  battleId: string;
}

export function loadBattleRewardMode(): BattleRewardMode {
  // The database row is the rollout authority. An unset or "grant" env value
  // adds no second gate; "off" and "shadow" may only restrict it further.
  const mode = str(Deno.env.get("BATTLE_REWARD_MODE")).toLowerCase() || "grant";
  if (mode !== "off" && mode !== "shadow" && mode !== "grant") {
    throw new Error("invalid_battle_reward_mode");
  }
  return mode;
}

export function loadBattleRewardProfileAllowlist(): ReadonlySet<string> | null {
  const raw = str(Deno.env.get("BATTLE_REWARD_DEVNET_PROFILE_ALLOWLIST"));
  if (!raw) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new Error("invalid_battle_reward_profile_allowlist");
  }
  if (!Array.isArray(parsed) || parsed.length === 0 || parsed.length > 100) {
    throw new Error("invalid_battle_reward_profile_allowlist");
  }
  const result = new Set<string>();
  for (const value of parsed) {
    const profileId = typeof value === "string" ? value.trim().toLowerCase() : "";
    if (!UUID_RE.test(profileId) || result.has(profileId)) {
      throw new Error("invalid_battle_reward_profile_allowlist");
    }
    result.add(profileId);
  }
  return result;
}

export function battleRewardIdempotencyKey(cluster: string, battleId: string): string {
  if (cluster !== "devnet" && cluster !== "mainnet-beta") throw new Error("invalid_reward_cluster");
  if (!UUID_RE.test(battleId)) throw new Error("invalid_battle_id");
  return `battle_reward:v1:${cluster}:${battleId.toLowerCase()}`;
}

/**
 * Classifies server-owned terminal state, then performs the entire positive
 * credit in one Postgres transaction. There is no wallet or Solana dependency.
 */
export async function advanceBattleReward(
  supabase: SupabaseClient,
  battleId: string,
): Promise<BattleRewardResult> {
  if (!UUID_RE.test(battleId)) throw new Error("invalid_battle_id");
  const normalizedBattleId = battleId.toLowerCase();
  const mode = loadBattleRewardMode();
  const { data: battle, error: battleError } = await supabase
    .from("authoritative_pve_battles")
    .select("battle_id,player_profile_id")
    .eq("battle_id", normalizedBattleId)
    .maybeSingle();
  if (battleError) throw battleError;
  if (!battle) throw new Error("battle_not_found");

  const profileId = str(battle.player_profile_id).toLowerCase();
  const environmentAllowlist = loadBattleRewardProfileAllowlist();
  const gateReason = mode === "off"
    ? "rewards_disabled"
    : mode === "shadow"
    ? "shadow_mode"
    : environmentAllowlist !== null && !environmentAllowlist.has(profileId)
    ? "not_allowlisted"
    : null;
  const classified = await rpc<BattleRewardResult>(supabase, "battle_reward_classify_service", {
    p_battle_id: normalizedBattleId,
    p_gate_reason: gateReason,
  });
  if (classified.status !== "eligible") return classified;
  if (mode !== "grant") throw new Error("eligible_reward_escaped_gate");

  return await rpc<BattleRewardResult>(supabase, "ram_battle_reward_settle_service", {
    p_battle_id: normalizedBattleId,
  });
}

async function rpc<T extends JsonMap>(
  supabase: SupabaseClient,
  name: string,
  args: JsonMap,
): Promise<T> {
  const { data, error } = await supabase.rpc(name, args);
  if (error) throw new Error(normalizeRpcError(error.message));
  if (!data || typeof data !== "object" || Array.isArray(data)) throw new Error("invalid_reward_rpc_response");
  return data as T;
}

function normalizeRpcError(message: string): string {
  const known = [
    "battle_not_found",
    "battle_not_terminal",
    "reward_config_not_allowed",
    "reward_tier_not_allowed",
    "battle_reward_operation_conflict",
    "ram_balance_overflow",
  ];
  return known.find((value) => message.includes(value)) ?? "battle_reward_rpc_failed";
}

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
