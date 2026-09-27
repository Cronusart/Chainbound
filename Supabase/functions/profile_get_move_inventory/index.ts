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
  modifier2_id: string | null;
  locked: boolean | null;
  exported_asset_id: string | null;
}

interface LoadoutRow {
  chainling_id: string;
  slot: number | null;
  move_item_id: string | null;
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
      .select("id, tier, move_id, modifier1_id, modifier2_id, locked, exported_asset_id")
      .eq("player_profile_id", profile.player_profile_id);

    if (moveError) throw moveError;

    const moves = ((moveRows ?? []) as MoveItemRow[]).map((row) => ({
      move_item_id: row.id,
      tier: normalizeTier(row.tier),
      move_id: row.move_id ?? "",
      modifier1_id: row.modifier1_id ?? "",
      modifier2_id: row.modifier2_id ?? "",
      locked: row.locked === true,
      exported_asset_id: row.exported_asset_id ?? "",
    }));

    const { data: chainlingRows, error: chainlingError } = await supabase
      .from("chainlings")
      .select("id")
      .eq("player_profile_id", profile.player_profile_id);

    if (chainlingError) throw chainlingError;

    const chainlingIds = ((chainlingRows ?? []) as Array<{ id: string }>).map((row) => row.id).filter(Boolean);
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
      }));
    }

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
