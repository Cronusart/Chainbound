using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for GrassPatchManager with interaction tuning and blade management utilities.
/// </summary>
[CustomEditor(typeof(GrassPatchManager))]
[CanEditMultipleObjects]
public class GrassPatchEditor : Editor
{
    private SerializedProperty playerTagProp;
    private SerializedProperty playerObjectProp;
    private SerializedProperty interactRadiusProp;
    private SerializedProperty usePivotFalloffProp;
    private SerializedProperty pushStrengthXZProp;
    private SerializedProperty bendStrengthProp;
    private SerializedProperty downStrengthProp;
    private SerializedProperty maxDownOffsetProp;
    private SerializedProperty heightMaskPowerProp;
    private SerializedProperty updateRateHzProp;
    private SerializedProperty decayTimeProp;
    private SerializedProperty smoothDecayProp;
    private SerializedProperty autoCollectBladesProp;
    private SerializedProperty shaderNameFilterProp;
    private SerializedProperty enableForwardOffsetProp;
    private SerializedProperty forwardOffsetProp;
    private SerializedProperty enableDebugLogsProp;
    private SerializedProperty drawDebugGizmosProp;
    
    private void OnEnable()
    {
        playerTagProp = serializedObject.FindProperty("playerTag");
        playerObjectProp = serializedObject.FindProperty("playerObject");
        interactRadiusProp = serializedObject.FindProperty("interactRadius");
        usePivotFalloffProp = serializedObject.FindProperty("usePivotFalloff");
        pushStrengthXZProp = serializedObject.FindProperty("pushStrengthXZ");
        bendStrengthProp = serializedObject.FindProperty("bendStrength");
        downStrengthProp = serializedObject.FindProperty("downStrength");
        maxDownOffsetProp = serializedObject.FindProperty("maxDownOffset");
        heightMaskPowerProp = serializedObject.FindProperty("heightMaskPower");
        updateRateHzProp = serializedObject.FindProperty("updateRateHz");
        decayTimeProp = serializedObject.FindProperty("decayTime");
        smoothDecayProp = serializedObject.FindProperty("smoothDecay");
        autoCollectBladesProp = serializedObject.FindProperty("autoCollectBlades");
        shaderNameFilterProp = serializedObject.FindProperty("shaderNameFilter");
        enableForwardOffsetProp = serializedObject.FindProperty("enableForwardOffset");
        forwardOffsetProp = serializedObject.FindProperty("forwardOffset");
        enableDebugLogsProp = serializedObject.FindProperty("enableDebugLogs");
        drawDebugGizmosProp = serializedObject.FindProperty("drawDebugGizmos");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grass Patch Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Manages grass interaction for a group of blade renderers. Use this to configure interaction settings and manage blades.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Blade Collection Section
        EditorGUILayout.LabelField("Blade Collection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(autoCollectBladesProp);
        EditorGUILayout.PropertyField(shaderNameFilterProp);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Collect Blades Now"))
        {
            CollectBlades();
        }
        if (GUILayout.Button("Refresh Base Positions"))
        {
            RefreshBasePositions();
        }
        EditorGUILayout.EndHorizontal();
        
        // Show blade count
        if (Application.isPlaying)
        {
            foreach (Object targetObj in targets)
            {
                GrassPatchManager manager = targetObj as GrassPatchManager;
                if (manager != null)
                {
                    EditorGUILayout.HelpBox($"Active Blades: {manager.BladeCount}", MessageType.None);
                    break;
                }
            }
        }
        
        EditorGUILayout.Space();
        
        // Player Detection Section
        EditorGUILayout.LabelField("Player Detection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(playerTagProp);
        EditorGUILayout.PropertyField(playerObjectProp);
        
        EditorGUILayout.Space();
        
        // Interaction Settings Section
        EditorGUILayout.LabelField("Interaction Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(interactRadiusProp);
        EditorGUILayout.PropertyField(usePivotFalloffProp);
        if (usePivotFalloffProp != null && usePivotFalloffProp.boolValue)
        {
            EditorGUILayout.HelpBox("Pivot falloff treats each child renderer as one plant. Space bush wrappers ~7 units apart and keep Interact Radius under half that spacing.", MessageType.Info);
        }
        EditorGUILayout.PropertyField(pushStrengthXZProp);
        EditorGUILayout.PropertyField(bendStrengthProp);
        EditorGUILayout.PropertyField(downStrengthProp);
        EditorGUILayout.PropertyField(maxDownOffsetProp);
        EditorGUILayout.PropertyField(heightMaskPowerProp);
        EditorGUILayout.HelpBox($"Height Mask Power: {heightMaskPowerProp.floatValue:F1} (1.0 = linear, 2.0+ = only tips move)", MessageType.None);
        
        EditorGUILayout.Space();
        
        // Performance Section
        EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(updateRateHzProp);
        EditorGUILayout.HelpBox($"Updates at {updateRateHzProp.floatValue} Hz ({1f / updateRateHzProp.floatValue:F3}s intervals)", MessageType.None);
        
        EditorGUILayout.Space();
        
        // Decay Settings
        EditorGUILayout.LabelField("Decay Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(decayTimeProp);
        EditorGUILayout.PropertyField(smoothDecayProp);
        
        EditorGUILayout.Space();
        
        // Forward Offset Section
        EditorGUILayout.LabelField("Forward Offset", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableForwardOffsetProp);
        if (enableForwardOffsetProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(forwardOffsetProp);
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        
        // Debug Section
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableDebugLogsProp);
        EditorGUILayout.PropertyField(drawDebugGizmosProp);
        
        EditorGUILayout.Space();
        
        // Test Controls (Play Mode Only)
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Test Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use these buttons to test grass interaction in play mode.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Interaction (North)"))
            {
                TestInteraction(Vector3.forward, Vector3.forward * 2f);
            }
            if (GUILayout.Button("Test Interaction (East)"))
            {
                TestInteraction(Vector3.right, Vector3.right * 2f);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Interaction (South)"))
            {
                TestInteraction(Vector3.back, Vector3.back * 2f);
            }
            if (GUILayout.Button("Test Interaction (West)"))
            {
                TestInteraction(Vector3.left, Vector3.left * 2f);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset All Blades"))
            {
                ResetAllBlades();
            }
            if (GUILayout.Button("Set Max Strength"))
            {
                SetMaxStrength();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void CollectBlades()
    {
        serializedObject.ApplyModifiedProperties();
        
        foreach (Object targetObj in targets)
        {
            GrassPatchManager manager = targetObj as GrassPatchManager;
            if (manager == null) continue;
            
            manager.CollectBladeRenderers();
            EditorUtility.SetDirty(manager);
        }
        
        Debug.Log($"Collected blade renderers for {targets.Length} manager(s)");
    }
    
    private void RefreshBasePositions()
    {
        serializedObject.ApplyModifiedProperties();
        
        foreach (Object targetObj in targets)
        {
            GrassPatchManager manager = targetObj as GrassPatchManager;
            if (manager == null) continue;
            
            manager.RefreshBladeBasePositions();
            EditorUtility.SetDirty(manager);
        }
        
        Debug.Log($"Refreshed base positions for {targets.Length} manager(s)");
    }
    
    private void TestInteraction(Vector3 direction, Vector3 velocity)
    {
        foreach (Object targetObj in targets)
        {
            GrassPatchManager manager = targetObj as GrassPatchManager;
            if (manager == null) continue;
            
            Vector3 center = manager.transform.position;
            float pushStr = pushStrengthXZProp.floatValue;
            float bendStr = bendStrengthProp.floatValue;
            float downStr = downStrengthProp.floatValue;
            
            // Apply test interaction to all blades
            Renderer[] renderers = manager.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend != null && rend.sharedMaterial != null)
                {
                    string shaderName = rend.sharedMaterial.shader != null ? rend.sharedMaterial.shader.name : "";
                    if (shaderName.Contains("GrassBend"))
                    {
                        manager.SetBladeInteraction(rend, center, velocity, pushStr, bendStr, downStr);
                    }
                }
            }
        }
        
        Debug.Log($"Applied test interaction to grass patches");
    }
    
    private void ResetAllBlades()
    {
        foreach (Object targetObj in targets)
        {
            GrassPatchManager manager = targetObj as GrassPatchManager;
            if (manager == null) continue;
            
            manager.ResetAllBlades();
        }
        
        Debug.Log($"Reset all blades for {targets.Length} manager(s)");
    }
    
    private void SetMaxStrength()
    {
        foreach (Object targetObj in targets)
        {
            GrassPatchManager manager = targetObj as GrassPatchManager;
            if (manager == null) continue;
            
            manager.SetInteractionStrength(1f, 1f, 1f);
        }
        
        Debug.Log($"Set max interaction strength for {targets.Length} manager(s)");
    }
}
