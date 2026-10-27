import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { prepareLockedParty, type PvpPartyWire } from "./pvp_party.ts";

Deno.test("PvP party lock rejects boss-only moves", () => {
  const party = {
    teamName: "Boss leak guard",
    chainlings: [{ currentHp: 1, moveIds: ["grind_tax"] }],
  } as unknown as PvpPartyWire;
  assertEquals(prepareLockedParty(party).error, "party_move_boss_only");
});
