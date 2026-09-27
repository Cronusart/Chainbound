using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lightweight DTO representing an opponent's escrowed move
    /// in the winner-selection UI. Deliberately omits rarity, modifiers,
    /// names, and all identifying info to maintain UI-level secrecy.
    ///
    /// The winner selects by index; the mapping back to real move
    /// data is held server/orchestrator-side only.
    /// </summary>
    [Serializable]
    public struct AnonymizedMove
    {
        /// <summary>
        /// Stable internal ID for this entry. Opaque to the selecting player.
        /// Used by the orchestrator to resolve the actual move after selection.
        /// </summary>
        public string internalId;

        /// <summary>
        /// Zero-based index in the anonymized array.
        /// The winner passes this index to <see cref="WinnerSelectionAPI.SelectMove"/>.
        /// </summary>
        public int index;
    }
}
