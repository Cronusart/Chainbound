import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { isProgressionFlagKeyValid, PROGRESSION_FLAG_KEY_MAX_LENGTH } from "./progression_flag_key.ts";

Deno.test("progression flag keys accept every key the Unity client writes", () => {
  const keys = [
    // Legacy undotted keys.
    "intro_cinematic_complete",
    "mart_departed_loomville",
    // Coffee courier hunt and NPC interaction profiles.
    "world.coffee.toby_delivery_accepted",
    "world.coffee.zeb_departed",
    "world.coffee.zeb_talked",
    "world.coffee.reynard_brewed",
    "world.coffee.cold_beer_granted",
    "world.coffee.holding_coffee_beans",
    "world.coffee.holding_cold_beer",
    "world.coffee.holding_tobys_delivery",
    "world.coffee.cofi_lootwak.defeated",
    "world.coffee.cofi_lootwak.player_lost",
    "world.fisher_ape.crayon_returned",
    "world.loomville_mart.open",
    "world.sanctuary.unlocked",
    "world.player.menu",
    "world.player.chainling_pc",
    "world.obstacle.link1.canopy_block",
    // World pickups and starter orbs.
    "world.pickup.link1.backpack_01",
    "world.pickup.link1.backpack_02",
    "world.pickup.jungle.backpack_01",
    "world.pickup.academy_interior.lucky_crayon",
    "world.pickup.loom_lab.orb",
    "world.pickup.loom_lab.dumpster_orb",
    "world.pickup.loom_lab.starter_orb.validape",
    "world.pickup.loomville_mart.open_crate_orb",
    "world.pickup.mart.shelf_orbs_left",
    // Rail guardrails built from GameObject names.
    "intro.guardrail.house",
    "intro.guardrail.playerhouse_door",
    "intro.guardrail.Intro_Collider1",
    "intro.guardrail.TransitionTrigger",
    "intro.guardrail.SeqTrigger_LoomvilleExitMart",
  ];
  for (const key of keys) assertEquals(isProgressionFlagKeyValid(key), true, key);
});

Deno.test("progression flag keys reject malformed dots, casing, and other glyphs", () => {
  const keys = [
    "",
    ".",
    ".world.coffee",
    "world.coffee.",
    "world..coffee",
    "World.coffee.zeb_talked",
    "_world.coffee",
    "1world.coffee",
    "world.1coffee",
    "world._coffee",
    "world.coffee-zeb",
    "world.coffee zeb",
    "world.coffee.{Starter}",
    "world/coffee",
    " world.coffee",
    "world.coffee\n",
    "world.café",
  ];
  for (const key of keys) assertEquals(isProgressionFlagKeyValid(key), false, JSON.stringify(key));
});

Deno.test("progression flag keys cap total length", () => {
  const atLimit = "world." + "a".repeat(PROGRESSION_FLAG_KEY_MAX_LENGTH - "world.".length);
  assertEquals(atLimit.length, PROGRESSION_FLAG_KEY_MAX_LENGTH);
  assertEquals(isProgressionFlagKeyValid(atLimit), true);
  assertEquals(isProgressionFlagKeyValid(atLimit + "a"), false);
});
