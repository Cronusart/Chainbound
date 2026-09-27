using UnityEngine;

public enum WalletPromptReason
{
    MainMenuNewGame,
    StarterCommitted,
    ManualAccountRecovery,
}

/// <summary>Reusable content definition for a wallet-link decision.</summary>
[CreateAssetMenu(fileName = "WalletPrompt", menuName = "Chainbound/Account/Wallet Prompt Definition")]
public class WalletPromptDefinitionSO : ScriptableObject
{
    [TextArea] public string title = "Connect your wallet now?";
    [TextArea] public string body = "Connect now to secure and recover this profile. You can keep playing without a wallet.";
    public string connectLabel = "Connect Wallet";
    public string skipLabel = "Play Without Wallet";
    public string retryLabel = "Try Again";
    public string conflictRestoreLabel = "Restore Wallet Profile";
    public string conflictSkipLabel = "Continue Without Wallet";
    public WalletPromptReason reason = WalletPromptReason.MainMenuNewGame;
    public bool suppressWhenLinked = true;
}
