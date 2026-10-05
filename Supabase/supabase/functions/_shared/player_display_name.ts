export const PLAYER_DISPLAY_NAME_MIN = 1;
export const PLAYER_DISPLAY_NAME_MAX = 16;
export const PLAYER_DISPLAY_NAME_TAKEN = "name_taken";
export const PLAYER_DISPLAY_NAME_INDEX = "ux_player_profiles_display_name_ci";

export function normalizePlayerDisplayName(value: string): string {
  return value.trim();
}

/// Case fold used by tests and client-side classification.
/// PostgreSQL lower(btrim(display_name)) is the source of truth.
export function playerDisplayNameKey(value: string): string {
  return normalizePlayerDisplayName(value).toLowerCase();
}

export function isPlayerDisplayNameLengthValid(value: string): boolean {
  const normalized = normalizePlayerDisplayName(value);
  return normalized.length >= PLAYER_DISPLAY_NAME_MIN && normalized.length <= PLAYER_DISPLAY_NAME_MAX;
}

/**
 * Two different profiles cannot hold the same name ignoring case.
 * The owning profile may re-save its own name, including a case-only change.
 * Null or blank existing names do not collide.
 * Grandfathered rows still count: a new claim is blocked by every holder.
 */
export function playerDisplayNamesConflict(
  ownerProfileId: string,
  otherProfileId: string,
  requestedName: string,
  existingName: string | null | undefined,
): boolean {
  if (existingName == null)
    return false;
  if (ownerProfileId && otherProfileId && ownerProfileId === otherProfileId)
    return false;

  const requested = playerDisplayNameKey(requestedName);
  const existing = playerDisplayNameKey(existingName);
  return requested.length > 0 && requested === existing;
}

/// True when the requested name is this profile's current name ignoring case.
/// That re-set is allowed even if other grandfathered rows share it.
export function isOwnDisplayNameReset(
  currentName: string | null | undefined,
  requestedName: string,
): boolean {
  const current = playerDisplayNameKey(currentName ?? "");
  const requested = playerDisplayNameKey(requestedName);
  return current.length > 0 && current === requested;
}

/**
 * Rule (a). A new claim is blocked when any other non-blank name matches,
 * grandfathered or enforced. Re-setting the caller's own current name is not
 * blocked. otherNames are the other profiles' display names.
 */
export function isPlayerDisplayNameClaimBlocked(
  ownerCurrentName: string | null | undefined,
  requestedName: string,
  otherNames: Array<string | null | undefined>,
): boolean {
  if (isOwnDisplayNameReset(ownerCurrentName, requestedName))
    return false;

  const requested = playerDisplayNameKey(requestedName);
  if (requested.length === 0)
    return false;

  return otherNames.some((name) => playerDisplayNameKey(name ?? "") === requested);
}

/**
 * Stamp display_name_claimed_at only when no other profile holds the name.
 * Shared grandfathered duplicates stay unstamped so the partial unique index
 * can be created without rewriting them.
 */
export function shouldStampDisplayNameClaim(
  requestedName: string,
  otherNames: Array<string | null | undefined>,
): boolean {
  const requested = playerDisplayNameKey(requestedName);
  if (requested.length === 0)
    return false;

  return !otherNames.some((name) => playerDisplayNameKey(name ?? "") === requested);
}

export function isPlayerDisplayNameUniqueViolation(
  error: { code?: string; message?: string; details?: string } | null | undefined,
): boolean {
  if (!error)
    return false;
  if (error.code === "23505")
    return true;

  const haystack = `${error.message ?? ""} ${error.details ?? ""}`.toLowerCase();
  return haystack.includes(PLAYER_DISPLAY_NAME_INDEX) || haystack.includes("duplicate key");
}
