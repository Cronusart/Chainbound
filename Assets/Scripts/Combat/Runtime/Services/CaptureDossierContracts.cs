using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Authority-owned identity for the post-capture dossier presentation. The UI
    /// must hydrate the Chainling by backend id rather than rebuilding rolled data.
    /// </summary>
    [Serializable]
    public sealed class CaptureDossierRequest
    {
        public string playerProfileId;
        public string chainlingId;
        public string fallbackDisplayName;
        public bool practice;

        /// <summary>
        /// Rolled target used when no authoritative row exists yet (editor practice
        /// captures, or a grant whose id has not arrived). Hydration is skipped and the
        /// page shows this snapshot as-is. Never persisted by the presenter.
        /// </summary>
        public ChainlingSnapshot localSnapshot;

        /// <summary>Whether the capture joined the party; drives the dossier placement line.</summary>
        public bool addedToParty;

        /// <summary>
        /// Runs after the book page finishes sliding in and before the index can be scrolled.
        /// The action updates the name printed on the page while the player types.
        /// </summary>
        public Func<Action<string>, Task> presentNamePrompt;
    }

    /// <summary>
    /// Presentation seam kept in Combat.Runtime so capture does not depend on the
    /// Player Menu assembly. Implemented by the reusable menu dossier presenter.
    /// </summary>
    public interface ICaptureDossierPresenter
    {
        Task PresentAsync(CaptureDossierRequest request);
    }
}
