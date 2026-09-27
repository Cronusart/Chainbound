using System.Collections.Generic;
using System.Text;

namespace Chainbound.Combat
{
    public enum BattleOutcomeKind
    {
        Victory,
        Defeat,
        Capture,
        Draw,
        Withdrawn,
        Escaped,
        Complete
    }

    /// <summary>
    /// Single source for post-battle banner titles, report titles, stingers, and rewards-page gating.
    /// Capture terminals are never classified as DRAW.
    /// </summary>
    public readonly struct BattleOutcomePresentation
    {
        public BattleOutcomeKind Kind { get; }
        public string BannerTitle { get; }
        public string ReportTitle { get; }
        public bool ShowRewardsPage { get; }
        public bool DidCapture { get; }

        public BattleOutcomePresentation(
            BattleOutcomeKind kind,
            string bannerTitle,
            string reportTitle,
            bool showRewardsPage,
            bool didCapture)
        {
            Kind = kind;
            BannerTitle = bannerTitle;
            ReportTitle = reportTitle;
            ShowRewardsPage = showRewardsPage;
            DidCapture = didCapture;
        }

        public static BattleOutcomePresentation Resolve(BattleResult result)
        {
            if (result == null)
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Complete,
                    "BATTLE COMPLETE",
                    "BATTLE COMPLETE",
                    false,
                    false);

            bool captured = result.DidCapture;

            if (result.wasFled)
            {
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Withdrawn,
                    "WITHDRAWN",
                    "WITHDRAWN",
                    false,
                    captured);
            }

            if (result.PlayerWon)
            {
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Victory,
                    "VICTORY!!!",
                    "VICTORY",
                    true,
                    captured);
            }

            if (result.EndedByCapture && captured)
            {
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Capture,
                    "CAPTURE",
                    "CAPTURE",
                    true,
                    true);
            }

            if (result.winnerTeamIndex == 1)
            {
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Defeat,
                    "DEFEAT",
                    "DEFEAT",
                    true,
                    captured);
            }

            if (result.winnerTeamIndex == -2 || result.IsDraw)
            {
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Draw,
                    "DRAW",
                    "DRAW",
                    true,
                    captured);
            }

            if (result.completionReason == BattleCompletionReason.TargetEscaped)
            {
                return new BattleOutcomePresentation(
                    BattleOutcomeKind.Escaped,
                    "ESCAPED",
                    "ESCAPED",
                    false,
                    false);
            }

            return new BattleOutcomePresentation(
                BattleOutcomeKind.Complete,
                "BATTLE COMPLETE",
                "BATTLE COMPLETE",
                false,
                captured);
        }

        public static string ResolveCapturedDisplayName(BattleResult result)
        {
            if (result == null || !result.DidCapture)
                return string.Empty;

            List<BattleCaptureRecord> successes = GetSuccessfulCaptures(result);
            if (successes.Count > 0)
                return JoinCaptureDisplayNames(successes);

            if (result.playerParticipants != null)
            {
                for (int i = 0; i < result.playerParticipants.Count; i++)
                {
                    BattleParticipant participant = result.playerParticipants[i];
                    if (participant == null)
                        continue;
                    if (!string.IsNullOrWhiteSpace(result.capturedBackendChainlingId) &&
                        string.Equals(result.capturedBackendChainlingId, participant.backendChainlingId, System.StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(participant.displayName))
                    {
                        return participant.displayName.Trim();
                    }
                }
            }

            return "the wild Chainling";
        }

        public static List<BattleCaptureRecord> GetSuccessfulCaptures(BattleResult result)
        {
            var successes = new List<BattleCaptureRecord>();
            if (result == null || result.captures == null)
                return successes;

            for (int i = 0; i < result.captures.Count; i++)
            {
                BattleCaptureRecord record = result.captures[i];
                if (record != null && record.succeeded)
                    successes.Add(record);
            }

            return successes;
        }

        private static string JoinCaptureDisplayNames(List<BattleCaptureRecord> successes)
        {
            var names = new List<string>(successes.Count);
            for (int i = 0; i < successes.Count; i++)
            {
                BattleCaptureRecord record = successes[i];
                string name = record != null && !string.IsNullOrWhiteSpace(record.displayName)
                    ? record.displayName.Trim()
                    : record != null && !string.IsNullOrWhiteSpace(record.subjectLabel)
                        ? record.subjectLabel.Trim()
                        : "";
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            if (names.Count == 0)
                return "the wild Chainling";
            if (names.Count == 1)
                return names[0];
            if (names.Count == 2)
                return names[0] + " and " + names[1];

            var builder = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                    builder.Append(i == names.Count - 1 ? ", and " : ", ");
                builder.Append(names[i]);
            }

            return builder.ToString();
        }
    }
}
