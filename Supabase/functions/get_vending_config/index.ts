import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { JsonMap, jsonResponse, readJsonBody, requirePost } from "../_shared/account_helpers.ts";
import { hashVendingConfig, VENDING_CONFIG } from "../_shared/vending_config.ts";

/**
 * Returns the authoritative vending lever board for clients.
 * Unity VendingConfigService applies this as an override on top of local SO defaults.
 *
 * Body (optional): { event_id?: string } — reserved for future event overlays.
 */
serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  // Body is optional; keep parse for future event_id overlays.
  await readJsonBody<JsonMap>(req);

  const cfg = VENDING_CONFIG;
  const configHash = hashVendingConfig(cfg);

  return jsonResponse({
    success: true,
    config_version: cfg.configVersion,
    generator_version: cfg.generatorVersion,
    config_hash: configHash,
    rarity_tier_weights_percent: cfg.rarityTierWeightsPercent,
    ram_cost_get_known: cfg.ramCostGetKnown,
    ram_cost_stamp_known: cfg.ramCostStampKnown,
    vending_daily_cap: cfg.vendingDailyCap,
    event_favor_boost: cfg.eventFavorBoost,
    stamp_ram_cost: cfg.stampRamCost,
    bind_ram_cost: cfg.bindRamCost,
  });
});
