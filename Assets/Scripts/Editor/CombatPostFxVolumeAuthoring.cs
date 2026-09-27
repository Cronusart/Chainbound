using Chainbound.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chainbound.Combat.EditorTools
{
    /// <summary>
    /// Scaffolds a combat-owned Post FX volume controller on the selected presenter
    /// (or creates a child under the selection). Never touches scene Global Volume.
    /// </summary>
    public static class CombatPostFxVolumeAuthoring
    {
        [MenuItem("Tools/Chainbound/Combat/PostFX/Scaffold Combat Post FX Volume On Selection", false, 240)]
        public static void ScaffoldOnSelection()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
            {
                EditorUtility.DisplayDialog(
                    "Combat Post FX",
                    "Select the Combat2BattlefieldPresenter (or any combat root) first.",
                    "OK");
                return;
            }

            var controller = target.GetComponent<CombatPostFxVolumeController>();
            if (controller == null)
                controller = target.GetComponentInChildren<CombatPostFxVolumeController>(true);

            if (controller == null)
            {
                Undo.AddComponent<CombatPostFxVolumeController>(target);
                controller = target.GetComponent<CombatPostFxVolumeController>();
            }

            // Touch OwnedVolume so the child Volume is created under the controller.
            Volume owned = controller.OwnedVolume;
            int volumeLayer = LayerMask.NameToLayer("Volume");
            if (volumeLayer >= 0 && owned.gameObject.layer != volumeLayer)
            {
                Undo.RecordObject(owned.gameObject, "Set Combat Post FX Volume Layer");
                owned.gameObject.layer = volumeLayer;
            }

            owned.weight = 0f;
            owned.sharedProfile = null;
            Undo.RegisterCreatedObjectUndo(owned.gameObject, "Create CombatPostFxVolume");
            EditorUtility.SetDirty(owned);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(target);

            var presenter = target.GetComponent<Combat2BattlefieldPresenter>();
            if (presenter != null)
            {
                var so = new SerializedObject(presenter);
                SerializedProperty prop = so.FindProperty("postFxVolumeController");
                if (prop != null)
                {
                    prop.objectReferenceValue = controller;
                    so.ApplyModifiedProperties();
                }
            }

            var director = target.GetComponent<CombatPresentationDirector>();
            if (director == null)
                director = target.GetComponentInChildren<CombatPresentationDirector>(true);
            if (director != null)
            {
                director.SetPostFxVolumeController(controller);
                EditorUtility.SetDirty(director);
            }

            Selection.activeGameObject = owned.gameObject;
            Debug.Log(
                "[CombatPostFxVolumeAuthoring] Ensured CombatPostFxVolumeController + owned Volume on " +
                target.name +
                ". Priority=" + owned.priority +
                ", weight starts at 0. This is separate from scene Global Volume / TimeOfDay.",
                controller);
        }
    }
}
