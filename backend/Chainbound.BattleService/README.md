# Chainbound Battle Service

.NET 8 authority for resumable PvE capture sessions. It links the exact Unity-free sources from `Assets/Scripts/Combat/Core` so client prediction and server resolution share fixed-point rules and RNG.

## Configuration

Required environment variables:

- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `SUPABASE_SERVICE_ROLE_KEY`
- `SOLANA_RPC_ENDPOINT`
- `AUTHORITATIVE_PVE_CAPTURE_ENABLED`

Optional path overrides:

- `GAMEPLAY_MANIFEST_PATH` (default `manifests/gameplay-manifest.json`)
- `CAPTURE_BALANCE_PATH` (default `manifests/capture-balance.json`)

Optional defeat-consequence levers:

- `BATTLE_ABANDONMENT_RAM_PENALTY` (default `5`)
- `BATTLE_DEFEAT_RESPAWN_HP_BP` (default `5000`, or 50% MaxHP)

When a 15-minute resume window expires, the service finalizes the session once as an abandoned PvE loss. That transaction caps RAM at zero, revives KO'd party Chainlings at the configured MaxHP fraction, and closes any pending capture attempt as interrupted. Player-level RAM scaling remains reserved for the later progression pass.

Never ship the service-role key in Unity. The client sends its Supabase bearer token; the service validates it through Supabase Auth and resolves the owned profile before every operation.

## Local verification

```powershell
dotnet build .\backend\Chainbound.BattleService\Chainbound.BattleService.csproj -c Release
dotnet run --project .\tools\Chainbound.CaptureCoreVerification\Chainbound.CaptureCoreVerification.csproj -c Release
```

The checked-in manifests are intentionally non-ready placeholders until the Unity export menu runs. Do not hand-edit their hashes.

See `Docs/Architecture/AuthoritativeTradingTraceCapture.md` for deployment order, balance boards, and the current rollout boundary.
