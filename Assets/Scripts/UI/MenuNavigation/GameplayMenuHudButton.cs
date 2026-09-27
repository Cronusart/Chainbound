using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Attach to the dedicated mobile HUD gameplay-menu button.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GameplayMenuHudButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private PlayerMenuController playerMenuController;
        [SerializeField] private MonoBehaviour confirmSfxBehaviour;

        private void OnEnable()
        {
            ResolveReferences();
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            if (confirmSfxBehaviour is IMenuUiSfx confirmSfx)
                confirmSfx.PlayConfirm();

            ResolveReferences();
            playerMenuController?.OpenToTab("inventory");
        }

        private void ResolveReferences()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (playerMenuController == null)
                playerMenuController = FindObjectOfType<PlayerMenuController>(true);
            if (confirmSfxBehaviour == null)
                confirmSfxBehaviour = FindMenuConfirmSfxBehaviour();
        }

        private static MonoBehaviour FindMenuConfirmSfxBehaviour()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMenuUiSfx)
                    return behaviours[i];
            }

            return null;
        }
    }
}
