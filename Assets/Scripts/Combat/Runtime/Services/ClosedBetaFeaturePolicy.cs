using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>Closed-beta allowlist for player-submittable on-chain instructions.</summary>
    public static class ClosedBetaFeaturePolicy
    {
        public const string DeniedMessage =
            "This on-chain action is disabled for the closed beta. Gameplay rewards remain off-chain.";

        private static readonly HashSet<string> AllowedPlayerInstructions =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "create_profile",
                "create_move_vault",
                "initialize_chainling_vault",
                "initialize_equipped_registry",
                "update_position"
            };

        public static bool IsPlayerInstructionAllowed(string instructionName)
        {
            return !string.IsNullOrWhiteSpace(instructionName) &&
                   AllowedPlayerInstructions.Contains(instructionName);
        }

        public static string DenialFor(string instructionName)
        {
            return string.IsNullOrWhiteSpace(instructionName)
                ? DeniedMessage
                : $"{DeniedMessage} Instruction: {instructionName}.";
        }
    }
}
