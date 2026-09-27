using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class ChainlingDisplayNameRulesTests
    {
        [TestCase("Validape", true)]
        [TestCase("O'Byte", true)]
        [TestCase("Signal-Fox", true)]
        [TestCase("A", false)]
        [TestCase("Two  Spaces", false)]
        [TestCase("Bad_Name", false)]
        [TestCase("ThirteenCharsX", false)]
        public void ValidatesCanonicalNameRules(string value, bool expected)
        {
            Assert.That(ChainlingDisplayNameRules.IsValid(value), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidOrSkippedNameUsesSpeciesFallback()
        {
            Assert.That(
                ChainlingDisplayNameRules.ResolveOrFallback("bad_name", "Validape"),
                Is.EqualTo("Validape"));
            Assert.That(
                ChainlingDisplayNameRules.ResolveOrFallback("", "Validape"),
                Is.EqualTo("Validape"));
        }

        [Test]
        public void SanitizeRemovesInvalidCharactersAndCollapsesSpaces()
        {
            Assert.That(
                ChainlingDisplayNameRules.Sanitize("  Sig_nal   Fox! "),
                Is.EqualTo("Signal Fox"));
        }
    }
}
