/**
 * Shared vending / progression lever constants.
 * Authoritative for Get Known, Stamp, and progression costs.
 * Keep in sync with Unity VendingConfigSO / ProgressionConfigSO defaults (ECONOMY_01).
 */

export const VENDING_CONFIG = {
  configVersion: 1,
  generatorVersion: 2,
  /** Percent weights: Common, Uncommon, Rare, Epic, Legendary, Mythic (ECONOMY_01). */
  rarityTierWeightsPercent: [45, 30, 17.5, 5, 2, 0.5] as number[],
  /** Atomic Known-to-Stamped Core creation cost. */
  stampRamCost: 75,
  /** Profile archive bind cost. */
  bindRamCost: 250,
  ramCostGetKnown: 0,
  ramCostStampKnown: 75,
  vendingDailyCap: 10,
  /** Server-gated event favor boost. Default 0 (Favor does not bias dispenser rarity). */
  eventFavorBoost: 0,
};

export const PROGRESSION_CONFIG = {
  configVersion: 1,
  rebirthRamCosts: [1000, 1500, 2000, 2500, 3000] as number[],
  /** MaxHP Cycle grants are percent-of-base in Unity (5%), not flat +5. */
  cycleAutoHpPercent: 0.05,
  cyclePickHpPercent: 0.05,
};

/** Simple stable hash for config stamping (not cryptographic). */
export function hashVendingConfig(cfg = VENDING_CONFIG): string {
  const payload = JSON.stringify({
    v: cfg.configVersion,
    gen: cfg.generatorVersion,
    tiers: cfg.rarityTierWeightsPercent,
    getKnown: cfg.ramCostGetKnown,
    stampKnown: cfg.ramCostStampKnown,
    cap: cfg.vendingDailyCap,
    favor: cfg.eventFavorBoost,
  });
  let h = 2166136261;
  for (let i = 0; i < payload.length; i++) {
    h ^= payload.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return ("00000000" + (h >>> 0).toString(16)).slice(-8);
}

export function getRebirthRamCost(rebirthCount: number): number {
  const costs = PROGRESSION_CONFIG.rebirthRamCosts;
  if (!costs.length) return 0;
  const index = Math.max(0, Math.min(rebirthCount, costs.length - 1));
  return Math.max(0, costs[index] ?? 0);
}
