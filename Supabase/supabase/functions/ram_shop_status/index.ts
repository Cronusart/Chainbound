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
import { findFinalizedSignatureForAddress } from "../_shared/ownership_spine.ts";
import {
  advancePurchaseSettlement,
  confirmPurchase,
  orderResponse,
  PurchaseOrderRow,
  RamShopError,
} from "../_shared/ram_shop.ts";

const ALLOWED_KEYS = new Set(["order_id", "signature", "reference"]);

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);
  if (Object.keys(body).some((key) => !ALLOWED_KEYS.has(key))) {
    return jsonResponse({ success: false, error: "unexpected_status_field" }, 400);
  }
  const orderId = str(body.order_id);
  const signature = str(body.signature);
  const reference = str(body.reference);
  if (!orderId && !signature && !reference) {
    return jsonResponse({ success: false, error: "order_lookup_required" }, 400);
  }
  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, {});
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    let query = supabase.from("ram_purchase_orders").select("*").eq("player_profile_id", profile.player_profile_id);
    if (orderId) query = query.eq("order_id", orderId);
    else if (signature) query = query.eq("payment_signature", signature);
    else query = query.eq("reference_address", reference);
    const { data, error } = await query.maybeSingle();
    if (error) throw error;
    if (!data) throw new RamShopError("order_not_found", 404);
    let order = data as PurchaseOrderRow;
    if (order.status === "submitted" || order.status === "quoted") {
      const recovered = order.payment_signature ||
        await findFinalizedSignatureForAddress(rpcUrlFor(order.cluster), order.reference_address);
      if (recovered) return jsonResponse(await confirmPurchase(supabase, order, recovered));
      return jsonResponse(orderResponse(order));
    }
    if (order.status === "payment_verified" || order.status === "credit_submitted") {
      return jsonResponse(await advancePurchaseSettlement(supabase, order));
    }
    return jsonResponse(orderResponse(order));
  } catch (error) {
    const status = error instanceof RamShopError ? error.httpStatus : 500;
    const code = error instanceof RamShopError ? error.code : "ram_shop_status_failed";
    console.error("[ram_shop_status]", code, error);
    return jsonResponse({ success: false, error: code }, status);
  }
});

function rpcUrlFor(cluster: string): string {
  const configuredCluster = str(Deno.env.get("RAM_SHOP_CLUSTER"));
  const rpcUrl = str(Deno.env.get("CHAINBOUND_SOLANA_RPC_ENDPOINT"));
  if (!rpcUrl || configuredCluster !== cluster) throw new RamShopError("order_cluster_mismatch", 503);
  return rpcUrl;
}
