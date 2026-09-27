using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class AccountLinkPolicyTests
    {
        [Test]
        public void WalletLinkRequiresExplicitConsent()
        {
            var result = AccountLinkPolicy.EvaluateWalletLink(new AccountLinkCollisionContext
            {
                currentPlayerProfileId = "profile-a",
                explicitConsent = false,
            });

            Assert.IsFalse(result.success);
            Assert.AreEqual(AccountLinkError.ExplicitConsentRequired, result.error);
        }

        [Test]
        public void WalletLinkAllowsCurrentProfileWhenNoActiveCollisionExists()
        {
            var result = AccountLinkPolicy.EvaluateWalletLink(new AccountLinkCollisionContext
            {
                currentPlayerProfileId = "profile-a",
                existingLinkActive = false,
                explicitConsent = true,
            });

            Assert.IsTrue(result.success);
            Assert.AreEqual(AccountLinkState.Linked, result.state);
            Assert.AreEqual("profile-a", result.playerProfileId);
        }

        [Test]
        public void WalletLinkBlocksDifferentProfileCollision()
        {
            var result = AccountLinkPolicy.EvaluateWalletLink(new AccountLinkCollisionContext
            {
                currentPlayerProfileId = "profile-a",
                existingLinkedPlayerProfileId = "profile-b",
                existingLinkActive = true,
                explicitConsent = true,
            });

            Assert.IsFalse(result.success);
            Assert.AreEqual(AccountLinkError.WalletLinkedToDifferentProfile, result.error);
            Assert.AreEqual(AccountLinkState.Conflict, result.state);
        }

        [Test]
        public void IdentityLinkBlocksDifferentProfileCollision()
        {
            var result = AccountLinkPolicy.EvaluateIdentityLink(new AccountLinkCollisionContext
            {
                currentPlayerProfileId = "profile-a",
                existingLinkedPlayerProfileId = "profile-b",
                existingLinkActive = true,
                explicitConsent = true,
            });

            Assert.IsFalse(result.success);
            Assert.AreEqual(AccountLinkError.IdentityLinkedToDifferentProfile, result.error);
        }
    }
}
