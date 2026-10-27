using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat.Editor
{
    public static class CombatMoveDetailsLibraryCardAuthoring
    {
        const string PrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        const string RolledFacePath = "Assets/Sprites/UI/LibraryCards/librarycard_frontUI.png";
        const string LearnedFacePath = "Assets/Sprites/UI/LibraryCards/librarycard_learned_frontUI.png";
        const string StampFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";

        [MenuItem("Tools/Chainbound/Combat/Add Move Details Library Card Toggle")]
        public static void ApplyMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool editingOpenStage = stage != null &&
                string.Equals(stage.assetPath.Replace('\\', '/'), PrefabPath, System.StringComparison.OrdinalIgnoreCase);

            GameObject root = editingOpenStage
                ? stage.prefabContentsRoot
                : PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
                throw new System.InvalidOperationException($"Could not load {PrefabPath}");

            try
            {
                Transform details = FindMoveSelectDetails(root.transform);
                if (details == null)
                    throw new System.InvalidOperationException("Move select Details object was not found.");

                EnsureDetails1(details);
                EnsureDetails2(details, out CombatMoveLibraryCardView card);
                CombatMoveSelectPanelUGUI panel = details.GetComponentInParent<CombatMoveSelectPanelUGUI>(true);
                if (panel != null && card != null)
                {
                    SerializedObject serialized = new SerializedObject(panel);
                    serialized.FindProperty("libraryCardView").objectReferenceValue = card;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (editingOpenStage)
                {
                    EditorUtility.SetDirty(root);
                    PrefabUtility.SaveAsPrefabAsset(root, stage.assetPath);
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
            }
            finally
            {
                if (!editingOpenStage)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool EnsureDetails1(Transform details)
        {
            Transform existing = details.Find("Details1");
            if (existing != null)
                return false;

            GameObject group = new GameObject("Details1", typeof(RectTransform));
            RectTransform rect = group.GetComponent<RectTransform>();
            rect.SetParent(details, false);
            Stretch(rect);

            var children = new List<Transform>();
            for (int i = 0; i < details.childCount; i++)
            {
                Transform child = details.GetChild(i);
                if (child == rect || child.name == "Details2")
                    continue;
                children.Add(child);
            }

            for (int i = 0; i < children.Count; i++)
                children[i].SetParent(rect, false);

            rect.SetAsFirstSibling();
            return true;
        }

        static bool EnsureDetails2(Transform details, out CombatMoveLibraryCardView card)
        {
            Transform existing = details.Find("Details2");
            if (existing != null)
            {
                // Details2 is authored, including MoveStats and MoveDetails. Do not rebuild it.
                card = existing.GetComponent<CombatMoveLibraryCardView>();
                return false;
            }

            GameObject group = new GameObject("Details2", typeof(RectTransform));
            RectTransform rect = group.GetComponent<RectTransform>();
            rect.SetParent(details, false);
            Stretch(rect);
            rect.SetAsLastSibling();

            GameObject cardObject = new GameObject("LibraryCardRoot", typeof(RectTransform));
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.SetParent(rect, false);
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(LibraryCardLayout.CardWidth, LibraryCardLayout.CardHeight);

            Image face = CreateImage("CardFace", cardRect);
            Stretch(face.rectTransform);
            face.preserveAspect = true;
            face.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RolledFacePath);
            Image type = CreateImage("TypeWell", cardRect);
            Image moveName = CreateImage("MoveNameStamp", cardRect);
            Image stats = CreateImage("StatsStamp", cardRect);
            Image mod1 = CreateImage("Mod1Stamp", cardRect);
            Image mod2 = CreateImage("Mod2Stamp", cardRect);
            Image moveDot = CreateImage("MoveDot", cardRect);
            Image mod1Dot = CreateImage("Mod1Dot", cardRect);
            Image mod2Dot = CreateImage("Mod2Dot", cardRect);

            card = group.AddComponent<CombatMoveLibraryCardView>();
            card.Bind(
                cardRect,
                face,
                AssetDatabase.LoadAssetAtPath<Sprite>(RolledFacePath),
                AssetDatabase.LoadAssetAtPath<Sprite>(LearnedFacePath),
                type,
                moveName,
                stats,
                mod1,
                mod2,
                moveDot,
                mod1Dot,
                mod2Dot,
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StampFontPath));
            card.ApplyLayout();
            cardRect.localScale = new Vector3(0.58f, 0.58f, 0.58f);
            group.SetActive(true);
            return true;
        }

        static Image CreateImage(string name, Transform parent)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.maskable = true;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }

        static Transform FindMoveSelectDetails(Transform root)
        {
            CombatMoveSelectPanelUGUI[] panels = root.GetComponentsInChildren<CombatMoveSelectPanelUGUI>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                Transform details = FindNamedChild(panels[i].transform, "Details");
                if (details != null)
                    return details;
            }

            return FindNamedChild(root, "Details");
        }

        static Transform FindNamedChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamedChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
