using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatActionOutlineTuning", menuName = "Chainbound/Combat/Config/Action Outline Tuning")]
    public class CombatActionOutlineTuningSO : ScriptableObject
    {
        [Header("Defaults")]
        public Color defaultOutlineColor = new Color(0.15f, 0.8f, 1f, 0.8f);
        [Min(0f)]
        public float defaultOutlineWidthPixels = 4f;
        [Range(0f, 1f)]
        public float defaultOutlineSoftness = 0.5f;

        [Header("Fade")]
        [Min(0f)]
        public float fadeInSeconds = 0.08f;
        [Min(0f)]
        public float fadeOutSeconds = 0.11f;
    }
}
