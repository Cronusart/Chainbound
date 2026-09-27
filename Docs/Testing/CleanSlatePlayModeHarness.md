# Clean-Slate Play Mode Harness

Use `Tools > Signal Jam > Testing > Play Bootstrap With Clean Local State` to test onboarding from a fresh local identity.

The command can be selected in Edit Mode or Play Mode. If the game is running, the harness first exits Play Mode so shutdown code cannot rewrite data after the reset. It then:

1. Clears the project's Unity Editor `PlayerPrefs`.
2. Reloads scripts to discard process-memory service/session caches.
3. Starts Play Mode through `Assets/Scenes/Bootstrap.unity`.
4. Forces the one-shot Bootstrap destination to `main_menu`.

`Tools > Signal Jam > Testing > Wipe Local State Only...` performs the same reset without entering Play Mode.

## Data cleared

- Profile-keyed narrative snapshots and mutation outboxes.
- Offline onboarding profile/checkpoint data.
- Cached anonymous authentication and offline account snapshot.
- Guest/install identity, wallet hints, and intro compatibility mirrors.
- Local settings stored in `PlayerPrefs`.

Exported logs and screenshots under `Application.persistentDataPath` are not gameplay state and are left intact.

## Backend boundary

The harness never mutates or deletes Supabase data. Clearing anonymous authentication causes the next online bootstrap to create a fresh anonymous test profile. Reconnecting an existing wallet/profile will intentionally restore that profile's authoritative server quest state.

