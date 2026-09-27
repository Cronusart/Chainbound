#if UNITY_EDITOR
using System;
using SignalJam.UI.MenuNavigation.PartyTab;
using SignalJam.VisualFx;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SignalJam.VisualFx.Editor
{
    public static class CycleProgressVisualBootstrap
    {
        private const string RootFolder = "Assets/Data/UI/VisualFx/CycleProgress";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string CatalogPath = RootFolder + "/PartyTabCycleVisualCatalog.asset";
        private const string PartyTabTypographyPath = "Assets/Data/UI/PartyTab/PartyTabCycleTypography.asset";
        private const string FontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        private const string MaterialTemplatePath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans_Underlay.mat";

        private static readonly string[] TierIds =
        {
            "cycle_i",
            "cycle_ii",
            "cycle_iii",
            "cycle_iv",
            "cycle_v",
        };

        private static readonly string[] TierNames =
        {
            "Cycle I",
            "Cycle II",
            "Cycle III",
            "Cycle IV",
            "Cycle V",
        };

        [MenuItem("Chainbound/VFX/Create Cycle Progress Visual Assets")]
        public static void CreateCycleProgressAssets()
        {
            EnsureFolder("Assets/Data/UI/VisualFx");
            EnsureFolder(RootFolder);
            EnsureFolder(MaterialFolder);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material template = AssetDatabase.LoadAssetAtPath<Material>(MaterialTemplatePath);
            if (font == null || template == null)
            {
                Debug.LogError("[CycleProgress] Missing PixeloidSans SDF or Underlay template material.");
                return;
            }

            VisualVariantCatalogEntry[] catalogEntries = new VisualVariantCatalogEntry[TierIds.Length];
            for (int i = 0; i < TierIds.Length; i++)
                catalogEntries[i] = BuildTier(i, font, template);

            VisualVariantCatalogSO catalog = CreateOrLoad<VisualVariantCatalogSO>(CatalogPath);
            SerializedObject catalogObject = new SerializedObject(catalog);
            SerializedProperty entries = catalogObject.FindProperty("entries");
            entries.arraySize = catalogEntries.Length;
            for (int i = 0; i < catalogEntries.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("tierId").stringValue = catalogEntries[i].tierId;
                entry.FindPropertyRelative("tierIndex").intValue = catalogEntries[i].tierIndex;
                entry.FindPropertyRelative("profile").objectReferenceValue = catalogEntries[i].profile;
            }

            catalogObject.ApplyModifiedPropertiesWithoutUndo();

            PartyTabCycleTypographySO partyTypography =
                CreateOrLoad<PartyTabCycleTypographySO>(PartyTabTypographyPath);
            SerializedObject partyObject = new SerializedObject(partyTypography);
            partyObject.FindProperty("variantCatalog").objectReferenceValue = catalog;
            partyObject.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            EditorUtility.FocusProjectWindow();

            Debug.Log(
                "[CycleProgress] Created cycle I-V visual assets under Assets/Data/UI/VisualFx/CycleProgress " +
                "and wired PartyTabCycleTypography.asset.");
        }

        private static VisualVariantCatalogEntry BuildTier(int index, TMP_FontAsset font, Material template)
        {
            TierDefinition definition = TierDefinition.ForIndex(index);
            string suffix = TierIds[index];

            VisualTierPaletteSO palette = CreateOrLoad<VisualTierPaletteSO>($"{RootFolder}/Palette_{suffix}.asset");
            ApplyPalette(palette, definition);

            Material material = CreateOrLoadMaterial($"{MaterialFolder}/CycleTypography_{suffix}.mat", template);
            ApplyMaterial(material, definition);

            VisualTierTypographyPresetSO typography =
                CreateOrLoad<VisualTierTypographyPresetSO>($"{RootFolder}/Typography_{suffix}.asset");
            ApplyTypography(typography, font, material, palette, definition);

            VisualTierProfileSO profile = CreateOrLoad<VisualTierProfileSO>($"{RootFolder}/Profile_{suffix}.asset");
            SerializedObject profileObject = new SerializedObject(profile);
            profileObject.FindProperty("tierId").stringValue = suffix;
            profileObject.FindProperty("displayName").stringValue = TierNames[index];
            profileObject.FindProperty("sharedPalette").objectReferenceValue = palette;
            profileObject.FindProperty("typographyPreset").objectReferenceValue = typography;
            profileObject.ApplyModifiedPropertiesWithoutUndo();

            return new VisualVariantCatalogEntry
            {
                tierId = suffix,
                tierIndex = index,
                profile = profile,
            };
        }

        private static void ApplyPalette(VisualTierPaletteSO palette, TierDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(palette);
            serialized.FindProperty("paletteId").stringValue = definition.PaletteId;
            serialized.FindProperty("primaryColor").colorValue = definition.Primary;
            serialized.FindProperty("secondaryColor").colorValue = definition.Secondary;
            serialized.FindProperty("accentColor").colorValue = definition.Accent;
            serialized.FindProperty("glowColor").colorValue = definition.Glow;
            serialized.FindProperty("rimColor").colorValue = definition.Rim;
            serialized.FindProperty("shadowColor").colorValue = definition.Shadow;
            serialized.FindProperty("emissiveColor").colorValue = definition.Emissive;
            serialized.FindProperty("masterIntensity").floatValue = definition.MasterIntensity;
            serialized.FindProperty("alphaMultiplier").floatValue = 1f;
            serialized.FindProperty("saturationMultiplier").floatValue = definition.Saturation;
            serialized.FindProperty("pulseDurationSeconds").floatValue = definition.PulseDuration;

            if (definition.UsePulse)
            {
                AnimationCurve pulse = AnimationCurve.EaseInOut(0f, 0.65f, 1f, 1f);
                serialized.FindProperty("intensityPulseCurve").animationCurveValue = pulse;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyMaterial(Material material, TierDefinition definition)
        {
            // Vertex-gradient tiers need a white face; Distance Field multiplies face * vertex color.
            material.SetColor(
                "_FaceColor",
                definition.UseVertexGradient ? Color.white : definition.Primary);

            if (definition.EnableOutline)
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetColor("_OutlineColor", definition.OutlineColor);
                material.SetFloat("_OutlineWidth", definition.OutlineWidth);
            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
                material.SetFloat("_OutlineWidth", 0f);
            }

            if (definition.EnableUnderlay)
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", definition.UnderlayColor);
                material.SetFloat("_UnderlayOffsetX", definition.UnderlayOffset.x);
                material.SetFloat("_UnderlayOffsetY", definition.UnderlayOffset.y);
                material.SetFloat("_UnderlayDilate", definition.UnderlayDilate);
                material.SetFloat("_UnderlaySoftness", definition.UnderlaySoftness);
            }
            else
            {
                material.DisableKeyword("UNDERLAY_ON");
            }

            if (definition.EnableGlow)
            {
                material.EnableKeyword("GLOW_ON");
                material.SetColor("_GlowColor", definition.Glow);
                material.SetFloat("_GlowPower", definition.GlowPower);
                material.SetFloat("_GlowOuter", definition.GlowOuter);
            }
            else
            {
                material.DisableKeyword("GLOW_ON");
                material.SetFloat("_GlowPower", 0f);
            }

            EditorUtility.SetDirty(material);
        }

        private static void ApplyTypography(
            VisualTierTypographyPresetSO typography,
            TMP_FontAsset font,
            Material material,
            VisualTierPaletteSO palette,
            TierDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(typography);
            serialized.FindProperty("palette").objectReferenceValue = palette;
            serialized.FindProperty("fontAsset").objectReferenceValue = font;
            serialized.FindProperty("materialPreset").objectReferenceValue = material;
            serialized.FindProperty("materialAssignment").enumValueIndex =
                (int)VisualTierMaterialAssignmentMode.MaterialInstance;
            serialized.FindProperty("faceColorSlot").enumValueIndex = (int)VisualTierColorSlot.Primary;
            serialized.FindProperty("applyFaceColorToTextColor").boolValue = !definition.UseVertexGradient;
            serialized.FindProperty("driveOutlineFromPalette").boolValue = definition.EnableOutline;
            serialized.FindProperty("outlineColorSlot").enumValueIndex = (int)VisualTierColorSlot.Shadow;
            serialized.FindProperty("customOutlineColor").colorValue = definition.OutlineColor;
            serialized.FindProperty("outlineWidth").floatValue = definition.OutlineWidth;
            serialized.FindProperty("driveUnderlayFromPalette").boolValue = definition.EnableUnderlay;
            serialized.FindProperty("underlayColorSlot").enumValueIndex = (int)VisualTierColorSlot.Glow;
            serialized.FindProperty("customUnderlayColor").colorValue = definition.UnderlayColor;
            serialized.FindProperty("underlayOffset").vector2Value = definition.UnderlayOffset;
            serialized.FindProperty("underlayDilate").floatValue = definition.UnderlayDilate;
            serialized.FindProperty("underlaySoftness").floatValue = definition.UnderlaySoftness;
            serialized.FindProperty("driveGlowFromPalette").boolValue = definition.EnableGlow;
            serialized.FindProperty("glowColorSlot").enumValueIndex = (int)VisualTierColorSlot.Glow;
            serialized.FindProperty("customGlowColor").colorValue = definition.Glow;
            serialized.FindProperty("glowPower").floatValue = definition.GlowPower;
            serialized.FindProperty("fontSizeMultiplier").floatValue = 1f;
            serialized.FindProperty("transformScaleMultiplier").floatValue = definition.TransformScale;
            serialized.FindProperty("useVertexGradient").boolValue = definition.UseVertexGradient;
            serialized.FindProperty("gradientScope").enumValueIndex = definition.UsePerTextBoundsGradient
                ? (int)VisualTierGradientScope.PerTextBounds
                : (int)VisualTierGradientScope.PerCharacter;
            serialized.FindProperty("vertexGradientMode").enumValueIndex =
                definition.UseVertexGradient ? (int)VisualTierVertexGradientMode.Horizontal : 0;
            serialized.FindProperty("vertexGradientStartSlot").enumValueIndex = (int)VisualTierColorSlot.Primary;
            serialized.FindProperty("vertexGradientEndSlot").enumValueIndex = (int)VisualTierColorSlot.Accent;
            serialized.FindProperty("customVertexGradientStart").colorValue = definition.Primary;
            serialized.FindProperty("customVertexGradientEnd").colorValue = definition.Accent;
            serialized.FindProperty("vertexGradientTopSlot").enumValueIndex = (int)VisualTierColorSlot.Primary;
            serialized.FindProperty("vertexGradientBottomSlot").enumValueIndex = (int)VisualTierColorSlot.Accent;
            serialized.FindProperty("customVertexGradientTop").colorValue = definition.Primary;
            serialized.FindProperty("customVertexGradientBottom").colorValue = definition.Accent;
            if (definition.UseVertexGradient)
            {
                serialized.FindProperty("underlayColorSlot").enumValueIndex = (int)VisualTierColorSlot.Primary;
                serialized.FindProperty("customUnderlayColor").colorValue = definition.UnderlayColor;
                serialized.FindProperty("outlineColorMode").enumValueIndex =
                    (int)VisualTierTypographyChannelColorMode.GradientStart;
                serialized.FindProperty("underlayColorMode").enumValueIndex =
                    (int)VisualTierTypographyChannelColorMode.GradientStart;
                serialized.FindProperty("glowColorMode").enumValueIndex =
                    (int)VisualTierTypographyChannelColorMode.GradientEnd;
            }
            else
            {
                serialized.FindProperty("outlineColorMode").enumValueIndex =
                    (int)VisualTierTypographyChannelColorMode.Uniform;
                serialized.FindProperty("underlayColorMode").enumValueIndex =
                    (int)VisualTierTypographyChannelColorMode.Uniform;
                serialized.FindProperty("glowColorMode").enumValueIndex =
                    (int)VisualTierTypographyChannelColorMode.Uniform;
            }
            serialized.FindProperty("animatePalettePulse").boolValue = definition.UsePulse;
            if (definition.UsePulse)
            {
                serialized.FindProperty("pulseIntensityMin").floatValue = 0.4f;
                serialized.FindProperty("pulseIntensityMax").floatValue = 1.85f;
                serialized.FindProperty("pulseGlowOuter").boolValue = true;
            }

            serialized.FindProperty("animateGradientShimmer").boolValue = definition.UseVertexGradient;
            serialized.FindProperty("gradientShimmerSpeed").floatValue =
                definition.UseVertexGradient ? 0.35f : 0f;
            serialized.FindProperty("gradientShimmerUseUnscaledTime").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateOrLoadMaterial(string path, Material template)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Material material = new Material(template);
            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct TierDefinition
        {
            public string PaletteId { get; }
            public Color Primary { get; }
            public Color Secondary { get; }
            public Color Accent { get; }
            public Color Glow { get; }
            public Color Rim { get; }
            public Color Shadow { get; }
            public Color Emissive { get; }
            public Color OutlineColor { get; }
            public Color UnderlayColor { get; }
            public float MasterIntensity { get; }
            public float Saturation { get; }
            public float TransformScale { get; }
            public float OutlineWidth { get; }
            public float UnderlayDilate { get; }
            public float UnderlaySoftness { get; }
            public float GlowPower { get; }
            public float GlowOuter { get; }
            public float PulseDuration { get; }
            public Vector2 UnderlayOffset { get; }
            public bool EnableOutline { get; }
            public bool EnableUnderlay { get; }
            public bool EnableGlow { get; }
            public bool UseVertexGradient { get; }
            public bool UsePerTextBoundsGradient { get; }
            public bool UsePulse { get; }

            private TierDefinition(
                string paletteId,
                Color primary,
                Color secondary,
                Color accent,
                Color glow,
                Color rim,
                Color shadow,
                Color emissive,
                Color outlineColor,
                Color underlayColor,
                float masterIntensity,
                float saturation,
                float transformScale,
                float outlineWidth,
                float underlayDilate,
                float underlaySoftness,
                float glowPower,
                float glowOuter,
                float pulseDuration,
                Vector2 underlayOffset,
                bool enableOutline,
                bool enableUnderlay,
                bool enableGlow,
                bool useVertexGradient,
                bool usePerTextBoundsGradient,
                bool usePulse)
            {
                PaletteId = paletteId;
                Primary = primary;
                Secondary = secondary;
                Accent = accent;
                Glow = glow;
                Rim = rim;
                Shadow = shadow;
                Emissive = emissive;
                OutlineColor = outlineColor;
                UnderlayColor = underlayColor;
                MasterIntensity = masterIntensity;
                Saturation = saturation;
                TransformScale = transformScale;
                OutlineWidth = outlineWidth;
                UnderlayDilate = underlayDilate;
                UnderlaySoftness = underlaySoftness;
                GlowPower = glowPower;
                GlowOuter = glowOuter;
                PulseDuration = pulseDuration;
                UnderlayOffset = underlayOffset;
                EnableOutline = enableOutline;
                EnableUnderlay = enableUnderlay;
                EnableGlow = enableGlow;
                UseVertexGradient = useVertexGradient;
                UsePerTextBoundsGradient = usePerTextBoundsGradient;
                UsePulse = usePulse;
            }

            public static TierDefinition ForIndex(int index)
            {
                return index switch
                {
                    0 => CycleI(),
                    1 => CycleII(),
                    2 => CycleIII(),
                    3 => CycleIV(),
                    _ => CycleV(),
                };
            }

            private static TierDefinition CycleI()
            {
                Color white = new Color(0.96f, 0.98f, 1f, 1f);
                Color glow = new Color(0.90f, 0.95f, 1f, 0.42f);
                return new TierDefinition(
                    "cycle_i_white_glow",
                    white,
                    new Color(0.88f, 0.93f, 1f, 1f),
                    new Color(0.82f, 0.90f, 1f, 1f),
                    glow,
                    glow,
                    new Color(0.20f, 0.24f, 0.32f, 0.85f),
                    white,
                    new Color(0.18f, 0.22f, 0.30f, 0.75f),
                    glow,
                    1f,
                    1f,
                    1f,
                    0f,
                    0.04f,
                    0.55f,
                    0.12f,
                    0.12f,
                    1.6f,
                    new Vector2(0f, -0.25f),
                    enableOutline: false,
                    enableUnderlay: true,
                    enableGlow: false,
                    useVertexGradient: false,
                    usePerTextBoundsGradient: false,
                    usePulse: false);
            }

            private static TierDefinition CycleII()
            {
                Color bronze = new Color(0.86f, 0.62f, 0.38f, 1f);
                Color bronzeDark = new Color(0.42f, 0.28f, 0.16f, 1f);
                return new TierDefinition(
                    "cycle_ii_bronze",
                    bronze,
                    new Color(0.72f, 0.48f, 0.28f, 1f),
                    new Color(0.95f, 0.76f, 0.46f, 1f),
                    new Color(1f, 0.78f, 0.42f, 0.55f),
                    new Color(0.98f, 0.70f, 0.36f, 1f),
                    bronzeDark,
                    bronze,
                    bronzeDark,
                    new Color(0.78f, 0.48f, 0.22f, 0.35f),
                    1.05f,
                    1.08f,
                    1.02f,
                    0.12f,
                    0.08f,
                    0.42f,
                    0.18f,
                    0.18f,
                    1.6f,
                    new Vector2(0f, -0.28f),
                    enableOutline: true,
                    enableUnderlay: true,
                    enableGlow: false,
                    useVertexGradient: false,
                    usePerTextBoundsGradient: false,
                    usePulse: false);
            }

            private static TierDefinition CycleIII()
            {
                Color silver = new Color(0.84f, 0.88f, 0.94f, 1f);
                Color silverDark = new Color(0.34f, 0.38f, 0.46f, 1f);
                return new TierDefinition(
                    "cycle_iii_silver",
                    silver,
                    new Color(0.70f, 0.76f, 0.86f, 1f),
                    new Color(0.96f, 0.98f, 1f, 1f),
                    new Color(0.82f, 0.90f, 1f, 0.62f),
                    new Color(0.92f, 0.96f, 1f, 1f),
                    silverDark,
                    silver,
                    silverDark,
                    new Color(0.72f, 0.82f, 0.98f, 0.38f),
                    1.08f,
                    1f,
                    1.05f,
                    0.18f,
                    0.10f,
                    0.48f,
                    0.24f,
                    0.22f,
                    1.6f,
                    new Vector2(0f, -0.30f),
                    enableOutline: true,
                    enableUnderlay: true,
                    enableGlow: true,
                    useVertexGradient: false,
                    usePerTextBoundsGradient: false,
                    usePulse: false);
            }

            private static TierDefinition CycleIV()
            {
                Color gold = new Color(1f, 0.84f, 0.34f, 1f);
                Color goldDark = new Color(0.48f, 0.30f, 0.08f, 1f);
                return new TierDefinition(
                    "cycle_iv_gold",
                    gold,
                    new Color(0.98f, 0.68f, 0.14f, 1f),
                    new Color(1f, 0.92f, 0.52f, 1f),
                    new Color(1f, 0.78f, 0.22f, 0.72f),
                    new Color(1f, 0.72f, 0.18f, 1f),
                    goldDark,
                    gold,
                    goldDark,
                    new Color(1f, 0.72f, 0.18f, 0.42f),
                    1.12f,
                    1.12f,
                    1.08f,
                    0.24f,
                    0.12f,
                    0.52f,
                    0.34f,
                    0.28f,
                    1.6f,
                    new Vector2(0f, -0.32f),
                    enableOutline: true,
                    enableUnderlay: true,
                    enableGlow: true,
                    useVertexGradient: false,
                    usePerTextBoundsGradient: false,
                    usePulse: false);
            }

            private static TierDefinition CycleV()
            {
                Color solPurple = new Color(0.60f, 0.27f, 1f, 1f);
                Color solGreen = new Color(0.08f, 0.95f, 0.58f, 1f);
                Color solTeal = new Color(0.18f, 0.82f, 0.86f, 1f);
                return new TierDefinition(
                    "cycle_v_solana",
                    solPurple,
                    solTeal,
                    solGreen,
                    solGreen,
                    solPurple,
                    new Color(0.16f, 0.08f, 0.28f, 0.92f),
                    solTeal,
                    new Color(0.12f, 0.05f, 0.22f, 1f),
                    new Color(0.60f, 0.27f, 1f, 0.30f),
                    1f,
                    1.15f,
                    1.12f,
                    0.28f,
                    0.14f,
                    0.58f,
                    0.52f,
                    0.36f,
                    1.35f,
                    new Vector2(0f, -0.34f),
                    enableOutline: true,
                    enableUnderlay: true,
                    enableGlow: true,
                    useVertexGradient: true,
                    usePerTextBoundsGradient: true,
                    usePulse: true);
            }
        }
    }
}
#endif
