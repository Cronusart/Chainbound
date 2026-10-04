import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { JsonMap, jsonResponse, readJsonBody, requirePost, str, supabaseAdmin } from "../_shared/account_helpers.ts";
import {
  ensureOwnershipRpcCluster,
  fetchChainRamBalance,
  loadOwnershipConfig,
  OwnershipConfigurationError,
  reconcileRamDeltaOnChain,
} from "../_shared/ownership_spine.ts";

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const configuredToken = str(Deno.env.get("CHAINBOUND_OPERATOR_TOKEN"));
  const configuredOperator = str(Deno.env.get("CHAINBOUND_OPERATOR_ID"));
  if (
    !configuredToken || !configuredOperator || str(req.headers.get("x-chainbound-operator-token")) !== configuredToken
  ) {
    return jsonResponse({ success: false, error: "operator_unauthorized" }, 401);
  }
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const caseId = str(body.case_id);
  const decision = str(body.decision).toLowerCase();
  const reason = str(body.reason);
  if (!UUID_RE.test(caseId) || !reason || reason.length > 1000 || !["approve", "reject"].includes(decision)) {
    return jsonResponse({ success: false, error: "invalid_reconciliation_decision" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const { data: caseRow, error: caseError } = await supabase
      .from("ram_reconciliation_cases")
      .select("*")
      .eq("case_id", caseId)
      .maybeSingle();
    if (caseError) throw caseError;
    if (!caseRow) return jsonResponse({ success: false, error: "reconciliation_case_not_found" }, 404);

    if (decision === "reject") {
      const { data, error } = await supabase.rpc("ram_reconciliation_reject", {
        p_case_id: caseId,
        p_reason: reason,
        p_approved_by: configuredOperator,
      });
      if (error) return jsonResponse({ success: false, error: normalizeRpcError(error.message) }, 409);
      return jsonResponse({ success: true, case_id: caseId, ...(data as JsonMap) });
    }

    const config = loadOwnershipConfig();
    if (str(caseRow.cluster) !== config.cluster) {
      return jsonResponse({ success: false, error: "reconciliation_cluster_mismatch" }, 409);
    }
    await ensureOwnershipRpcCluster(config);
    const walletAddress = str(caseRow.wallet_address);
    const [{ data: profile, error: profileError }, chainBalance] = await Promise.all([
      supabase.from("player_profiles").select("ram_balance").eq("player_profile_id", caseRow.player_profile_id)
        .maybeSingle(),
      fetchChainRamBalance({ config, walletAddress }),
    ]);
    if (profileError) throw profileError;
    if (!profile) throw new Error("profile_not_found");
    const profileBalance = Number(profile.ram_balance);
    const auditedProfileBalance = Number(caseRow.profile_balance);
    const auditedChainBalance = Number(caseRow.chain_balance);
    if (profileBalance !== auditedProfileBalance || chainBalance !== auditedChainBalance) {
      return jsonResponse({ success: false, error: "reconciliation_balances_changed_reaudit_required" }, 409);
    }
    const direction = profileBalance > chainBalance ? "credit_chain" : "debit_chain";
    const amount = Math.abs(profileBalance - chainBalance);
    if (!Number.isSafeInteger(amount) || amount <= 0) {
      return jsonResponse({ success: false, error: "reconciliation_delta_not_required" }, 409);
    }

    const { error: approvalError } = await supabase.rpc("ram_reconciliation_approve", {
      p_case_id: caseId,
      p_direction: direction,
      p_amount: amount,
      p_reason: reason,
      p_approved_by: configuredOperator,
    });
    if (approvalError) return jsonResponse({ success: false, error: normalizeRpcError(approvalError.message) }, 409);

    const chain = await reconcileRamDeltaOnChain({ config, walletAddress, caseId, direction, amount });
    if (chain.balanceBefore !== chainBalance || chain.balanceAfter !== profileBalance) {
      throw new Error("reconciliation_receipt_balance_mismatch");
    }
    const { error: submittedError } = await supabase.rpc("ram_reconciliation_mark_submitted", {
      p_case_id: caseId,
      p_signature: chain.signature,
      p_receipt_address: chain.receiptAddress,
    });
    if (submittedError) throw submittedError;
    const { data: settled, error: settlementError } = await supabase.rpc("ram_reconciliation_settle", {
      p_case_id: caseId,
      p_signature: chain.signature,
      p_receipt_address: chain.receiptAddress,
      p_balance_before: chain.balanceBefore,
      p_balance_after: chain.balanceAfter,
    });
    if (settlementError) throw settlementError;
    return jsonResponse({
      success: true,
      case_id: caseId,
      direction,
      amount: String(amount),
      signature: chain.signature,
      receipt_address: chain.receiptAddress,
      recovered: chain.recovered,
      ...(settled as JsonMap),
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    console.error("[ram_operator_reconcile]", error);
    return jsonResponse({ success: false, error: "ram_operator_reconciliation_failed" }, 500);
  }
});

function normalizeRpcError(message: string): string {
  const value = str(message);
  const known = [
    "reconciliation_case_not_found",
    "reconciliation_case_not_approvable",
    "reconciliation_case_not_rejectable",
    "profile_balance_changed_since_audit",
    "ram_ledger_not_frozen",
    "reconciliation_delta_mismatch",
    "reconciliation_approval_conflict",
  ];
  return known.find((code) => value.includes(code)) ?? value ?? "reconciliation_failed";
}
