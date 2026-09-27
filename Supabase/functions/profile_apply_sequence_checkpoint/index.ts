import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);

  const checkpoint = asObject(body.sequenceCheckpoint);
  const mutationId = str(body.mutationId);
  const sequenceId = str(checkpoint.sequenceId);
  if (!mutationId || !sequenceId) {
    return jsonResponse({ success: false, error: "mutation_id_and_sequence_id_required" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_apply_sequence_checkpoint_tx", {
      p_profile_id: profile.player_profile_id,
      p_mutation_id: mutationId,
      p_expected_revision: nonNegativeInteger(body.expectedRevision),
      p_sequence_id: sequenceId,
      p_status: str(checkpoint.status).toLowerCase(),
      p_checkpoint_id: str(checkpoint.checkpointId),
      p_checkpoint_label: str(checkpoint.checkpointLabel),
      p_last_completed_label: str(checkpoint.lastCompletedLabel),
      p_checkpoint_step_index: integerValue(checkpoint.checkpointStepIndex),
      p_scene_id: str(checkpoint.sceneId),
      p_spawn_id: str(checkpoint.spawnId),
      p_persistent_variables: entriesToObject(checkpoint.persistentVariables),
      p_completed_step_guids: stringArray(checkpoint.completedStepGuids),
      p_played_gate_keys: stringArray(checkpoint.playedGateKeys),
      p_rail_flag_snapshot: flagEntriesToObject(checkpoint.railFlagSnapshot),
      p_sequence_version: Math.max(1, integerValue(checkpoint.sequenceVersion)),
    });

    if (error) throw error;
    const result = asObject(data);
    if (result.success !== true) {
      const status = result.error === "revision_conflict" || result.error === "completed_sequence_immutable"
        ? 409
        : 400;
      return jsonResponse(result, status);
    }

    return jsonResponse({
      ...result,
      sequenceCheckpoint: mapSequenceCheckpoint(asObject(result.sequenceCheckpoint)),
    });
  } catch (error) {
    console.error("[profile_apply_sequence_checkpoint]", error);
    return jsonResponse({ success: false, error: "profile_apply_sequence_checkpoint_failed" }, 500);
  }
});

function entriesToObject(value: unknown): JsonMap {
  if (!Array.isArray(value)) return {};
  const result: JsonMap = {};
  for (const raw of value) {
    const entry = asObject(raw);
    const key = str(entry.key);
    if (key) result[key] = str(entry.value);
  }
  return result;
}

function flagEntriesToObject(value: unknown): JsonMap {
  if (!Array.isArray(value)) return {};
  const result: JsonMap = {};
  for (const raw of value) {
    const entry = asObject(raw);
    const key = str(entry.key);
    if (key) result[key] = booleanValue(entry.value);
  }
  return result;
}

function mapSequenceCheckpoint(checkpoint: JsonMap): JsonMap {
  return {
    sequenceId: str(checkpoint.sequenceId),
    status: str(checkpoint.status),
    checkpointId: str(checkpoint.checkpointId),
    checkpointLabel: str(checkpoint.checkpointLabel),
    lastCompletedLabel: str(checkpoint.lastCompletedLabel),
    checkpointStepIndex: integerValue(checkpoint.checkpointStepIndex),
    sceneId: str(checkpoint.sceneId),
    spawnId: str(checkpoint.spawnId),
    persistentVariables: objectEntries(checkpoint.persistentVariables),
    railFlagSnapshot: flagObjectEntries(checkpoint.railFlagSnapshot),
    completedStepGuids: stringArray(checkpoint.completedStepGuids),
    playedGateKeys: stringArray(checkpoint.playedGateKeys),
    revision: nonNegativeInteger(checkpoint.revision),
    sequenceVersion: Math.max(1, integerValue(checkpoint.sequenceVersion)),
    updatedAtUtc: str(checkpoint.updatedAtUtc),
  };
}

function objectEntries(value: unknown): JsonMap[] {
  const source = asObject(value);
  return Object.entries(source).map(([key, entryValue]) => ({ key, value: str(entryValue) }));
}

function flagObjectEntries(value: unknown): JsonMap[] {
  const source = asObject(value);
  return Object.entries(source).map(([key, entryValue]) => ({
    key,
    value: booleanValue(entryValue),
  }));
}

function booleanValue(value: unknown): boolean {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  const normalized = str(value).toLowerCase();
  return normalized === "true" || normalized === "1";
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.map((entry) => str(entry)).filter((entry) => entry.length > 0) : [];
}

function asObject(value: unknown): JsonMap {
  return value && typeof value === "object" && !Array.isArray(value) ? value as JsonMap : {};
}

function integerValue(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.trunc(parsed) : 0;
}

function nonNegativeInteger(value: unknown): number {
  return Math.max(0, integerValue(value));
}
