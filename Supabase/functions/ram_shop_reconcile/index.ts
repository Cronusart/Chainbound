import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { jsonResponse, requirePost, str, supabaseAdmin } from "../_shared/account_helpers.ts";
import { findFinalizedSignatureForAddress } from "../_shared/ownership_spine.ts";
import { advanceBindOperation } from "../_shared/ownership_bind.ts";
import {
  advancePurchaseSettlement,
  PurchaseOrderRow,
  reconcileQuotedPurchase,
  secretsMatch,
} from "../_shared/ram_shop.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const expected = str(Deno.env.get("RAM_RECONCILER_SECRET"));
  if (!(await secretsMatch(req.headers.get("x-reconciler-secret") ?? "", expected))) {
    return jsonResponse({ success: false, error: "unauthorized" }, 401);
  }
  const supabase = supabaseAdmin();
  const workerId = `edge:${crypto.randomUUID()}`;
  try {
    const { data, error } = await supabase.rpc("ram_shop_claim_reconcile_batch", {
      p_worker_id: workerId,
      p_limit: 20,
      p_lease_seconds: 60,
    });
    if (error) throw error;
    let advanced = 0;
    const failures: string[] = [];
    for (const raw of (data ?? []) as PurchaseOrderRow[]) {
      try {
        if (raw.status === "submitted" || raw.status === "quoted") {
          await reconcileQuotedPurchase(supabase, raw, findFinalizedSignatureForAddress);
        } else {
          await advancePurchaseSettlement(supabase, raw);
        }
        advanced++;
      } catch (error) {
        failures.push(`${raw.order_id}:${error instanceof Error ? error.message : "unknown"}`);
        await supabase.from("ram_purchase_orders").update({
          reconcile_after: new Date(Date.now() + 30_000).toISOString(),
          lease_owner: null,
          lease_expires_at: null,
        }).eq("id", raw.id).eq("lease_owner", workerId);
      }
    }
    const { data: binds, error: bindClaimError } = await supabase.rpc("ownership_bind_claim_reconcile_batch", {
      p_worker_id: workerId,
      p_limit: 20,
      p_lease_seconds: 60,
    });
    if (bindClaimError) throw bindClaimError;
    let bindsAdvanced = 0;
    for (const raw of (binds ?? []) as Array<Record<string, unknown>>) {
      try {
        const result = await advanceBindOperation(
          supabase,
          str(raw.player_profile_id),
          raw,
          str(raw.signature),
        );
        if (result.pending === true) {
          const { error: deferError } = await supabase.from("ownership_bind_operations").update({
            reconcile_after: new Date(Date.now() + 30_000).toISOString(),
            lease_owner: null,
            lease_expires_at: null,
          }).eq("operation_id", raw.operation_id).eq("lease_owner", workerId);
          if (deferError) throw deferError;
        }
        bindsAdvanced++;
      } catch (error) {
        failures.push(`bind:${str(raw.operation_id)}:${error instanceof Error ? error.message : "unknown"}`);
        await supabase.from("ownership_bind_operations").update({
          reconcile_after: new Date(Date.now() + 30_000).toISOString(),
          lease_owner: null,
          lease_expires_at: null,
        }).eq("operation_id", raw.operation_id).eq("lease_owner", workerId);
      }
    }
    return jsonResponse({
      success: true,
      claimed: (data ?? []).length,
      advanced,
      audit_claimed: 0,
      audited: 0,
      bind_claimed: (binds ?? []).length,
      binds_advanced: bindsAdvanced,
      failures,
    });
  } catch (error) {
    console.error("[ram_shop_reconcile]", error);
    return jsonResponse({ success: false, error: "ram_shop_reconcile_failed" }, 500);
  }
});
