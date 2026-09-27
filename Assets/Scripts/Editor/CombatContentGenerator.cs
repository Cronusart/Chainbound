#if ENABLE_LEGACY_COMBAT
using UnityEngine;
using UnityEditor;
using System.IO;
using SignalJam.Combat;

namespace SignalJam.Editor
{
    /// <summary>
    /// Editor utility to generate sample combat content.
    /// Creates 8 Data States, 9 Abilities, and 3 Chainlings per spec.
    /// LEGACY: This tool depends on the old combat system. It is disabled unless
    /// ENABLE_LEGACY_COMBAT is defined in Player Settings.
    /// </summary>
    public class CombatContentGenerator : EditorWindow
    {
        [MenuItem("Tools/Chainbound/Combat/Generate Legacy Combat Content")]
        public static void ShowWindow()
        {
            GetWindow<CombatContentGenerator>("Combat Content Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Combat Content Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            if (GUILayout.Button("Generate All Content", GUILayout.Height(30)))
            {
                GenerateAllContent();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Generate Data States Only"))
            {
                GenerateDataStates();
            }

            if (GUILayout.Button("Generate Abilities Only"))
            {
                GenerateAbilities();
            }

            if (GUILayout.Button("Generate Chainlings Only"))
            {
                GenerateChainlings();
            }
        }

        private void GenerateAllContent()
        {
            GenerateDataStates();
            GenerateAbilities();
            GenerateChainlings();
            Debug.Log("All combat content generated!");
        }

        private void GenerateDataStates()
        {
            string folderPath = "Assets/Data/Combat/DataStates";
            EnsureFolderExists(folderPath);

            CreateDataState(DataStateType.Compressed, "Compressed", 
                "+4% Integrity regen",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Volatile, "-25% Volatile potency"),
                    CreateEdge(DataStateType.Fragmented, "Convert 1 Control duration - -3% Integrity")
                },
                folderPath);

            CreateDataState(DataStateType.Encrypted, "Encrypted",
                "Control duration -1 (min 1)",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Corrupted, "Cleanse 1 tick at end of turn"),
                    CreateEdge(DataStateType.Spectral, "-50% echo chance")
                },
                folderPath);

