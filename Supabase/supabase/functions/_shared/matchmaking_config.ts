/**
 * Authoritative casual matchmaking levers.
 * Keep the canonical string identical to BalanceConfigHashUtility.HashMatchmakingConfig.
 */

export interface MatchmakingLevers {
  configVersion: number;
  casualEnabled: boolean;
  rankedEnabled: boolean;
  requireWalletForCasual: boolean;
  requireWalletForRanked: boolean;
  queueTimeoutSeconds: number;
  turnForfeitMissedTurns: number;
  turnActionTimeLimitSeconds: number;
  maxAppVersionSkew: number;
  defaultCasualRulesetId: string;
  rematchCooldownSeconds: number;
  lobbyMaxPlayers: number;
  ratingBandStart: number;
  widenPerSecond: number;
  bandCap: number;
  resultWindowSeconds: number;
  lockDeadlineSeconds: number;
}

export const MATCHMAKING_CONFIG: MatchmakingLevers = {
  configVersion: 1,
  casualEnabled: true,
  rankedEnabled: false,
  requireWalletForCasual: false,
  requireWalletForRanked: true,
  queueTimeoutSeconds: 120,
  turnForfeitMissedTurns: 2,
  turnActionTimeLimitSeconds: 60,
  maxAppVersionSkew: 0,
  defaultCasualRulesetId: "pvp_casual",
  rematchCooldownSeconds: 30,
  lobbyMaxPlayers: 16,
  ratingBandStart: 100,
  widenPerSecond: 1,
  bandCap: 400,
  resultWindowSeconds: 45,
  lockDeadlineSeconds: 30,
};

export function matchmakingCanonical(cfg: MatchmakingLevers = MATCHMAKING_CONFIG): string {
  return [
    `matchmaking|v=${cfg.configVersion}`,
    `casual=${cfg.casualEnabled ? 1 : 0}`,
    `ranked=${cfg.rankedEnabled ? 1 : 0}`,
    `walletCasual=${cfg.requireWalletForCasual ? 1 : 0}`,
    `walletRanked=${cfg.requireWalletForRanked ? 1 : 0}`,
    `queueTimeout=${cfg.queueTimeoutSeconds}`,
    `forfeitMisses=${cfg.turnForfeitMissedTurns}`,
    `turnLimit=${cfg.turnActionTimeLimitSeconds}`,
    `versionSkew=${cfg.maxAppVersionSkew}`,
    `ruleset=${cfg.defaultCasualRulesetId ?? ""}`,
    `rematch=${cfg.rematchCooldownSeconds}`,
    `lobbyMax=${cfg.lobbyMaxPlayers}`,
    `ratingStart=${cfg.ratingBandStart}`,
    `widen=${cfg.widenPerSecond}`,
    `bandCap=${cfg.bandCap}`,
    `resultWindow=${cfg.resultWindowSeconds}`,
    `lockDeadline=${cfg.lockDeadlineSeconds}`,
  ].join("|");
}

export async function hashMatchmakingConfig(cfg: MatchmakingLevers = MATCHMAKING_CONFIG): Promise<string> {
  const bytes = new TextEncoder().encode(matchmakingCanonical(cfg));
  const hash = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(hash)).map((b) => b.toString(16).padStart(2, "0")).join("");
}

/** Exact match when skew is 0. A positive skew allows that many differing dot-segments. */
export function versionsCompatible(left: string, right: string, skew: number): boolean {
  const a = (left ?? "").trim();
  const b = (right ?? "").trim();
  if (!a || !b) return false;
  if (skew <= 0) return a === b;
  const aParts = a.split(".");
  const bParts = b.split(".");
  const count = Math.max(aParts.length, bParts.length);
  let diffs = 0;
  for (let i = 0; i < count; i++) {
    if ((aParts[i] ?? "") !== (bParts[i] ?? "")) diffs++;
  }
  return diffs <= skew;
}

export interface EnqueueCheck {
  queueType: string;
  rulesetId: string;
  configHash: string;
  appVersion: string;
  resolverHash: string;
  rulesetHash: string;
  buildStamp: string;
  walletLinked: boolean;
}

export function rejectEnqueue(cfg: MatchmakingLevers, serverHash: string, request: EnqueueCheck): string | null {
  const queueType = (request.queueType || "casual").trim().toLowerCase();
  if (queueType === "ranked") {
    return cfg.rankedEnabled ? "ranked_not_in_m0" : "ranked_disabled";
  }
  if (queueType !== "casual") return "queue_type_rejected";
  if (!cfg.casualEnabled) return "casual_disabled";
  if (cfg.requireWalletForCasual && !request.walletLinked) return "wallet_required";
  if (!request.rulesetId || request.rulesetId !== cfg.defaultCasualRulesetId) return "ruleset_rejected";
  if (!request.configHash || request.configHash !== serverHash) return "config_mismatch";
  if (!request.appVersion || !request.appVersion.trim()) return "app_version_required";
  if (!request.resolverHash || !request.resolverHash.trim()) return "resolver_hash_required";
  if (!request.rulesetHash || !request.rulesetHash.trim()) return "ruleset_hash_required";
  if (!request.buildStamp || !request.buildStamp.trim()) return "build_stamp_required";
  return null;
}
