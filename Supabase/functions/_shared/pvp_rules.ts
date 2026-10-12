/**
 * Structural turn checks and result-window rules for casual dual-sim.
 * Cross-turn AP regen stays in the Unity resolver. A tampered resolver disputes on hash compare.
 */

import { versionsCompatible } from "./matchmaking_config.ts";
import { apCostForMove, PVP_AP_CAP } from "./move_ap.ts";

export const PVP_ROSTER_MAX = 3;
export const PVP_ACTIVE_MAX = 2;
export const HP_POLICY_FULL_HEAL = "full_heal";

export interface LockedChainling {
  moveIds: string[];
  moveApCosts?: number[];
  fainted?: boolean;
}

export interface TurnAction {
  actionType: number;
  moveIndex?: number;
  swapTargetIndex?: number;
  teamIndex?: number;
  targetTeamIndex?: number;
  targetSlotIndex?: number;
}

const UseMove = 0;
const Swap = 1;
const Pass = 2;

export interface PartyHpRow {
  currentHp: number | null;
}

/** Reject an empty, oversized, or all-fainted party. Unknown HP (-1) is not fainted. */
export function partyLockError(chainlings: PartyHpRow[]): string | null {
  const roster = Array.isArray(chainlings) ? chainlings : [];
  if (roster.length < 1) return "empty_party";
  if (roster.length > PVP_ROSTER_MAX) return "party_too_large";
  const allFainted = roster.every((chainling) => {
    const hp = chainling?.currentHp;
    return typeof hp === "number" && Number.isFinite(hp) && hp >= 0 && hp <= 0;
  });
  return allFainted ? "party_fainted" : null;
}

export function activeRosterAfter(rosterLength: number, priorTurns: TurnAction[][]): number[] {
  const active = [0, 1].filter((index) => index < rosterLength);
  const history = Array.isArray(priorTurns) ? priorTurns : [];
  for (const turn of history) {
    if (!Array.isArray(turn)) continue;
    for (let slot = 0; slot < turn.length && slot < active.length; slot++) {
      const action = turn[slot];
      if (!action || action.actionType !== Swap) continue;
      const target = action.swapTargetIndex ?? -1;
      if (target >= 0 && target < rosterLength) active[slot] = target;
    }
  }
  return active;
}

/**
 * Accept a move any locked chainling can legally cast. Auto-swaps and swap-moves
 * are not in the turn log, so binding the index to the reconstructed active slot
 * rejects honest moves. The forensic hash still catches a tampered resolver.
 */
function rosterMoveError(roster: LockedChainling[], moveIndex: number): string | null {
  if (!Number.isInteger(moveIndex) || moveIndex < 0) return "move_illegal";
  let sawIndex = false;
  let sawUnknown = false;
  let sawAp = false;
  for (const chainling of roster) {
    const moveIds = Array.isArray(chainling?.moveIds) ? chainling.moveIds : [];
    if (moveIndex >= moveIds.length || !moveIds[moveIndex]) continue;
    sawIndex = true;
    const moveId = moveIds[moveIndex];
    const stamped = Array.isArray(chainling.moveApCosts) ? chainling.moveApCosts[moveIndex] : undefined;
    const catalog = apCostForMove(moveId);
    const cost = typeof stamped === "number" && Number.isFinite(stamped) ? stamped : catalog;
    if (catalog === null || cost === null || cost !== catalog) {
      sawUnknown = true;
      continue;
    }
    if (cost < 0 || cost > PVP_AP_CAP) {
      sawAp = true;
      continue;
    }
    return null;
  }
  if (!sawIndex) return "move_illegal";
  if (sawUnknown) return "move_unknown";
  if (sawAp) return "ap_illegal";
  return "move_illegal";
}

