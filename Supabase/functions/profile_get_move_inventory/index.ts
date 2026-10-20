import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

interface MoveItemRow {
  id: string;
  tier: string | number | null;
  move_id: string | null;
  modifier1_id: string | null;
  modifier1_effect_value: number | string | null;
  modifier1_effect_value_locked: boolean | null;
  modifier2_id: string | null;
  modifier2_effect_value: number | string | null;
  modifier2_effect_value_locked: boolean | null;
  locked: boolean | null;
  exported_asset_id: string | null;
  ownership_known_id: string | null;
  stamp_operation_id: string | null;
  bind_operation_id: string | null;
  bind_sealed_at: string | null;
}

interface LoadoutRow {
  chainling_id: string;
  slot: number | null;
  move_item_id: string | null;
}

interface ChainlingRow {
  id: string;
  display_name: string | null;
  species_id: string | null;
}

interface KnownRow {
  known_id: string;
  acquisition: string | null;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data: moveRows, error: moveError } = await supabase
      .from("move_items")
      .select(
        "id, tier, move_id, modifier1_id, modifier1_effect_value, modifier1_effect_value_locked, modifier2_id, modifier2_effect_value, modifier2_effect_value_locked, locked, exported_asset_id, ownership_known_id, stamp_operation_id, bind_operation_id, bind_sealed_at",
      )
      .eq("player_profile_id", profile.player_profile_id);

    if (moveError) throw moveError;

    const { data: chainlingRows, error: chainlingError } = await supabase
      .from("chainlings")
      .select("id, display_name, species_id")
      .eq("player_profile_id", profile.player_profile_id);

    if (chainlingError) throw chainlingError;

    const chainlings = (chainlingRows ?? []) as ChainlingRow[];
    const chainlingIds = chainlings.map((row) => row.id).filter(Boolean);
    const chainlingNames = new Map(
      chainlings.map((row) => [row.id, row.display_name?.trim() || row.species_id?.trim() || "Chainling"]),
    );
    let loadouts: Record<string, unknown>[] = [];

    if (chainlingIds.length > 0) {
      const { data: loadoutRows, error: loadoutError } = await supabase
        .from("loadouts")
        .select("chainling_id, slot, move_item_id")
        .in("chainling_id", chainlingIds)
        .order("chainling_id", { ascending: true })
        .order("slot", { ascending: true });

      if (loadoutError) throw loadoutError;

      loadouts = ((loadoutRows ?? []) as LoadoutRow[]).map((row) => ({
        chainling_id: row.chainling_id,
        slot: row.slot ?? -1,
        move_item_id: row.move_item_id ?? "",
        chainling_name: chainlingNames.get(row.chainling_id) ?? "Chainling",
      }));
    }

    const equippedByMoveItem = new Map<string, { chainlingId: string; slot: number; chainlingName: string }>();
    for (const loadout of loadouts) {
      const moveItemId = String(loadout.move_item_id ?? "");
      if (!moveItemId) continue;
      equippedByMoveItem.set(moveItemId, {
        chainlingId: String(loadout.chainling_id ?? ""),
        slot: Number(loadout.slot ?? -1),
        chainlingName: String(loadout.chainling_name ?? "Chainling"),
      });
    }

    const knownIds = ((moveRows ?? []) as MoveItemRow[])
      .map((row) => row.ownership_known_id ?? "")
      .filter(Boolean);
    const acquisitionByKnownId = new Map<string, string>();
    if (knownIds.length > 0) {
      const { data: knownRows, error: knownError } = await supabase
        .from("ownership_known_moves")
        .select("known_id, acquisition")
        .in("known_id", knownIds);
      if (knownError) throw knownError;
      for (const known of (knownRows ?? []) as KnownRow[]) {
        acquisitionByKnownId.set(known.known_id, known.acquisition ?? "");
      }
    }

    const moves = ((moveRows ?? []) as MoveItemRow[]).map((row) => {
      const equipped = equippedByMoveItem.get(row.id);
      return {
        move_item_id: row.id,
        tier: normalizeTier(row.tier),
        move_id: row.move_id ?? "",
        modifier1_id: row.modifier1_id ?? "",
        modifier1_effect_value: Number(row.modifier1_effect_value ?? 0),
        modifier1_effect_value_locked: row.modifier1_effect_value_locked === true,
        modifier2_id: row.modifier2_id ?? "",
        modifier2_effect_value: Number(row.modifier2_effect_value ?? 0),
        modifier2_effect_value_locked: row.modifier2_effect_value_locked === true,
        // Kept as the old field for deployed clients. It always means PvP escrow.
        locked: row.locked === true,
        escrow_locked: row.locked === true,
        bind_sealed: Boolean(row.bind_sealed_at),
        bind_operation_id: row.bind_operation_id ?? "",
        known_instance_id: row.ownership_known_id ?? "",
        stamp_operation_id: row.stamp_operation_id ?? "",
        acquisition: acquisitionByKnownId.get(row.ownership_known_id ?? "") ?? "",
        exported_asset_id: row.exported_asset_id ?? "",
        equipped_chainling_id: equipped?.chainlingId ?? "",
        equipped_slot: equipped?.slot ?? -1,
        equipped_chainling_name: equipped?.chainlingName ?? "",
      };
    });

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      moves,
      loadouts,
    });
  } catch (error) {
    console.error("[profile_get_move_inventory]", error);
    return jsonResponse({ success: false, error: "profile_get_move_inventory_failed" }, 500);
  }
});

function normalizeTier(value: string | number | null): string {
  if (typeof value === "number") {
    return value === 0 ? "known" : "stamped";
  }

  const normalized = typeof value === "string" ? value.trim().toLowerCase() : "";
  if (normalized === "known") return "known";
  if (normalized === "stamped") return "stamped";
  // One-way storage migration adapter. Historical values never escape this boundary.
  if (normalized === "ordinal") return "known";
  if (normalized === "inscription" || normalized === "rune") return "stamped";
  return "known";
}
