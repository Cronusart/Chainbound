# App lifecycle interruptions

`AppLifecycleService` lives under the persistent `GameServices` prefab. It treats
OS focus loss, application pause, and the editor simulation key as recoverable
interruptions. It does not reset scenes, battles, anonymous sessions, JWTs, or
wallet authorization.

| Context | Time scale | Audio | Overlay | Input | Battle |
| --- | ---: | --- | --- | --- | --- |
| Overworld / Player menu / Dialogue | 0 | Lifecycle mute | Continue | Locked | Preserved |
| PvE combat | 0 | Lifecycle mute | Continue / Flee | Locked | Preserved |
| Wallet signing | unchanged | unchanged | “Waiting for Seed Vault…” / Cancel | Locked | Preserved |
| Main menu / Loading | unchanged | unchanged | None | Unchanged | N/A |

The policy asset is `Assets/Data/UI/AppInterruptPolicy.asset`; the overlay is
`Assets/Prefabs/UI/AppInterruptOverlay.prefab`. Use the lifecycle component's
policy reference and the overlay prefab's authored labels/buttons to tune the
experience. `AudioService.SetLifecycleMute` temporarily drives mixer buses to
the mute floor while retaining each user-selected bus volume.

## Wiring

The editor command `Tools/Signal Jam/App Lifecycle/Wire GameServices` creates or
repairs the policy asset, overlay prefab, `AppLifecycleService`, and the UI-side
`CombatMenuSuppressionBridge` child in `GameServices.prefab`. The bridge listens
to `CombatBattleController` events without adding a Combat.Runtime → UI
dependency and suppresses the gameplay menu for the battle lifetime.

Wallet operations expose `SolanaWalletService.IsWalletUiActive`. MWA auth tokens
are persisted through `SecureCredentialStore` (`mwa.authToken`), while
`SolWallet_LastPubkey` remains display-only. MWA cancellation and timeout should
be surfaced to the account flow as a user cancellation; they must not erase the
anonymous session or cached JWT.

## Editor simulation and smoke checklist

Enable `simulateInEditor` on `AppLifecycleService` and press F9 (configurable)
to show the same pause overlay without leaving the editor. Verify:

1. World/menu input is locked and time scale/audio are restored after Continue.
2. Combat shows Continue and Flee; Flee calls `CombatBattleController.FleeBattle`
   and releases the menu suppression only after the battle ends.
3. Wallet signing keeps the native request recoverable, supports Cancel, and
   dismisses automatically when the provider callback completes.
4. Returning from focus loss resumes pending Solana operations without a second
   main-menu bootstrap.

> **Future PvP interrupt-cycle stub:** add server-authoritative per-player turn
> timers and explicit timeout notifications at the `CombatPvE`/wallet boundary.
> A client lifecycle pause must not be interpreted as a PvP forfeit; timeout,
> grace-period, and reconnect outcomes belong to the match service.
