using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public enum VfxResolveTier
    {
        Explicit,
        LegacyCategory,
        Typed,
        Typeless,
        Role,
        RoleParent,
        Global,
        None
    }

    public readonly struct VfxMoveKey : IEquatable<VfxMoveKey>
    {
        public readonly string definitionId;
        public readonly MoveCategory category;
        public readonly ChainlingType baseType;
        public readonly MoveVfxRole role;

        public VfxMoveKey(string definitionId, MoveCategory category, ChainlingType baseType, MoveVfxRole role)
        {
            this.definitionId = definitionId;
            this.category = category;
            this.baseType = baseType;
            this.role = role;
        }

        public static VfxMoveKey From(MoveInstance move)
        {
            return move == null
                ? default
                : new VfxMoveKey(move.definitionId, move.category, move.baseType, MoveVfxRoleClassifier.Classify(move));
        }

        public static VfxMoveKey From(MoveDefinitionSO move)
        {
            return move == null
                ? default
                : new VfxMoveKey(move.moveId, move.category, move.baseType, MoveVfxRoleClassifier.Classify(move));
        }

        public bool Equals(VfxMoveKey other)
        {
            return string.Equals(definitionId, other.definitionId, StringComparison.Ordinal) &&
                   category == other.category &&
                   baseType == other.baseType &&
                   role == other.role;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxMoveKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = definitionId != null ? StringComparer.Ordinal.GetHashCode(definitionId) : 0;
                hash = (hash * 397) ^ (int)category;
                hash = (hash * 397) ^ (int)baseType;
                hash = (hash * 397) ^ (int)role;
                return hash;
            }
        }

        public static bool operator ==(VfxMoveKey left, VfxMoveKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxMoveKey left, VfxMoveKey right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct VfxResolution
    {
        private const int PhaseSlotCount = 4;

        public readonly CombatVFXLibrary.VFXEntry entry;
        public readonly VfxResolveTier tier;
        public readonly string slotLabel;
        public readonly string debugTrace;
        public readonly VfxResolveTier[] phaseTiers;
        public readonly string[] phaseLabels;
        public readonly bool isComposed;

        public VfxResolution(CombatVFXLibrary.VFXEntry entry, VfxResolveTier tier, string slotLabel, string debugTrace)
            : this(entry, tier, slotLabel, debugTrace, null, null, false)
        {
        }

        public VfxResolution(
            CombatVFXLibrary.VFXEntry entry,
            VfxResolveTier tier,
            string slotLabel,
            string debugTrace,
            VfxResolveTier[] phaseTiers,
            string[] phaseLabels,
            bool isComposed)
        {
            this.entry = entry;
            this.tier = tier;
            this.slotLabel = slotLabel;
            this.debugTrace = debugTrace;
            this.phaseTiers = phaseTiers;
            this.phaseLabels = phaseLabels;
            this.isComposed = isComposed;
        }

        public VfxResolveTier PhaseTier(CombatVFXLibrary.VFXPhase phase)
        {
            int index = (int)phase;
            if (phaseTiers == null || (uint)index >= (uint)phaseTiers.Length)
                return VfxResolveTier.None;
            return phaseTiers[index];
        }

        public string PhaseLabel(CombatVFXLibrary.VFXPhase phase)
        {
            int index = (int)phase;
            if (phaseLabels == null || (uint)index >= (uint)phaseLabels.Length || string.IsNullOrEmpty(phaseLabels[index]))
                return "None";
            return phaseLabels[index];
        }

        internal static int PhaseCount => PhaseSlotCount;
    }

    /// <summary>Pure, ordered move VFX fallback resolver.</summary>
    public static class CombatVfxResolver
    {
        private readonly struct Candidate
        {
            public readonly CombatVFXLibrary.VFXEntry entry;
            public readonly VfxResolveTier tier;
            public readonly string label;

            public Candidate(CombatVFXLibrary.VFXEntry entry, VfxResolveTier tier, string label)
            {
                this.entry = entry;
                this.tier = tier;
                this.label = label;
            }
        }

        public static VfxResolution Resolve(
            VfxMoveKey key,
            IReadOnlyList<CombatVFXLibrary.VFXEntry> entries,
            CombatVFXFallbacks fallbacks)
        {
            var trace = new List<string>(8);
            var candidates = new List<Candidate>(8);
            bool stop = false;

            void Consider(CombatVFXLibrary.VFXEntry candidate, VfxResolveTier tier, string label)
            {
                if (stop)
                    return;

                if (!IsUsable(candidate))
                {
                    trace.Add(label + " empty");
                    return;
                }

                trace.Add(label);
                candidates.Add(new Candidate(candidate, tier, label));
                if (candidates.Count == 1 && candidate.suppressPhaseFill)
                    stop = true;
            }

            CombatVFXLibrary.VFXEntry exact = FindExplicit(entries, key.definitionId);
            Consider(exact, VfxResolveTier.Explicit, $"Explicit:{key.definitionId}");

            CombatVFXLibrary.VFXEntry legacy = FindLegacy(entries, key.category);
            Consider(legacy, VfxResolveTier.LegacyCategory, $"Legacy:{key.category}");

            if (fallbacks == null)
                return Finish(key, candidates, trace);

            bool damage = key.category == MoveCategory.DamagingMelee || key.category == MoveCategory.DamagingRanged;
            if (damage)
            {
                CombatVFXLibrary.VFXEntry typed = fallbacks.GetTyped(key.baseType, key.category);
                VfxResolveTier primaryTier = key.baseType == ChainlingType.None
                    ? VfxResolveTier.Typeless
                    : VfxResolveTier.Typed;
                string primaryLabel = key.baseType == ChainlingType.None
                    ? $"Typeless:{RangeLabel(key.category)}"
                    : $"Typed:{key.baseType}/{RangeLabel(key.category)}";
                Consider(typed, primaryTier, primaryLabel);

                if (key.baseType != ChainlingType.None)
                {
                    CombatVFXLibrary.VFXEntry typeless = fallbacks.GetTyped(ChainlingType.None, key.category);
                    Consider(typeless, VfxResolveTier.Typeless, $"Typeless:{RangeLabel(key.category)}");
                }

                CombatVFXLibrary.VFXEntry global = fallbacks.GetGlobal(key.category);
                Consider(global, VfxResolveTier.Global, $"Global:{RangeLabel(key.category)}");
                return Finish(key, candidates, trace);
            }

            if (key.role == MoveVfxRole.Passive)
                return Finish(key, candidates, trace);

            CombatVFXLibrary.VFXEntry roleEntry = fallbacks.GetRole(key.role);
            Consider(roleEntry, VfxResolveTier.Role, $"Role:{key.role}");

            MoveVfxRole parent = MoveVfxRoleClassifier.ParentRole(key.role);
            if (parent != key.role)
            {
                CombatVFXLibrary.VFXEntry parentEntry = fallbacks.GetRole(parent);
                Consider(parentEntry, VfxResolveTier.RoleParent, $"RoleParent:{parent}");
            }

            CombatVFXLibrary.VFXEntry utility = fallbacks.GetGlobal(key.category);
            Consider(utility, VfxResolveTier.Global, "Global:Utility");
            return Finish(key, candidates, trace);
        }

        public static bool IsUsable(CombatVFXLibrary.VFXEntry entry)
        {
            if (entry == null)
                return false;
            foreach (CombatVFXLibrary.VFXPhase phase in Enum.GetValues(typeof(CombatVFXLibrary.VFXPhase)))
            {
                if (entry.TryGetPhaseConfig(phase, out _))
                    return true;
            }
            return false;
        }

        private static VfxResolution Finish(VfxMoveKey key, List<Candidate> candidates, List<string> trace)
        {
            if (candidates.Count == 0)
                return None(trace);

            Candidate basis = candidates[0];
            VfxResolveTier[] tiers = NoneTiers();
            string[] labels = NoneLabels();
            var chosen = new Candidate[VfxResolution.PhaseCount];
            var found = new bool[VfxResolution.PhaseCount];
            bool filledFromLaterTier = false;

            foreach (CombatVFXLibrary.VFXPhase phase in Enum.GetValues(typeof(CombatVFXLibrary.VFXPhase)))
            {
                int index = (int)phase;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!candidates[i].entry.TryGetPhaseConfig(phase, out _))
                        continue;

                    chosen[index] = candidates[i];
                    found[index] = true;
                    tiers[index] = candidates[i].tier;
                    labels[index] = candidates[i].label;
                    if (i > 0)
                        filledFromLaterTier = true;
                    break;
                }
            }

            if (basis.entry.suppressPhaseFill || !filledFromLaterTier)
                return Unmerged(basis, trace);

            CombatVFXLibrary.VFXEntry composed = Compose(key, basis.entry, chosen, found);
            return new VfxResolution(
                composed,
                basis.tier,
                basis.label,
                FormatTrace(trace, labels),
                tiers,
                labels,
                true);
        }

        private static VfxResolution Unmerged(Candidate basis, List<string> trace)
        {
            VfxResolveTier[] tiers = NoneTiers();
            string[] labels = NoneLabels();
            foreach (CombatVFXLibrary.VFXPhase phase in Enum.GetValues(typeof(CombatVFXLibrary.VFXPhase)))
            {
                if (!basis.entry.TryGetPhaseConfig(phase, out _))
                    continue;

                int index = (int)phase;
                tiers[index] = basis.tier;
                labels[index] = basis.label;
            }

            return new VfxResolution(
                basis.entry,
                basis.tier,
                basis.label,
                FormatTrace(trace, labels),
                tiers,
                labels,
                false);
        }

        private static CombatVFXLibrary.VFXEntry Compose(
            VfxMoveKey key,
            CombatVFXLibrary.VFXEntry basis,
            Candidate[] chosen,
            bool[] found)
        {
            int impactIndex = (int)CombatVFXLibrary.VFXPhase.Impact;
            CombatVFXLibrary.VFXEntry impactSource = found[impactIndex] ? chosen[impactIndex].entry : null;
            var composed = new CombatVFXLibrary.VFXEntry
            {
                moveDefinitionId = key.definitionId,
                isCategoryFallback = false,
                suppressPhaseFill = false,
                impactDelay = ResolveImpactDelay(basis, impactSource),
                impactSound = basis.impactSound != null ? basis.impactSound : impactSource?.impactSound,
                missSound = basis.missSound != null ? basis.missSound : impactSource?.missSound
            };

            foreach (CombatVFXLibrary.VFXPhase phase in Enum.GetValues(typeof(CombatVFXLibrary.VFXPhase)))
            {
                int index = (int)phase;
                CombatVFXLibrary.VFXPhaseConfig config;
                if (found[index] && chosen[index].entry.TryGetPhaseConfig(phase, out CombatVFXLibrary.VFXPhaseConfig source))
                    config = source.Clone();
                else
                    config = DisabledPhase();
                config.EnsureDefaults();
                AssignPhase(composed, phase, config);
            }

            return composed;
        }

        private static float ResolveImpactDelay(CombatVFXLibrary.VFXEntry basis, CombatVFXLibrary.VFXEntry impactSource)
        {
            if (basis != null && basis.impactDelay > 0f)
                return basis.impactDelay;
            if (impactSource != null && impactSource.impactDelay > 0f)
                return impactSource.impactDelay;
            return -1f;
        }

        private static CombatVFXLibrary.VFXPhaseConfig DisabledPhase()
        {
            var config = new CombatVFXLibrary.VFXPhaseConfig { enabled = false };
            config.EnsureDefaults();
            return config;
        }

        private static void AssignPhase(
            CombatVFXLibrary.VFXEntry entry,
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXPhaseConfig config)
        {
            switch (phase)
            {
                case CombatVFXLibrary.VFXPhase.Cast:
                    entry.castVfx = config;
                    break;
                case CombatVFXLibrary.VFXPhase.Strike:
                    entry.strikeVfx = config;
                    break;
                case CombatVFXLibrary.VFXPhase.Projectile:
                    entry.projectileVfx = config;
                    break;
                case CombatVFXLibrary.VFXPhase.Impact:
                    entry.impactVfx = config;
                    break;
            }
        }

        private static VfxResolution None(List<string> trace)
        {
            trace.Add("None");
            VfxResolveTier[] tiers = NoneTiers();
            string[] labels = NoneLabels();
            return new VfxResolution(null, VfxResolveTier.None, "None", FormatTrace(trace, labels), tiers, labels, false);
        }

        private static string FormatTrace(List<string> trace, string[] labels)
        {
            return string.Join(" -> ", trace) + " | " +
                   $"Cast={Label(labels, CombatVFXLibrary.VFXPhase.Cast)} " +
                   $"Strike={Label(labels, CombatVFXLibrary.VFXPhase.Strike)} " +
                   $"Projectile={Label(labels, CombatVFXLibrary.VFXPhase.Projectile)} " +
                   $"Impact={Label(labels, CombatVFXLibrary.VFXPhase.Impact)}";
        }

        private static string Label(string[] labels, CombatVFXLibrary.VFXPhase phase)
        {
            int index = (int)phase;
            if (labels == null || (uint)index >= (uint)labels.Length || string.IsNullOrEmpty(labels[index]))
                return "None";
            return labels[index];
        }

        private static VfxResolveTier[] NoneTiers()
        {
            var tiers = new VfxResolveTier[VfxResolution.PhaseCount];
            for (int i = 0; i < tiers.Length; i++)
                tiers[i] = VfxResolveTier.None;
            return tiers;
        }

        private static string[] NoneLabels()
        {
            var labels = new string[VfxResolution.PhaseCount];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = "None";
            return labels;
        }

        private static CombatVFXLibrary.VFXEntry FindExplicit(IReadOnlyList<CombatVFXLibrary.VFXEntry> entries, string id)
        {
            if (entries == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                CombatVFXLibrary.VFXEntry entry = entries[i];
                if (entry != null && !entry.isCategoryFallback && entry.moveDefinitionId == id)
                    return entry;
            }
            return null;
        }

        private static CombatVFXLibrary.VFXEntry FindLegacy(IReadOnlyList<CombatVFXLibrary.VFXEntry> entries, MoveCategory category)
        {
            if (entries == null)
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                CombatVFXLibrary.VFXEntry entry = entries[i];
                if (entry != null && entry.isCategoryFallback && entry.fallbackCategory == category)
                    return entry;
            }
            return null;
        }

        private static string RangeLabel(MoveCategory category)
        {
            return category == MoveCategory.DamagingRanged ? "Ranged" : "Melee";
        }
    }
}
