using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Optional scene marker for authored scenes that must never expose the gameplay
    /// player menu. Combat scenes are detected automatically from their battle controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayMenuSceneBlocker : MonoBehaviour
    {
    }
}
