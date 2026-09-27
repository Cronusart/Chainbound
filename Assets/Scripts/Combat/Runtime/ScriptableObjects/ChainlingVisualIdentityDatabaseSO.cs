using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingVisualIdentityDatabase", menuName = "Chainbound/Combat/Chainlings/Cosmetics/Identity Database")]
    public class ChainlingVisualIdentityDatabaseSO : ScriptableObject
    {
        [Header("Persistent Presentation")]
        [SerializeField] private List<ChainlingAuraDefinitionSO> auras = new List<ChainlingAuraDefinitionSO>();
        [SerializeField] private List<ChainlingHudCardVariantDefinitionSO> hudCardVariants = new List<ChainlingHudCardVariantDefinitionSO>();
        [SerializeField] private List<ChainlingPartyColumnVariantDefinitionSO> partyColumnVariants = new List<ChainlingPartyColumnVariantDefinitionSO>();

        [Header("Swap And KO FX")]
        [FormerlySerializedAs("spawnFx")]
        [SerializeField] private List<ChainlingSwapInFxDefinitionSO> swapInFx = new List<ChainlingSwapInFxDefinitionSO>();
        [FormerlySerializedAs("koSwapFx")]
        [SerializeField] private List<ChainlingSwapOutFxDefinitionSO> swapOutFx = new List<ChainlingSwapOutFxDefinitionSO>();
        [FormerlySerializedAs("defenderKoFx")]
        [SerializeField] private List<ChainlingKoFxDefinitionSO> koFx = new List<ChainlingKoFxDefinitionSO>();

        [Header("Pose Overrides")]
        [SerializeField] private List<ChainlingVictoryPoseDefinitionSO> victoryPoses = new List<ChainlingVictoryPoseDefinitionSO>();
        [SerializeField] private List<ChainlingIdleStyleDefinitionSO> idleStyles = new List<ChainlingIdleStyleDefinitionSO>();
        [SerializeField] private List<ChainlingIdleFlourishDefinitionSO> idleFlourishes = new List<ChainlingIdleFlourishDefinitionSO>();

        [Header("Body Animation Overrides")]
        [SerializeField] private List<ChainlingMeleeAttackStyleDefinitionSO> meleeAttackStyles = new List<ChainlingMeleeAttackStyleDefinitionSO>();
        [SerializeField] private List<ChainlingRangedAttackStyleDefinitionSO> rangedAttackStyles = new List<ChainlingRangedAttackStyleDefinitionSO>();
        [SerializeField] private List<ChainlingHitReactionStyleDefinitionSO> hitReactionStyles = new List<ChainlingHitReactionStyleDefinitionSO>();
        [FormerlySerializedAs("enemyKoStyles")]
        [SerializeField] private List<ChainlingAttackerKoStyleDefinitionSO> attackerKoStyles = new List<ChainlingAttackerKoStyleDefinitionSO>();

        public IReadOnlyList<ChainlingAuraDefinitionSO> Auras => auras;
        public IReadOnlyList<ChainlingHudCardVariantDefinitionSO> HudCardVariants => hudCardVariants;
        public IReadOnlyList<ChainlingPartyColumnVariantDefinitionSO> PartyColumnVariants => partyColumnVariants;
        public IReadOnlyList<ChainlingSwapInFxDefinitionSO> SwapInFx => swapInFx;
        public IReadOnlyList<ChainlingSwapOutFxDefinitionSO> SwapOutFx => swapOutFx;
        public IReadOnlyList<ChainlingKoFxDefinitionSO> KoFx => koFx;
        public IReadOnlyList<ChainlingVictoryPoseDefinitionSO> VictoryPoses => victoryPoses;
        public IReadOnlyList<ChainlingIdleStyleDefinitionSO> IdleStyles => idleStyles;
        public IReadOnlyList<ChainlingIdleFlourishDefinitionSO> IdleFlourishes => idleFlourishes;
        public IReadOnlyList<ChainlingMeleeAttackStyleDefinitionSO> MeleeAttackStyles => meleeAttackStyles;
        public IReadOnlyList<ChainlingRangedAttackStyleDefinitionSO> RangedAttackStyles => rangedAttackStyles;
        public IReadOnlyList<ChainlingHitReactionStyleDefinitionSO> HitReactionStyles => hitReactionStyles;
        public IReadOnlyList<ChainlingAttackerKoStyleDefinitionSO> AttackerKoStyles => attackerKoStyles;

        public ChainlingAuraDefinitionSO GetAura(string id)
        {
            return Resolve(auras, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingHudCardVariantDefinitionSO GetHudCardVariant(string id)
        {
            return Resolve(hudCardVariants, ChainlingVisualIdentity.NormalizeHudCardVariantId(id), ChainlingVisualIdentity.DefaultId);
        }

        public ChainlingPartyColumnVariantDefinitionSO GetPartyColumnVariant(string id)
        {
            return Resolve(partyColumnVariants, ChainlingVisualIdentity.NormalizePartyColumnVariantId(id), ChainlingVisualIdentity.DefaultId);
        }

        public ChainlingSwapInFxDefinitionSO GetSwapInFx(string id)
        {
            return Resolve(swapInFx, id, ChainlingVisualIdentity.DefaultId);
        }

        public ChainlingSwapInFxDefinitionSO GetSwapInFx(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(swapInFx, id, ChainlingVisualIdentity.DefaultId, speciesId, visualVariantId);
        }

        public ChainlingSwapOutFxDefinitionSO GetSwapOutFx(string id)
        {
            return Resolve(swapOutFx, id, ChainlingVisualIdentity.DefaultId);
        }

        public ChainlingSwapOutFxDefinitionSO GetSwapOutFx(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(swapOutFx, id, ChainlingVisualIdentity.DefaultId, speciesId, visualVariantId);
        }

        public ChainlingKoFxDefinitionSO GetKoFx(string id)
        {
            return Resolve(koFx, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingKoFxDefinitionSO GetKoFx(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(koFx, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        public ChainlingVictoryPoseDefinitionSO GetVictoryPose(string id)
        {
            return Resolve(victoryPoses, id, ChainlingVisualIdentity.DefaultId);
        }

        public ChainlingVictoryPoseDefinitionSO GetVictoryPose(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(victoryPoses, id, ChainlingVisualIdentity.DefaultId, speciesId, visualVariantId);
        }

        public ChainlingIdleStyleDefinitionSO GetIdleStyle(string id)
        {
            return Resolve(idleStyles, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingIdleStyleDefinitionSO GetIdleStyle(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(idleStyles, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        public ChainlingIdleFlourishDefinitionSO GetIdleFlourish(string id)
        {
            return Resolve(idleFlourishes, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingIdleFlourishDefinitionSO GetIdleFlourish(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(idleFlourishes, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        public ChainlingMeleeAttackStyleDefinitionSO GetMeleeAttackStyle(string id)
        {
            return Resolve(meleeAttackStyles, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingMeleeAttackStyleDefinitionSO GetMeleeAttackStyle(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(meleeAttackStyles, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        public ChainlingRangedAttackStyleDefinitionSO GetRangedAttackStyle(string id)
        {
            return Resolve(rangedAttackStyles, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingRangedAttackStyleDefinitionSO GetRangedAttackStyle(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(rangedAttackStyles, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        public ChainlingHitReactionStyleDefinitionSO GetHitReactionStyle(string id)
        {
            return Resolve(hitReactionStyles, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingHitReactionStyleDefinitionSO GetHitReactionStyle(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(hitReactionStyles, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        public ChainlingAttackerKoStyleDefinitionSO GetAttackerKoStyle(string id)
        {
            return Resolve(attackerKoStyles, id, ChainlingVisualIdentity.NoneId);
        }

        public ChainlingAttackerKoStyleDefinitionSO GetAttackerKoStyle(string id, string speciesId, string visualVariantId)
        {
            return ResolveCompatible(attackerKoStyles, id, ChainlingVisualIdentity.NoneId, speciesId, visualVariantId);
        }

        private static T Resolve<T>(IList<T> entries, string id, string fallbackId)
            where T : ChainlingVisualDefinitionSO
        {
            if (entries == null || entries.Count == 0)
                return null;

            string normalizedId = ChainlingVisualIdentity.NormalizeId(id, fallbackId);
            T exact = Find(entries, normalizedId);
            if (exact != null)
                return exact;

            if (!string.Equals(normalizedId, fallbackId, StringComparison.Ordinal))
                return Find(entries, fallbackId);

            return null;
        }

        private static T Find<T>(IList<T> entries, string id)
            where T : ChainlingVisualDefinitionSO
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static T ResolveCompatible<T>(
            IList<T> entries,
            string id,
            string fallbackId,
            string speciesId,
            string visualVariantId)
            where T : ChainlingVisualDefinitionSO
        {
            if (entries == null || entries.Count == 0)
                return null;

            string normalizedId = ChainlingVisualIdentity.NormalizeId(id, fallbackId);
            T exact = FindCompatible(entries, normalizedId, speciesId, visualVariantId);
            if (exact != null)
                return exact;

            if (!string.Equals(normalizedId, fallbackId, StringComparison.Ordinal))
                return FindCompatible(entries, fallbackId, speciesId, visualVariantId);

            return null;
        }

        private static T FindCompatible<T>(IList<T> entries, string id, string speciesId, string visualVariantId)
            where T : ChainlingVisualDefinitionSO
        {
            T entry = Find(entries, id);
            return entry != null && entry.IsCompatibleWith(speciesId, visualVariantId) ? entry : null;
        }
    }
}
