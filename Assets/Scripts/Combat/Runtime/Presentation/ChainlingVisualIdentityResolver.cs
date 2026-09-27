namespace Chainbound.Combat
{
    public static class ChainlingVisualIdentityResolver
    {
        public static ResolvedChainlingVisualIdentity Resolve(
            string speciesId,
            ChainlingVisualIdentity identity,
            CombatVisualDatabaseSO visualDatabase,
            ChainlingVisualIdentityDatabaseSO identityDatabase)
        {
            return Resolve(speciesId, identity, visualDatabase, identityDatabase, ChainlingType.None);
        }

        public static ResolvedChainlingVisualIdentity Resolve(
            string speciesId,
            ChainlingVisualIdentity identity,
            CombatVisualDatabaseSO visualDatabase,
            ChainlingVisualIdentityDatabaseSO identityDatabase,
            ChainlingType entangledType)
        {
            ChainlingVisualIdentity normalized = ChainlingVisualIdentity.CloneOrDefault(identity);
            var resolved = new ResolvedChainlingVisualIdentity
            {
                speciesId = speciesId != null ? speciesId.Trim() : string.Empty,
                identity = normalized,
                entangledType = entangledType,
                baseProfile = visualDatabase != null
                    ? visualDatabase.GetProfile(speciesId, normalized.visualVariantId, entangledType)
                    : null,
            };
            resolved.visualVariantId = normalized.visualVariantId;

            if (identityDatabase == null)
                return resolved;

            resolved.aura = identityDatabase.GetAura(normalized.auraId);
            resolved.hudCardVariant = identityDatabase.GetHudCardVariant(normalized.hudCardVariantId);
            resolved.partyColumnVariant = identityDatabase.GetPartyColumnVariant(normalized.partyColumnVariantId);
            resolved.swapInFx = identityDatabase.GetSwapInFx(normalized.swapInFxId, resolved.speciesId, resolved.visualVariantId);
            resolved.swapOutFx = identityDatabase.GetSwapOutFx(normalized.swapOutFxId, resolved.speciesId, resolved.visualVariantId);
            resolved.koFx = identityDatabase.GetKoFx(normalized.koFxId, resolved.speciesId, resolved.visualVariantId);
            resolved.victoryPose = identityDatabase.GetVictoryPose(normalized.victoryPoseId, resolved.speciesId, resolved.visualVariantId);
            resolved.idleStyle = identityDatabase.GetIdleStyle(normalized.idleStyleId, resolved.speciesId, resolved.visualVariantId);
            resolved.idleFlourish = identityDatabase.GetIdleFlourish(normalized.idleFlourishId, resolved.speciesId, resolved.visualVariantId);
            resolved.meleeAttackStyle = identityDatabase.GetMeleeAttackStyle(normalized.meleeAttackStyleId, resolved.speciesId, resolved.visualVariantId);
            resolved.rangedAttackStyle = identityDatabase.GetRangedAttackStyle(normalized.rangedAttackStyleId, resolved.speciesId, resolved.visualVariantId);
            resolved.hitReactionStyle = identityDatabase.GetHitReactionStyle(normalized.hitReactionStyleId, resolved.speciesId, resolved.visualVariantId);
            resolved.attackerKoStyle = identityDatabase.GetAttackerKoStyle(normalized.attackerKoStyleId, resolved.speciesId, resolved.visualVariantId);
            return resolved;
        }
    }

    public sealed class ResolvedChainlingVisualIdentity
    {
        public string speciesId;
        public string visualVariantId;
        public ChainlingType entangledType;
        public ChainlingVisualIdentity identity;
        public CombatVisualProfileSO baseProfile;
        public ChainlingAuraDefinitionSO aura;
        public ChainlingHudCardVariantDefinitionSO hudCardVariant;
        public ChainlingPartyColumnVariantDefinitionSO partyColumnVariant;
        public ChainlingSwapInFxDefinitionSO swapInFx;
        public ChainlingSwapOutFxDefinitionSO swapOutFx;
        public ChainlingKoFxDefinitionSO koFx;
        public ChainlingVictoryPoseDefinitionSO victoryPose;
        public ChainlingIdleStyleDefinitionSO idleStyle;
        public ChainlingIdleFlourishDefinitionSO idleFlourish;
        public ChainlingMeleeAttackStyleDefinitionSO meleeAttackStyle;
        public ChainlingRangedAttackStyleDefinitionSO rangedAttackStyle;
        public ChainlingHitReactionStyleDefinitionSO hitReactionStyle;
        public ChainlingAttackerKoStyleDefinitionSO attackerKoStyle;
    }
}
