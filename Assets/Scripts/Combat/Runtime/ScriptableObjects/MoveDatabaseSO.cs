using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Central database holding all move definitions.
    /// Data-driven: new moves are added by creating SO assets, not editing code.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveDatabase", menuName = "Chainbound/Combat/Moves/Move Database")]
    public class MoveDatabaseSO : ScriptableObject
    {
        [Tooltip("All move definitions in the game.")]
        public List<MoveDefinitionSO> moves = new List<MoveDefinitionSO>();

        private Dictionary<string, MoveDefinitionSO> _lookup;
        private Dictionary<ushort, MoveDefinitionSO> _hashedLookup;

        /// <summary>Get a move by its stable ID.</summary>
        public MoveDefinitionSO GetById(string moveId)
        {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(moveId, out var def);
            return def;
        }

        /// <summary>Get a move by the hashed on-chain ushort move ID.</summary>
        public MoveDefinitionSO GetByHashedId(ushort hashedMoveId)
        {
            if (_hashedLookup == null) BuildLookup();
            _hashedLookup.TryGetValue(hashedMoveId, out var def);
            return def;
        }

        /// <summary>Get all moves of a given type (or None for typeless).</summary>
        public List<MoveDefinitionSO> GetByType(ChainlingType type)
        {
            var result = new List<MoveDefinitionSO>();
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].baseType == type)
                    result.Add(moves[i]);
            }
            return result;
        }

        /// <summary>Get all moves of a given category.</summary>
        public List<MoveDefinitionSO> GetByCategory(MoveCategory category)
        {
            var result = new List<MoveDefinitionSO>();
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].category == category)
                    result.Add(moves[i]);
            }
            return result;
        }

        /// <summary>Rebuild the lookup dictionary.</summary>
        public void BuildLookup()
        {
            _lookup = new Dictionary<string, MoveDefinitionSO>(moves.Count);
            _hashedLookup = new Dictionary<ushort, MoveDefinitionSO>(moves.Count);
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] != null && !string.IsNullOrEmpty(moves[i].moveId))
                {
                    _lookup[moves[i].moveId] = moves[i];
                    ushort hashedId = HashMoveId(moves[i].moveId);
                    if (!_hashedLookup.ContainsKey(hashedId))
                        _hashedLookup[hashedId] = moves[i];
                }
            }
        }

        private void OnEnable() => BuildLookup();

        public static ushort HashMoveId(string moveId)
        {
            if (string.IsNullOrEmpty(moveId)) return 0;
            uint hash = 2166136261;
            for (int i = 0; i < moveId.Length; i++)
            {
                hash ^= moveId[i];
                hash *= 16777619;
            }
            return (ushort)(hash & 0xFFFF);
        }
    }
}