            CreateDataState(DataStateType.Fragmented, "Fragmented",
                "+2% Integrity chip per hit",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Compressed, "Suppress regen -8% next turn"),
                    CreateEdge(DataStateType.Synthetic, "Disable variance reduction 1 turn")
                },
                folderPath);

            CreateDataState(DataStateType.Volatile, "Volatile",
                "+10% non-damage potency, -4% regen",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Fluid, "Apply Aftershock on swap-in"),
                    CreateEdge(DataStateType.Encrypted, "First Control ignores reduction")
                },
                folderPath);

            CreateDataState(DataStateType.Corrupted, "Corrupted",
                "Debuffs last +1 turn",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Compressed, "Regen -4% while debuffed"),
                    CreateEdge(DataStateType.Fluid, "Debuffs tick off-field")
                },
                folderPath);

            CreateDataState(DataStateType.Synthetic, "Synthetic",
                "-25% incoming debuff potency, -25% outgoing buff potency",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Volatile, "Halve Volatile bonus"),
                    CreateEdge(DataStateType.Spectral, "Block echo once per battle")
                },
                folderPath);

            CreateDataState(DataStateType.Fluid, "Fluid",
                "First swap cleanses 1 debuff",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Fragmented, "-1% chip"),
                    CreateEdge(DataStateType.Corrupted, "Cleanse 1 Corrupted tick on swap-in")
                },
                folderPath);

            CreateDataState(DataStateType.Spectral, "Spectral",
                "20% chance to echo minor non-damage effect",
                new DataStateEdge[] {
                    CreateEdge(DataStateType.Encrypted, "Resisted echo - -3% Integrity"),
                    CreateEdge(DataStateType.Synthetic, "Bypass echo block once")
                },
                folderPath);

            Debug.Log("8 Data States generated!");
        }

        private void GenerateAbilities()
        {
            string folderPath = "Assets/Data/Combat/Abilities";
            EnsureFolderExists(folderPath);

            CreateAbility("Quick Ping", "Fast low-damage attack", SpeedTag.Fast, 0, 0, 15, folderPath);
            CreateAbility("Packet Snare", "Control debuff", SpeedTag.Normal, 2, 0, 10, folderPath);
            CreateAbility("Heavy Byte", "High-damage attack", SpeedTag.Slow, 3, 0, 50, folderPath);
            CreateAbility("Firewall Shell", "Integrity buff", SpeedTag.Fast, 2, 0, 0, folderPath);
            CreateAbility("Signal Leak", "Integrity chip damage", SpeedTag.Normal, 1, 0, 5, folderPath);
            CreateAbility("Pressure Ping", "Damage with debuff", SpeedTag.Fast, 1, 0, 20, folderPath);
            CreateAbility("Overload Spike", "Delayed high damage", SpeedTag.Delayed, 4, 0, 80, folderPath);
            CreateAbility("Rollback", "Cleanse self", SpeedTag.Instant, 3, 0, 0, folderPath);
            CreateAbility("Checksum Break", "Reduce target Integrity regen", SpeedTag.Normal, 2, 0, 12, folderPath);

            Debug.Log("9 Abilities generated!");
        }

        private void GenerateChainlings()
        {
            string folderPath = "Assets/Data/Combat/Species";
            EnsureFolderExists(folderPath);

            CreateSpecies("Swiftpaw", 80, 45, 75, 100f, DataStateType.Fluid, AffinityType.Momentum, folderPath);
            CreateSpecies("Bulwark", 150, 35, 30, 100f, DataStateType.Compressed, AffinityType.Survival, folderPath);
            CreateSpecies("Fangfire", 100, 70, 50, 100f, DataStateType.Volatile, AffinityType.Strike, folderPath);

            Debug.Log("3 Chainlings generated!");
        }

        private void CreateDataState(DataStateType type, string name, string signatureDesc, DataStateEdge[] edges, string folderPath)
        {
            DataStateDef state = ScriptableObject.CreateInstance<DataStateDef>();
            state.stateType = type;
            state.signatureName = $"{name} Signature";
            state.signatureDescription = signatureDesc;
            SetSignatureEffect(state, type, signatureDesc);
            state.edges = edges;

            string assetPath = $"{folderPath}/{name}.asset";
            AssetDatabase.CreateAsset(state, assetPath);
        }

        private void SetSignatureEffect(DataStateDef state, DataStateType type, string signatureDesc)
        {
            switch (type)
            {
                case DataStateType.Compressed:
                    state.signatureEffectType = "IntegrityRegenModifier";
                    state.signatureEffectParams = "{\"modifier\": 4.0}";
                    break;
                case DataStateType.Fragmented:
                    state.signatureEffectType = "IntegrityChipBonus";
                    state.signatureEffectParams = "{\"amount\": 2.0}";
                    break;
                case DataStateType.Volatile:
                    state.signatureEffectType = "NonDamagePotencyBonus";
                    state.signatureEffectParams = "{\"multiplier\": 1.1, \"regenPenalty\": -4.0}";
                    break;
                case DataStateType.Encrypted:
                    state.signatureEffectType = "ControlDurationModifier";
                    state.signatureEffectParams = "{\"reduction\": 1}";
                    break;
                case DataStateType.Corrupted:
                    state.signatureEffectType = "DebuffDurationBonus";
                    state.signatureEffectParams = "{\"bonus\": 1}";
                    break;
                case DataStateType.Synthetic:
                    state.signatureEffectType = "DebuffPotencyModifier";
                    state.signatureEffectParams = "{\"incoming\": -0.25, \"outgoing\": -0.25}";
                    break;
                case DataStateType.Fluid:
                    state.signatureEffectType = "SwapCleanse";
                    state.signatureEffectParams = "{\"count\": 1}";
                    break;
                case DataStateType.Spectral:
                    state.signatureEffectType = "SpectralEcho";
                    state.signatureEffectParams = "{\"chance\": 0.2}";
                    break;
                default:
                    state.signatureEffectType = "Unknown";
                    state.signatureEffectParams = "{}";
                    break;
            }
        }

        private DataStateEdge CreateEdge(DataStateType targetState, string description)
        {
            DataStateEdge edge = new DataStateEdge();
            edge.targetState = targetState;
            edge.edgeDescription = description;
            SetEdgeEffect(edge, description);
            return edge;
        }

        private void SetEdgeEffect(DataStateEdge edge, string description)
        {
            if (description.Contains("-25%") || description.Contains("potency"))
            {
                edge.edgeEffectType = "PotencyReduction";
                edge.edgeEffectParams = "{\"multiplier\": 0.75}";
            }
            else if (description.Contains("Suppress regen") || description.Contains("regen -8%"))
            {
                edge.edgeEffectType = "RegenSuppression";
                edge.edgeEffectParams = "{\"amount\": -8.0}";
            }
            else if (description.Contains("Disable variance"))
            {
                edge.edgeEffectType = "VarianceDisable";
                edge.edgeEffectParams = "{\"turns\": 1}";
            }
            else if (description.Contains("First Control ignores"))
            {
                edge.edgeEffectType = "ControlIgnore";
                edge.edgeEffectParams = "{\"count\": 1}";
            }
            else if (description.Contains("regen -4%") && description.Contains("debuffed"))
            {
                edge.edgeEffectType = "RegenDebuffPenalty";
                edge.edgeEffectParams = "{\"amount\": -4.0}";
            }
            else if (description.Contains("Halve"))
            {
                edge.edgeEffectType = "PotencyHalve";
                edge.edgeEffectParams = "{\"multiplier\": 0.5}";
            }
            else if (description.Contains("-1% chip") || description.Contains("-1% Integrity chip"))
            {
                edge.edgeEffectType = "ChipReduction";
                edge.edgeEffectParams = "{\"amount\": -1.0}";
            }
            else if (description.Contains("Cleanse"))
            {
                edge.edgeEffectType = "Cleanse";
                edge.edgeEffectParams = "{\"count\": 1}";
            }
            else if (description.Contains("-50% echo") || description.Contains("echo chance"))
            {
                edge.edgeEffectType = "EchoReduction";
                edge.edgeEffectParams = "{\"multiplier\": 0.5}";
            }
            else if (description.Contains("Block echo"))
            {
                edge.edgeEffectType = "EchoBlock";
                edge.edgeEffectParams = "{\"count\": 1}";
            }
            else if (description.Contains("Bypass echo"))
            {
                edge.edgeEffectType = "EchoBypass";
                edge.edgeEffectParams = "{\"count\": 1}";
            }
            else if (description.Contains("Aftershock"))
            {
                edge.edgeEffectType = "Aftershock";
                edge.edgeEffectParams = "{}";
            }
            else if (description.Contains("tick off-field"))
            {
                edge.edgeEffectType = "OffFieldTick";
                edge.edgeEffectParams = "{}";
            }
            else if (description.Contains("Integrity"))
            {
                edge.edgeEffectType = "IntegrityModifier";
                edge.edgeEffectParams = "{\"amount\": -3.0}";
            }
            else
            {
                edge.edgeEffectType = "Custom";
                edge.edgeEffectParams = "{}";
            }
        }

        private void CreateAbility(string name, string description, SpeedTag speedTag, int cooldown, int maxUses, int baseDamage, string folderPath)
        {
            AbilityDef ability = ScriptableObject.CreateInstance<AbilityDef>();
            ability.abilityName = name;
            ability.description = description;
            ability.speedTag = speedTag;
            ability.cooldown = cooldown;
            ability.maxUses = maxUses;
            ability.baseDamage = baseDamage;
            ability.effects = new AbilityEffect[0];

            string assetPath = $"{folderPath}/{name}.asset";
            AssetDatabase.CreateAsset(ability, assetPath);
        }

        private void CreateSpecies(string name, int hp, int atk, int spd, float integrity, DataStateType preferredState, AffinityType affinity, string folderPath)
        {
            SpeciesDef species = ScriptableObject.CreateInstance<SpeciesDef>();
            species.speciesName = name;
            species.baseHP = hp;
            species.baseATK = atk;
            species.baseSPD = spd;
            species.baseIntegrity = integrity;
            species.preferredDataState = preferredState;
            species.affinityType = affinity;
            species.defaultAbilities = new AbilityDef[0];

            string assetPath = $"{folderPath}/{name}.asset";
            AssetDatabase.CreateAsset(species, assetPath);
        }

        private void EnsureFolderExists(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parentFolder = Path.GetDirectoryName(folderPath).Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }
}
#endif
