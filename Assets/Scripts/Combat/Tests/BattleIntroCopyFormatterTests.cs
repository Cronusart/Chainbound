using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class BattleIntroCopyFormatterTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly Color _accent = new Color(1f, 0.78f, 0.22f, 1f);

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void WildBase_KeepsExistingCopy()
        {
            BattleIntroCopyResult result = FormatWild(CreateSubject("Validape"));
            Assert.AreEqual("A wild Validape has appeared!", Plain(result));
        }

        [Test]
        public void WildVariant_AppendsVariantAfterName()
        {
            BattleIntroCopySubject subject = CreateSubject("Validape", isVariant: true);
            BattleIntroCopyResult result = FormatWild(subject);
            Assert.AreEqual(
                "A wild Validape variant has appeared!",
                Plain(result));
            Assert.AreEqual(2, result.spans.Count);
            Assert.AreEqual(BattleIntroCopySpanKind.Name, result.spans[0].kind);
            Assert.AreEqual(BattleIntroCopySpanKind.Variant, result.spans[1].kind);
            Assert.AreEqual("variant".Length, result.spans[1].visibleLength);
        }

        [Test]
        public void WildStacked_UsesMutatedEntangledNameVariantOrder()
        {
            BattleIntroCopySubject subject = CreateSubject(
                "Validape",
                isMutated: true,
                isEntangled: true,
                entangledType: ChainlingType.Node,
                isVariant: true);

            Assert.AreEqual(
                "A wild mutated entangled Validape variant has appeared!",
                Plain(FormatWild(subject)));
        }

        [Test]
        public void WildDual_StacksPerCreature()
        {
            var first = CreateSubject("Validape", isMutated: true, isVariant: true);
            var second = CreateSubject("Kitsauto", isEntangled: true, entangledType: ChainlingType.Burned);

            BattleIntroCopyResult result = WildEncounterIntroFormatter.Format(
                CreateConfig(),
                new[] { first, second },
                isPvP: false,
                isWild: true,
                opponentTeamName: "Wild",
                vfxAccent: _accent);

            Assert.AreEqual(
                "A wild mutated Validape variant and a wild entangled Kitsauto have appeared!",
                Plain(result));
        }

        [Test]
        public void TraitOrder_IsConfigurable()
        {
            BattleIntroCopyConfigSO config = CreateConfig();
            config.traitOrder = new List<BattleIntroTraitSlot>
            {
                BattleIntroTraitSlot.Variant,
                BattleIntroTraitSlot.Name,
                BattleIntroTraitSlot.Mutated,
            };

            BattleIntroCopySubject subject = CreateSubject("Validape", isMutated: true, isVariant: true);
            Assert.AreEqual(
                "A wild variant Validape mutated has appeared!",
                Plain(FormatWild(subject, config)));
        }

        [Test]
        public void Labels_AreConfigurable()
        {
            BattleIntroCopyConfigSO config = CreateConfig();
            config.mutatedStyle.label = "glitched";
            config.variantStyle.label = "skin";

            BattleIntroCopySubject subject = CreateSubject("Validape", isMutated: true, isVariant: true);
            Assert.AreEqual(
                "A wild glitched Validape skin has appeared!",
                Plain(FormatWild(subject, config)));
        }

        [Test]
        public void Colorize_WrapsTraitWords()
        {
            BattleIntroCopyConfigSO config = CreateConfig();
            config.mutatedStyle.colorize = true;
            config.mutatedStyle.colorSource = BattleIntroTraitColorSource.Solid;
            config.mutatedStyle.fallbackColor = new Color(1f, 0f, 0f, 1f);
            config.nameStyle.colorize = false;
            config.variantStyle.colorize = false;

            BattleIntroCopySubject subject = CreateSubject("Validape", isMutated: true, isVariant: true);
            BattleIntroCopyResult result = FormatWild(subject, config);

            Assert.AreEqual("A wild mutated Validape variant has appeared!", Plain(result));
            StringAssert.Contains("<color=#FF0000>mutated</color>", result.richText);
            StringAssert.DoesNotContain("<color=", result.richText.Replace("<color=#FF0000>mutated</color>", string.Empty));
        }

        [Test]
        public void Pvp_IgnoresWildTraits()
        {
            BattleIntroCopySubject subject = CreateSubject("Validape", isMutated: true, isVariant: true);
            BattleIntroCopyResult result = WildEncounterIntroFormatter.Format(
                CreateConfig(),
                new[] { subject },
                isPvP: true,
                isWild: true,
                opponentTeamName: "Wild",
                vfxAccent: _accent);

            Assert.AreEqual("Opponent has challenged you!", Plain(result));
        }

        [Test]
        public void Trainer_UsesTeamToken()
        {
            BattleIntroCopyResult result = WildEncounterIntroFormatter.Format(
                CreateConfig(),
                new[] { CreateSubject("Validape") },
                isPvP: false,
                isWild: false,
                opponentTeamName: "Rogue Node",
                vfxAccent: _accent);

            Assert.AreEqual("Rogue Node has challenged you!", Plain(result));
        }

        [Test]
        public void FromChainling_ReadsMutatedFromAuraAndVariantFromSkin()
        {
            var chainling = new ChainlingInstance
            {
                displayName = "Validape",
                visualIdentity = new ChainlingVisualIdentity
                {
                    visualVariantId = "chrome",
                    auraId = "glow_mut",
                },
                isEntangled = true,
                entangledType = ChainlingType.Meme,
            };

            BattleIntroCopySubject subject = BattleIntroCopySubject.FromChainling(chainling);
            Assert.IsTrue(subject.isMutated);
            Assert.IsTrue(subject.isVariant);
            Assert.IsTrue(subject.isEntangled);
            Assert.AreEqual(ChainlingType.Meme, subject.entangledType);
        }

        [Test]
        public void EntangledColor_UsesTypeDisplayFallback()
        {
            BattleIntroCopyConfigSO config = CreateConfig();
            config.entangledStyle.colorize = true;
            config.entangledStyle.colorSource = BattleIntroTraitColorSource.EntangledTypeDisplay;
            config.nameStyle.colorize = false;

            BattleIntroCopySubject subject = CreateSubject(
                "Validape",
                isEntangled: true,
                entangledType: ChainlingType.Burned);
            BattleIntroCopyResult result = FormatWild(subject, config);

            Color expected = ChainlingTypeDisplaySO.CreateFallback(ChainlingType.Burned).badgeColor;
            string hex = ColorUtility.ToHtmlStringRGB(expected);
            StringAssert.Contains($"<color=#{hex}>entangled</color>", result.richText);
        }

        private BattleIntroCopyResult FormatWild(
            BattleIntroCopySubject subject,
            BattleIntroCopyConfigSO config = null)
        {
            return WildEncounterIntroFormatter.Format(
                config != null ? config : CreateConfig(),
                new[] { subject },
                isPvP: false,
                isWild: true,
                opponentTeamName: "Wild",
                vfxAccent: _accent);
        }

        private BattleIntroCopyConfigSO CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<BattleIntroCopyConfigSO>();
            config.ResetToDefaults();
            config.mutatedStyle.colorize = false;
            config.entangledStyle.colorize = false;
            config.nameStyle.colorize = false;
            config.variantStyle.colorize = false;
            _created.Add(config);
            return config;
        }

        private static BattleIntroCopySubject CreateSubject(
            string name,
            bool isMutated = false,
            bool isEntangled = false,
            ChainlingType entangledType = ChainlingType.None,
            bool isVariant = false)
        {
            return new BattleIntroCopySubject
            {
                displayName = name,
                isMutated = isMutated,
                isEntangled = isEntangled,
                entangledType = entangledType,
                isVariant = isVariant,
            };
        }

        private static string Plain(BattleIntroCopyResult result)
        {
            return WildEncounterIntroFormatter.ToPlainText(result != null ? result.richText : string.Empty);
        }
    }
}
