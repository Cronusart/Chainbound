using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// Typed capture effects derived from the display/offline item catalog.
    /// Production services resolve the same IDs from the authoritative server catalog.
    /// </summary>
    public static class CaptureItemCatalog
    {
        public static CaptureEffectVector ToEffectVector(PlayerItemDefinition definition)
        {
            if (definition == null)
                return CaptureEffectVector.None;

            return new CaptureEffectVector(
                definition.captureScanRating,
                definition.captureStabilityBasisPoints,
                definition.captureBandWidthBasisPoints,
                definition.captureLockRateBasisPoints,
                definition.captureTimeBasisPoints);
        }

        public static bool TryResolveOrb(string itemId, out CaptureOrbProfile profile)
        {
            profile = null;
            if (!PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition definition) ||
                definition == null ||
                definition.effectFamily != PlayerItemEffectFamily.CaptureTool)
            {
                return false;
            }

            profile = new CaptureOrbProfile
            {
                OrbId = definition.itemId,
                Effects = ToEffectVector(definition),
                EntanglesOnSuccess = definition.entanglesOnCapture,
            };
            return true;
        }

        public static bool IsUtilityGear(PlayerItemDefinition definition, string slot = null)
        {
            if (definition == null || definition.effectFamily != PlayerItemEffectFamily.UtilityGear)
                return false;

            return string.IsNullOrWhiteSpace(slot) ||
                   string.Equals(definition.utilitySlot, slot, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCaptureBoost(PlayerItemDefinition definition)
        {
            return definition != null &&
                   definition.effectFamily == PlayerItemEffectFamily.CaptureBoost &&
                   definition.captureBoostDurationSeconds > 0;
        }
    }
}
