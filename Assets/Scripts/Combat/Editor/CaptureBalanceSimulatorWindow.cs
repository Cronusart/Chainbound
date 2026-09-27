using System;
using System.Text;
using Chainbound.Combat.Core;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Deterministic Monte Carlo harness for capture lever boards. The bot deliberately
    /// observes on a reaction cadence and tracks with a dead zone so it models practical
    /// competence instead of reading a guaranteed winning input from the simulation.
    /// </summary>
    public sealed class CaptureBalanceSimulatorWindow : EditorWindow
    {
        private enum OrbPreset
        {
            Signal,
            Lattice,
            Quick,
            Entangle,
        }

        private CaptureBalanceConfigSO balanceConfig;
        private CaptureVolatilityProfile profile = CaptureVolatilityProfile.Standard;
        private OrbPreset orb = OrbPreset.Signal;
        private int simulationCount = 5000;
        private int targetHpPercent = 15;
        private int averagePositiveIv = 0;
        private int alertness;
        private bool mutated;
        private bool entangled;
        private int variantRarityRank;
        private int mutationRarityRank;
        private float botCompetence = 0.60f;
        private bool equippedHunter;
        private int seed = 1731;
        private Vector2 scroll;
        private string report = string.Empty;

        [MenuItem("Tools/Chainbound/Combat/Capture Balance Simulator")]
        public static void ShowWindow()
        {
            var window = GetWindow<CaptureBalanceSimulatorWindow>("Capture Simulator");
            window.minSize = new Vector2(580f, 520f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Trading-Trace Capture Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs the shared fixed-point Core with a reaction-limited tracking bot. " +
                "Profile is the Calm/Standard/Volatile/Extreme motion kit. HP at or below 15% has no HP resistance; " +
                "the first tick above 15% applies the cliff floor, then ramps to max HP. " +
                "Trait toggles use CaptureBalanceConfig tables. Use identical seeds when comparing board edits. " +
                "See Docs/Specs/Combat/CAPTURE_DIFFICULTY_COMPOSITION_v1.md.",
                MessageType.Info);

            balanceConfig = (CaptureBalanceConfigSO)EditorGUILayout.ObjectField(
                "Capture Balance", balanceConfig, typeof(CaptureBalanceConfigSO), false);
            profile = (CaptureVolatilityProfile)EditorGUILayout.EnumPopup("Profile", profile);
            orb = (OrbPreset)EditorGUILayout.EnumPopup("Orb", orb);
            simulationCount = Mathf.Clamp(EditorGUILayout.IntField("Attempts", simulationCount), 1, 100000);
            targetHpPercent = EditorGUILayout.IntSlider("Target HP %", targetHpPercent, 1, 100);
            averagePositiveIv = EditorGUILayout.IntSlider(
                "All non-HP IVs",
                averagePositiveIv,
                CombatConstants.IV_MIN,
                CombatConstants.IV_MAX);
            alertness = EditorGUILayout.IntSlider("Alertness", alertness, 0, 20);
            mutated = EditorGUILayout.Toggle("Mutated", mutated);
            mutationRarityRank = EditorGUILayout.IntSlider("Mutation rarity", mutationRarityRank, 0, 5);
            entangled = EditorGUILayout.Toggle("Entangled", entangled);
            variantRarityRank = EditorGUILayout.IntSlider("Variant rarity", variantRarityRank, 0, 5);
            botCompetence = EditorGUILayout.Slider("Bot Competence", botCompetence, 0f, 1f);
            equippedHunter = EditorGUILayout.Toggle("Hunter Loadout", equippedHunter);
            seed = EditorGUILayout.IntField("Comparison Seed", seed);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Run Monte Carlo", GUILayout.Height(28f)))
                RunSimulation();

            EditorGUILayout.Space(8f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunSimulation()
        {
            CaptureBalanceData rules = balanceConfig != null
                ? balanceConfig.ToCoreData()
                : CaptureBalanceData.CreateDefault().Normalized();
            CaptureMotionProfile motion = rules.ResolveProfile(profile);
            CaptureEffectVector hunterEffects = equippedHunter
                ? new CaptureEffectVector(5, 0, 1500, 0, 0)
                : CaptureEffectVector.None;
            CaptureOrbProfile orbProfile = ResolveOrb(orb);

            int locked = 0;
            int escaped = 0;
            int timedOut = 0;
            long lockTicks = 0;
            long finalMeter = 0;
            int totalInsideTicks = 0;
            int totalTicks = 0;

            for (int i = 0; i < simulationCount; i++)
            {
                uint attemptSeed = unchecked((uint)seed) ^ DeterministicCaptureRng.StableSeed("capture-bot", unchecked((uint)i));
                CaptureChallengeSnapshot challenge = CaptureChallengeResolver.Resolve(
                    "simulation-" + i,
                    "balance-tool",
                    attemptSeed,
                    0,
                    "editor-simulation",
                    BuildTarget(i),
                    motion,
                    orbProfile,
                    hunterEffects,
                    CaptureEffectVector.None,
                    rules);

                CaptureSimulationState state = CaptureSimulation.CreateInitial(challenge);
                var botRng = new DeterministicCaptureRng(attemptSeed ^ 0xB07C0DEu);
                int reactionTicks = Mathf.RoundToInt(Mathf.Lerp(12f, 2f, botCompetence));
                int lookAheadTicks = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, botCompetence));
                int deadZone = Mathf.RoundToInt(Mathf.Lerp(900f, 140f, botCompetence));
                int observationNoise = Mathf.RoundToInt(Mathf.Lerp(900f, 30f, botCompetence));
                int observedTarget = state.TargetPositionUnits;
                bool held = false;

                while (state.Outcome == CaptureOutcomeKind.None)
                {
                    if (state.Tick % Math.Max(1, reactionTicks) == 0)
                    {
                        observedTarget = state.TargetPositionUnits +
                            botRng.NextInt(-observationNoise, observationNoise + 1);
                        int projectedBand = state.BandPositionUnits +
                            (state.BandVelocityUnitsPerSecond * lookAheadTicks /
                             Math.Max(1, challenge.Parameters.TickRate));
                        if (projectedBand < observedTarget - deadZone)
                            held = true;
                        else if (projectedBand > observedTarget + deadZone)
                            held = false;
                    }

                    state = CaptureSimulation.Step(state, challenge.Parameters, held).State;
                    if (state.IsInsideBand)
                        totalInsideTicks++;
                    totalTicks++;
                }

                finalMeter += state.MeterBasisPoints;
                switch (state.Outcome)
                {
                    case CaptureOutcomeKind.Locked:
                        locked++;
                        lockTicks += state.Tick;
                        break;
                    case CaptureOutcomeKind.Escaped:
                        escaped++;
                        break;
                    default:
                        timedOut++;
                        break;
                }
            }

            float scale = 100f / Math.Max(1, simulationCount);
            float averageLockSeconds = locked > 0
                ? (float)lockTicks / locked / Math.Max(1, rules.TickRate)
                : 0f;
            var sb = new StringBuilder(512);
            sb.AppendLine("=== Capture Monte Carlo ===");
            sb.AppendLine("Attempts: " + simulationCount);
            sb.AppendLine("Profile: " + profile);
            sb.AppendLine("Orb: " + orb);
            sb.AppendLine("HP: " + targetHpPercent + "%");
            sb.AppendLine("Non-HP IVs: " + averagePositiveIv);
            sb.AppendLine("Alertness: " + alertness);
            sb.AppendLine("Mutated: " + (mutated ? "yes (" + mutationRarityRank + ")" : "no"));
            sb.AppendLine("Entangled: " + (entangled ? "yes" : "no"));
            sb.AppendLine("Variant rarity: " + variantRarityRank);
            sb.AppendLine("Bot competence: " + botCompetence.ToString("0.00"));
            sb.AppendLine("Hunter loadout: " + (equippedHunter ? "yes" : "no"));
            sb.AppendLine();
            sb.AppendLine("Locked:   " + locked + " (" + (locked * scale).ToString("0.00") + "%)");
            sb.AppendLine("Escaped:  " + escaped + " (" + (escaped * scale).ToString("0.00") + "%)");
            sb.AppendLine("Timed out:" + timedOut + " (" + (timedOut * scale).ToString("0.00") + "%)");
            sb.AppendLine("Mean final meter: " + ((float)finalMeter / Math.Max(1, simulationCount) / 100f).ToString("0.00") + "%");
            sb.AppendLine("Time in band: " + (100f * totalInsideTicks / Math.Max(1, totalTicks)).ToString("0.00") + "%");
            sb.AppendLine("Mean successful lock time: " + averageLockSeconds.ToString("0.00") + "s");
            sb.AppendLine();
            sb.AppendLine("Tuning targets from the capture rollout plan:");
            sb.AppendLine("Standard competence: 60–75% Locked");
            sb.AppendLine("Extreme baseline: 5–15% Locked");
            sb.AppendLine("Extreme equipped hunter: 25–40% Locked");
            sb.AppendLine("Extreme failures should skew toward Timed Out.");
            report = sb.ToString();
        }

        private CaptureTargetContext BuildTarget(int index)
        {
            return new CaptureTargetContext
            {
                TargetId = "balance-target-" + index,
                SpeciesId = "balance-target",
                CurrentHp = targetHpPercent,
                MaxHp = 100,
                LegacyBaseCaptureRate = LegacyRateFor(profile),
                Alertness = alertness,
                IsMutated = mutated,
                IsEntangled = entangled,
                VariantRarityRank = variantRarityRank,
                MutationRarityRank = mutated ? mutationRarityRank : 0,
                Ivs = new CaptureIvValues
                {
                    MeleeAttack = averagePositiveIv,
                    MeleeDefense = averagePositiveIv,
                    RangedAttack = averagePositiveIv,
                    RangedDefense = averagePositiveIv,
                    Speed = averagePositiveIv,
                },
            };
        }

        private static int LegacyRateFor(CaptureVolatilityProfile value)
        {
            switch (value)
            {
                case CaptureVolatilityProfile.Calm: return 70;
                case CaptureVolatilityProfile.Standard: return 45;
                case CaptureVolatilityProfile.Volatile: return 25;
                default: return 10;
            }
        }

        private static CaptureOrbProfile ResolveOrb(OrbPreset value)
        {
            switch (value)
            {
                case OrbPreset.Lattice:
                    return new CaptureOrbProfile
                    {
                        OrbId = "lattice_orb",
                        Effects = new CaptureEffectVector(0, 0, 2500, -1500, 0),
                    };
                case OrbPreset.Quick:
                    return new CaptureOrbProfile
                    {
                        OrbId = "quick_orb",
                        Effects = new CaptureEffectVector(0, 0, -2000, 3000, 0),
                    };
                case OrbPreset.Entangle:
                    return new CaptureOrbProfile
                    {
                        OrbId = "entangle_orb",
                        Effects = new CaptureEffectVector(0, 0, -2000, 0, -2500),
                        EntanglesOnSuccess = true,
                    };
                default:
                    return new CaptureOrbProfile
                    {
                        OrbId = "signal_orb",
                        Effects = CaptureEffectVector.None,
                    };
            }
        }
    }
}
