using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class BattleIntroVfxProfileOverride
    {
        public string speciesId;
        public string visualVariantId;
        public BattleIntroVfxProfileSO profile;
    }

    [CreateAssetMenu(fileName = "BattleIntroPresentation", menuName = "Chainbound/Combat/Presentation/Battle Intro/Presentation")]
    public class BattleIntroPresentationSO : ScriptableObject
    {
        [Header("Center Staging")]
        [Tooltip("Show the enemy lead at center screen during dialogue, then slide to their slot.")]
        public bool useCenterStaging;

        [Tooltip("Duration of the slide from center to the creature's default slot.")]
        [Min(0f)]
        public float centerSlideDuration = 0.4f;

        public Ease centerSlideEase = Ease.InOutCubic;

        [Tooltip("Pause after dialogue completes before the center-staged creature slides to its slot.")]
        [Min(0f)]
        public float postDialogueDelay = 0.3f;

        [Header("Spawn Sequencing")]
        [Tooltip("When true, the rest of the active battle lineup stays hidden until dialogue + center staging finishes.")]
        public bool delayPlayerPartyUntilAfterDialogue = true;

        [Tooltip("Standard wait after starting one spawn before starting the next intro spawn. Spawn FX may continue playing after this delay.")]
        [Min(0f)]
        public float spawnAdvanceDelaySeconds = 0.2f;

        [Tooltip("Optional player-party spawn cadence. Negative uses Spawn Advance Delay Seconds.")]
        public float playerSpawnAdvanceDelaySeconds = -1f;

        [Tooltip("Full span between dual wilds when center-staging both. Lead/support are placed at center ± (offset * 0.5). Ignored for single-wild (lead stays on center).")]
        public Vector3 secondaryCenterOffset = new Vector3(2.5f, 0f, -0.35f);

        [Header("Intro Message")]
        [Tooltip("When false, the local battle-intro message cannot be skipped.")]
        public bool allowSkipIntroMessage = true;

        [Tooltip("Copy / trait-color lever board. If omitted, BalanceConfigRuntime.BattleIntroCopy is used.")]
        public BattleIntroCopyConfigSO copyConfig;

        [Header("Intro Camera Beat")]
        [Tooltip("Blend into closer/eye-level framing at intro start, then gradually restore BattleProfile while the intro message plays.")]
        public bool enableIntroCameraBeat;

        [Tooltip("Added to BattleProfile.followOffset.y for intro framing (e.g. 2 raises the camera). Not an absolute Y.")]
        public float introFollowOffsetY;

        [Tooltip("Absolute followOffset.z while intro framing is active (BattleProfile default is -60).")]
        public float introFollowOffsetZ = -40f;

        [Tooltip("Temporary pitch angle while intro framing is active (BattleProfile default is 16). Lower = more eye-level.")]
        public float introPitchAngle = 8f;

        [Tooltip("Seconds to blend from BattleProfile into intro framing at the start of the beat. 0 snaps instantly.")]
        [Min(0f)]
        public float introCameraBlendInSeconds = 0.75f;

        [Tooltip("Seconds to blend back to BattleProfile while the intro message is on screen.")]
        [Min(0f)]
        public float introCameraBlendOutSeconds = 4f;

        [Header("Wallet Timing")]
        [Tooltip("Override the presenter's default post-glitch wallet delay. Negative uses the presenter default.")]
        public float postGlitchWalletDelay = -1f;

        [Header("VFX Profiles")]
        public BattleIntroVfxProfileSO defaultVfxProfile;
        public BattleIntroVfxProfileSO bossVfxProfile;
        public BattleIntroVfxProfileSO entangledVfxProfile;
        public BattleIntroVfxProfileSO pvpVfxProfile;
        public List<BattleIntroVfxProfileOverride> vfxProfileOverrides = new List<BattleIntroVfxProfileOverride>();

        [Header("Debug")]
        [Tooltip("Enable staging-only logs for this presentation asset.")]
        public bool enableDebugLogs;

        public BattleIntroVfxProfileSO ResolveVfxProfile(BattleIntroRequest request)
        {
            BattleIntroVfxProfileSO overrideProfile = ResolveExactOverride(request);
            if (overrideProfile != null)
                return overrideProfile;

            overrideProfile = ResolveSpeciesOverride(request);
            if (overrideProfile != null)
                return overrideProfile;

            if (request.isBoss && bossVfxProfile != null)
                return bossVfxProfile;

            if (request.hasEntangledOpponent && entangledVfxProfile != null)
                return entangledVfxProfile;

            if (request.isPvP && pvpVfxProfile != null)
                return pvpVfxProfile;

            return defaultVfxProfile;
        }

        public BattleIntroCopyConfigSO ResolveCopyConfig()
        {
            return copyConfig != null ? copyConfig : BalanceConfigRuntime.BattleIntroCopy;
        }

        public float ResolvePlayerSpawnAdvanceDelay()
        {
            return playerSpawnAdvanceDelaySeconds >= 0f
                ? playerSpawnAdvanceDelaySeconds
                : spawnAdvanceDelaySeconds;
        }

        private BattleIntroVfxProfileSO ResolveExactOverride(BattleIntroRequest request)
        {
            if (vfxProfileOverrides == null || vfxProfileOverrides.Count == 0)
                return null;

            string leadSpeciesId = NormalizeId(request.leadOpponentSpeciesId);
            string leadVariantId = NormalizeId(request.leadOpponentVisualVariantId);
            if (string.IsNullOrEmpty(leadSpeciesId) || string.IsNullOrEmpty(leadVariantId))
                return null;

            for (int i = 0; i < vfxProfileOverrides.Count; i++)
            {
                BattleIntroVfxProfileOverride entry = vfxProfileOverrides[i];
                if (entry == null || entry.profile == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.visualVariantId))
                    continue;

                if (IdEquals(entry.speciesId, leadSpeciesId) && IdEquals(entry.visualVariantId, leadVariantId))
                    return entry.profile;
            }

            return null;
        }

        private BattleIntroVfxProfileSO ResolveSpeciesOverride(BattleIntroRequest request)
        {
            if (vfxProfileOverrides == null || vfxProfileOverrides.Count == 0)
                return null;

            string leadSpeciesId = NormalizeId(request.leadOpponentSpeciesId);
            BattleIntroVfxProfileSO leadProfile = ResolveSpeciesOverride(leadSpeciesId);
            if (leadProfile != null)
                return leadProfile;

            string[] speciesIds = request.opponentSpeciesIds;
            if (speciesIds == null)
                return null;

            for (int i = 0; i < speciesIds.Length; i++)
            {
                BattleIntroVfxProfileSO profile = ResolveSpeciesOverride(speciesIds[i]);
                if (profile != null)
                    return profile;
            }

            return null;
        }

        private BattleIntroVfxProfileSO ResolveSpeciesOverride(string speciesId)
        {
            speciesId = NormalizeId(speciesId);
            if (string.IsNullOrEmpty(speciesId))
                return null;

            for (int i = 0; i < vfxProfileOverrides.Count; i++)
            {
                BattleIntroVfxProfileOverride entry = vfxProfileOverrides[i];
                if (entry == null || entry.profile == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.visualVariantId))
                    continue;

                if (IdEquals(entry.speciesId, speciesId))
                    return entry.profile;
            }

            return null;
        }

        private static bool IdEquals(string lhs, string rhs)
        {
            return string.Equals(NormalizeId(lhs), NormalizeId(rhs), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        private void OnValidate()
        {
            centerSlideDuration = Mathf.Max(0f, centerSlideDuration);
            postDialogueDelay = Mathf.Max(0f, postDialogueDelay);
            spawnAdvanceDelaySeconds = Mathf.Max(0f, spawnAdvanceDelaySeconds);
            if (playerSpawnAdvanceDelaySeconds < 0f)
                playerSpawnAdvanceDelaySeconds = -1f;
            postGlitchWalletDelay = Mathf.Max(-1f, postGlitchWalletDelay);
            introCameraBlendInSeconds = Mathf.Max(0f, introCameraBlendInSeconds);
            introCameraBlendOutSeconds = Mathf.Max(0f, introCameraBlendOutSeconds);
        }
    }
}
