using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(MoveDefinitionSO))]
    public sealed class MoveDefinitionSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "wallPresentation")
                    continue;

                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(iterator, includeChildren: true);
                }
            }

            DrawWallPresentation();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWallPresentation()
        {
            var move = target as MoveDefinitionSO;
            SerializedProperty wallPresentation = serializedObject.FindProperty("wallPresentation");
            if (move == null || wallPresentation == null)
                return;

            bool isWallMove = CombatWallStateUtility.MoveDefinitionPlacesWall(move);
            bool hasWallArt = HasWallPresentationData(wallPresentation);

            if (!isWallMove && !hasWallArt)
                return;

            EditorGUILayout.Space();
            if (isWallMove)
            {
                EditorGUILayout.PropertyField(wallPresentation, includeChildren: true);

                SerializedProperty wallSfxId = wallPresentation.FindPropertyRelative("wallSfxId");
                if (wallSfxId != null && string.IsNullOrWhiteSpace(wallSfxId.stringValue))
                {
                    EditorGUILayout.HelpBox(
                        "Assign a wallSfxId so wall audio resolves to a material/source catalog entry instead of the legacy source-tag fallback.",
                        MessageType.Warning);
                }

                SerializedProperty sprite = wallPresentation.FindPropertyRelative("wallSprite");
                if (sprite != null && sprite.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "This wall move has no move-specific wall sprite. Runtime uses CombatWallVisualProfileSO default art at profile spriteSize.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This move has wall presentation data but is not detected as a wall move. Add a wall utility effect/tag or clear this orphan wall art.",
                    MessageType.Warning);
                EditorGUILayout.PropertyField(wallPresentation, includeChildren: true);
            }
        }

        private static bool HasWallPresentationData(SerializedProperty wallPresentation)
        {
            SerializedProperty sprite = wallPresentation.FindPropertyRelative("wallSprite");
            SerializedProperty material = wallPresentation.FindPropertyRelative("wallMaterial");
            SerializedProperty wallSfxId = wallPresentation.FindPropertyRelative("wallSfxId");
            SerializedProperty scale = wallPresentation.FindPropertyRelative("wallSpriteScale");
            SerializedProperty tint = wallPresentation.FindPropertyRelative("wallTint");

            return (wallSfxId != null && !string.IsNullOrWhiteSpace(wallSfxId.stringValue))
                || (sprite != null && sprite.objectReferenceValue != null)
                || (material != null && material.objectReferenceValue != null)
                || (scale != null && scale.vector2Value != Vector2.zero)
                || (tint != null && tint.colorValue.a > 0f);
        }
    }
}
