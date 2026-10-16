using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Chainbound.Combat
{
    public readonly struct StampedModifierRoll
    {
        public readonly string sectionId;
        public readonly float effectValue;

        public StampedModifierRoll(string sectionId, float effectValue)
        {
            this.sectionId = sectionId;
            this.effectValue = effectValue;
        }
    }

    /// <summary>
    /// Modifier generation for Stamped and Stamped tiers.
    /// Primary method: VRF-derived (GenerateModifierFromVrf) using
    /// sha256(vrf_seed || action_type || move_id || owner || context_bytes).
    /// Callers should include <see cref="VendingConfigSO.generatorVersion"/> in contextBytes
    /// so rolls stay deterministic per config generation contract.
    /// </summary>
    public static class MoveModifierGenerator
    {
        public const int DefaultMaxAttempts = 24;

        // Legacy fallback pool used only when no catalog is provided.
        private static readonly string[] LegacyModifierPool = new[]
        {
            "2.1-a", "2.1-b", "2.1-c", "2.1-d", "2.1-e",
            "2.2-a", "2.2-b", "2.2-c", "2.2-d",
            "2.3-a", "2.3-b", "2.3-c", "2.3-d", "2.3-e",
            "3.1-a", "3.1-b", "3.1-c",
            "3.2-a",
            "6.1-a", "6.1-b", "6.1-c", "6.1-d", "6.1-e", "6.1-f",
            "6.2-a", "6.2-b", "6.2-c",
            "6.3-a", "6.3-b", "6.3-c", "6.3-d", "6.3-e",
            "6.4-a", "6.4-b", "6.4-c", "6.4-d", "6.4-e",
            "6.4-f", "6.4-g", "6.4-h", "6.4-i",
            "6.7-a", "6.7-b", "6.7-c",
            "7.1-a", "7.1-b", "7.1-c", "7.1-d",
            "7.2-a", "7.2-b", "7.2-c",
            "8.1-a", "8.1-b", "8.1-c", "8.1-d", "8.1-e",
        };

        private static readonly RarityTier[] RarityTiers =
        {
            RarityTier.Common,
            RarityTier.Uncommon,
            RarityTier.Rare,
            RarityTier.Epic,
            RarityTier.Legendary,
            RarityTier.Mythic,
        };

        /// <summary>
        /// Legacy generator that returns a deterministic section id from a static pool.
        /// Prefer catalog-based overloads.
        /// </summary>
        public static string GenerateModifierFromVrf(
            byte[] vrfSeed, byte actionType, ushort moveId,
            byte[] ownerPubkey, byte[] contextBytes)
        {
            byte[] derivedHash = ComputeVrfDerivedSeed(vrfSeed, actionType, moveId, ownerPubkey, contextBytes);
            int index = DeterministicIndex(derivedHash, 0, LegacyModifierPool.Length);
            return LegacyModifierPool[index];
        }

        /// <summary>
        /// Catalog-driven generation for authored MoveDefinition assets.
        /// Returns empty string when no valid modifier can be found.
        /// </summary>
        public static string GenerateModifierFromVrf(
            byte[] vrfSeed,
            byte actionType,
            ushort moveId,
            byte[] ownerPubkey,
            byte[] contextBytes,
            ModifierCatalogSO modifierCatalog,
            MoveDefinitionSO moveDefinition,
            IReadOnlyCollection<string> existingModifierSectionIds = null,
            int maxAttempts = 24,
            VendingConfigSO vendingConfig = null,
            ModifierBalanceOverlaySO overlay = null)
        {
            if (TryGenerateModifierFromVrf(
                    vrfSeed,
                    actionType,
                    moveId,
                    ownerPubkey,
                    contextBytes,
                    modifierCatalog,
                    moveDefinition,
                    existingModifierSectionIds,
                    out string sectionId,
                    out _,
                    maxAttempts,
                    vendingConfig,
                    overlay))
            {
                return sectionId;
            }

            return string.Empty;
        }

        /// <summary>
        /// Catalog-driven generation for runtime MoveInstance objects.
        /// Returns empty string when no valid modifier can be found.
        /// </summary>
        public static string GenerateModifierFromVrf(
            byte[] vrfSeed,
            byte actionType,
            ushort moveId,
            byte[] ownerPubkey,
            byte[] contextBytes,
            ModifierCatalogSO modifierCatalog,
            MoveInstance move,
            IReadOnlyCollection<string> existingModifierSectionIds = null,
            int maxAttempts = 24,
            VendingConfigSO vendingConfig = null,
            ModifierBalanceOverlaySO overlay = null)
        {
            if (TryGenerateModifierFromVrf(
                    vrfSeed,
                    actionType,
                    moveId,
                    ownerPubkey,
                    contextBytes,
                    modifierCatalog,
                    move,
                    existingModifierSectionIds,
                    out string sectionId,
                    out _,
                    maxAttempts,
                    vendingConfig,
                    overlay))
            {
                return sectionId;
            }

            return string.Empty;
        }

        public static bool TryGenerateModifierFromVrf(
            byte[] vrfSeed,
            byte actionType,
            ushort moveId,
            byte[] ownerPubkey,
            byte[] contextBytes,
            ModifierCatalogSO modifierCatalog,
            MoveDefinitionSO moveDefinition,
            IReadOnlyCollection<string> existingModifierSectionIds,
            out string sectionId,
            out string reason,
            int maxAttempts = 24,
            VendingConfigSO vendingConfig = null,
            ModifierBalanceOverlaySO overlay = null)
        {
            sectionId = string.Empty;
            if (!TryGenerateStampedModifiersFromVrf(
                    vrfSeed,
                    actionType,
                    moveId,
                    ownerPubkey,
                    contextBytes,
                    modifierCatalog,
                    moveDefinition,
                    1,
                    out IReadOnlyList<StampedModifierRoll> rolls,
                    out reason,
                    existingModifierSectionIds,
                    maxAttempts,
                    vendingConfig,
                    overlay))
            {
                return false;
            }

            sectionId = rolls[0].sectionId;
            return true;
        }

        /// <summary>
        /// Shared stamp derivation used by production VRF and deterministic development seeds.
        /// The complete rarity-qualified catalog is filtered through CanApply before each pick.
        /// If any requested slot has no eligible candidate, the whole stamp fails without fallback.
        /// Effect values use an independent derived hash and an inclusive [min,max] float roll.
        /// </summary>
        public static bool TryGenerateStampedModifiersFromVrf(
            byte[] vrfSeed,
            byte actionType,
            ushort moveId,
            byte[] ownerPubkey,
            byte[] contextBytes,
            ModifierCatalogSO modifierCatalog,
            MoveDefinitionSO moveDefinition,
            int requestedModifierCount,
            out IReadOnlyList<StampedModifierRoll> rolls,
            out string reason,
            IReadOnlyCollection<string> existingModifierSectionIds = null,
            int maxAttempts = DefaultMaxAttempts,
            VendingConfigSO vendingConfig = null,
            ModifierBalanceOverlaySO overlay = null)
        {
            var generated = new List<StampedModifierRoll>(2);
            rolls = generated;
            reason = string.Empty;

            if (moveDefinition == null)
            {
                reason = "Move definition is null.";
                return false;
            }
            if (modifierCatalog == null || modifierCatalog.GetAll() == null || modifierCatalog.GetAll().Count == 0)
            {
                reason = "Modifier catalog is required; unvalidated legacy fallback is disabled for stamping.";
                return false;
            }
            if (requestedModifierCount < 1 || requestedModifierCount > 2)
            {
                reason = "Stamp modifier count must be 1 or 2.";
                return false;
            }
            if (maxAttempts < 1)
            {
                reason = "Max attempts must be at least 1.";
                return false;
            }

            vendingConfig = ResolveVendingConfig(vendingConfig);
            overlay = ResolveOverlay(overlay);
            RarityTier rolledRarity = GenerateRarityFromVrf(
                vrfSeed, actionType, moveId, ownerPubkey, contextBytes, vendingConfig);
            List<ModifierDefinitionSO> rarityPool = BuildCandidatePool(
                modifierCatalog, rolledRarity, moveDefinition.category, overlay);

            var assigned = new List<string>();
            if (existingModifierSectionIds != null)
            {
                foreach (string existing in existingModifierSectionIds)
                {
                    if (!string.IsNullOrWhiteSpace(existing))
                        assigned.Add(existing.Trim());
                }
            }

            for (int slot = 0; slot < requestedModifierCount; slot++)
            {
                var eligible = new List<ModifierDefinitionSO>(rarityPool.Count);
                string lastRejection = string.Empty;
                for (int i = 0; i < rarityPool.Count; i++)
                {
                    ModifierDefinitionSO candidate = rarityPool[i];
                    ModifierValidationResult validation = ModifierValidationService.CanApply(
                        candidate, moveDefinition, modifierCatalog, assigned);
                    if (validation.isValid)
                        eligible.Add(candidate);
                    else
                        lastRejection = validation.reason;
                }

                if (eligible.Count == 0)
                {
                    reason = $"No eligible modifier exists for stamp slot {slot + 1} on move '{moveDefinition.moveId}'.";
                    if (!string.IsNullOrWhiteSpace(lastRejection))
                        reason += $" Last rejection: {lastRejection}";
                    return false;
                }

                byte[] pickContext = BuildDomainContext(contextBytes, "modifier-pick", slot);
                byte[] pickHash = ComputeVrfDerivedSeed(vrfSeed, actionType, moveId, ownerPubkey, pickContext);
                ModifierDefinitionSO picked = PickWeighted(eligible, overlay, pickHash, 0);
                if (picked == null || string.IsNullOrWhiteSpace(picked.sectionId))
                {
                    reason = $"Deterministic modifier pick failed for stamp slot {slot + 1}.";
                    return false;
                }

                byte[] valueContext = BuildDomainContext(contextBytes, "effect-value", slot);
                byte[] valueHash = ComputeVrfDerivedSeed(vrfSeed, actionType, moveId, ownerPubkey, valueContext);
                float effectValue = RollLockedEffectValue(picked, overlay, valueHash);
                generated.Add(new StampedModifierRoll(picked.sectionId.Trim(), effectValue));
                assigned.Add(picked.sectionId.Trim());
            }

            return true;
        }

        public static bool TryGenerateModifierFromVrf(
            byte[] vrfSeed,
            byte actionType,
            ushort moveId,
            byte[] ownerPubkey,
            byte[] contextBytes,
            ModifierCatalogSO modifierCatalog,
            MoveInstance move,
            IReadOnlyCollection<string> existingModifierSectionIds,
            out string sectionId,
            out string reason,
            int maxAttempts = 24,
            VendingConfigSO vendingConfig = null,
            ModifierBalanceOverlaySO overlay = null)
        {
            sectionId = string.Empty;
            reason = string.Empty;

            if (move == null)
            {
                reason = "Move instance is null.";
                return false;
            }

            if (modifierCatalog == null || modifierCatalog.GetAll() == null || modifierCatalog.GetAll().Count == 0)
            {
                sectionId = GenerateModifierFromVrf(vrfSeed, actionType, moveId, ownerPubkey, contextBytes);
                reason = "Modifier catalog missing; legacy static pool was used.";
                return !string.IsNullOrEmpty(sectionId);
            }

            vendingConfig = ResolveVendingConfig(vendingConfig);
            overlay = ResolveOverlay(overlay);

            var rolledRarity = GenerateRarityFromVrf(
                vrfSeed, actionType, moveId, ownerPubkey, contextBytes, vendingConfig);
            var pool = BuildCandidatePool(modifierCatalog, rolledRarity, move.category, overlay);
            if (pool.Count == 0)
            {
                reason = $"No modifiers found for rarity <= {rolledRarity}.";
                return false;
            }

            var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int attempts = Math.Max(1, Math.Min(maxAttempts, pool.Count * 2));
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                byte[] attemptContext = BuildAttemptContext(contextBytes, attempt);
                byte[] hash = ComputeVrfDerivedSeed(vrfSeed, actionType, moveId, ownerPubkey, attemptContext);
                var candidate = PickWeighted(pool, overlay, hash, 0);
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.sectionId))
                    continue;
                if (!attempted.Add(candidate.sectionId))
                    continue;

                var validation = ModifierValidationService.CanApply(
                    candidate,
                    move,
                    modifierCatalog,
                    existingModifierSectionIds);
                if (validation.isValid)
                {
                    sectionId = candidate.sectionId;
                    return true;
                }

                reason = validation.reason;
            }

            if (string.IsNullOrEmpty(reason))
                reason = "Unable to find a valid modifier after deterministic re-roll attempts.";
            return false;
        }

        /// <summary>
        /// Generate a rarity tier from a VRF seed using ECONOMY_01 basis-point thresholds.
        /// When <paramref name="vendingConfig"/> is null, resolves <see cref="BalanceConfigRuntime.Vending"/>.
        /// </summary>
        public static RarityTier GenerateRarityFromVrf(
            byte[] vrfSeed, byte actionType, ushort moveId,
            byte[] ownerPubkey, byte[] contextBytes,
            VendingConfigSO vendingConfig = null)
        {
            vendingConfig = ResolveVendingConfig(vendingConfig);
            byte[] derivedHash = ComputeVrfDerivedSeed(vrfSeed, actionType, moveId, ownerPubkey, contextBytes);
            int[] thresholds = vendingConfig.BuildCumulativeThresholds();
            int roll = DeterministicIndex(derivedHash, 4, VendingConfigSO.RollBasisPoints);

            int tierCount = Math.Min(thresholds.Length, RarityTiers.Length);
            for (int i = 0; i < tierCount; i++)
            {
                if (roll < thresholds[i])
                    return RarityTiers[i];
            }
            return RarityTier.Common;
        }

        /// <summary>
        /// Compute the VRF-derived hash: sha256(vrf_seed || action_type || move_id || owner || context).
        /// </summary>
        public static byte[] ComputeVrfDerivedSeed(
            byte[] vrfSeed, byte actionType, ushort moveId,
            byte[] ownerPubkey, byte[] contextBytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] seed = vrfSeed ?? Array.Empty<byte>();
                byte[] moveIdBytes = BitConverter.GetBytes(moveId);
                int ctxLen = contextBytes?.Length ?? 0;
                int ownerLen = ownerPubkey?.Length ?? 0;

                byte[] input = new byte[seed.Length + 1 + 2 + ownerLen + ctxLen];
                int offset = 0;
                if (seed.Length > 0)
                {
                    Buffer.BlockCopy(seed, 0, input, offset, seed.Length);
                    offset += seed.Length;
                }

                input[offset++] = actionType;
                Buffer.BlockCopy(moveIdBytes, 0, input, offset, 2);
                offset += 2;

                if (ownerPubkey != null && ownerLen > 0)
                {
                    Buffer.BlockCopy(ownerPubkey, 0, input, offset, ownerLen);
                    offset += ownerLen;
                }

                if (contextBytes != null && ctxLen > 0)
                    Buffer.BlockCopy(contextBytes, 0, input, offset, ctxLen);

                return sha.ComputeHash(input);
            }
        }

        // Legacy deterministic methods kept for migration compatibility.

        [Obsolete("Use GenerateModifierFromVrf for new upgrades. Kept for migration compatibility.")]
        public static string GenerateModifierSectionId(byte[] playerPubkeyBytes, long timestamp, ushort moveId)
        {
            byte[] seed = ComputeSeed(playerPubkeyBytes, timestamp, moveId);
            int index = DeterministicIndex(seed, 0, LegacyModifierPool.Length);
            return LegacyModifierPool[index];
        }

        [Obsolete("Use GenerateRarityFromVrf for new upgrades. Kept for migration compatibility.")]
        public static RarityTier GenerateModifierRarity(byte[] playerPubkeyBytes, long timestamp, ushort moveId)
        {
            byte[] seed = ComputeSeed(playerPubkeyBytes, timestamp, moveId);
            VendingConfigSO vendingConfig = ResolveVendingConfig(null);
            int[] thresholds = vendingConfig.BuildCumulativeThresholds();
            int roll = DeterministicIndex(seed, 4, VendingConfigSO.RollBasisPoints);

            int tierCount = Math.Min(thresholds.Length, RarityTiers.Length);
            for (int i = 0; i < tierCount; i++)
            {
                if (roll < thresholds[i])
                    return RarityTiers[i];
            }

            return RarityTier.Common;
        }

        public static byte[] ComputeSeed(byte[] playerPubkeyBytes, long timestamp, ushort moveId)
        {
            using (var sha = SHA256.Create())
            {
                byte[] pubkey = playerPubkeyBytes ?? Array.Empty<byte>();
                byte[] timestampBytes = BitConverter.GetBytes(timestamp);
                byte[] moveIdBytes = BitConverter.GetBytes(moveId);

                byte[] input = new byte[pubkey.Length + timestampBytes.Length + moveIdBytes.Length];
                int offset = 0;
                if (pubkey.Length > 0)
                {
                    Buffer.BlockCopy(pubkey, 0, input, offset, pubkey.Length);
                    offset += pubkey.Length;
                }

                Buffer.BlockCopy(timestampBytes, 0, input, offset, timestampBytes.Length);
                offset += timestampBytes.Length;
                Buffer.BlockCopy(moveIdBytes, 0, input, offset, moveIdBytes.Length);

                return sha.ComputeHash(input);
            }
        }

        private static VendingConfigSO ResolveVendingConfig(VendingConfigSO vendingConfig)
        {
            return vendingConfig != null ? vendingConfig : BalanceConfigRuntime.Vending;
        }

        private static ModifierBalanceOverlaySO ResolveOverlay(ModifierBalanceOverlaySO overlay)
        {
            return overlay != null ? overlay : BalanceConfigRuntime.ActiveModifierOverlay;
        }

        private static int DeterministicIndex(byte[] hash, int byteOffset, int range)
        {
            if (range <= 0)
                return 0;
            if (hash == null || hash.Length < byteOffset + 4)
                return 0;
            uint value = BitConverter.ToUInt32(hash, byteOffset);
            return (int)(value % (uint)range);
        }

        private static List<ModifierDefinitionSO> BuildCandidatePool(
            ModifierCatalogSO catalog,
            RarityTier rolledRarity,
            MoveCategory moveCategory,
            ModifierBalanceOverlaySO overlay)
        {
            var pool = new List<ModifierDefinitionSO>();
            var all = catalog.GetAll();
            if (all == null)
                return pool;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrWhiteSpace(def.sectionId))
                    continue;

                RarityTier minimumRarity = ResolveMinimumRarity(def, overlay);
                if (minimumRarity > rolledRarity)
                    continue;

                if (IsRetiredFromVending(def.sectionId) || ResolveWeight(def, overlay) <= 0)
                    continue;

                pool.Add(def);
            }

            return pool;
        }

        private static ModifierDefinitionSO PickWeighted(
            List<ModifierDefinitionSO> pool,
            ModifierBalanceOverlaySO overlay,
            byte[] hash,
            int byteOffset)
        {
            if (pool == null || pool.Count == 0)
                return null;

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
                totalWeight += ResolveWeight(pool[i], overlay);

            if (totalWeight <= 0)
            {
                int uniformIndex = DeterministicIndex(hash, byteOffset, pool.Count);
                return pool[uniformIndex];
            }

            int roll = DeterministicIndex(hash, byteOffset, totalWeight);
            int cumulative = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += ResolveWeight(pool[i], overlay);
                if (roll < cumulative)
                    return pool[i];
            }

            return pool[pool.Count - 1];
        }

        private static bool IsRetiredFromVending(string sectionId)
        {
            return sectionId.Equals("3.2-b", StringComparison.OrdinalIgnoreCase)
                || sectionId.Equals("3.2-c", StringComparison.OrdinalIgnoreCase)
                || sectionId.Equals("10.1-d", StringComparison.OrdinalIgnoreCase)
                || sectionId.Equals("10.2-b", StringComparison.OrdinalIgnoreCase)
                || sectionId.Equals("10.2-c", StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveWeight(ModifierDefinitionSO definition, ModifierBalanceOverlaySO overlay)
        {
            if (definition == null)
                return 0;
            if (overlay != null)
                return overlay.ResolveWeight(definition);
            return Math.Max(0, definition.weight);
        }

        private static RarityTier ResolveMinimumRarity(
            ModifierDefinitionSO definition, ModifierBalanceOverlaySO overlay)
        {
            if (definition == null)
                return RarityTier.Common;
            if (overlay != null)
                return overlay.ResolveMinimumRarity(definition);
            return definition.minimumRarity;
        }

        private static byte[] BuildAttemptContext(byte[] contextBytes, int attempt)
        {
            byte[] baseCtx = contextBytes ?? Array.Empty<byte>();
            byte[] attemptBytes = BitConverter.GetBytes(attempt);
            byte[] combined = new byte[baseCtx.Length + attemptBytes.Length];
            if (baseCtx.Length > 0)
                Buffer.BlockCopy(baseCtx, 0, combined, 0, baseCtx.Length);
            Buffer.BlockCopy(attemptBytes, 0, combined, baseCtx.Length, attemptBytes.Length);
            return combined;
        }

        private static byte[] BuildDomainContext(byte[] contextBytes, string domain, int slot)
        {
            byte[] baseContext = contextBytes ?? Array.Empty<byte>();
            byte[] domainBytes = System.Text.Encoding.UTF8.GetBytes(domain ?? string.Empty);
            byte[] slotBytes = BitConverter.GetBytes(slot);
            byte[] combined = new byte[baseContext.Length + 1 + domainBytes.Length + slotBytes.Length];
            if (baseContext.Length > 0)
                Buffer.BlockCopy(baseContext, 0, combined, 0, baseContext.Length);
            int offset = baseContext.Length;
            combined[offset++] = 0;
            if (domainBytes.Length > 0)
            {
                Buffer.BlockCopy(domainBytes, 0, combined, offset, domainBytes.Length);
                offset += domainBytes.Length;
            }
            Buffer.BlockCopy(slotBytes, 0, combined, offset, slotBytes.Length);
            return combined;
        }

        private static float RollLockedEffectValue(
            ModifierDefinitionSO definition,
            ModifierBalanceOverlaySO overlay,
            byte[] hash)
        {
            float min = overlay != null ? overlay.ResolveEffectMin(definition) : definition.effectMin;
            float max = overlay != null ? overlay.ResolveEffectMax(definition) : definition.effectMax;
            if (max < min)
            {
                float swap = min;
                min = max;
                max = swap;
            }
            if (Math.Abs(max - min) < 0.0001f)
                return min;

            uint raw = hash != null && hash.Length >= 8 ? BitConverter.ToUInt32(hash, 4) : 0u;
            double unitInclusive = raw / (double)uint.MaxValue;
            return (float)Math.Round(min + ((max - min) * unitInclusive), 4, MidpointRounding.AwayFromZero);
        }
    }
}
