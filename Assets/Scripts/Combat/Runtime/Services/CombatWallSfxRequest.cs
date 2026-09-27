using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class CombatWallSfxRequest
    {
        public CombatWallSfxCue cue;
        public string wallSfxId = string.Empty;
        public string wallSourceTag = string.Empty;
        public string targetId = string.Empty;
        public string sourceId = string.Empty;
        public int eventTimestamp = -1;
        public Combat2ChainlingView targetView;
        public Combat2ChainlingView sourceView;
        public float delaySeconds;
        public float volumeScale = 1f;
        public bool preferBarrierAnchor = true;
    }
}
