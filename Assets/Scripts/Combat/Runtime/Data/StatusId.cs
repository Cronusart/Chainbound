namespace Chainbound.Combat
{
    /// <summary>
    /// Stable enum for every status in the game.
    /// Matches COMBAT_02A_StatusFrameworkAndCatalog_v1.2 Section 6.
    /// IDs are explicit so data assets remain stable across reorders.
    /// </summary>
    public enum StatusId
    {
        None = 0,

        // â”€â”€ ChemistryOnly Statuses (Section 6.1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Pumped              = 100,  // +1 AP at End of Turn
        Drained             = 101,  // -1 AP at End of Turn
        Rugged              = 102,  // Severity/16 MaxHP DoT; severity advances at End of Turn
        Overclocked         = 103,  // Legacy migration ID; no longer authored
        Leaked              = 104,  // On action: 1/10 HP to all
        Defueled            = 105,  // Remove one harmful chemistry status from self (Instant)
        Bridged             = 106,  // Legacy migration ID; use Splinter
        Firewalled          = 107,  // Legacy migration ID; use Firewall
        Jammed              = 108,  // Accuracy down (Acc -2)
        Halted              = 109,  // Legacy migration ID; use Slowed
        Crashed             = 110,  // Legacy migration ID; use Shattered
        FOMO                = 111,  // Atk +1 for 3 turns, 1/16 MaxHP self DoT
        Siphoned            = 112,  // Heal 25% dmg dealt
        Evasive             = 113,  // Legacy migration ID; use Dusted/General alternatives
        Anchored            = 114,  // Next move must be single-target (Consumed)
        Amplified           = 115,  // Next move x2 power (Consumed)
        Dusted              = 116,  // Eva -3, Spd -3
        Narrowcast          = 117,  // Legacy migration ID; use Anchored
        BacklashType        = 118,  // Contact adds typed DoT
        Corroded            = 119,  // Def -1 at End of Turn
        LiquidityPump       = 120,  // Legacy migration ID; use Drip
        Reinforced          = 121,  // Def +3
        TypeCoating         = 122,  // Override to mono-type (parameterized by type)
        HypedTypeCoating    = 123,  // Universal override (parameterized by type)
        CascadeFailure      = 124,  // Counter: third apply deals 25% MaxHP, then resets
        Isolated            = 125,  // Cannot receive ally Support moves
        Firewall            = 126,  // First projectile hit each turn takes -30% damage
        Slowed              = 127,  // Speed -2
        Shattered           = 128,  // 8% current HP chip + Def -1
        Drip                = 129,  // Heal 6% MaxHP at End of Turn
        Splinter            = 130,  // Next damaging move spreads at 65%, or +15% vs one foe

        // â”€â”€ General Statuses (Section 6.2) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Rekt                = 200,  // 25% lose action
        Offline             = 201,  // Sleep; wakes on damage
        GlitchedMind        = 202,  // 50% mis-act
        FUD                 = 203,  // Atk/Def/Acc/Spd -1
        HODLed              = 204,  // Immune stat drops; Def ramps +1/turn
        FiftyOnePercentAttacked = 205,  // Team slow & acc down
        MEVExtracted        = 206,  // Steal 1 AP per turn
        DustStorm           = 207,  // Field eva down
        Overleveraged       = 208,  // Next heavy hit causes recoil (Consumed)
        BridgedOut          = 209,  // Swap with ally (Instant)
        ForkedChain         = 210,  // Copy self-debuffs to ally
        CompressedData      = 211,  // Eva +2, Spd -1
        BotSwarm            = 212,  // Team Atk/Def -1
        MemeSurge           = 213,  // Random stat +1
        ShardPartition      = 214,  // Team speed +1
        EncryptedSeal       = 215,  // Debuff immunity
        BurnedTokens        = 216,  // Enemy DoT heals user
        FrozenAssets        = 217,  // Prevent switches
        NodeRelay           = 218,  // Redirect healing (Instant)
        GasSpike            = 219,  // Atk +2, Def -2

        // â”€â”€ General Statuses â€” v1.2 Additions (Section 6.3) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        ThreadLock          = 300,  // Target cannot gain AP
        ExploitMark         = 301,  // Allies deal +15% dmg to target
        Broadcast           = 302,  // Next move hits all foes (Consumed, General)
        Yield               = 303,  // +1 AP at End of Turn (General analog of Pumped)
        Immovable           = 304,  // Immune to forced swap
        Staggered           = 305,  // Next move -10% power (Consumed)
        Surging             = 306,  // Next move +25% power (Consumed)
        Unsealed            = 307,  // 50% chance to bypass single-hit barriers per hit
        Relay               = 308,  // Full barrier bypass for next hit (Consumed)
        DamageIntercept     = 309,  // Redirect next single-target attack to interceptor (Consumed)
        SearField           = 310,  // Atk -1 for the rest of the granting turn; stage reverts on expiry

        // â”€â”€ Engine-Internal Statuses â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Recharging          = 900,  // Stamped recharge timer (ConsumedStatus, see 02C Â§1.2)
    }
}
