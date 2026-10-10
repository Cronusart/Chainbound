# Chainbound Battle Service

.NET 8 authority for resumable PvE capture sessions. It links the exact Unity-free sources from `Assets/Scripts/Combat/Core` so client prediction and server resolution share fixed-point rules and RNG.

## Configuration

Required environment variables:

- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `SUPABASE_SERVICE_ROLE_KEY`
- `SOLANA_RPC_ENDPOINT`
- `AUTHORITATIVE_PVE_CAPTURE_ENABLED`
- `BATTLE_REWARD_SERVICE_SECRET` (must match the private Edge function secret)

Optional path overrides:

- `GAMEPLAY_MANIFEST_PATH` (default `manifests/gameplay-manifest.json`)
- `CAPTURE_BALANCE_PATH` (default `manifests/capture-balance.json`)
- `BATTLE_REWARDS_PATH` (default `manifests/battle-rewards.json`)
- `BATTLE_REWARD_SETTLE_URL` (default `${SUPABASE_URL}/functions/v1/battle_reward_settle`)
- `BATTLE_REWARD_TIMEOUT_SECONDS` (default `8`, range `1`-`15`)
- `BATTLE_REWARD_RETRY_COOLDOWN_SECONDS` (default `5`, range `1`-`60`)
- `BATTLE_REWARD_MODE` (optional outer restriction: `off`, `shadow`, or `grant`; unset defaults to pass-through `grant`)

Optional defeat-consequence lever:

- `BATTLE_DEFEAT_RESPAWN_HP_BP` (default `5000`, or 50% MaxHP)

When the 5-minute resume window expires, or the player forfeits, the service finalizes the session once as an abandoned PvE loss. That transaction sets every equipped party Chainling to the respawn MaxHP fraction, closes any pending capture attempt as interrupted, and stamps a zero-credit RAM outcome. PvE defeat, flee, forfeit, and abandonment never debit RAM.

Battle RAM is Postgres-canonical. A terminal action/capture/forfeit transaction stamps the immutable outcome, then the service calls the private `battle_reward_settle` Edge function. Only a server-owned `victory` can credit RAM; the database derives the configured 5/15/25 amount, enforces the 100-per-UTC-day cap, writes one ledger operation keyed by `battle_id`, updates `player_profiles.ram_balance`, and returns the stored receipt plus `balanceAfter`. Wallets and Solana are not involved in battle earning.

For Devnet rollout, the database singleton `battle_reward_settings` is the only grant authority and defaults to `mode='off'`; a credit requires database `mode='grant'`, the Devnet cluster marker, and the profile in the database UUID allowlist. Unset Edge/BattleService mode and Edge allowlist values add no second grant gate. Explicit Edge `off`/`shadow` or an optional Edge UUID allowlist may only restrict the database decision. Missing or invalid database settings produce a durable zero receipt.

`battle_reward_settle` is explicitly configured with `verify_jwt = false` in `Supabase/config.toml`. This is intentional because BattleService may use a modern `sb_secret_` service key, which is sent in `apikey` rather than as a bearer JWT. The function itself requires both that service key and `x-battle-reward-secret` with constant-time comparisons. Never expose either credential to Unity.

Never ship the service-role key in Unity. The client sends its Supabase bearer token; the service validates it through Supabase Auth and resolves the owned profile before every operation.

## Local verification

```powershell
dotnet build .\backend\Chainbound.BattleService\Chainbound.BattleService.csproj -c Release
dotnet run --project .\tools\Chainbound.CaptureCoreVerification\Chainbound.CaptureCoreVerification.csproj -c Release
```

Manifest hashes are SHA-256 over canonical JSON (recursively sorted object keys, compact UTF-8), so whitespace and CRLF/LF differences do not change the authority hash. JSON is also pinned to LF by `.gitattributes`. Generate manifests through the Unity export menu; do not hand-edit their hashes.

See `Docs/Architecture/AuthoritativeTradingTraceCapture.md` for deployment order, balance boards, and the current rollout boundary.
