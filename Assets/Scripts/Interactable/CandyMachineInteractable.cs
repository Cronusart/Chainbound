using SignalJam.UI.MenuNavigation;
using UnityEngine;

/// <summary>
/// Scene binding from an InteractableObject to the shared candy machine UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class CandyMachineInteractable : MonoBehaviour
{
    [SerializeField] private CandyMachineScreen screenPrefab;
    [SerializeField] private CandyMachinePullDirector pullDirector;

    private CandyMachineScreen screen;

    public CandyMachineScreen ScreenPrefab => screenPrefab;

    public CandyMachinePullDirector PullDirector => pullDirector;

    public void Configure(CandyMachineScreen prefab)
    {
        screenPrefab = prefab;
        if (pullDirector == null)
            pullDirector = GetComponentInParent<CandyMachinePullDirector>(true);
    }

    public void Configure(CandyMachineScreen prefab, CandyMachinePullDirector director)
    {
        screenPrefab = prefab;
        pullDirector = director;
    }

    public void OpenCandyMachine()
    {
        if (screen == null)
            screen = CandyMachineScreen.Instance;
        if (screen == null && screenPrefab != null)
        {
            screen = Instantiate(screenPrefab);
            screen.name = CandyMachineIds.PresenterChildName;
        }

        if (screen == null)
        {
            Debug.LogWarning("[CandyMachineInteractable] Candy machine presenter was not found.", this);
            return;
        }

        if (!screen.Open(pullDirector))
        {
            Debug.LogWarning("[CandyMachineInteractable] Candy machine presenter failed to open.", this);
        }
    }
}
