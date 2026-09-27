namespace Chainbound.Combat
{
    public static class AccountLinkPolicy
    {
        public static AccountLinkResult EvaluateIdentityLink(AccountLinkCollisionContext context)
        {
            return EvaluateLink(
                context,
                AccountLinkError.IdentityAlreadyLinkedToCurrentProfile,
                AccountLinkError.IdentityLinkedToDifferentProfile,
                "Identity is already linked to this profile.",
                "Identity is already linked to another profile.");
        }

        public static AccountLinkResult EvaluateWalletLink(AccountLinkCollisionContext context)
        {
            return EvaluateLink(
                context,
                AccountLinkError.WalletAlreadyLinkedToCurrentProfile,
                AccountLinkError.WalletLinkedToDifferentProfile,
                "Wallet is already linked to this profile.",
                "Wallet is already linked to another profile.");
        }

        private static AccountLinkResult EvaluateLink(
            AccountLinkCollisionContext context,
            AccountLinkError sameProfileError,
            AccountLinkError differentProfileError,
            string sameProfileMessage,
            string differentProfileMessage)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.currentPlayerProfileId))
                return AccountLinkResult.Failed(AccountLinkError.MissingCurrentProfile, "Current player profile is required.");

            if (!context.explicitConsent)
                return AccountLinkResult.Failed(AccountLinkError.ExplicitConsentRequired, "Linking requires explicit player action.");

            if (!context.existingLinkActive || string.IsNullOrWhiteSpace(context.existingLinkedPlayerProfileId))
                return AccountLinkResult.Success(context.currentPlayerProfileId);

            if (context.existingLinkedPlayerProfileId == context.currentPlayerProfileId)
            {
                return AccountLinkResult.Failed(
                    sameProfileError,
                    sameProfileMessage,
                    AccountLinkState.Linked);
            }

            return AccountLinkResult.Failed(differentProfileError, differentProfileMessage);
        }
    }
}
