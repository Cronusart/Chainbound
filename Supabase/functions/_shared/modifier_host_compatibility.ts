import { MODIFIER_POOL } from "./modifier_pool.generated.ts";
import { MOVE_CARD_CATALOG } from "./ownership_card_catalog.generated.ts";

// Mirrors the non-empty AllowedCategories cells in Modifiers.csv. Empty rows are
// intentionally unrestricted. Keep this map covered by the pool-compatibility test
// when the generated modifier pool changes.
const ALLOWED_HOST_CATEGORIES: Readonly<Record<string, readonly string[]>> = {
  "2.1-a": ["DamagingMelee", "DamagingRanged"],
  "2.1-b": ["DamagingMelee", "DamagingRanged"],
  "2.1-c": ["DamagingMelee", "DamagingRanged"],
  "2.1-d": ["DamagingMelee", "DamagingRanged"],
  "2.1-e": ["DamagingMelee", "DamagingRanged"],
  "2.3-a": ["DamagingMelee", "DamagingRanged"],
  "2.3-b": ["DamagingMelee", "DamagingRanged"],
  "2.3-c": ["DamagingMelee", "DamagingRanged"],
  "2.3-d": ["DamagingMelee", "DamagingRanged"],
  "2.3-e": ["DamagingMelee", "DamagingRanged"],
  "3.2-a": ["DamagingMelee", "DamagingRanged", "StatusUtility"],
  "4.2-a": ["DamagingMelee", "DamagingRanged"],
  "4.3-a": ["DamagingMelee", "DamagingRanged"],
  "4.3-b": ["DamagingMelee", "DamagingRanged"],
  "5.1-a": ["DamagingMelee", "DamagingRanged"],
  "5.2-a": ["DamagingMelee"],
  "5.2-b": ["DamagingMelee"],
  "5.2-c": ["DamagingMelee"],
  "5.2-d": ["DamagingMelee", "DamagingRanged"],
  "6.1-a": ["DamagingMelee", "DamagingRanged"],
  "6.1-b": ["DamagingMelee", "DamagingRanged"],
  "6.1-c": ["DamagingMelee", "DamagingRanged"],
  "6.1-d": ["DamagingMelee", "DamagingRanged"],
  "6.1-e": ["DamagingMelee", "DamagingRanged"],
  "6.1-f": ["DamagingMelee", "DamagingRanged"],
  "6.2-a": ["DamagingMelee", "DamagingRanged"],
  "6.2-b": ["DamagingMelee", "DamagingRanged"],
  "6.2-c": ["DamagingMelee", "DamagingRanged"],
  "6.3-a": ["DamagingMelee", "DamagingRanged"],
  "6.3-b": ["DamagingMelee", "DamagingRanged"],
  "6.3-d": ["DamagingMelee", "DamagingRanged"],
  "6.3-e": ["DamagingMelee", "DamagingRanged"],
  "6.4-d": ["Support"],
  "6.4-e": ["Support"],
  "6.4-h": ["Support"],
  "6.4-i": ["Support"],
  "7.1-a": ["DamagingMelee", "DamagingRanged"],
  "7.1-b": ["Support"],
  "7.1-c": ["Support"],
  "7.1-d": ["StatusUtility"],
  "7.2-a": ["DamagingMelee", "DamagingRanged"],
  "7.2-b": ["DamagingMelee", "DamagingRanged"],
  "7.2-c": ["StatusUtility"],
  "10.1-a": ["DamagingMelee", "DamagingRanged"],
  "10.1-b": ["DamagingMelee", "DamagingRanged"],
  "10.1-c": ["DamagingMelee", "DamagingRanged"],
  "10.1-d": ["Passive"],
  "1.1-a": ["DamagingMelee", "DamagingRanged"],
  "1.2-a": ["DamagingMelee", "DamagingRanged"],
  "2.4-a": ["StatusUtility", "Support"],
  "2.5-a": ["StatusUtility", "Support"],
  "2.5-b": ["StatusUtility", "Support"],
  "2.6-a": ["DamagingMelee", "DamagingRanged"],
  "3.3-a": ["DamagingMelee", "DamagingRanged", "StatusUtility"],
  "4.4-a": ["DamagingMelee", "DamagingRanged"],
  "4.5-a": ["Support"],
  "4.6-a": ["DamagingMelee", "DamagingRanged"],
  "4.7-a": ["DamagingMelee", "DamagingRanged"],
  "5.2-e": ["DamagingMelee"],
  "5.2-f": ["DamagingMelee"],
  "5.2-g": ["DamagingMelee"],
  "6.5-a": ["DamagingMelee", "DamagingRanged"],
  "6.5-b": ["DamagingMelee", "DamagingRanged"],
  "6.6-a": ["DamagingMelee", "DamagingRanged"],
  "6.6-b": ["DamagingMelee", "DamagingRanged"],
  "6.7-a": ["DamagingMelee", "DamagingRanged"],
  "6.7-b": ["DamagingMelee", "DamagingRanged"],
  "6.7-c": ["DamagingMelee", "DamagingRanged"],
  "8.1-a": ["Passive"],
  "8.1-b": ["Passive"],
  "8.1-c": ["Passive"],
  "8.1-d": ["Passive"],
  "8.1-e": ["Passive"],
  "1.3-a": ["StatusUtility", "Support", "Passive"],
  "1.3-b": ["StatusUtility"],
  "4.8-a": ["DamagingMelee", "DamagingRanged"],
  "6.8-a": ["StatusUtility", "Support"],
  "6.8-b": ["StatusUtility", "Support", "Passive"],
  "6.8-c": ["StatusUtility", "Support", "Passive"],
  "6.8-d": ["Passive"],
  "6.8-e": ["StatusUtility", "Support", "Passive"],
  "6.8-f": ["StatusUtility", "Support", "Passive"],
  "6.8-g": ["Support", "StatusUtility"],
  "7.3-a": ["StatusUtility", "Support"],
};

function normalizeCategory(value: string): string {
  return value.replace(/[^A-Za-z0-9]/g, "");
}

export function eligibleModifierPoolForMove(moveId: string): readonly string[] {
  const catalog = MOVE_CARD_CATALOG as Record<string, { categoryName: string }>;
  const move = catalog[moveId];
  if (!move) throw new Error("unknown_stamp_host_move");

  const hostCategory = normalizeCategory(move.categoryName);
  const eligible = MODIFIER_POOL.filter((sectionId) => {
    const allowed = ALLOWED_HOST_CATEGORIES[sectionId];
    return !allowed || allowed.includes(hostCategory);
  });
  if (eligible.length === 0) throw new Error("no_compatible_modifiers");
  return eligible;
}

export function allowedHostCategoriesForModifier(sectionId: string): readonly string[] | null {
  return ALLOWED_HOST_CATEGORIES[sectionId] ?? null;
}
