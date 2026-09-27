using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat.Tests
{
    public class ChainlingPartyColumnVariantTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void VisualIdentityCloneAndNormalizePreservesPartyColumnVariantId()
        {
            var identity = new ChainlingVisualIdentity
            {
                partyColumnVariantId = " neon_frame ",
            };

            var clone = ChainlingVisualIdentity.CloneOrDefault(identity);
            clone.Normalize();

            Assert.AreEqual("neon_frame", clone.partyColumnVariantId);
        }

        [Test]
        public void VisualIdentitySaveCloneUsesPartyColumnFallback()
        {
            var identity = new ChainlingVisualIdentity
            {
                hudCardVariantId = "retro_card",
                partyColumnVariantId = "",
            };

            var save = ChainlingVisualIdentity.CloneForPlayerSave(identity);

            Assert.AreEqual("retro_card", save.hudCardVariantId);
            Assert.AreEqual(ChainlingVisualIdentity.DefaultId, save.partyColumnVariantId);
        }

        [Test]
        public void ResolverReturnsPartyColumnVariantFromIdentityDatabase()
        {
            var identityDatabase = CreateAsset<ChainlingVisualIdentityDatabaseSO>();
            var defaultVariant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            var neonVariant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(defaultVariant, ChainlingVisualIdentity.DefaultId);
            SetDefinitionId(neonVariant, "neon_frame");
            SetPrivateField(identityDatabase, "partyColumnVariants", new List<ChainlingPartyColumnVariantDefinitionSO>
            {
                defaultVariant,
                neonVariant,
            });

            var identity = new ChainlingVisualIdentity { partyColumnVariantId = "neon_frame" };
            ResolvedChainlingVisualIdentity resolved = ChainlingVisualIdentityResolver.Resolve(
                "sparkpup",
                identity,
                null,
                identityDatabase);

            Assert.AreSame(neonVariant, resolved.partyColumnVariant);
        }

        [Test]
        public void ResolverFallsBackToDefaultPartyColumnVariant()
        {
            var identityDatabase = CreateAsset<ChainlingVisualIdentityDatabaseSO>();
            var defaultVariant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(defaultVariant, ChainlingVisualIdentity.DefaultId);
            SetPrivateField(identityDatabase, "partyColumnVariants", new List<ChainlingPartyColumnVariantDefinitionSO>
            {
                defaultVariant,
            });

            var identity = new ChainlingVisualIdentity { partyColumnVariantId = "missing_variant" };
            ResolvedChainlingVisualIdentity resolved = ChainlingVisualIdentityResolver.Resolve(
                "sparkpup",
                identity,
                null,
                identityDatabase);

            Assert.AreSame(defaultVariant, resolved.partyColumnVariant);
        }

        [Test]
        public void PartyColumnShellApplierApplyAndRestore()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            Sprite defaultSprite = CreateSprite();
            Sprite customSprite = CreateSprite();

            shell.CardChromeImage.sprite = defaultSprite;
            shell.ApplyPartyColumnVariant(null);
            Assert.AreSame(defaultSprite, shell.CardChromeImage.sprite);

            var variant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(variant, "neon_frame");
            variant.staticCardFrameSprite = customSprite;
            shell.ApplyPartyColumnVariant(variant);
            Assert.AreSame(customSprite, shell.CardChromeImage.sprite);

            shell.ApplyPartyColumnVariant(null);
            Assert.AreSame(defaultSprite, shell.CardChromeImage.sprite);
        }

        [Test]
        public void PartyColumnShellEmptyHidesCardChrome()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            var variant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(variant, "neon_frame");
            variant.staticCardFrameSprite = CreateSprite();

            shell.ApplyPartyColumnVariant(variant);
            shell.Bind(
                0,
                entry: null,
                species: null,
                resolvedVisualIdentity: new ResolvedChainlingVisualIdentity { partyColumnVariant = variant },
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            Assert.IsFalse(shell.CardChromeImage.enabled);
        }

        [Test]
        public void PartyColumnShellFilledShowsCardChrome()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            var variant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(variant, "neon_frame");
            variant.staticCardFrameSprite = CreateSprite();

            shell.ApplyPartyColumnVariant(variant);
            shell.Bind(
                0,
                new PlayerRosterEntry
                {
                    snapshot = new ChainlingSnapshot
                    {
                        backendChainlingId = "chainling-1",
                        speciesId = "sparkpup",
                    },
                },
                species: null,
                resolvedVisualIdentity: new ResolvedChainlingVisualIdentity { partyColumnVariant = variant },
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            Assert.IsTrue(shell.CardChromeImage.enabled);
            shell.SetFocused(true);
            Assert.IsTrue(shell.CardChromeImage.enabled);
        }

        [Test]
        public void PartyColumnShellAppliesPaletteColorsFromPrimaryType()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            ChainlingPartyTypeChromePaletteSO palette = CreatePalette(
                ChainlingType.Gas,
                new Color(0.11f, 0.22f, 0.33f, 0.4f),
                new Color(0.44f, 0.55f, 0.66f, 0.5f));
            SpeciesDefinitionSO species = CreateSpecies(ChainlingType.Gas);

            shell.ConfigureTypeChromePalette(palette);
            shell.Bind(
                0,
                CreateFilledEntry(),
                species,
                resolvedVisualIdentity: null,
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            AssertColor(new Color(0.11f, 0.22f, 0.33f, 1f), shell.TypeBackgroundImage.color);
            AssertColor(new Color(0.44f, 0.55f, 0.66f, 1f), shell.EmptyPedestalImage.color);
        }

        [Test]
        public void PartyColumnShellDualTypeUsesPrimaryType()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            ChainlingPartyTypeChromePaletteSO palette = CreatePalette(
                ChainlingType.Gas,
                new Color(0.12f, 0.23f, 0.34f, 1f),
                new Color(0.45f, 0.56f, 0.67f, 1f),
                ChainlingType.Burned,
                new Color(0.7f, 0.1f, 0.1f, 1f),
                new Color(0.8f, 0.2f, 0.2f, 1f));
            SpeciesDefinitionSO species = CreateSpecies(ChainlingType.Gas, ChainlingType.Burned);

            shell.ConfigureTypeChromePalette(palette);
            shell.Bind(
                0,
                CreateFilledEntry(),
                species,
                resolvedVisualIdentity: null,
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            AssertColor(new Color(0.12f, 0.23f, 0.34f, 1f), shell.TypeBackgroundImage.color);
            AssertColor(new Color(0.45f, 0.56f, 0.67f, 1f), shell.EmptyPedestalImage.color);
        }

        [Test]
        public void PartyColumnShellEmptyUsesNeutralPaletteColors()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            ChainlingPartyTypeChromePaletteSO palette = CreatePalette(
                ChainlingType.Node,
                Color.blue,
                Color.cyan);
            SetPrivateField(
                palette,
                "emptyBackgroundColor",
                new Color(0.01f, 0.02f, 0.03f, 0.6f),
                typeof(ChainlingPartyTypeChromePaletteSO));
            SetPrivateField(
                palette,
                "emptyPedestalColor",
                new Color(0.04f, 0.05f, 0.06f, 0.7f),
                typeof(ChainlingPartyTypeChromePaletteSO));

            shell.ConfigureTypeChromePalette(palette);
            shell.Bind(
                0,
                entry: null,
                species: null,
                resolvedVisualIdentity: null,
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            AssertColor(new Color(0.01f, 0.02f, 0.03f, 0.6f), shell.TypeBackgroundImage.color);
            AssertColor(new Color(0.04f, 0.05f, 0.06f, 0.7f), shell.EmptyPedestalImage.color);
        }

        [Test]
        public void PartyColumnShellMissingPaletteFallsBackToTypeDisplayColor()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            SpeciesDefinitionSO species = CreateSpecies(ChainlingType.Shard);

            shell.Bind(
                0,
                CreateFilledEntry(),
                species,
                resolvedVisualIdentity: null,
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            Color expected = ChainlingTypeDisplaySO.CreateFallback(ChainlingType.Shard).badgeColor;
            expected.a = 1f;
            AssertColor(expected, shell.TypeBackgroundImage.color);
            AssertColor(expected, shell.EmptyPedestalImage.color);
        }

        [Test]
        public void PartyColumnShellMissingTypeEntryFallsBackToTypeDisplayColor()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            ChainlingPartyTypeChromePaletteSO palette = CreatePalette(
                ChainlingType.Node,
                Color.blue,
                Color.cyan);
            SpeciesDefinitionSO species = CreateSpecies(ChainlingType.Bot);

            shell.ConfigureTypeChromePalette(palette);
            shell.Bind(
                0,
                CreateFilledEntry(),
                species,
                resolvedVisualIdentity: null,
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            Color expected = ChainlingTypeDisplaySO.CreateFallback(ChainlingType.Bot).badgeColor;
            expected.a = 1f;
            AssertColor(expected, shell.TypeBackgroundImage.color);
            AssertColor(expected, shell.EmptyPedestalImage.color);
        }

        [Test]
        public void PartyColumnVariantDoesNotOverwriteTypeBackgroundOrPedestalTint()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            Sprite customSprite = CreateSprite();
            var variant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(variant, "neon_frame");
            variant.staticCardFrameSprite = customSprite;
            variant.overrideBackgroundColor = true;
            variant.backgroundColorTint = Color.red;

            ChainlingPartyTypeChromePaletteSO palette = CreatePalette(
                ChainlingType.Compressed,
                new Color(0.21f, 0.31f, 0.41f, 1f),
                new Color(0.51f, 0.61f, 0.71f, 1f));
            SpeciesDefinitionSO species = CreateSpecies(ChainlingType.Compressed);

            shell.ConfigureTypeChromePalette(palette);
            shell.Bind(
                0,
                CreateFilledEntry(),
                species,
                new ResolvedChainlingVisualIdentity { partyColumnVariant = variant },
                summary: null,
                viewMode: ChainlingPartyViewMode.General,
                focused: false,
                focusHandler: null,
                submitHandler: null);

            Assert.AreSame(customSprite, shell.CardChromeImage.sprite);
            AssertColor(new Color(0.21f, 0.31f, 0.41f, 1f), shell.TypeBackgroundImage.color);
            AssertColor(new Color(0.51f, 0.61f, 0.71f, 1f), shell.EmptyPedestalImage.color);
        }

        [Test]
        public void PartyColumnVariantDoesNotOverwriteInfoPanelBackground()
        {
            ChainlingPartyColumnShell shell = CreateShell();
            Image infoPanelBackground = FindInfoPanelBackground(shell);
            Sprite defaultSprite = CreateSprite();
            Sprite variantSprite = CreateSprite();
            Color defaultColor = new Color(0.13f, 0.17f, 0.23f, 0.91f);

            infoPanelBackground.sprite = defaultSprite;
            infoPanelBackground.color = defaultColor;

            var variant = CreateAsset<ChainlingPartyColumnVariantDefinitionSO>();
            SetDefinitionId(variant, "neon_frame");
            variant.infoPanelBackgroundSprite = variantSprite;

            shell.ApplyPartyColumnVariant(variant);
            Assert.AreSame(defaultSprite, infoPanelBackground.sprite);
            AssertColor(defaultColor, infoPanelBackground.color);

            shell.ApplyPartyColumnVariant(null);
            Assert.AreSame(defaultSprite, infoPanelBackground.sprite);
            AssertColor(defaultColor, infoPanelBackground.color);
        }

        [Test]
        public void InfoPanelViewContentGroupTargetsViewsRoot()
        {
            ChainlingPartyColumnShell shell = CreateShell();

            CanvasGroup viewContentGroup = shell.InfoPanelViewContentGroup;

            Assert.NotNull(viewContentGroup);
            Assert.AreEqual("Views", viewContentGroup.name);
            Assert.AreNotSame(shell.InfoPanelGroup, viewContentGroup);
        }

        [Test]
        public void ValidateOwnedCosmeticsRejectsUnownedPartyColumnId()
        {
            var identity = new ChainlingVisualIdentity
            {
                partyColumnVariantId = "neon_frame",
            };

            string error = InvokeValidateOwnedCosmetics(
                identity,
                new[] { ChainlingVisualIdentity.DefaultId });

            Assert.AreEqual("cosmetic_not_owned:neon_frame", error);
        }

        [Test]
        public void VisualIdentityMutationPatchesPartyColumnVariantInRosterCache()
        {
            var roster = new PlayerRosterSnapshot
            {
                chainlings = new List<PlayerRosterEntry>
                {
                    new PlayerRosterEntry
                    {
                        snapshot = new ChainlingSnapshot
                        {
                            backendChainlingId = "chainling-1",
                            visualIdentity = ChainlingVisualIdentity.CreateDefault(),
                        },
                    },
                },
            };

            var identity = new ChainlingVisualIdentity { partyColumnVariantId = "neon_frame" };
            bool patched = SupabaseVisualIdentityMutation.PatchRosterCache(roster, "chainling-1", identity);

            Assert.IsTrue(patched);
            Assert.AreEqual("neon_frame", roster.chainlings[0].snapshot.visualIdentity.partyColumnVariantId);
        }

        private ChainlingPartyColumnShell CreateShell()
        {
            var root = new GameObject(
                "party_column_shell",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(ChainlingPartyColumnShell));
            _createdObjects.Add(root);

            ChainlingPartyColumnShell shell = root.GetComponent<ChainlingPartyColumnShell>();
            shell.BuildDefaultLayoutForEditor();
            return shell;
        }

        private static Image FindInfoPanelBackground(ChainlingPartyColumnShell shell)
        {
            Transform background = shell.transform.Find("InfoPanel/Background");
            Assert.NotNull(background, "Expected generated InfoPanel/Background.");
            Image image = background.GetComponent<Image>();
            Assert.NotNull(image, "Expected InfoPanel/Background Image.");
            return image;
        }

        private static string InvokeValidateOwnedCosmetics(
            ChainlingVisualIdentity identity,
            IReadOnlyCollection<string> ownedCosmeticIds)
        {
            MethodInfo method = typeof(SupabaseVisualIdentityMutation).GetMethod(
                "ValidateOwnedCosmetics",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, "Missing ValidateOwnedCosmetics on SupabaseVisualIdentityMutation.");
            return (string)method.Invoke(null, new object[] { identity, ownedCosmeticIds });
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }

        private ChainlingPartyTypeChromePaletteSO CreatePalette(
            ChainlingType type,
            Color backgroundColor,
            Color pedestalColor)
        {
            var palette = CreateAsset<ChainlingPartyTypeChromePaletteSO>();
            SetPrivateField(
                palette,
                "entries",
                new List<ChainlingPartyTypeChromePaletteSO.TypeChromeEntry>
                {
                    new ChainlingPartyTypeChromePaletteSO.TypeChromeEntry
                    {
                        type = type,
                        backgroundColor = backgroundColor,
                        pedestalColor = pedestalColor,
                    },
                },
                typeof(ChainlingPartyTypeChromePaletteSO));
            return palette;
        }

        private ChainlingPartyTypeChromePaletteSO CreatePalette(
            ChainlingType primaryType,
            Color primaryBackgroundColor,
            Color primaryPedestalColor,
            ChainlingType secondaryType,
            Color secondaryBackgroundColor,
            Color secondaryPedestalColor)
        {
            var palette = CreateAsset<ChainlingPartyTypeChromePaletteSO>();
            SetPrivateField(
                palette,
                "entries",
                new List<ChainlingPartyTypeChromePaletteSO.TypeChromeEntry>
                {
                    new ChainlingPartyTypeChromePaletteSO.TypeChromeEntry
                    {
                        type = primaryType,
                        backgroundColor = primaryBackgroundColor,
                        pedestalColor = primaryPedestalColor,
                    },
                    new ChainlingPartyTypeChromePaletteSO.TypeChromeEntry
                    {
                        type = secondaryType,
                        backgroundColor = secondaryBackgroundColor,
                        pedestalColor = secondaryPedestalColor,
                    },
                },
                typeof(ChainlingPartyTypeChromePaletteSO));
            return palette;
        }

        private SpeciesDefinitionSO CreateSpecies(
            ChainlingType primaryType,
            ChainlingType secondaryType = ChainlingType.None)
        {
            var species = CreateAsset<SpeciesDefinitionSO>();
            species.primaryType = primaryType;
            species.secondaryType = secondaryType;
            species.speciesId = "test_species";
            species.displayName = "Test Species";
            return species;
        }

        private static PlayerRosterEntry CreateFilledEntry()
        {
            return new PlayerRosterEntry
            {
                snapshot = new ChainlingSnapshot
                {
                    backendChainlingId = "chainling-1",
                    speciesId = "test_species",
                },
            };
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            _createdObjects.Add(sprite);
            return sprite;
        }

        private static void SetDefinitionId(ChainlingVisualDefinitionSO definition, string id)
        {
            SetPrivateField(definition, "id", id, typeof(ChainlingVisualDefinitionSO));
        }

        private static void SetPrivateField(object target, string fieldName, object value, System.Type ownerType = null)
        {
            ownerType ??= target.GetType();
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing private field {fieldName} on {ownerType.Name}.");
            field.SetValue(target, value);
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }
    }
}
