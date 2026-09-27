using System;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public struct AudioLoopToken : IEquatable<AudioLoopToken>
    {
        [SerializeField] private int id;

        public int Id => id;
        public bool IsValid => id > 0;

        public AudioLoopToken(int id)
        {
            this.id = id;
        }

        public bool Equals(AudioLoopToken other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is AudioLoopToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id;
        }

        public static bool operator ==(AudioLoopToken left, AudioLoopToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AudioLoopToken left, AudioLoopToken right)
        {
            return !left.Equals(right);
        }
    }
}