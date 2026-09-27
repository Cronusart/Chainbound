using SignalJam.UI.MobileControls;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Optional prefab-side bridge from MobileControlRouter Select to the player menu.
    /// Designers can still wire MobileControlRouter.onSelect directly.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayMenuInputBridge : MonoBehaviour
    {
        [SerializeField] private MobileControlRouter router;
        [SerializeField] private PlayerMenuController playerMenuController;
        [SerializeField] private bool openOnSelect = true;

        private void OnEnable()
        {
            ResolveReferences();
            if (router != null)
                router.SelectRouted += HandleSelect;
        }

        private void OnDisable()
        {
            if (router != null)
                router.SelectRouted -= HandleSelect;
        }

        private void HandleSelect()
        {
            if (!openOnSelect) return;

            ResolveReferences();
            playerMenuController?.Open();
        }

        private void ResolveReferences()
        {
            if (router == null)
                router = FindObjectOfType<MobileControlRouter>(true);
            if (playerMenuController == null)
                playerMenuController = FindObjectOfType<PlayerMenuController>(true);
        }
    }
}
