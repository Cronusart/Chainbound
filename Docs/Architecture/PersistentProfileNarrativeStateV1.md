# Persistent Profile Narrative State v1

## Ownership

- `profile_quest_states` is authoritative for quest lifecycle, objective progress, and immutable completion resolution data.
- `profile_sequence_checkpoints` is authoritative for resumable in-progress sequence state. Checkpoint IDs are resume authority; raw step indexes are diagnostic/advisory only. `rail_flag_snapshot` stores sequence-rail world flags captured at `Step_SetCheckpoint` (durable `world.*` flags stay on progression flags, not this column).
- A sequence's persistent variables may hold pending resolution context while it is active. `Step_CompleteQuestWithResolution` promotes authored bindings into the completed quest row.
- `player_profiles.intro_complete` remains a compatibility mirror. Completing `chainbound_intro` updates the quest row and mirror in the same database transaction.
- Quest resolution data does not belong in `progression_state` or NPC memory.

## Runtime flow

`NarrativeProfileStateCoordinator` fetches the authenticated snapshot, merges the profile-keyed local outbox, restores definitions through `QuestCatalogSO`, and restores an active sequence through `SequenceCatalogSO`. Main-menu play routing waits for this hydration before starting a sequence or changing gameplay scenes.

Runtime routers, guards, and interactables read `QuestService` and `SequenceRunner` only. They never perform per-interaction backend requests and fail closed while `QuestService.IsHydrated` is false.

Every mutation is written to the local snapshot/outbox first. Remote replay is ordered, mutation-ID idempotent, and revision checked. Revision conflicts are rebased against a fresh server snapshot. A server-side Completed quest cannot regress; conflicting completion outcomes preserve the server's immutable resolution.

## Content rules

- Every quest defines a stable default resolution. Branched quests define allowed resolution IDs and context keys.
- Every interruption-sensitive sequence gate uses an explicit `Step_SetCheckpoint` with a stable checkpoint ID, authored `resumeSceneId` / `resumeSpawnId`, and a safe resume **label** (idle wait/roam, never mid-dialogue). Continue loads that scene before the sequence runs.
- Unique world pickups and sequence guardrails use profile world flags (`WorldPresenceBinding` + unique IDs, or `intro.guardrail.<object>` / `world.pickup.*` plus gates). Guardrail catalog rules apply only after a flag exists. `world.*` flags are durable and are never rewound. Rail flags (`intro.guardrail.*`, `sequence.*`) are auto-snapshotted onto the checkpoint row (`rail_flag_snapshot`); continue restores that snapshot so later rails (e.g. Collider3) do not stay on. Old saves with an empty snapshot fall back to the authored resume list.
- Completed-lifecycle interaction rules must opt into a lifecycle mask. Existing rules remain Active-only.
- Historical completion context is backfilled only when a starter can be identified unambiguously. Otherwise `legacy_complete` with empty context selects generic NPC fallback dialogue.

## Pre-v1 limitation

Active quest or sequence progress created before v1 and never persisted cannot be reconstructed safely. Migration does not infer that state. If an authored checkpoint disappears after a content revision, runtime recovery tries the checkpoint ID, then its stored label, then the sequence's safe resume label and logs the fallback.
