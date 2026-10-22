import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

interface ProfileStatsRow {
  display_name?: string | null;
  progression_state?: Record<string, unknown> | null;
  ram_balance?: number | null;
  ram_balance_version?: number | null;
}

interface LifetimeStatsRow {
  battles_won?: number | null;
  battles_lost?: number | null;
  battles_drawn?: number | null;
  battles_fled?: number | null;
  total_kos_scored?: number | null;
  total_kos_taken?: number | null;
  total_captures?: number | null;
  entangled_captures?: number | null;
  capture_attempts?: number | null;
  capture_orbs_used?: number | null;
  capture_timeouts?: number | null;
  capture_targets_escaped?: number | null;
  capture_perfect_locks?: number | null;
  captures_above_low_hp?: number | null;
  captures_by_orb?: Record<string, number> | null;
  fastest_capture_milliseconds?: number | null;
  extreme_profile_captures?: number | null;
  entangle_orb_captures?: number | null;
  total_rebirths?: number | null;
  total_star_ups?: number | null;
  total_mastery_cycles?: number | null;
  total_ram_earned?: number | null;
  total_ram_spent?: number | null;
  total_ordinals_acquired?: number | null;
  total_inscriptions_created?: number | null;
  candy_machine_rolls?: number | null;
  candy_machine_stamps?: number | null;
  unique_species_captured?: number | null;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data: profileRow, error: profileError } = await supabase
      .from("player_profiles")
      .select("display_name, progression_state, ram_balance, ram_balance_version")
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    if (profileError) throw profileError;

    const profileStats = (profileRow ?? {}) as ProfileStatsRow;
    const wallet = await getActiveWallet(supabase, profile.player_profile_id);
    const ramBalance = Number(profileStats.ram_balance ?? 0);

    let stats: LifetimeStatsRow = {};
    const { data: statsRow, error: statsError } = await supabase
      .from("player_lifetime_stats")
      .select(
        "battles_won, battles_lost, battles_drawn, battles_fled, total_kos_scored, total_kos_taken, total_captures, entangled_captures, capture_attempts, capture_orbs_used, capture_timeouts, capture_targets_escaped, capture_perfect_locks, captures_above_low_hp, captures_by_orb, fastest_capture_milliseconds, extreme_profile_captures, entangle_orb_captures, total_rebirths, total_star_ups, total_mastery_cycles, total_ram_earned, total_ram_spent, total_ordinals_acquired, total_inscriptions_created, candy_machine_rolls, candy_machine_stamps, unique_species_captured",
      )
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    if (statsError) throw statsError;
    stats = (statsRow ?? {}) as LifetimeStatsRow;

    const { data: achievementRows, error: achievementError } = await supabase
      .from("profile_capture_achievements")
      .select("achievement_id")
      .eq("player_profile_id", profile.player_profile_id)
      .order("unlocked_at", { ascending: true });
    if (achievementError) throw achievementError;

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      display_name: profileStats.display_name ?? profile.display_name ?? "",
      wallet_address: str(wallet?.wallet_public_key),
      ram_balance: ramBalance,
      ram_balance_version: Number(profileStats.ram_balance_version ?? 0),
      progression_state: profileStats.progression_state ?? {},
      stats: {
        battles_won: stats.battles_won ?? 0,
        battles_lost: stats.battles_lost ?? 0,
        battles_drawn: stats.battles_drawn ?? 0,
        battles_fled: stats.battles_fled ?? 0,
        total_kos_scored: stats.total_kos_scored ?? 0,
        total_kos_taken: stats.total_kos_taken ?? 0,
        total_captures: stats.total_captures ?? 0,
        entangled_captures: stats.entangled_captures ?? 0,
        capture_attempts: stats.capture_attempts ?? 0,
        capture_orbs_used: stats.capture_orbs_used ?? 0,
        capture_timeouts: stats.capture_timeouts ?? 0,
        capture_targets_escaped: stats.capture_targets_escaped ?? 0,
        capture_perfect_locks: stats.capture_perfect_locks ?? 0,
        captures_above_low_hp: stats.captures_above_low_hp ?? 0,
        captures_by_orb: JSON.stringify(stats.captures_by_orb ?? {}),
        fastest_capture_milliseconds: stats.fastest_capture_milliseconds ?? 0,
        extreme_profile_captures: stats.extreme_profile_captures ?? 0,
        entangle_orb_captures: stats.entangle_orb_captures ?? 0,
        total_rebirths: stats.total_rebirths ?? 0,
        total_star_ups: stats.total_star_ups ?? 0,
        total_mastery_cycles: stats.total_mastery_cycles ?? 0,
        total_ram_earned: stats.total_ram_earned ?? 0,
        total_ram_spent: stats.total_ram_spent ?? 0,
        total_ordinals_acquired: stats.total_ordinals_acquired ?? 0,
        total_inscriptions_created: stats.total_inscriptions_created ?? 0,
        candy_machine_rolls: stats.candy_machine_rolls ?? 0,
        candy_machine_stamps: stats.candy_machine_stamps ?? 0,
        unique_species_captured: stats.unique_species_captured ?? 0,
      },
      capture_achievement_ids: (achievementRows ?? []).map((row) => str(row.achievement_id)),
    });
  } catch (error) {
    console.error("[profile_get_stats]", error);
    return jsonResponse({ success: false, error: "profile_get_stats_failed" }, 500);
  }
});
