import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { JsonMap, str } from "./account_helpers.ts";
import { normalizePublicKey, resolveVerifiedWalletForProfile } from "./onchain_cosmetics.ts";

export interface BagStackRecord {
  item_id: string;
  quantity: number;
  core_asset_id?: string;
  category?: string;
  display_name?: string;
  ownership_kind?: string;
}

export async function loadProfileItemStacks(
  supabase: SupabaseClient,
  playerProfileId: string,
): Promise<BagStackRecord[]> {
  const { data, error } = await supabase
    .from("chainbound_profile_item_stacks")
    .select("item_id, quantity, chainbound_item_catalog(display_name, category, ownership_kind)")
    .eq("player_profile_id", playerProfileId)
    .gt("quantity", 0);

  if (error) throw new Error(`profile_item_stacks_failed:${error.message}`);

  const rows: BagStackRecord[] = [];
  for (const row of data ?? []) {
    const catalog = (row as JsonMap).chainbound_item_catalog as JsonMap | null;
    rows.push({
      item_id: str(row.item_id),
      quantity: Number(row.quantity) || 0,
      category: str(catalog?.category),
      display_name: str(catalog?.display_name),
      ownership_kind: str(catalog?.ownership_kind) || "profile_stack",
    });
  }
  return rows;
}

export async function loadCachedCoreItemStacks(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletPublicKey: string,
  cluster: string,
): Promise<BagStackRecord[]> {
  const { data, error } = await supabase
    .from("chainbound_core_item_ownership_cache")
    .select("item_id, asset_id, core_kind, chainbound_item_catalog(display_name, category, ownership_kind)")
    .eq("player_profile_id", playerProfileId)
    .eq("wallet_public_key", walletPublicKey)
    .eq("cluster", cluster)
    .eq("verification_status", "verified");

  if (error) throw new Error(`core_item_cache_failed:${error.message}`);

  const rows: BagStackRecord[] = [];
  for (const row of data ?? []) {
    const catalog = (row as JsonMap).chainbound_item_catalog as JsonMap | null;
    rows.push({
      item_id: str(row.item_id),
      quantity: 1,
      core_asset_id: str(row.asset_id),
      category: str(catalog?.category),
      display_name: str(catalog?.display_name),
      ownership_kind: "core_collectible",
    });
  }
  return rows;
}

export async function buildItemInventoryPayload(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletHint: string,
  ramBalance: number,
) {
  const cluster = str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || "devnet";
  const profileStacks = await loadProfileItemStacks(supabase, playerProfileId);

  const wallet = await resolveVerifiedWalletForProfile(supabase, playerProfileId, walletHint);
  let coreStacks: BagStackRecord[] = [];
  let walletPublicKey = "";
  if (wallet.ok && wallet.wallet_public_key) {
    walletPublicKey = wallet.wallet_public_key;
    coreStacks = await loadCachedCoreItemStacks(
      supabase,
      playerProfileId,
      walletPublicKey,
      cluster,
    );
  }

  return {
    ok: true as const,
    wallet_public_key: walletPublicKey,
    cluster,
    ram_balance: ramBalance,
    stacks: [...profileStacks, ...coreStacks],
  };
}

