using Chainbound.Combat.Core;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;

namespace Chainbound.Combat.Tests
{
    public sealed class PlayerRamBalanceStoreTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerRamBalanceStore.Clear();
        }

        [Test]
        public void SettlementWinsOverOlderInFlightProfileRead()
        {
            PlayerRamBalanceStore.ReadToken oldRead = PlayerRamBalanceStore.BeginProfileRead("profile-a");
            Assert.IsTrue(PlayerRamBalanceStore.ApplySettlement("profile-a", new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Settled,
                BalanceAfter = 15,
                AppliedRam = 15,
                BalanceVersion = 10,
                ReceiptSequence = 10,
            }));

            Assert.IsFalse(PlayerRamBalanceStore.TryApplyProfileRead(oldRead, 0, 9));
            Assert.IsTrue(PlayerRamBalanceStore.TryGet("profile-a", out long balance));
            Assert.AreEqual(15, balance);
        }

        [Test]
        public void OlderSettlementCannotOverwriteNewerBalance()
        {
            Assert.IsTrue(PlayerRamBalanceStore.ApplySettlement("profile-a", new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Settled,
                BalanceAfter = 20,
                BalanceVersion = 20,
                ReceiptSequence = 20,
            }));
            Assert.IsFalse(PlayerRamBalanceStore.ApplySettlement("profile-a", new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Zero,
                BalanceAfter = 5,
                BalanceVersion = 19,
                ReceiptSequence = 19,
            }));
            Assert.IsTrue(PlayerRamBalanceStore.TryGet("profile-a", out long balance));
            Assert.AreEqual(20, balance);
        }

        [Test]
        public void CurrentProfileReadCanApplyDecreases()
        {
            PlayerRamBalanceStore.ReadToken first = PlayerRamBalanceStore.BeginProfileRead("profile-a");
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyProfileRead(first, 20, 1));

            PlayerRamBalanceStore.ReadToken second = PlayerRamBalanceStore.BeginProfileRead("profile-a");
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyProfileRead(second, 5, 2));
            Assert.IsTrue(PlayerRamBalanceStore.TryGet("profile-a", out long balance));
            Assert.AreEqual(5, balance);
        }

        [Test]
        public void BalanceIsScopedToProfile()
        {
            PlayerRamBalanceStore.ReadToken read = PlayerRamBalanceStore.BeginProfileRead("profile-a");
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyProfileRead(read, 7, 1));

            Assert.IsFalse(PlayerRamBalanceStore.TryGet("profile-b", out _));
        }

        [Test]
        public void NewProfileReadReplacesPreviousProfileScope()
        {
            PlayerRamBalanceStore.ReadToken first = PlayerRamBalanceStore.BeginProfileRead("profile-a");
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyProfileRead(first, 7, 1));

            PlayerRamBalanceStore.ReadToken second = PlayerRamBalanceStore.BeginProfileRead("profile-b");
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyProfileRead(second, 9, 2));
            Assert.IsFalse(PlayerRamBalanceStore.TryGet("profile-a", out _));
            Assert.IsTrue(PlayerRamBalanceStore.TryGet("profile-b", out long balance));
            Assert.AreEqual(9, balance);
        }

        [Test]
        public void WalletOnlyReadDoesNotClearKnownProfileBalance()
        {
            PlayerRamBalanceStore.ReadToken first = PlayerRamBalanceStore.BeginProfileRead("profile-a");
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyProfileRead(first, 7, 1));

            PlayerRamBalanceStore.BeginProfileRead("");

            Assert.IsTrue(PlayerRamBalanceStore.TryGet("profile-a", out long balance));
            Assert.AreEqual(7, balance);
        }

        [Test]
        public void OlderShopOrOwnershipBalanceCannotOverwriteNewerSource()
        {
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyAuthoritative("profile-a", 80, 30));
            Assert.IsFalse(PlayerRamBalanceStore.TryApplyAuthoritative("profile-a", 5, 29));
            Assert.IsTrue(PlayerRamBalanceStore.TryGet("profile-a", out long balance));
            Assert.AreEqual(80, balance);
        }

        [Test]
        public void CandyMachineDisplayUsesCanonicalProfileBalance()
        {
            Assert.IsTrue(PlayerRamBalanceStore.TryApplyAuthoritative("profile-a", 65, 4));

            Assert.AreEqual(65, CandyMachineScreen.ResolveDisplayRamBalance("profile-a", 0));
            Assert.AreEqual(12, CandyMachineScreen.ResolveDisplayRamBalance("profile-b", 12));
        }
    }
}
