using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Chainbound.Combat.Core;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Exports deterministic, versioned gameplay data consumed by the .NET battle service.
    /// The raw UTF-8 gameplay file is the manifest hash input; clients and the service compare it byte-for-byte.
    /// </summary>
    public static class AuthoritativeGameplayManifestExporter
    {
        private const string CaptureRoot = "Assets/Data/Combat/Capture";
        private const string ProfilePath = CaptureRoot + "/CaptureProfileCatalog.asset";
        private const string BalancePath = CaptureRoot + "/CaptureBalanceConfig.asset";
        private const string PresentationPath = CaptureRoot + "/CapturePresentationConfig.asset";
        private const string AuthorityPath = CaptureRoot + "/AuthoritativeCombatServiceConfig.asset";
        private const string RewardsConfigPath = "Assets/Data/Combat/Config/PostBattleRewardsConfig_Default.asset";
        private const string ExportRoot = "backend/Chainbound.BattleService/manifests";
        private const string GameplayPath = ExportRoot + "/gameplay-manifest.json";
        private const string CapturePath = ExportRoot + "/capture-balance.json";
        private const string RewardsPath = ExportRoot + "/battle-rewards.json";

        [InitializeOnLoadMethod]
        private static void EnsureLeverBoardsAfterImport()
        {
            EditorApplication.delayCall += TryEnsureLeverBoards;
        }

        private static void TryEnsureLeverBoards()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryEnsureLeverBoards;
                return;
            }

            bool boardsMissing = AssetDatabase.LoadAssetAtPath<CaptureBalanceConfigSO>(BalancePath) == null ||
                AssetDatabase.LoadAssetAtPath<CapturePresentationConfigSO>(PresentationPath) == null ||
                AssetDatabase.LoadAssetAtPath<AuthoritativeCombatServiceConfigSO>(AuthorityPath) == null;
            if (boardsMissing)
                EnsureDefaultLeverBoards();

            bool manifestPlaceholder = !File.Exists(GameplayPath) ||
                File.ReadAllText(GameplayPath).Contains("\"encounters\": []");
            bool capturePlaceholder = !File.Exists(CapturePath) ||
                File.ReadAllText(CapturePath).Contains("\"configHash\": \"\"");
            bool rewardsMissing = !File.Exists(RewardsPath);
            if (manifestPlaceholder || capturePlaceholder || rewardsMissing)
            {
                try
                {
                    ExportAll();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[AuthoritativeGameplayManifestExporter] Initial export is not ready; " +
                        "the production authority flag remains disabled. " + exception.Message);
                }
            }
        }

        [MenuItem("Tools/Chainbound/Capture/Ensure Default Lever Boards", priority = 510)]
        public static void EnsureDefaultLeverBoards()
        {
            EnsureFolder(CaptureRoot);

            CaptureProfileCatalogSO profiles = AssetDatabase.LoadAssetAtPath<CaptureProfileCatalogSO>(ProfilePath);
            if (profiles == null)
            {
                profiles = ScriptableObject.CreateInstance<CaptureProfileCatalogSO>();
                profiles.ResetToDefaults();
                AssetDatabase.CreateAsset(profiles, ProfilePath);
            }

            CaptureBalanceConfigSO balance = AssetDatabase.LoadAssetAtPath<CaptureBalanceConfigSO>(BalancePath);
            if (balance == null)
            {
                balance = ScriptableObject.CreateInstance<CaptureBalanceConfigSO>();
                balance.profileCatalog = profiles;
                AssetDatabase.CreateAsset(balance, BalancePath);
            }
            else if (balance.profileCatalog == null)
            {
                balance.profileCatalog = profiles;
                EditorUtility.SetDirty(balance);
            }

            if (AssetDatabase.LoadAssetAtPath<CapturePresentationConfigSO>(PresentationPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CapturePresentationConfigSO>(), PresentationPath);
            if (AssetDatabase.LoadAssetAtPath<AuthoritativeCombatServiceConfigSO>(AuthorityPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AuthoritativeCombatServiceConfigSO>(), AuthorityPath);

            WireGameServices(balance, AssetDatabase.LoadAssetAtPath<CapturePresentationConfigSO>(PresentationPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Chainbound/Capture/Export Authoritative Manifests", priority = 511)]
        public static void ExportAll()
        {
            EnsureDefaultLeverBoards();
            CaptureBalanceConfigSO balance = AssetDatabase.LoadAssetAtPath<CaptureBalanceConfigSO>(BalancePath);
            if (balance == null)
                throw new InvalidOperationException("Capture balance board could not be created.");
            PostBattleRewardsConfigSO rewards = AssetDatabase.LoadAssetAtPath<PostBattleRewardsConfigSO>(RewardsConfigPath);
            if (rewards == null)
                throw new InvalidOperationException($"Post-battle rewards board is missing at {RewardsConfigPath}.");

            GameplayManifestDto gameplay = BuildGameplayManifest();
            if (gameplay.encounters.Count == 0)
                throw new InvalidOperationException("No authoritative encounters were exported. Keep the rollout flag disabled.");

            Directory.CreateDirectory(ExportRoot);
            WriteUtf8(GameplayPath, JsonUtility.ToJson(gameplay, true) + "\n");
            string manifestHash = HashCanonicalJson(File.ReadAllText(GameplayPath, Encoding.UTF8));

            CaptureBalanceManifestDto capture = BuildCaptureManifest(balance);
            WriteUtf8(CapturePath, JsonUtility.ToJson(capture, true) + "\n");

            BattleRewardsManifestDto rewardManifest = BuildRewardsManifest(rewards);
            WriteUtf8(RewardsPath, JsonUtility.ToJson(rewardManifest, true) + "\n");
            string rewardsHash = HashCanonicalJson(File.ReadAllText(RewardsPath, Encoding.UTF8));

            string[] authorityAssets = AssetDatabase.FindAssets("t:AuthoritativeCombatServiceConfigSO");
            for (int i = 0; i < authorityAssets.Length; i++)
            {
                AuthoritativeCombatServiceConfigSO config = AssetDatabase.LoadAssetAtPath<AuthoritativeCombatServiceConfigSO>(AssetDatabase.GUIDToAssetPath(authorityAssets[i]));
                if (config == null)
                    continue;
                config.gameplayManifestHash = manifestHash;
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AuthoritativeGameplayManifestExporter] Exported {gameplay.encounters.Count} encounters; manifest={manifestHash}; capture={capture.configHash}; rewards={rewardsHash}.");
        }

        private static GameplayManifestDto BuildGameplayManifest()
        {
            var manifest = new GameplayManifestDto();
            Dictionary<string, SpeciesDefinitionSO> species = LoadAssets<SpeciesDefinitionSO>()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.speciesId))
                .GroupBy(x => x.speciesId.Trim(), StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            foreach (SpeciesDefinitionSO value in species.Values.OrderBy(x => x.speciesId, StringComparer.Ordinal))
                manifest.species.Add(ToSpecies(value));
            foreach (MoveDefinitionSO value in LoadAssets<MoveDefinitionSO>().Where(x => x != null && !string.IsNullOrWhiteSpace(x.moveId)).OrderBy(x => x.moveId, StringComparer.Ordinal))
                manifest.moves.Add(ToMove(value));
            foreach (StatusDefinitionSO value in LoadAssets<StatusDefinitionSO>().Where(x => x != null).OrderBy(x => (int)x.statusId))
                manifest.statuses.Add(ToStatus(value));
            var exportedStatusIds = new HashSet<StatusId>(manifest.statuses.Select(x => (StatusId)x.numericId));
            foreach (StatusId statusId in Enum.GetValues(typeof(StatusId)).Cast<StatusId>().OrderBy(x => (int)x))
            {
                if (statusId == StatusId.None || exportedStatusIds.Contains(statusId))
                    continue;
                manifest.statuses.Add(new StatusDto
                {
                    statusId = statusId.ToString(),
                    numericId = (int)statusId,
                    displayName = statusId.ToString(),
                    effectSummary = "No authored StatusDefinitionSO; reserved runtime identifier.",
                });
            }
            manifest.statuses = manifest.statuses.OrderBy(x => x.numericId).ToList();
            foreach (CombatRulesetSO value in LoadAssets<CombatRulesetSO>().Where(x => x != null && !string.IsNullOrWhiteSpace(x.rulesetId)).OrderBy(x => x.rulesetId, StringComparer.Ordinal))
                manifest.rulesets.Add(ToRuleset(value));

            foreach (EncounterZoneSO zone in LoadAssets<EncounterZoneSO>().Where(x => x != null && !string.IsNullOrWhiteSpace(x.zoneId)).OrderBy(x => x.zoneId, StringComparer.Ordinal))
            {
                List<ZoneEncounterEntry> entries = ResolveZoneEntries(zone);
                foreach (TimeSegment segment in Enum.GetValues(typeof(TimeSegment)).Cast<TimeSegment>())
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        ZoneEncounterEntry entry = entries[i];
                        float segmentWeight = entry != null && entry.IsSegmentAllowed(segment)
                            ? Mathf.Max(0f, entry.weight * entry.GetSegmentWeightMultiplier(segment))
                            : 0f;
                        if (entry == null || segmentWeight <= 0f || !species.TryGetValue(entry.speciesId ?? "", out SpeciesDefinitionSO definition))
                            continue;
                        manifest.encounters.Add(ToZoneEncounter(zone, entry, definition, segment, segmentWeight));
                    }
                }
            }

            foreach (ManualEncounterProviderSO provider in LoadAssets<ManualEncounterProviderSO>().Where(x => x != null).OrderBy(x => x.AuthoritativeEncounterId, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(provider.AuthoritativeEncounterId))
                    continue;
                manifest.encounters.Add(ToManualEncounter(provider, species));
            }
            manifest.encounters = manifest.encounters.OrderBy(x => x.encounterId, StringComparer.Ordinal).ToList();
            return manifest;
        }

        private static ManifestEncounterDto ToZoneEncounter(
            EncounterZoneSO zone,
            ZoneEncounterEntry entry,
            SpeciesDefinitionSO species,
            TimeSegment segment,
            float resolvedWeight)
        {
            EncounterDifficulty difficulty = zone.inheritDifficulty && zone.area != null ? zone.area.difficulty : zone.difficulty;
            bool boss = difficulty == EncounterDifficulty.Boss;
            string moveId = ResolveOrdinalMove(entry, zone, species);
            int ivMin = zone.ivMinOverride != int.MinValue
                ? zone.ivMinOverride
                : zone.area != null && zone.area.wildTraits != null ? zone.area.wildTraits.ivMin : CombatConstants.IV_MIN;
            int ivMax = zone.ivMaxOverride != int.MinValue
                ? zone.ivMaxOverride
                : zone.area != null && zone.area.wildTraits != null ? zone.area.wildTraits.ivMax : CombatConstants.IV_MAX;
            if (ivMax < ivMin)
                ivMax = ivMin;
            string segmentId = segment.ToString().ToLowerInvariant();
            var target = new ManifestTargetDto
            {
                targetId = $"{zone.zoneId}:{segmentId}:{entry.speciesId}", speciesId = species.speciesId, displayName = species.displayName,
                difficultyId = difficulty.ToString().ToLowerInvariant(), maxHp = Mathf.Max(1, species.baseMaxHP),
                currentHp = Mathf.Max(1, species.baseMaxHP), legacyBaseCaptureRate = entry.baseCaptureRate,
                captureProfile = (int)species.defaultCaptureProfile,
                stars = entry.baseStars, starVariance = entry.starVariance, level = 1,
                rollIvs = true, ivMin = ivMin, ivMax = ivMax,
                knownMoveId = moveId, startsActive = true,
            };
            return new ManifestEncounterDto
            {
                encounterId = $"zone:{zone.zoneId}:{segmentId}:{entry.speciesId}", zoneId = zone.zoneId,
                timeSegment = segmentId,
                battleKind = boss ? (int)AuthoritativeBattleKind.Boss : (int)AuthoritativeBattleKind.Wild,
                rewardTier = ResolveRewardTier(difficulty),
                captureEnabled = !boss, capturePolicy = (int)EncounterCapturePolicy.UseEncounterDefault,
                weight = Mathf.Max(1, Mathf.RoundToInt(resolvedWeight * 1000f)), targets = new List<ManifestTargetDto> { target },
            };
        }

        private static ManifestEncounterDto ToManualEncounter(ManualEncounterProviderSO provider, Dictionary<string, SpeciesDefinitionSO> species)
        {
            EncounterSnapshot source = provider.GenerateEncounter(0x514A4D);
            var result = new ManifestEncounterDto
            {
                encounterId = provider.AuthoritativeEncounterId.Trim(), zoneId = "",
                battleKind = (int)source.authoritativeBattleKind, captureEnabled = source.captureEnabled,
                capturePolicy = (int)source.capturePolicy, rewardTier = ResolveRewardTier(source.difficulty), weight = 1,
                rewardEligibilityQuestId = provider.RewardEligibilityQuestId?.Trim() ?? "",
            };
            for (int i = 0; i < source.chainlings.Count; i++)
            {
                ChainlingSnapshot chainling = source.chainlings[i];
                if (chainling == null || string.IsNullOrWhiteSpace(chainling.speciesId))
                    continue;
                species.TryGetValue(chainling.speciesId, out SpeciesDefinitionSO definition);
                int maxHp = Mathf.Max(1, (definition != null ? definition.baseMaxHP : 1) + ReadArray(chainling.ivs, 0));
                result.targets.Add(new ManifestTargetDto
                {
                    targetId = $"{result.encounterId}:{i}", speciesId = chainling.speciesId,
                    displayName = string.IsNullOrWhiteSpace(chainling.displayName) ? definition != null ? definition.displayName : chainling.speciesId : chainling.displayName,
                    visualVariantId = chainling.visualIdentity != null ? chainling.visualIdentity.visualVariantId : "base",
                    mutationId = chainling.mutationId ?? "none", mutated = chainling.isMutated,
                    entangled = chainling.isEntangled, entangledTypeId = chainling.entangledType.ToString().ToLowerInvariant(),
                    difficultyId = source.difficulty.ToString().ToLowerInvariant(), currentHp = chainling.currentHp < 0 ? maxHp : Mathf.Clamp(chainling.currentHp, 1, maxHp),
                    maxHp = maxHp, legacyBaseCaptureRate = chainling.legacyBaseCaptureRate,
                    captureProfile = chainling.hasCaptureProfile
                        ? (int)chainling.captureProfile
                        : definition != null ? (int)definition.defaultCaptureProfile : (int)CaptureVolatilityProfile.Standard, stars = chainling.stars,
                    level = Mathf.Max(1, chainling.level), ivs = CopyArray(chainling.ivs),
                    knownMoveId = !string.IsNullOrWhiteSpace(chainling.knownMoveId) ? chainling.knownMoveId : FirstMove(chainling.moveIds),
                    startsActive = i < CombatConstants.ACTIVE_PER_TEAM,
                });
            }
            return result;
        }

        private static List<ZoneEncounterEntry> ResolveZoneEntries(EncounterZoneSO zone)
        {
            var map = new Dictionary<string, ZoneEncounterEntry>(StringComparer.Ordinal);
            if (zone.entryMergeMode != EncounterEntryMergeMode.Replace && zone.area != null && zone.area.entries != null)
                for (int i = 0; i < zone.area.entries.Count; i++)
                    if (zone.area.entries[i] != null && !string.IsNullOrWhiteSpace(zone.area.entries[i].speciesId))
                        map[zone.area.entries[i].speciesId] = zone.area.entries[i];
            if (zone.entries != null)
                for (int i = 0; i < zone.entries.Count; i++)
                    if (zone.entries[i] != null && !string.IsNullOrWhiteSpace(zone.entries[i].speciesId))
                        map[zone.entries[i].speciesId] = zone.entries[i];
            return map.Values.OrderBy(x => x.speciesId, StringComparer.Ordinal).ToList();
        }

        private static string ResolveOrdinalMove(ZoneEncounterEntry entry, EncounterZoneSO zone, SpeciesDefinitionSO species)
        {
            IEnumerable<WeightedMoveEntry> moves = entry.movePool != null ? entry.movePool.entries : entry.inlineMovePool;
            WeightedMoveEntry first = moves != null ? moves.Where(x => x != null && x.weight > 0 && !string.IsNullOrWhiteSpace(x.moveId)).OrderBy(x => x.moveId, StringComparer.Ordinal).FirstOrDefault() : null;
            if (first != null) return first.moveId;
            WildMovePoolSO fallback = zone.defaultMovePool != null ? zone.defaultMovePool : zone.area != null ? zone.area.defaultMovePool : null;
            if (fallback != null)
            {
                first = fallback.entries.Where(x => x != null && x.weight > 0 && !string.IsNullOrWhiteSpace(x.moveId)).OrderBy(x => x.moveId, StringComparer.Ordinal).FirstOrDefault();
                if (first != null) return first.moveId;
            }
            if (species.initialMoves != null && species.initialMoves.Count > 0 && species.initialMoves[0] != null)
                return species.initialMoves[0].moveId;
            return "baseline_jab";
        }

        private static CaptureBalanceManifestDto BuildCaptureManifest(CaptureBalanceConfigSO source)
        {
            CaptureBalanceData data = source.ToCoreData();
            var dto = new CaptureBalanceManifestDto { configVersion = source.configVersion, configHash = source.ComputeConfigHash() };
            dto.balance.CopyFrom(data);
            string[] types = { "node", "gas", "block", "encrypted", "burned", "frozen", "forked", "mined", "staked", "compressed", "bot", "glitched", "shard" };
            for (int i = 0; i < types.Length; i++) dto.entangledTypes.Add(new WeightedTypeDto { typeId = types[i], weight = 1 });
            return dto;
        }

        private static BattleRewardsManifestDto BuildRewardsManifest(PostBattleRewardsConfigSO source)
        {
            return new BattleRewardsManifestDto
            {
                schemaVersion = 1,
                configVersion = Mathf.Max(1, source.configVersion),
                rewards = new BattleRewardValuesDto
                {
                    standard = Mathf.Max(0, source.ramStandard),
                    elite = Mathf.Max(0, source.ramElite),
                    boss = Mathf.Max(0, source.ramBoss),
                },
            };
        }

        private static string ResolveRewardTier(EncounterDifficulty difficulty)
        {
            switch (difficulty)
            {
                case EncounterDifficulty.Elite:
                    return BattleRewardTier.Elite;
                case EncounterDifficulty.Boss:
                    return BattleRewardTier.Boss;
                default:
                    return BattleRewardTier.Standard;
            }
        }

        private static SpeciesDto ToSpecies(SpeciesDefinitionSO value) => new SpeciesDto
        {
            speciesId = value.speciesId, displayName = value.displayName, primaryType = (int)value.primaryType, secondaryType = (int)value.secondaryType,
            baseMaxHp = value.baseMaxHP, baseMeleeAttack = value.baseMeleeAtk, baseMeleeDefense = value.baseMeleeDef,
            baseRangedAttack = value.baseRangedAtk, baseRangedDefense = value.baseRangedDef, baseSpeed = value.baseSpeed,
            initialMoveIds = value.initialMoves != null ? value.initialMoves.Where(x => x != null).Select(x => x.moveId).OrderBy(x => x, StringComparer.Ordinal).ToList() : new List<string>(),
        };

        private static MoveDto ToMove(MoveDefinitionSO value)
        {
            MoveResolveProfile profile = value.resolveProfile ?? new MoveResolveProfile();
            MoveDamageFormulaTuning damage = profile.damage ?? new MoveDamageFormulaTuning();
            MoveConditionalDamageTuning conditional = profile.conditionalDamage ?? new MoveConditionalDamageTuning();
            MoveAccuracyTuning accuracy = profile.accuracy ?? new MoveAccuracyTuning();
            var dto = new MoveDto
            {
                moveId = value.moveId, displayName = value.displayName, category = (int)value.category, baseType = (int)value.baseType,
                rarity = (int)value.rarity, targeting = (int)value.targeting, contactType = (int)value.contactType, power = value.power,
                apCost = value.apCost, accuracy = value.accuracy, hitCountMin = value.hitCountMin, hitCountMax = value.hitCountMax,
                rechargeTurns = value.rechargeTurns, tags = value.tags != null ? value.tags.OrderBy(x => x, StringComparer.Ordinal).ToList() : new List<string>(),
                stageChanges = CombatCsvRoundTrip.FormatStageChangesPublic(value.stageChanges),
                utilityEffects = CombatCsvRoundTrip.FormatUtilityEffectsPublic(value.utilityEffects),
                hpCostPercent = value.hpCostPercent,
                healPercent = value.healPercentMaxHP,
                bossOnly = MoveAvailability.IsBossOnly(value),
                spendsRemainingAp = HasTag(value.tags, "SpendRemainingAp"),
                moveEffectsOncePerTarget = profile.moveEffectsOncePerTarget,
                accuracyTuning = new AccuracyTuningDto
                {
                    forceHit = accuracy.forceHit,
                    forceMiss = accuracy.forceMiss,
                    multiplier = accuracy.accuracyMultiplier,
                    bonusPercent = accuracy.accuracyBonusPercent,
                    ignoreEvasionStages = accuracy.ignoreEvasionStages,
                    minHitChance = accuracy.minHitChance,
                    maxHitChance = accuracy.maxHitChance,
                },
                resolveProfile = new ResolveProfileDto
                {
                    primaryTiming = profile.primaryTiming.ToString(),
                    utilityTiming = profile.utilityExecutionTiming.ToString(),
                    conditionalPolicy = profile.conditionalEffectPolicy.ToString(),
                    conditions = CombatCsvRoundTrip.FormatResolveConditionsPublic(profile.resolveConditions),
                    requireAll = profile.requireAllResolveConditions,
                    damagePowerMultiplier = damage.powerMultiplier,
                    damagePreMitigationMultiplier = damage.preMitigationDamageMultiplier,
                    damageFinalMultiplier = damage.finalDamageMultiplier,
                    damageOverrideAttackStat = damage.overrideAttackStat,
                    damageAttackStat = damage.attackStatOverride.ToString(),
                    conditionalDamageMultiplier = conditional.multiplier,
                    conditionalDamageConditions = CombatCsvRoundTrip.FormatResolveConditionsPublic(conditional.conditions),
                    conditionalDamageRequireAll = conditional.requireAllConditions,
                    conditionalDamageScalingCondition = conditional.useScalingCondition
                        ? CombatCsvRoundTrip.FormatResolveConditionPublic(conditional.scalingCondition)
                        : string.Empty,
                    conditionalDamageBonusPerCount = conditional.bonusMultiplierPerCount,
                    conditionalDamageMaxMultiplier = conditional.maxMultiplier,
                    randomDamageMultipliers = CombatCsvRoundTrip.FormatFloatListPublic(conditional.randomMultipliers),
                },
            };
            if (value.statusEffects != null)
                for (int i = 0; i < value.statusEffects.Count; i++)
                    dto.statusEffects.Add(new MoveStatusDto { statusId = value.statusEffects[i].statusId.ToString(), duration = value.statusEffects[i].duration, chanceBasisPoints = Mathf.RoundToInt(value.statusEffects[i].chance * 10000f) });
            return dto;
        }

        private static StatusDto ToStatus(StatusDefinitionSO value) => new StatusDto
        {
            statusId = value.statusId.ToString(), numericId = (int)value.statusId, displayName = value.displayName,
            effectSummary = value.effectSummary, statusClass = (int)value.statusClass,
            origin = (int)value.origin, defaultDuration = value.defaultDuration, counterThreshold = value.counterThreshold,
            isBuff = value.isBuff, isDebuff = value.isDebuff,
            preventsAction = value.preventsAction, endOfTurnDamageBasisPoints = Mathf.RoundToInt(value.endOfTurnDamagePercent * 10000f),
            endOfTurnApChange = value.endOfTurnAPChange,
        };

        private static bool HasTag(IReadOnlyList<string> tags, string expected)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static RulesetDto ToRuleset(CombatRulesetSO value) => new RulesetDto
        {
            rulesetId = value.rulesetId, displayName = value.displayName, configVersion = value.configVersion,
            allowedTypes = value.allowedTypes != null ? value.allowedTypes.Select(x => (int)x).OrderBy(x => x).ToList() : new List<int>(),
            requiredMoveIds = value.requiredMoveIds != null ? value.requiredMoveIds.OrderBy(x => x, StringComparer.Ordinal).ToList() : new List<string>(),
            bannedMoveIds = value.bannedMoveIds != null ? value.bannedMoveIds.OrderBy(x => x, StringComparer.Ordinal).ToList() : new List<string>(),
        };

        private static List<T> LoadAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var result = new List<T>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        private static void WireGameServices(CaptureBalanceConfigSO balance, CapturePresentationConfigSO presentation)
        {
            string[] prefabs = AssetDatabase.FindAssets("GameServices t:Prefab");
            for (int p = 0; p < prefabs.Length; p++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabs[p]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    foreach (BalanceConfigService service in root.GetComponentsInChildren<BalanceConfigService>(true))
                    {
                        var serialized = new SerializedObject(service);
                        serialized.FindProperty("captureBalanceConfig").objectReferenceValue = balance;
                        serialized.FindProperty("capturePresentationConfig").objectReferenceValue = presentation;
                        changed |= serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour == null || behaviour.GetType().Name != "SolanaServiceBridge") continue;
                        var serialized = new SerializedObject(behaviour);
                        SerializedProperty property = serialized.FindProperty("authoritativeCombatConfig");
                        if (property == null) continue;
                        property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AuthoritativeCombatServiceConfigSO>(AuthorityPath);
                        changed |= serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WriteUtf8(string path, string value) => File.WriteAllText(path, value, new UTF8Encoding(false));
        /// <summary>
        /// Mirrors Chainbound.BattleService CanonicalJsonHash (System.Text.Json): object keys sorted ordinally,
        /// no whitespace, numbers kept exactly as written (never re-parsed as doubles), strings re-escaped with
        /// System.Text.Json's default JavaScriptEncoder rules. Hash = lowercase SHA-256 of the UTF-8 canonical text.
        /// </summary>
        public static string HashCanonicalJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            int index = 0;
            if (json.Length > 0 && json[0] == '\uFEFF') index = 1;
            var builder = new StringBuilder(json.Length);
            SkipJsonWhitespace(json, ref index);
            WriteCanonicalValue(json, ref index, builder);
            SkipJsonWhitespace(json, ref index);
            if (index != json.Length) throw new FormatException($"Unexpected trailing JSON content at {index}.");
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static void WriteCanonicalValue(string json, ref int index, StringBuilder output)
        {
            if (index >= json.Length) throw new FormatException("Unexpected end of JSON.");
            char c = json[index];
            if (c == '{')
            {
                index++;
                var members = new List<KeyValuePair<string, string>>();
                SkipJsonWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}') { index++; }
                else
                {
                    while (true)
                    {
                        SkipJsonWhitespace(json, ref index);
                        string key = ReadJsonString(json, ref index);
                        SkipJsonWhitespace(json, ref index);
                        ExpectJsonChar(json, ref index, ':');
                        SkipJsonWhitespace(json, ref index);
                        var value = new StringBuilder();
                        WriteCanonicalValue(json, ref index, value);
                        members.Add(new KeyValuePair<string, string>(key, value.ToString()));
                        SkipJsonWhitespace(json, ref index);
                        if (index < json.Length && json[index] == ',') { index++; continue; }
                        ExpectJsonChar(json, ref index, '}');
                        break;
                    }
                }
                output.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, string> member in members.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (!first) output.Append(',');
                    first = false;
                    AppendServiceEscapedString(output, member.Key);
                    output.Append(':').Append(member.Value);
                }
                output.Append('}');
                return;
            }
            if (c == '[')
            {
                index++;
                output.Append('[');
                SkipJsonWhitespace(json, ref index);
                if (index < json.Length && json[index] == ']') { index++; output.Append(']'); return; }
                bool first = true;
                while (true)
                {
                    SkipJsonWhitespace(json, ref index);
                    if (!first) output.Append(',');
                    first = false;
                    WriteCanonicalValue(json, ref index, output);
                    SkipJsonWhitespace(json, ref index);
                    if (index < json.Length && json[index] == ',') { index++; continue; }
                    ExpectJsonChar(json, ref index, ']');
                    break;
                }
                output.Append(']');
                return;
            }
            if (c == '"') { AppendServiceEscapedString(output, ReadJsonString(json, ref index)); return; }
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                int start = index;
                while (index < json.Length && "+-0123456789.eE".IndexOf(json[index]) >= 0) index++;
                output.Append(json, start, index - start); // number text exactly as written
                return;
            }
            foreach (string literal in new[] { "true", "false", "null" })
            {
                if (string.CompareOrdinal(json, index, literal, 0, literal.Length) == 0)
                {
                    index += literal.Length;
                    output.Append(literal);
                    return;
                }
            }
            throw new FormatException($"Unexpected JSON character '{c}' at {index}.");
        }

        private static string ReadJsonString(string json, ref int index)
        {
            ExpectJsonChar(json, ref index, '"');
            var value = new StringBuilder();
            while (true)
            {
                if (index >= json.Length) throw new FormatException("Unterminated JSON string.");
                char c = json[index++];
                if (c == '"') return value.ToString();
                if (c != '\\') { value.Append(c); continue; }
                if (index >= json.Length) throw new FormatException("Unterminated JSON escape.");
                char e = json[index++];
                switch (e)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        value.Append((char)Convert.ToInt32(json.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default: throw new FormatException($"Invalid JSON escape '\\{e}'.");
                }
            }
        }

        // System.Text.Json default encoder: only printable ASCII outside the HTML-sensitive set passes through.
        private static void AppendServiceEscapedString(StringBuilder output, string value)
        {
            output.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': output.Append("\\\\"); continue;
                    case '\n': output.Append("\\n"); continue;
                    case '\r': output.Append("\\r"); continue;
                    case '\t': output.Append("\\t"); continue;
                    case '\b': output.Append("\\b"); continue;
                    case '\f': output.Append("\\f"); continue;
                }
                bool passThrough = c >= 0x20 && c <= 0x7E && c != '"' && c != '<' && c != '>' && c != '&' && c != '\'' && c != '+' && c != '`';
                if (passThrough) output.Append(c);
                else output.Append("\\u").Append(((int)c).ToString("X4"));
            }
            output.Append('"');
        }

        private static void SkipJsonWhitespace(string json, ref int index)
        {
            while (index < json.Length && (json[index] == ' ' || json[index] == '\t' || json[index] == '\n' || json[index] == '\r')) index++;
        }

        private static void ExpectJsonChar(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
                throw new FormatException($"Expected '{expected}' at {index}.");
            index++;
        }
        private static int ReadArray(int[] values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : 0;
        private static int[] CopyArray(int[] source) { var result = new int[6]; if (source != null) Array.Copy(source, result, Math.Min(6, source.Length)); return result; }
        private static string FirstMove(List<string> moves) => moves != null && moves.Count > 0 && !string.IsNullOrWhiteSpace(moves[0]) ? moves[0] : "baseline_jab";

        [Serializable] private sealed class GameplayManifestDto { public int manifestVersion = 3; public List<SpeciesDto> species = new List<SpeciesDto>(); public List<MoveDto> moves = new List<MoveDto>(); public List<StatusDto> statuses = new List<StatusDto>(); public List<RulesetDto> rulesets = new List<RulesetDto>(); public List<ManifestEncounterDto> encounters = new List<ManifestEncounterDto>(); }
        [Serializable] private sealed class SpeciesDto { public string speciesId; public string displayName; public int primaryType; public int secondaryType; public int baseMaxHp; public int baseMeleeAttack; public int baseMeleeDefense; public int baseRangedAttack; public int baseRangedDefense; public int baseSpeed; public List<string> initialMoveIds = new List<string>(); }
        [Serializable] private sealed class MoveDto { public string moveId; public string displayName; public int category; public int baseType; public int rarity; public int targeting; public int contactType; public int power; public int apCost; public int accuracy; public int hitCountMin; public int hitCountMax; public int rechargeTurns; public List<string> tags = new List<string>(); public List<MoveStatusDto> statusEffects = new List<MoveStatusDto>(); public string stageChanges; public string utilityEffects; public float hpCostPercent; public int healPercent; public ResolveProfileDto resolveProfile = new ResolveProfileDto(); public AccuracyTuningDto accuracyTuning = new AccuracyTuningDto(); public bool bossOnly; public bool spendsRemainingAp; public bool moveEffectsOncePerTarget; }
        [Serializable] private sealed class MoveStatusDto { public string statusId; public int duration; public int chanceBasisPoints; }
        [Serializable] private sealed class ResolveProfileDto { public string primaryTiming; public string utilityTiming; public string conditionalPolicy; public string conditions; public bool requireAll; public float damagePowerMultiplier; public float damagePreMitigationMultiplier; public float damageFinalMultiplier; public bool damageOverrideAttackStat; public string damageAttackStat; public float conditionalDamageMultiplier; public string conditionalDamageConditions; public bool conditionalDamageRequireAll; public string conditionalDamageScalingCondition; public float conditionalDamageBonusPerCount; public float conditionalDamageMaxMultiplier; public string randomDamageMultipliers; }
        [Serializable] private sealed class AccuracyTuningDto { public bool forceHit; public bool forceMiss; public float multiplier; public float bonusPercent; public int ignoreEvasionStages; public float minHitChance; public float maxHitChance; }
        [Serializable] private sealed class StatusDto { public string statusId; public int numericId; public string displayName; public string effectSummary; public int statusClass; public int origin; public int defaultDuration; public int counterThreshold; public bool isBuff; public bool isDebuff; public bool preventsAction; public int endOfTurnDamageBasisPoints; public int endOfTurnApChange; }
        [Serializable] private sealed class RulesetDto { public string rulesetId; public string displayName; public int configVersion; public List<int> allowedTypes = new List<int>(); public List<string> requiredMoveIds = new List<string>(); public List<string> bannedMoveIds = new List<string>(); }
        [Serializable] private sealed class ManifestEncounterDto { public string encounterId; public string zoneId; public string timeSegment = ""; public int battleKind; public string rewardTier = BattleRewardTier.Standard; public string rewardEligibilityQuestId = ""; public bool captureEnabled; public int capturePolicy; public int weight = 1; public List<ManifestTargetDto> targets = new List<ManifestTargetDto>(); }
        [Serializable] private sealed class ManifestTargetDto { public string targetId; public string speciesId; public string displayName; public string visualVariantId = "base"; public string mutationId = "none"; public bool mutated; public bool entangled; public string entangledTypeId = "none"; public string difficultyId = "standard"; public int currentHp = 1; public int maxHp = 1; public int legacyBaseCaptureRate = 45; public int captureProfile = 1; public int stars; public int starVariance; public int level = 1; public bool rollIvs; public int ivMin = CombatConstants.IV_MIN; public int ivMax = CombatConstants.IV_MAX; public int[] ivs = new int[6]; public string knownMoveId = "baseline_jab"; public bool startsActive = true; }
        [Serializable] private sealed class BattleRewardsManifestDto { public int schemaVersion = 1; public int configVersion = 1; public BattleRewardValuesDto rewards = new BattleRewardValuesDto(); }
        [Serializable] private sealed class BattleRewardValuesDto { public int standard; public int elite; public int boss; }
        [Serializable] private sealed class CaptureBalanceManifestDto { public int configVersion; public string configHash; public BalanceDto balance = new BalanceDto(); public List<WeightedTypeDto> entangledTypes = new List<WeightedTypeDto>(); }
        [Serializable] private sealed class WeightedTypeDto { public string typeId; public int weight; }
        [Serializable] private sealed class MotionProfileDto { public string profileId; public int difficulty; public int motionMultiplierBasisPoints; public int timerMultiplierBasisPoints; public int spikeChanceMultiplierBasisPoints; }
        [Serializable] private sealed class BalanceDto
        {
            public int tickRate, standardTimerMilliseconds, baseBandHeightBasisPoints, lockMovementBasisPointsPerSecond, escapeMovementBasisPointsPerSecond, lowHpThresholdBasisPoints, hpAboveThresholdFloorBasisPoints, hpResistanceCurveMilli, fullHpVelocityBonusBasisPoints, fullHpAccelerationBonusBasisPoints, fullHpTimerPenaltyBasisPoints, positiveIvMotionBonusMaximumBasisPoints, alertnessVelocityBasisPoints, alertnessAccelerationBasisPoints, alertnessSpikeAmplitudeBasisPoints, maximumAttemptMilliseconds, bandLiftAccelerationUnitsPerSecondSquared, bandGravityUnitsPerSecondSquared, bandDragBasisPointsPerSecond, bandMaximumSpeedUnitsPerSecond, bandEdgeBounceBasisPoints, traceAccelerationUnitsPerSecondSquared, traceMaximumSpeedUnitsPerSecond, traceMeanReversionUnitsPerSecondSquared, traceEdgeBounceBasisPoints, spikeChanceBasisPointsPerTick, spikeVelocityImpulseUnitsPerSecond, mutatedMotionBasisPoints, entangledMotionBasisPoints, traitSpikeChancePerMotionBasisPoints;
            public int[] variantRarityMotionBasisPoints;
            public int[] mutationRarityMotionBasisPoints;
            public List<MotionProfileDto> profiles = new List<MotionProfileDto>();
            public void CopyFrom(CaptureBalanceData value)
            {
                tickRate=value.TickRate; standardTimerMilliseconds=value.StandardTimerMilliseconds; baseBandHeightBasisPoints=value.BaseBandHeightBasisPoints; lockMovementBasisPointsPerSecond=value.LockMovementBasisPointsPerSecond; escapeMovementBasisPointsPerSecond=value.EscapeMovementBasisPointsPerSecond; lowHpThresholdBasisPoints=value.LowHpThresholdBasisPoints; hpAboveThresholdFloorBasisPoints=value.HpAboveThresholdFloorBasisPoints; hpResistanceCurveMilli=value.HpResistanceCurveMilli; fullHpVelocityBonusBasisPoints=value.FullHpVelocityBonusBasisPoints; fullHpAccelerationBonusBasisPoints=value.FullHpAccelerationBonusBasisPoints; fullHpTimerPenaltyBasisPoints=value.FullHpTimerPenaltyBasisPoints; positiveIvMotionBonusMaximumBasisPoints=value.PositiveIvMotionBonusMaximumBasisPoints; alertnessVelocityBasisPoints=value.AlertnessVelocityBasisPoints; alertnessAccelerationBasisPoints=value.AlertnessAccelerationBasisPoints; alertnessSpikeAmplitudeBasisPoints=value.AlertnessSpikeAmplitudeBasisPoints; maximumAttemptMilliseconds=value.MaximumAttemptMilliseconds; bandLiftAccelerationUnitsPerSecondSquared=value.BandLiftAccelerationUnitsPerSecondSquared; bandGravityUnitsPerSecondSquared=value.BandGravityUnitsPerSecondSquared; bandDragBasisPointsPerSecond=value.BandDragBasisPointsPerSecond; bandMaximumSpeedUnitsPerSecond=value.BandMaximumSpeedUnitsPerSecond; bandEdgeBounceBasisPoints=value.BandEdgeBounceBasisPoints; traceAccelerationUnitsPerSecondSquared=value.TraceAccelerationUnitsPerSecondSquared; traceMaximumSpeedUnitsPerSecond=value.TraceMaximumSpeedUnitsPerSecond; traceMeanReversionUnitsPerSecondSquared=value.TraceMeanReversionUnitsPerSecondSquared; traceEdgeBounceBasisPoints=value.TraceEdgeBounceBasisPoints; spikeChanceBasisPointsPerTick=value.SpikeChanceBasisPointsPerTick; spikeVelocityImpulseUnitsPerSecond=value.SpikeVelocityImpulseUnitsPerSecond; mutatedMotionBasisPoints=value.MutatedMotionBasisPoints; entangledMotionBasisPoints=value.EntangledMotionBasisPoints; traitSpikeChancePerMotionBasisPoints=value.TraitSpikeChancePerMotionBasisPoints;
                variantRarityMotionBasisPoints = value.VariantRarityMotionBasisPoints;
                mutationRarityMotionBasisPoints = value.MutationRarityMotionBasisPoints;
                profiles = value.Profiles.OrderBy(x => x.ProfileId, StringComparer.Ordinal).Select(x => new MotionProfileDto { profileId=x.ProfileId, difficulty=(int)x.Difficulty, motionMultiplierBasisPoints=x.MotionMultiplierBasisPoints, timerMultiplierBasisPoints=x.TimerMultiplierBasisPoints, spikeChanceMultiplierBasisPoints=x.SpikeChanceMultiplierBasisPoints }).ToList();
            }
        }
    }
}
