# NPC sequence: claim and release (dialogue / cutscene holds)

## What already exists

- **`Step_NPC_ClaimControl`** (`SignalJam/Narrative/Steps/NPC/Claim Control`) — calls `NPCController.ClaimControl`.
- **`Step_NPC_ReleaseControl`** (`SignalJam/Narrative/Steps/NPC/Release Control`) — releases that claim.

`ClaimType`:

- **Hard** — `NPCInterruptPolicy.Frozen`: movement disabled, agent stopped. Use for “NPC must not move during dialogue.”
- **Soft** — `Suppressed`: intended for lighter interruption; route behavior differs from Hard. Prefer **Hard** for a full hold.

## How it works now (handle tracking)

1. **Claim** stores the returned `ClaimHandle` in `SequenceContext` (keyed by `npcId`).
2. **Release** calls `ClaimHandle.Release()` via `TryReleaseStoredNpcClaim` for that `npcId`.
3. **Safety net**: `SequenceRunner` calls `context.ReleaseAllNpcClaims()` when a sequence **finishes** or **`StopSequence()`** runs, so NPCs are not left frozen if a step is skipped or the sequence aborts.
4. **`StartSequence`** clears context (including releasing any lingering NPC claims before a new run).

## Recommended sequence pattern (freeze for dialogue)

Use the same `npcId` string as on the NPC’s `NPCController` (registered in `NPCRegistry`).

1. **`Step_NPC_ClaimControl`** — `claimType = Hard`, `reason` e.g. `"dialogue_block_01"`.
2. **`Step_PlayDialogue`** (or camera / other steps).
3. **`Step_NPC_ReleaseControl`** — same `npcId`.

Optional: pair with **`Step_NPC_EnableAutonomy`** (disable before claim, enable after release) if you also want schedule/route following off for the whole block; **Hard claim** alone already stops movement.

## Pitfalls

- **Mismatched `npcId`** — claim and release must use the same id; registry lookup fails if the id is empty or wrong.
- **Double Hard claim** — a second `ClaimControl` for the same NPC without release overwrites the stored handle and releases the previous one (warning logged).
- **Fallback on release** — `Step_NPC_ReleaseControl` can call `ReleaseAllClaims()` on the NPC if no handle was stored (`releaseAllClaimsIfNoStoredHandle`, default on). Turn off if you need stricter behavior.

## Files touched (implementation)

- `SequenceContext` — `StoreNpcClaim`, `TryReleaseStoredNpcClaim`, `ReleaseAllNpcClaims`; integrated into `Clear` / `ClearAll`.
- `SequenceRunner` — releases NPC claims on sequence completion and on `StopSequence`.
- `Step_NPC_ClaimControl` / `Step_NPC_ReleaseControl` — wired to context storage.
