using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class ChainlingsMenuTestHarnessTests
    {
        [Test]
        public void RegenerateTestRoster_SeedsPartySnapshotsWithEquippedMoves()
        {
            GameObject go = new GameObject("ChainlingsMenuTestHarnessTests");
            try
            {
                ChainlingsMenuTestHarness harness = go.AddComponent<ChainlingsMenuTestHarness>();
                harness.ApplyGenerationSettings(
                    newSeed: 1776,
                    rosterSize: 6,
                    partySize: CombatConstants.ROSTER_SIZE,
                    moveInventorySize: 48,
                    regenerateOnEnableFlag: false,
                    unlockCosmetics: true,
                    includeRebirthReady: true,
                    allowEntangled: true);
                harness.RegenerateTestRoster();

                PlayerRosterSnapshot roster = harness.FetchRosterAsync("profile", "wallet").GetAwaiter().GetResult();
                MoveInventorySnapshot inventory = harness.FetchMoveInventoryAsync("profile", "wallet").GetAwaiter().GetResult();

                int partyCount = 0;
                bool sawOverflow = false;
                bool sawUnderOrEqualViewport = false;
                for (int i = 0; i < roster.chainlings.Count; i++)
                {
                    PlayerRosterEntry entry = roster.chainlings[i];
                    if (entry == null || !entry.isPartyMember)
                        continue;

                    partyCount++;
                    Assert.NotNull(entry.snapshot.moveIds);
                    int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(entry.snapshot.rebirthCount);
                    Assert.Greater(
                        entry.snapshot.moveIds.Count,
                        0,
                        "Sandbox party snapshots should expose equipped moves for the party Moves view.");
                    Assert.LessOrEqual(entry.snapshot.moveIds.Count, maxSlots);

                    if (entry.snapshot.moveIds.Count > MoveLoadoutSlotRules.VisibleRowsBeforeScroll)
                        sawOverflow = true;
                    else
                        sawUnderOrEqualViewport = true;

                    for (int j = 0; j < entry.snapshot.moveIds.Count; j++)
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(entry.snapshot.moveIds[j]));
                        Assert.IsTrue(InventoryHasEquippedMove(inventory, entry.snapshot.backendChainlingId, entry.snapshot.moveIds[j]));
                    }
                }

                Assert.AreEqual(CombatConstants.ROSTER_SIZE, partyCount);
                Assert.IsTrue(
                    sawOverflow,
                    "Sandbox party should include at least one loadout above the authored 4-row viewport for scroll testing.");
                Assert.IsTrue(
                    sawUnderOrEqualViewport,
                    "Sandbox party should include at least one loadout at or below the authored 4-row viewport.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RegenerateTestRoster_SeedsXpProgressAndPersistedHpVariation()
        {
            GameObject go = new GameObject("ChainlingsMenuTestHarnessTests");
            try
            {
                ChainlingsMenuTestHarness harness = go.AddComponent<ChainlingsMenuTestHarness>();
                harness.ApplyGenerationSettings(
                    newSeed: 1776,
                    rosterSize: 6,
                    partySize: CombatConstants.ROSTER_SIZE,
                    moveInventorySize: 30,
                    regenerateOnEnableFlag: false,
                    unlockCosmetics: true,
                    includeRebirthReady: true,
                    allowEntangled: true);
                harness.RegenerateTestRoster();

                PlayerRosterSnapshot roster = harness.FetchRosterAsync("profile", "wallet").GetAwaiter().GetResult();

                var masteryXpValues = new System.Collections.Generic.HashSet<int>();
                bool hasPersistedDamagedHp = false;
                for (int i = 0; i < roster.chainlings.Count; i++)
                {
                    PlayerRosterEntry entry = roster.chainlings[i];
                    ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
                    Assert.NotNull(snapshot);
                    Assert.AreEqual(entry.masteryXp, snapshot.masteryXp);
                    Assert.GreaterOrEqual(snapshot.currentHp, 0);

                    masteryXpValues.Add(snapshot.masteryXp);
                    if (snapshot.currentHp < 100)
                        hasPersistedDamagedHp = true;
                }

                Assert.Greater(masteryXpValues.Count, 1);
                Assert.IsTrue(hasPersistedDamagedHp);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EquipMoveAsync_ClearingSlotRefreshesSnapshotMoveIds()
        {
            GameObject go = new GameObject("ChainlingsMenuTestHarnessTests");
            try
            {
                ChainlingsMenuTestHarness harness = go.AddComponent<ChainlingsMenuTestHarness>();
                harness.ApplyGenerationSettings(
                    newSeed: 1776,
                    rosterSize: 6,
                    partySize: CombatConstants.ROSTER_SIZE,
                    moveInventorySize: 48,
                    regenerateOnEnableFlag: false,
                    unlockCosmetics: true,
                    includeRebirthReady: true,
                    allowEntangled: true);
                harness.RegenerateTestRoster();

                PlayerRosterEntry partyEntry = FindPartyEntry(harness);
                Assert.NotNull(partyEntry);
                Assert.NotNull(partyEntry.snapshot.moveIds);
                Assert.Greater(partyEntry.snapshot.moveIds.Count, 0);
                int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(partyEntry.snapshot.rebirthCount);
                Assert.LessOrEqual(partyEntry.snapshot.moveIds.Count, maxSlots);
                int beforeCount = partyEntry.snapshot.moveIds.Count;

                MenuMutationResult result = harness
                    .EquipMoveAsync("profile", "wallet", partyEntry.snapshot.backendChainlingId, 0, "")
                    .GetAwaiter()
                    .GetResult();

                Assert.IsTrue(result.success, result.error);

                PlayerRosterEntry refreshed = FindPartyEntry(harness, partyEntry.snapshot.backendChainlingId);
                Assert.NotNull(refreshed);
                Assert.AreEqual(beforeCount - 1, refreshed.snapshot.moveIds.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static PlayerRosterEntry FindPartyEntry(ChainlingsMenuTestHarness harness, string chainlingId = null)
        {
            PlayerRosterSnapshot roster = harness.FetchRosterAsync("profile", "wallet").GetAwaiter().GetResult();
            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry == null || !entry.isPartyMember || entry.snapshot == null)
                    continue;
                if (string.IsNullOrWhiteSpace(chainlingId) || entry.snapshot.backendChainlingId == chainlingId)
                    return entry;
            }

            return null;
        }

        private static bool InventoryHasEquippedMove(MoveInventorySnapshot inventory, string chainlingId, string moveId)
        {
            if (inventory == null || inventory.moves == null)
                return false;

            for (int i = 0; i < inventory.moves.Count; i++)
            {
                MenuMoveItem move = inventory.moves[i];
                if (move != null &&
                    move.moveId == moveId &&
                    move.equippedChainlingId == chainlingId &&
                    move.equippedSlot >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
