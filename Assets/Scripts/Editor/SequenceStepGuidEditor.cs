#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector helpers for <see cref="SequenceStep"/> assets. Duplicating a step in the Project
/// window copies the hidden <c>stepGuid</c>, so execute-once tracking treats the copy as the same step.
/// </summary>
[CustomEditor(typeof(SequenceStep), true)]
public sealed class SequenceStepGuidEditor : Editor
{
    private SerializedProperty stepGuidProp;

    private void OnEnable()
    {
        stepGuidProp = serializedObject.FindProperty("stepGuid");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Execute-once identity", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Duplicating this asset (or copying YAML) copies the hidden Step GUID. " +
            "If Execute Once is on, the sequence may skip this step because the GUID already " +
            "matches a completed step. Use the button below to assign a new GUID to this asset only.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(stepGuidProp == null))
        {
            EditorGUILayout.SelectableLabel(
                stepGuidProp != null && !string.IsNullOrEmpty(stepGuidProp.stringValue)
                    ? stepGuidProp.stringValue
                    : "(empty — assigned on first access at runtime)",
                EditorStyles.textField,
                GUILayout.MinHeight(18f));

            if (GUILayout.Button("Regenerate Step GUID"))
            {
                if (stepGuidProp != null)
                {
                    Undo.RecordObject(target, "Regenerate Sequence Step GUID");
                    stepGuidProp.stringValue = System.Guid.NewGuid().ToString();
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[SequenceStep] New stepGuid for '{target.name}': {stepGuidProp.stringValue}");
                }
            }
        }
    }
}
#endif
