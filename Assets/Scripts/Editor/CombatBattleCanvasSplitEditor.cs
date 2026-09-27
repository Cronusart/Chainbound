using Chainbound.Combat;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Editor
{
    /// <summary>
    /// Splits CombatBattleCanvas into overlay HUD and screen-space-camera intro canvases
    /// so HUD avoids depth-of-field while intro particles keep camera canvas sorting.
    /// Preserves existing component file IDs by moving components instead of recreating them.
    /// </summary>
    public static class CombatBattleCanvasSplitEditor
    {
        private const string PrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        private const string HudCanvasName = "CombatHudCanvas";
        private const string IntroCanvasName = "CombatIntroCanvas";
        private const string SafeAreaRootName = "SafeAreaRoot";
        private const string IntroRootName = "BattleIntroRoot";

        private const int HudSortingOrder = 400;
        private const int IntroSortingOrder = 500;
        private const float IntroPlaneDistance = 0.4f;

        [MenuItem("Tools/Chainbound/Combat/Split Combat Battle Canvas (Hud Overlay + Intro Camera)", false, 205)]
        public static void SplitCombatBattleCanvasMenuItem()
        {
            if (SplitCombatBattleCanvasPrefab(force: false))
                AssetDatabase.Refresh();
        }

        public static bool SplitCombatBattleCanvasPrefab(bool force = false)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[CombatBattleCanvasSplitEditor] Failed to load prefab: " + PrefabPath);
                return false;
            }

            try
            {
                if (!force && IsAlreadySplit(root.transform))
                {
                    Debug.Log("[CombatBattleCanvasSplitEditor] Prefab already split; skipping.");
                    return true;
                }

                ApplySplit(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CombatBattleCanvasSplitEditor] Split complete: " + PrefabPath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsAlreadySplit(Transform root)
        {
            return root.Find(HudCanvasName) != null && root.Find(IntroCanvasName) != null;
        }

        private static void ApplySplit(GameObject root)
        {
            RectTransform rootRt = root.GetComponent<RectTransform>();
            if (rootRt == null)
                throw new System.InvalidOperationException("CombatBattleCanvas root must have RectTransform.");

            if (IsAlreadySplit(root.transform) && root.GetComponent<Canvas>() == null)
                return;

            Canvas rootCanvas = root.GetComponent<Canvas>();
            if (rootCanvas == null)
                throw new System.InvalidOperationException("CombatBattleCanvas root must have Canvas before split.");

            CanvasScaler rootScaler = root.GetComponent<CanvasScaler>();
            GraphicRaycaster rootRaycaster = root.GetComponent<GraphicRaycaster>();

            RectTransform safeAreaRoot = FindChildRect(rootRt, SafeAreaRootName);
            if (safeAreaRoot == null)
                throw new System.InvalidOperationException("SafeAreaRoot not found under CombatBattleCanvas.");

            RectTransform introRoot = FindChildRect(safeAreaRoot, IntroRootName);
            if (introRoot == null)
                throw new System.InvalidOperationException("BattleIntroRoot not found under SafeAreaRoot.");

            MonoBehaviour safeAreaAdapter = safeAreaRoot.GetComponent<MonoBehaviour>();

            RectTransform hudRt = CreateCanvasChild(rootRt, HudCanvasName);
            RectTransform introRt = CreateCanvasChild(rootRt, IntroCanvasName);

            Canvas hudCanvas = hudRt.gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = HudSortingOrder;
            hudCanvas.additionalShaderChannels = rootCanvas.additionalShaderChannels;
            hudCanvas.vertexColorAlwaysGammaSpace = rootCanvas.vertexColorAlwaysGammaSpace;

            CanvasScaler hudScaler = hudRt.gameObject.AddComponent<CanvasScaler>();
            CopyCanvasScaler(rootScaler, hudScaler);

            if (rootRaycaster != null)
                MoveComponent(rootRaycaster, hudRt.gameObject);
            else
                hudRt.gameObject.AddComponent<GraphicRaycaster>();

            MoveBehaviour<CombatBattleUGUIController>(root, hudRt.gameObject);
            MoveBehaviour<CombatUiStageController>(root, hudRt.gameObject);
            MoveBehaviour<CombatUiSfxPlayer>(root, hudRt.gameObject);

            MoveComponent(rootCanvas, introRt.gameObject);
            rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            rootCanvas.planeDistance = IntroPlaneDistance;
            rootCanvas.sortingOrder = IntroSortingOrder;

            if (rootScaler != null)
                MoveComponent(rootScaler, introRt.gameObject);

            RectTransform introSafeAreaRoot = CreateIntroSafeAreaRoot(introRt, safeAreaAdapter);

            safeAreaRoot.SetParent(hudRt, false);
            introRoot.SetParent(introSafeAreaRoot, false);
            introRoot.SetAsLastSibling();

            rootRt.localScale = Vector3.one;
            StretchFull(rootRt);
            hudRt.SetSiblingIndex(0);
            introRt.SetSiblingIndex(1);
        }

        private static RectTransform CreateIntroSafeAreaRoot(RectTransform introCanvasRt, MonoBehaviour sourceSafeAreaAdapter)
        {
            var existing = FindChildRect(introCanvasRt, SafeAreaRootName);
            if (existing != null)
                return existing;

            var go = new GameObject(SafeAreaRootName, typeof(RectTransform));
            go.layer = introCanvasRt.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(introCanvasRt, false);
            StretchFull(rt);

            if (sourceSafeAreaAdapter != null)
            {
                System.Type adapterType = sourceSafeAreaAdapter.GetType();
                Component copy = go.AddComponent(adapterType);
                EditorUtility.CopySerialized(sourceSafeAreaAdapter, copy);
            }

            return rt;
        }

        private static RectTransform CreateCanvasChild(RectTransform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
                return existing;

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            StretchFull(rt);
            rt.localScale = Vector3.one;
            return rt;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
        }

        private static void CopyCanvasScaler(CanvasScaler source, CanvasScaler target)
        {
            if (source == null || target == null)
                return;

            target.uiScaleMode = source.uiScaleMode;
            target.referencePixelsPerUnit = source.referencePixelsPerUnit;
            target.scaleFactor = source.scaleFactor;
            target.referenceResolution = source.referenceResolution;
            target.screenMatchMode = source.screenMatchMode;
            target.matchWidthOrHeight = source.matchWidthOrHeight;
            target.physicalUnit = source.physicalUnit;
            target.fallbackScreenDPI = source.fallbackScreenDPI;
            target.defaultSpriteDPI = source.defaultSpriteDPI;
            target.dynamicPixelsPerUnit = source.dynamicPixelsPerUnit;
        }

        private static void MoveComponent(Component component, GameObject destination)
        {
            if (component == null || destination == null)
                return;

            if (component.gameObject == destination)
                return;

            if (!ComponentUtility.CopyComponent(component))
                throw new System.InvalidOperationException("Failed to copy " + component.GetType().Name + ".");

            if (!ComponentUtility.PasteComponentAsNew(destination))
                throw new System.InvalidOperationException("Failed to move " + component.GetType().Name + " to " + destination.name + ".");

            Object.DestroyImmediate(component, true);
        }

        private static void MoveBehaviour<T>(GameObject source, GameObject destination) where T : Component
        {
            T component = source.GetComponent<T>();
            if (component != null)
                MoveComponent(component, destination);
        }

        private static RectTransform FindChildRect(Transform parent, string name)
        {
            if (parent == null)
                return null;

            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                    return children[i] as RectTransform;
            }

            return null;
        }
    }
}
