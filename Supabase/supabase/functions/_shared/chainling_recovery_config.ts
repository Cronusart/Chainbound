/**
 * Authoritative passive-recovery levers. Unity mirrors these values for UI and
 * previews, but only the server-side resolver applies HP changes.
 */
export const CHAINLING_RECOVERY_CONFIG = {
  configVersion: 2,
  enabled: true,
  /** One completed recovery interval every 30 minutes of real time. */
  intervalSeconds: 1800,
  /** 10% of canonical MaxHP per completed interval, expressed in basis points. */
  healBasisPoints: 1000,
  /** At most 24 hours of intervals per resolver invocation. */
  maxCatchupIntervals: 48,
  /** Equipped party Chainlings do not heal. Only PC storage does. */
  storageOnly: true,
};

/** Simple stable hash for client diagnostics and config stamping (not cryptographic). */
export function hashChainlingRecoveryConfig(cfg = CHAINLING_RECOVERY_CONFIG): string {
  const payload = JSON.stringify({
    v: cfg.configVersion,
    enabled: cfg.enabled,
    interval: cfg.intervalSeconds,
    healBp: cfg.healBasisPoints,
    catchup: cfg.maxCatchupIntervals,
    storage: cfg.storageOnly,
  });
  let h = 2166136261;
  for (let i = 0; i < payload.length; i++) {
    h ^= payload.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return ("00000000" + (h >>> 0).toString(16)).slice(-8);
}
