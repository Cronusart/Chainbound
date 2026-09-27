import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const [questsResult, sequencesResult] = await Promise.all([
      supabase
        .from("profile_quest_states")
        .select(
          "quest_id,lifecycle,objective_progress,resolution_id,resolution_context,revision,content_version,resolved_at,updated_at",
        )
        .eq("player_profile_id", profile.player_profile_id)
        .order("quest_id"),
      supabase
        .from("profile_sequence_checkpoints")
        .select(
          "sequence_id,status,checkpoint_id,checkpoint_label,last_completed_label,checkpoint_step_index,scene_id,spawn_id,persistent_variables,rail_flag_snapshot,completed_step_guids,played_gate_keys,revision,sequence_version,updated_at",
        )
        .eq("player_profile_id", profile.player_profile_id)
        .order("sequence_id"),
    ]);

    if (questsResult.error) throw questsResult.error;
    if (sequencesResult.error) throw sequencesResult.error;

    return jsonResponse({
      success: true,
      playerProfileId: profile.player_profile_id,
      schemaVersion: 1,
      questStates: (questsResult.data ?? []).map(mapQuestState),
      sequenceCheckpoints: (sequencesResult.data ?? []).map(mapSequenceCheckpoint),
    });
  } catch (error) {
    console.error("[profile_get_narrative_state]", error);
    return jsonResponse({ success: false, error: "profile_get_narrative_state_failed" }, 500);
  }
});

function mapQuestState(row: JsonMap): JsonMap {
  return {
    questId: stringValue(row.quest_id),
    lifecycle: stringValue(row.lifecycle),
    objectiveProgress: objectEntries(row.objective_progress, "objectiveId", "currentProgress"),
    resolution: {
      resolutionId: stringValue(row.resolution_id),
      context: objectEntries(row.resolution_context, "key", "value"),
      resolvedAtUtc: stringValue(row.resolved_at),
    },
    revision: integerValue(row.revision),
    contentVersion: Math.max(1, integerValue(row.content_version)),
    updatedAtUtc: stringValue(row.updated_at),
  };
}

function mapSequenceCheckpoint(row: JsonMap): JsonMap {
  return {
    sequenceId: stringValue(row.sequence_id),
    status: stringValue(row.status),
    checkpointId: stringValue(row.checkpoint_id),
    checkpointLabel: stringValue(row.checkpoint_label),
    lastCompletedLabel: stringValue(row.last_completed_label),
    checkpointStepIndex: integerValue(row.checkpoint_step_index),
    sceneId: stringValue(row.scene_id),
    spawnId: stringValue(row.spawn_id),
    persistentVariables: objectEntries(row.persistent_variables, "key", "value"),
    railFlagSnapshot: flagEntries(row.rail_flag_snapshot),
    completedStepGuids: stringArray(row.completed_step_guids),
    playedGateKeys: stringArray(row.played_gate_keys),
    revision: integerValue(row.revision),
    sequenceVersion: Math.max(1, integerValue(row.sequence_version)),
    updatedAtUtc: stringValue(row.updated_at),
  };
}

function objectEntries(value: unknown, keyName: string, valueName: string): JsonMap[] {
  if (!value || typeof value !== "object" || Array.isArray(value)) return [];
  return Object.entries(value as JsonMap).map(([key, entryValue]) => ({
    [keyName]: key,
    [valueName]: valueName === "currentProgress" ? integerValue(entryValue) : stringValue(entryValue),
  }));
}

function flagEntries(value: unknown): JsonMap[] {
  if (!value || typeof value !== "object" || Array.isArray(value)) return [];
  return Object.entries(value as JsonMap).map(([key, entryValue]) => ({
    key,
    value: booleanValue(entryValue),
  }));
}

function booleanValue(value: unknown): boolean {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  const normalized = stringValue(value).toLowerCase();
  return normalized === "true" || normalized === "1";
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.map(stringValue).filter((entry) => entry.length > 0) : [];
}

function stringValue(value: unknown): string {
  return value == null ? "" : String(value);
}

function integerValue(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.trunc(parsed) : 0;
}
