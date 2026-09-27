# Authoritative Trading-Trace Capture

## Runtime shape

The capture minigame is split into three layers:

- `Chainbound.Combat.Core` contains deterministic fixed-point capture resolution, RNG, simulation state, snapshots, outcomes, and service contracts. It has no Unity dependency and is linked directly into the .NET 8 authority.
- `Chainbound.Combat.Runtime` owns Unity input, prediction, rendering, haptics, GameServices adapters, item display fallbacks, encounter routing, and battle presentation.
- `backend/Chainbound.BattleService` owns access-token validation, profile/loadout lookup, session versions, target truth, Orb consumption, capture outcome, grants, checkpoints, and Solana-calibrated real-time expiry. Authoritative player-party resolution remains part of the combat-resolver rollout boundary below.

Production clients predict the same 30 Hz integer simulation for responsiveness. Only the service result is committed. The WebSocket returns authoritative corrections at 10 Hz. Disconnecting retains the last input for 250 ms and then releases it; the capture deadline continues.

`TimeOfDayManager` remains presentation-only. Capture boosters and resumable battle deadlines use server time calibrated from finalized Solana block time, with monotonic extrapolation limited to ten minutes. When that authority is stale, the service refuses new expiry-sensitive operations rather than guessing.

An unrecovered 15-minute battle expiry is committed once as an abandoned PvE loss. The same service-only transaction interrupts a pending capture attempt without refunding its Orb, caps the RAM deduction at the available balance, and revives persisted KO'd party Chainlings. The deployment levers are `BATTLE_ABANDONMENT_RAM_PENALTY` (default 5) and `BATTLE_DEFEAT_RESPAWN_HP_BP` (default 5000, or 50% MaxHP); the latter uses the same canonical species/IV/progression MaxHP derivation as passive recovery.

## Balance lever boards

Default assets are created under `Assets/Data/Combat/Capture` after the editor imports the capture tooling, or explicitly through:

`Tools > Chainbound > Capture > Ensure Default Lever Boards`

The boards are:

- `CaptureBalanceConfig.asset`: tick rate, standard timer, band size, Lock/Escape rates, low-HP threshold and curve, HP/IV/Alertness difficulty, band physics, trace physics, spikes, and the ten-minute technical maximum. Full-HP levers on this asset were raised for a 2026-09-04 playtest; Core test defaults are unchanged. Composition rules and the intended species/trait/IV stack: [CAPTURE_DIFFICULTY_COMPOSITION_v1.md](../Specs/Combat/CAPTURE_DIFFICULTY_COMPOSITION_v1.md).
- `CaptureProfileCatalog.asset`: Calm, Standard, Volatile, and Extreme profile motion/timer/spike multipliers. Extreme starts with a 65% standard timer after deterministic bot calibration. Local practice picks a profile from encounter `baseCaptureRate` buckets, not from mutated/entangled/variant.
- `CapturePresentationConfig.asset`: candlestick chart look, dashed price/band-edge overlay, grid/band colors, optional trading-grid art, haptic cooldowns, flashes, correction smoothing, and tutorial/outcome presentation.
- `AuthoritativeCombatServiceConfig.asset`: service URL, gameplay manifest hash, request timeout, editor-practice allowance, and the production rollout flag.

Server-side target overrides remain in `capture_balance_profiles`, `capture_profile_assignments`, and `capture_live_events`. Assignment precedence is highest specificity and then priority; unresolved ties are rejected. Active events apply in ascending priority, can replace the profile, and then add typed deltas. A capture attempt hashes its resolved profile, assignment, and event version stamps so live edits cannot mutate an in-progress attempt.

Use `Tools > Chainbound > Combat > Capture Balance Simulator` for repeatable Monte Carlo comparisons. The tool runs the same Core simulation with configurable HP, non-HP IVs, Alertness, Orb, profile, and a reaction-limited bot. With the initial seed and competence model, the current calibration is approximately:

- Standard low-HP baseline: 60–75% Lock.
- Extreme low-HP baseline: 5–15% Lock.
- Extreme with the Scanner V + Wideband two-slot hunter loadout: 25–40% Lock.
- Full-HP, perfect-IV Extreme combinations remain intentionally below that range; the content rules allow these rare traits to stack.

Timeout is a failed attempt: the Orb is consumed and battle resumes with the targeted Chainling still in the fight. An empty lock meter is the flee outcome (no KO); other living wilds stay. Alertness is not used as a retry lever after a timeout.

## Content and inventory

Capture bonuses are typed basis-point vectors: Scan, Stability, Band Width, Lock Rate, and Capture Time. Head, Body, Orb, and distinct active booster item rows add linearly. Equipping is locked for an active battle. Activating an existing booster tier extends its real-time expiry without increasing that tier's magnitude.

The seeded content is defined in both `PlayerItemCatalogSeed` for display/editor fallback and the authoritative migration for production:

- Head: Scanner I/III/V and Stability Visor.
- Body: Wideband, Lockdrive, and Reserve Clock rigs.
- Orbs: Signal, Lattice, Quick, and Entangle fixed profiles.
- Boosts: Band Expander and Lock Accelerant S/M/L.

The Profile screen reuses the Head and Body anchors and exposes a Capture Record drawer. The Inventory screen performs authority-backed equip/activation actions. Entangle Orbs are intentionally absent from ordinary vending/static pickup seeds.

## Manifests and deployment

Run:

`Tools > Chainbound > Capture > Export Authoritative Manifests`

The exporter writes stable, versioned files to `backend/Chainbound.BattleService/manifests`, computes the gameplay SHA-256 hash from the exact UTF-8 bytes, and stamps every authority config asset. Export fails when no encounter is available, which prevents enabling an empty authority.

Deployment order:

1. Apply `Supabase/migrations/20260903010000_authoritative_trading_trace_capture.sql`.
2. Deploy the updated `profile_get_stats` Edge Function.
3. Export the final gameplay and capture manifests from the same Unity content revision used for the client build.
4. Build and deploy `backend/Chainbound.BattleService/Dockerfile` with `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_ROLE_KEY`, and `SOLANA_RPC_ENDPOINT` supplied as secrets/configuration.
5. Keep `AUTHORITATIVE_PVE_CAPTURE_ENABLED=false` until shadow/parity testing succeeds. Set the matching Unity board flag only after the service readiness endpoint is healthy.

The service's `/health/ready` endpoint requires valid authority configuration, non-empty exported encounters, a non-empty capture config hash, a fresh Solana time sample, and the service rollout flag.

## Rollout boundary

The capture authority, persistence, and client integration are implemented behind the rollout gate. The existing full combat move/status resolver still resides in `Chainbound.Combat.Runtime`; the service currently rejects non-capture team commands with `combat_core_migration_required`. Therefore production authoritative PvE must remain disabled until that resolver is extracted into Core with behavior parity and the service processes ordinary team/enemy turns. Editor/test practice remains explicitly local.

This boundary is deliberate: enabling the flag before resolver parity would allow a capture attempt to start but could not authoritatively continue normal combat after a timeout.

## Verification

- Unity EditMode coverage: `Assets/Scripts/Combat/Tests/CaptureCoreTests.cs`.
- Cross-runtime deterministic verification: `dotnet run --project tools/Chainbound.CaptureCoreVerification/Chainbound.CaptureCoreVerification.csproj -c Release`.
- Service build: `dotnet build backend/Chainbound.BattleService/Chainbound.BattleService.csproj -c Release`.
- Edge Function type check: `deno check Supabase/functions/profile_get_stats/index.ts`.

The cross-runtime harness pins a golden fixed-point state. Change that expected state only as an intentional config/manifest-version migration.
