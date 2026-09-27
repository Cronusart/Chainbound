# Server-side Helius DAS Proxy Design (Devnet → later Mainnet)

Status: **DEPLOYED AND VERIFIED ON DEVNET; server secret set (value redacted).** Do not put
Helius API keys in Unity `SolanaConfig` or any shipping client asset.

Last updated: 2026-09-19

## Why

- Metaplex Core indexed reads work best on a DAS-enabled RPC.
- Public `https://api.devnet.solana.com` is fine for credential-free bring-up and can be rate-limited or incomplete under load.
- A free Helius plan is appropriate **server-side**; the client must never embed the key.

## Trust boundary

```
Unity (anon JWT only)
  → Supabase Edge Function (service role / secrets vault)
      → Helius DAS HTTPS (API key from Supabase secrets)
          → Devnet (or Mainnet later)
```

Unity keeps:

- `supabaseUrl` + `supabaseAnonKey` only
- Public Solana RPC optional for non-DAS calls
- `coreMovesCollectionId` + expected authority as **trust pins** (not secrets)

Supabase secret (final name):

- `HELIUS_DAS_URL` — full `https://devnet.helius-rpc.com/?api-key=<key>` endpoint
- Never expose these to `anon` via client-readable config tables

## Proposed Edge Function

**Final name:** `ownership_das_proxy`

**Auth:** Same as other ownership functions — valid user JWT; resolve `player_profile_id` from auth mapping (ignore/ spoof-reject body profile id).

**Methods allowed (allowlist):**

| JSON-RPC method | Purpose |
|-----------------|--------|
| `getAsset` | Single asset / collection verify |
| `getAssetsByGroup` | Collection inventory page |
| `getAssetsByOwner` | Wallet-owned assets; `ownerAddress` is bound to the authenticated profile's active wallet |

Reject all other methods (no `sendTransaction`, no admin RPCs).

**Request body (client):**

```json
{
  "method": "getAssetsByGroup",
  "params": {
    "groupKey": "collection",
    "groupValue": "<must equal trusted Moves collection or allowlisted collections>",
    "page": 1,
    "limit": 20
  }
}
```

**Server checks:**

1. JWT → profile (same helper as `ownership_roll_known`).
2. Method ∈ allowlist.
3. For collection queries: `groupValue` ∈ `{ coreMovesCollectionId, …allowlisted Devnet collections }`.
4. Clamp `limit` to 1–50 and `page` to 1–1000.
5. Forward to Helius; return redacted JSON (strip provider headers).

**Responses:** Pass through DAS `result` / map provider errors to `502 das_upstream` without leaking the API key.

## Unity usage

- Optional `OwnershipDasClient` calling `ownership_das_proxy` with anon JWT.
- Editor menu can keep calling public Devnet directly for credential-free probes.
- Production inventory UI prefers Edge proxy once deployed.

## Rollout status

1. [x] Add `HELIUS_DAS_URL` through the Supabase secret store — name-only verification completed
   2026-09-19; value never read or pasted into chat/Codex.
2. [x] Implement + deploy `ownership_das_proxy` with allowlist + JWT (version 2, ACTIVE,
   `verify_jwt=true`).
3. [x] Complete authenticated smokes: no-JWT → 401; getAsset → 200 `MplCoreCollection`;
   getAssetsByGroup → 200 with limit clamped to 50; disallowed method → 400; wrong collection → 400.
   Redacted evidence: `Logs/ownership-das-proxy-auth-smoke-20260919T213308Z.log`.
4. Point Editor/optional runtime client at the proxy behind a define or config flag defaulting to public Devnet until GO.
5. Mainnet: new secret + allowlist; do not reuse Devnet key material in client builds.

## Explicit non-goals

- Replacing public Devnet in `SolanaConfig` with a Helius URL that contains a key
- Client-side DAS for mint/Stamp authority
- Broad RPC proxy (transactions stay on dedicated ownership Edge Functions)
