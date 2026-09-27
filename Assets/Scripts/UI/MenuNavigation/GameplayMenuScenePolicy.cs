using System;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Central scene-wide policy for the gameplay player menu. Combat scenes are
    /// identified by their runtime controller rather than by a growing list of names.
    /// </summary>
    public static class GameplayMenuScenePolicy
    {
        private const string MainMenuSceneName = "main_menu";

        public static bool BlocksPlayerMenu(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            if (string.Equals(scene.name, MainMenuSceneName, StringComparison.Ordinal))
                return true;

            if (!scene.isLoaded)
                return false;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                if (root.GetComponentInChildren<GameplayMenuSceneBlocker>(true) != null)
                    return true;
            }

            return CombatSceneUtility.IsCombatScene(scene);
        }

        public static bool BlocksPlayerMenuInActiveScene()
        {
            return BlocksPlayerMenu(SceneManager.GetActiveScene());
        }
    }
}
