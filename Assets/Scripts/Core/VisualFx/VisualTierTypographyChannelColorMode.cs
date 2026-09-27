namespace SignalJam.VisualFx
{
    /// <summary>
    /// How outline / underlay / glow colors are chosen when face uses a gradient.
    /// Stock TMP shaders only support uniform material colors for these passes.
    /// </summary>
    public enum VisualTierTypographyChannelColorMode
    {
        /// <summary>Use the channel's palette slot / custom color.</summary>
        Uniform = 0,

        /// <summary>Use the face gradient start color.</summary>
        GradientStart = 1,

        /// <summary>Use the face gradient end color.</summary>
        GradientEnd = 2,

        /// <summary>Blend start/end (50/50).</summary>
        GradientMidpoint = 3,
    }
}
