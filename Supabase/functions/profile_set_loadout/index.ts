import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

const ON_CHAIN_STORAGE_SLOTS = 16;
const BASE_MOVE_SLOTS = 4;
const MAX_MOVE_SLOTS = 9;
const REBIRTH_MAX = 5;
const MAX_PASSIVE_MOVES = 2;
const PASSIVE_MOVE_IDS = new Set([
  "turn_start_ap_gain",
  "on_hit_momentum",
  "on_crit_bonus",
  "defensive_reflex",
  "sustain_regen",
  "status_resistance",
  "emergency_recovery",
  "precision_focus",
  "reactive_ping",
  "low_ap_surge",
  "spite_counter",
  "desperation_surge",
  "fee_collector",
  "immutable_ledger",
  "firewall_passive",
  "conductive_counter",
  "extraction_hit",
  "patience_passive",
  "stake_refund",
  "streamlined_flow",
  "automated_pressure",
  "unstable_code",
  "parallel_passive",
  "shattered_armor",
  "double_or_nothing",
  "meme_momentum",
  "gecko_guard",
]);

interface ChainlingRow {
  id: string;
  rebirth_count: number | null;
}

interface MoveItemRow {
  id: string;
  player_profile_id: string | null;
  locked: boolean | null;
  move_id: string | null;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const chainlingId = str(body.chainling_id);
  const moveItemId = str(body.move_item_id);
  const slot = Number(body.slot);

  if (!chainlingId) {
    return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  }

  if (!Number.isInteger(slot) || slot < 0 || slot >= ON_CHAIN_STORAGE_SLOTS) {
    return jsonResponse({ success: false, error: "invalid_loadout_slot" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data: chainling, error: chainlingError } = await supabase
      .from("chainlings")
      .select("id, rebirth_count")
      .eq("id", chainlingId)
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    if (chainlingError) throw chainlingError;
    if (!chainling) {
      return jsonResponse({ success: false, error: "chainling_not_found_or_not_owned" }, 404);
    }

    const maxSlots = resolveMaxSlots((chainling as ChainlingRow).rebirth_count);
    if (slot >= maxSlots) {
      return jsonResponse({
        success: false,
        error: "slot_exceeds_chainling_capacity",
        max_slots: maxSlots,
      }, 400);
    }

    if (!moveItemId) {
      const { error: deleteError } = await supabase
        .from("loadouts")
        .delete()
        .eq("chainling_id", chainlingId)
        .eq("slot", slot);

      if (deleteError) throw deleteError;

      return jsonResponse({
        success: true,
        player_profile_id: profile.player_profile_id,
        chainling_id: chainlingId,
        slot,
        move_item_id: "",
      });
    }

    const { data: moveItem, error: moveError } = await supabase
      .from("move_items")
      .select("id, player_profile_id, locked, move_id")
      .eq("id", moveItemId)
      .maybeSingle();

    if (moveError) throw moveError;
    if (!moveItem) {
      return jsonResponse({ success: false, error: "move_item_not_found" }, 404);
    }

    const move = moveItem as MoveItemRow;
    if (move.player_profile_id !== profile.player_profile_id) {
      return jsonResponse({ success: false, error: "move_item_not_owned" }, 403);
    }

    if (move.locked === true) {
      return jsonResponse({ success: false, error: "move_item_locked" }, 409);
    }

    const capError = await validateLoadoutCaps(
      supabase,
      chainlingId,
      slot,
      moveItemId,
      move,
    );
    if (capError) return capError;

    const { data: existingRows, error: existingError } = await supabase
      .from("loadouts")
      .select("chainling_id, slot")
      .eq("move_item_id", moveItemId);

    if (existingError) throw existingError;

    for (const row of existingRows ?? []) {
      const existing = row as { chainling_id?: string; slot?: number };
      if (existing.chainling_id !== chainlingId || existing.slot !== slot) {
        const { error: removeOldError } = await supabase
          .from("loadouts")
          .delete()
          .eq("chainling_id", existing.chainling_id)
          .eq("slot", existing.slot);

        if (removeOldError) throw removeOldError;
      }
    }

    const { error: upsertError } = await supabase
      .from("loadouts")
      .upsert({
        chainling_id: chainlingId,
        slot,
        move_item_id: moveItemId,
      }, {
        onConflict: "chainling_id,slot",
      });

    if (upsertError) throw upsertError;

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      chainling_id: chainlingId,
      slot,
      move_item_id: moveItemId,
    });
  } catch (error) {
    console.error("[profile_set_loadout]", error);
    return jsonResponse({ success: false, error: "profile_set_loadout_failed" }, 500);
  }
});

async function validateLoadoutCaps(
  supabase: ReturnType<typeof supabaseAdmin>,
  chainlingId: string,
  targetSlot: number,
  targetMoveItemId: string,
  targetMove: MoveItemRow,
): Promise<Response | null> {
  const { data: equippedRows, error } = await supabase
    .from("loadouts")
    .select("slot, move_item_id, move_items(move_id)")
    .eq("chainling_id", chainlingId);

  if (error) throw error;

  let passiveCount = isPassiveMove(targetMove.move_id) ? 1 : 0;
  const targetMoveId = normalizeMoveId(targetMove.move_id);

  for (const row of equippedRows ?? []) {
    const loadout = row as {
      slot?: number;
      move_item_id?: string;
      move_items?: MoveItemRow | MoveItemRow[] | null;
    };

    if (loadout.slot === targetSlot || loadout.move_item_id === targetMoveItemId) {
      continue;
    }

    const move = normalizeJoinedMoveItem(loadout.move_items);
    if (!move) continue;

    const existingMoveId = normalizeMoveId(move.move_id);
    if (targetMoveId && existingMoveId === targetMoveId) {
      return jsonResponse({ success: false, error: "duplicate_move_id" }, 409);
    }

    if (isPassiveMove(move.move_id)) passiveCount++;
  }

  if (passiveCount > MAX_PASSIVE_MOVES) {
    return jsonResponse({
      success: false,
      error: "passive_cap_exceeded",
      max_passives: MAX_PASSIVE_MOVES,
    }, 400);
  }

  return null;
}

function normalizeJoinedMoveItem(value: MoveItemRow | MoveItemRow[] | null | undefined): MoveItemRow | null {
  if (Array.isArray(value)) return value.length > 0 ? value[0] : null;
  return value ?? null;
}

function resolveMaxSlots(rebirthCount: number | null): number {
  const rebirth = Math.min(REBIRTH_MAX, Math.max(0, Math.floor(Number(rebirthCount ?? 0))));
  return Math.min(MAX_MOVE_SLOTS, Math.max(BASE_MOVE_SLOTS, BASE_MOVE_SLOTS + rebirth));
}

function isPassiveMove(moveId: string | null): boolean {
  return PASSIVE_MOVE_IDS.has(normalizeMoveId(moveId));
}

function normalizeMoveId(moveId: string | null): string {
  return String(moveId ?? "").trim().toLowerCase();
}
