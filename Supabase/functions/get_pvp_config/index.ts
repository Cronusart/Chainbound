import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { jsonResponse, readJsonBody, requirePost } from "../_shared/account_helpers.ts";
import { hashMatchmakingConfig, MATCHMAKING_CONFIG } from "../_shared/matchmaking_config.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  await readJsonBody(req);

  const cfg = MATCHMAKING_CONFIG;
  const configHash = await hashMatchmakingConfig(cfg);
  return jsonResponse({
    success: true,
    config_version: cfg.configVersion,
    config_hash: configHash,
    casual_enabled: cfg.casualEnabled,
    ranked_enabled: cfg.rankedEnabled,
    require_wallet_for_casual: cfg.requireWalletForCasual,
    require_wallet_for_ranked: cfg.requireWalletForRanked,
    queue_timeout_seconds: cfg.queueTimeoutSeconds,
    turn_forfeit_missed_turns: cfg.turnForfeitMissedTurns,
    turn_action_time_limit_seconds: cfg.turnActionTimeLimitSeconds,
    max_app_version_skew: cfg.maxAppVersionSkew,
    default_casual_ruleset_id: cfg.defaultCasualRulesetId,
    rematch_cooldown_seconds: cfg.rematchCooldownSeconds,
    lobby_max_players: cfg.lobbyMaxPlayers,
    rating_band_start: cfg.ratingBandStart,
    widen_per_second: cfg.widenPerSecond,
    band_cap: cfg.bandCap,
    result_window_seconds: cfg.resultWindowSeconds,
    lock_deadline_seconds: cfg.lockDeadlineSeconds,
  });
});
