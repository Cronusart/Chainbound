using UnityEngine;

namespace Chainbound.Combat
{
    public abstract class CombatVfxDefinitionSO : ScriptableObject
    {
        public string vfxId;
        [Min(0f)] public float defaultScale = 1f;
    }
}