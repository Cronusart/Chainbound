using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// State of a team (one player's side) in combat.
    /// 2v2 format: each team has 2 active Chainlings on the field and 1 on the bench.
    /// When an active Chainling faints, the bench Chainling auto-swaps in.
    /// </summary>
    [Serializable]
    public class TeamState
    {
        /// <summary>Team identifier (0 or 1).</summary>
        public int teamIndex;

        /// <summary>Display name for this team.</summary>
        public string teamName;

        /// <summary>
        /// Full roster of Chainlings on this team (up to ROSTER_SIZE = 3).
        /// Indices 0..N where some are active and one may be on the bench.
        /// </summary>
        public List<ChainlingInstance> chainlings;

        /// <summary>
        /// Indices into <see cref="chainlings"/> for the Chainlings currently on the field.
        /// In 2v2 this holds up to 2 entries. Order = slot 0 (lead), slot 1 (partner).
        /// </summary>
        public List<int> activeIndices;

        /// <summary>Is this team fully defeated (all 3 Chainlings fainted)?</summary>
        public bool IsDefeated
        {
            get
            {
                if (chainlings == null || chainlings.Count == 0) return true;
                for (int i = 0; i < chainlings.Count; i++)
                {
                    if (chainlings[i].isAlive) return false;
                }
                return true;
            }
        }

        /// <summary>Get the lead active Chainling (slot 0). May be null if slot is empty.</summary>
        public ChainlingInstance LeadChainling => GetActiveInSlot(0);

        /// <summary>Get the partner active Chainling (slot 1). May be null if slot is empty.</summary>
        public ChainlingInstance PartnerChainling => GetActiveInSlot(1);

        /// <summary>
        /// Get the first alive active Chainling (convenience for single-target resolution).
        /// Prefers slot 0 (lead), falls back to slot 1 (partner).
        /// </summary>
        public ChainlingInstance ActiveChainling => LeadChainling ?? PartnerChainling;

        /// <summary>Get all currently active (on-field) Chainlings.</summary>
        public List<ChainlingInstance> GetActiveChainlings()
        {
            var result = new List<ChainlingInstance>(CombatConstants.ACTIVE_PER_TEAM);
            for (int i = 0; i < activeIndices.Count; i++)
            {
                int idx = activeIndices[i];
                if (idx >= 0 && idx < chainlings.Count && chainlings[idx].isAlive)
                    result.Add(chainlings[idx]);
            }
            return result;
        }

        /// <summary>Get the Chainling in a specific active slot (0 = lead, 1 = partner).</summary>
        public ChainlingInstance GetActiveInSlot(int slot)
        {
            if (activeIndices == null || slot < 0 || slot >= activeIndices.Count)
                return null;
            int idx = activeIndices[slot];
            if (idx < 0 || idx >= chainlings.Count) return null;
            var c = chainlings[idx];
            return c.isAlive ? c : null;
        }

        /// <summary>Get the number of alive Chainlings currently occupying active slots.</summary>
        public int CountAliveActiveSlots()
        {
            int count = 0;
            if (activeIndices == null || chainlings == null)
                return count;

            for (int slot = 0; slot < activeIndices.Count; slot++)
            {
                if (GetActiveInSlot(slot) != null)
                    count++;
            }

            return count;
        }

        /// <summary>Get the number of alive Chainlings remaining (active + bench).</summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                if (chainlings != null)
                {
                    for (int i = 0; i < chainlings.Count; i++)
                    {
                        if (chainlings[i].isAlive) count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Empty party bench slots: bench capacity minus living Chainlings currently occupying the bench.
        /// Does not count empty move slots. A 2-Chainling party has 1 empty bench slot in 2v2.
        /// </summary>
        public int CountEmptyBenchSlots()
        {
            return Math.Max(0, CombatConstants.BENCH_SLOTS_PER_TEAM - CountOccupiedBenchSlots());
        }

        /// <summary>Living Chainlings on this team that are not in an active field slot.</summary>
        public int CountOccupiedBenchSlots()
        {
            int occupied = 0;
            if (chainlings == null || activeIndices == null)
                return occupied;

            for (int i = 0; i < chainlings.Count; i++)
            {
                ChainlingInstance candidate = chainlings[i];
                if (candidate == null || !candidate.isAlive)
                    continue;
                if (!activeIndices.Contains(i))
                    occupied++;
            }

            return occupied;
        }

        /// <summary>
        /// Find the bench Chainling index (alive, not in activeIndices).
        /// Returns -1 if no bench Chainling is available.
        /// </summary>
        public int GetBenchIndex()
        {
            for (int i = 0; i < chainlings.Count; i++)
            {
                if (!chainlings[i].isAlive) continue;
                if (!activeIndices.Contains(i)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Auto-swap: replace a fainted active Chainling with the bench Chainling.
        /// Returns the new Chainling that swapped in, or null if bench is empty.
        /// </summary>
        public ChainlingInstance AutoSwapFromBench(int activeSlot)
        {
            int benchIdx = GetBenchIndex();
            if (benchIdx < 0) return null; // No bench available

            // Mark old Chainling as inactive
            int oldIdx = activeIndices[activeSlot];
            int slotAp = 0;
            if (oldIdx >= 0 && oldIdx < chainlings.Count)
            {
                chainlings[oldIdx].isActive = false;
                slotAp = chainlings[oldIdx].currentAP;
            }

            // Swap in bench Chainling
            activeIndices[activeSlot] = benchIdx;
            chainlings[benchIdx].isActive = true;
            chainlings[benchIdx].currentAP = slotAp;
            chainlings[benchIdx].ClampAP();
            return chainlings[benchIdx];
        }

        /// <summary>
        /// Removes a captured or escaped active target without setting HP to zero or creating a KO.
        /// A living bench target fills the vacated field slot when available.
        /// </summary>
        public bool RemoveActiveWithoutKo(string instanceId, out ChainlingInstance replacement)
        {
            replacement = null;
            if (string.IsNullOrWhiteSpace(instanceId) || chainlings == null || activeIndices == null)
                return false;

            int rosterIndex = chainlings.FindIndex(c => c != null && string.Equals(c.instanceId, instanceId, StringComparison.Ordinal));
            if (rosterIndex < 0)
                return false;

            int activeSlot = activeIndices.IndexOf(rosterIndex);
            if (activeSlot < 0)
                return false;

            ChainlingInstance removed = chainlings[rosterIndex];
            int inheritedAp = removed != null ? removed.currentAP : 0;
            if (removed != null)
                removed.isActive = false;
            activeIndices[activeSlot] = -1;
            chainlings.RemoveAt(rosterIndex);

            for (int slot = 0; slot < activeIndices.Count; slot++)
            {
                if (activeIndices[slot] > rosterIndex)
                    activeIndices[slot]--;
            }

            int benchIndex = GetBenchIndex();
            if (benchIndex >= 0)
            {
                activeIndices[activeSlot] = benchIndex;
                replacement = chainlings[benchIndex];
                replacement.isActive = true;
                replacement.currentAP = inheritedAp;
                replacement.ClampAP();
            }

            return true;
        }

        public TeamState()
        {
            chainlings = new List<ChainlingInstance>();
            activeIndices = new List<int>();
        }

        public TeamState(int index, string name) : this()
        {
            teamIndex = index;
            teamName = name;
        }
    }
}
