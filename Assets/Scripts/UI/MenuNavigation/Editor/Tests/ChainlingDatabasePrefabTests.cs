using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor.Tests
{
    public class ChainlingDatabasePrefabTests
    {
        const string PlayerMenuRootPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        const string SandboxRootPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab";

        [Test]
        public void PlayerMenuRoots_AuthorTheDatabaseInProfileBSlots()
        {
            ChainlingDatabaseMenuAuthoring.UpgradeAll();
            AssertAuthored(PlayerMenuRootPath);
            AssertAuthored(SandboxRootPath);
        }

        static void AssertAuthored(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ChainlingDatabaseScreen database = null;
                bool handbookRemains = false;
                MenuScreen[] screens = root.GetComponentsInChildren<MenuScreen>(true);
                for (int i = 0; i < screens.Length; i++)
                {
                    if (screens[i] is ChainlingDatabaseScreen candidate && screens[i].ScreenId == "database")
                        database = candidate;
                    if (screens[i].ScreenId == "handbook")
                        handbookRemains = true;
                }

                Assert.IsNotNull(database, path);
                AssertHandbookStaysSeparate(root, path, handbookRemains);
                Assert.IsNull(database.GetComponent<PlaceholderMenuScreen>());
                Assert.AreEqual("database", database.ScreenId);
                Assert.IsNotNull(database.EncounterCatalog);
                Assert.Greater(database.EncounterCatalog.zones.Count, 0);

                ChainlingsPartyScreen party = root.GetComponentInChildren<ChainlingsPartyScreen>(true);
                if (party != null)
                    Assert.AreSame(database.EncounterCatalog, party.EncounterCatalog);

                AssertAnchors(database.transform.Find("TopLeft"), ChainlingDatabaseLayout.TopLeftMin, ChainlingDatabaseLayout.TopLeftMax);
                AssertAnchors(database.transform.Find("TopCenter"), ChainlingDatabaseLayout.TopCenterMin, ChainlingDatabaseLayout.TopCenterMax);
                AssertAnchors(database.transform.Find("TopRight"), ChainlingDatabaseLayout.TopRightMin, ChainlingDatabaseLayout.TopRightMax);
                AssertAnchors(database.transform.Find("DialogueLowerCenter"), ChainlingDatabaseLayout.DialogueMin, ChainlingDatabaseLayout.DialogueMax);
                AssertAnchors(database.transform.Find("DatabaseSheet"), ChainlingDatabaseLayout.SheetMin, ChainlingDatabaseLayout.SheetMax);
                Assert.IsFalse(database.transform.Find("DatabaseSheet").gameObject.activeSelf);

                Image mark = database.transform.Find("TopRight/UnknownMark").GetComponent<Image>();
                Assert.IsNull(mark.sprite);
                Assert.IsFalse(mark.raycastTarget);

                RawImage scanlines = database.transform.Find("DatabaseBackground/ScanlineOverlay").GetComponent<RawImage>();
                Assert.IsFalse(scanlines.raycastTarget);

                Button zoneRow = database.transform.Find(ChainlingDatabaseLayout.ZoneContentPath + "/ZoneRow_00").GetComponent<Button>();
                Assert.AreEqual(Navigation.Mode.Explicit, zoneRow.navigation.mode);
                Assert.GreaterOrEqual(zoneRow.GetComponent<LayoutElement>().minHeight, 60f);
                Assert.IsTrue(HasMotion(zoneRow));

                Selectable[] selectables = database.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < selectables.Length; i++)
                {
                    Transform transform = selectables[i].transform;
                    bool inSafeSlot = transform.IsChildOf(database.transform.Find("TopLeft"))
                        || transform.IsChildOf(database.transform.Find("TopCenter"))
                        || transform.IsChildOf(database.transform.Find("DatabaseSheet"));
                    Assert.IsTrue(inSafeSlot, selectables[i].name);
                }

                TMP_Text[] labels = database.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    Assert.IsFalse(labels[i].text.Contains("Seen/caught discovery"), labels[i].name);
                    Assert.IsFalse(labels[i].text.Contains("Ready for prefab-specific content"), labels[i].name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AssertHandbookStaysSeparate(GameObject root, string path, bool handbookRemains)
        {
            if (handbookRemains)
            {
                Assert.IsTrue(handbookRemains, path);
                return;
            }

            PlayerMenuController controller = root.GetComponentInChildren<PlayerMenuController>(true);
            Assert.IsNotNull(controller, path);
            MenuTabDefinitionSO[] definitions = controller.GetTabDefinitions();
            Assert.IsNotNull(definitions, path);
            int handbook = -1;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && definitions[i].TabId == "handbook")
                    handbook = i;
            }

            Assert.GreaterOrEqual(handbook, 0, path);
            Assert.IsNull(controller.GetTabScreen(handbook), path);
        }

        static bool HasMotion(Button button)
        {
            MonoBehaviour[] behaviours = button.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == "UiSelectableMotion")
                    return true;
            }

            return false;
        }

        static void AssertAnchors(Transform transform, Vector2 min, Vector2 max)
        {
            Assert.IsNotNull(transform);
            RectTransform rect = transform.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin.x, Is.EqualTo(min.x).Within(0.001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(min.y).Within(0.001f));
            Assert.That(rect.anchorMax.x, Is.EqualTo(max.x).Within(0.001f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(max.y).Within(0.001f));
        }
    }
}
