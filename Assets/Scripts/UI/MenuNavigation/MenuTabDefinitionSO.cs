using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    public enum PlayerMenuTabLock
    {
        None,
        HandbookUnlock,
    }

    public enum PlayerMenuTabScreenResolveMode
    {
        /// <summary>Use an embedded/scene instance when valid; otherwise prefab/auto-resolve.</summary>
        PreferEmbeddedThenPrefab = 0,
        /// <summary>Always instantiate from prefab/auto-resolve (ignore embedded slot unless it already matches).</summary>
        PrefabOnly = 1,
        /// <summary>Only use the embedded/scene instance; never instantiate.</summary>
        EmbeddedOnly = 2,
    }

    [CreateAssetMenu(fileName = "MenuTab_", menuName = "Chainbound/UI/Menu/Menu Tab Definition")]
    public class MenuTabDefinitionSO : ScriptableObject
    {
        [SerializeField] private string tabId = "";
        [SerializeField] private string displayName = "";
        [TextArea(2, 4)]
        [SerializeField] private string description = "";
        [SerializeField] private Sprite icon;
        [SerializeField] private PlayerMenuTabLock lockRule = PlayerMenuTabLock.None;
        [SerializeField] private string lockedLabel = "Locked";
        [SerializeField] private Color accentColor = Color.white;

        [Header("Tab Screen")]
        [Tooltip("Authoring prefab for this tab. Use Tools/Chainbound/UI/Sync Player Menu Tab Screens to embed it on PlayerMenuRoot.")]
        [SerializeField] private MenuScreen tabScreenPrefab;
        [SerializeField] private PlayerMenuTabScreenResolveMode screenResolveMode = PlayerMenuTabScreenResolveMode.PreferEmbeddedThenPrefab;
        [Tooltip("Deprecated at runtime. Tab bodies should be embedded on PlayerMenuRoot.tabScreens.")]
        [SerializeField] private bool autoResolveTabScreen;

        public string TabId => tabId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? tabId : displayName;
        public string Description => string.IsNullOrWhiteSpace(description) ? DisplayName : description;
        public Sprite Icon => icon;
        public PlayerMenuTabLock LockRule => lockRule;
        public string LockedLabel => lockedLabel;
        public Color AccentColor => accentColor;
        public MenuScreen TabScreenPrefab => tabScreenPrefab;
        public PlayerMenuTabScreenResolveMode ScreenResolveMode => screenResolveMode;
        public bool AutoResolveTabScreen => autoResolveTabScreen;
    }
}
