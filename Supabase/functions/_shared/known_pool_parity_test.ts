import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { EVENT_KNOWN_GRANT_CATALOG, KNOWN_POOL } from "./known_pool.generated.ts";

const repoPrefix = import.meta.url.includes("/Supabase/supabase/functions/_shared/")
  ? "../../../../"
  : "../../../";
const manifestUrl = new URL(`${repoPrefix}backend/Chainbound.BattleService/manifests/gameplay-manifest.json`, import.meta.url);

Deno.test("generated Known catalogs use authoritative element and power", async () => {
  const manifest = JSON.parse(await Deno.readTextFile(manifestUrl));
  const moves = new Map(manifest.moves.map((move: { moveId: string }) => [move.moveId, move]));
  for (const entry of KNOWN_POOL) {
    const move = moves.get(entry.moveId) as { baseType: number; power: number } | undefined;
    assertEquals(Boolean(move), true, `unknown Known pool move ${entry.moveId}`);
    assertEquals({ element: entry.element, basePower: entry.basePower }, { element: move!.baseType, basePower: move!.power });
  }
  for (const [moveId, entry] of Object.entries(EVENT_KNOWN_GRANT_CATALOG)) {
    const move = moves.get(moveId) as { baseType: number; power: number } | undefined;
    assertEquals(Boolean(move), true, `unknown event Known move ${moveId}`);
    assertEquals(entry, { element: move!.baseType, basePower: move!.power });
  }
});
