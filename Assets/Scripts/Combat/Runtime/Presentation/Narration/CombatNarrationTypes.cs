using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public enum CombatNarrationAdvanceMode
    {
        AutoWithOptionalAdvance = 0,
        ConfirmEveryPage = 1,
        Automatic = 2,
    }

    public enum CombatNarrationDelivery
    {
        Live = 0,
        OutcomeSummary = 1,
        PopupOrHudOnly = 2,
        TranscriptOnly = 3,
        Silent = 4,
    }

    public enum CombatNarrationPageKind
    {
        Action = 0,
        Resolution = 1,
        Consequence = 2,
        BattleResult = 3,
        System = 4,
    }

    [Serializable]
    public sealed class CombatNarrationPage
    {
        public CombatNarrationPageKind kind;
        public string speaker = "";
        public string text = "";
        public float minimumDisplaySeconds;
        /// <summary>Seconds of full text that must show before this page can be skipped.</summary>
        public float skipLockoutSeconds;

        public bool IsReadable => !string.IsNullOrWhiteSpace(text);
    }

    [Serializable]
    public sealed class CombatNarrationTranscriptEntry
    {
        public int turnIndex;
        public int actionIndex;
        public int sequenceIndex;
        public CombatNarrationPageKind kind;
        public string speaker = "";
        public string text = "";
    }

    [Serializable]
    public sealed class CombatNarrationActionPlan
    {
        public int firstEventTimestamp = -1;
        public int lastEventTimestamp = -1;
        public int turnIndex;
        public int actionIndex;
        public bool livePresentationEnabled = true;
        public CombatNarrationPage actionPage;
        public CombatNarrationPage resolutionPage;
        public CombatNarrationPage consequencePage;
        public List<CombatNarrationTranscriptEntry> transcriptEntries = new List<CombatNarrationTranscriptEntry>();

        public bool HasEventRange => firstEventTimestamp >= 0 && lastEventTimestamp >= firstEventTimestamp;

        public bool HasLivePages => livePresentationEnabled &&
                                    ((actionPage != null && actionPage.IsReadable) ||
                                     (resolutionPage != null && resolutionPage.IsReadable) ||
                                     (consequencePage != null && consequencePage.IsReadable));

        public IEnumerable<CombatNarrationPage> EnumerateResolutionPages()
        {
            if (resolutionPage != null && resolutionPage.IsReadable)
                yield return resolutionPage;
            if (consequencePage != null && consequencePage.IsReadable)
                yield return consequencePage;
        }
    }
}
