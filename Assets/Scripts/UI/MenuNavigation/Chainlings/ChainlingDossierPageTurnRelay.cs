using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Attached at runtime to the selectables of an open dossier page. Horizontal moves
    /// that the selectable does not consume (no explicit left/right target) turn the
    /// page instead, so D-pad left/right flips through the dossier like a stack of pages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingDossierPageTurnRelay : MonoBehaviour, IMoveHandler
    {
        private Action<int> onTurn;
        private Selectable selectable;

        public static ChainlingDossierPageTurnRelay Attach(Selectable target, Action<int> onTurn)
        {
            if (target == null)
                return null;

            ChainlingDossierPageTurnRelay relay = target.GetComponent<ChainlingDossierPageTurnRelay>();
            if (relay == null)
                relay = target.gameObject.AddComponent<ChainlingDossierPageTurnRelay>();
            relay.selectable = target;
            relay.onTurn = onTurn;
            return relay;
        }

        public static void Detach(Selectable target)
        {
            if (target == null)
                return;
            ChainlingDossierPageTurnRelay relay = target.GetComponent<ChainlingDossierPageTurnRelay>();
            if (relay != null)
                relay.onTurn = null;
        }

        public void OnMove(AxisEventData eventData)
        {
            if (onTurn == null || eventData == null)
                return;

            if (selectable == null)
                selectable = GetComponent<Selectable>();

            Navigation navigation = selectable != null ? selectable.navigation : default;
            bool explicitNavigation = navigation.mode == Navigation.Mode.Explicit;
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (!explicitNavigation || navigation.selectOnLeft == null)
                        onTurn(-1);
                    break;
                case MoveDirection.Right:
                    if (!explicitNavigation || navigation.selectOnRight == null)
                        onTurn(1);
                    break;
            }
        }
    }
}
