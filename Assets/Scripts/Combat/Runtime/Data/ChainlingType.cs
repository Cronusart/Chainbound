namespace Chainbound.Combat
{
    /// <summary>
    /// The 14 canonical Chainling types.
    /// Order matches COMBAT_01_TypeChemistry_v1.1 Section 1.
    /// Used as row/column keys in the chemistry matrix.
    /// </summary>
    public enum ChainlingType
    {
        None = 0,       // Typeless / unresolved
        Node = 1,
        Gas = 2,
        Block = 3,
        Encrypted = 4,
        Burned = 5,
        Frozen = 6,
        Forked = 7,
        Mined = 8,
        Staked = 9,
        Compressed = 10,
        Bot = 11,
        Glitched = 12,
        Shard = 13,
        Meme = 14,
    }
}
