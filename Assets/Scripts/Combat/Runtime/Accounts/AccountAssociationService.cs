using UnityEngine;

namespace Chainbound.Combat
{
    public class AccountAssociationService
    {
        public AccountLinkResult CanLinkIdentity(
            PlayerAccountSession currentSession,
            string existingLinkedProfileId,
            bool existingLinkActive,
            bool explicitConsent)
        {
            var result = AccountLinkPolicy.EvaluateIdentityLink(new AccountLinkCollisionContext
            {
                currentPlayerProfileId = currentSession != null ? currentSession.PlayerProfileId : "",
                existingLinkedPlayerProfileId = existingLinkedProfileId ?? "",
                existingLinkActive = existingLinkActive,
                explicitConsent = explicitConsent,
            });

            Log("identity", result);
            return result;
        }

        public AccountLinkResult CanLinkWallet(
            PlayerAccountSession currentSession,
            string existingLinkedProfileId,
            bool existingLinkActive,
            bool explicitConsent)
        {
            var result = AccountLinkPolicy.EvaluateWalletLink(new AccountLinkCollisionContext
            {
                currentPlayerProfileId = currentSession != null ? currentSession.PlayerProfileId : "",
                existingLinkedPlayerProfileId = existingLinkedProfileId ?? "",
                existingLinkActive = existingLinkActive,
                explicitConsent = explicitConsent,
            });

            Log("wallet", result);
            return result;
        }

        private static void Log(string target, AccountLinkResult result)
        {
            if (result == null)
                return;

            if (result.success)
                Debug.Log($"[AccountAssociationService] {target} link allowed for profile={result.playerProfileId}");
            else
                Debug.LogWarning($"[AccountAssociationService] {target} link blocked: {result.error} {result.message}");
        }
    }
}