export function validateTurnActions(
  seat: "a" | "b",
  chainlings: LockedChainling[],
  actions: TurnAction[],
  priorTurns: TurnAction[][] = [],
): string | null {
  const roster = Array.isArray(chainlings) ? chainlings : [];
  if (roster.length < 1) return "empty_party";
  if (roster.length > PVP_ROSTER_MAX) return "party_too_large";
  const active = activeRosterAfter(roster.length, priorTurns);
  if (!Array.isArray(actions) || actions.length !== active.length) return "action_count";
  const expectedTeam = seat === "a" ? 0 : 1;

  for (let slot = 0; slot < actions.length; slot++) {
    const action = actions[slot];
    if (!action || typeof action.actionType !== "number") return "action_type";
    if (action.teamIndex !== expectedTeam) return "seat_mismatch";
    const actorIndex = active[slot];
    const actor = roster[actorIndex];
    if (!actor) return "action_count";
    if (action.actionType === Pass) continue;
    if (action.actionType === Swap) {
      const target = action.swapTargetIndex ?? -1;
      if (target < 0 || target >= roster.length) return "swap_illegal";
      if (active.indexOf(target) >= 0) return "swap_illegal";
      if (roster[target]?.fainted === true) return "swap_illegal";
      continue;
    }
    if (action.actionType !== UseMove) return "action_type";
    const moveError = rosterMoveError(roster, action.moveIndex ?? -1);
    if (moveError) return moveError;
    if (action.targetSlotIndex !== undefined && action.targetSlotIndex !== -1) {
      if (action.targetSlotIndex < 0 || action.targetSlotIndex > 1) return "target_illegal";
    }
    if (action.targetTeamIndex !== undefined && action.targetTeamIndex !== -1) {
      if (action.targetTeamIndex !== 0 && action.targetTeamIndex !== 1) return "target_illegal";
    }
  }
  return null;
}

export function settleHashes(hashA: string | null, hashB: string | null): "pending" | "completed" | "disputed" {
  const a = (hashA ?? "").trim();
  const b = (hashB ?? "").trim();
  if (!a || !b) return "pending";
  return a === b ? "completed" : "disputed";
}

export interface ResultWindowSettlement {
  state: "pending" | "completed" | "disputed";
  winnerSeat: string | null;
}

/** Both hashes compare immediately. One hash at the window end is disputed, never a forfeit. */
export function settleResultWindow(
  hashA: string | null,
  hashB: string | null,
  winnerA: number | null,
  winnerB: number | null,
  windowElapsed: boolean,
): ResultWindowSettlement {
  const a = (hashA ?? "").trim();
  const b = (hashB ?? "").trim();
  const both = a.length > 0 && b.length > 0;
  if (both) {
    const claimsAgree = winnerA !== null && winnerA === winnerB && (winnerA === 0 || winnerA === 1 || winnerA === -2);
    if (a === b && claimsAgree) {
      return { state: "completed", winnerSeat: winnerSeatForTeam(winnerA) };
    }
    return { state: "disputed", winnerSeat: null };
  }
  if (windowElapsed && (a.length > 0 || b.length > 0)) {
    return { state: "disputed", winnerSeat: null };
  }
  return { state: "pending", winnerSeat: null };
}

export function winnerSeatForTeam(team: number | null): string | null {
  if (team === 0) return "a";
  if (team === 1) return "b";
  if (team === -2) return "draw";
  return null;
}

export interface MissSweep {
  missesA: number;
  missesB: number;
  state: "continue" | "forfeit_a" | "forfeit_b" | "cancelled";
}

export function applyDeadlineMisses(
  missesA: number,
  missesB: number,
  seatASubmitted: boolean,
  seatBSubmitted: boolean,
  threshold: number,
  hashesPresent = false,
): MissSweep {
  if (hashesPresent) {
    return { missesA, missesB, state: "continue" };
  }
  const nextA = seatASubmitted ? missesA : missesA + 1;
  const nextB = seatBSubmitted ? missesB : missesB + 1;
  const limit = Math.max(1, threshold);
  const aOut = nextA >= limit;
  const bOut = nextB >= limit;
  if (aOut && bOut) return { missesA: nextA, missesB: nextB, state: "cancelled" };
  if (aOut) return { missesA: nextA, missesB: nextB, state: "forfeit_a" };
  if (bOut) return { missesA: nextA, missesB: nextB, state: "forfeit_b" };
  return { missesA: nextA, missesB: nextB, state: "continue" };
}

export interface PairingIdentity {
  queueType: string;
  rulesetId: string;
  configHash: string;
  appVersion: string;
  resolverHash: string;
  rulesetHash: string;
  buildStamp: string;
  authUserId: string;
}

export function pairingCompatible(
  left: PairingIdentity,
  right: PairingIdentity,
  versionSkew: number,
): boolean {
  const authA = (left.authUserId ?? "").trim();
  const authB = (right.authUserId ?? "").trim();
  if (authA.length > 0 && authA === authB) return false;
  return left.queueType === right.queueType &&
    left.rulesetId === right.rulesetId &&
    left.configHash === right.configHash &&
    left.resolverHash === right.resolverHash &&
    left.rulesetHash === right.rulesetHash &&
    left.buildStamp === right.buildStamp &&
    versionsCompatible(left.appVersion, right.appVersion, versionSkew);
}
