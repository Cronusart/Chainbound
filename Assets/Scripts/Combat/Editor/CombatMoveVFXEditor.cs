using Chainbound.Combat;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(CombatMoveVFX))]
    internal sealed class CombatMoveVFXEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CombatMoveVFX moveVfx = (CombatMoveVFX)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Sprite Playback", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Current Frame", moveVfx.CurrentSpriteFrame);
                EditorGUILayout.IntField("Frame Count", moveVfx.SpriteFrameCount);
                EditorGUILayout.ObjectField(
                    "Current Sprite",
                    moveVfx.CurrentSpriteFrameSprite,
                    typeof(Sprite),
                    false);

                Texture authoredTexture = moveVfx.CurrentSpriteFrameSprite != null
                    ? moveVfx.CurrentSpriteFrameSprite.texture
                    : null;
                EditorGUILayout.ObjectField("Authored Texture", authoredTexture, typeof(Texture), false);
                EditorGUILayout.ObjectField(
                    "Applied Slot Texture",
                    moveVfx.AppliedSpriteSourceTexture,
                    typeof(Texture),
                    false);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Runtime frame values populate on a spawned or playing move VFX instance.",
                    MessageType.None);
            }
        }

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying && target != null;
        }
    }
}
