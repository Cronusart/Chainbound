import { deriveKnownResult, deriveRamOperationId, KnownAuthorityResult } from "./ownership_spine.ts";

/** Upper bound on one Candy Machine bundle; matches the RPC and table check. */
export const KNOWN_ROLL_MAX_PULLS = 20;

export interface KnownBundlePull {
  index: number;
  idempotencyKey: string;
  operationId: string;
  result: KnownAuthorityResult;
}

/** Missing pull_count means a single roll. Anything else must be an integer in 1..KNOWN_ROLL_MAX_PULLS. */
export function parsePullCount(value: unknown): number | null {
  if (value === undefined || value === null) return 1;
  if (typeof value !== "number" || !Number.isInteger(value)) return null;
  return value >= 1 && value <= KNOWN_ROLL_MAX_PULLS ? value : null;
}

/**
 * Per-pull key stored on the Known row and ledger operation. The RPC derives
 * the same string itself, so the two must stay in lockstep.
 */
export function knownBundlePullKey(bundleKey: string, index: number): string {
  return `${bundleKey}#${index}`;
}

/**
 * Each pull is a pure function of (profile, bundle key, index): the result uses
 * the same authority digest as a single roll keyed by the per-pull key, so a
 * retried bundle re-derives byte-identical pulls and the RPC can replay it.
 */
export async function deriveKnownBundle(
  secret: Uint8Array,
  profileId: string,
  bundleKey: string,
  pullCount: number,
): Promise<KnownBundlePull[]> {
  const pulls: KnownBundlePull[] = [];
  for (let index = 0; index < pullCount; index++) {
    const idempotencyKey = knownBundlePullKey(bundleKey, index);
    pulls.push({
      index,
      idempotencyKey,
      operationId: await deriveRamOperationId(profileId, "known_roll", idempotencyKey),
      result: await deriveKnownResult(secret, profileId, idempotencyKey),
    });
  }
  return pulls;
}

/** Shape expected by ownership_roll_known_bundle_profile_atomic's p_pulls argument. */
export function knownBundleRpcPulls(pulls: readonly KnownBundlePull[]): Record<string, unknown>[] {
  return pulls.map((pull) => ({
    operation_id: pull.operationId,
    move_id: pull.result.moveId,
    onchain_move_id: pull.result.onchainMoveId,
    element: pull.result.element,
    base_power: pull.result.basePower,
  }));
}
