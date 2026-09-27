#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Chainbound.Combat
{
    internal static class WalletActionPadSetupEditor
    {
        private const string RootFolder = "Assets/Scripts/UI/Combat/WalletScreen";
        private const string UxmlPath = RootFolder + "/WalletActionPad.uxml";
        private const string RenderTexturePath = RootFolder + "/WalletScreenRT.renderTexture";
        private const string PanelSettingsPath = RootFolder + "/WalletScreenPanelSettings.asset";
        private const string ScanlineAImagePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_A.png";
        private const string ScanlineBImagePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_B.png";
        private const string ScanlineAPath = RootFolder + "/WalletScanline_A.asset";
        private const string ScanlineBPath = RootFolder + "/WalletScanline_B.asset";
        private const string ScreenMaterialPath = "Assets/Sprites/DepthWrite/DepthWriteUnlit.mat";

        [MenuItem("Tools/Chainbound/Combat/Wallet Screen/Setup Action Pad")]
        private static void SetupActionPad()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError("WalletActionPad.uxml was not found. Setup aborted.");
                return;
            }

            var rt = EnsureRenderTexture();
            var panelSettings = EnsurePanelSettings(rt);
            var scanlineA = EnsureScanlineFrame(ScanlineAImagePath, ScanlineAPath, false);
            var scanlineB = EnsureScanlineFrame(ScanlineBImagePath, ScanlineBPath, true);

            var walletScreenUI = GameObject.Find("WalletScreenUI");
            if (walletScreenUI == null)
                walletScreenUI = new GameObject("WalletScreenUI");

            var document = GetOrAddComponent<UIDocument>(walletScreenUI);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            EditorUtility.SetDirty(document);

            var scanlinesAnimator = GetOrAddComponent<WalletScanlinesAnimator>(walletScreenUI);
            SetObjectField(scanlinesAnimator, "document", document);
            SetObjectField(scanlinesAnimator, "frameA", scanlineA);
            SetObjectField(scanlinesAnimator, "frameB", scanlineB);
            SetFloatField(scanlinesAnimator, "fps", 2f);

            var selectorController = Object.FindFirstObjectByType<CombatActionSelectorController>(FindObjectsInactive.Include);
            var padController = GetOrAddComponent<WalletActionPadController>(walletScreenUI);
            SetObjectField(padController, "walletDocument", document);
            SetObjectField(padController, "selectorController", selectorController);
            SetFloatField(padController, "blinkInterval", 0.35f);

            var screenRenderer = FindWalletScreenRenderer();
            if (screenRenderer != null)
            {
                var screenSurface = GetOrAddComponent<WalletScreenSurface>(screenRenderer.gameObject);
                SetObjectField(screenSurface, "screenRenderer", screenRenderer);
                SetIntField(screenSurface, "materialIndex", 0);
                SetObjectField(screenSurface, "targetRT", rt);
                SetStringField(screenSurface, "textureProperty", "_BaseMap");
                var screenMaterial = AssetDatabase.LoadAssetAtPath<Material>(ScreenMaterialPath);
                if (screenMaterial != null)
                    screenRenderer.sharedMaterial = screenMaterial;
                screenSurface.ApplyRenderTexture();
            }
            else
            {
                Debug.LogWarning("Wallet screen MeshRenderer was not found. Assign WalletScreenSurface manually.");
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
                EditorSceneManager.MarkSceneDirty(activeScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Wallet Action Pad setup complete.");
        }

        private static RenderTexture EnsureRenderTexture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt != null)
                return rt;

            rt = new RenderTexture(320, 180, 0, RenderTextureFormat.ARGB32)
            {
                name = "WalletScreenRT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };

            AssetDatabase.CreateAsset(rt, RenderTexturePath);
            return rt;
        }

        private static PanelSettings EnsurePanelSettings(RenderTexture rt)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = 1f;
            panelSettings.targetTexture = rt;
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = Color.clear;
            EditorUtility.SetDirty(panelSettings);
            return panelSettings;
        }

        private static Texture2D EnsureScanlineFrame(string preferredPath, string fallbackPath, bool offset)
        {
            var preferred = AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath);
            if (preferred != null)
            {
                EnsureScanlineImportSettings(preferredPath);
                return preferred;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPath);
            if (texture == null)
            {
                texture = BuildScanlineTexture(offset);
                AssetDatabase.CreateAsset(texture, fallbackPath);
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void EnsureScanlineImportSettings(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                return;

            bool changed = false;

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static Texture2D BuildScanlineTexture(bool offset)
        {
            const int width = 64;
            const int height = 64;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = offset ? "WalletScanline_B" : "WalletScanline_A",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            var dark = new Color32(60, 78, 66, 110);
            var light = new Color32(88, 110, 94, 70);

            for (int y = 0; y < height; y++)
            {
                bool useLight = ((y + (offset ? 1 : 0)) & 1) == 0;
                var rowColor = useLight ? light : dark;
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, rowColor);
            }

            tex.Apply(false, false);
            return tex;
        }

        private static MeshRenderer FindWalletScreenRenderer()
        {
            var byPath = GameObject.Find("Hardware_Wallet/ScreenMeshRenderer");
            if (byPath != null)
                return byPath.GetComponent<MeshRenderer>();

            var named = GameObject.Find("ScreenMeshRenderer");
            if (named != null)
                return named.GetComponent<MeshRenderer>();

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null)
                return existing;

            return Undo.AddComponent<T>(go);
        }

        private static void SetObjectField(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var prop = serializedObject.FindProperty(fieldName);
            if (prop == null)
                return;

            prop.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetFloatField(Object target, string fieldName, float value)
        {
            var serializedObject = new SerializedObject(target);
            var prop = serializedObject.FindProperty(fieldName);
            if (prop == null)
                return;

            prop.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetIntField(Object target, string fieldName, int value)
        {
            var serializedObject = new SerializedObject(target);
            var prop = serializedObject.FindProperty(fieldName);
            if (prop == null)
                return;

            prop.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetStringField(Object target, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var prop = serializedObject.FindProperty(fieldName);
            if (prop == null)
                return;

            prop.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif

