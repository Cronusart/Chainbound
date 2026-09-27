using System.Text;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds capture-result narration subjects from target traits.
    /// Copy templates live on <see cref="CapturePresentationConfigSO"/>.
    /// </summary>
    public static class CaptureResultCopy
    {
        public const string SubjectToken = "{subject}";

        public static string FormatPrompt(string template, string subject)
        {
            string text = string.IsNullOrWhiteSpace(template)
                ? "You've captured a {subject}! Would you like to give it a name?"
                : template;
            string value = string.IsNullOrWhiteSpace(subject) ? "wild Chainling" : subject.Trim();
            return text.Replace(SubjectToken, value);
        }

        public static string FormatCapturedSubject(ChainlingInstance target, string fallbackDisplayName)
        {
            string species = ResolveSpeciesName(target, fallbackDisplayName);
            var builder = new StringBuilder(64);
            builder.Append("wild");
            if (target != null && target.isMutated)
                builder.Append(" mutated");
            if (target != null && target.isEntangled)
                builder.Append(" entangled");

            string variant = FormatVariantQualifier(target);
            if (!string.IsNullOrEmpty(variant))
            {
                builder.Append(' ');
                builder.Append(variant);
            }

            builder.Append(' ');
            builder.Append(species);
            return builder.ToString();
        }

        public static string FormatBattleReportCaptureLine(BattleCaptureRecord record)
        {
            string subject = "wild Chainling";
            if (record != null)
            {
                if (!string.IsNullOrWhiteSpace(record.subjectLabel))
                    subject = record.subjectLabel.Trim();
                else if (!string.IsNullOrWhiteSpace(record.displayName))
                    subject = "wild " + record.displayName.Trim();
            }

            float seconds = record != null && record.durationSeconds > 0f
                ? record.durationSeconds
                : 0f;
            return "Captured: " + subject + " in " + seconds.ToString("0.0") + " seconds";
        }

        public static string ResolveFailureCopy(CapturePresentationConfigSO board)
        {
            if (board != null && !string.IsNullOrWhiteSpace(board.failureNarrativeCopy))
                return board.failureNarrativeCopy.Trim();
            return "The capture attempt was unsuccessful. The signal slipped the lock.";
        }

        private static string ResolveSpeciesName(ChainlingInstance target, string fallbackDisplayName)
        {
            if (target != null && !string.IsNullOrWhiteSpace(target.displayName))
                return target.displayName.Trim();
            if (!string.IsNullOrWhiteSpace(fallbackDisplayName))
                return fallbackDisplayName.Trim();
            if (target != null && !string.IsNullOrWhiteSpace(target.speciesId))
                return target.speciesId.Trim();
            return "Chainling";
        }

        private static string FormatVariantQualifier(ChainlingInstance target)
        {
            if (target == null || target.visualIdentity == null)
                return string.Empty;

            string variantId = ChainlingVisualIdentity.NormalizeBodySkinVariantId(target.visualIdentity.visualVariantId);
            if (string.IsNullOrWhiteSpace(variantId) ||
                string.Equals(variantId, ChainlingVisualIdentity.BaseVariantId, System.StringComparison.Ordinal) ||
                string.Equals(variantId, ChainlingVisualIdentity.DefaultId, System.StringComparison.Ordinal) ||
                string.Equals(variantId, ChainlingVisualIdentity.NoneId, System.StringComparison.Ordinal) ||
                string.Equals(variantId, ChainlingVisualIdentity.EntangledVariantId, System.StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return variantId.Replace('_', ' ');
        }
    }
}
