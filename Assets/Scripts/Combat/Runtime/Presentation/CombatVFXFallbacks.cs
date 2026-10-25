using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatVFXFallbacks", menuName = "Chainbound/Combat/Presentation/VFX/VFX Fallbacks")]
    public sealed class CombatVFXFallbacks : ScriptableObject
    {
        [Serializable]
        public sealed class TypedDamageRow
        {
            public ChainlingType type;
            public CombatVFXLibrary.VFXEntry melee;
            public CombatVFXLibrary.VFXEntry ranged;
        }

        [Serializable]
        public sealed class WallStateRow
        {
            public BlockKind kind;
            [Tooltip("None is the kind-level default used when an exact tier row is absent.")]
            public BlockTier tier;
            [Tooltip("Fallback wall art. Per-move wallPresentation fields take precedence.")]
            public MoveWallPresentation art;
            public CombatVFXLibrary.VFXPhaseConfig raiseVfx = new CombatVFXLibrary.VFXPhaseConfig();
            public CombatVFXLibrary.VFXPhaseConfig loopVfx = new CombatVFXLibrary.VFXPhaseConfig();
            public CombatVFXLibrary.VFXPhaseConfig hitVfx = new CombatVFXLibrary.VFXPhaseConfig();
            public CombatVFXLibrary.VFXPhaseConfig breakVfx = new CombatVFXLibrary.VFXPhaseConfig();
            public CombatVFXLibrary.VFXPhaseConfig expireVfx = new CombatVFXLibrary.VFXPhaseConfig();
        }

        [Header("Damage by Base Type")]
        [SerializeField] private TypedDamageRow[] typed = Array.Empty<TypedDamageRow>();

        [Header("Core Utility Roles")]
        [SerializeField] private CombatVFXLibrary.VFXEntry heal;
        [SerializeField] private CombatVFXLibrary.VFXEntry buff;
        [SerializeField] private CombatVFXLibrary.VFXEntry debuff;

        [Header("Specific Utility Roles (optional; fall through to core roles)")]
        [SerializeField] private CombatVFXLibrary.VFXEntry wall;
        [SerializeField] private CombatVFXLibrary.VFXEntry barrier;
        [SerializeField] private CombatVFXLibrary.VFXEntry armor;
        [SerializeField] private CombatVFXLibrary.VFXEntry apGain;
        [SerializeField] private CombatVFXLibrary.VFXEntry apDrain;
        [SerializeField] private CombatVFXLibrary.VFXEntry swap;

        [Header("Global Defaults")]
        [SerializeField] private CombatVFXLibrary.VFXEntry globalMelee;
        [SerializeField] private CombatVFXLibrary.VFXEntry globalRanged;
        [SerializeField] private CombatVFXLibrary.VFXEntry globalUtility;

        [Header("Wall State Profiles")]
        [Tooltip("Exact kind/tier rows plus a tier=None default per kind. Audio remains owned by CombatWallSfxCatalogSO.")]
        [SerializeField] private WallStateRow[] wallStates = Array.Empty<WallStateRow>();

        public IReadOnlyList<TypedDamageRow> TypedRows => typed;
        public IReadOnlyList<WallStateRow> WallStateRows => wallStates;
        public int Version => version;

        [NonSerialized] private int version;

        public void MarkChanged()
        {
            version++;
        }

        public WallStateRow ResolveWallState(BlockKind kind, BlockTier tier)
        {
            WallStateRow kindDefault = null;
            if (wallStates == null)
                return null;
            for (int i = 0; i < wallStates.Length; i++)
            {
                WallStateRow row = wallStates[i];
                if (row == null || row.kind != kind)
                    continue;
                if (row.tier == tier)
                    return row;
                if (row.tier == BlockTier.None && kindDefault == null)
                    kindDefault = row;
            }
            return kindDefault;
        }

        public MoveWallPresentation ResolveWallArt(
            BlockKind kind,
            BlockTier tier,
            MoveWallPresentation perMove)
        {
            WallStateRow row = ResolveWallState(kind, tier);
            if (row == null)
                return perMove;

            MoveWallPresentation result = perMove;
            if (result.wallSprite == null)
                result.wallSprite = row.art.wallSprite;
            if (result.wallMaterial == null)
                result.wallMaterial = row.art.wallMaterial;
            if (result.wallSpriteScale == Vector2.zero)
                result.wallSpriteScale = row.art.wallSpriteScale;
            if (result.wallTint.a <= 0f)
                result.wallTint = row.art.wallTint;
            return result;
        }

        public CombatVFXLibrary.VFXEntry GetTyped(ChainlingType type, MoveCategory category)
        {
            if (typed == null)
                return null;
            for (int i = 0; i < typed.Length; i++)
            {
                TypedDamageRow row = typed[i];
                if (row == null || row.type != type)
                    continue;
                return category == MoveCategory.DamagingRanged ? row.ranged : row.melee;
            }
            return null;
        }

        public CombatVFXLibrary.VFXEntry GetRole(MoveVfxRole role)
        {
            switch (role)
            {
                case MoveVfxRole.Heal: return heal;
                case MoveVfxRole.Buff: return buff;
                case MoveVfxRole.Debuff: return debuff;
                case MoveVfxRole.Wall: return wall;
                case MoveVfxRole.Barrier: return barrier;
                case MoveVfxRole.Armor: return armor;
                case MoveVfxRole.ApGain: return apGain;
                case MoveVfxRole.ApDrain: return apDrain;
                case MoveVfxRole.Swap: return swap;
                default: return null;
            }
        }

        public CombatVFXLibrary.VFXEntry GetGlobal(MoveCategory category)
        {
            if (category == MoveCategory.DamagingMelee)
                return globalMelee;
            if (category == MoveCategory.DamagingRanged)
                return globalRanged;
            return globalUtility;
        }

        public void SetTyped(ChainlingType type, MoveCategory category, CombatVFXLibrary.VFXEntry entry)
        {
            NormalizeTypedRows();
            TypedDamageRow row = typed[(int)type];
            if (category == MoveCategory.DamagingRanged)
                row.ranged = entry;
            else
                row.melee = entry;
        }

        public void SetRole(MoveVfxRole role, CombatVFXLibrary.VFXEntry entry)
        {
            switch (role)
            {
                case MoveVfxRole.Heal: heal = entry; break;
                case MoveVfxRole.Buff: buff = entry; break;
                case MoveVfxRole.Debuff: debuff = entry; break;
                case MoveVfxRole.Wall: wall = entry; break;
                case MoveVfxRole.Barrier: barrier = entry; break;
                case MoveVfxRole.Armor: armor = entry; break;
                case MoveVfxRole.ApGain: apGain = entry; break;
                case MoveVfxRole.ApDrain: apDrain = entry; break;
                case MoveVfxRole.Swap: swap = entry; break;
            }
        }

        public void SetGlobal(MoveCategory category, CombatVFXLibrary.VFXEntry entry)
        {
            if (category == MoveCategory.DamagingMelee)
                globalMelee = entry;
            else if (category == MoveCategory.DamagingRanged)
                globalRanged = entry;
            else
                globalUtility = entry;
        }

        public void NormalizeTypedRows()
        {
            Array values = Enum.GetValues(typeof(ChainlingType));
            var existing = new Dictionary<ChainlingType, TypedDamageRow>();
            if (typed != null)
            {
                for (int i = 0; i < typed.Length; i++)
                {
                    TypedDamageRow row = typed[i];
                    if (row != null && !existing.ContainsKey(row.type))
                        existing.Add(row.type, row);
                }
            }

            var normalized = new TypedDamageRow[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                ChainlingType type = (ChainlingType)values.GetValue(i);
                if (!existing.TryGetValue(type, out TypedDamageRow row))
                    row = new TypedDamageRow();
                row.type = type;
                normalized[i] = row;
            }
            typed = normalized;
        }

        public void NormalizeWallStateRows()
        {
            var keys = new[]
            {
                (BlockKind.Wall, BlockTier.None),
                (BlockKind.Wall, BlockTier.Lower),
                (BlockKind.Wall, BlockTier.Mid),
                (BlockKind.Wall, BlockTier.Greater),
                (BlockKind.Barrier, BlockTier.None),
                (BlockKind.Barrier, BlockTier.Lower),
                (BlockKind.Barrier, BlockTier.Mid),
            };
            var existing = new Dictionary<string, WallStateRow>(StringComparer.Ordinal);
            if (wallStates != null)
            {
                for (int i = 0; i < wallStates.Length; i++)
                {
                    WallStateRow row = wallStates[i];
                    if (row == null)
                        continue;
                    string key = $"{row.kind}:{row.tier}";
                    if (!existing.ContainsKey(key))
                        existing.Add(key, row);
                }
            }

            var normalized = new WallStateRow[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                string key = $"{keys[i].Item1}:{keys[i].Item2}";
                if (!existing.TryGetValue(key, out WallStateRow row))
                    row = new WallStateRow();
                row.kind = keys[i].Item1;
                row.tier = keys[i].Item2;
                normalized[i] = row;
            }
            wallStates = normalized;
        }

        public void CollectValidationWarnings(List<string> warnings)
        {
            if (warnings == null)
                return;
            if (typed != null)
            {
                for (int i = 0; i < typed.Length; i++)
                {
                    TypedDamageRow row = typed[i];
                    if (row == null) continue;
                    ValidateEntry(row.melee, $"{row.type} Melee", false, warnings);
                    ValidateEntry(row.ranged, $"{row.type} Ranged", true, warnings);
                }
            }

            ValidateEntry(globalMelee, "Global Melee", false, warnings);
            ValidateEntry(globalRanged, "Global Ranged", true, warnings);
            ValidateEntry(heal, "Heal", null, warnings);
            ValidateEntry(buff, "Buff", null, warnings);
            ValidateEntry(debuff, "Debuff", null, warnings);
            ValidateEntry(wall, "Wall", null, warnings);
            ValidateEntry(barrier, "Barrier", null, warnings);
            ValidateEntry(armor, "Armor", null, warnings);
            ValidateEntry(apGain, "AP Gain", null, warnings);
            ValidateEntry(apDrain, "AP Drain", null, warnings);
            ValidateEntry(swap, "Swap", null, warnings);
            ValidateEntry(globalUtility, "Global Utility", null, warnings);

            if (wallStates != null)
            {
                for (int i = 0; i < wallStates.Length; i++)
                {
                    WallStateRow row = wallStates[i];
                    if (row != null && row.kind == BlockKind.Barrier && row.tier == BlockTier.Greater)
                        warnings.Add("Greater Barrier wall-state rows are unsupported by the current balance ladder.");
                }
            }
        }

        private static void ValidateEntry(CombatVFXLibrary.VFXEntry entry, string label, bool? ranged, List<string> warnings)
        {
            if (IsBlank(entry))
                return;
            if (entry.impactDelay > 0f)
                warnings.Add($"{label} has impactDelay {entry.impactDelay:0.###}; fallback timing must remain marker/travel driven (-1). ");
            if (ranged == true &&
                !entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Projectile, out _) &&
                !CombatVfxResolver.IsUsable(entry))
                warnings.Add($"{label} has no usable Projectile phase.");
            if (ranged == false &&
                !entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Strike, out _) &&
                !entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Impact, out _))
                warnings.Add($"{label} has neither a usable Strike nor Impact phase.");
        }

        private static bool IsBlank(CombatVFXLibrary.VFXEntry entry)
        {
            if (entry == null)
                return true;
            if (!string.IsNullOrWhiteSpace(entry.moveDefinitionId) || entry.isCategoryFallback ||
                entry.impactDelay > 0f || entry.impactSound != null || entry.missSound != null)
                return false;
            return IsBlank(entry.castVfx) && IsBlank(entry.strikeVfx) &&
                   IsBlank(entry.projectileVfx) && IsBlank(entry.impactVfx);
        }

        private static bool IsBlank(CombatVFXLibrary.VFXPhaseConfig phase)
        {
            return phase == null || (!phase.enabled && phase.vfxPrefab == null &&
                phase.phaseStartSound == null && phase.postFxCue == null);
        }

        private void OnValidate()
        {
            MarkChanged();
            var warnings = new List<string>();
            CollectValidationWarnings(warnings);
            NormalizeTypedRows();
            NormalizeWallStateRows();
            for (int i = 0; i < warnings.Count; i++)
                Debug.LogWarning($"[CombatVFXFallbacks] {warnings[i]}", this);
        }
    }
}
