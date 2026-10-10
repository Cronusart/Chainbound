using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Inspector-configurable encounter provider for the real battle scene.
    /// Designers define opponent parties without code while encounter / VRF systems mature.
    /// Feeds the real <see cref="CombatBattleController"/> pipeline with production-shaped snapshots.
    /// </summary>
    [CreateAssetMenu(fileName = "ManualEncounterProvider", menuName = "Chainbound/Combat/Encounters/Manual Encounter Provider")]
    public class ManualEncounterProviderSO : ScriptableObject, IEncounterProvider
    {
        [Header("Authority Manifest")]
        [Tooltip("Stable narrative encounter ID exported for the battle service. Leave empty only for editor-only providers.")]
        [SerializeField] private string authoritativeEncounterId = "";

        [Tooltip("Profile quest that must currently be available or active for the first RAM reward. Empty fails closed for narrative rewards.")]
        [SerializeField] private string rewardEligibilityQuestId = "";

        [Header("Opponent Roster (1-3 Chainlings)")]
        [Tooltip("Ordered roster: index 0 = Lead, 1 = Partner, 2 = Bench.")]
        [SerializeField] private List<ManualChainlingEntry> roster = new List<ManualChainlingEntry>();

        [Header("Opponent Team")]
        [SerializeField] private string teamName = "Wild";
        [SerializeField] private AIPersonality aiPersonality = AIPersonality.Aggressive;
        [SerializeField] private EncounterDifficulty difficulty = EncounterDifficulty.Standard;
        [SerializeField] private UnityEngine.Object bossIntroDialogueAsset;

        [Header("Flee Rules")]
        [Tooltip("Use Difficulty Default locks Boss encounters while allowing Standard and Elite encounters. Override only for intentional encounter-specific behavior.")]
        [SerializeField] private EncounterFleePolicy fleePolicy = EncounterFleePolicy.UseDifficultyDefault;

        [Header("Capture Rules")]
        [Tooltip("Scripted/trainer encounters are locked by default. Enabled is an explicit reusable policy override.")]
        [SerializeField] private Chainbound.Combat.Core.EncounterCapturePolicy capturePolicy = Chainbound.Combat.Core.EncounterCapturePolicy.Disabled;

        [Header("UI")]
        [Tooltip("Hide HUD cards, action pad, and wallet for the entire battle. Use for intro cutscene encounters before the player receives the device. Overrides uiPresentationMode when true.")]
        [SerializeField] private bool hideCombatUI;

        [Tooltip("Which combat UI to reveal. Ignored when Hide Combat UI is true (then always Hidden).")]
        [SerializeField] private CombatUiPresentationMode uiPresentationMode = CombatUiPresentationMode.Full;

        [Header("Battlefield Presentation")]
        [Tooltip("Presentation-only multiplier applied to opponent sprite quad sizes for this encounter. 1 keeps the normal Chainling scale.")]
        [Min(0.01f)]
        [SerializeField] private float opponentVisualScaleMultiplier = 1f;

        [Header("Narrative / Scripted Exit")]
        [Tooltip("Optional: presentation overrides for battle intro staging. Null uses presenter defaults.")]
        [SerializeField] private BattleIntroPresentationSO introPresentation;

        [Tooltip("If true, no manual turn is started after the battle intro. Use with IntroEncounterAutoEscaper (dialogue + flee) so real combat does not begin.")]
        [SerializeField] private bool skipManualTurnAfterIntro;

        [Header("Victory Rewards")]
        [Tooltip("Profile-authoritative Known grants issued once per account when the player wins. Not candy rolls.")]
        [SerializeField] private List<EncounterVictoryKnownGrant> victoryKnownGrants = new List<EncounterVictoryKnownGrant>();

        public string TeamName => teamName;
        public AIPersonality AiPersonality => aiPersonality;
        public EncounterDifficulty Difficulty => difficulty;
        public EncounterFleePolicy FleePolicy => fleePolicy;
        public UnityEngine.Object BossIntroDialogueAsset => bossIntroDialogueAsset;
        public string AuthoritativeEncounterId => authoritativeEncounterId;
        public string RewardEligibilityQuestId => rewardEligibilityQuestId;
        public bool HideCombatUI => hideCombatUI;
        public BattleIntroPresentationSO IntroPresentation => introPresentation;
        public float OpponentVisualScaleMultiplier => Mathf.Max(0.01f, opponentVisualScaleMultiplier);

        public EncounterSnapshot GenerateEncounter(int seed)
        {
            var snapshot = new EncounterSnapshot
            {
                difficulty = difficulty,
                fleePolicy = fleePolicy,
                teamName = teamName,
                aiPersonality = aiPersonality,
                encounterSeed = seed,
                hideCombatUI = hideCombatUI,
                uiPresentationMode = hideCombatUI ? CombatUiPresentationMode.Hidden : uiPresentationMode,
                skipManualTurnAfterIntro = skipManualTurnAfterIntro,
                captureEnabled = capturePolicy == Chainbound.Combat.Core.EncounterCapturePolicy.Enabled,
                capturePolicy = capturePolicy,
                opponentVisualScaleMultiplier = OpponentVisualScaleMultiplier,
                authoritativeBattleKind = difficulty == EncounterDifficulty.Boss
                    ? Chainbound.Combat.Core.AuthoritativeBattleKind.Boss
                    : Chainbound.Combat.Core.AuthoritativeBattleKind.Scripted,
                narrativeEncounterId = authoritativeEncounterId ?? "",
            };

            if (victoryKnownGrants != null && victoryKnownGrants.Count > 0)
            {
                for (int i = 0; i < victoryKnownGrants.Count; i++)
                    snapshot.victoryKnownGrants.Add(victoryKnownGrants[i]);
            }

            int count = Mathf.Min(roster.Count, CombatConstants.ROSTER_SIZE);
            for (int i = 0; i < count; i++)
            {
                ManualChainlingEntry entry = roster[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
                    continue;

                ChainlingSnapshot built = ManualChainlingSnapshotBuilder.Build(entry);
                if (built != null)
                    snapshot.chainlings.Add(built);
            }

            return snapshot;
        }
    }
}
