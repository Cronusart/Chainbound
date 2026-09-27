import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

// Read-only live-ops feed for SupabaseEncounterEventsProvider.
// See Docs/Specs/Combat/ENCOUNTER_EVENTS_SUPABASE.md

interface EncounterEventRow {
  id: string;
  name: string;
  starts_at: string;
  ends_at: string;
  enabled: boolean;
  payload: unknown;
}

function toUnix(value: string): number {
  const ms = new Date(value).getTime();
  return Number.isFinite(ms) ? Math.floor(ms / 1000) : 0;
}

function num(value: unknown, fallback = 0): number {
  const n = typeof value === "number" ? value : Number(value);
  return Number.isFinite(n) ? n : fallback;
}

function int(value: unknown, fallback = 0): number {
  return Math.trunc(num(value, fallback));
}

function text(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function stringArray(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return value.filter((v) => typeof v === "string" && v.trim() !== "") as string[];
}

/**
 * Unity deserializes with JsonUtility, which aborts the whole object on a type
 * mismatch (e.g. segments as "1" instead of 1). Designer-authored jsonb is coerced
 * to the exact DTO shape here so a single bad field can't blank out an event.
 */
function normalizeOps(value: unknown): JsonMap[] {
  if (!Array.isArray(value)) return [];

  const ops: JsonMap[] = [];
  for (const raw of value) {
    if (raw === null || typeof raw !== "object" || Array.isArray(raw)) continue;
    const op = raw as JsonMap;

    // Only require a non-negative integer: unknown op types are inert on the client,
    // so new EncounterModifierOpType values never need a function redeploy.
    const opType = int(op.op_type, -1);
    if (opType < 0) {
      console.warn("[get_encounter_events] skipped op with invalid op_type", op.op_type);
      continue;
    }

    ops.push({
      op_type: opType,
      target_id: text(op.target_id),
      mode: int(op.mode, 0),
      value: num(op.value, 0),
      inject_weight: int(op.inject_weight, 0),
      inject_capture_rate: int(op.inject_capture_rate, 0),
      inject_base_stars: int(op.inject_base_stars, 0),
    });
  }
  return ops;
}

function normalizePayload(value: unknown): JsonMap {
  const src = (value !== null && typeof value === "object" && !Array.isArray(value))
    ? value as JsonMap
    : {};

  return {
    modifier_id: text(src.modifier_id),
    display_name: text(src.display_name),
    area_ids: stringArray(src.area_ids),
    zone_ids: stringArray(src.zone_ids),
    species_ids: stringArray(src.species_ids),
    segments: int(src.segments, 0),
    filter_by_difficulty: src.filter_by_difficulty === true,
    difficulty: int(src.difficulty, 0),
    start_unix_utc: int(src.start_unix_utc, 0),
    end_unix_utc: int(src.end_unix_utc, 0),
    ops: normalizeOps(src.ops),
  };
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req) ?? {};

  // now_unix lets the client pin the evaluation instant; fall back to server time.
  const requestedNow = int(body.now_unix, 0);
  const nowUnix = requestedNow > 0 ? requestedNow : Math.floor(Date.now() / 1000);
  const nowIso = new Date(nowUnix * 1000).toISOString();

  try {
    const supabase = supabaseAdmin();

    const { data, error } = await supabase
      .from("encounter_events")
      .select("id, name, starts_at, ends_at, enabled, payload")
      .eq("enabled", true)
      .lte("starts_at", nowIso)
      .gte("ends_at", nowIso)
      .order("starts_at", { ascending: true });

    if (error) throw error;

    const rows = (data ?? []) as EncounterEventRow[];
    const events = rows.map((row) => ({
      id: row.id,
      name: row.name ?? "",
      starts_at_unix: toUnix(row.starts_at),
      ends_at_unix: toUnix(row.ends_at),
      enabled: row.enabled === true,
      payload: normalizePayload(row.payload),
    }));

    return jsonResponse({ events, server_now_unix: nowUnix });
  } catch (error) {
    console.error("[get_encounter_events]", error);
    // Empty events keeps the client on its last cache / local-only modifiers.
    return jsonResponse({ error: "get_encounter_events_failed", events: [] }, 500);
  }
});
