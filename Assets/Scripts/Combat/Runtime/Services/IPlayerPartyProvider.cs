using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Provides the player's current party for battle initialization.
    /// Implementations may pull from an inventory system, NFT wallet,
    /// or inspector-configured fallback.
    /// Pure interface â€” no Unity dependencies.
    /// </summary>
    public interface IPlayerPartyProvider
    {
        /// <summary>
        /// Return the player's current party as a snapshot.
        /// Contains 1-3 chainlings with species, level, and loadout info.
        /// </summary>
        PartySnapshot GetCurrentParty();
    }

    /// <summary>
    /// Optional party source that can read saved loadouts again before a battle starts.
    /// Manual and test providers leave this unimplemented.
    /// </summary>
    public interface IPlayerLoadoutRefresh
    {
        Task<PartySnapshot> RefreshPartyAsync();
    }
}
