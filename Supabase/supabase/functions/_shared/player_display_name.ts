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
