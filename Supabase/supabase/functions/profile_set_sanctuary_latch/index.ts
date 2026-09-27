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

const SCENE_ID_RE = /^[A-Za-z][A-Za-z0-9_]{0,63}$/;
const SPAWN_ID_RE = /^[a-z][a-z0-9_]{1,63}$/;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const sanctuary = asObject(body.sanctuary);
  const lastLatch = parseLatch(sanctuary.lastLatch);
  if (sanctuary.lastLatch != null && !lastLatch) {
    return jsonResponse({ success: false, error: "invalid_last_latch" }, 400);
  }

  const lastPortal = parsePortal(sanctuary.lastPortal);
  if (sanctuary.lastPortal != null && !lastPortal) {
    return jsonResponse({ success: false, error: "invalid_last_portal" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data: profileRow, error: readError } = await supabase
      .from("player_profiles")
      .select("progression_state")
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    if (readError) throw readError;

    const current = asObject((profileRow as JsonMap | null)?.progression_state);
    const nextSanctuary: JsonMap = {};
    if (lastLatch) nextSanctuary.lastLatch = lastLatch;
    if (lastPortal) nextSanctuary.lastPortal = lastPortal;

    const next = { ...current, sanctuary: nextSanctuary };

    const { error: updateError } = await supabase
      .from("player_profiles")
      .update({ progression_state: next })
      .eq("player_profile_id", profile.player_profile_id);

    if (updateError) throw updateError;

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      progression_state: next,
    });
  } catch (error) {
    console.error("[profile_set_sanctuary_latch]", error);
    return jsonResponse({ success: false, error: "profile_set_sanctuary_latch_failed" }, 500);
  }
});

function asObject(value: unknown): JsonMap {
  return value && typeof value === "object" && !Array.isArray(value) ? value as JsonMap : {};
}

function parseLatch(value: unknown): JsonMap | null {
  if (value == null) return null;
  const obj = asObject(value);
  const kindRaw = str(obj.kind);
  const kind = kindRaw === "1" || kindRaw.toLowerCase() === "portal" ? "Portal" : "Hub";
  const sceneId = str(obj.sceneId);
  const spawnId = str(obj.spawnId);
  if (!SCENE_ID_RE.test(sceneId) || !SPAWN_ID_RE.test(spawnId)) return null;
  return { kind, sceneId, spawnId };
}

function parsePortal(value: unknown): JsonMap | null {
  if (value == null) return null;
  const obj = asObject(value);
  const sceneId = str(obj.sceneId);
  const spawnId = str(obj.spawnId);
  if (!SCENE_ID_RE.test(sceneId) || !SPAWN_ID_RE.test(spawnId)) return null;
  return { sceneId, spawnId };
}
