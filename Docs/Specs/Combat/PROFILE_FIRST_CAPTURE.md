# Profile-First Wild Capture

**Status:** Active design (guest-first)  
**Related:** `ECONOMY_01_CURRENCY_AND_ITEMS.md`, `Account_Platform_Refactor.md`, `SPEC_03_ONCHAIN_CAPTURE_AND_MINT.md` (wallet bind path only)

## Goal

Players battle and capture freely without connecting a wallet. Wallet is required later for **Bind to Chain** / ownership signing — not for standard catch.

## Capture grant (standard wild)

| Layer | What happens |
|-------|----------------|
| Chainling | **Supabase dossier** row (`chainlings`) — same shape as Toby starter |
| Move reward | **Known ordinal** in `move_items` (`tier = ordinal`, source `wild_capture`) |
| Solana PDA | **Not** created at capture |
| Inscription NFT | **Not** minted at capture |

## Wallet later

| Action | Requires wallet |
|--------|-----------------|
| Continue as Guest / intro / starter | No |
| Wild battle + capture | No |
| Bind to Chain (NFT) | Yes |
| Optional dossier → PDA sync | When wallet linked |
| Entangled / premium mint path | Yes (deferred) |

## Server contract

Edge: `profile_capture_wild`  
Auth: `x-player-profile-id` (+ optional `x-client-session-id`)  
Body: species, level, HP ratio, capture rate inputs, `idempotency_key`, optional `ordinal_move_id`, IV/entangled flags.

RPC: `profile_capture_wild_tx` — atomic:

1. Resolve the authenticated `player_profile_id`
2. Capture roll (server RNG; same formula family as `CaptureRateCalculator`)
3. On miss: return `{ success: false, reason: "capture_failed" }` (no inventory write)
4. On hit: insert `chainlings` dossier (party slot or storage), grant Known ordinal via existing grant helper, bump lifetime capture stats
5. Idempotent on `(player_profile_id, idempotency_key)`

## Client

- Primary: `SupabaseProfileCaptureService` (`ICaptureService`)
- Legacy on-chain: `CaptureService` + MoveVault — **Bind / wallet path only**, not default wild catch
- `BattleResult` / post-battle UI prefer `backendChainlingId` + ordinal move id over PDA/inscription fields

## Out of scope (follow-ups)

- Capture-tool inventory consume on attempt
- Re-enable in-battle Capture UI (quarantined today) — call `ICaptureService` via `SolanaServiceBridge.CaptureService` / `SupabaseProfileCaptureService`, then `CaptureResult.ApplyToBattleResult`
- Entangled premium inscription at catch
- Species-driven ordinal move pools (default today: client `ordinal_move_id`, else `baseline_jab`)
- Active party capacity: `CombatConstants.ROSTER_SIZE` (3); overflow → storage (`is_party_member=false`)
