using SignalJam.UI.MenuNavigation;
using UnityEngine;

/// <summary>
/// Reusable scene binding from an InteractableObject to the shared Chainling PC screen.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChainlingPcTerminalInteractable : MonoBehaviour
{
    [SerializeField] private string terminalId = ChainlingPcTerminalIds.PlayerRoomTerminalId;
    [SerializeField] private ChainlingPcTerminalScreen screenPrefab;

    private ChainlingPcTerminalScreen screen;

    public string TerminalId => string.IsNullOrWhiteSpace(terminalId)
        ? ChainlingPcTerminalIds.PlayerRoomTerminalId
        : terminalId.Trim();

    public ChainlingPcTerminalScreen ScreenPrefab => screenPrefab;

    public void Configure(string id, ChainlingPcTerminalScreen prefab)
    {
        terminalId = string.IsNullOrWhiteSpace(id)
            ? ChainlingPcTerminalIds.PlayerRoomTerminalId
            : id.Trim();
        screenPrefab = prefab;
    }

    public void OpenTerminal()
    {
        if (!ChainlingPcTerminalIds.IsUnlocked(ProfileWorldFlagStateService.Instance))
            return;

        if (screen == null)
            screen = FindFirstObjectByType<ChainlingPcTerminalScreen>(FindObjectsInactive.Include);
        if (screen == null && screenPrefab != null)
        {
            screen = Instantiate(screenPrefab);
            screen.name = "ChainlingPcTerminalScreen";
            DontDestroyOnLoad(screen.gameObject);
        }

        if (screen == null)
        {
            Debug.LogWarning(
                "[ChainlingPcTerminalInteractable] Chainling PC screen prefab is missing.",
                this);
            return;
        }

        screen.Open(TerminalId);
    }
}
