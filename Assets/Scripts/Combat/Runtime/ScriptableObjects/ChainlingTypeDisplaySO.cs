using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingTypeDisplay", menuName = "Chainbound/Combat/Chainlings/Type Display")]
    public class ChainlingTypeDisplaySO : ScriptableObject
    {
        [Serializable]
        public struct TypeDisplayEntry
        {
            public ChainlingType type;
            public string displayName;

            [TextArea(2, 4)]
            public string shortDescription;

            public Color badgeColor;
        }

        [SerializeField] private List<TypeDisplayEntry> entries = new List<TypeDisplayEntry>();

        public TypeDisplayEntry GetEntryOrFallback(ChainlingType type)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].type == type)
                        return entries[i];
                }
            }

            return CreateFallback(type);
        }

        public static TypeDisplayEntry CreateFallback(ChainlingType type)
        {
            return new TypeDisplayEntry
            {
                type = type,
                displayName = type == ChainlingType.None ? "Typeless" : type.ToString(),
                shortDescription = ResolveFallbackDescription(type),
                badgeColor = ResolveFallbackColor(type),
            };
        }

        private static string ResolveFallbackDescription(ChainlingType type)
        {
            switch (type)
            {
                case ChainlingType.None:
                    return "No inherent chain type. Uses typeless presentation where applicable.";
                case ChainlingType.Node:
                    return "Stable and resilient. Anchors the network and holds the line.";
                case ChainlingType.Gas:
                    return "Volatile and quick. Builds pressure for explosive turns.";
                case ChainlingType.Block:
                    return "Heavy and structured. Wins by absorbing hits and standing firm.";
                case ChainlingType.Encrypted:
                    return "Secretive and slippery. Hard to read, harder to pin down.";
                case ChainlingType.Burned:
                    return "Hot and aggressive. Trades safety for raw damage output.";
                case ChainlingType.Frozen:
                    return "Cold and controlling. Slows momentum and locks options down.";
                case ChainlingType.Forked:
                    return "Adaptive and split-minded. Thrives when the battle branches.";
                case ChainlingType.Mined:
                    return "Patient and industrious. Turns steady effort into long-term value.";
                case ChainlingType.Staked:
                    return "Committed and durable. Gains strength by staying in play.";
                case ChainlingType.Compressed:
                    return "Dense and efficient. Packs impact into small, precise windows.";
                case ChainlingType.Bot:
                    return "Relentless and systematic. Pressures opponents through routine.";
                case ChainlingType.Glitched:
                    return "Chaotic and unstable. Breaks patterns and creates strange openings.";
                case ChainlingType.Shard:
                    return "Sharp and fragmentary. Excels at piercing weak points.";
                case ChainlingType.Meme:
                    return "Wild and infectious. Wins through momentum, surprise, and style.";
                default:
                    return "A still-unclassified chainling type.";
            }
        }

        private static Color ResolveFallbackColor(ChainlingType type)
        {
            switch (type)
            {
                case ChainlingType.Node:
                    return new Color(0.20f, 0.57f, 0.90f, 1f);
                case ChainlingType.Gas:
                    return new Color(0.96f, 0.64f, 0.18f, 1f);
                case ChainlingType.Block:
                    return new Color(0.44f, 0.53f, 0.71f, 1f);
                case ChainlingType.Encrypted:
                    return new Color(0.35f, 0.30f, 0.73f, 1f);
                case ChainlingType.Burned:
                    return new Color(0.86f, 0.27f, 0.18f, 1f);
                case ChainlingType.Frozen:
                    return new Color(0.39f, 0.82f, 0.96f, 1f);
                case ChainlingType.Forked:
                    return new Color(0.61f, 0.44f, 0.89f, 1f);
                case ChainlingType.Mined:
                    return new Color(0.73f, 0.54f, 0.20f, 1f);
                case ChainlingType.Staked:
                    return new Color(0.17f, 0.66f, 0.42f, 1f);
                case ChainlingType.Compressed:
                    return new Color(0.28f, 0.71f, 0.66f, 1f);
                case ChainlingType.Bot:
                    return new Color(0.66f, 0.69f, 0.76f, 1f);
                case ChainlingType.Glitched:
                    return new Color(0.90f, 0.20f, 0.56f, 1f);
                case ChainlingType.Shard:
                    return new Color(0.82f, 0.80f, 0.90f, 1f);
                case ChainlingType.Meme:
                    return new Color(0.97f, 0.48f, 0.69f, 1f);
                default:
                    return new Color(0.37f, 0.44f, 0.52f, 1f);
            }
        }
    }
}
