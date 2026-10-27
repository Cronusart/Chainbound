import { assertEquals, assertThrows } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { deriveStampResult, EXPECTED_MODIFIER_POOL_HASH, MODIFIER_POOL } from "./ownership_spine.ts";
import {
  canPairModifiers,
  eligibleModifierPoolForMove,
  eligibleSecondModifiersForMove,
} from "./modifier_host_compatibility.ts";
import { STAMP_HOST_MOVES, STAMP_MODIFIER_RULES } from "./stamp_rules.generated.ts";

const hostIds = Object.keys(STAMP_HOST_MOVES).filter((moveId) => {
  try {
    eligibleModifierPoolForMove(moveId);
    return true;
  } catch {
    return false;
  }
});

Deno.test("every pinned pool row has a generated stamp rule", () => {
  for (const sectionId of MODIFIER_POOL) assertEquals(sectionId in STAMP_MODIFIER_RULES, true, sectionId);
});

Deno.test("eligible stamp modifiers always fit the host move", () => {
  for (const moveId of hostIds) {
    const host = STAMP_HOST_MOVES[moveId];
    for (const sectionId of eligibleModifierPoolForMove(moveId)) {
      const rule = STAMP_MODIFIER_RULES[sectionId];
      const label = `${sectionId} on ${moveId}`;
      assertEquals(rule.weight > 0, true, label);
      assertEquals(rule.allowedCategories.length === 0 || rule.allowedCategories.includes(host.category), true, label);
      assertEquals(rule.powerCap === 0 || host.power <= rule.powerCap, true, label);
      assertEquals(rule.apCostCap === 0 || host.apCost <= rule.apCostCap, true, label);
      assertEquals(!rule.requiresMultiHit || host.multiHit, true, label);
      const blocked = rule.forbiddenTags.some((tag) => host.tags.some((hostTag) => hostTag.toLowerCase() === tag.toLowerCase()));
      assertEquals(blocked, false, label);
    }
  }
});

Deno.test("retired rows stay pinned but are never rolled", () => {
  const retired = MODIFIER_POOL.filter((sectionId) => STAMP_MODIFIER_RULES[sectionId].weight <= 0);
  assertEquals(retired.length > 0, true);
  for (const moveId of hostIds) {
    const eligible = eligibleModifierPoolForMove(moveId);
    for (const sectionId of retired) assertEquals(eligible.includes(sectionId), false, `${sectionId} on ${moveId}`);
  }
});

Deno.test("Extra Hit only rolls on multi-hit damaging moves", () => {
  assertEquals(STAMP_HOST_MOVES["baseline_striker"].multiHit, false);
  assertEquals(eligibleModifierPoolForMove("baseline_striker").includes("4.1-a"), false);
  const multi = hostIds.find((moveId) =>
    STAMP_HOST_MOVES[moveId].multiHit && STAMP_HOST_MOVES[moveId].category.startsWith("Damaging")
  );
  assertEquals(multi !== undefined, true);
  assertEquals(eligibleModifierPoolForMove(multi!).includes("4.1-a"), true);
});

Deno.test("accuracy and power stamps never roll on passives", () => {
  const passive = hostIds.find((moveId) => STAMP_HOST_MOVES[moveId].category === "Passive");
  assertEquals(passive !== undefined, true);
  const eligible = eligibleModifierPoolForMove(passive!);
  for (const sectionId of ["2.1-a", "2.2-a", "2.7-a", "3.1-a", "6.3-c", "10.2-a"]) {
    assertEquals(eligible.includes(sectionId), false, sectionId);
  }
});

Deno.test("second-modifier candidates stack with the first", () => {
  for (const moveId of hostIds) {
    for (const firstId of eligibleModifierPoolForMove(moveId)) {
      for (const secondId of eligibleSecondModifiersForMove(moveId, firstId)) {
        assertEquals(canPairModifiers(firstId, secondId), true, `${firstId}+${secondId} on ${moveId}`);
        const first = STAMP_MODIFIER_RULES[firstId];
        const second = STAMP_MODIFIER_RULES[secondId];
        assertEquals(first.slotType === "None" || first.slotType !== second.slotType, true);
      }
    }
  }
  assertEquals(canPairModifiers("1.3-a", "1.1-a"), false, "forbidden combo");
  assertEquals(canPairModifiers("1.1-a", "1.3-a"), false, "forbidden combo, reverse direction");
  assertEquals(canPairModifiers("5.2-a", "5.2-d"), false, "two stat substitutions");
});

Deno.test("two-modifier stamp rolls are compatible across many digests", async () => {
  const hosts = hostIds.filter((_, index) => index % 7 === 0);
  for (let seed = 0; seed < 40; seed++) {
    const secret = Uint8Array.from({ length: 64 }, (_, index) => (index * 31 + seed) & 0xff);
    for (const moveId of hosts) {
      if (eligibleModifierPoolForMove(moveId).length < 2) continue;
      let result;
      try {
        result = await deriveStampResult(secret, "profile-a", `known-${seed}`, 2, 1, EXPECTED_MODIFIER_POOL_HASH, moveId);
      } catch (error) {
        assertEquals((error as Error).message, "insufficient_compatible_modifiers");
        continue;
      }
      const eligible = eligibleModifierPoolForMove(moveId);
      assertEquals(eligible.includes(result.modifierIds[0]), true);
      assertEquals(eligible.includes(result.modifierIds[1]), true);
      assertEquals(canPairModifiers(result.modifierIds[0], result.modifierIds[1]), true);
    }
  }
});

Deno.test("the deployed prepare-stamp handler passes the Known's move to the roller", async () => {
  // Always read the deployed Supabase/functions tree, even when run from the nested copy.
  const functionPrefix = import.meta.url.includes("/Supabase/supabase/functions/_shared/")
    ? "../../../functions/"
    : "../";
  const source = await Deno.readTextFile(new URL(`${functionPrefix}ownership_prepare_stamp/index.ts`, import.meta.url));
  const start = source.indexOf("await deriveStampResult(");
  const call = source.slice(start, source.indexOf(");", start));
  assertEquals(start >= 0, true);
  assertEquals(call.includes("str(known.move_id)"), true);
});

Deno.test("the stamp roller refuses to run without a host move", async () => {
  const secret = new Uint8Array(64);
  let message = "";
  try {
    await deriveStampResult(secret, "profile-a", "known-a", 1, 1, EXPECTED_MODIFIER_POOL_HASH, "");
  } catch (error) {
    message = (error as Error).message;
  }
  assertEquals(message, "stamp_host_move_required");
  assertThrows(() => eligibleModifierPoolForMove("not_a_move"), Error, "unknown_stamp_host_move");
});
