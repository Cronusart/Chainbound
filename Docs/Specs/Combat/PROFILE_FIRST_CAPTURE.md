# Authoritative Wild Capture

Standard wild capture is profile-backed and off-chain. It never creates a wallet
asset or writes to the on-chain Chainling vault. Wallet Bind remains a separate,
explicit ownership action.

## Production grant path

`Chainbound.BattleService` is the only production creator of captured Chainlings.
It holds the authoritative battle session, resolves the capture challenge, and
calls `authoritative_begin_capture_attempt_service` and
`authoritative_finalize_capture_attempt_service` with server-selected target
state and ordinal move data.

The database serializes each profile's attempts with a profile lock. It checks
the total ownership cap before an Orb is consumed and again before a locked
result is granted. The final transaction creates the dossier, capture ledger,
ordinal move item, and loadout slot `0` together. A retried finalization returns
the original Chainling rather than issuing a second grant.

## Capacity and roster

- Total captured Chainlings: **25** per profile.
- Active party: first free slot of **3**.
- PC storage: remaining **22** slots.
- At `25 / 25`, capture is blocked before an Orb is consumed. No overflow,
  replacement, deletion, or automatic release occurs.
- `profile_get_chainlings` is read-only and returns server-derived owned, party,
  and storage counts/capacities. The PC is a pure reader of that roster.

## Rollout and practice

The authority service and Unity rollout flags remain disabled until ordinary-turn
resolver parity (moves, enemy turns, KO, timeout, resume) passes its test matrix.
Editor practice capture is explicitly unsaved and must say so in the result copy.
On device, capture fails closed while authority is disabled.

## Retired path

`profile_capture_wild`, `profile_capture_wild_tx`, `SupabaseProfileCaptureService`,
and `ICaptureService` are retired. Historical migrations and existing Chainling
rows remain for audit and test-data continuity, but the old client-controlled
execution path has no runtime grant permission.
