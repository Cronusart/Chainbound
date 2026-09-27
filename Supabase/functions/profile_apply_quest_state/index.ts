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

  const state = asObject(body.questState);
  const resolution = asObject(state.resolution);
  const mutationId = str(body.mutationId);
  const questId = str(state.questId);
  if (!mutationId || !questId) {
    return jsonResponse({ success: false, error: "mutation_id_and_quest_id_required" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_apply_quest_state_tx", {
      p_profile_id: profile.player_profile_id,
      p_mutation_id: mutationId,
      p_expected_revision: nonNegativeInteger(body.expectedRevision),
      p_quest_id: questId,
      p_lifecycle: str(state.lifecycle).toLowerCase(),
      p_objective_progress: entriesToObject(state.objectiveProgress, "objectiveId", "currentProgress", true),
      p_resolution_id: str(resolution.resolutionId),
      p_resolution_context: entriesToObject(resolution.context, "key", "value", false),
      p_content_version: Math.max(1, integerValue(state.contentVersion)),
    });

    if (error) throw error;
    const result = asObject(data);
    if (result.success !== true) {
      const status = result.error === "revision_conflict" || result.error === "completed_quest_immutable" ? 409 : 400;
      return jsonResponse(result, status);
    }

    return jsonResponse({
      ...result,
      questState: mapQuestState(asObject(result.questState)),
    });
  } catch (error) {
    console.error("[profile_apply_quest_state]", error);
    return jsonResponse({ success: false, error: "profile_apply_quest_state_failed" }, 500);
  }
});

function entriesToObject(
  value: unknown,
  keyName: string,
  valueName: string,
  numeric: boolean,
): JsonMap {
  if (!Array.isArray(value)) return {};
  const result: JsonMap = {};
  for (const raw of value) {
    const entry = asObject(raw);
    const key = str(entry[keyName]);
    if (!key) continue;
    result[key] = numeric ? nonNegativeInteger(entry[valueName]) : str(entry[valueName]);
  }
  return result;
}

function mapQuestState(state: JsonMap): JsonMap {
  return {
    questId: str(state.questId),
    lifecycle: str(state.lifecycle),
    objectiveProgress: objectEntries(state.objectiveProgress, "objectiveId", "currentProgress", true),
    resolution: {
      resolutionId: str(state.resolutionId),
      context: objectEntries(state.resolutionContext, "key", "value", false),
      resolvedAtUtc: str(state.resolvedAtUtc),
    },
    revision: nonNegativeInteger(state.revision),
    contentVersion: Math.max(1, integerValue(state.contentVersion)),
    updatedAtUtc: str(state.updatedAtUtc),
  };
}

function objectEntries(
  value: unknown,
  keyName: string,
  valueName: string,
  numeric: boolean,
): JsonMap[] {
  const source = asObject(value);
  return Object.entries(source).map(([key, entryValue]) => ({
    [keyName]: key,
    [valueName]: numeric ? nonNegativeInteger(entryValue) : str(entryValue),
  }));
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
