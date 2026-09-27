using System;

namespace Chainbound.Combat
{
    [Flags]
    public enum CombatPopupMotionLayers
    {
        None = 0,
        Translate = 1 << 0,
        Shake = 1 << 1,
        Punch = 1 << 2,
    }

    public enum CombatPopupTranslateMode
    {
        FloatUp,
        FloatDown,
        WorldOffset,
        Arc,
    }
}
