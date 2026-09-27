import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { CHAINLING_RECOVERY_CONFIG } from "./chainling_recovery_config.ts";

const RPC_ENDPOINT_ENV = "CHAINBOUND_SOLANA_RPC_ENDPOINT";
const RPC_TIMEOUT_MS = 1500;

/**
 * Lazily resolves elapsed recovery before a roster snapshot is returned. This
 * is intentionally best-effort: an unavailable Solana RPC never blocks login
 * or combat, it only defers recovery until a later authoritative read.
 */
export async function applyChainlingPassiveRecovery(
  supabase: SupabaseClient,
  playerProfileId: string,
): Promise<void> {
  const cfg = CHAINLING_RECOVERY_CONFIG;
  if (!cfg.enabled || cfg.healBasisPoints <= 0 || !playerProfileId) return;

  const authoritativeTime = await getSolanaBlockTimeIso();
  if (!authoritativeTime) {
    console.warn("[chainling_passive_recovery] Skipped: Solana block time unavailable.");
    return;
  }

  const { error } = await supabase.rpc("profile_apply_chainling_passive_recovery_tx", {
    p_profile_id: playerProfileId,
    p_authoritative_time: authoritativeTime,
    p_interval_seconds: cfg.intervalSeconds,
    p_heal_basis_points: cfg.healBasisPoints,
    p_max_catchup_intervals: cfg.maxCatchupIntervals,
    p_party_only: cfg.partyMembersOnly,
  });

  if (error) {
    // Do not fail the containing read. The resolver is idempotent and will retry
    // on the next authenticated roster/party request.
    console.warn("[chainling_passive_recovery] Skipped:", error.message);
  }
}

async function getSolanaBlockTimeIso(): Promise<string | null> {
  const endpoint = (Deno.env.get(RPC_ENDPOINT_ENV) ?? "").trim();
  if (!endpoint) return null;

  try {
    const slot = await solanaRpc<number>(endpoint, "getSlot", [{ commitment: "finalized" }]);
    if (!Number.isSafeInteger(slot) || slot <= 0) return null;

    const unixSeconds = await solanaRpc<number | null>(endpoint, "getBlockTime", [slot]);
    if (typeof unixSeconds !== "number" || !Number.isSafeInteger(unixSeconds) || unixSeconds <= 0) return null;

    return new Date(unixSeconds * 1000).toISOString();
  } catch (error) {
    console.warn("[chainling_passive_recovery] Solana time lookup failed:", error);
    return null;
  }
}

async function solanaRpc<T>(endpoint: string, method: string, params: unknown[]): Promise<T> {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), RPC_TIMEOUT_MS);
  try {
    const response = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ jsonrpc: "2.0", id: "chainling-passive-recovery", method, params }),
      signal: controller.signal,
    });
    if (!response.ok) throw new Error(`${method} HTTP ${response.status}`);

    const payload = await response.json() as { result?: T; error?: { message?: string } };
    if (payload.error) throw new Error(payload.error.message ?? `${method} RPC error`);
    return payload.result as T;
  } finally {
    clearTimeout(timeoutId);
  }
}
