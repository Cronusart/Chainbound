using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CaptureResultCopyTests
    {
        [Test]
        public void FormatCapturedSubject_UsesWildSpeciesName()
        {
            var target = new ChainlingInstance { displayName = "Kitsauto" };
            Assert.AreEqual("wild Kitsauto", CaptureResultCopy.FormatCapturedSubject(target, "Chainling"));
        }

        [Test]
        public void FormatCapturedSubject_IncludesMutatedEntangledAndVariant()
        {
            var target = new ChainlingInstance
            {
                displayName = "Kitsauto",
                isMutated = true,
                isEntangled = true,
                visualIdentity = new ChainlingVisualIdentity { visualVariantId = "chrome" },
            };

            Assert.AreEqual(
                "wild mutated entangled chrome Kitsauto",
                CaptureResultCopy.FormatCapturedSubject(target, "Chainling"));
        }

        [Test]
        public void FormatPrompt_ReplacesSubjectToken()
        {
            string prompt = CaptureResultCopy.FormatPrompt(
                "You've captured a {subject}! Would you like to give it a name?",
                "wild mutated Kitsauto");

            Assert.AreEqual(
                "You've captured a wild mutated Kitsauto! Would you like to give it a name?",
                prompt);
        }

        [Test]
        public void ResolveFailureCopy_UsesBoardThenFallback()
        {
            var board = UnityEngine.ScriptableObject.CreateInstance<CapturePresentationConfigSO>();
            board.failureNarrativeCopy = "The lock broke.";
            Assert.AreEqual("The lock broke.", CaptureResultCopy.ResolveFailureCopy(board));
            Assert.IsFalse(string.IsNullOrWhiteSpace(CaptureResultCopy.ResolveFailureCopy(null)));
        }

        [Test]
        public void FormatBattleReportCaptureLine_IncludesKindAndDuration()
        {
            var record = new BattleCaptureRecord
            {
                subjectLabel = "wild mutated Validape",
                displayName = "Validape",
                durationSeconds = 4.2f,
                succeeded = true,
            };

            Assert.AreEqual(
                "Captured: wild mutated Validape in 4.2 seconds",
                CaptureResultCopy.FormatBattleReportCaptureLine(record));
        }
    }
}
