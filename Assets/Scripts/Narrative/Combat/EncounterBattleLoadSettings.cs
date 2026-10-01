using System;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Per-encounter load configuration for narrative battles: scene name, SceneLoader options,
    /// and optional timeout overrides. Empty / inherit flags defer to <see cref="NarrativeEncounterService"/> defaults.
    /// </summary>
    [Serializable]
    public class EncounterBattleLoadSettings
    {
        [Header("Battle scene")]
        [Tooltip("Scene to load for this encounter. Leave empty to use the service default battle scene.")]
        public string battleSceneName = "";

        [Tooltip("Combat stage id (loomville, link1, jungle) passed through SceneLoader. Empty uses loomville. This is scenery selection, not an overworld player spawn.")]
        public string battleSpawnId = "";

        [Header("Transition (SceneLoader)")]
        [Tooltip("When enabled, use Transition Style below instead of the service default.")]
        public bool overrideTransitionStyle = false;

        public TransitionStyle transitionStyle = TransitionStyle.QuickFade;

        [Tooltip("When enabled, use Use Scene Loader below instead of the service default.")]
        public bool overrideUseSceneLoader = false;

        [Tooltip("Legacy compatibility field. All production loads now route through SceneLoader.")]
        public bool useSceneLoader = true;

        [Header("Flow / timeouts")]
        [Tooltip("When enabled, use the timeout and wait values below instead of the service defaults.")]
        public bool overrideFlowTimeouts = false;

        [Min(0.5f)]
        public float sceneLoadTimeoutSeconds = 12f;

        [Min(0.5f)]
        public float battleControllerTimeoutSeconds = 12f;

        [Min(0.5f)]
        public float returnSceneTimeoutSeconds = 20f;

        public bool waitForReturnSceneAfterBattle = true;

        /// <summary>
        /// Resolves the scene to load: per-entry name, optional legacy serialized override, then service default.
        /// </summary>
        public string ResolveBattleSceneName(string serviceDefaultBattleScene, string legacyBattleSceneOverride)
        {
            if (!string.IsNullOrWhiteSpace(battleSceneName))
                return battleSceneName.Trim();

            if (!string.IsNullOrWhiteSpace(legacyBattleSceneOverride))
                return legacyBattleSceneOverride.Trim();

            return serviceDefaultBattleScene;
        }

        public TransitionStyle ResolveTransitionStyle(TransitionStyle serviceDefault) =>
            overrideTransitionStyle ? transitionStyle : serviceDefault;

        public bool ResolveUseSceneLoader(bool serviceDefault) =>
            overrideUseSceneLoader ? useSceneLoader : serviceDefault;

        public float ResolveSceneLoadTimeout(float serviceDefault) =>
            overrideFlowTimeouts ? sceneLoadTimeoutSeconds : serviceDefault;

        public float ResolveBattleControllerTimeout(float serviceDefault) =>
            overrideFlowTimeouts ? battleControllerTimeoutSeconds : serviceDefault;

        public float ResolveReturnSceneTimeout(float serviceDefault) =>
            overrideFlowTimeouts ? returnSceneTimeoutSeconds : serviceDefault;

        public bool ResolveWaitForReturnScene(bool serviceDefault) =>
            overrideFlowTimeouts ? waitForReturnSceneAfterBattle : serviceDefault;
    }
}
