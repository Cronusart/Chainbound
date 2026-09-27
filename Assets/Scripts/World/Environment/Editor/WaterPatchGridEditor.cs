using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for WaterPatchGrid with regeneration controls and info display.
/// </summary>
[CustomEditor(typeof(WaterPatchGrid))]
[CanEditMultipleObjects]
public class WaterPatchGridEditor : Editor
{
    private SerializedProperty widthTilesProp;
    private SerializedProperty heightTilesProp;
    private SerializedProperty tileWorldSizeProp;
    private SerializedProperty pixelScaleProp;
    private SerializedProperty tileSubdivisionsProp;
    private SerializedProperty sharedMaterialProp;
    private SerializedProperty generateOnAwakeProp;
    private SerializedProperty generateInEditModeProp;
    private SerializedProperty planeAxisProp;
    private SerializedProperty anchorProp;
    private SerializedProperty twoSidedProp;
    private SerializedProperty originOffsetProp;
    private SerializedProperty useSpriteAnimationProp;
    private SerializedProperty spriteAnimationSequenceProp;
    private SerializedProperty spriteAnimationSpeedProp;
    
    private void OnEnable()
    {
        widthTilesProp = serializedObject.FindProperty("widthTiles");
        heightTilesProp = serializedObject.FindProperty("heightTiles");
        tileWorldSizeProp = serializedObject.FindProperty("tileWorldSize");
        pixelScaleProp = serializedObject.FindProperty("pixelScale");
        tileSubdivisionsProp = serializedObject.FindProperty("tileSubdivisions");
        sharedMaterialProp = serializedObject.FindProperty("sharedMaterial");
        generateOnAwakeProp = serializedObject.FindProperty("generateOnAwake");
        generateInEditModeProp = serializedObject.FindProperty("generateInEditMode");
        planeAxisProp = serializedObject.FindProperty("planeAxis");
        anchorProp = serializedObject.FindProperty("anchor");
        twoSidedProp = serializedObject.FindProperty("twoSided");
        originOffsetProp = serializedObject.FindProperty("originOffset");
        useSpriteAnimationProp = serializedObject.FindProperty("useSpriteAnimation");
        spriteAnimationSequenceProp = serializedObject.FindProperty("spriteAnimationSequence");
        spriteAnimationSpeedProp = serializedObject.FindProperty("spriteAnimationSpeed");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        WaterPatchGrid grid = (WaterPatchGrid)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Water Patch Grid", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Generates a rectangular grid of water tiles using a shared material for optimal batching.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Grid Info Section (Read-only)
        EditorGUILayout.LabelField("Grid Info", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        int tileCount = grid.GetTileCount();
        float finalTileSize = grid.GetFinalTileSize();
        EditorGUILayout.IntField("Total Tiles", tileCount);
        EditorGUILayout.FloatField("Final Tile Size", finalTileSize);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
        
        // Grid Configuration
        EditorGUILayout.LabelField("Grid Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(widthTilesProp);
        EditorGUILayout.PropertyField(heightTilesProp);
        EditorGUILayout.PropertyField(tileWorldSizeProp, new GUIContent("Tile World Size", "Base world size before pixel scale"));
        EditorGUILayout.PropertyField(pixelScaleProp, new GUIContent("Pixel Scale", "Scale multiplier for pixel art (default: 5)"));
        EditorGUILayout.PropertyField(tileSubdivisionsProp, new GUIContent("Tile Subdivisions", "Quads per tile edge. Keep ocean grids at 1; use 3+ for local vertex effects."));
        
        // Show calculated final size
        float calculatedFinalSize = tileWorldSizeProp.floatValue * pixelScaleProp.floatValue;
        int subdivisions = Mathf.Max(1, tileSubdivisionsProp.intValue);
        EditorGUILayout.HelpBox(
            $"Final tile size: {calculatedFinalSize:F2} world units | Vertex spacing: {calculatedFinalSize / subdivisions:F2}",
            MessageType.None);
        EditorGUILayout.Space();
        
        // Material
        EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sharedMaterialProp, new GUIContent("Shared Material", "Material used by all tiles (shared reference for batching)"));
        if (sharedMaterialProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("No material assigned. Grid will not generate without a material.", MessageType.Warning);
        }
        EditorGUILayout.Space();
        
        // Generation Settings
        EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(generateOnAwakeProp, new GUIContent("Generate On Awake", "Generate grid when entering play mode"));
        EditorGUILayout.PropertyField(generateInEditModeProp, new GUIContent("Generate In Edit Mode", "Auto-regenerate when values change in editor"));
        EditorGUILayout.Space();
        
        // Orientation
        EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(planeAxisProp, new GUIContent("Plane Axis", "XZ for horizontal (top-down), XY for vertical (2D)"));
        EditorGUILayout.PropertyField(anchorProp, new GUIContent("Anchor", "Center: grid centered around parent. BottomLeft: grid starts at parent position"));
        EditorGUILayout.PropertyField(twoSidedProp, new GUIContent("Two Sided", "Render both sides of tiles (requires material/shader support)"));
        EditorGUILayout.Space();
        
        // Offset
        EditorGUILayout.LabelField("Offset", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(originOffsetProp, new GUIContent("Origin Offset", "Offset the grid origin position"));
        EditorGUILayout.Space();
        
        // Sprite Animation (Optional)
        EditorGUILayout.LabelField("Sprite Animation (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useSpriteAnimationProp, new GUIContent("Use Sprite Animation", "Enable frame-by-frame sprite animation instead of shader scrolling. Less performant but allows discrete frames."));
        
        if (useSpriteAnimationProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spriteAnimationSequenceProp, new GUIContent("Sprite Sequence", "Sprite frames for animation (8-12 frames recommended)"));
            EditorGUILayout.PropertyField(spriteAnimationSpeedProp, new GUIContent("Animation Speed (FPS)", "Frames per second for sprite animation"));
            
            // Show info about sprite animation
            SerializedProperty spritesArray = spriteAnimationSequenceProp.FindPropertyRelative("sprites");
            int spriteCount = spritesArray != null ? spritesArray.arraySize : 0;
            if (spriteCount > 0)
            {
                EditorGUILayout.HelpBox($"Sprite animation: {spriteCount} frames at {spriteAnimationSpeedProp.floatValue} FPS", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("No sprites assigned. Sprite animation will not work.", MessageType.Warning);
            }
            
            EditorGUILayout.HelpBox(
                "⚠️ Performance Note: Sprite animation uses MaterialPropertyBlock updates per frame, which is less performant than shader-based scrolling. " +
                "Shader scrolling (default) is recommended for best performance.",
                MessageType.Info
            );
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Using shader-based scrolling (default). This is the most performant option - animation happens entirely on GPU with no CPU overhead.",
                MessageType.Info
            );
        }
        EditorGUILayout.Space();
        
        // Action Buttons
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f); // Light blue
        if (GUILayout.Button("Regenerate Grid", GUILayout.Height(30)))
        {
            foreach (Object targetObj in targets)
            {
                WaterPatchGrid targetGrid = targetObj as WaterPatchGrid;
                if (targetGrid != null)
                {
                    targetGrid.RegenerateGrid();
                    EditorUtility.SetDirty(targetGrid);
                }
            }
            
            // Refresh scene view
            SceneView.RepaintAll();
        }
        
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Light red
        if (GUILayout.Button("Clear Grid", GUILayout.Height(30)))
        {
            foreach (Object targetObj in targets)
            {
                WaterPatchGrid targetGrid = targetObj as WaterPatchGrid;
                if (targetGrid != null)
                {
                    targetGrid.ClearGrid();
                    EditorUtility.SetDirty(targetGrid);
                }
            }
            
            // Refresh scene view
            SceneView.RepaintAll();
        }
        
        GUI.backgroundColor = Color.white; // Reset
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Performance Notes
        EditorGUILayout.LabelField("Performance Notes", EditorStyles.boldLabel);
        string performanceNote = "• All tiles share the same material reference (sharedMaterial) for optimal GPU batching\n";
        if (useSpriteAnimationProp.boolValue)
        {
            performanceNote += "• ⚠️ Sprite animation enabled: Uses MaterialPropertyBlock updates per frame (less performant)\n";
            performanceNote += "• Consider using shader-based scrolling for better performance\n";
        }
        else
        {
            performanceNote += "• Shader-based scrolling: No per-frame CPU updates (most performant)\n";
        }
        performanceNote += "• Generation happens at edit-time or on-demand\n";
        performanceNote += "• Tiles are generated on 'PartialOccluders' layer\n";
        performanceNote += "• Keep tile count reasonable for your target platform";
        
        EditorGUILayout.HelpBox(performanceNote, MessageType.Info);
        
        serializedObject.ApplyModifiedProperties();
    }
}

