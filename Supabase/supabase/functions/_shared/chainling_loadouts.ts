import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

interface LoadoutRow {
  chainling_id: string;
  slot: number | null;
  move_item_id: string | null;
}

interface MoveItemRow {
  id: string;
  move_id: string | null;
  modifier1_id: string | null;
  modifier2_id: string | null;
}

export interface ChainlingLoadoutPayload {
  move_ids: string[];
  modifier1_section_ids: string[];
  modifier2_section_ids: string[];
}

export async function loadChainlingLoadouts(
  supabase: SupabaseClient,
  chainlingIds: string[],
): Promise<Map<string, ChainlingLoadoutPayload>> {
  const normalizedIds = Array.from(new Set(chainlingIds.filter((id) => typeof id === "string" && id.length > 0)));
  const result = new Map<string, ChainlingLoadoutPayload>();
  for (const chainlingId of normalizedIds) result.set(chainlingId, emptyLoadout());

  if (normalizedIds.length === 0) return result;

  const { data: loadoutData, error: loadoutError } = await supabase
    .from("loadouts")
    .select("chainling_id, slot, move_item_id")
    .in("chainling_id", normalizedIds)
    .order("chainling_id", { ascending: true })
    .order("slot", { ascending: true });

  if (loadoutError) throw loadoutError;

  const loadoutRows = (loadoutData ?? []) as LoadoutRow[];
  const moveItemIds = Array.from(
    new Set(loadoutRows.map((row) => row.move_item_id ?? "").filter((id) => id.length > 0)),
  );

  const moveItems = new Map<string, MoveItemRow>();
  if (moveItemIds.length > 0) {
    const { data: moveItemData, error: moveItemError } = await supabase
      .from("move_items")
      .select("id, move_id, modifier1_id, modifier2_id")
      .in("id", moveItemIds);

    if (moveItemError) throw moveItemError;
    for (const row of (moveItemData ?? []) as MoveItemRow[]) moveItems.set(row.id, row);
  }

  for (const loadoutRow of loadoutRows) {
    const slot = loadoutRow.slot ?? -1;
    const moveItem = loadoutRow.move_item_id ? moveItems.get(loadoutRow.move_item_id) : undefined;
    if (slot < 0 || slot >= 16 || !moveItem?.move_id) continue;

    const loadout = result.get(loadoutRow.chainling_id) ?? emptyLoadout();
    ensureSlotCapacity(loadout, slot + 1);
    loadout.move_ids[slot] = moveItem.move_id;
    loadout.modifier1_section_ids[slot] = moveItem.modifier1_id ?? "";
    loadout.modifier2_section_ids[slot] = moveItem.modifier2_id ?? "";
    result.set(loadoutRow.chainling_id, loadout);
  }

  return result;
}

function emptyLoadout(): ChainlingLoadoutPayload {
  return {
    move_ids: [],
    modifier1_section_ids: [],
    modifier2_section_ids: [],
  };
}

function ensureSlotCapacity(loadout: ChainlingLoadoutPayload, size: number): void {
  while (loadout.move_ids.length < size) loadout.move_ids.push("");
  while (loadout.modifier1_section_ids.length < size) loadout.modifier1_section_ids.push("");
  while (loadout.modifier2_section_ids.length < size) loadout.modifier2_section_ids.push("");
}
