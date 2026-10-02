using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class PlayerRosterCapacityTests
    {
        [Test]
        public void ServerCapacitySnapshot_ReportsTwentyTwoStorageSlots()
        {
            var roster = new PlayerRosterSnapshot
            {
                ownedCount = 25,
                ownedCapacity = 25,
                partyCount = 3,
                partyCapacity = 3,
                storageCount = 22,
                storageCapacity = 22,
                canCapture = false,
            };

            Assert.That(roster.IsAtCapacity, Is.True);
            Assert.That(roster.StorageCount, Is.EqualTo(22));
            Assert.That(roster.StorageCapacity, Is.EqualTo(22));
        }

        [Test]
        public void LegacyReaderFallback_DerivesStorageFromRosterAndParty()
        {
            var roster = new PlayerRosterSnapshot
            {
                chainlings = new System.Collections.Generic.List<PlayerRosterEntry>
                {
                    new PlayerRosterEntry(), new PlayerRosterEntry(), new PlayerRosterEntry(), new PlayerRosterEntry(),
                },
                partyCount = 3,
            };

            Assert.That(roster.OwnedCount, Is.EqualTo(4));
            Assert.That(roster.StorageCount, Is.EqualTo(1));
            Assert.That(roster.IsAtCapacity, Is.False);
        }
    }
}
