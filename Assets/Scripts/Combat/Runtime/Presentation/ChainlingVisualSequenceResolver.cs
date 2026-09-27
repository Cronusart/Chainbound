namespace Chainbound.Combat
{
    public enum ChainlingAnimSlot
    {
        Idle,
        IdleFlourish,
        SwapIn,
        Melee,
        Ranged,
        SwapOut,
        Ko,
        Hit,
        AttackerKoTaunt
    }

    public readonly struct ResolvedChainlingVisualClip
    {
        public readonly SpriteAnimationSequence sequence;
        public readonly float fps;
        public readonly int markerFrame;
        public readonly ChainlingVisualDefinitionSO sourceDefinition;

        public ResolvedChainlingVisualClip(
            SpriteAnimationSequence sequence,
            float fps,
            int markerFrame = -1,
            ChainlingVisualDefinitionSO sourceDefinition = null)
        {
            this.sequence = sequence;
            this.fps = fps > 0f ? fps : 1f;
            this.markerFrame = markerFrame;
            this.sourceDefinition = sourceDefinition;
        }

        public bool HasSequence => sequence != null && sequence.Length > 0;
    }

    public static class ChainlingVisualSequenceResolver
    {
        public static ResolvedChainlingVisualClip Resolve(ResolvedChainlingVisualIdentity resolved, ChainlingAnimSlot slot)
        {
            CombatVisualProfileSO profile = resolved != null ? resolved.baseProfile : null;
            switch (slot)
            {
                case ChainlingAnimSlot.Idle:
                    if (TryFromProfileAlternate(resolved, slot, out var idleAlternate))
                        return idleAlternate;
                    if (resolved != null && resolved.idleStyle != null && resolved.idleStyle.HasSequence)
                        return FromPose(resolved.idleStyle);
                    return FromProfile(profile != null ? profile.idleSequence : null, profile != null ? profile.idleFPS : 4f);

                case ChainlingAnimSlot.IdleFlourish:
                    if (TryFromProfileAlternate(resolved, slot, out var flourishAlternate))
                        return flourishAlternate;
                    return FromPose(resolved != null ? resolved.idleFlourish : null);

                case ChainlingAnimSlot.SwapIn:
                    if (TryFromProfileAlternate(resolved, slot, out var swapInAlternate))
                        return swapInAlternate;
                    if (resolved != null && resolved.swapInFx != null && resolved.swapInFx.HasBodySequence)
                        return FromFx(resolved.swapInFx);
                    return FromProfile(profile != null ? profile.swapInSequence : null, profile != null ? profile.swapInFPS : 10f);

                case ChainlingAnimSlot.Melee:
                    if (TryFromProfileAlternate(resolved, slot, out var meleeAlternate))
                        return meleeAlternate;
                    if (resolved != null && resolved.meleeAttackStyle != null && resolved.meleeAttackStyle.HasSequence)
                        return FromPose(resolved.meleeAttackStyle, resolved.meleeAttackStyle.markerFrame);
                    return FromProfile(profile != null ? profile.meleeAttackSequence : null, profile != null ? profile.meleeAttackFPS : 12f);

                case ChainlingAnimSlot.Ranged:
                    if (TryFromProfileAlternate(resolved, slot, out var rangedAlternate))
                        return rangedAlternate;
                    if (resolved != null && resolved.rangedAttackStyle != null && resolved.rangedAttackStyle.HasSequence)
                        return FromPose(resolved.rangedAttackStyle, resolved.rangedAttackStyle.markerFrame);
                    return FromProfile(profile != null ? profile.rangedAttackSequence : null, profile != null ? profile.rangedAttackFPS : 10f);

                case ChainlingAnimSlot.SwapOut:
                    if (TryFromProfileAlternate(resolved, slot, out var swapOutAlternate))
                        return swapOutAlternate;
                    if (resolved != null && resolved.swapOutFx != null && resolved.swapOutFx.HasBodySequence)
                        return FromFx(resolved.swapOutFx);
                    return FromProfile(profile != null ? profile.swapOutSequence : null, profile != null ? profile.swapOutFPS : 10f);

                case ChainlingAnimSlot.Ko:
                    if (TryFromProfileAlternate(resolved, slot, out var koAlternate))
                        return koAlternate;
                    if (resolved != null && resolved.koFx != null && resolved.koFx.HasBodySequence)
                        return FromFx(resolved.koFx);
                    ResolvedChainlingVisualClip profileKo = FromProfile(profile != null ? profile.koSequence : null, profile != null ? profile.koFPS : 10f);
                    return profileKo.HasSequence
                        ? profileKo
                        : FromProfile(profile != null ? profile.swapOutSequence : null, profile != null ? profile.swapOutFPS : 10f);

                case ChainlingAnimSlot.Hit:
                    if (TryFromProfileAlternate(resolved, slot, out var hitAlternate))
                        return hitAlternate;
                    if (resolved != null && resolved.hitReactionStyle != null && resolved.hitReactionStyle.HasSequence)
                        return FromPose(resolved.hitReactionStyle);
                    return FromProfile(profile != null ? profile.hitSequence : null, profile != null ? profile.hitFPS : 8f);

                case ChainlingAnimSlot.AttackerKoTaunt:
                    if (TryFromProfileAlternate(resolved, slot, out var attackerKoAlternate))
                        return attackerKoAlternate;
                    return FromPose(resolved != null ? resolved.attackerKoStyle : null);

                default:
                    return default;
            }
        }

        public static bool IsVariantCompatible(ChainlingVisualDefinitionSO definition, string speciesId, string visualVariantId)
        {
            return definition == null || definition.IsCompatibleWith(speciesId, visualVariantId);
        }

        private static bool TryFromProfileAlternate(ResolvedChainlingVisualIdentity resolved, ChainlingAnimSlot slot, out ResolvedChainlingVisualClip clip)
        {
            clip = default;
            if (resolved == null || resolved.baseProfile == null || resolved.identity == null)
                return false;

            string styleId = GetEquippedStyleId(resolved.identity, slot);
            if (!resolved.baseProfile.TryGetAnimationAlternate(slot, styleId, out CombatVisualAnimationAlternate alternate))
                return false;

            clip = new ResolvedChainlingVisualClip(alternate.sequence, alternate.ResolvedFps, alternate.markerFrame);
            return true;
        }

        private static string GetEquippedStyleId(ChainlingVisualIdentity identity, ChainlingAnimSlot slot)
        {
            switch (slot)
            {
                case ChainlingAnimSlot.Idle: return identity.idleStyleId;
                case ChainlingAnimSlot.IdleFlourish: return identity.idleFlourishId;
                case ChainlingAnimSlot.SwapIn: return identity.swapInFxId;
                case ChainlingAnimSlot.Melee: return identity.meleeAttackStyleId;
                case ChainlingAnimSlot.Ranged: return identity.rangedAttackStyleId;
                case ChainlingAnimSlot.SwapOut: return identity.swapOutFxId;
                case ChainlingAnimSlot.Ko: return identity.koFxId;
                case ChainlingAnimSlot.Hit: return identity.hitReactionStyleId;
                case ChainlingAnimSlot.AttackerKoTaunt: return identity.attackerKoStyleId;
                default: return ChainlingVisualIdentity.NoneId;
            }
        }

        private static ResolvedChainlingVisualClip FromProfile(SpriteAnimationSequence sequence, float fps)
        {
            return new ResolvedChainlingVisualClip(sequence, fps);
        }

        private static ResolvedChainlingVisualClip FromFx(ChainlingFxDefinitionSO definition)
        {
            return definition != null
                ? new ResolvedChainlingVisualClip(definition.bodySequence, definition.bodyFps, sourceDefinition: definition)
                : default;
        }

        private static ResolvedChainlingVisualClip FromPose(ChainlingPoseDefinitionSO definition, int markerFrame = -1)
        {
            return definition != null
                ? new ResolvedChainlingVisualClip(definition.sequence, definition.fps, markerFrame, definition)
                : default;
        }
    }
}
