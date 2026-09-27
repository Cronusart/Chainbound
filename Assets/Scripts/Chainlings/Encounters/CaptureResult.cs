using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Parameters for a capture attempt.
    /// </summary>
    [Serializable]
    public class CaptureParams
    {
        /// <summary>Species ID of the target Chainling.</summary>
        public string speciesId;

        /// <summary>Target's current HP.</summary>
        public int currentHP;

        /// <summary>Target's max HP.</summary>
        public int maxHP;

        /// <summary>Base capture rate for this species (0-100).</summary>
        public int baseCaptureRate;

        /// <summary>Status modifier (default 1.0).</summary>
        public float statusModifier = 1.0f;

        /// <summary>Device modifier (default 1.0).</summary>
        public float deviceModifier = 1.0f;

        /// <summary>Favor score for capture probability bonus. Negative = use FavorRuntime.</summary>
        public int favorScore = -1;

        /// <summary>Known Known move id to grant on success (exactly one).</summary>
        public string knownMoveId;

        /// <summary>Client idempotency key (required for profile path retries).</summary>
        public string idempotencyKey;

        /// <summary>Star level of the wild Chainling.</summary>
        public int stars;

        /// <summary>Legacy level field (fallback when stars unset).</summary>
        public int level;

        /// <summary>IVs [6].</summary>
        public int[] ivs;

        public bool entangledFlag;
        public ChainlingType entangledType = ChainlingType.None;
        public bool mutatedTypeFlag;
        public string mutationId = ChainlingVisualIdentity.NoneId;
        public string visualVariantId = ChainlingVisualIdentity.BaseVariantId;
    }

    /// <summary>
    /// Result of a capture attempt (profile-first Supabase path).
    /// </summary>
    [Serializable]
    public class CaptureResult
    {
        /// <summary>Whether the capture succeeded.</summary>
        public bool success;

        /// <summary>The calculated capture probability.</summary>
        public float probability;

        /// <summary>The capture roll value (0-1).</summary>
        public float roll;

        /// <summary>Supabase dossier id (profile-first path).</summary>
        public string backendChainlingId;

        /// <summary>Known Known move id granted on success.</summary>
        public string grantedKnownMoveId;

        /// <summary>move_items row id for the granted Known.</summary>
        public string grantedMoveItemId;

        /// <summary>True when the dossier was added to the active party.</summary>
        public bool isPartyMember;

        /// <summary>Party slot when isPartyMember; otherwise ignored.</summary>
        public int partySlot;

        /// <summary>True when the server replayed a prior idempotent result.</summary>
        public bool alreadyApplied;

        /// <summary>Error / miss reason if failed.</summary>
        public string errorMessage;

        public static CaptureResult FromError(string errorMessage)
        {
            return new CaptureResult
            {
                success = false,
                errorMessage = errorMessage ?? "Unknown error.",
            };
        }

        /// <summary>
        /// Writes capture fields onto a battle result for post-battle routing/copy.
        /// </summary>
        public void ApplyToBattleResult(BattleResult battleResult)
        {
            if (battleResult == null)
                return;

            battleResult.captureAttempted = true;
            battleResult.captureSucceeded = success;
            if (!success)
                return;

            battleResult.capturedBackendChainlingId = backendChainlingId ?? "";
            battleResult.capturedKnownMoveId = grantedKnownMoveId ?? "";
            // Profile-first capture does not mint Stamped moves.
            if (string.IsNullOrWhiteSpace(battleResult.mintedStampedMoveAssetId))
                battleResult.mintedStampedMoveAssetId = null;
        }
    }
}
