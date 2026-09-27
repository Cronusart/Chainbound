#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps DetailBody/DevGrantStrip visible in the prefab and lays out dev grant controls in one row.
/// </summary>
public static class CandyMachineDevGrantStripLayout
{
    public const string StripPath = "SafeAreaPanel/SafeContent/DetailBody/DevGrantStrip";

    [MenuItem("Tools/Chainbound/UI/Apply Candy Machine Dev Grant Strip Layout")]
    public static void ApplyToPrefabMenu()
    {
        const string prefabPath = CandyMachineAuthoring.PrefabPath;
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform strip = root.transform.Find(StripPath);
            if (strip == null)
            {
                Debug.LogError($"[CandyMachineDevGrantStripLayout] Missing {StripPath}.");
                return;
            }

            Apply(strip);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[CandyMachineDevGrantStripLayout] DevGrantStrip layout applied to CandyMachinePanel.prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void Apply(Transform strip)
    {
        if (strip == null)
            return;

        CandyMachineDevGrantStripLayoutUtility.ApplyStripRoot(strip as RectTransform);

        Button init = FindButton(strip, "InitializeSpineButton");
        Button ram = FindButton(strip, "GrantRamButton");
        Button known = FindButton(strip, "GrantKnownButton");
        Button clear = FindButton(strip, "ClearButton");
        var status = strip.Find("GrantStatus")?.GetComponent<TMPro.TMP_Text>();

        if (init == null && ram != null)
        {
            init = Object.Instantiate(ram, strip, false);
            init.name = "InitializeSpineButton";
            var label = init.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null)
                label.text = "INIT SPINE";
        }

        CandyMachineDevGrantStripLayoutUtility.LayoutControl(init, 0.02f, 0.17f);
        CandyMachineDevGrantStripLayoutUtility.LayoutControl(ram, 0.18f, 0.33f);
        CandyMachineDevGrantStripLayoutUtility.LayoutControl(known, 0.34f, 0.49f);
        CandyMachineDevGrantStripLayoutUtility.LayoutControl(clear, 0.50f, 0.62f);
        if (status != null)
            CandyMachineDevGrantStripLayoutUtility.LayoutControl(status.rectTransform, 0.63f, 0.98f);

        var panel = strip.GetComponent<CandyMachineDevGrantPanel>();
        if (panel != null)
        {
            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("initializeSpineButton").objectReferenceValue = init;
            so.FindProperty("grantRamButton").objectReferenceValue = ram;
            so.FindProperty("grantKnownButton").objectReferenceValue = known;
            so.FindProperty("clearButton").objectReferenceValue = clear;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        strip.gameObject.SetActive(true);
    }

    private static Button FindButton(Transform strip, string name)
    {
        Transform child = strip.Find(name);
        return child != null ? child.GetComponent<Button>() : null;
    }
}
#endif
