import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { MOVE_AP_COST, MOVE_SPENDS_REMAINING_AP } from "./move_ap.ts";

const repoPrefix = import.meta.url.includes("/Supabase/supabase/functions/_shared/")
  ? "../../../../"
  : "../../../";
const manifestUrl = new URL(`${repoPrefix}backend/Chainbound.BattleService/manifests/gameplay-manifest.json`, import.meta.url);

Deno.test("generated move AP table matches the authoritative manifest", async () => {
  const bytes = await Deno.readFile(manifestUrl);
  const manifest = JSON.parse(new TextDecoder().decode(bytes));
  const expected = Object.fromEntries(manifest.moves.map((move: { moveId: string; apCost: number }) => [move.moveId, move.apCost]));
  assertEquals(MOVE_AP_COST, expected);
  assertEquals(
    [...MOVE_SPENDS_REMAINING_AP].sort(),
    manifest.moves.filter((move: { spendsRemainingAp: boolean }) => move.spendsRemainingAp)
      .map((move: { moveId: string }) => move.moveId).sort(),
  );

  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
  const hash = [...digest].map((value) => value.toString(16).padStart(2, "0")).join("");
  const generated = await Deno.readTextFile(new URL("./move_ap.ts", import.meta.url));
  assertEquals(generated.split("\n", 1)[0].includes(`manifestHash=${hash}`), true);
});
