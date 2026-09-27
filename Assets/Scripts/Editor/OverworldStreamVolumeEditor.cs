#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring helpers for Link1's Loomville ↔ Jungle seam. Directives stay data
/// on the volume; this only fills the reusable Ensure/Release/SetActive list.
/// </summary>
[CustomEditor(typeof(OverworldStreamVolume))]
public sealed class OverworldStreamVolumeEditor : Editor
{
    private const string VolumeId = "link1_jungle_prefetch";
    private const string BufferChildName = "BufferZone";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Inner collider = load Jungle, make Link1 active, unload Loomville.\n" +
            "Child BufferZone = town-side hysteresis. Offset it toward Loomville and do not let it stick past the jungle face. " +
            "Walking back and leaving the buffer (not the inner volume) reloads Loomville and unloads Jungle.\n" +
            "Do not use SceneTransitionTrigger at this seam.",
            MessageType.Info);

        OverworldStreamVolume volume = (OverworldStreamVolume)target;

        if (GUILayout.Button("Apply Loomville ↔ Jungle Seam Directives"))
        {
            Undo.RecordObject(volume, "Apply Loomville-Jungle stream directives");
            ApplyLoomvilleJungleSeam(serializedObject);
        }

        if (GUILayout.Button("Create Town-Side Buffer Child"))
        {
            CreateTownSideBuffer(volume);
        }
    }

    [MenuItem("GameObject/Signal Jam/Configure Overworld Stream Volume (Loomville-Jungle)", false, 11)]
    private static void ConfigureSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Overworld Stream Volume",
                "Select the TransitionZone collider in Link1 first.",
                "OK");
            return;
        }

        Collider trigger = selected.GetComponent<Collider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(selected);
        }

        trigger.isTrigger = true;

        OverworldStreamVolume volume = selected.GetComponent<OverworldStreamVolume>();
        if (volume == null)
            volume = Undo.AddComponent<OverworldStreamVolume>(selected);

        SerializedObject serialized = new SerializedObject(volume);
        ApplyLoomvilleJungleSeam(serialized);
        CreateTownSideBuffer(volume);
        Selection.activeGameObject = selected;
        EditorUtility.SetDirty(selected);
        EditorUtility.DisplayDialog(
            "Overworld Stream Volume",
            "Configured inner directives and a town-side BufferZone child. Stretch the child toward Loomville so leaving it (not the inner box) restores town and drops Jungle.",
            "OK");
    }

    internal static void ApplyLoomvilleJungleSeam(SerializedObject serialized)
    {
        serialized.Update();
        serialized.FindProperty("streamVolumeId").stringValue = VolumeId;
        serialized.FindProperty("playerTag").stringValue = "Player";

        SerializedProperty entered = serialized.FindProperty("onPlayerEntered");
        entered.arraySize = 3;
        SetDirective(entered.GetArrayElementAtIndex(0), OverworldStreamVolume.Operation.EnsureChunk, "Jungle");
        SetDirective(entered.GetArrayElementAtIndex(1), OverworldStreamVolume.Operation.SetActiveChunk, "Link1");
        SetDirective(entered.GetArrayElementAtIndex(2), OverworldStreamVolume.Operation.ReleaseChunk, "Loomville");

        serialized.FindProperty("onPlayerExited").arraySize = 0;

        SerializedProperty bufferExited = serialized.FindProperty("onBufferExited");
        bufferExited.arraySize = 2;
        SetDirective(bufferExited.GetArrayElementAtIndex(0), OverworldStreamVolume.Operation.EnsureChunk, "Loomville");
        SetDirective(bufferExited.GetArrayElementAtIndex(1), OverworldStreamVolume.Operation.ReleaseChunk, "Jungle");

        serialized.ApplyModifiedProperties();
    }

    private static void SetDirective(
        SerializedProperty directive,
        OverworldStreamVolume.Operation operation,
        string chunkSceneId)
    {
        directive.FindPropertyRelative("operation").enumValueIndex = (int)operation;
        directive.FindPropertyRelative("chunkSceneId").stringValue = chunkSceneId;
    }

    private static void CreateTownSideBuffer(OverworldStreamVolume volume)
    {
        Transform existing = volume.transform.Find(BufferChildName);
        GameObject child = existing != null ? existing.gameObject : null;
        if (child == null)
        {
            child = new GameObject(BufferChildName);
            Undo.RegisterCreatedObjectUndo(child, "Create stream buffer");
            child.transform.SetParent(volume.transform, false);
        }

        BoxCollider parentBox = volume.GetComponent<BoxCollider>();
        BoxCollider childBox = child.GetComponent<BoxCollider>();
        if (childBox == null)
            childBox = Undo.AddComponent<BoxCollider>(child);

        childBox.isTrigger = true;
        if (parentBox != null)
        {
            float extra = parentBox.size.z * 0.85f;
            float inset = Mathf.Max(0.25f, parentBox.size.z * 0.08f);
            childBox.center = parentBox.center + new Vector3(0f, 0f, -(extra + inset) * 0.5f);
            childBox.size = new Vector3(
                parentBox.size.x * 1.08f,
                parentBox.size.y * 1.08f,
                parentBox.size.z + extra - inset);
        }

        OverworldStreamBuffer buffer = child.GetComponent<OverworldStreamBuffer>();
        if (buffer == null)
            Undo.AddComponent<OverworldStreamBuffer>(child);

        EditorUtility.SetDirty(child);
        EditorUtility.SetDirty(volume);
    }
}
#endif
