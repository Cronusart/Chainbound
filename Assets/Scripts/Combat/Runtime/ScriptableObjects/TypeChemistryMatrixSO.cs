using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum ChemistryRecipient
    {
        Defender = 0,
        Attacker = 1,
    }

    public enum ChemistryRelation
    {
        Resisted = -1,
        Neutral = 0,
        Advantage = 1,
    }

    public enum ChemistryStatusFamily
    {
        None,
        Liquidity,
        Volatility,
        Latency,
        Structure,
        Exposure,
        Sustain,
        Cleanse,
        Contagion,
        Coatings,
        Setup,
    }

    public enum ChemistryStatusPolarity
    {
        Neutral,
        Beneficial,
        Harmful,
        Hybrid,
    }

    /// <summary>
    /// Authoritative chemistry family and recipient rules shared by runtime,
    /// import validation, and presentation.
    /// </summary>
    public static class ChemistryStatusRules
    {
        public static ChemistryStatusFamily GetFamily(StatusId statusId)
        {
            switch (statusId)
            {
                case StatusId.Pumped:
                case StatusId.Drained:
                case StatusId.LiquidityPump:
                    return ChemistryStatusFamily.Liquidity;

                case StatusId.Rugged:
                case StatusId.FOMO:
                case StatusId.GasSpike:
                    return ChemistryStatusFamily.Volatility;

                case StatusId.Jammed:
                case StatusId.Halted:
                case StatusId.Slowed:
                case StatusId.Offline:
                case StatusId.Isolated:
                    return ChemistryStatusFamily.Latency;

                case StatusId.Firewall:
                case StatusId.Firewalled:
                case StatusId.Shattered:
                case StatusId.Corroded:
                case StatusId.Dusted:
                case StatusId.Reinforced:
                case StatusId.Anchored:
                    return ChemistryStatusFamily.Structure;

                case StatusId.BacklashType:
                case StatusId.Leaked:
                    return ChemistryStatusFamily.Exposure;

                case StatusId.Siphoned:
                case StatusId.Drip:
                    return ChemistryStatusFamily.Sustain;

                case StatusId.Defueled:
                    return ChemistryStatusFamily.Cleanse;

                case StatusId.CascadeFailure:
                case StatusId.Splinter:
                case StatusId.Broadcast:
                    return ChemistryStatusFamily.Contagion;

                case StatusId.TypeCoating:
                case StatusId.HypedTypeCoating:
                    return ChemistryStatusFamily.Coatings;

                case StatusId.Amplified:
                case StatusId.Bridged:
                case StatusId.Narrowcast:
                    return ChemistryStatusFamily.Setup;

                default:
                    return ChemistryStatusFamily.None;
            }
        }

        public static ChemistryStatusPolarity GetPolarity(StatusId statusId)
        {
            switch (statusId)
            {
                case StatusId.Pumped:
                case StatusId.Firewall:
                case StatusId.Reinforced:
                case StatusId.Siphoned:
                case StatusId.Drip:
                case StatusId.Splinter:
                case StatusId.Amplified:
                case StatusId.Defueled:
                    return ChemistryStatusPolarity.Beneficial;

                case StatusId.FOMO:
                    return ChemistryStatusPolarity.Hybrid;

                case StatusId.Drained:
                case StatusId.Rugged:
                case StatusId.Jammed:
                case StatusId.Slowed:
                case StatusId.Isolated:
                case StatusId.Shattered:
                case StatusId.Corroded:
                case StatusId.Dusted:
                case StatusId.Anchored:
                case StatusId.BacklashType:
                case StatusId.Leaked:
                case StatusId.CascadeFailure:
                    return ChemistryStatusPolarity.Harmful;

                default:
                    return ChemistryStatusPolarity.Neutral;
            }
        }

        public static bool IsCoreFamily(ChemistryStatusFamily family)
        {
            return family >= ChemistryStatusFamily.Liquidity &&
                   family <= ChemistryStatusFamily.Contagion;
        }

        public static bool IsRecipientValid(StatusId statusId, ChemistryRecipient recipient)
        {
            ChemistryStatusPolarity polarity = GetPolarity(statusId);
            if (polarity == ChemistryStatusPolarity.Beneficial || polarity == ChemistryStatusPolarity.Hybrid)
                return recipient == ChemistryRecipient.Attacker;
            if (polarity == ChemistryStatusPolarity.Harmful)
                return recipient == ChemistryRecipient.Defender;
            return false;
        }
    }

    /// <summary>
    /// ScriptableObject holding the type chemistry reaction matrix.
    /// Each attacker/defender cell has exactly one stackless chemistry output.
    /// </summary>
    [CreateAssetMenu(fileName = "TypeChemistryMatrix", menuName = "Chainbound/Combat/Types/Type Chemistry Matrix")]
    public class TypeChemistryMatrixSO : ScriptableObject
    {
        [Tooltip("All chemistry reactions. Keyed by attacker type row, defender type column.")]
        public List<ChemistryReaction> reactions = new List<ChemistryReaction>();

        public bool TryGetReaction(ChainlingType attacker, ChainlingType defender, out ChemistryOutput output)
        {
            for (int i = 0; i < reactions.Count; i++)
            {
                if (reactions[i].attackerType == attacker && reactions[i].defenderType == defender)
                {
                    output = reactions[i].output;
                    return output.statusId != StatusId.None;
                }
            }

            output = default;
            return false;
        }
    }

    [Serializable]
    public class ChemistryReaction
    {
        public ChainlingType attackerType;
        public ChainlingType defenderType;
        public ChemistryOutput output;
    }

    [Serializable]
    public struct ChemistryOutput
    {
        public StatusId statusId;

        [Tooltip("Who receives the reaction. Positive and hybrid effects target the attacker; harmful effects target the defender.")]
        public ChemistryRecipient recipient;

        [Tooltip("For parameterized statuses like TypeCoating and BacklashType.")]
        public ChainlingType parameterType;
    }
}
