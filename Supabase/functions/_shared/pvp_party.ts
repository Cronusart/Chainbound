import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { loadChainlingLoadouts } from "./chainling_loadouts.ts";
import { apCostForMove } from "./move_ap.ts";
import { HP_POLICY_FULL_HEAL, partyLockError } from "./pvp_rules.ts";

interface ChainlingRow {
  id: string;
  species_id: string | null;
  stars: number | null;
  mastery_xp: number | null;
  rebirth_count: number | null;
  evolution_stage: number | null;
  display_name: string | null;
  ivs: number[] | null;
  rebirth_iv_bonuses: number[] | null;
  entangled_flag: boolean | null;
  entangled_type: string | null;
  mutation_id: string | null;
  current_hp: number | null;
}

const ENTANGLED_TYPE: Record<string, number> = {
  none: 0,
  node: 1,
  gas: 2,
  block: 3,
  encrypted: 4,
  burned: 5,
  frozen: 6,
  forked: 7,
  mined: 8,
  staked: 9,
  compressed: 10,
  bot: 11,
  glitched: 12,
  shard: 13,
  meme: 14,
};

export interface PvpPartyWire {
  teamName: string;
  hpPolicy?: string;
  chainlings: PvpChainlingWire[];
}

export interface PvpChainlingWire {
  speciesId: string;
  backendChainlingId: string;
  displayName: string;
  stars: number;
  masteryXp: number;
  rebirthCount: number;
  evolutionStage: number;
  moveIds: string[];
  modifier1SectionIds: string[];
  modifier2SectionIds: string[];
  ivs: number[];
  rebirthIvBonuses: number[];
  currentHp: number;
  moveApCosts?: number[];
  fainted?: boolean;
  isMutated: boolean;
  mutationId: string;
  isEntangled: boolean;
  entangledType: number;
}

export async function loadLockedParty(
  supabase: SupabaseClient,
  profileId: string,
): Promise<PvpPartyWire | null> {
  const { data: profile, error: profileError } = await supabase
    .from("player_profiles")
    .select("display_name")
    .eq("player_profile_id", profileId)
    .maybeSingle();
  if (profileError) throw profileError;

  const { data, error } = await supabase
    .from("chainlings")
    .select(
      "id, species_id, stars, mastery_xp, rebirth_count, evolution_stage, display_name, ivs, rebirth_iv_bonuses, entangled_flag, entangled_type, mutation_id, current_hp, party_slot",
    )
    .eq("player_profile_id", profileId)
    .eq("is_party_member", true)
    .order("party_slot", { ascending: true });
  if (error) throw error;

  const rows = (data ?? []) as ChainlingRow[];
  if (rows.length < 1) return null;
  const loadouts = await loadChainlingLoadouts(supabase, rows.map((row) => row.id));
  const teamName = typeof profile?.display_name === "string" && profile.display_name.trim().length > 0
    ? profile.display_name.trim()
    : "Trainer";

  return {
    teamName,
    chainlings: rows.map((row) => toWire(row, loadouts.get(row.id))),
  };
}

export async function profileHasActiveWallet(supabase: SupabaseClient, profileId: string): Promise<boolean> {
  const { data, error } = await supabase
    .from("profile_wallets")
    .select("id")
    .eq("player_profile_id", profileId)
    .eq("is_active", true)
    .is("unlinked_at", null)
    .limit(1);
  if (error) throw error;
  return Array.isArray(data) && data.length > 0;
}

function toWire(
  row: ChainlingRow,
  loadout: { move_ids: string[]; modifier1_section_ids: string[]; modifier2_section_ids: string[] } | undefined,
): PvpChainlingWire {
  const moveIds: string[] = [];
  const modifier1SectionIds: string[] = [];
  const modifier2SectionIds: string[] = [];
  const sourceMoves = loadout?.move_ids ?? [];
  for (let i = 0; i < sourceMoves.length; i++) {
    const moveId = (sourceMoves[i] ?? "").trim();
    if (!moveId) continue;
    moveIds.push(moveId);
    modifier1SectionIds.push((loadout?.modifier1_section_ids[i] ?? "").trim());
    modifier2SectionIds.push((loadout?.modifier2_section_ids[i] ?? "").trim());
  }

  const mutationId = (row.mutation_id ?? "").trim() || "none";
  const entangled = row.entangled_flag === true;
  const entangledName = entangled ? (row.entangled_type ?? "none").trim().toLowerCase() : "none";
  return {
    speciesId: (row.species_id ?? "").trim(),
    backendChainlingId: row.id,
    displayName: row.display_name ?? "",
    stars: row.stars ?? 0,
    masteryXp: row.mastery_xp ?? 0,
    rebirthCount: row.rebirth_count ?? 0,
    evolutionStage: row.evolution_stage ?? 1,
    moveIds,
    modifier1SectionIds,
    modifier2SectionIds,
    ivs: padInts(row.ivs, 6),
    rebirthIvBonuses: padInts(row.rebirth_iv_bonuses, 6),
    currentHp: row.current_hp ?? -1,
    isMutated: mutationId.toLowerCase() !== "none" && mutationId.length > 0,
    mutationId,
    isEntangled: entangled,
    entangledType: ENTANGLED_TYPE[entangledName] ?? 0,
  };
}

/** Full-heal casual lock: stamp catalog AP costs and clear fainted. Persisted HP stays for the pre-lock check. */
export function prepareLockedParty(party: PvpPartyWire): { party: PvpPartyWire; error: string | null } {
  const chainlings = Array.isArray(party?.chainlings) ? party.chainlings : [];
  const problem = partyLockError(chainlings.map((chainling) => ({ currentHp: chainling?.currentHp ?? null })));
  if (problem) return { party, error: problem };

  const stamped: PvpChainlingWire[] = [];
  for (const chainling of chainlings) {
    const moveIds = Array.isArray(chainling.moveIds) ? chainling.moveIds : [];
    const moveApCosts: number[] = [];
    for (const moveId of moveIds) {
      const cost = apCostForMove(moveId);
      if (cost === null) return { party, error: "party_move_unknown" };
      moveApCosts.push(cost);
    }
    stamped.push({ ...chainling, moveIds, moveApCosts, fainted: false });
  }

  return {
    party: { ...party, hpPolicy: HP_POLICY_FULL_HEAL, chainlings: stamped },
    error: null,
  };
}

function padInts(value: number[] | null, size: number): number[] {
  const source = Array.isArray(value) ? value : [];
  const result: number[] = [];
  for (let i = 0; i < size; i++) {
    const parsed = Number(source[i]);
    result.push(Number.isFinite(parsed) ? Math.trunc(parsed) : 0);
  }
  return result;
}
