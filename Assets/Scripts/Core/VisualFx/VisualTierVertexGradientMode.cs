namespace SignalJam.VisualFx
{
    /// <summary>
    /// How TMP vertex colors are arranged for multi-color typography.
    /// </summary>
    public enum VisualTierVertexGradientMode
    {
        /// <summary>Top = start color, bottom = end color.</summary>
        Vertical = 0,

        /// <summary>Left = start color, right = end color (best for short labels like Cycle V).</summary>
        Horizontal = 1,

        /// <summary>Purple top-left → green top-right → green bottom-left → purple bottom-right (Solana-like diagonal).</summary>
        SolanaDiagonal = 2,
    }
}
