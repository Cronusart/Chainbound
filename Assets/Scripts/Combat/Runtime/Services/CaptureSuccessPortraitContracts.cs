using System.Collections;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Species portrait shown after a successful capture, before the dossier.
    /// Kept in Combat.Runtime so capture does not depend on the starter UI assembly.
    /// </summary>
    public sealed class CaptureSuccessPortraitRequest
    {
        public string speciesDisplayName;
        public CombatVisualProfileSO visualProfile;
        public Sprite fallbackSprite;
    }

    public interface ICaptureSuccessPortraitPresenter
    {
        IEnumerator Present(CaptureSuccessPortraitRequest request);
    }
}
