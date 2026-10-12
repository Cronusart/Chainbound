using UnityEngine;

namespace Chainbound.Hub
{
    /// <summary>
    /// The hub ships in the Editor and development builds. Release player builds stay docked.
    /// </summary>
    public static class HubBuild
    {
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
    }
}
