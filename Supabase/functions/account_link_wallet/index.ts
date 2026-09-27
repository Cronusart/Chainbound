import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  jsonResponse,
  requirePost,
} from "../_shared/account_helpers.ts";

/**
 * Retired fail-closed endpoint.
 *
 * This route historically accepted a deterministic client-authored message and
 * merely recorded whether a signature string was present. Presence is not proof
 * of wallet ownership and permits replay, so production clients must use:
 *
 *   wallet_auth_challenge -> wallet signature -> wallet_connect_verified
 *
 * Keep the function deployed temporarily to give older builds a deterministic,
 * non-mutating upgrade response instead of leaving an unsafe mutation surface.
 */
serve((req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  return jsonResponse(
    {
      success: false,
      error: "legacy_wallet_link_disabled",
      message: "Update Chainbound to link a wallet with a verified one-time challenge.",
    },
    410,
  );
});