export async function prepareCoreItemConsume(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletHint: string,
  itemId: string,
  assetId: string,
  reason: string,
) {
  const wallet = await resolveVerifiedWalletForProfile(supabase, playerProfileId, walletHint);
  if (!wallet.ok || !wallet.wallet_public_key) {
    return { ok: false as const, error: wallet.error ?? "wallet_verification_failed", status: wallet.status ?? 400 };
  }

  const normalizedItemId = str(itemId);
  const normalizedAssetId = normalizePublicKey(assetId);
  if (!normalizedItemId) return { ok: false as const, error: "item_id_required", status: 400 };
  if (!normalizedAssetId) return { ok: false as const, error: "asset_id_invalid", status: 400 };

  const cluster = str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || "devnet";

  const { data: owned, error: ownedError } = await supabase
    .from("chainbound_core_item_ownership_cache")
    .select("id, item_id, asset_id")
    .eq("player_profile_id", playerProfileId)
    .eq("wallet_public_key", wallet.wallet_public_key)
    .eq("cluster", cluster)
    .eq("asset_id", normalizedAssetId)
    .eq("item_id", normalizedItemId)
    .eq("verification_status", "verified")
    .maybeSingle();

  if (ownedError) throw new Error(`core_item_ownership_lookup_failed:${ownedError.message}`);
  if (!owned) {
    return { ok: false as const, error: "core_item_not_owned_or_unverified", status: 403 };
  }

  // Unsigned Core burn tx construction is deferred to program/IDL wiring.
  // Intent row still registers the consume so Unity can exercise prepare → sign → finalize.
  const actionSummary =
    `Burn Core item ${normalizedItemId} (${normalizedAssetId}) on ${cluster}. ` +
    `Fees/program shown at wallet sign time when unsigned_tx_base64 is populated.`;

  const { data: intent, error: intentError } = await supabase
    .from("chainbound_core_item_consume_intents")
    .insert({
      player_profile_id: playerProfileId,
      wallet_public_key: wallet.wallet_public_key,
      cluster,
      item_id: normalizedItemId,
      asset_id: normalizedAssetId,
      reason: str(reason),
      status: "prepared",
      unsigned_tx_base64: null,
      action_summary: actionSummary,
      estimated_fee_lamports: 0,
    })
    .select("consume_intent_id")
    .single();

  if (intentError) throw new Error(`consume_intent_insert_failed:${intentError.message}`);

  return {
    ok: true as const,
    consume_intent_id: str(intent.consume_intent_id),
    item_id: normalizedItemId,
    asset_id: normalizedAssetId,
    unsigned_tx_base64: "",
    cluster,
    action_summary: actionSummary,
    estimated_fee_lamports: 0,
  };
}

export async function finalizeCoreItemConsume(
  supabase: SupabaseClient,
  playerProfileId: string,
  consumeIntentId: string,
  txSignature: string,
) {
  const intentId = str(consumeIntentId);
  if (!intentId) return { ok: false as const, error: "consume_intent_id_required", status: 400 };

  const { data: intent, error } = await supabase
    .from("chainbound_core_item_consume_intents")
    .select("*")
    .eq("consume_intent_id", intentId)
    .eq("player_profile_id", playerProfileId)
    .maybeSingle();

  if (error) throw new Error(`consume_intent_lookup_failed:${error.message}`);
  if (!intent) return { ok: false as const, error: "consume_intent_not_found", status: 404 };
  if (str(intent.status) === "finalized") {
    return {
      ok: true as const,
      consume_intent_id: intentId,
      tx_signature: str(intent.tx_signature) || str(txSignature),
    };
  }

  const cluster = str(intent.cluster) || "devnet";
  const assetId = str(intent.asset_id);
  const wallet = str(intent.wallet_public_key);

  // Mark ownership cache consumed / remove row. Full on-chain burn proof lands with tx builder.
  await supabase
    .from("chainbound_core_item_ownership_cache")
    .delete()
    .eq("player_profile_id", playerProfileId)
    .eq("wallet_public_key", wallet)
    .eq("cluster", cluster)
    .eq("asset_id", assetId);

  const { error: updateError } = await supabase
    .from("chainbound_core_item_consume_intents")
    .update({
      status: "finalized",
      tx_signature: str(txSignature) || "deferred_no_tx",
      finalized_at: new Date().toISOString(),
    })
    .eq("consume_intent_id", intentId);

  if (updateError) throw new Error(`consume_intent_finalize_failed:${updateError.message}`);

  return {
    ok: true as const,
    consume_intent_id: intentId,
    tx_signature: str(txSignature) || "deferred_no_tx",
  };
}
