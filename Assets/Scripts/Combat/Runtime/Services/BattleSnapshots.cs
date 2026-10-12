using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lightweight snapshot of a single chainling for battle initialization.
    /// Pure C# DTO with no Unity dependencies.
    /// </summary>
    [Serializable]
    public class ChainlingSnapshot
    {
        /// <summary>Species definition ID (e.g. "validape", "gilarime").</summary>
        public string speciesId;

        /// <summary>Supabase chainling UUID. Null for manual/test parties.</summary>
        public string backendChainlingId;

        /// <summary>Display name override. If empty, species default is used.</summary>
        public string displayName;

        /// <summary>Deprecated legacy level hint for one-time star migration when <see cref="stars"/> is 0.</summary>
        public int level;

        /// <summary>Star count (0-5). CB-inspired progression. Used for HUD display.</summary>
        public int stars;

        /// <summary>Current mastery XP balance used for star progression. Lifetime earned XP is tracked separately.</summary>
        public int masteryXp;

        /// <summary>Rebirth count (0-5). Permanent progression applied on top of stars.</summary>
        public int rebirthCount;

        /// <summary>Evolution stage (1-based).</summary>
        public int evolutionStage;

        /// <summary>Visual variant and cosmetic/effect IDs resolved locally by combat presentation.</summary>
        public ChainlingVisualIdentity visualIdentity;

        /// <summary>Wild mutation flag (zone encounters).</summary>
        public bool isMutated;

        /// <summary>Mutation FX id (aura); empty/<c>none</c> when not mutated.</summary>
        public string mutationId;

        /// <summary>Wild entangled / bootleg flag (zone encounters).</summary>
        public bool isEntangled;

        /// <summary>Rolled entangled type when <see cref="isEntangled"/>; otherwise <see cref="ChainlingType.None"/>.</summary>
        public ChainlingType entangledType;

        /// <summary>
        /// Exactly one Known move id granted on wild capture.
        /// Distinct from <see cref="moveIds"/> which is the full battle kit.
        /// </summary>
        public string knownMoveId;

        /// <summary>
        /// Legacy encounter value used only as a fallback mapper into Calm/Standard/Volatile/Extreme.
        /// Prefer <see cref="hasCaptureProfile"/> / <see cref="captureProfile"/> from the species definition.
        /// </summary>
        public int legacyBaseCaptureRate = 45;

        public bool hasCaptureProfile;
        public CaptureVolatilityProfile captureProfile = CaptureVolatilityProfile.Standard;
        public int captureMutationRarityRank;

        /// <summary>Optional on-chain collectible mint/asset address for archived Chainlings.</summary>
        public string nftMintAddress;

        /// <summary>Unix timestamp for when this Chainling was archived/anchored. 0 means dossier-only.</summary>
        public long archivedAtUnix;

        /// <summary>
        /// Move definition IDs to equip. Battle uses these ids only. An empty list equips nothing.
        /// </summary>
        public List<string> moveIds;

        /// <summary>
        /// Optional per-move modifier_1 section IDs aligned to <see cref="moveIds"/> index.
        /// Empty/null entries mean "no override for that move slot".
        /// </summary>
        public List<string> modifier1SectionIds;

        /// <summary>
        /// Optional per-move modifier_2 section IDs aligned to <see cref="moveIds"/> index.
        /// Empty/null entries mean "no override for that move slot".
        /// </summary>
        public List<string> modifier2SectionIds;

        /// <summary>
        /// Wild-rolled base IVs in stat order:
        /// [MaxHP, MeleeAtk, MeleeDef, RangedAtk, RangedDef, Speed].
        /// Do not merge Cycle allocation counts into this array.
        /// </summary>
        public int[] ivs;

        /// <summary>
        /// Cycle player-pick allocation counts (same order as <see cref="ivs"/>).
        /// Sum of elements should equal <see cref="rebirthCount"/>. Applied via
        /// <see cref="ProgressionConfigSO"/> (combat flat+percent; MaxHP percent-of-base by default),
        /// not as IV merges.
        /// </summary>
        public int[] rebirthIvBonuses;

        /// <summary>
        /// Persisted current HP from backend. <see cref="ChainlingVitalsConstants.UnsyncedCurrentHp"/> (-1) means
        /// unsynced / full HP at battle start and in menu.
        /// </summary>
        public int currentHp = ChainlingVitalsConstants.UnsyncedCurrentHp;

        public ChainlingSnapshot()
        {
            moveIds = new List<string>();
            modifier1SectionIds = new List<string>();
            modifier2SectionIds = new List<string>();
            ivs = new int[6];
            rebirthIvBonuses = new int[6];
            level = 50;
            evolutionStage = 1;
            visualIdentity = ChainlingVisualIdentity.CreateDefault();
        }
    }

    /// <summary>
    /// Snapshot of a player's party (1-3 chainlings) for battle initialization.
    /// </summary>
    [Serializable]
    public class PartySnapshot
    {
        /// <summary>Ordered list of chainlings. Index 0 = Lead, 1 = Partner, 2 = Bench.</summary>
        public List<ChainlingSnapshot> chainlings;

        /// <summary>Display name for this team.</summary>
        public string teamName;

        public PartySnapshot()
        {
            chainlings = new List<ChainlingSnapshot>();
            teamName = "Player";
        }

        public int Count => chainlings != null ? chainlings.Count : 0;
    }

    /// <summary>
    /// Controls which combat UI elements are revealed during battle.
    /// Used for staged presentation (no flash, animated reveal) and intro flee encounters.
    /// </summary>
    public enum CombatUiPresentationMode
    {
        /// <summary>Reveal full combat UI (HUD cards, action panel, turn ribbon, target indicators, wallet) after intro.</summary>
        Full = 0,

        /// <summary>Show player/enemy HUD cards only. Hide wallet, action panel, turn ribbon, targeting.</summary>
        InfoOnly = 1,

        /// <summary>Hide all combat UI for the entire battle (e.g. intro cutscene encounters).</summary>
        Hidden = 2,
    }

    /// <summary>
    /// Snapshot of an encounter's opponent party for battle initialization.
    /// </summary>
    [Serializable]
    public class EncounterSnapshot
    {
        /// <summary>Opponent chainlings (1-3).</summary>
        public List<ChainlingSnapshot> chainlings;

        /// <summary>Difficulty tier used by post-battle rewards.</summary>
        public EncounterDifficulty difficulty;

        /// <summary>Display name for the opponent team.</summary>
        public string teamName;

        /// <summary>AI personality for the CPU side.</summary>
        public AIPersonality aiPersonality;

        /// <summary>Seed used to generate this encounter (for replay/verification).</summary>
        public int encounterSeed;

        /// <summary>Resolved area id (wild zone encounters).</summary>
        public string areaId;

        /// <summary>Resolved zone id (wild zone encounters).</summary>
        public string zoneId;

        /// <summary>Time-of-day segment at roll time.</summary>
        public TimeSegment timeSegment;

        /// <summary>
        /// Encounter-authored player flee behavior. UseDifficultyDefault makes Boss encounters non-fleeable
        /// while keeping standard and elite encounters escapable.
        /// </summary>
        public EncounterFleePolicy fleePolicy;

        /// <summary>Reusable capture policy; default rules still allow only capture-enabled wild battles.</summary>
        public EncounterCapturePolicy capturePolicy;

        /// <summary>Whether this encounter authored capture support before policy overrides.</summary>
        public bool captureEnabled;

        /// <summary>Authority-facing battle classification used by capture policy resolution.</summary>
        public AuthoritativeBattleKind authoritativeBattleKind;

        /// <summary>When true, hide HUD cards, action pad, and wallet for the entire battle (e.g. intro cutscene encounters).</summary>
        [System.Obsolete("Use uiPresentationMode instead. Kept for backward compatibility; hideCombatUI=true maps to CombatUiPresentationMode.Hidden.")]
        public bool hideCombatUI;

        /// <summary>Which combat UI elements to reveal. hideCombatUI=true overrides to Hidden.</summary>
        public CombatUiPresentationMode uiPresentationMode;

        /// <summary>
        /// Presentation-only multiplier for opponent sprite quad sizes. Kept on the
        /// encounter snapshot so boss framing can be authored without changing the
        /// shared Chainling slot or species visual profiles.
        /// </summary>
        public float opponentVisualScaleMultiplier;

        /// <summary>Effective UI mode: Hidden when hideCombatUI is true, otherwise uiPresentationMode.</summary>
        public CombatUiPresentationMode EffectiveUiMode => hideCombatUI ? CombatUiPresentationMode.Hidden : uiPresentationMode;

        /// <summary>
        /// When true, <see cref="CombatBattleController.NotifyBattleIntroComplete"/> does not call
        /// <see cref="CombatBattleController.ExecuteNextTurn"/> so scripted intro flows (e.g. dialogue + flee)
        /// can run before any combat turn. Set from <see cref="ManualEncounterProviderSO"/> for narrative encounters.
        /// </summary>
        public bool skipManualTurnAfterIntro;

        /// <summary>Stable narrative encounter id for victory grants and analytics.</summary>
        public string narrativeEncounterId;

        /// <summary>Known move grants to issue on player victory (idempotent per account).</summary>
        public List<EncounterVictoryKnownGrant> victoryKnownGrants = new List<EncounterVictoryKnownGrant>();

        public EncounterSnapshot()
        {
            chainlings = new List<ChainlingSnapshot>();
            difficulty = EncounterDifficulty.Standard;
            teamName = "Wild";
            aiPersonality = AIPersonality.Aggressive;
            capturePolicy = EncounterCapturePolicy.UseEncounterDefault;
            authoritativeBattleKind = AuthoritativeBattleKind.Scripted;
            opponentVisualScaleMultiplier = 1f;
        }

        public int Count => chainlings != null ? chainlings.Count : 0;
    }

    /// <summary>
    /// Whether the battle is a PvE encounter or a PvP wager match.
    /// Determines which post-battle path the router takes.
    /// </summary>
    public enum BattleType
    {
        PvE,
        PvP,
    }

    public enum BattleCompletionReason
    {
        Standard = 0,
        PlayerFled = 1,
        Captured = 2,
        TargetEscaped = 3,
        ServiceInterrupted = 4,
    }

    /// <summary>
    /// Result of a completed battle, passed to reward/transition hooks.
    /// </summary>
    [Serializable]
    public class BattleResult
    {
        /// <summary>Index of the winning team (0 = player, 1 = opponent, -2 = draw).</summary>
        public int winnerTeamIndex;

        /// <summary>Total turns elapsed.</summary>
        public int turnCount;

        /// <summary>Battle duration in seconds (wall-clock, not game time).</summary>
        public float durationSeconds;

        /// <summary>True if the player fled.</summary>
        public bool wasFled;

        /// <summary>
        /// Scripted/narrative withdraw. Skip WITHDRAWN overlay and let the encounter
        /// service return to the overworld without waiting on post-battle UI.
        /// </summary>
        public bool skipPostBattlePresentation;

        /// <summary>Explicit terminal cause so capture is never misclassified as a KO victory or player loss.</summary>
        public BattleCompletionReason completionReason;

        /// <summary>The seed used for this battle (for replay).</summary>
        public int seed;

        /// <summary>PvE or PvP. Determines post-battle routing.</summary>
        public BattleType battleType;

        /// <summary>
        /// Match ID for PvP wager matches. Null/empty for PvE.
        /// Used by the post-battle router to drive Stamped selection.
        /// </summary>
        public string pvpMatchId;

        /// <summary>Casual battle-spine match. Skips wager selection and PvE rewards.</summary>
        public bool casualSpineMatch;

        /// <summary>Team index of the local player. PvE stays 0. Casual seat B is 1.</summary>
        public int viewerTeamIndex;

        /// <summary>Whether the player attempted a capture during this battle.</summary>
        public bool captureAttempted;

        /// <summary>Whether the capture attempt succeeded.</summary>
        public bool captureSucceeded;

        /// <summary>Supabase dossier id for the captured Chainling (profile-first path).</summary>
        public string capturedBackendChainlingId;

        /// <summary>Successful wild captures from this battle, in attempt order.</summary>
        public List<BattleCaptureRecord> captures;

        /// <summary>Known Known move id granted on capture (profile-first path).</summary>
        public string capturedKnownMoveId;

        /// <summary>Legacy: PDA address of the captured ChainlingAccount (wallet bind path).</summary>
        public string capturedChainlingPda;

        /// <summary>Legacy: Core Asset address of a minted Stamped (Bind / premium path only).</summary>
        public string mintedStampedMoveAssetId;

        /// <summary>Resolved area id for reward / event modifiers.</summary>
        public string areaId;

        /// <summary>Resolved zone id for reward / event modifiers.</summary>
        public string zoneId;

        /// <summary>Time-of-day segment at encounter roll time.</summary>
        public TimeSegment timeSegment;

        /// <summary>Encounter difficulty tier. Drives XP and RAM rewards.</summary>
        public EncounterDifficulty encounterDifficulty;

        /// <summary>
        /// Player level captured at battle start when that progression system exists. Zero keeps the current
        /// defeat RAM penalty flat.
        /// </summary>
        public int playerLevel;

        /// <summary>Snapshot of player party participation for reward distribution.</summary>
        public List<BattleParticipant> playerParticipants;

        /// <summary>Total enemy KOs by species ID.</summary>
        public Dictionary<string, int> enemyKOsBySpecies;

        /// <summary>Total enemy KOs across the battle.</summary>
        public int totalEnemyKOs;

        /// <summary>Precomputed PvE combat-stat contributions, before XP/victory reward merging.</summary>
        public List<ChainlingBattleStatContribution> pveStatContributions;

        /// <summary>Narrative encounter id stamped from the encounter snapshot.</summary>
        public string narrativeEncounterId;

        /// <summary>Copied from encounter at battle start; consumed by post-battle victory grant service.</summary>
        public List<EncounterVictoryKnownGrant> pendingVictoryKnownGrants = new List<EncounterVictoryKnownGrant>();

        /// <summary>Resolved victory Known grants after persistence.</summary>
        public List<EncounterVictoryKnownGrantResult> victoryKnownGrantResults = new List<EncounterVictoryKnownGrantResult>();

        /// <summary>Stable hash of the legacy combat-event stream for replay/idempotency context.</summary>
        public string forensicHash;

        /// <summary>Active combat ruleset id stamped at battle start (competitive fairness / replay).</summary>
        public string rulesetId;

        /// <summary>Config hash of the active ruleset (+ resolver) stamped at battle start.</summary>
        public string configHash;

        /// <summary>
        /// BattleService's authoritative RAM receipt. The local reward calculator must never
        /// be used as proof that RAM was credited.
        /// </summary>
        public BattleRewardSettlement authoritativeRamSettlement;

        public BattleResult()
        {
            encounterDifficulty = EncounterDifficulty.Standard;
            playerParticipants = new List<BattleParticipant>();
            enemyKOsBySpecies = new Dictionary<string, int>(StringComparer.Ordinal);
            pveStatContributions = new List<ChainlingBattleStatContribution>();
            captures = new List<BattleCaptureRecord>();
            pendingVictoryKnownGrants = new List<EncounterVictoryKnownGrant>();
            victoryKnownGrantResults = new List<EncounterVictoryKnownGrantResult>();
        }

        /// <summary>True if the viewing seat won. PvE viewers are team 0.</summary>
        public bool PlayerWon => winnerTeamIndex == viewerTeamIndex;

        /// <summary>True if the battle ended in a draw.</summary>
        public bool IsDraw => winnerTeamIndex == -2;

        /// <summary>True if this is a PvP wager match.</summary>
        public bool IsPvP => battleType == BattleType.PvP;

        /// <summary>True if a capture was attempted and succeeded.</summary>
        public bool DidCapture => captureAttempted && captureSucceeded;

        public bool EndedByCapture => completionReason == BattleCompletionReason.Captured ||
                                      completionReason == BattleCompletionReason.TargetEscaped;
    }

    [Serializable]
    public class BattleCaptureRecord
    {
        public string speciesId;
        public string displayName;
        public string subjectLabel;
        public bool isMutated;
        public bool isEntangled;
        public float durationSeconds;
        public bool succeeded;
        public string backendChainlingId;
    }

    [Serializable]
    public class BattleParticipant
    {
        public int rosterIndex;
        public string instanceId;
        public string speciesId;
        public string displayName;
        public string backendChainlingId;
        public int masteryXp;
        public int stars;
        public int rebirthCount;
        public bool isEntangled;
        public List<string> knownMoveIds = new List<string>();
        public bool wasActive;
        public bool wasBenched;
        public bool wasKOd;
        public int endCurrentHp;
        public int endMaxHp;
    }
}
