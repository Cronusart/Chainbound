using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatSfxOrchestrator : MonoBehaviour, ICombatSfxOrchestrator
    {
        private enum CombatSfxRole
        {
            Impact,
            Consequence,
            Status,
            PostBattle,
            Wall
        }

        private sealed class TargetOutcomeSequence
        {
            public string TargetKey;
            public int FirstIndex;
            public CombatSfxOutcomeContext FirstContext;
            public CombatSfxOutcomeContext PrimaryContext;
            public CombatSfxOutcomeContext StatusContext;
            public CombatSfxOutcomeContext KoContext;
        }

        [Header("Catalog")]
        [SerializeField] private CombatBattleSfxCatalogSO catalog;

        [Header("Bridge")]
        [Tooltip("Optional MonoBehaviour implementing ICombatAudioBridge.")]
        [SerializeField] private MonoBehaviour audioBridgeBehaviour;
        [SerializeField] private bool autoDiscoverAudioBridge = true;
        [SerializeField] private bool enableDebugLogs;

        private ICombatAudioBridge _configuredBridge;
        private readonly HashSet<string> _playedWallCueKeys = new HashSet<string>();
        private BattleResult _playedPostBattleResult;

        public CombatBattleSfxCatalogSO Catalog => catalog;

        public void Configure(CombatBattleSfxCatalogSO sfxCatalog, ICombatAudioBridge audioBridge = null)
        {
            if (sfxCatalog != null)
                catalog = sfxCatalog;

            if (audioBridge != null)
                _configuredBridge = audioBridge;
        }

        public void SetCatalog(CombatBattleSfxCatalogSO sfxCatalog)
        {
            if (sfxCatalog != null)
                catalog = sfxCatalog;
        }

        public void PlayTurnStartApGrant(IReadOnlyList<CombatEvent> events, Vector3? position = null)
        {
            if (events == null || events.Count == 0)
                return;

            Play(catalog != null ? catalog.TurnStartApGrant : null, position, null, catalog != null ? catalog.TurnStartApVolumeScale : 1f);
        }

        public void PlayApFeedback(CombatEvent evt, Combat2ChainlingView targetView)
        {
            Play(catalog != null ? catalog.ResolveApFeedback(evt) : null, ResolvePosition(targetView), ResolveFollow(targetView));
        }

        public void PlayOutcomeBatch(CombatSfxOutcomeBatch batch)
        {
            if (batch == null || batch.outcomes == null || batch.outcomes.Count == 0)
                return;

            List<TargetOutcomeSequence> sequences = BuildOutcomeSequences(batch);
            if (sequences.Count == 0)
                return;

            bool isMultiTarget = sequences.Count > 1;
            float stagger = isMultiTarget && catalog != null ? catalog.DefaultImpactStaggerSeconds : 0f;
            float batchVolumeScale = isMultiTarget && catalog != null ? catalog.MultiTargetVolumeScale : 1f;
            int maxVoices = catalog != null ? catalog.MaxSimultaneousOutcomeVoices : int.MaxValue;
            int primaryVoiceCount = 0;
            int koIndex = 0;

            for (int i = 0; i < sequences.Count; i++)
            {
                TargetOutcomeSequence sequence = sequences[i];
                float targetDelay = ResolveTargetDelay(sequence, i, stagger);

                if (sequence.PrimaryContext != null && primaryVoiceCount < maxVoices)
                {
                    PlayPrimaryOutcome(sequence.PrimaryContext, targetDelay, batchVolumeScale);
                    PlayImpactFallbacks(sequence.PrimaryContext, targetDelay, batchVolumeScale);
                    primaryVoiceCount++;
                }

                if (sequence.StatusContext != null)
                {
                    float statusDelay = targetDelay;
                    if (sequence.PrimaryContext != null && catalog != null)
                        statusDelay += catalog.StatusAfterImpactDelaySeconds;
                    PlayStatusAfterImpact(sequence.StatusContext, statusDelay, batchVolumeScale);
                }

                if (sequence.KoContext != null)
                {
                    float koDelay = targetDelay;
                    if (catalog != null)
                    {
                        koDelay += catalog.KoDelayAfterImpactSeconds;
                        koDelay += koIndex * catalog.KoStaggerSeconds;
                    }

                    PlayKoConsequence(sequence.KoContext, koDelay, batchVolumeScale);
                    koIndex++;
                }
            }
        }

        public void PlayWallCue(CombatWallSfxRequest request)
        {
            if (request == null || catalog == null || catalog.WallSfxCatalog == null)
                return;

            string cueKey = BuildWallCueKey(request);
            if (!string.IsNullOrEmpty(cueKey) && !_playedWallCueKeys.Add(cueKey))
                return;

            string wallSfxId = !string.IsNullOrWhiteSpace(request.wallSfxId)
                ? request.wallSfxId
                : request.wallSourceTag;
            UnityEngine.Object sound = catalog.WallSfxCatalog.ResolveSound(wallSfxId, request.cue);
            if (sound == null)
                return;

            Combat2ChainlingView anchorView = request.cue == CombatWallSfxCue.Reflect && request.sourceView != null
                ? request.sourceView
                : request.targetView;
            PlayResolved(
                sound,
                request.delaySeconds,
                ResolvePosition(anchorView, request.preferBarrierAnchor && request.cue != CombatWallSfxCue.Reflect),
                ResolveFollow(anchorView),
                Mathf.Clamp01(request.volumeScale),
                CombatSfxRole.Wall,
                null);
        }

        public void PlayStatusAfterImpact(CombatSfxOutcomeContext context, float delaySeconds)
        {
            if (context == null)
                return;

            PlayStatusAfterImpact(context, delaySeconds, 1f);
        }

        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        public void PlayDamageOutcome(CombatEvent evt, Combat2ChainlingView targetView, bool isCrit)
        {
            PlayOutcomeBatch(CreateSingleOutcomeBatch(evt, targetView, isCrit: isCrit));
        }

        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        public void PlayMiss(CombatEvent evt, Combat2ChainlingView targetView)
        {
            PlayOutcomeBatch(CreateSingleOutcomeBatch(evt, targetView));
        }

        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        public void PlayBlock(CombatEvent evt, Combat2ChainlingView targetView)
        {
            PlayOutcomeBatch(CreateSingleOutcomeBatch(evt, targetView));
        }

        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        public void PlayHeal(CombatEvent evt, Combat2ChainlingView targetView)
        {
            PlayOutcomeBatch(CreateSingleOutcomeBatch(evt, targetView));
        }

        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        public void PlayStatus(CombatEvent evt, Combat2ChainlingView targetView, StatusDefinitionSO statusDefinition)
        {
            PlayOutcomeBatch(CreateSingleOutcomeBatch(evt, targetView, statusDefinition: statusDefinition));
        }

        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        public void PlayKo(CombatEvent evt, Combat2ChainlingView targetView)
        {
            PlayOutcomeBatch(CreateSingleOutcomeBatch(evt, targetView, isKo: true));
        }

        public void PlaySpawn(Combat2ChainlingView view)
        {
            if (HasProfileAudioForState(view, CombatChainlingVisual.CombatAnimState.SwapIn))
                return;

            UnityEngine.Object sound = ResolveProfileSpawnFallback(view);
            Play(sound != null ? sound : (catalog != null ? catalog.GlitchIn : null), ResolvePosition(view), ResolveFollow(view));
        }

        public void PlaySwapOut(CombatEvent evt, Combat2ChainlingView outgoingView)
        {
            if (HasProfileAudioForState(outgoingView, CombatChainlingVisual.CombatAnimState.SwapOut))
                return;

            Play(catalog != null ? catalog.SwapOut : null, ResolvePosition(outgoingView), ResolveFollow(outgoingView));
        }

        public void PlaySwapIn(CombatEvent evt, Combat2ChainlingView incomingView)
        {
            if (HasProfileAudioForState(incomingView, CombatChainlingVisual.CombatAnimState.SwapIn))
                return;

            UnityEngine.Object sound = ResolveProfileSwapInFallback(incomingView);
            Play(sound != null ? sound : (catalog != null ? catalog.SwapIn : null), ResolvePosition(incomingView), ResolveFollow(incomingView));
        }

        public void PlayPostBattle(BattleResult result)
        {
            StopBattleMusic(0f);
            if (ReferenceEquals(_playedPostBattleResult, result) && result != null)
                return;

            _playedPostBattleResult = result;
            PlayResolved(
                catalog != null ? catalog.ResolvePostBattle(result) : null,
                0f,
                null,
                null,
                1f,
                CombatSfxRole.PostBattle,
                null);
        }

        public void StopBattleMusic(float fadeSeconds = 0f)
        {
            ResolveAudioBridge()?.StopSceneMusic(fadeSeconds);
        }

        public void PlayXpShare(BattleRewardSummary summary)
        {
            if (catalog == null || !HasXpRewards(summary))
                return;

            Play(catalog.XpShare);
        }

        public void PlayRamTick()
        {
            if (catalog == null)
                return;

            Play(catalog.RamGrantTick, volumeScale: 0.62f);
        }

        public void PlayStarUp(BattleRewardSummary summary)
        {
            if (catalog == null || !HasStarRewards(summary))
                return;

            Play(catalog.StarUp);
        }

        public void PlayRewardApplied(BattleRewardSummary summary)
        {
            if (catalog == null)
                return;

            Play(catalog.RewardConfirm);
        }

        private CombatSfxOutcomeBatch CreateSingleOutcomeBatch(
            CombatEvent evt,
            Combat2ChainlingView targetView,
            StatusDefinitionSO statusDefinition = null,
            bool isCrit = false,
            bool isKo = false)
        {
            var batch = new CombatSfxOutcomeBatch(evt.moveId, evt.sourceId, evt.turn);
            batch.Add(new CombatSfxOutcomeContext(evt, targetView, statusDefinition, isCrit || evt.boolValue, isKo)
            {
                sourceView = null,
                moveCategory = MoveCategory.Support,
                volumeScale = 1f,
            });
            return batch;
        }

        private List<TargetOutcomeSequence> BuildOutcomeSequences(CombatSfxOutcomeBatch batch)
        {
            var sequences = new List<TargetOutcomeSequence>();

            for (int i = 0; i < batch.outcomes.Count; i++)
            {
                CombatSfxOutcomeContext context = batch.outcomes[i];
                if (context == null)
                    continue;

                TargetOutcomeSequence sequence = FindOrCreateSequence(sequences, context, i);
                if (sequence.FirstContext == null)
                    sequence.FirstContext = context;

                if (context.isKo || context.evt.eventType == CombatEventType.ChainlingKO)
                {
                    sequence.KoContext = context;
                    continue;
                }

                if (IsStatusOutcome(context.evt.eventType))
                {
                    sequence.StatusContext = SelectHigherPriorityStatus(sequence.StatusContext, context);
                    continue;
                }

                if (IsPrimaryOutcome(context.evt.eventType))
                    sequence.PrimaryContext = SelectHigherPriorityPrimary(sequence.PrimaryContext, context);
            }

            sequences.Sort((a, b) => a.FirstIndex.CompareTo(b.FirstIndex));
            return sequences;
        }

        private static TargetOutcomeSequence FindOrCreateSequence(List<TargetOutcomeSequence> sequences, CombatSfxOutcomeContext context, int index)
        {
            string key = ResolveTargetKey(context);
            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i].TargetKey == key)
                    return sequences[i];
            }

            var sequence = new TargetOutcomeSequence
            {
                TargetKey = key,
                FirstIndex = index,
                FirstContext = context,
            };
            sequences.Add(sequence);
            return sequence;
        }

        private static CombatSfxOutcomeContext SelectHigherPriorityPrimary(CombatSfxOutcomeContext current, CombatSfxOutcomeContext candidate)
        {
            if (current == null)
                return candidate;

            int currentPriority = GetPrimaryPriority(current);
            int candidatePriority = GetPrimaryPriority(candidate);
            return candidatePriority > currentPriority ? candidate : current;
        }

        private static CombatSfxOutcomeContext SelectHigherPriorityStatus(CombatSfxOutcomeContext current, CombatSfxOutcomeContext candidate)
        {
            if (current == null)
                return candidate;

            int currentPriority = GetStatusPriority(current.evt.eventType);
            int candidatePriority = GetStatusPriority(candidate.evt.eventType);
            return candidatePriority > currentPriority ? candidate : current;
        }

        private void PlayPrimaryOutcome(CombatSfxOutcomeContext context, float delaySeconds, float batchVolumeScale)
        {
            UnityEngine.Object sound = ResolvePrimaryOutcomeSound(context);
            bool preferBarrier = context != null && context.evt.eventType == CombatEventType.BarrierBlocked;
            Vector3? position = ResolvePosition(context != null ? context.targetView : null, preferBarrier);
            Transform follow = ResolveFollow(context != null ? context.targetView : null);
            float volumeScale = ResolveVolumeScale(context, batchVolumeScale);

            if (context != null && IsDamageOutcome(context.evt.eventType) &&
                (context.isCrit || context.evt.boolValue) &&
                catalog != null && catalog.CritWindupSound != null)
            {
                PlayResolved(
                    catalog.CritWindupSound,
                    Mathf.Max(0f, delaySeconds - catalog.CritWindupLeadSeconds),
                    position,
                    follow,
                    volumeScale,
                    CombatSfxRole.Impact,
                    context);
            }

            PlayResolved(
                sound,
                delaySeconds,
                position,
                follow,
                volumeScale,
                CombatSfxRole.Impact,
                context);
        }

        private void PlayImpactFallbacks(CombatSfxOutcomeContext context, float delaySeconds, float batchVolumeScale)
        {
            if (context == null)
                return;

            // Miss/dodge should play the miss clip only — never impact accents or hit-reaction fallsbacks.
            if (!IsDamageOutcome(context.evt.eventType))
                return;

            UnityEngine.Object hitReaction = ResolveProfileHitFallback(context);
            if (hitReaction != null)
            {
                PlayResolved(
                    hitReaction,
                    delaySeconds,
                    ResolvePosition(context.targetView),
                    ResolveFollow(context.targetView),
                    ResolveVolumeScale(context, batchVolumeScale),
                    CombatSfxRole.Consequence,
                    context);
            }

            UnityEngine.Object moveAccent = ResolveMoveImpactAccentFallback(context);
            if (moveAccent != null)
            {
                float accentDelay = delaySeconds + (catalog != null ? catalog.CritAccentDelaySeconds : 0f);
                PlayResolved(
                    moveAccent,
                    accentDelay,
                    ResolvePosition(context.targetView),
                    ResolveFollow(context.targetView),
                    ResolveVolumeScale(context, batchVolumeScale),
                    CombatSfxRole.Impact,
                    context);
            }
        }

        private void PlayStatusAfterImpact(CombatSfxOutcomeContext context, float delaySeconds, float batchVolumeScale)
        {
            if (context == null)
                return;

            UnityEngine.Object sound = catalog != null ? catalog.ResolveStatusSound(context.evt, context.statusDefinition) : null;
            PlayResolved(
                sound,
                delaySeconds,
                ResolvePosition(context.targetView),
                ResolveFollow(context.targetView),
                ResolveVolumeScale(context, batchVolumeScale),
                CombatSfxRole.Status,
                context);
        }

        private void PlayKoConsequence(CombatSfxOutcomeContext context, float delaySeconds, float batchVolumeScale)
        {
            UnityEngine.Object sound = ResolveKoSound(context);
            PlayResolved(
                sound,
                delaySeconds,
                ResolvePosition(context != null ? context.targetView : null),
                ResolveFollow(context != null ? context.targetView : null),
                ResolveVolumeScale(context, batchVolumeScale),
                CombatSfxRole.Consequence,
                context);
        }

        private UnityEngine.Object ResolvePrimaryOutcomeSound(CombatSfxOutcomeContext context)
        {
            if (context == null)
                return null;

            switch (context.evt.eventType)
            {
                case CombatEventType.BarrierBlocked:
                    return ResolveWallBlockSound(context) ?? (catalog != null ? catalog.Block : null);
                case CombatEventType.Miss:
                case CombatEventType.Dodge:
                    return context.authoredMissSound ?? (catalog != null ? catalog.Miss : null);
                case CombatEventType.HealApplied:
                    return catalog != null ? catalog.Heal : null;
                case CombatEventType.DamageDealt:
                case CombatEventType.HPDotDamage:
                case CombatEventType.HPRecoilDamage:
                case CombatEventType.PassiveDamageDealt:
                    return context.authoredImpactSound ??
                           (catalog != null ? catalog.ResolveDamageOutcome(context.isCrit || context.evt.boolValue) : null);
                default:
                    return null;
            }
        }

        private UnityEngine.Object ResolveWallBlockSound(CombatSfxOutcomeContext context)
        {
            if (catalog == null || catalog.WallSfxCatalog == null || context == null)
                return null;

            string wallSfxId = !string.IsNullOrWhiteSpace(context.wallSfxId)
                ? context.wallSfxId
                : context.wallSourceTag;
            return catalog.WallSfxCatalog.ResolveSound(wallSfxId, CombatWallSfxCue.Block);
        }

        private UnityEngine.Object ResolveKoSound(CombatSfxOutcomeContext context)
        {
            if (context != null &&
                !HasProfileAudioForState(context.targetView, CombatChainlingVisual.CombatAnimState.SwapOut))
            {
                CombatVisualProfileSO profile = ResolveProfile(context.targetView);
                if (profile != null && profile.koSound != null)
                    return profile.koSound;
            }

            return catalog != null ? catalog.Ko : null;
        }

        private static UnityEngine.Object ResolveProfileSpawnFallback(Combat2ChainlingView view)
        {
            CombatVisualProfileSO profile = ResolveProfile(view);
            return profile != null ? profile.spawnSound : null;
        }

        private static UnityEngine.Object ResolveProfileSwapInFallback(Combat2ChainlingView view)
        {
            CombatVisualProfileSO profile = ResolveProfile(view);
            if (profile == null)
                return null;

            return profile.swapInSound != null ? profile.swapInSound : profile.spawnSound;
        }

        private static UnityEngine.Object ResolveProfileHitFallback(CombatSfxOutcomeContext context)
        {
            if (context == null || !IsDamageOutcome(context.evt.eventType))
                return null;

            if (HasProfileAudioForState(context.targetView, CombatChainlingVisual.CombatAnimState.Hit))
                return null;

            CombatVisualProfileSO profile = ResolveProfile(context.targetView);
            return profile != null ? profile.hitReactionSound : null;
        }

        private static UnityEngine.Object ResolveMoveImpactAccentFallback(CombatSfxOutcomeContext context)
        {
            if (context == null || !IsDamageOutcome(context.evt.eventType) ||
                context.authoredImpactSound != null ||
                context.moveDefinition == null || context.moveDefinition.impactAccentSound == null)
                return null;

            CombatPresentationTrackSO track = context.moveDefinition.presentationTrack;
            if (track != null && track.HasAudioEvents())
                return null;

            return context.moveDefinition.impactAccentSound;
        }

        private static float ResolveTargetDelay(TargetOutcomeSequence sequence, int targetIndex, float stagger)
        {
            float authoredDelay = sequence != null && sequence.FirstContext != null
                ? Mathf.Max(0f, sequence.FirstContext.targetDelaySeconds)
                : 0f;
            return authoredDelay + Mathf.Max(0f, targetIndex * stagger);
        }

        private static float ResolveVolumeScale(CombatSfxOutcomeContext context, float batchVolumeScale)
        {
            float contextScale = context != null && context.volumeScale > 0f ? context.volumeScale : 1f;
            return Mathf.Clamp01(contextScale * Mathf.Max(0f, batchVolumeScale));
        }

        private static int GetPrimaryPriority(CombatSfxOutcomeContext context)
        {
            if (context == null)
                return 0;

            switch (context.evt.eventType)
            {
                case CombatEventType.BarrierBlocked:
                    return 400;
                case CombatEventType.Miss:
                case CombatEventType.Dodge:
                    return 300;
                case CombatEventType.DamageDealt:
                case CombatEventType.HPDotDamage:
                case CombatEventType.HPRecoilDamage:
                case CombatEventType.PassiveDamageDealt:
                    return context.isCrit || context.evt.boolValue ? 250 : 200;
                case CombatEventType.HealApplied:
                    return 100;
                default:
                    return 0;
            }
        }

        private static int GetStatusPriority(CombatEventType eventType)
        {
            switch (eventType)
            {
                case CombatEventType.StatusImmune:
                    return 300;
                case CombatEventType.StatusCleansed:
                    return 200;
                case CombatEventType.StatusApplied:
                case CombatEventType.ChemistryStatusApplied:
                case CombatEventType.StatusRefreshed:
                    return 100;
                default:
                    return 0;
            }
        }

        private static bool IsPrimaryOutcome(CombatEventType eventType)
        {
            return IsDamageOutcome(eventType) ||
                   eventType == CombatEventType.Miss ||
                   eventType == CombatEventType.Dodge ||
                   eventType == CombatEventType.BarrierBlocked ||
                   eventType == CombatEventType.HealApplied;
        }

        private static bool IsDamageOutcome(CombatEventType eventType)
        {
            return eventType == CombatEventType.DamageDealt ||
                   eventType == CombatEventType.HPDotDamage ||
                   eventType == CombatEventType.HPRecoilDamage ||
                   eventType == CombatEventType.PassiveDamageDealt;
        }

        private static bool IsStatusOutcome(CombatEventType eventType)
        {
            return eventType == CombatEventType.StatusApplied ||
                   eventType == CombatEventType.ChemistryStatusApplied ||
                   eventType == CombatEventType.StatusRefreshed ||
                   eventType == CombatEventType.StatusImmune ||
                   eventType == CombatEventType.StatusCleansed;
        }

        private static string ResolveTargetKey(CombatSfxOutcomeContext context)
        {
            if (context == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(context.evt.targetId))
                return context.evt.targetId;

            if (context.targetView != null)
                return context.targetView.GetInstanceID().ToString();

            return context.GetHashCode().ToString();
        }

        private void Play(UnityEngine.Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
        {
            if (soundEvent == null)
                return;

            var bridge = ResolveAudioBridge();
            if (bridge == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[CombatSfxOrchestrator] No ICombatAudioBridge found for combat SFX.", this);
                return;
            }

            bridge.PlayOneShot(soundEvent, position, follow, volumeScale);
        }

        private void PlayResolved(
            UnityEngine.Object soundEvent,
            float delaySeconds,
            Vector3? position,
            Transform follow,
            float volumeScale,
            CombatSfxRole role,
            CombatSfxOutcomeContext context)
        {
            if (soundEvent == null)
                return;

            float delay = Mathf.Max(0f, delaySeconds);
            LogResolved(soundEvent, delay, volumeScale, role, context);

            if (delay <= 0f || !isActiveAndEnabled)
            {
                Play(soundEvent, position, follow, volumeScale);
                return;
            }

            StartCoroutine(PlayDelayed(soundEvent, delay, position, follow, volumeScale));
        }

        private IEnumerator PlayDelayed(UnityEngine.Object soundEvent, float delay, Vector3? position, Transform follow, float volumeScale)
        {
            yield return new WaitForSeconds(delay);
            Play(soundEvent, position, follow, volumeScale);
        }

        private void LogResolved(UnityEngine.Object soundEvent, float delay, float volumeScale, CombatSfxRole role, CombatSfxOutcomeContext context)
        {
            if (!enableDebugLogs && (catalog == null || !catalog.DebugLogResolvedSequence))
                return;

            string moveId = context != null
                ? (!string.IsNullOrEmpty(context.evt.moveId) ? context.evt.moveId : context.evt.sourceId)
                : string.Empty;
            string targetId = context != null ? context.evt.targetId : string.Empty;
            string soundName = soundEvent != null ? soundEvent.name : "null";

            Debug.Log($"[SFX] move={moveId} role={role} target={targetId} sound={soundName} delay={delay:0.###} volume={volumeScale:0.##}", this);
        }

        private ICombatAudioBridge ResolveAudioBridge()
        {
            if (_configuredBridge != null)
                return _configuredBridge;

            MonoBehaviour resolvedBehaviour;
            ICombatAudioBridge bridge = CombatAudioBridgeResolver.Resolve(
                audioBridgeBehaviour,
                autoDiscoverAudioBridge,
                out resolvedBehaviour);
            if (resolvedBehaviour != null)
                audioBridgeBehaviour = resolvedBehaviour;
            return bridge;
        }

        private static string BuildWallCueKey(CombatWallSfxRequest request)
        {
            if (request == null || request.eventTimestamp < 0)
                return string.Empty;

            return string.Concat(
                request.eventTimestamp.ToString(), "|",
                request.targetId ?? string.Empty, "|",
                request.sourceId ?? string.Empty, "|",
                request.cue.ToString());
        }

        private static bool HasXpRewards(BattleRewardSummary summary)
        {
            if (summary == null || summary.chainlingRewards == null)
                return false;

            for (int i = 0; i < summary.chainlingRewards.Count; i++)
            {
                var reward = summary.chainlingRewards[i];
                if (reward != null && reward.xpEarned > 0)
                    return true;
            }

            return false;
        }

        private static bool HasStarRewards(BattleRewardSummary summary)
        {
            if (summary == null || summary.chainlingRewards == null)
                return false;

            for (int i = 0; i < summary.chainlingRewards.Count; i++)
            {
                var reward = summary.chainlingRewards[i];
                if (reward == null)
                    continue;
                if (reward.gainedStar ||
                    reward.newStars > reward.previousStars ||
                    (reward.starsCrossed != null && reward.starsCrossed.Count > 0))
                    return true;
            }

            return false;
        }

        private static Vector3? ResolvePosition(Combat2ChainlingView view, bool preferBarrier = false)
        {
            if (view == null)
                return null;

            return view.ResolvePopupWorldPosition(preferBarrier);
        }

        private static Transform ResolveFollow(Combat2ChainlingView view)
        {
            return view != null ? view.ImpactAnchor : null;
        }

        private static CombatVisualProfileSO ResolveProfile(Combat2ChainlingView view)
        {
            return view != null && view.Visual != null ? view.Visual.CurrentProfile : null;
        }

        private static bool HasProfileAudioForState(Combat2ChainlingView view, CombatChainlingVisual.CombatAnimState state)
        {
            CombatVisualProfileSO profile = ResolveProfile(view);
            CombatPresentationTrackSO track = profile != null ? profile.presentationTrack : null;
            return track != null && track.HasAudioEventForState(state);
        }
    }
}
