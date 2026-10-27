import { MODIFIER_POOL } from "./modifier_pool.generated.ts";
import { STAMP_HOST_MOVES, STAMP_MODIFIER_RULES } from "./stamp_rules.generated.ts";
import type { StampHostMove, StampModifierRule } from "./stamp_rules.generated.ts";

// Server mirror of ModifierValidationService.CanApply. The rules come from the
// imported Unity assets (CombatServerTablesExporter), so the roller and the client
// trust check read the same AllowedCategories, ForbiddenTags, ForbiddenCombos,
// SlotType, caps, and Extra Hit's multi-hit requirement. Weight 0 rows (retired)
// stay in the pinned pool but are never rolled.

function ruleFor(sectionId: string): StampModifierRule {
  const rule = STAMP_MODIFIER_RULES[sectionId];
  if (!rule) throw new Error("unknown_stamp_modifier");
  return rule;
}

function hostFor(moveId: string): StampHostMove {
  const host = STAMP_HOST_MOVES[(moveId ?? "").trim()];
  if (!host) throw new Error("unknown_stamp_host_move");
  return host;
}

function sameText(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}

function fitsHost(rule: StampModifierRule, host: StampHostMove): boolean {
  if (rule.weight <= 0) return false;
  if (rule.allowedCategories.length > 0 && !rule.allowedCategories.includes(host.category)) return false;
  if (rule.powerCap > 0 && host.power > rule.powerCap) return false;
  if (rule.apCostCap > 0 && host.apCost > rule.apCostCap) return false;
  if (rule.requiresMultiHit && !host.multiHit) return false;
  return !rule.forbiddenTags.some((tag) => host.tags.some((hostTag) => sameText(tag, hostTag)));
}

/** True when two modifiers can share one stamp: no forbidden combo, slot type, or swap trigger clash. */
export function canPairModifiers(firstId: string, secondId: string): boolean {
  if (sameText(firstId, secondId)) return false;
  const first = ruleFor(firstId);
  const second = ruleFor(secondId);
  if (first.forbiddenCombos.some((id) => sameText(id, secondId))) return false;
  if (second.forbiddenCombos.some((id) => sameText(id, firstId))) return false;
  if (first.slotType !== "None" && first.slotType === second.slotType) return false;
  if (first.trigger === "OnSwapIn" && second.trigger === "OnSwapIn") return false;
  if (first.trigger === "OnSwapOut" && second.trigger === "OnSwapOut") return false;
  return true;
}

/** Pool rows that work on this host move, in pinned pool order. */
export function eligibleModifierPoolForMove(moveId: string): readonly string[] {
  const host = hostFor(moveId);
  const eligible = MODIFIER_POOL.filter((sectionId) => fitsHost(ruleFor(sectionId), host));
  if (eligible.length === 0) throw new Error("no_compatible_modifiers");
  return eligible;
}

/** Second-modifier candidates for a two-modifier stamp whose first roll is firstId. */
export function eligibleSecondModifiersForMove(moveId: string, firstId: string): readonly string[] {
  return eligibleModifierPoolForMove(moveId).filter((sectionId) => canPairModifiers(firstId, sectionId));
}

export function allowedHostCategoriesForModifier(sectionId: string): readonly string[] | null {
  const rule = STAMP_MODIFIER_RULES[sectionId];
  return rule && rule.allowedCategories.length > 0 ? rule.allowedCategories : null;
}
