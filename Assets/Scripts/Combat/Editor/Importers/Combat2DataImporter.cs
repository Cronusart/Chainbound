using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Editor window for importing spec text files into Combat ScriptableObject assets.
    /// Imports combat data from CSV files and spec text.
    /// Menu: Tools/Chainbound/Combat/Data Importer
    /// </summary>
    public class Combat2DataImporter : EditorWindow
    {
        private string _speciesMovePoolsSpecPath = "Docs/Specs/Combat/COMBAT_Species.txt";
        private string _dataFolder = "Docs/Specs/Combat/Data";
        private string _outputFolder = "Assets/Data/Combat";

        // Random matchup: Party = player team, Encounter = opponent team
        private ManualPartyProviderSO _partyProvider;
        private ManualEncounterProviderSO _encounterProvider;
        private int _randomRosterSize = CombatConstants.ROSTER_SIZE;
        private const int MovesPerChainling = 4;

        // Organized subpaths: each category has its own folder, with a Database/Catalog subfolder for the collection asset
        private string MovesFolder => $"{_outputFolder}/Moves";
        private string MovesDatabaseFolder => $"{_outputFolder}/Moves/Database";
        private string ModifiersFolder => $"{_outputFolder}/Modifiers";
        private string ModifiersCatalogFolder => $"{_outputFolder}/Modifiers/Catalog";
        private string StatusDefFolder => $"{_outputFolder}/StatusDef";
        private string StatusDefCatalogFolder => $"{_outputFolder}/StatusDef/Catalog";
        private string ChainlingSpeciesFolder => $"{_outputFolder}/ChainlingSpecies";
        private string ChainlingSpeciesDatabaseFolder => $"{_outputFolder}/ChainlingSpecies/Database";
        private string TypeChemistryFolder => $"{_outputFolder}/Type/Chemistry";
        private Vector2 _scrollPos;
        private string _logOutput = "";
        private bool _dryRun = false;
        private int _modifierDensity = 1;
        private UnityEngine.Object _targetProvider;

        [MenuItem("Tools/Chainbound/Combat/Data Importer")]
        public static void ShowWindow()
        {
            GetWindow<Combat2DataImporter>("Combat Data Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Combat Data Importer", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("This tool generates ScriptableObject assets from CSV files and spec text.");
            GUILayout.Label("If parsing fails, create assets manually using the SO menus.");
            GUILayout.Space(10);

            // Path fields
            GUILayout.BeginHorizontal();
            GUILayout.Label("Move Pools Spec:", GUILayout.Width(120));
            _speciesMovePoolsSpecPath = GUILayout.TextField(_speciesMovePoolsSpecPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select Species Move Pools Spec", "Assets", "txt");
                if (!string.IsNullOrEmpty(path))
                    _speciesMovePoolsSpecPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Data Folder:", GUILayout.Width(120));
            _dataFolder = GUILayout.TextField(_dataFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Data Folder", "Docs/Specs/Combat", "Data");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _dataFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _dataFolder = path;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Output Folder:", GUILayout.Width(120));
            _outputFolder = GUILayout.TextField(_outputFolder);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            _dryRun = GUILayout.Toggle(_dryRun, "Dry Run (log only, no writes)", GUILayout.Width(280));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // CSV Data Import buttons
            GUILayout.Label("CSV Data Import", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Import Statuses (CSV)", GUILayout.Height(30)))
            {
                ImportStatusesFromCSV();
            }

            if (GUILayout.Button("Import Moves (CSV)", GUILayout.Height(30)))
            {
                ImportMovesFromCSV();
            }

            if (GUILayout.Button("Validate Moves CSV", GUILayout.Height(30)))
            {
                string summary = CombatMovesCsvValidation.ValidateAndWriteReport(
                    ResolveCsvPath("Moves.csv"),
                    CombatMovesCsvValidation.DefaultReportPath);
                _logOutput = summary + "\n";
            }

            if (GUILayout.Button("Audit Moves CSV vs Assets", GUILayout.Height(30)))
            {
                string summary = CombatMovesCsvParityAuditor.AuditAndWriteReport(
                    ResolveCsvPath("Moves.csv"),
                    MovesFolder,
                    $"{MovesDatabaseFolder}/MoveDatabase.asset",
                    CombatMovesCsvParityAuditor.DefaultReportPath);
                _logOutput = summary + "\n";
            }

            if (GUILayout.Button("Export Moves to CSV", GUILayout.Height(30)))
            {
                ExportMovesToCSV();
            }

            if (GUILayout.Button("Import Modifiers (CSV)", GUILayout.Height(30)))
            {
                ImportModifiersFromCSV();
            }

            if (GUILayout.Button("Export Modifiers to CSV", GUILayout.Height(30)))
            {
                ExportModifiersToCSV();
            }

            if (GUILayout.Button("Import Chemistry Matrix (CSV)", GUILayout.Height(30)))
            {
                ImportChemistryMatrixFromCSV();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Import Species Pools (CSV)", GUILayout.Height(30)))
            {
                ImportSpeciesPoolsFromCSV();
            }

            if (GUILayout.Button("Import All From Data", GUILayout.Height(30)))
            {
                ImportAllFromData();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Random Matchup Generation
            GUILayout.Label("Random Matchup", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Party Provider:", GUILayout.Width(120));
            _partyProvider = (ManualPartyProviderSO)EditorGUILayout.ObjectField(_partyProvider, typeof(ManualPartyProviderSO), false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Encounter Provider:", GUILayout.Width(120));
            _encounterProvider = (ManualEncounterProviderSO)EditorGUILayout.ObjectField(_encounterProvider, typeof(ManualEncounterProviderSO), false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Roster Size:", GUILayout.Width(120));
            _randomRosterSize = EditorGUILayout.IntSlider(_randomRosterSize, 1, CombatConstants.ROSTER_SIZE);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Random Lineup (Party)", GUILayout.Height(25)))
            {
                GenerateRandomLineup(_partyProvider);
            }
            if (GUILayout.Button("Generate Random Lineup (Encounter)", GUILayout.Height(25)))
            {
                GenerateRandomLineup(_encounterProvider);
            }
            if (GUILayout.Button("Generate Random Matchup", GUILayout.Height(25)))
            {
                GenerateRandomMatchup();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Test & Balance Tooling
            GUILayout.Label("Test & Balance Tooling", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target Provider:", GUILayout.Width(120));
            _targetProvider = EditorGUILayout.ObjectField(_targetProvider, typeof(ScriptableObject), false);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Modifier Density:", GUILayout.Width(120));
            _modifierDensity = EditorGUILayout.IntSlider(_modifierDensity, 0, 2);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Assign Random Test Modifiers", GUILayout.Height(30)))
            {
                AssignRandomTestModifiers();
            }

            if (GUILayout.Button("Apply Loadout (CSV)", GUILayout.Height(30)))
            {
                ApplyLoadoutFromCSV();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Starter content generation
            GUILayout.Label("Starter Generation", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Empty Status Catalog", GUILayout.Height(30)))
            {
                GenerateStatusCatalog();
            }

            if (GUILayout.Button("Generate Empty Stamped DB", GUILayout.Height(30)))
            {
                GenerateEmptyMoveDatabase();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate All Empty Databases", GUILayout.Height(30)))
            {
                GenerateAllEmptyDatabases();
            }

            if (GUILayout.Button("Generate Starter Species", GUILayout.Height(30)))
            {
                GenerateStarterSpecies();
            }

            if (GUILayout.Button("Generate Starter Stamped moves (4 each)", GUILayout.Height(30)))
            {
                GenerateStarterStampedMovesAndAssign();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Import Species Move Pools (Spec 06)", GUILayout.Height(30)))
            {
                ImportSpeciesMovePools();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Log output
            GUILayout.Label("Output Log:", EditorStyles.boldLabel);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
            GUILayout.Label(_logOutput, EditorStyles.wordWrappedLabel);
            GUILayout.EndScrollView();
        }

        /// <summary>Ensures the full organized folder hierarchy exists for combat data.</summary>
        private void EnsureOrganizedFolders()
        {
            EnsureFolderExists(_outputFolder);
            EnsureFolderExists(MovesFolder);
            EnsureFolderExists(MovesDatabaseFolder);
            EnsureFolderExists(ModifiersFolder);
            EnsureFolderExists(ModifiersCatalogFolder);
            EnsureFolderExists(StatusDefFolder);
            EnsureFolderExists(StatusDefCatalogFolder);
            EnsureFolderExists(ChainlingSpeciesFolder);
            EnsureFolderExists(ChainlingSpeciesDatabaseFolder);
            EnsureFolderExists(TypeChemistryFolder);
        }

        private static void EnsureFolderExists(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);
            if (!string.IsNullOrEmpty(folder))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private void GenerateStatusCatalog()
        {
            EnsureOrganizedFolders();

            var catalog = ScriptableObject.CreateInstance<StatusCatalogSO>();
            _logOutput = "Generating Status Catalog...\n";

            // Create status definitions for each StatusId
            var statusIds = (StatusId[])Enum.GetValues(typeof(StatusId));
            foreach (var id in statusIds)
            {
                if (id == StatusId.None) continue;

                var def = ScriptableObject.CreateInstance<StatusDefinitionSO>();
                def.statusId = id;
                def.displayName = id.ToString();
                def.effectSummary = $"[TODO: Fill from spec] {id}";
                def.statusClass = StatusClass.Duration;
                def.origin = (int)id >= 100 && (int)id < 200 ? StatusOrigin.ChemistryOnly : StatusOrigin.General;
                def.defaultDuration = 2;
                def.name = $"StatusDef_{id}";

                string defPath = $"{StatusDefFolder}/StatusDef_{id}.asset";
                AssetDatabase.CreateAsset(def, defPath);
                catalog.definitions.Add(def);

                _logOutput += $"  Created: {defPath}\n";
            }

            string catalogPath = $"{StatusDefCatalogFolder}/StatusCatalog.asset";
            AssetDatabase.CreateAsset(catalog, catalogPath);
            _logOutput += $"\nStatus Catalog saved to: {catalogPath}\n";
            _logOutput += $"Total: {catalog.definitions.Count} status definitions\n";
            _logOutput += "\nIMPORTANT: Review each StatusDefinitionSO and fill in correct values from spec.\n";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void GenerateEmptyMoveDatabase()
        {
            EnsureOrganizedFolders();

            var database = ScriptableObject.CreateInstance<MoveDatabaseSO>();
            _logOutput = "Generating Empty Stamped Database...\n";

            string dbPath = $"{MovesDatabaseFolder}/MoveDatabase.asset";
            AssetDatabase.CreateAsset(database, dbPath);

            _logOutput += $"Empty database saved to: {dbPath}\n";
            _logOutput += "Use Combat/Stamped Definition to create individual Stamped SOs.\n";
            _logOutput += "Then add them to the database's Stamped moves list.\n";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void GenerateAllEmptyDatabases()
        {
            EnsureOrganizedFolders();
            _logOutput = "Generating all empty databases...\n\n";

            // Status Catalog
            GenerateStatusCatalog();

            // Stamped Database
            var inscDb = ScriptableObject.CreateInstance<MoveDatabaseSO>();
            AssetDatabase.CreateAsset(inscDb, $"{MovesDatabaseFolder}/MoveDatabase.asset");
            _logOutput += $"Created: Moves/Database/MoveDatabase.asset\n";

            // Modifier Catalog
            var modCat = ScriptableObject.CreateInstance<ModifierCatalogSO>();
            AssetDatabase.CreateAsset(modCat, $"{ModifiersCatalogFolder}/ModifierCatalog.asset");
            _logOutput += $"Created: Modifiers/Catalog/ModifierCatalog.asset\n";

            // Species Database
            var speciesDb = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            AssetDatabase.CreateAsset(speciesDb, $"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset");
            _logOutput += $"Created: ChainlingSpecies/Database/SpeciesDatabase.asset\n";

            // Chemistry Matrix
            var chemMatrix = ScriptableObject.CreateInstance<TypeChemistryMatrixSO>();
            AssetDatabase.CreateAsset(chemMatrix, $"{TypeChemistryFolder}/TypeChemistryMatrix.asset");
            _logOutput += $"Created: Type/Chemistry/TypeChemistryMatrix.asset\n";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _logOutput += "\nAll empty databases created. Fill via Inspector or extend this importer.\n";
        }

        private void GenerateStarterSpecies()
        {
            EnsureOrganizedFolders();
            _logOutput = "Generating Starter Species...\n\n";

            // Validape ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Node type
            var validape = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            validape.speciesId = "validape";
            validape.displayName = "Validape";
            validape.description = "It lives in noisy troops and never travels far from its kin. A blow from its hammer is said to lift the spirits of every ape nearby.";
            validape.primaryType = ChainlingType.Node;
            validape.secondaryType = ChainlingType.None;
            validape.baseMaxHP = 110;
            validape.baseMeleeAtk = 65;
            validape.baseMeleeDef = 55;
            validape.baseRangedAtk = 50;
            validape.baseRangedDef = 55;
            validape.baseSpeed = 55;
            validape.maxEvolutionStages = 3;
            validape.isStarter = true;
            validape.combatRoleOverride = SpeciesCombatRole.Support;
            validape.designNotes = "Starter. Exclusive moves: Validape Slam, Extend Protocol (Spec 06 v1.2).";
            validape.name = "Species_Validape";
            AssetDatabase.CreateAsset(validape, $"{ChainlingSpeciesFolder}/Species_Validape.asset");
            _logOutput += "Created: Species_Validape.asset\n";

            // Gilarime ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Frozen type
            var gilarime = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            gilarime.speciesId = "gilarime";
            gilarime.displayName = "Gilarime";
            gilarime.description = "Its hide stays ice-cold even under fire. Abandoned nests are often found with a Gilarime still standing watch, raygun trained on the dark.";
            gilarime.primaryType = ChainlingType.Frozen;
            gilarime.secondaryType = ChainlingType.None;
            gilarime.baseMaxHP = 120;
            gilarime.baseMeleeAtk = 50;
            gilarime.baseMeleeDef = 65;
            gilarime.baseRangedAtk = 60;
            gilarime.baseRangedDef = 60;
            gilarime.baseSpeed = 45;
            gilarime.maxEvolutionStages = 3;
            gilarime.isStarter = true;
            gilarime.combatRoleOverride = SpeciesCombatRole.RangedGuard;
            gilarime.designNotes = "Starter. Exclusive moves: Gecko Guard (once per battle). Spec 06 v1.2.";
            gilarime.name = "Species_Gilarime";
            AssetDatabase.CreateAsset(gilarime, $"{ChainlingSpeciesFolder}/Species_Gilarime.asset");
            _logOutput += "Created: Species_Gilarime.asset\n";

            // Kitsauto ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Bot type
            var kitsauto = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            kitsauto.speciesId = "kitsauto";
            kitsauto.displayName = "Kitsauto";
            kitsauto.description = "This fox draws its blade before its eyes have opened. It darts through brush so quickly the stalks fall only after it has gone.";
            kitsauto.primaryType = ChainlingType.Bot;
            kitsauto.secondaryType = ChainlingType.None;
            kitsauto.baseMaxHP = 95;
            kitsauto.baseMeleeAtk = 70;
            kitsauto.baseMeleeDef = 45;
            kitsauto.baseRangedAtk = 55;
            kitsauto.baseRangedDef = 45;
            kitsauto.baseSpeed = 75;
            kitsauto.maxEvolutionStages = 3;
            kitsauto.isStarter = true;
            kitsauto.combatRoleOverride = SpeciesCombatRole.Speed;
            kitsauto.designNotes = "Starter. Exclusive moves: Fox Feint (once per battle). Spec 06 v1.2.";
            kitsauto.name = "Species_Kitsauto";
            AssetDatabase.CreateAsset(kitsauto, $"{ChainlingSpeciesFolder}/Species_Kitsauto.asset");
            _logOutput += "Created: ChainlingSpecies/Species_Kitsauto.asset\n";

            // Add to Species Database if it exists
            var dbPath = $"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(dbPath);
            if (db != null)
            {
                db.species.Clear();
                db.species.Add(validape);
                db.species.Add(gilarime);
                db.species.Add(kitsauto);
                EditorUtility.SetDirty(db);
                _logOutput += "\nAdded all starters to SpeciesDatabase.asset\n";
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _logOutput += "\nStarter species generated. Assign learnable/exclusive Stamped moves in Inspector.\n";
        }

        private void GenerateStarterStampedMovesAndAssign()
        {
            EnsureOrganizedFolders();
            _logOutput = "Generating Starter Stamped moves (4 per starter)...\n\n";

            var inscDbPath = $"{MovesDatabaseFolder}/MoveDatabase.asset";
            var inscDb = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>(inscDbPath);
            if (inscDb == null)
            {
                inscDb = ScriptableObject.CreateInstance<MoveDatabaseSO>();
                AssetDatabase.CreateAsset(inscDb, inscDbPath);
                _logOutput += "Created: MoveDatabase.asset\n";
            }

            // Validape (Node)
            var hammerSync = CreateOrUpdateMove(inscDb, "Move_HammerSync", def =>
            {
                def.moveId = "hammer_sync";
                def.displayName = "Hammer Sync";
                def.description = "110 power. +10% damage per buffed ally (cap +30%).";
                def.category = MoveCategory.DamagingMelee;
                def.baseType = ChainlingType.Node;
                def.rarity = RarityTier.Rare;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.Contact;
                def.power = 110;
                def.apCost = 2;
                def.accuracy = 95;
                def.hitCountMin = 1;
                def.hitCountMax = 1;
                def.rechargeTurns = 0;
                def.designNotes = "Validape exclusive (Spec 06 v1.2).";
            });

            var packRally = CreateOrUpdateMove(inscDb, "Move_PackRally", def =>
            {
                def.moveId = "pack_rally";
                def.displayName = "Pack Rally";
                def.description = "All allies +1 Atk and +1 Speed.";
                def.category = MoveCategory.Support;
                def.baseType = ChainlingType.Node;
                def.rarity = RarityTier.Uncommon;
                def.targeting = MoveTargeting.AllAllies;
                def.contactType = ContactType.None;
                def.power = 0;
                def.apCost = 3;
                def.accuracy = 0;
                def.hitCountMin = 1;
                def.hitCountMax = 1;
                def.rechargeTurns = 0;
                def.stageChanges.Clear();
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.MeleeAtk, stages = 1, chance = 1f, targetsSelf = false });
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.Speed, stages = 1, chance = 1f, targetsSelf = false });
                def.designNotes = "Validape exclusive (Spec 06 v1.2).";
            });

            var baselineStriker = CreateOrUpdateMove(inscDb, "Move_BaselineStriker", def =>
            {
                def.moveId = "baseline_striker";
                def.displayName = "Baseline Striker";
                def.description = "Reliable typeless damage.";
                def.category = MoveCategory.DamagingMelee;
                def.baseType = ChainlingType.None;
                def.rarity = RarityTier.Common;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.Contact;
                def.power = 75;
                def.apCost = 2;
                def.accuracy = 95;
                def.hitCountMin = 1;
                def.hitCountMax = 1;
                def.rechargeTurns = 0;
            });

            var buffAlly = CreateOrUpdateMove(inscDb, "Move_BuffAlly", def =>
            {
                def.moveId = "buff_ally";
                def.displayName = "Buff Ally";
                def.description = "Target ally +1 Atk and +1 Def.";
                def.category = MoveCategory.Support;
                def.baseType = ChainlingType.None;
                def.rarity = RarityTier.Common;
                def.targeting = MoveTargeting.SingleAlly;
                def.contactType = ContactType.None;
                def.power = 0;
                def.apCost = 2;
                def.accuracy = 0;
                def.stageChanges.Clear();
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.MeleeAtk, stages = 1, chance = 1f, targetsSelf = false });
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.MeleeDef, stages = 1, chance = 1f, targetsSelf = false });
            });

            // Gilarime (Frozen)
            var cryoRay = CreateOrUpdateMove(inscDb, "Move_CryoRay", def =>
            {
                def.moveId = "cryo_ray";
                def.displayName = "Cryo Ray";
                def.description = "110 power. 25% chance to apply -2 Speed.";
                def.category = MoveCategory.DamagingRanged;
                def.baseType = ChainlingType.Frozen;
                def.rarity = RarityTier.Rare;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.Projectile;
                def.power = 110;
                def.apCost = 2;
                def.accuracy = 95;
                def.stageChanges.Clear();
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.Speed, stages = -2, chance = 0.25f, targetsSelf = false });
                def.designNotes = "Gilarime exclusive (Spec 06 v1.2).";
            });

            var geckoGuard = CreateOrUpdateMove(inscDb, "Move_GeckoGuard", def =>
            {
                def.moveId = "gecko_guard";
                def.displayName = "Gecko Guard";
                def.description = "Below 50% HP: +2 Def, +1 Speed (once per battle).";
                def.category = MoveCategory.Passive;
                def.baseType = ChainlingType.Frozen;
                def.rarity = RarityTier.Rare;
                def.targeting = MoveTargeting.Self;
                def.contactType = ContactType.None;
                def.power = 0;
                def.apCost = 0;
                def.accuracy = 0;
                def.rechargeTurns = 999;
                def.designNotes = "Gilarime exclusive (Spec 06 v1.2). Trigger not implemented.";
            });

            var coldSnap = CreateOrUpdateMove(inscDb, "Move_ColdSnap", def =>
            {
                def.moveId = "cold_snap";
                def.displayName = "Cold Snap";
                def.description = "Target -2 Speed.";
                def.category = MoveCategory.StatusUtility;
                def.baseType = ChainlingType.Frozen;
                def.rarity = RarityTier.Common;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.None;
                def.power = 0;
                def.apCost = 2;
                def.accuracy = 0;
                def.stageChanges.Clear();
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.Speed, stages = -2, chance = 1f, targetsSelf = false });
            });

            var frostShot = CreateOrUpdateMove(inscDb, "Move_FrostShot", def =>
            {
                def.moveId = "frost_shot";
                def.displayName = "Frost Shot";
                def.description = "100 power. 25% chance to apply -1 Speed.";
                def.category = MoveCategory.DamagingRanged;
                def.baseType = ChainlingType.Frozen;
                def.rarity = RarityTier.Common;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.Projectile;
                def.power = 100;
                def.apCost = 2;
                def.accuracy = 95;
                def.stageChanges.Clear();
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.Speed, stages = -1, chance = 0.25f, targetsSelf = false });
            });

            // Kitsauto (Bot)
            var bladeFlurry = CreateOrUpdateMove(inscDb, "Move_BladeFlurry", def =>
            {
                def.moveId = "blade_flurry";
                def.displayName = "Blade Flurry";
                def.description = "30 power x3 hits.";
                def.category = MoveCategory.DamagingMelee;
                def.baseType = ChainlingType.Bot;
                def.rarity = RarityTier.Rare;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.Contact;
                def.power = 30;
                def.apCost = 2;
                def.accuracy = 95;
                def.hitCountMin = 3;
                def.hitCountMax = 3;
                def.designNotes = "Kitsauto exclusive (Spec 06 v1.2).";
            });

            var foxFeint = CreateOrUpdateMove(inscDb, "Move_FoxFeint", def =>
            {
                def.moveId = "fox_feint";
                def.displayName = "Fox Feint";
                def.description = "Dodge + counter (once per battle).";
                def.category = MoveCategory.StatusUtility;
                def.baseType = ChainlingType.Bot;
                def.rarity = RarityTier.Rare;
                def.targeting = MoveTargeting.Self;
                def.contactType = ContactType.None;
                def.power = 0;
                def.apCost = 1;
                def.accuracy = 0;
                def.rechargeTurns = 999;
                def.tags.Clear();
                def.tags.Add("TRIG");
                def.designNotes = "Kitsauto exclusive (Spec 06 v1.2). Trigger not implemented.";
            });

            var debuffScript = CreateOrUpdateMove(inscDb, "Move_DebuffScript", def =>
            {
                def.moveId = "debuff_script";
                def.displayName = "Debuff Script";
                def.description = "Target -1 Atk and -1 Def.";
                def.category = MoveCategory.StatusUtility;
                def.baseType = ChainlingType.Bot;
                def.rarity = RarityTier.Common;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.None;
                def.power = 0;
                def.apCost = 2;
                def.accuracy = 0;
                def.stageChanges.Clear();
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.MeleeAtk, stages = -1, chance = 1f, targetsSelf = false });
                def.stageChanges.Add(new MoveStageChangeDef { stat = CoreStat.MeleeDef, stages = -1, chance = 1f, targetsSelf = false });
            });

            var baselineJab = CreateOrUpdateMove(inscDb, "Move_BaselineJab", def =>
            {
                def.moveId = "baseline_jab";
                def.displayName = "Baseline Jab";
                def.description = "40 power. Fast chip damage.";
                def.category = MoveCategory.DamagingMelee;
                def.baseType = ChainlingType.None;
                def.rarity = RarityTier.Common;
                def.targeting = MoveTargeting.SingleEnemy;
                def.contactType = ContactType.Contact;
                def.power = 40;
                def.apCost = 1;
                def.accuracy = 95;
            });

            var baselineShot = FindMoveById(BuildMoveLookup(inscDb), "baseline_shot");

            // Ensure starter species exist
            var validape = LoadOrCreateStarterSpecies("Species_Validape", GenerateStarterSpecies);
            var gilarime = LoadOrCreateStarterSpecies("Species_Gilarime", GenerateStarterSpecies);
            var kitsauto = LoadOrCreateStarterSpecies("Species_Kitsauto", GenerateStarterSpecies);

            AssignStarterMoves(validape,
                new List<MoveDefinitionSO> { hammerSync, packRally, baselineStriker, buffAlly },
                new List<MoveDefinitionSO> { hammerSync, packRally },
                new List<MoveDefinitionSO> { baselineStriker });
            AssignStarterMoves(gilarime,
                new List<MoveDefinitionSO> { cryoRay, geckoGuard, coldSnap, frostShot, baselineShot },
                new List<MoveDefinitionSO> { cryoRay, geckoGuard },
                new List<MoveDefinitionSO> { baselineShot });
            AssignStarterMoves(kitsauto,
                new List<MoveDefinitionSO> { bladeFlurry, foxFeint, debuffScript, baselineJab },
                new List<MoveDefinitionSO> { bladeFlurry, foxFeint },
                new List<MoveDefinitionSO> { baselineJab });

            // Add to Species Database if it exists or create it
            var speciesDbPath = $"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset";
            var speciesDb = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(speciesDbPath);
            if (speciesDb == null)
            {
                speciesDb = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
                AssetDatabase.CreateAsset(speciesDb, speciesDbPath);
                _logOutput += "Created: SpeciesDatabase.asset\n";
            }

            AddSpeciesIfMissing(speciesDb, validape);
            AddSpeciesIfMissing(speciesDb, gilarime);
            AddSpeciesIfMissing(speciesDb, kitsauto);

            EditorUtility.SetDirty(speciesDb);
            EditorUtility.SetDirty(inscDb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _logOutput += "\nStarter Stamped moves created and assigned (4 each).\n";
        }

        private MoveDefinitionSO CreateOrUpdateMove(
            MoveDatabaseSO db,
            string assetName,
            Action<MoveDefinitionSO> configure)
        {
            string path = $"{MovesFolder}/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<MoveDefinitionSO>();
                def.name = assetName;
                AssetDatabase.CreateAsset(def, path);
                _logOutput += $"Created: {assetName}.asset\n";
            }

            def.statusEffects.Clear();
            def.stageChanges.Clear();
            def.tags.Clear();
            def.moveId = "";
            def.displayName = "";
            def.description = "";
            def.category = MoveCategory.StatusUtility;
            def.baseType = ChainlingType.None;
            def.rarity = RarityTier.Common;
            def.targeting = MoveTargeting.SingleEnemy;
            def.contactType = ContactType.None;
            def.power = 0;
            def.apCost = 0;
            def.accuracy = 0;
            def.hitCountMin = 1;
            def.hitCountMax = 1;
            def.rechargeTurns = 0;
            def.designNotes = "";
            configure(def);
            EditorUtility.SetDirty(def);

            if (!db.moves.Contains(def))
                db.moves.Add(def);

            return def;
        }

        private SpeciesDefinitionSO LoadOrCreateStarterSpecies(string assetName, Action ensureExists)
        {
            string path = $"{ChainlingSpeciesFolder}/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<SpeciesDefinitionSO>(path);
            if (def == null)
            {
                ensureExists?.Invoke();
                def = AssetDatabase.LoadAssetAtPath<SpeciesDefinitionSO>(path);
            }
            return def;
        }

        private void AssignStarterMoves(
            SpeciesDefinitionSO species,
            List<MoveDefinitionSO> learnable,
            List<MoveDefinitionSO> exclusive,
            List<MoveDefinitionSO> initial)
        {
            if (species == null) return;

            species.learnableMoves.Clear();
            species.learnLevels.Clear();
            for (int i = 0; i < learnable.Count; i++)
            {
                if (learnable[i] == null) continue;
                species.learnableMoves.Add(learnable[i]);
                species.learnLevels.Add(1);
            }

            species.exclusiveMoves.Clear();
            for (int i = 0; i < exclusive.Count; i++)
            {
                if (exclusive[i] != null)
                    species.exclusiveMoves.Add(exclusive[i]);
            }

            species.initialMoves.Clear();
            for (int i = 0; i < initial.Count; i++)
            {
                if (initial[i] != null)
                    species.initialMoves.Add(initial[i]);
            }

            EditorUtility.SetDirty(species);
        }

        private void AddSpeciesIfMissing(SpeciesDatabaseSO db, SpeciesDefinitionSO species)
        {
            if (db == null || species == null) return;
            if (!db.species.Contains(species))
                db.species.Add(species);
        }

        // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Species Move Pool Import (Spec 06) ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

        private static readonly HashSet<string> StarterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "validape", "gilarime", "kitsauto" };

        private struct SpeciesMovePoolEntry
        {
            public string speciesId;
            public List<string> learnableIds;
            public List<string> exclusiveIds;
            public string flagshipId;
        }

        private void ImportSpeciesMovePools()
        {
            EnsureOrganizedFolders();
            _logOutput = "Importing Species Move Pools (Spec 06)...\n\n";

            var speciesDbPath = $"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset";
            var speciesDb = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(speciesDbPath);
            if (speciesDb == null)
            {
                _logOutput += "ERROR: SpeciesDatabase.asset not found. Run 'Generate Starter Species' first.\n";
                return;
            }

            var moveDbPath = $"{MovesDatabaseFolder}/MoveDatabase.asset";
            var moveDb = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>(moveDbPath);
            if (moveDb == null)
            {
                _logOutput += "ERROR: MoveDatabase.asset not found. Run 'Generate Starter Stamped moves' first.\n";
                return;
            }

            if (!File.Exists(_speciesMovePoolsSpecPath))
            {
                _logOutput += $"ERROR: Spec file not found at: {_speciesMovePoolsSpecPath}\n";
                return;
            }

            string specText = File.ReadAllText(_speciesMovePoolsSpecPath);
            var entries = ParseSpeciesMovePoolsSpec(specText);
            _logOutput += $"Parsed {entries.Count} species entries from spec.\n\n";

            var moveLookup = BuildMoveLookup(moveDb);

            int assignedCount = 0;
            foreach (var entry in entries)
            {
                var speciesDef = speciesDb.GetById(entry.speciesId);
                if (speciesDef == null)
                {
                    _logOutput += $"WARNING: Species '{entry.speciesId}' not found in database ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â skipped.\n";
                    continue;
                }

                // Resolve learnable moves
                var learnableMoves = new List<MoveDefinitionSO>();
                foreach (var id in entry.learnableIds)
                {
                    var moveDef = FindMoveById(moveLookup, id);
                    if (moveDef != null)
                        learnableMoves.Add(moveDef);
                    else
                        _logOutput += $"  WARNING: Move '{id}' not found for {entry.speciesId} learnable pool ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â skipped.\n";
                }

                // Resolve exclusive moves
                var exclusiveMoves = new List<MoveDefinitionSO>();
                if (StarterIds.Contains(entry.speciesId))
                {
                    foreach (var id in entry.exclusiveIds)
                    {
                        var moveDef = FindMoveById(moveLookup, id);
                        if (moveDef != null)
                            exclusiveMoves.Add(moveDef);
                        else
                            _logOutput += $"  WARNING: Exclusive move '{id}' not found for {entry.speciesId} ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â skipped.\n";
                    }
                }
                else if (entry.exclusiveIds.Count > 0)
                {
                    _logOutput += $"  ERROR: Non-starter '{entry.speciesId}' has {entry.exclusiveIds.Count} exclusives ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â stripped per spec rules.\n";
                }

                // Resolve flagship
                MoveDefinitionSO flagshipDef = null;
                if (!string.IsNullOrEmpty(entry.flagshipId))
                {
                    flagshipDef = FindMoveById(moveLookup, entry.flagshipId);
                    if (flagshipDef == null)
                        _logOutput += $"  ERROR: Flagship move '{entry.flagshipId}' not found for {entry.speciesId}.\n";
                }
                else
                {
                    _logOutput += $"  ERROR: No flagship move resolved for {entry.speciesId}.\n";
                }

                if (flagshipDef != null && !learnableMoves.Contains(flagshipDef) && !exclusiveMoves.Contains(flagshipDef))
                    _logOutput += $"  ERROR: Flagship '{entry.flagshipId}' not in learnable or exclusive pool for {entry.speciesId}.\n";

                // Assign to SO
                speciesDef.learnableMoves = learnableMoves;
                speciesDef.learnLevels = new List<int>();
                for (int i = 0; i < learnableMoves.Count; i++)
                    speciesDef.learnLevels.Add(1);

                speciesDef.exclusiveMoves = exclusiveMoves;
                speciesDef.flagshipMove = flagshipDef;
                EditorUtility.SetDirty(speciesDef);

                assignedCount++;
                _logOutput += $"  {entry.speciesId}: {learnableMoves.Count} learnable, {exclusiveMoves.Count} exclusive, flagship={flagshipDef?.moveId ?? "NONE"}\n";
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _logOutput += $"\nDone. Assigned move pools to {assignedCount}/{entries.Count} species.\n";
        }

        private List<SpeciesMovePoolEntry> ParseSpeciesMovePoolsSpec(string text)
        {
            var results = new List<SpeciesMovePoolEntry>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            var speciesHeaderRx = new Regex(@"^\d+\.\d+\s+(\w+)\s*\(", RegexOptions.IgnoreCase);
            var idLineRx = new Regex(@"^ID:\s*(\w+)", RegexOptions.IgnoreCase);
            var numberedMoveRx = new Regex(@"^\d+\.\s+(.+?)(?:\s+\[.*\])?\s*(?:ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â|-)");
            var sectionLabelRx = new Regex(@"^(EXCLUSIVE|NODE TYPE|FROZEN TYPE|BOT TYPE|TYPELESS)\b", RegexOptions.IgnoreCase);
            var learnablePoolHeaderRx = new Regex(@"^LEARNABLE POOL\b", RegexOptions.IgnoreCase);

            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i].Trim();

                var headerMatch = speciesHeaderRx.Match(line);
                if (!headerMatch.Success) { i++; continue; }

                string speciesName = headerMatch.Groups[1].Value.ToLowerInvariant();

                // Search for the ID line within this block
                string speciesId = speciesName;
                int blockStart = i;
                for (int j = i + 1; j < lines.Length && j < i + 30; j++)
                {
                    var idMatch = idLineRx.Match(lines[j].Trim());
                    if (idMatch.Success)
                    {
                        speciesId = idMatch.Groups[1].Value.ToLowerInvariant();
                        break;
                    }
                }

                // Find the LEARNABLE POOL section
                var entry = new SpeciesMovePoolEntry
                {
                    speciesId = speciesId,
                    learnableIds = new List<string>(),
                    exclusiveIds = new List<string>(),
                    flagshipId = null
                };

                bool inLearnablePool = false;
                bool inExclusiveSection = false;
                i++;

                while (i < lines.Length)
                {
                    line = lines[i].Trim();

                    // Stop at next species header or end-of-section
                    if (speciesHeaderRx.IsMatch(line))
                        break;
                    if (line.StartsWith("====="))
                        break;

                    if (learnablePoolHeaderRx.IsMatch(line))
                    {
                        inLearnablePool = true;
                        inExclusiveSection = false;
                        i++;
                        continue;
                    }

                    if (inLearnablePool)
                    {
                        // Detect subsection labels
                        var sectionMatch = sectionLabelRx.Match(line);
                        if (sectionMatch.Success)
                        {
                            inExclusiveSection = sectionMatch.Groups[1].Value
                                .Equals("EXCLUSIVE", StringComparison.OrdinalIgnoreCase);
                            i++;
                            continue;
                        }

                        // Detect end of learnable pool
                        if (line.StartsWith("POOL RESTRICTIONS", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("EQUIPPABLE vs", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("PLAYSTYLE", StringComparison.OrdinalIgnoreCase))
                        {
                            inLearnablePool = false;
                            i++;
                            continue;
                        }

                        var moveMatch = numberedMoveRx.Match(line);
                        if (moveMatch.Success)
                        {
                            string moveName = moveMatch.Groups[1].Value.Trim();
                            string moveId = DeriveStampedMoveId(moveName);
                            entry.learnableIds.Add(moveId);

                            if (inExclusiveSection)
                            {
                                entry.exclusiveIds.Add(moveId);
                                if (entry.flagshipId == null)
                                    entry.flagshipId = moveId;
                            }
                        }
                    }

                    i++;
                }

                results.Add(entry);
            }

            return results;
        }

        private Dictionary<string, MoveDefinitionSO> BuildMoveLookup(MoveDatabaseSO db)
        {
            var lookup = new Dictionary<string, MoveDefinitionSO>(StringComparer.OrdinalIgnoreCase);
            foreach (var move in db.moves)
            {
                if (move == null) continue;
                if (!string.IsNullOrEmpty(move.moveId))
                    lookup[move.moveId] = move;
                string derivedId = DeriveStampedMoveId(move.displayName);
                if (!string.IsNullOrEmpty(derivedId) && !lookup.ContainsKey(derivedId))
                    lookup[derivedId] = move;
            }
            return lookup;
        }

        private static MoveDefinitionSO FindMoveById(Dictionary<string, MoveDefinitionSO> lookup, string moveId)
        {
            if (string.IsNullOrEmpty(moveId)) return null;
            lookup.TryGetValue(moveId, out var def);
            return def;
        }

        private static string DeriveStampedMoveId(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            return Regex.Replace(displayName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        }

        // ———— CSV Parsing Infrastructure ————

        public static string[] ParseCsvLinePublic(string line) => ParseCSVLine(line);

        public static bool TryParseResolveConditionPublic(string raw, out MoveResolveCondition condition)
            => TryParseResolveCondition(raw, out condition);

        private static string[] ParseCSVLine(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (i <= line.Length)
            {
                if (i == line.Length) { fields.Add(""); break; }

                if (line[i] == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i += 2; }
                            else { i++; break; }
                        }
                        else { sb.Append(line[i]); i++; }
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    fields.Add(line.Substring(start, i - start));
                    if (i < line.Length) i++;
                }
            }
            return fields.ToArray();
        }

        private static string GetField(string[] fields, int index)
        {
            if (fields == null || index < 0 || index >= fields.Length) return "";
            return fields[index].Trim();
        }

        private static int ParseInt(string value, int fallback = 0)
        {
            return int.TryParse(value, out var v) ? v : fallback;
        }

        private static float ParseFloat(string value, float fallback = 0f)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        private static bool ParseBool(string value, bool fallback = false)
        {
            return bool.TryParse(value, out var v) ? v : fallback;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse<T>(value, true, out var v) ? v : fallback;
        }

        private static string[] SplitPipe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new string[0];
            return value.Split('|');
        }

        private string ResolveCsvPath(string filename)
        {
            return Path.Combine(_dataFolder, filename).Replace('\\', '/');
        }

        /// <summary>Resolves path for spec-level CSVs (e.g. COMBAT_Interaction_Matrix) in Docs/Specs/Combat/.</summary>
        private string ResolveSpecCsvPath(string filename)
        {
            string specFolder = Path.GetDirectoryName(_dataFolder);
            return Path.Combine(specFolder ?? _dataFolder, filename).Replace('\\', '/');
        }

        private string[] ReadCsvLines(string filename)
        {
            string path = ResolveCsvPath(filename);
            if (!File.Exists(path))
            {
                _logOutput += $"ERROR: {filename} not found at: {path}\n";
                return null;
            }
            return File.ReadAllLines(path);
        }

        private string[] ReadSpecCsvLines(string filename)
        {
            string path = ResolveSpecCsvPath(filename);
            if (!File.Exists(path))
            {
                _logOutput += $"ERROR: {filename} not found at: {path}\n";
                return null;
            }
            return File.ReadAllLines(path);
        }

        // ———— Import Statuses from CSV ————

        private string[] ReadFirstExistingSpecCsvLines(out string filename, params string[] candidates)
        {
            filename = string.Empty;
            if (candidates == null || candidates.Length == 0)
                return null;

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                string path = ResolveSpecCsvPath(candidate);
                if (!File.Exists(path))
                    continue;

                filename = candidate;
                return File.ReadAllLines(path);
            }

            _logOutput += $"ERROR: none of the expected spec CSVs were found: {string.Join(", ", candidates)}\n";
            return null;
        }

        private void ImportStatusesFromCSV()
        {
            _logOutput = "Importing Statuses from CSV...\n\n";
            var lines = ReadCsvLines("Statuses.csv");
            if (lines == null) return;

            EnsureOrganizedFolders();

            string catalogPath = $"{StatusDefCatalogFolder}/StatusCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<StatusCatalogSO>(catalogPath);
            if (catalog == null && !_dryRun)
            {
                catalog = ScriptableObject.CreateInstance<StatusCatalogSO>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                _logOutput += "Created: StatusCatalog.asset\n";
            }
            if (catalog != null) catalog.definitions.Clear();

            int imported = 0;
            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var f = ParseCSVLine(line);
                if (f.Length < 14)
                {
                    _logOutput += $"  WARN: Row {row + 1} has {f.Length} fields (need 14), skipped\n";
                    continue;
                }

                string statusIdStr = GetField(f, 0);
                if (!Enum.TryParse<StatusId>(statusIdStr, out var statusId))
                {
                    _logOutput += $"  WARN: Unknown StatusId '{statusIdStr}' on row {row + 1}, skipped\n";
                    continue;
                }

                string displayName       = GetField(f, 1);
                var origin               = ParseEnum(GetField(f, 2), StatusOrigin.General);
                var statusClass          = ParseEnum(GetField(f, 3), StatusClass.Duration);
                int duration             = ParseInt(GetField(f, 4));
                int counterThreshold     = ParseInt(GetField(f, 5));
                string effectSummary     = GetField(f, 6);
                string stageEffectsRaw   = GetField(f, 7);
                bool isParameterized     = ParseBool(GetField(f, 8));
                bool isDebuff            = ParseBool(GetField(f, 9));
                bool isBuff              = ParseBool(GetField(f, 10));
                bool preventsAction      = ParseBool(GetField(f, 11));
                float eotDmgPct          = ParseFloat(GetField(f, 12));
                int eotAPChange          = ParseInt(GetField(f, 13));

                string assetName = $"StatusDef_{statusId}";
                string assetPath = $"{StatusDefFolder}/{assetName}.asset";

                if (_dryRun)
                {
                    _logOutput += $"  [DRY] {assetName}: {displayName} ({origin}/{statusClass})\n";
                    imported++;
                    continue;
                }

                var def = AssetDatabase.LoadAssetAtPath<StatusDefinitionSO>(assetPath);
                bool created = def == null;
                if (created)
                {
                    def = ScriptableObject.CreateInstance<StatusDefinitionSO>();
                    def.name = assetName;
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.statusId             = statusId;
                def.displayName          = displayName;
                def.origin               = origin;
                def.statusClass          = statusClass;
                def.defaultDuration      = duration;
                def.counterThreshold     = counterThreshold;
                def.effectSummary        = effectSummary;
                def.isParameterized      = isParameterized;
                def.isDebuff             = isDebuff;
                def.isBuff               = isBuff;
                def.preventsAction       = preventsAction;
                def.hasEndOfTurnDamage   = eotDmgPct > 0f;
                def.endOfTurnDamagePercent = eotDmgPct;
                def.hasEndOfTurnAP       = eotAPChange != 0;
                def.endOfTurnAPChange    = eotAPChange;
                def.stageEffects         = ParseStatusStageEffects(stageEffectsRaw);

                EditorUtility.SetDirty(def);
                if (catalog != null) catalog.definitions.Add(def);
                imported++;
                _logOutput += $"  {(created ? "Created" : "Updated")}: {assetName}\n";
            }

            if (catalog != null) EditorUtility.SetDirty(catalog);
            if (!_dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            _logOutput += $"\n{(_dryRun ? "[DRY RUN] " : "")}Imported {imported} statuses.\n";
        }

        private static StatusStageEffect[] ParseStatusStageEffects(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new StatusStageEffect[0];
            var entries = raw.Split('|');
            var results = new List<StatusStageEffect>();
            foreach (var entry in entries)
            {
                string trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                string[] parts = trimmed.Split(':');
                if (parts.Length < 2) continue;
                if (!Enum.TryParse<CoreStat>(parts[0].Trim(), true, out var stat)) continue;
                if (!int.TryParse(parts[1].Trim(), out var stages)) continue;
                bool applyOnce = parts.Length < 3 || !bool.TryParse(parts[2].Trim(), out var ao) || ao;
                results.Add(new StatusStageEffect { stat = stat, stages = stages, applyOnce = applyOnce });
            }
            return results.ToArray();
        }

        // ———— Export Moves / Modifiers to CSV ————

        public void ExportMovesToCSV()
        {
            string csvPath = ResolveCsvPath("Moves.csv");
            _logOutput = CombatCsvRoundTrip.ExportMovesToCSV(
                csvPath,
                MovesFolder,
                $"{MovesDatabaseFolder}/MoveDatabase.asset") + "\n";
        }

        public void ExportModifiersToCSV()
        {
            string csvPath = ResolveCsvPath("Modifiers.csv");
            _logOutput = CombatCsvRoundTrip.ExportModifiersToCSV(
                csvPath,
                ModifiersFolder,
                $"{ModifiersCatalogFolder}/ModifierCatalog.asset") + "\n";
        }

        public static void RunExportMovesToCSV()
        {
            CombatCsvRoundTrip.ExportMovesToCSV();
        }

        public static void RunExportModifiersToCSV()
        {
            CombatCsvRoundTrip.ExportModifiersToCSV();
        }

        public static void RunImportMovesFromCSV()
        {
            GetWindow<Combat2DataImporter>().ImportMovesFromCSV();
        }

        public static void RunImportSpeciesPoolsFromCSV()
        {
            GetWindow<Combat2DataImporter>().ImportSpeciesPoolsFromCSV();
        }

        public static void RunImportStatusesFromCSV()
        {
            GetWindow<Combat2DataImporter>().ImportStatusesFromCSV();
        }

        public static void RunImportChemistryMatrixFromCSV()
        {
            GetWindow<Combat2DataImporter>().ImportChemistryMatrixFromCSV();
        }

        public static void RunResetDefaultResolverConfig()
        {
            const string assetPath = "Assets/Data/Combat/Config/CombatResolverConfig_Default.asset";
            CombatResolverConfigSO config = AssetDatabase.LoadAssetAtPath<CombatResolverConfigSO>(assetPath);
            if (config == null)
                throw new InvalidOperationException($"Could not find resolver config asset at {assetPath}.");

            Undo.RecordObject(config, "Reset Combat Resolver Config Defaults");
            config.ResetToDefaults();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"Reset resolver config defaults at {assetPath}. Hash={config.ComputeConfigHash()}");
        }

        public static void RunImportModifiersFromCSV()
        {
            GetWindow<Combat2DataImporter>().ImportModifiersFromCSV();
        }

        // ———— Import Moves from CSV ————

        public void ImportMovesFromCSV()
        {
            _logOutput = "Importing Moves from CSV (header-based)...\n\n";
            var lines = ReadCsvLines("Moves.csv");
            if (lines == null) return;

            int headerIndex = CombatMovesCsvSchema.FindDataHeaderLineIndex(lines);
            if (headerIndex < 0)
            {
                _logOutput += "ERROR: No Moves.csv header row starting with MoveId was found.\n";
                return;
            }

            string[] headerFields = ParseCSVLine(lines[headerIndex]);
            if (!CombatMovesCsvSchema.TryBuildColumnMap(
                    headerFields,
                    out CombatCsvColumnMap columns,
                    out List<string> missingRequired,
                    out List<string> unknownColumns))
            {
                _logOutput += "ERROR: Moves.csv is missing required columns: " +
                              string.Join(", ", missingRequired) + "\n";
                return;
            }

            for (int i = 0; i < unknownColumns.Count; i++)
                _logOutput += $"  WARN: Unknown header column '{unknownColumns[i]}' (ignored)\n";

            var validationIssues = new List<CombatMovesCsvIssue>();
            CombatMovesCsvValidation.ValidateFile(ResolveCsvPath("Moves.csv"), validationIssues);
            int validationErrors = 0;
            for (int i = 0; i < validationIssues.Count; i++)
            {
                if (validationIssues[i].Severity == CombatMovesCsvIssueSeverity.Error)
                    validationErrors++;
            }

            EnsureOrganizedFolders();

            string dbPath = $"{MovesDatabaseFolder}/MoveDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>(dbPath);
            if (db == null && !_dryRun)
            {
                db = ScriptableObject.CreateInstance<MoveDatabaseSO>();
                AssetDatabase.CreateAsset(db, dbPath);
                _logOutput += "Created: MoveDatabase.asset\n";
            }
            if (db != null) db.moves.Clear();

            int imported = 0;
            int skipped = 0;
            for (int row = headerIndex + 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                string[] f = ParseCSVLine(line);
                string moveId = columns.Get(f, "MoveId");
                if (string.IsNullOrEmpty(moveId))
                {
                    _logOutput += $"  WARN: Empty MoveId on row {row + 1}, skipped\n";
                    skipped++;
                    continue;
                }

                string displayName = columns.Get(f, "DisplayName");
                var category = ParseEnum(columns.Get(f, "Category"), MoveCategory.StatusUtility);
                var baseType = ParseEnum(columns.Get(f, "BaseType"), ChainlingType.None);
                var rarity = ParseEnum(columns.Get(f, "Rarity"), RarityTier.Common);
                var targeting = ParseEnum(columns.Get(f, "Targeting"), MoveTargeting.SingleEnemy);
                var contactType = ParseEnum(columns.Get(f, "ContactType"), ContactType.None);
                int power = ParseInt(columns.Get(f, "Power"));
                int apCost = ParseInt(columns.Get(f, "APCost"));
                int accuracy = ParseInt(columns.Get(f, "Accuracy"));
                int hitMin = ParseInt(columns.Get(f, "HitMin"), 1);
                int hitMax = ParseInt(columns.Get(f, "HitMax"), 1);
                int rechargeTurns = ParseInt(columns.Get(f, "RechargeTurns"));
                string statusFxRaw = columns.Get(f, "StatusEffects");
                string stageChgRaw = columns.Get(f, "StageChanges");
                string tagsRaw = columns.Get(f, "Tags");
                var triggerCond = ParseEnum(columns.Get(f, "TriggerCondition"), PassiveTriggerCondition.None);
                float hpThresh = ParseFloat(columns.Get(f, "HPThreshold"));
                bool triggerOnce = ParseBool(columns.Get(f, "TriggerOnce"));
                int healPctMaxHP = ParseInt(columns.Get(f, "HealPercentMaxHP"));
                string description = columns.Get(f, "Description");
                var selectionPolicy = ParseEnum(columns.Get(f, "SelectionPolicy"), MoveSelectionPolicy.EffectOnly);
                var activeConditionType = ParseEnum(columns.Get(f, "ActiveConditionType"), ActiveConditionType.None);
                float activeConditionThreshold = ParseFloat(columns.Get(f, "ActiveConditionThreshold"));
                string utilityEffectsRaw = columns.Get(f, "UtilityEffects");
                float hpCostPercent = ParseFloat(columns.Get(f, "HpCostPercent"));
                MoveResolveProfile resolveProfile = ParseMoveResolveProfile(columns, f);
                string designNotes = columns.Get(f, "DesignNotes");

                string assetName = $"Move_{moveId}";
                string assetPath = $"{MovesFolder}/{assetName}.asset";

                if (_dryRun)
                {
                    _logOutput += $"  [DRY] {assetName}: {displayName} ({category}/{baseType})\n";
                    imported++;
                    continue;
                }

                var def = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(assetPath);
                bool created = def == null;
                if (created)
                {
                    def = ScriptableObject.CreateInstance<MoveDefinitionSO>();
                    def.name = assetName;
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.moveId = moveId;
                def.displayName = displayName;
                def.description = description;
                def.category = category;
                def.baseType = baseType;
                def.rarity = rarity;
                def.targeting = targeting;
                def.contactType = contactType;
                def.power = power;
                def.apCost = apCost;
                def.accuracy = accuracy;
                def.hitCountMin = hitMin;
                def.hitCountMax = hitMax;
                def.rechargeTurns = rechargeTurns;
                def.triggerCondition = triggerCond;
                def.hpThresholdPercent = hpThresh;
                def.triggerOncePerBattle = triggerOnce;
                def.healPercentMaxHP = healPctMaxHP;
                def.statusEffects = ParseMoveStatusEffects(statusFxRaw);
                def.stageChanges = ParseMoveStageChanges(stageChgRaw);
                def.tags = new List<string>(SplitPipe(tagsRaw));
                def.selectionPolicy = selectionPolicy;
                def.activeConditionType = activeConditionType;
                def.activeConditionThresholdPercent = activeConditionThreshold;
                def.utilityEffects = ParseUtilityEffects(utilityEffectsRaw);
                def.hpCostPercent = hpCostPercent;
                def.resolveProfile = resolveProfile;
                if (columns.Has("DesignNotes"))
                    def.designNotes = designNotes;

                // wallPresentation (including wallSfxId) is asset-authored rather than CSV-authored.
                // Intentionally leave it untouched so a Moves.csv reimport preserves wall presentation IDs.

                EditorUtility.SetDirty(def);
                if (db != null && !db.moves.Contains(def)) db.moves.Add(def);
                imported++;
                _logOutput += $"  {(created ? "Created" : "Updated")}: {assetName}\n";
            }

            if (db != null) EditorUtility.SetDirty(db);
            if (!_dryRun) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }

            string reportBody = CombatMovesCsvValidation.FormatMarkdownReport(
                ResolveCsvPath("Moves.csv"),
                validationIssues);
            CombatMovesCsvValidation.WriteReport(CombatMovesCsvValidation.DefaultReportPath, reportBody);

            _logOutput += $"\n{(_dryRun ? "[DRY RUN] " : "")}Imported {imported} moves";
            if (skipped > 0)
                _logOutput += $", skipped {skipped}";
            _logOutput += $".\nValidation: {validationErrors} error(s), report → {CombatMovesCsvValidation.DefaultReportPath}\n";
        }

        private static List<MoveStatusEffectDef> ParseMoveStatusEffects(string raw)
        {
            var list = new List<MoveStatusEffectDef>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var entry in raw.Split('|'))
            {
                string[] parts = entry.Trim().Split(':');
                if (parts.Length < 3) continue;
                if (!Enum.TryParse<StatusId>(parts[0].Trim(), true, out var sid)) continue;
                int dur = ParseInt(parts[1].Trim());
                float chance = ParseFloat(parts[2].Trim(), 1f);
                bool hasOverride = parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]);
                var targetOvr = hasOverride ? ParseEnum(parts[3].Trim(), MoveTargeting.SingleEnemy) : MoveTargeting.SingleEnemy;
                list.Add(new MoveStatusEffectDef
                {
                    statusId = sid, duration = dur, chance = chance,
                    overrideTargeting = hasOverride, targetOverride = targetOvr,
                    parameterType = ChainlingType.None,
                });
            }
            return list;
        }

        private static List<MoveStageChangeDef> ParseMoveStageChanges(string raw)
        {
            var list = new List<MoveStageChangeDef>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var entry in raw.Split('|'))
            {
                string[] parts = entry.Trim().Split(':');
                if (parts.Length < 4) continue;
                if (!Enum.TryParse<CoreStat>(parts[0].Trim(), true, out var stat)) continue;
                int stages = ParseInt(parts[1].Trim());
                float chance = ParseFloat(parts[2].Trim(), 1f);
                bool self = ParseBool(parts[3].Trim());
                list.Add(new MoveStageChangeDef { stat = stat, stages = stages, chance = chance, targetsSelf = self });
            }
            return list;
        }

        private static List<UtilityEffectPayload> ParseUtilityEffects(string raw)
        {
            var list = new List<UtilityEffectPayload>();
            if (string.IsNullOrWhiteSpace(raw))
                return list;

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                string[] parts = entry.Split(':');
                if (parts.Length < 1)
                    continue;

                if (!Enum.TryParse(parts[0].Trim(), true, out UtilityEffectType effectType))
                    continue;

                string p1 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                string p2 = parts.Length > 2 ? parts[2].Trim() : string.Empty;
                string p3 = parts.Length > 3 ? parts[3].Trim() : string.Empty;
                string p4 = parts.Length > 4 ? parts[4].Trim() : string.Empty;

                var effect = new UtilityEffectPayload
                {
                    effectType = effectType,
                    stackKey = p4,
                    targetOverride = MoveTargeting.SingleEnemy,
                    chance = 1f,
                };

                switch (effectType)
                {
                    case UtilityEffectType.MitigationWindow:
                        effect.magnitude = ParsePercentOrFloat(p1);
                        effect.turnBudget = ParseInt(p2);
                        effect.hitBudget = ParseInt(p3);
                        break;

                    case UtilityEffectType.GainApOnDamageIfSpeedHigher:
                        effect.apGain = ParseInt(p1, 1);
                        effect.turnBudget = ParseInt(p2, 1);
                        effect.hitBudget = ParseInt(p3);
                        break;

                    case UtilityEffectType.FullAbsorbBarrier:
                        effect.turnBudget = ParseInt(p1, 3);
                        effect.hitBudget = ParseInt(p2, 3);
                        effect.stackKey = p3;
                        break;

                    case UtilityEffectType.DamageThresholdBarrier:
                        effect.magnitude = ParseInt(p1);
                        effect.turnBudget = ParseInt(p2, 1);
                        effect.hitBudget = ParseInt(p3);
                        effect.reflectPercent = ParsePercentOrFloat(p4);
                        effect.stackKey = parts.Length > 5 ? parts[5].Trim() : string.Empty;
                        break;

                    case UtilityEffectType.WallHitRetaliation:
                        effect.apGain = ParseInt(p1, 1);
                        effect.stackKey = p2;
                        break;

                    case UtilityEffectType.GainAP:
                    case UtilityEffectType.DrainAP:
                    case UtilityEffectType.StealAP:
                    case UtilityEffectType.TransferAP:
                    case UtilityEffectType.RefundAP:
                    case UtilityEffectType.NextMoveAPSurcharge:
                        effect.apGain = ParseInt(p1, 1);
                        effect.chance = string.IsNullOrWhiteSpace(p2) ? 1f : ParsePercentOrFloat(p2);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p3)
                            ? MoveTargeting.SingleEnemy
                            : ParseEnum(p3, MoveTargeting.SingleEnemy);
                        effect.stackKey = p4;
                        break;

                    case UtilityEffectType.TurnStartPendingStatus:
                        effect.statusId = ParseEnum(p1, StatusId.None);
                        effect.statusDuration = ParseInt(p2, 1);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p3)
                            ? MoveTargeting.SingleEnemy
                            : ParseEnum(p3, MoveTargeting.SingleEnemy);
                        effect.stackKey = p4;
                        break;

                    case UtilityEffectType.Cleanse:
                        effect.apGain = ParseInt(p1, 1);
                        effect.magnitude = effect.apGain;
                        effect.chance = string.IsNullOrWhiteSpace(p2) ? 1f : ParsePercentOrFloat(p2);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p3)
                            ? MoveTargeting.SingleAlly
                            : ParseEnum(p3, MoveTargeting.SingleAlly);
                        effect.stackKey = p4;
                        break;

                    case UtilityEffectType.BonusHealPercentIfSpeedHigher:
                        effect.magnitude = ParsePercentOrFloat(p1);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p2)
                            ? MoveTargeting.Self
                            : ParseEnum(p2, MoveTargeting.Self);
                        effect.stackKey = p3;
                        break;

                    case UtilityEffectType.IncomingDamageMitigation:
                        effect.magnitude = ParsePercentOrFloat(p1);
                        effect.contactFilter = ParseEnum(p2, UtilityContactFilter.Any);
                        effect.stackKey = p3;
                        break;

                    case UtilityEffectType.ChangeStage:
                        effect.stageStat = ParseEnum(p1, CombatStageStat.MeleeAtk);
                        effect.stageAmount = ParseInt(p2);
                        effect.chance = string.IsNullOrWhiteSpace(p3) ? 1f : ParsePercentOrFloat(p3);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p4)
                            ? MoveTargeting.Self
                            : ParseEnum(p4, MoveTargeting.Self);
                        effect.stageCap = parts.Length > 5 ? ParseInt(parts[5].Trim()) : 0;
                        effect.affectAllAllies = parts.Length > 6 && ParseBool(parts[6].Trim());
                        effect.stackKey = parts.Length > 7 ? parts[7].Trim() : string.Empty;
                        break;

                    case UtilityEffectType.RandomStageChange:
                        effect.stageAmount = ParseInt(p1);
                        effect.stageAmountMax = ParseInt(p2, effect.stageAmount);
                        effect.chance = string.IsNullOrWhiteSpace(p3) ? 1f : ParsePercentOrFloat(p3);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p4)
                            ? MoveTargeting.Self
                            : ParseEnum(p4, MoveTargeting.Self);
                        effect.stageCap = parts.Length > 5 ? ParseInt(parts[5].Trim()) : 0;
                        effect.stackKey = parts.Length > 6 ? parts[6].Trim() : string.Empty;
                        break;

                    case UtilityEffectType.ArmUtilityWindow:
                        effect.armedTrigger = ParseEnum(p1, ArmedWindowTrigger.None);
                        effect.turnBudget = ParseInt(p2, 1);
                        effect.hitBudget = ParseInt(p3, 1);
                        effect.apGain = ParseInt(p4);
                        effect.healPercentMaxHP = parts.Length > 5 ? ParsePercentOrFloat(parts[5].Trim()) : 0f;
                        effect.stageStat = parts.Length > 6
                            ? ParseEnum(parts[6].Trim(), CombatStageStat.MeleeAtk)
                            : CombatStageStat.MeleeAtk;
                        effect.stageAmount = parts.Length > 7 ? ParseInt(parts[7].Trim()) : 0;
                        effect.targetOverride = parts.Length > 8
                            ? ParseEnum(parts[8].Trim(), MoveTargeting.Self)
                            : MoveTargeting.Self;
                        effect.counterDamagePercentMaxHP = parts.Length > 9
                            ? ParsePercentOrFloat(parts[9].Trim())
                            : 0f;
                        effect.affectAllAllies = parts.Length > 10 && ParseBool(parts[10].Trim());
                        effect.stageCap = parts.Length > 11 ? ParseInt(parts[11].Trim()) : 0;
                        effect.stackKey = parts.Length > 12 ? parts[12].Trim() : string.Empty;
                        break;

                    case UtilityEffectType.BankAPUntilNextTurn:
                        effect.threshold = ParseInt(p1, 5);
                        effect.apGain = ParseInt(p2, 1);
                        effect.stackKey = p3;
                        effect.targetOverride = MoveTargeting.Self;
                        break;

                    case UtilityEffectType.GainApIfDistinctTargetsHitAtLeast:
                        effect.threshold = ParseInt(p1, 2);
                        effect.apGain = ParseInt(p2, 1);
                        effect.targetOverride = MoveTargeting.Self;
                        effect.stackKey = p3;
                        break;

                    case UtilityEffectType.FieldEntryStageChange:
                        effect.stageStat = ParseEnum(p1, CombatStageStat.Speed);
                        effect.stageAmount = ParseInt(p2);
                        effect.turnBudget = ParseInt(p3, 1);
                        effect.chance = string.IsNullOrWhiteSpace(p4) ? 1f : ParsePercentOrFloat(p4);
                        effect.stackKey = parts.Length > 5 ? parts[5].Trim() : string.Empty;
                        break;

                    case UtilityEffectType.MoveAPDiscountWhileHPAbove:
                        effect.apGain = ParseInt(p1, 1);
                        effect.threshold = ParseInt(p2, 70);
                        effect.stackKey = p3;
                        break;

                    case UtilityEffectType.LifestealPercent:
                        effect.magnitude = ParsePercentOrFloat(p1);
                        effect.targetOverride = MoveTargeting.Self;
                        effect.stackKey = p2;
                        break;

                    case UtilityEffectType.HealPercentIfStageAtLeast:
                        effect.healPercentMaxHP = ParsePercentOrFloat(p1);
                        effect.stageStat = ParseEnum(p2, CombatStageStat.MeleeDef);
                        effect.threshold = ParseInt(p3);
                        effect.targetOverride = string.IsNullOrWhiteSpace(p4)
                            ? MoveTargeting.Self
                            : ParseEnum(p4, MoveTargeting.Self);
                        effect.stackKey = parts.Length > 5 ? parts[5].Trim() : string.Empty;
                        break;
                }

                list.Add(effect);
            }

            return list;
        }

        private static MoveResolveProfile ParseMoveResolveProfile(CombatCsvColumnMap columns, string[] fields)
        {
            columns ??= new CombatCsvColumnMap(new Dictionary<string, int>());
            fields ??= Array.Empty<string>();

            string requireAllRaw = columns.Get(fields, "ResolveRequireAll");
            string condRequireAllRaw = columns.Get(fields, "ConditionalDamageRequireAll");
            string scalingRaw = columns.Get(fields, "ConditionalDamageScalingCondition");

            var profile = new MoveResolveProfile
            {
                primaryTiming = ParseEnum(
                    columns.Get(fields, "ResolvePrimaryTiming"),
                    MoveEffectTimingBucket.ResolveTimePreHit),
                utilityExecutionTiming = ParseEnum(
                    columns.Get(fields, "ResolveUtilityTiming"),
                    MoveUtilityExecutionTiming.UtilityOnly),
                conditionalEffectPolicy = ParseEnum(
                    columns.Get(fields, "ResolveConditionalPolicy"),
                    MoveConditionalEffectPolicy.LegacyAndResolveConditions),
                resolveConditions = ParseResolveConditions(columns.Get(fields, "ResolveConditions")),
                requireAllResolveConditions = string.IsNullOrWhiteSpace(requireAllRaw) || ParseBool(requireAllRaw, true),
            };

            profile.damage.powerMultiplier = ParseFloat(columns.Get(fields, "DamagePowerMultiplier"), 1f);
            profile.damage.preMitigationDamageMultiplier = ParseFloat(columns.Get(fields, "DamagePreMitigationMultiplier"), 1f);
            profile.damage.finalDamageMultiplier = ParseFloat(columns.Get(fields, "DamageFinalMultiplier"), 1f);
            profile.damage.overrideAttackStat = ParseBool(columns.Get(fields, "DamageOverrideAttackStat"));
            profile.damage.attackStatOverride = ParseEnum(columns.Get(fields, "DamageAttackStat"), CoreStat.MeleeAtk);

            profile.conditionalDamage.multiplier = ParseFloat(columns.Get(fields, "ConditionalDamageMultiplier"), 1f);
            profile.conditionalDamage.conditions = ParseResolveConditions(columns.Get(fields, "ConditionalDamageConditions"));
            profile.conditionalDamage.requireAllConditions =
                string.IsNullOrWhiteSpace(condRequireAllRaw) || ParseBool(condRequireAllRaw, true);
            if (!string.IsNullOrWhiteSpace(scalingRaw) &&
                TryParseResolveCondition(scalingRaw, out MoveResolveCondition scaling))
            {
                profile.conditionalDamage.useScalingCondition = true;
                profile.conditionalDamage.scalingCondition = scaling;
            }
            profile.conditionalDamage.bonusMultiplierPerCount =
                ParseFloat(columns.Get(fields, "ConditionalDamageBonusPerCount"));
            profile.conditionalDamage.maxMultiplier =
                ParseFloat(columns.Get(fields, "ConditionalDamageMaxMultiplier"));
            profile.conditionalDamage.randomMultipliers =
                ParseFloatList(columns.Get(fields, "RandomDamageMultipliers"));
            MoveResolveProfileUtility.EnsureNestedDefaults(profile);
            return profile;
        }

        private static List<MoveResolveCondition> ParseResolveConditions(string raw)
        {
            var conditions = new List<MoveResolveCondition>();
            if (string.IsNullOrWhiteSpace(raw))
                return conditions;
            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                if (TryParseResolveCondition(entries[i], out MoveResolveCondition condition))
                    conditions.Add(condition);
            }
            return conditions;
        }

        private static bool TryParseResolveCondition(string raw, out MoveResolveCondition condition)
        {
            condition = new MoveResolveCondition();
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string[] parts = raw.Trim().Split(':');
            if (parts.Length < 2 || !Enum.TryParse(parts[1].Trim(), true, out MoveResolveConditionType type))
                return false;
            condition.subject = ParseEnum(parts[0].Trim(), MoveResolveConditionSubject.Self);
            condition.conditionType = type;
            condition.threshold = parts.Length > 2 ? ParseFloat(parts[2].Trim()) : 0f;
            condition.statusId = parts.Length > 3 ? ParseEnum(parts[3].Trim(), StatusId.None) : StatusId.None;
            condition.stat = parts.Length > 4 ? ParseEnum(parts[4].Trim(), CoreStat.MeleeAtk) : CoreStat.MeleeAtk;
            condition.comparisonStat = parts.Length > 5 ? ParseEnum(parts[5].Trim(), CoreStat.MeleeAtk) : CoreStat.MeleeAtk;
            return true;
        }

        private static List<float> ParseFloatList(string raw)
        {
            var values = new List<float>();
            if (string.IsNullOrWhiteSpace(raw))
                return values;
            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
                values.Add(ParseFloat(parts[i].Trim(), 1f));
            return values;
        }

        private static float ParsePercentOrFloat(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0f;

            string trimmed = raw.Trim();
            bool isPercent = trimmed.EndsWith("%", StringComparison.Ordinal);
            if (isPercent)
                trimmed = trimmed.Substring(0, trimmed.Length - 1);

            if (!float.TryParse(trimmed, out float value))
                return 0f;

            if (isPercent || value > 1f)
                return value / 100f;
            return value;
        }

        // ———— Import Modifiers from CSV ————

        public void ImportModifiersFromCSV()
        {
            _logOutput = "Importing Modifiers from CSV...\n\n";
            var lines = ReadCsvLines("Modifiers.csv");
            if (lines == null) return;

            EnsureOrganizedFolders();

            string catPath = $"{ModifiersCatalogFolder}/ModifierCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ModifierCatalogSO>(catPath);
            if (catalog == null && !_dryRun)
            {
                catalog = ScriptableObject.CreateInstance<ModifierCatalogSO>();
                AssetDatabase.CreateAsset(catalog, catPath);
                _logOutput += "Created: ModifierCatalog.asset\n";
            }
            if (catalog != null) catalog.modifiers.Clear();

            string[] headerFields = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string headerLine = lines[i].Trim();
                if (string.IsNullOrEmpty(headerLine) || headerLine.StartsWith("#")) continue;
                headerFields = ParseCSVLine(headerLine);
                break;
            }

            int displayNameCol = CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "DisplayName");
            bool headerDriven = displayNameCol >= 0;
            int categoryCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "Category") : 1;
            int scopeCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "Scope") : 2;
            int minRarityCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "MinRarity") : 3;
            int effectMinCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "EffectMin") : 4;
            int effectMaxCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "EffectMax") : 5;
            int allowedCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "AllowedCategories") : 6;
            int forbiddenCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "ForbiddenCombos") : 7;
            int slotCostCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "SlotCost") : 8;
            int infusionCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "IsTypeInfusion") : 9;
            int statSubCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "IsStatSub") : 10;
            int subStatCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "SubStat") : 11;
            int triggerCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "TriggerCondition") : 12;
            int triggerChanceCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "TriggerChance") : 13;
            int effectDescCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "EffectDescription") : 14;
            int powerCapCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "PowerCap") : 15;
            int apCostCapCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "APCostCap") : 16;
            int forbiddenTagsCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "ForbiddenTags") : 17;
            int slotTypeCol = headerDriven ? CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "SlotType") : 18;
            int weightCol = CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "Weight");
            int lowHpCol = CombatCsvRoundTrip.FindHeaderColumnIndex(headerFields, "LowHpThreshold");

            int imported = 0;
            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var f = ParseCSVLine(line);
                int minFields = headerDriven ? 16 : 15;
                if (f.Length < minFields)
                {
                    _logOutput += $"  WARN: Row {row + 1} has {f.Length} fields (need at least {minFields}), skipped\n";
                    continue;
                }

                string sectionId        = GetField(f, 0);
                if (string.Equals(sectionId, "SectionId", StringComparison.OrdinalIgnoreCase))
                    continue;

                string displayName      = displayNameCol >= 0 ? GetField(f, displayNameCol) : "";
                var category            = ParseEnum(GetField(f, categoryCol), ModifierCategory.POT);
                var scope               = ParseEnum(GetField(f, scopeCol), ModifierScope.Active);
                var minRarity           = ParseEnum(GetField(f, minRarityCol), RarityTier.Common);
                float effectMin         = ParseFloat(GetField(f, effectMinCol));
                float effectMax         = ParseFloat(GetField(f, effectMaxCol));
                string allowedCatsRaw   = GetField(f, allowedCol);
                string forbiddenRaw     = GetField(f, forbiddenCol);
                int slotCost            = ParseInt(GetField(f, slotCostCol));
                bool isTypeInfusion     = ParseBool(GetField(f, infusionCol));
                bool isStatSub          = ParseBool(GetField(f, statSubCol));
                var subStat             = ParseEnum(GetField(f, subStatCol), CoreStat.MeleeAtk);
                var triggerCond         = ParseEnum(GetField(f, triggerCol), ModifierTriggerCondition.None);
                float triggerChance     = ParseFloat(GetField(f, triggerChanceCol));
                string effectDesc       = GetField(f, effectDescCol);
                int powerCap            = powerCapCol >= 0 ? ParseInt(GetField(f, powerCapCol)) : 0;
                int apCostCap           = apCostCapCol >= 0 ? ParseInt(GetField(f, apCostCapCol)) : 0;
                string forbiddenTagsRaw = forbiddenTagsCol >= 0 ? GetField(f, forbiddenTagsCol) : "";
                var slotType            = slotTypeCol >= 0
                    ? ParseEnum(GetField(f, slotTypeCol), ModifierSlotType.None)
                    : ModifierSlotType.None;

                float lowHpThreshold = 30f;
                if (lowHpCol >= 0)
                    lowHpThreshold = ParseFloat(GetField(f, lowHpCol), 30f);
                else if (!headerDriven && weightCol != 19 && f.Length >= 20)
                    lowHpThreshold = ParseFloat(GetField(f, 19), 30f);

                int weight = 1;
                if (weightCol >= 0)
                    weight = Math.Max(0, ParseInt(GetField(f, weightCol), 1));
                else if (!headerDriven && f.Length >= 21)
                    weight = Math.Max(0, ParseInt(GetField(f, 20), 1));

                if (string.IsNullOrEmpty(sectionId))
                {
                    _logOutput += $"  WARN: Empty SectionId on row {row + 1}, skipped\n";
                    continue;
                }

                string safeName = Regex.Replace(sectionId, @"[^a-zA-Z0-9_\-]", "_");
                string assetName = $"Modifier_{safeName}";
                string assetPath = $"{ModifiersFolder}/{assetName}.asset";

                if (_dryRun)
                {
                    _logOutput += $"  [DRY] {assetName}: {category}/{scope} — {effectDesc}\n";
                    imported++;
                    continue;
                }

                var def = AssetDatabase.LoadAssetAtPath<ModifierDefinitionSO>(assetPath);
                bool created = def == null;
                if (created)
                {
                    def = ScriptableObject.CreateInstance<ModifierDefinitionSO>();
                    def.name = assetName;
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.sectionId            = sectionId;
                def.displayName          = displayName;
                def.category             = category;
                def.scope                = scope;
                def.minimumRarity        = minRarity;
                def.effectMin            = effectMin;
                def.effectMax            = effectMax;
                def.weight               = weight;
                def.slotCostIncrease     = slotCost;
                def.isTypeInfusion       = isTypeInfusion;
                def.isStatSubstitution   = isStatSub;
                def.substitutionStat     = subStat;
                def.triggerCondition     = triggerCond;
                def.triggerChance        = triggerChance;
                def.effectDescription    = effectDesc;
                def.powerCap             = powerCap;
                def.apCostCap            = apCostCap;
                def.forbiddenTags        = SplitPipe(forbiddenTagsRaw);
                def.slotType             = slotType;
                def.lowHpThresholdPercent = lowHpThreshold;

                var allowedCats = new List<MoveCategory>();
                foreach (string s in SplitPipe(allowedCatsRaw))
                {
                    if (Enum.TryParse<MoveCategory>(s.Trim(), true, out var mc))
                        allowedCats.Add(mc);
                }
                def.allowedCategories = allowedCats.ToArray();
                def.forbiddenCombinations = SplitPipe(forbiddenRaw);

                EditorUtility.SetDirty(def);
                if (catalog != null) catalog.modifiers.Add(def);
                imported++;
                _logOutput += $"  {(created ? "Created" : "Updated")}: {assetName}\n";
            }

            if (catalog != null) EditorUtility.SetDirty(catalog);
            if (!_dryRun) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            _logOutput += $"\n{(_dryRun ? "[DRY RUN] " : "")}Imported {imported} modifiers.\n";
        }

        // ———— Import Chemistry Matrix from CSV ————

        private void ImportChemistryMatrixFromCSV()
        {
            _logOutput = "Importing Chemistry Matrix from CSV...\n\n";
            string sourceFile;
            var lines = ReadFirstExistingSpecCsvLines(
                out sourceFile,
                "COMBAT_Interaction_Matrix_v1.csv",
                "COMBAT_Interaction_Matrix_v0.csv");
            if (lines == null) return;
            _logOutput += $"Source: {sourceFile}\n\n";

            EnsureOrganizedFolders();

            string assetPath = $"{TypeChemistryFolder}/TypeChemistryMatrix.asset";
            var chemMatrix = AssetDatabase.LoadAssetAtPath<TypeChemistryMatrixSO>(assetPath);
            if (chemMatrix == null && !_dryRun)
            {
                chemMatrix = ScriptableObject.CreateInstance<TypeChemistryMatrixSO>();
                AssetDatabase.CreateAsset(chemMatrix, assetPath);
                _logOutput += "Created: TypeChemistryMatrix.asset\n";
            }

            if (lines.Length < 3)
            {
                _logOutput += "ERROR: CSV has fewer than 3 rows (need header + data).\n";
                return;
            }

            var statusCatalog = AssetDatabase.LoadAssetAtPath<StatusCatalogSO>($"{StatusDefCatalogFolder}/StatusCatalog.asset");
            if (statusCatalog == null)
            {
                _logOutput += "ERROR: StatusCatalog.asset is required to validate chemistry outputs.\n";
                return;
            }

            var headerFields = ParseCSVLine(lines[1].Trim());
            if (headerFields.Length < 2)
            {
                _logOutput += "ERROR: Header row has no defender type columns.\n";
                return;
            }

            var defenderTypes = new List<ChainlingType>();
            for (int c = 1; c < headerFields.Length && c <= 14; c++)
            {
                string colName = headerFields[c].Trim();
                if (string.IsNullOrEmpty(colName)) break;
                if (Enum.TryParse<ChainlingType>(colName, true, out var dt))
                    defenderTypes.Add(dt);
                else
                    _logOutput += $"  WARN: Unknown defender type '{colName}' in column {c + 1}, skipping column\n";
            }

            int imported = 0;
            int validationErrors = 0;
            var parsedReactions = new List<ChemistryReaction>();
            var seenDirections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int row = 2; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCSVLine(line);
                if (fields.Length < 2) continue;

                string attackerStr = fields[0].Trim();
                if (!Enum.TryParse<ChainlingType>(attackerStr, true, out var attackerType))
                {
                    _logOutput += $"  WARN: Unknown attacker type '{attackerStr}' on row {row + 1}, skipped\n";
                    continue;
                }

                for (int col = 1; col < fields.Length && col - 1 < defenderTypes.Count; col++)
                {
                    string cell = fields[col].Trim();
                    if (string.IsNullOrEmpty(cell) || cell == "-") continue;

                    var defenderType = defenderTypes[col - 1];
                    if (attackerType == defenderType)
                    {
                        _logOutput += $"  ERROR: Self chemistry cell is not allowed: {attackerType} row {row + 1} col {col + 1}\n";
                        validationErrors++;
                        continue;
                    }

                    string directionKey = $"{attackerType}>{defenderType}";
                    string reverseKey = $"{defenderType}>{attackerType}";
                    if (seenDirections.Contains(reverseKey))
                    {
                        _logOutput += $"  ERROR: Mutual chemistry advantage detected: {attackerType}->{defenderType} conflicts with {defenderType}->{attackerType}\n";
                        validationErrors++;
                        continue;
                    }
                    if (!seenDirections.Add(directionKey))
                    {
                        _logOutput += $"  ERROR: Duplicate chemistry direction detected: {attackerType}->{defenderType}\n";
                        validationErrors++;
                        continue;
                    }

                    if (!TryParseChemistryCell(cell, out ChemistryOutput output, out string parseError))
                    {
                        _logOutput += $"  ERROR: {parseError} at row {row + 1} col {col + 1}: '{cell}'\n";
                        validationErrors++;
                        continue;
                    }

                    StatusDefinitionSO statusDef = statusCatalog.Get(output.statusId);
                    if (statusDef == null || statusDef.origin != StatusOrigin.ChemistryOnly)
                    {
                        _logOutput += $"  ERROR: {output.statusId} is not an authored ChemistryOnly status at row {row + 1} col {col + 1}\n";
                        validationErrors++;
                        continue;
                    }
                    if (!ChemistryStatusRules.IsCoreFamily(ChemistryStatusRules.GetFamily(output.statusId)))
                    {
                        _logOutput += $"  ERROR: {output.statusId} is outside the eight core chemistry families at row {row + 1} col {col + 1}\n";
                        validationErrors++;
                        continue;
                    }
                    if (!ChemistryStatusRules.IsRecipientValid(output.statusId, output.recipient))
                    {
                        _logOutput += $"  ERROR: {output.statusId} cannot target {output.recipient} at row {row + 1} col {col + 1}\n";
                        validationErrors++;
                        continue;
                    }

                    if (_dryRun)
                    {
                        _logOutput += $"  [DRY] {attackerType} vs {defenderType}: {output.statusId} @{output.recipient}\n";
                        imported++;
                        continue;
                    }

                    parsedReactions.Add(new ChemistryReaction
                    {
                        attackerType = attackerType,
                        defenderType = defenderType,
                        output = output
                    });
                    imported++;
                }
            }

            if (validationErrors > 0)
            {
                _logOutput += $"\nERROR: Chemistry import aborted with {validationErrors} validation error(s).\n";
                return;
            }

            if (!_dryRun && chemMatrix != null)
            {
                chemMatrix.reactions.Clear();
                chemMatrix.reactions.AddRange(parsedReactions);
                EditorUtility.SetDirty(chemMatrix);
            }
            if (!_dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            _logOutput += $"\n{(_dryRun ? "[DRY RUN] " : "")}Imported {imported} chemistry reactions.\n";
        }

        private static bool TryParseChemistryCell(
            string cell,
            out ChemistryOutput output,
            out string error)
        {
            output = default;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(cell))
            {
                error = "Empty chemistry cell";
                return false;
            }

            if (cell.Contains(" + ") || Regex.IsMatch(cell, @"\s+x\d+\s*", RegexOptions.IgnoreCase))
            {
                error = "Chemistry cells are stackless and may contain only one output";
                return false;
            }

            Match recipientMatch = Regex.Match(cell, @"\s+@(Self|Foe)\s*$", RegexOptions.IgnoreCase);
            if (!recipientMatch.Success)
            {
                error = "Chemistry output must declare @Self or @Foe";
                return false;
            }

            ChemistryRecipient recipient = recipientMatch.Groups[1].Value.Equals("Self", StringComparison.OrdinalIgnoreCase)
                ? ChemistryRecipient.Attacker
                : ChemistryRecipient.Defender;
            string outputName = cell.Substring(0, recipientMatch.Index).Trim();
            if (!TryParseChemistryOutput(outputName, recipient, out output))
            {
                error = $"Unknown chemistry output '{outputName}'";
                return false;
            }

            return true;
        }

        private static bool TryParseChemistryOutput(
            string name,
            ChemistryRecipient recipient,
            out ChemistryOutput output)
        {
            output = default;
            if (string.IsNullOrWhiteSpace(name)) return false;

            string normalized = name.Replace(" ", "");

            if (name.StartsWith("Hyped ", StringComparison.OrdinalIgnoreCase) && name.Contains("Coating"))
            {
                string typePart = name.Substring(6).Replace(" Coating", "").Trim();
                if (TryParseCoatingType(typePart, out var paramType))
                {
                    output = new ChemistryOutput { statusId = StatusId.HypedTypeCoating, recipient = recipient, parameterType = paramType };
                    return true;
                }
            }

            if (name.Contains("Coating") && !name.StartsWith("Hyped"))
            {
                string typePart = name.Replace(" Coating", "").Trim();
                if (string.IsNullOrEmpty(typePart) && name.Contains(" "))
                    typePart = name.Split(new[] { " " }, StringSplitOptions.None)[0];
                if (!string.IsNullOrEmpty(typePart) && TryParseCoatingType(typePart, out var paramType))
                {
                    output = new ChemistryOutput { statusId = StatusId.TypeCoating, recipient = recipient, parameterType = paramType };
                    return true;
                }
            }

            if (name.StartsWith("Backlash ", StringComparison.OrdinalIgnoreCase) && name.Contains("Dmg"))
            {
                string typePart = name.Replace("Backlash ", "").Replace(" Dmg", "").Replace("Dmg", "").Trim();
                if (TryParseCoatingType(typePart, out var paramType))
                {
                    output = new ChemistryOutput { statusId = StatusId.BacklashType, recipient = recipient, parameterType = paramType };
                    return true;
                }
            }

            var statusMap = new Dictionary<string, StatusId>(StringComparer.OrdinalIgnoreCase)
            {
                { "Pumped", StatusId.Pumped }, { "Drained", StatusId.Drained }, { "Rugged", StatusId.Rugged },
                { "Defueled", StatusId.Defueled }, { "Firewall", StatusId.Firewall }, { "Slowed", StatusId.Slowed },
                { "Shattered", StatusId.Shattered }, { "Drip", StatusId.Drip }, { "Splinter", StatusId.Splinter },
                { "FOMO", StatusId.FOMO }, { "Siphoned", StatusId.Siphoned },
                { "Anchored", StatusId.Anchored }, { "Amplified", StatusId.Amplified }, { "Dusted", StatusId.Dusted },
                { "Corroded", StatusId.Corroded }, { "Reinforced", StatusId.Reinforced },
                { "Isolated", StatusId.Isolated }, { "Jammed", StatusId.Jammed },
                { "Leaked", StatusId.Leaked },
                { "CascadeFailure", StatusId.CascadeFailure },
                { "Cascade Failure", StatusId.CascadeFailure }
            };

            if (statusMap.TryGetValue(name, out var statusId) || statusMap.TryGetValue(normalized, out statusId))
            {
                output = new ChemistryOutput { statusId = statusId, recipient = recipient, parameterType = ChainlingType.None };
                return true;
            }

            return false;
        }

        private static bool TryParseCoatingType(string typeName, out ChainlingType type)
        {
            type = ChainlingType.None;
            if (string.IsNullOrWhiteSpace(typeName)) return false;

            var map = new Dictionary<string, ChainlingType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Glitch", ChainlingType.Glitched }, { "Glitched", ChainlingType.Glitched },
                { "Freeze", ChainlingType.Frozen }, { "Frozen", ChainlingType.Frozen },
                { "Fork", ChainlingType.Forked }, { "Forked", ChainlingType.Forked },
                { "Node", ChainlingType.Node }, { "Gas", ChainlingType.Gas }, { "Block", ChainlingType.Block },
                { "Encrypted", ChainlingType.Encrypted }, { "Burned", ChainlingType.Burned },
                { "Shard", ChainlingType.Shard }, { "Bot", ChainlingType.Bot }, { "Mined", ChainlingType.Mined },
                { "Staked", ChainlingType.Staked }, { "Compressed", ChainlingType.Compressed },
                { "Meme", ChainlingType.Meme }
            };

            return map.TryGetValue(typeName.Trim(), out type);
        }

        private static void AppendImportedProgression(
            SpeciesDefinitionSO species,
            List<MoveDefinitionSO> moves,
            ProgressionPoolTag tag,
            MoveDefinitionSO flagship)
        {
            if (species?.progressionMovePool == null || moves == null)
                return;

            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinitionSO move = moves[i];
                if (move == null)
                    continue;

                bool alreadyPresent = false;
                for (int j = 0; j < species.progressionMovePool.Count; j++)
                {
                    SpeciesProgressionMoveEntry existing = species.progressionMovePool[j];
                    if (existing != null && existing.move == move)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (alreadyPresent)
                    continue;

                bool signature = flagship != null && move == flagship;
                species.progressionMovePool.Add(new SpeciesProgressionMoveEntry
                {
                    move = move,
                    weight = signature ? SpeciesProgressionPoolWeights.Signature : SpeciesProgressionPoolWeights.Standard,
                    useWeightAtStarFive = signature,
                    weightAtStarFive = signature
                        ? SpeciesProgressionPoolWeights.SignatureAtStarFive
                        : SpeciesProgressionPoolWeights.Standard,
                    isSignature = signature,
                    poolTag = tag,
                    minStar = 0,
                    maxStar = CombatConstants.STAR_MAX,
                    excludeIfOwned = false,
                });
            }
        }

        // ———— Import Species Pools from CSV ————

        private void ImportSpeciesPoolsFromCSV()
        {
            _logOutput = "Importing Species Pools from CSV...\n\n";
            var lines = ReadCsvLines("SpeciesMovePools.csv");
            if (lines == null) return;

            EnsureOrganizedFolders();

            var speciesDb = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>($"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset");
            if (speciesDb == null) { _logOutput += "ERROR: SpeciesDatabase.asset not found.\n"; return; }

            var moveDb = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>($"{MovesDatabaseFolder}/MoveDatabase.asset");
            if (moveDb == null) { _logOutput += "ERROR: MoveDatabase.asset not found.\n"; return; }

            var moveLookup = BuildMoveLookup(moveDb);
            int imported = 0;

            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var f = ParseCSVLine(line);
                if (f.Length < 4)
                {
                    _logOutput += $"  WARN: Row {row + 1} has {f.Length} fields (need 4), skipped\n";
                    continue;
                }

                string speciesId      = GetField(f, 0);
                string learnableRaw   = GetField(f, 1);
                string exclusiveRaw   = GetField(f, 2);
                string flagshipId     = GetField(f, 3);
                string initialRaw     = GetField(f, 4);
                string offTypeRaw     = GetField(f, 5);

                var speciesDef = speciesDb.GetById(speciesId);
                if (speciesDef == null)
                {
                    _logOutput += $"  WARN: Species '{speciesId}' not in database, skipped\n";
                    continue;
                }

                if (_dryRun)
                {
                    _logOutput += $"  [DRY] {speciesId}: learnable={SplitPipe(learnableRaw).Length}, exclusive={SplitPipe(exclusiveRaw).Length}, initial={SplitPipe(initialRaw).Length}\n";
                    imported++;
                    continue;
                }

                var learnableMoves = new List<MoveDefinitionSO>();
                foreach (string id in SplitPipe(learnableRaw))
                {
                    var m = FindMoveById(moveLookup, id.Trim());
                    if (m != null) learnableMoves.Add(m);
                    else _logOutput += $"    WARN: Move '{id}' not found for {speciesId}\n";
                }

                var exclusiveMoves = new List<MoveDefinitionSO>();
                foreach (string id in SplitPipe(exclusiveRaw))
                {
                    var m = FindMoveById(moveLookup, id.Trim());
                    if (m != null) exclusiveMoves.Add(m);
                    else _logOutput += $"    WARN: Exclusive move '{id}' not found for {speciesId}\n";
                }

                MoveDefinitionSO flagship = null;
                if (!string.IsNullOrEmpty(flagshipId))
                    flagship = FindMoveById(moveLookup, flagshipId);

                var initialMoves = new List<MoveDefinitionSO>();
                foreach (string id in SplitPipe(initialRaw))
                {
                    var m = FindMoveById(moveLookup, id.Trim());
                    if (m == null)
                    {
                        _logOutput += $"    WARN: Initial move '{id}' not found for {speciesId}\n";
                        continue;
                    }

                    if (!learnableMoves.Contains(m) && !exclusiveMoves.Contains(m))
                    {
                        _logOutput += $"    WARN: Initial move '{id}' is not in {speciesId}'s learnable or exclusive pool\n";
                        continue;
                    }

                    if (!initialMoves.Contains(m))
                        initialMoves.Add(m);
                }

                var offTypeMoves = new List<MoveDefinitionSO>();
                foreach (string id in SplitPipe(offTypeRaw))
                {
                    var m = FindMoveById(moveLookup, id.Trim());
                    if (m != null && !offTypeMoves.Contains(m))
                        offTypeMoves.Add(m);
                    else if (m == null)
                        _logOutput += $"    WARN: Off-type move '{id}' not found for {speciesId}\n";
                }

                speciesDef.learnableMoves = learnableMoves;
                speciesDef.learnLevels = new List<int>();
                for (int i = 0; i < learnableMoves.Count; i++) speciesDef.learnLevels.Add(1);
                speciesDef.exclusiveMoves = exclusiveMoves;
                speciesDef.flagshipMove = flagship;
                speciesDef.initialMoves = initialMoves;
                speciesDef.offTypeMoveExceptions = offTypeMoves;
                speciesDef.progressionMovePool = new List<SpeciesProgressionMoveEntry>();
                AppendImportedProgression(speciesDef, learnableMoves, ProgressionPoolTag.Learnable, flagship);
                AppendImportedProgression(speciesDef, exclusiveMoves, ProgressionPoolTag.Exclusive, flagship);
                EditorUtility.SetDirty(speciesDef);

                imported++;
                _logOutput += $"  {speciesId}: {learnableMoves.Count} learnable, {exclusiveMoves.Count} exclusive, initial={initialMoves.Count}, flagship={flagship?.moveId ?? "NONE"}\n";
            }

            if (!_dryRun) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            _logOutput += $"\n{(_dryRun ? "[DRY RUN] " : "")}Imported {imported} species pools.\n";
        }

        // ———— Import All From Data ————

        private void ImportAllFromData()
        {
            _logOutput = "=== Import All From Data ===\n\n";
            ImportStatusesFromCSV();
            _logOutput += "\n---\n\n";
            ImportChemistryMatrixFromCSV();
            _logOutput += "\n---\n\n";
            ImportMovesFromCSV();
            _logOutput += "\n---\n\n";
            ImportModifiersFromCSV();
            _logOutput += "\n---\n\n";
            ImportSpeciesPoolsFromCSV();
            _logOutput += "\n=== All imports complete. ===\n";
        }

        // ———— Test Tooling: Random Modifier Assignment ————

        private void AssignRandomTestModifiers()
        {
            _logOutput = "Assigning Random Test Modifiers...\n\n";

            var modCatPath = $"{ModifiersCatalogFolder}/ModifierCatalog.asset";
            var modCatalog = AssetDatabase.LoadAssetAtPath<ModifierCatalogSO>(modCatPath);
            if (modCatalog == null || modCatalog.modifiers.Count == 0)
            {
                _logOutput += "ERROR: ModifierCatalog is empty or not found. Import modifiers first.\n";
                return;
            }

            var moveDbPath = $"{MovesDatabaseFolder}/MoveDatabase.asset";
            var moveDb = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>(moveDbPath);
            if (moveDb == null)
            {
                _logOutput += "ERROR: MoveDatabase.asset not found.\n";
                return;
            }
            var moveLookup = BuildMoveLookup(moveDb);

            if (_targetProvider == null)
            {
                _logOutput += "ERROR: No target provider set. Drag a ManualPartyProviderSO or ManualEncounterProviderSO into the Target Provider field.\n";
                return;
            }

            var rng = new System.Random();
            var serialized = new SerializedObject(_targetProvider);
            var rosterProp = serialized.FindProperty("roster");
            if (rosterProp == null || !rosterProp.isArray)
            {
                _logOutput += "ERROR: Target provider does not have a 'roster' array.\n";
                return;
            }

            for (int c = 0; c < rosterProp.arraySize; c++)
            {
                var entry = rosterProp.GetArrayElementAtIndex(c);
                var moveIdsProp = entry.FindPropertyRelative("moveIds");
                var mod1Prop = entry.FindPropertyRelative("modifier1SectionIds");
                var mod2Prop = entry.FindPropertyRelative("modifier2SectionIds");
                var speciesIdProp = entry.FindPropertyRelative("speciesId");

                if (moveIdsProp == null || mod1Prop == null || mod2Prop == null) continue;
                string speciesId = speciesIdProp != null ? speciesIdProp.stringValue : "?";

                mod1Prop.arraySize = moveIdsProp.arraySize;
                mod2Prop.arraySize = moveIdsProp.arraySize;

                for (int m = 0; m < moveIdsProp.arraySize; m++)
                {
                    string mid = moveIdsProp.GetArrayElementAtIndex(m).stringValue;
                    var moveDef = FindMoveById(moveLookup, mid);
                    MoveCategory moveCat = moveDef != null ? moveDef.category : MoveCategory.StatusUtility;
                    var moveFallback = new MoveInstance
                    {
                        definitionId = mid,
                        displayName = moveDef != null ? moveDef.displayName : mid,
                        category = moveCat,
                        basePower = moveDef != null ? moveDef.power : 0,
                        apCost = moveDef != null ? moveDef.apCost : 0,
                        tags = moveDef != null ? new List<string>(moveDef.tags) : new List<string>(),
                    };

                    string picked1 = "";
                    string picked2 = "";
                    if (_modifierDensity >= 1)
                    {
                        var pool1 = new List<ModifierDefinitionSO>();
                        foreach (var mod in modCatalog.modifiers)
                        {
                            if (mod == null) continue;
                            var check = moveDef != null
                                ? ModifierValidationService.CanApply(mod, moveDef, modCatalog, Array.Empty<string>())
                                : ModifierValidationService.CanApply(mod, moveFallback, modCatalog, Array.Empty<string>());
                            if (check.isValid)
                                pool1.Add(mod);
                        }

                        if (pool1.Count > 0)
                            picked1 = pool1[rng.Next(pool1.Count)].sectionId;
                    }
                    if (_modifierDensity >= 2 && !string.IsNullOrEmpty(picked1))
                    {
                        var existing = new[] { picked1 };
                        var pool2 = new List<ModifierDefinitionSO>();
                        foreach (var mod in modCatalog.modifiers)
                        {
                            if (mod == null) continue;
                            if (string.Equals(mod.sectionId, picked1, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var check = moveDef != null
                                ? ModifierValidationService.CanApply(mod, moveDef, modCatalog, existing)
                                : ModifierValidationService.CanApply(mod, moveFallback, modCatalog, existing);
                            if (check.isValid)
                                pool2.Add(mod);
                        }

                        if (pool2.Count > 0)
                            picked2 = pool2[rng.Next(pool2.Count)].sectionId;
                    }

                    mod1Prop.GetArrayElementAtIndex(m).stringValue = picked1;
                    mod2Prop.GetArrayElementAtIndex(m).stringValue = picked2;

                    _logOutput += $"  [{speciesId}] move '{mid}': mod1={picked1}, mod2={picked2}\n";
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_targetProvider);
            AssetDatabase.SaveAssets();
            _logOutput += $"\nRandom modifiers assigned (density={_modifierDensity}).\n";
        }

        // ———— Test Tooling: Apply Loadout from CSV ————

        private void ApplyLoadoutFromCSV()
        {
            _logOutput = "Applying Test Loadout...\n\n";
            var lines = ReadCsvLines("TestLoadouts.csv");
            if (lines == null) return;

            if (_targetProvider == null)
            {
                _logOutput += "ERROR: No target provider set.\n";
                return;
            }

            var serialized = new SerializedObject(_targetProvider);
            var rosterProp = serialized.FindProperty("roster");
            if (rosterProp == null || !rosterProp.isArray) { _logOutput += "ERROR: No 'roster' array.\n"; return; }

            int applied = 0;
            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var f = ParseCSVLine(line);
                if (f.Length < 5) continue;

                string loadoutId  = GetField(f, 0);
                string speciesId  = GetField(f, 1);
                string moveIdsRaw = GetField(f, 2);
                string mod1Raw    = GetField(f, 3);
                string mod2Raw    = GetField(f, 4);

                if (applied >= rosterProp.arraySize) rosterProp.InsertArrayElementAtIndex(applied);
                var entry = rosterProp.GetArrayElementAtIndex(applied);

                entry.FindPropertyRelative("speciesId").stringValue = speciesId;
                entry.FindPropertyRelative("displayName").stringValue = "";
                entry.FindPropertyRelative("stars").intValue = 3;
                entry.FindPropertyRelative("rebirthCount").intValue = 0;
                entry.FindPropertyRelative("evolutionStage").intValue = 1;

                var moveIdsProp = entry.FindPropertyRelative("moveIds");
                var mod1Prop = entry.FindPropertyRelative("modifier1SectionIds");
                var mod2Prop = entry.FindPropertyRelative("modifier2SectionIds");

                string[] moveIds = SplitPipe(moveIdsRaw);
                string[] mod1Ids = SplitPipe(mod1Raw);
                string[] mod2Ids = SplitPipe(mod2Raw);

                moveIdsProp.arraySize = moveIds.Length;
                mod1Prop.arraySize = moveIds.Length;
                mod2Prop.arraySize = moveIds.Length;

                for (int i = 0; i < moveIds.Length; i++)
                {
                    moveIdsProp.GetArrayElementAtIndex(i).stringValue = moveIds[i].Trim();
                    mod1Prop.GetArrayElementAtIndex(i).stringValue = i < mod1Ids.Length ? mod1Ids[i].Trim() : "";
                    mod2Prop.GetArrayElementAtIndex(i).stringValue = i < mod2Ids.Length ? mod2Ids[i].Trim() : "";
                }

                _logOutput += $"  Slot {applied}: [{loadoutId}] {speciesId} with {moveIds.Length} moves\n";
                applied++;
            }

            rosterProp.arraySize = applied;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_targetProvider);
            AssetDatabase.SaveAssets();
            _logOutput += $"\nApplied {applied} loadout entries.\n";
        }

        // ———— Random Matchup Generation ————

        private void GenerateRandomLineup(UnityEngine.Object provider)
        {
            if (provider == null)
            {
                _logOutput = "ERROR: No provider set. Assign Party or Encounter provider.\n";
                return;
            }

            _logOutput = $"Generating Random Lineup for {provider.name}...\n\n";

            var speciesDb = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>($"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset");
            if (speciesDb == null || speciesDb.species == null || speciesDb.species.Count == 0)
            {
                _logOutput += "ERROR: SpeciesDatabase.asset not found or empty. Import species first.\n";
                return;
            }

            var moveDb = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>($"{MovesDatabaseFolder}/MoveDatabase.asset");
            if (moveDb == null || moveDb.moves == null || moveDb.moves.Count == 0)
            {
                _logOutput += "ERROR: MoveDatabase.asset not found or empty. Import moves first.\n";
                return;
            }

            var modCatalog = AssetDatabase.LoadAssetAtPath<ModifierCatalogSO>($"{ModifiersCatalogFolder}/ModifierCatalog.asset");
            var moveLookup = BuildMoveLookup(moveDb);

            var rng = new System.Random();
            var pool = new List<SpeciesDefinitionSO>();
            foreach (var s in speciesDb.species)
            {
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.speciesId)) continue;
                var learnable = s.learnableMoves != null ? s.learnableMoves : new List<MoveDefinitionSO>();
                var exclusive = s.exclusiveMoves != null ? s.exclusiveMoves : new List<MoveDefinitionSO>();
                if (learnable.Count + exclusive.Count < MovesPerChainling)
                    continue;
                pool.Add(s);
            }

            if (pool.Count == 0)
            {
                _logOutput += "ERROR: No species with at least 4 moves in pool. Assign move pools to species first.\n";
                return;
            }

            var serialized = new SerializedObject(provider);
            var rosterProp = serialized.FindProperty("roster");
            if (rosterProp == null || !rosterProp.isArray)
            {
                _logOutput += "ERROR: Provider does not have a 'roster' array.\n";
                return;
            }

            rosterProp.arraySize = 0;
            int n = Mathf.Clamp(_randomRosterSize, 1, CombatConstants.ROSTER_SIZE);

            for (int i = 0; i < n; i++)
            {
                var speciesDef = pool[rng.Next(pool.Count)];
                var allMoves = new List<MoveDefinitionSO>();
                if (speciesDef.exclusiveMoves != null)
                    foreach (var m in speciesDef.exclusiveMoves)
                        if (m != null) allMoves.Add(m);
                if (speciesDef.learnableMoves != null)
                    foreach (var m in speciesDef.learnableMoves)
                        if (m != null && !allMoves.Contains(m)) allMoves.Add(m);

                if (allMoves.Count < MovesPerChainling)
                {
                    _logOutput += $"  WARN: {speciesDef.speciesId} has only {allMoves.Count} moves, skipping.\n";
                    continue;
                }

                var selected = new List<MoveDefinitionSO>();
                var indices = new List<int>();
                for (int j = 0; j < allMoves.Count; j++) indices.Add(j);
                for (int k = 0; k < MovesPerChainling && indices.Count > 0; k++)
                {
                    int idx = rng.Next(indices.Count);
                    selected.Add(allMoves[indices[idx]]);
                    indices.RemoveAt(idx);
                }

                rosterProp.InsertArrayElementAtIndex(rosterProp.arraySize);
                var entry = rosterProp.GetArrayElementAtIndex(rosterProp.arraySize - 1);
                entry.FindPropertyRelative("speciesId").stringValue = speciesDef.speciesId;
                entry.FindPropertyRelative("displayName").stringValue = "";
                entry.FindPropertyRelative("stars").intValue = 3;
                entry.FindPropertyRelative("rebirthCount").intValue = 0;
                entry.FindPropertyRelative("evolutionStage").intValue = 1;

                var moveIdsProp = entry.FindPropertyRelative("moveIds");
                var mod1Prop = entry.FindPropertyRelative("modifier1SectionIds");
                var mod2Prop = entry.FindPropertyRelative("modifier2SectionIds");
                moveIdsProp.arraySize = selected.Count;
                mod1Prop.arraySize = selected.Count;
                mod2Prop.arraySize = selected.Count;

                for (int m = 0; m < selected.Count; m++)
                {
                    var moveDef = selected[m];
                    string mid = moveDef.moveId;
                    moveIdsProp.GetArrayElementAtIndex(m).stringValue = mid;

                    string picked1 = "";
                    string picked2 = "";
                    if (modCatalog != null && modCatalog.modifiers != null && modCatalog.modifiers.Count > 0 && _modifierDensity >= 1)
                    {
                        var pool1 = new List<ModifierDefinitionSO>();
                        foreach (var mod in modCatalog.modifiers)
                        {
                            if (mod == null) continue;
                            var check = ModifierValidationService.CanApply(mod, moveDef, modCatalog, Array.Empty<string>());
                            if (check.isValid) pool1.Add(mod);
                        }
                        if (pool1.Count > 0)
                            picked1 = pool1[rng.Next(pool1.Count)].sectionId;
                    }
                    if (modCatalog != null && modCatalog.modifiers != null && _modifierDensity >= 2 && !string.IsNullOrEmpty(picked1))
                    {
                        var existing = new[] { picked1 };
                        var pool2 = new List<ModifierDefinitionSO>();
                        foreach (var mod in modCatalog.modifiers)
                        {
                            if (mod == null || string.Equals(mod.sectionId, picked1, StringComparison.OrdinalIgnoreCase))
                                continue;
                            var check = ModifierValidationService.CanApply(mod, moveDef, modCatalog, existing);
                            if (check.isValid) pool2.Add(mod);
                        }
                        if (pool2.Count > 0)
                            picked2 = pool2[rng.Next(pool2.Count)].sectionId;
                    }

                    mod1Prop.GetArrayElementAtIndex(m).stringValue = picked1;
                    mod2Prop.GetArrayElementAtIndex(m).stringValue = picked2;
                }

                _logOutput += $"  Slot {i}: {speciesDef.speciesId} with {selected.Count} moves\n";
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(provider);
            AssetDatabase.SaveAssets();
            _logOutput += $"\nRandom lineup generated ({rosterProp.arraySize} chainlings).\n";
        }

        private void GenerateRandomMatchup()
        {
            _logOutput = "Generating Random Matchup...\n\n";

            if (_partyProvider == null)
            {
                _logOutput += "ERROR: Party Provider not set.\n";
                return;
            }
            if (_encounterProvider == null)
            {
                _logOutput += "ERROR: Encounter Provider not set.\n";
                return;
            }

            var speciesDb = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>($"{ChainlingSpeciesDatabaseFolder}/SpeciesDatabase.asset");
            if (speciesDb == null || speciesDb.species == null || speciesDb.species.Count == 0)
            {
                _logOutput += "ERROR: SpeciesDatabase.asset not found or empty.\n";
                return;
            }

            var pool = new List<SpeciesDefinitionSO>();
            foreach (var s in speciesDb.species)
            {
                if (s == null || string.IsNullOrEmpty(s.speciesId)) continue;
                var learnable = s.learnableMoves != null ? s.learnableMoves : new List<MoveDefinitionSO>();
                var exclusive = s.exclusiveMoves != null ? s.exclusiveMoves : new List<MoveDefinitionSO>();
                if (learnable.Count + exclusive.Count >= MovesPerChainling)
                    pool.Add(s);
            }

            if (pool.Count < _randomRosterSize * 2)
            {
                _logOutput += "WARN: Few species available; encounter may overlap with party.\n";
            }

            GenerateRandomLineup(_partyProvider);
            var partyLog = _logOutput;
            GenerateRandomLineup(_encounterProvider);
            _logOutput = partyLog + "\n---\n\n" + _logOutput + "\nRandom matchup complete.\n";
        }
    }
}
