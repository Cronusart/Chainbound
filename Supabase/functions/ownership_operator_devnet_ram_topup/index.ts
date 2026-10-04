import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { jsonResponse, requirePost } from "../_shared/account_helpers.ts";

// The one-off receipt-less Devnet mirror repair predates the unified ledger.
// Keep the route fail-closed so old deployed clients receive an explicit
// terminal response instead of discovering a missing function or mutating RAM.
serve((req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  return jsonResponse({
    success: false,
    error: "legacy_operator_topup_retired_use_ram_operator_reconcile",
  }, 410);
});
