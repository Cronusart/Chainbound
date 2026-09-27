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
import { buildItemInventoryPayload } from "../_shared/onchain_items.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const spawnId = str(body.spawn_id);
  if (!spawnId) return jsonResponse({ success: false, error: "spawn_id_required" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const idempotencyKey = str(body.idempotency_key) || null;

    const { data, error } = await supabase.rpc("profile_grant_world_pickup_tx", {
      p_profile_id: profile.player_profile_id,
      p_spawn_id: spawnId,
      p_idempotency_key: idempotencyKey,
    });

    if (error) {
      console.error("[profile_grant_world_pickup] rpc", error);
      return jsonResponse(
        { success: false, error: error.message || "world_pickup_grant_failed" },
        400,
      );
    }

    const result = (data ?? {}) as JsonMap;
    if (result.ok === false) {
      return jsonResponse({
        success: false,
        error: str(result.error) || "not_applied",
        ok: false,
      }, 409);
    }

    const { data: profileRow } = await supabase
      .from("player_profiles")
      .select("ram_balance")
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    const payload = await buildItemInventoryPayload(
      supabase,
      profile.player_profile_id,
      str(body.wallet_address) || str(body.wallet_public_key),
      Number(profileRow?.ram_balance) || 0,
    );

    return jsonResponse({
      success: true,
      ok: true,
      already_claimed: result.already_claimed === true,
      player_profile_id: profile.player_profile_id,
      spawn_id: spawnId,
      item_id: result.item_id ?? "",
      quantity: result.quantity ?? 0,
      remaining_quantity: result.remaining_quantity ?? 0,
      ram_balance: payload.ok ? payload.ram_balance : Number(profileRow?.ram_balance) || 0,
      stacks: payload.ok ? payload.stacks : [],
      wallet_public_key: payload.ok ? payload.wallet_public_key : "",
      cluster: payload.ok ? payload.cluster : "devnet",
    });
  } catch (error) {
    console.error("[profile_grant_world_pickup]", error);
    return jsonResponse({ success: false, error: "profile_grant_world_pickup_failed" }, 500);
  }
});
