import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { KNOWN_POOL, EVENT_KNOWN_GRANT_CATALOG } from "./known_pool.generated.ts";
import { BOSS_ONLY_MOVE_IDS } from "./move_flags.generated.ts";

Deno.test("Known and event catalogs exclude boss-only moves", () => {
  for (const entry of KNOWN_POOL) assertEquals(BOSS_ONLY_MOVE_IDS.has(entry.moveId), false, entry.moveId);
  for (const moveId of Object.keys(EVENT_KNOWN_GRANT_CATALOG)) assertEquals(BOSS_ONLY_MOVE_IDS.has(moveId), false, moveId);
});
