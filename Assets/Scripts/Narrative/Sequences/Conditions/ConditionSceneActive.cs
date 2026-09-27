using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// True when <see cref="SceneManager.GetActiveScene"/> name matches (Known comparison).
/// Use with <see cref="Step_If"/> / <see cref="Step_Branch"/> for cross-scene branching.
/// </summary>
[CreateAssetMenu(fileName = "Condition_SceneActive", menuName = "Chainbound/Narrative/Conditions/Scene Active")]
public class ConditionSceneActive : SequenceCondition
{
    [Tooltip("Scene name to compare against SceneManager.GetActiveScene().name.")]
    [SerializeField] private string sceneName = "";

    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ConditionSceneActive] Scene name is empty!");
            return false;
        }

        return string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal);
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ConditionSceneActive] Scene name is not set!");
            return false;
        }

        return true;
    }
}
