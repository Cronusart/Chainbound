using UnityEngine;
using UnityEditor;

namespace SignalJam.Camera.Editor
{
    /// <summary>
    /// Editor utility to validate and reset CameraProfile assets against a template.
    /// Ensures profiles stay in sync with canonical defaults and prevents accidental drift.
    /// </summary>
    public static class CameraProfileTemplateValidator
    {
        private const string MenuRoot = "Tools/Chainbound/Camera/Profiles/";

        [MenuItem(MenuRoot + "Validate All Against Template", false, 100)]
        public static void ValidateAll()
        {
            CameraProfile template = GetTemplateFromSelection();
            if (template == null)
            {
                EditorUtility.DisplayDialog("No Template", "Select a CameraProfile asset to use as template, then run again.", "OK");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:CameraProfile");
            var results = new System.Text.StringBuilder();
            results.AppendLine($"Template: {template.name}");
            results.AppendLine();

            int driftCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<CameraProfile>(path);
                if (profile == null || profile == template) continue;

                string diff = profile.GetDiffSummary(template);
                if (diff != "identical")
                {
                    driftCount++;
                    results.AppendLine($"[DRIFT] {profile.name}: {diff}");
                }
                else
                {
                    results.AppendLine($"[OK] {profile.name}");
                }
            }

            results.AppendLine();
            results.AppendLine($"Summary: {driftCount} profile(s) differ from template.");

            Debug.Log($"[CameraProfileTemplateValidator]\n{results}");
            EditorUtility.DisplayDialog("Validation Complete", results.ToString(), "OK");
        }

        [MenuItem(MenuRoot + "Reset Selected to Template", false, 101)]
        public static void ResetSelectedToTemplate()
        {
            CameraProfile template = GetTemplateFromSelection();
            if (template == null)
            {
                EditorUtility.DisplayDialog("No Template", "Select a CameraProfile asset to use as template.", "OK");
                return;
            }

            int count = 0;
            foreach (var obj in Selection.objects)
            {
                if (obj is CameraProfile profile && profile != template)
                {
                    Undo.RecordObject(profile, "Reset CameraProfile to Template");
                    profile.CopyFrom(template);
                    EditorUtility.SetDirty(profile);
                    count++;
                }
            }

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[CameraProfileTemplateValidator] Reset {count} profile(s) to template '{template.name}'.");
                EditorUtility.DisplayDialog("Reset Complete", $"Reset {count} profile(s) to template.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Nothing to Reset", "Select one or more CameraProfile assets (other than the template) to reset.", "OK");
            }
        }

        [MenuItem(MenuRoot + "Reset All to Template", false, 102)]
        public static void ResetAllToTemplate()
        {
            CameraProfile template = GetTemplateFromSelection();
            if (template == null)
            {
                EditorUtility.DisplayDialog("No Template", "Select a CameraProfile asset to use as template.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Reset All Profiles", $"Reset ALL CameraProfile assets to match '{template.name}'? This cannot be undone.", "Reset All", "Cancel"))
                return;

            var guids = AssetDatabase.FindAssets("t:CameraProfile");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<CameraProfile>(path);
                if (profile == null || profile == template) continue;

                Undo.RecordObject(profile, "Reset CameraProfile to Template");
                profile.CopyFrom(template);
                EditorUtility.SetDirty(profile);
                count++;
            }

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[CameraProfileTemplateValidator] Reset {count} profile(s) to template '{template.name}'.");
            }

            EditorUtility.DisplayDialog("Reset Complete", $"Reset {count} profile(s) to template.", "OK");
        }

        private static CameraProfile GetTemplateFromSelection()
        {
            if (Selection.activeObject is CameraProfile p)
                return p;

            foreach (var obj in Selection.objects)
            {
                if (obj is CameraProfile profile)
                    return profile;
            }

            return null;
        }
    }
}
