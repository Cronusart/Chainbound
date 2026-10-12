import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { JsonMap, jsonResponse, readJsonBody, requirePost } from "../_shared/account_helpers.ts";
import { CHAINLING_RECOVERY_CONFIG, hashChainlingRecoveryConfig } from "../_shared/chainling_recovery_config.ts";

/** Returns the passive-recovery board used by the server resolver. */
serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  await readJsonBody<JsonMap>(req);
  const cfg = CHAINLING_RECOVERY_CONFIG;
  return jsonResponse({
    success: true,
    config_version: cfg.configVersion,
    config_hash: hashChainlingRecoveryConfig(cfg),
    enabled: cfg.enabled,
    interval_seconds: cfg.intervalSeconds,
    heal_basis_points: cfg.healBasisPoints,
    max_catchup_intervals: cfg.maxCatchupIntervals,
    storage_only: cfg.storageOnly,
  });
});
