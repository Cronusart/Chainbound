// Keys written into player_profiles.progression_state by profile_set_progression_flag.
// The first segment is a lowercase namespace (world, intro, sequence, or a legacy
// undotted key). Later segments may carry mixed case because intro.guardrail.* keys
// embed GameObject names such as Intro_Collider1 (Step_EnableGameObject).
export const PROGRESSION_FLAG_KEY_MAX_LENGTH = 96;

const PROGRESSION_FLAG_KEY_RE = /^[a-z][a-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)*$/;

export function isProgressionFlagKeyValid(key: string): boolean {
  return key.length <= PROGRESSION_FLAG_KEY_MAX_LENGTH && PROGRESSION_FLAG_KEY_RE.test(key);
}
