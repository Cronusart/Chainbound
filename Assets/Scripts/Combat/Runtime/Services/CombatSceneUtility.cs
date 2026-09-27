using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chainbound.Combat
{
    /// <summary>
    /// Identifies combat scenes from their runtime authority rather than scene names.
    /// Shared by presentation, overworld persistence, and UI adapters.
    /// </summary>
    public static class CombatSceneUtility
    {
        public static bool IsCombatScene(Scene scene)
        {
            return FindBattleController(scene) != null;
        }

        public static CombatBattleController FindBattleController(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                CombatBattleController battle = root.GetComponentInChildren<CombatBattleController>(true);
                if (battle != null)
                    return battle;
            }

            return null;
        }
    }
}
