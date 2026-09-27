using System;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public struct CombatMusicDuckToken : IEquatable<CombatMusicDuckToken>
    {
        [SerializeField] private int id;

        public int Id => id;
        public bool IsValid => id > 0;

        public CombatMusicDuckToken(int id)
        {
            this.id = id;
        }

        public bool Equals(CombatMusicDuckToken other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMusicDuckToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id;
        }
    }
}
