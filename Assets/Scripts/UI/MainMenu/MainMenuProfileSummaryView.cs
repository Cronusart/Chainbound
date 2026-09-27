using System;
using Chainbound.Combat;
using TMPro;
using UnityEngine;

/// <summary>Displays the profile data shared with the future full Profile tab.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuProfileSummaryView : MonoBehaviour
{
    [SerializeField] private TMP_Text headingLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private MonoBehaviour playerStatsProviderBehaviour;

    private int bindVersion;

    public void Bind(MainMenuProfileSnapshot snapshot)
    {
        int version = ++bindVersion;
        IPlayerStatsProvider statsProvider = playerStatsProviderBehaviour as IPlayerStatsProvider;

        if (headingLabel != null)
            headingLabel.text = string.IsNullOrWhiteSpace(snapshot.displayName) ? "PLAYER PROFILE" : snapshot.displayName.Trim();

        if (bodyLabel != null)
        {
            bodyLabel.text =
                $"Profile  {Shorten(snapshot.playerProfileId, 18)}\n" +
                $"Wallet   {Shorten(snapshot.walletPublicKey, 18)}\n" +
                $"Link     {snapshot.accountLinkState}";
        }

        if (statusLabel != null)
            statusLabel.text = statsProvider == null ? "READY TO CONTINUE" : "LOADING PLAYER STATS...";

        if (statsProvider != null)
            FetchStats(statsProvider, snapshot, version);
    }

    private async void FetchStats(IPlayerStatsProvider provider, MainMenuProfileSnapshot snapshot, int version)
    {
        try
        {
            PlayerStatsSnapshot stats = await provider.FetchStatsAsync(snapshot.playerProfileId, snapshot.walletPublicKey);
            if (this == null || version != bindVersion || statusLabel == null)
                return;

            statusLabel.text = stats == null
                ? "READY TO CONTINUE"
                : $"RAM {stats.ramBalance:N0}   BATTLES {stats.battlesWon}W / {stats.battlesLost}L / {stats.battlesDrawn}D";
        }
        catch (Exception)
        {
            if (this != null && version == bindVersion && statusLabel != null)
                statusLabel.text = "READY TO CONTINUE";
        }
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Not linked";

        string trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        int side = Mathf.Max(3, (maxLength - 3) / 2);
        return trimmed.Substring(0, side) + "..." + trimmed.Substring(trimmed.Length - side);
    }
}
