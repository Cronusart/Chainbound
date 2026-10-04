import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { jsonResponse, requirePost, str, supabaseAdmin } from "../_shared/account_helpers.ts";
import {
  ensureOwnershipRpcCluster,
  fetchChainRamBalance,
  findFinalizedSignatureForAddress,
  loadOwnershipConfig,
} from "../_shared/ownership_spine.ts";
import { advanceBindOperation } from "../_shared/ownership_bind.ts";
import { advancePurchaseSettlement, confirmPurchase, PurchaseOrderRow } from "../_shared/ram_shop.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const expected = str(Deno.env.get("RAM_RECONCILER_SECRET"));
  if (!expected || req.headers.get("x-reconciler-secret") !== expected) {
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
        if (raw.status === "submitted") {
          const signature = raw.payment_signature ||
            await findFinalizedSignatureForAddress(rpcUrlFor(raw.cluster), raw.reference_address);
          if (signature) await confirmPurchase(supabase, raw, signature);
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
    const config = loadOwnershipConfig();
    await ensureOwnershipRpcCluster(config);
    const { data: audits, error: auditClaimError } = await supabase.rpc("ram_ledger_claim_audit_batch", {
      p_worker_id: workerId,
      p_limit: 20,
      p_lease_seconds: 60,
    });
    if (auditClaimError) throw auditClaimError;
    let audited = 0;
    for (const raw of (audits ?? []) as Array<Record<string, unknown>>) {
      try {
        if (str(raw.cluster) !== config.cluster) throw new Error("ledger_audit_cluster_mismatch");
        const chainBalance = await fetchChainRamBalance({
          config,
          walletAddress: str(raw.wallet_address),
        });
        const { error: auditError } = await supabase.rpc("ram_ledger_register_audit", {
          p_player_profile_id: str(raw.player_profile_id),
          p_wallet_address: str(raw.wallet_address),
          p_cluster: str(raw.cluster),
          p_chain_balance: chainBalance,
        });
        if (auditError) throw auditError;
        const { error: releaseAuditLeaseError } = await supabase.from("ram_ledger_accounts").update({
          audit_lease_owner: null,
          audit_lease_expires_at: null,
        }).eq("player_profile_id", raw.player_profile_id).eq("audit_lease_owner", workerId);
        if (releaseAuditLeaseError) throw releaseAuditLeaseError;
        audited++;
      } catch (error) {
        failures.push(`audit:${str(raw.player_profile_id)}:${error instanceof Error ? error.message : "unknown"}`);
        await supabase.from("ram_ledger_accounts").update({
          audit_after: new Date(Date.now() + 30_000).toISOString(),
          audit_lease_owner: null,
          audit_lease_expires_at: null,
        }).eq("player_profile_id", raw.player_profile_id).eq("audit_lease_owner", workerId);
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
      audit_claimed: (audits ?? []).length,
      audited,
      bind_claimed: (binds ?? []).length,
      binds_advanced: bindsAdvanced,
      failures,
    });
  } catch (error) {
    console.error("[ram_shop_reconcile]", error);
    return jsonResponse({ success: false, error: "ram_shop_reconcile_failed" }, 500);
  }
});

function rpcUrlFor(cluster: string): string {
  if (str(Deno.env.get("RAM_SHOP_CLUSTER")) !== cluster) throw new Error("order_cluster_mismatch");
  const url = str(Deno.env.get("CHAINBOUND_SOLANA_RPC_ENDPOINT"));
  if (!url) throw new Error("rpc_missing");
  return url;
}
