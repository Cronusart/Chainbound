using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Acquisition values and ink for the two library-card faces.
    /// Candy rolls and event grants share the rolled face. Battle learns use the learned face.
    /// </summary>
    public static class LibraryCardFaces
    {
        public const string CandyRoll = "candy_roll";
        public const string EventGrant = "event_grant";
        public const string ProgressionLearn = "progression_learn";

        public static readonly UnityEngine.Color32 SageInk = new UnityEngine.Color32(55, 78, 74, 255);
        public static readonly UnityEngine.Color32 LearnedInk = new UnityEngine.Color32(158, 69, 57, 255);

        public static string Normalize(string acquisition)
        {
            if (string.Equals(acquisition, ProgressionLearn, StringComparison.OrdinalIgnoreCase))
                return ProgressionLearn;
            if (string.Equals(acquisition, EventGrant, StringComparison.OrdinalIgnoreCase))
                return EventGrant;
            return CandyRoll;
        }

        public static bool IsLearned(string acquisition)
        {
            return Normalize(acquisition) == ProgressionLearn;
        }
    }
}
