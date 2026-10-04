import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { CHAINLING_RECOVERY_CONFIG } from "./chainling_recovery_config.ts";

const RPC_ENDPOINT_ENV = "CHAINBOUND_SOLANA_RPC_ENDPOINT";
const RPC_TIMEOUT_MS = 1500;
/**
 * getBlockTime on the latest finalized slot is often null. These offsets stay
 * inside the resolver's one-hour acceptance window (300 slots is about two minutes).
 */
const BLOCK_TIME_SLOT_OFFSETS = [0, 30, 150, 300];
const MAX_PAST_MS = 60 * 60 * 1000;
const MAX_FUTURE_MS = 5 * 60 * 1000;

export type RecoveryClock = {
  iso: string;
  source: "solana" | "server_utc";
};

/**
 * Solana cluster time when a nearby finalized block has a timestamp. Otherwise
 * server UTC, which is the same real 24-hour day time-of-day follows. The
 * player device clock is never accepted.
 */
export async function resolveRecoveryClock(): Promise<RecoveryClock> {
  const solana = await getSolanaBlockTimeIso();
  if (solana) return { iso: solana, source: "solana" };

  console.warn(
    "[chainling_passive_recovery] Solana block time unavailable; using server UTC.",
  );
  return { iso: new Date().toISOString(), source: "server_utc" };
}

/**
 * Lazily resolves elapsed storage recovery before a roster snapshot is returned.
 * A clock failure never blocks login or combat.
 */
export async function applyChainlingPassiveRecovery(
  supabase: SupabaseClient,
  playerProfileId: string,
  clock?: RecoveryClock,
): Promise<void> {
  const cfg = CHAINLING_RECOVERY_CONFIG;
  if (!cfg.enabled || cfg.healBasisPoints <= 0 || !playerProfileId) return;

  const resolved = clock ?? await resolveRecoveryClock();
  const applied = await applyWithClock(supabase, playerProfileId, resolved);
  if (applied || resolved.source !== "solana") return;

  const fallback = { iso: new Date().toISOString(), source: "server_utc" as const };
  console.warn(
    "[chainling_passive_recovery] Solana timestamp was rejected; retrying with server UTC.",
  );
  await applyWithClock(supabase, playerProfileId, fallback);
}

async function applyWithClock(
  supabase: SupabaseClient,
  playerProfileId: string,
  clock: RecoveryClock,
): Promise<boolean> {
  const cfg = CHAINLING_RECOVERY_CONFIG;
  const { data, error } = await supabase.rpc("profile_apply_chainling_passive_recovery_tx", {
    p_profile_id: playerProfileId,
    p_authoritative_time: clock.iso,
    p_interval_seconds: cfg.intervalSeconds,
    p_heal_basis_points: cfg.healBasisPoints,
    p_max_catchup_intervals: cfg.maxCatchupIntervals,
    p_storage_only: cfg.storageOnly,
  });

  if (error) {
    console.warn(`[chainling_passive_recovery] Skipped (${clock.source}):`, error.message);
    return false;
  }

  const payload = data as { applied?: boolean; reason?: string; updated_chainlings?: number } | null;
  if (payload && payload.applied === false) {
    console.warn(
      `[chainling_passive_recovery] Not applied (${clock.source}): ${payload.reason ?? "unknown"}`,
    );
    return false;
  }

  if (payload && (payload.updated_chainlings ?? 0) > 0) {
    console.log(
      `[chainling_passive_recovery] Updated ${payload.updated_chainlings} storage Chainlings via ${clock.source}.`,
    );
  }
  return true;
}

async function getSolanaBlockTimeIso(): Promise<string | null> {
  const endpoint = (Deno.env.get(RPC_ENDPOINT_ENV) ?? "").trim();
  if (!endpoint) return null;

  try {
    const slot = await solanaRpc<number>(endpoint, "getSlot", [{ commitment: "finalized" }]);
    if (!Number.isSafeInteger(slot) || slot <= 0) return null;

    const samples = await Promise.all(BLOCK_TIME_SLOT_OFFSETS.map(async (offset) => {
      const target = slot - offset;
      if (target <= 0) return null;
      try {
        const unixSeconds = await solanaRpc<number | null>(endpoint, "getBlockTime", [target]);
        return isUsableUnixSeconds(unixSeconds) ? unixSeconds : null;
      } catch {
        return null;
      }
    }));

    const unixSeconds = samples.find((sample) => sample != null);
    return unixSeconds == null ? null : new Date(unixSeconds * 1000).toISOString();
  } catch (error) {
    console.warn("[chainling_passive_recovery] Solana time lookup failed:", error);
    return null;
  }
}

function isUsableUnixSeconds(unixSeconds: number | null): unixSeconds is number {
  if (typeof unixSeconds !== "number" || !Number.isSafeInteger(unixSeconds) || unixSeconds <= 0) {
    return false;
  }

  const skewMs = Date.now() - unixSeconds * 1000;
  return skewMs <= MAX_PAST_MS && skewMs >= -MAX_FUTURE_MS;
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
