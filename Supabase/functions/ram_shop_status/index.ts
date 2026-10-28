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
  orderResponseWithBalanceVersion,
  PurchaseOrderRow,
  RamShopError,
  reconcileQuotedPurchase,
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
    let numericOrderId: number | null = null;
    if (!orderId && signature) {
      const { data: attempt, error: attemptError } = await supabase.from("ram_payment_attempts")
        .select("order_id").eq("signature", signature).eq("status", "verified").maybeSingle();
      if (attemptError) throw attemptError;
      numericOrderId = attempt ? Number(attempt.order_id) : null;
    }
    let query = supabase.from("ram_purchase_orders").select("*").eq("player_profile_id", profile.player_profile_id);
    if (orderId) query = query.eq("order_id", orderId);
    else if (signature) {
      query = numericOrderId === null ? query.eq("payment_signature", signature) : query.eq("id", numericOrderId);
    } else query = query.eq("reference_address", reference);
    const { data, error } = await query.maybeSingle();
    if (error) throw error;
    if (!data) throw new RamShopError("order_not_found", 404);
    let order = data as PurchaseOrderRow;
    if (order.status === "submitted" || order.status === "quoted") {
      return jsonResponse(
        await reconcileQuotedPurchase(
          supabase,
          order,
          findFinalizedSignatureForAddress,
        ),
      );
    }
    if (order.status === "payment_verified" || order.status === "credit_submitted") {
      return jsonResponse(await advancePurchaseSettlement(supabase, order));
    }
    return jsonResponse(await orderResponseWithBalanceVersion(supabase, order));
  } catch (error) {
    const status = error instanceof RamShopError ? error.httpStatus : 500;
    const code = error instanceof RamShopError ? error.code : "ram_shop_status_failed";
    console.error("[ram_shop_status]", code, error);
    return jsonResponse({ success: false, error: code }, status);
  }
});
