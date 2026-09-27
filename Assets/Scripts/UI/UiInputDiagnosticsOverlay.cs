using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

/// <summary>
/// Dev-only overlay: EventSystem / InputSystemUIInputModule health and raycast stack on click.
/// Helps distinguish "nothing receives pointer" (blocking UI, no EventSystem) from keyboard wiring.
///
/// Setup: Add to a persistent object (e.g. GameServices root or SequenceRunner). Toggle with F11.
/// </summary>
[DisallowMultipleComponent]
public class UiInputDiagnosticsOverlay : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F11;
    [SerializeField] private float panelWidth = 420f;
    [SerializeField] private float panelHeight = 320f;
    [SerializeField] private float panelX = 12f;
    [SerializeField] private float panelY = 340f;

    [Header("Raycast log")]
    [Tooltip("Log full raycast stack to the Console when primary pointer goes down.")]
    [SerializeField] private bool logRaycastStackOnClick = true;

    [Tooltip("Only log when the top hit path contains this substring (e.g. Keyboard). Empty = always log.")]
    [SerializeField] private string logOnlyWhenTopHitContains = "";

    private bool _overlayVisible = true;
    private readonly List<RaycastResult> _raycastScratch = new List<RaycastResult>(32);
    private string _lastRaycastSummary = "";

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _overlayVisible = !_overlayVisible;

        if (!logRaycastStackOnClick || EventSystem.current == null)
            return;

        if (!TryGetPrimaryPointerDown(out Vector2 screenPos))
            return;

        _raycastScratch.Clear();
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        EventSystem.current.RaycastAll(ped, _raycastScratch);

        if (_raycastScratch.Count == 0)
        {
            _lastRaycastSummary = "(no UI raycast hits)";
            Debug.LogWarning("[UiInputDiagnostics] Raycast: 0 hits at " + screenPos + ". Check EventSystem, GraphicRaycaster, or blocking mask.");
            return;
        }

        var top = _raycastScratch[0];
        string topPath = GetHierarchyPath(top.gameObject);
        if (!string.IsNullOrEmpty(logOnlyWhenTopHitContains) &&
            topPath.IndexOf(logOnlyWhenTopHitContains, System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        var sb = new StringBuilder(512);
        sb.AppendLine($"[UiInputDiagnostics] Raycast hits={_raycastScratch.Count} at {screenPos}");
        for (int i = 0; i < _raycastScratch.Count && i < 12; i++)
        {
            RaycastResult r = _raycastScratch[i];
            string path = GetHierarchyPath(r.gameObject);
            Canvas c = r.gameObject.GetComponentInParent<Canvas>();
            int sort = c != null ? c.sortingOrder : 0;
            string graphic = r.module is GraphicRaycaster ? "GraphicRaycaster" : r.module != null ? r.module.GetType().Name : "(null)";
            sb.AppendLine($"  [{i}] depth={r.depth} sort={sort} mod={graphic} -> {path}");
        }

        _lastRaycastSummary = topPath;
        Debug.Log(sb.ToString());
    }

    private static bool TryGetPrimaryPointerDown(out Vector2 screenPos)
    {
        screenPos = default;

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                return true;
            }
        }

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                screenPos = touch.position.ReadValue();
                return true;
            }
        }

        return false;
    }

    private void OnGUI()
    {
        if (!showOverlay || !_overlayVisible)
            return;

        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight));
        GUI.Box(new Rect(0, 0, panelWidth, panelHeight), "");
        GUILayout.BeginVertical(GUILayout.Width(panelWidth - 10));

        GUILayout.Label("UI Input Diagnostics (F11)", new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });

        EventSystem es = EventSystem.current;
        if (es == null)
        {
            GUILayout.Label("EventSystem.current: NULL — UI clicks will not work.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"EventSystem: {es.gameObject.name} (enabled={es.enabled})");

        var uiMod = es.GetComponent<InputSystemUIInputModule>();
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (uiMod != null)
            GUILayout.Label("Module: InputSystemUIInputModule");
        else if (legacy != null)
            GUILayout.Label("Module: StandaloneInputModule (legacy)");
        else
            GUILayout.Label("Module: MISSING — add InputSystemUIInputModule or StandaloneInputModule.");

        if (uiMod != null)
        {
            if (uiMod.actionsAsset != null)
                GUILayout.Label($"actionsAsset: {uiMod.actionsAsset.name}");
            else
                GUILayout.Label("actionsAsset: (null — use Assign Default Actions or wire InputSystem_Actions)");

            var map = uiMod.actionsAsset != null ? uiMod.actionsAsset.FindActionMap("UI", false) : null;
            if (map != null)
                GUILayout.Label($"UI action map enabled: {map.enabled}");
        }

        int esCount = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (esCount > 1)
            GUILayout.Label($"WARNING: {esCount} EventSystems active — duplicates break UI.");

        GUILayout.Space(6);
        GUILayout.Label("Last click top hit path:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        GUILayout.Label(string.IsNullOrEmpty(_lastRaycastSummary) ? "(none yet)" : _lastRaycastSummary, new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true });

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return "";

        Transform t = go.transform;
        var sb = new StringBuilder(128);
        while (t != null)
        {
            if (sb.Length > 0)
                sb.Insert(0, "/");
            sb.Insert(0, t.name);
            t = t.parent;
        }

        return sb.ToString();
    }
}

#else

/// <summary>
/// Stripped in release builds; use DEVELOPMENT_BUILD or Editor to compile the full overlay.
/// </summary>
public sealed class UiInputDiagnosticsOverlay : MonoBehaviour
{
}

#endif
