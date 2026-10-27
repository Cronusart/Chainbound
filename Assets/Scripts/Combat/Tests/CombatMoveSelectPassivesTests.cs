using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatMoveSelectPassivesTests
    {
        private static readonly Color DarkLabel = new Color32(0x2E, 0x22, 0x2F, 0xFF);
        private static readonly Color PassiveSelected = new Color32(0xC7, 0xDC, 0xD0, 0xFF);
        private static readonly Color PassiveNormal = new Color32(0x9B, 0xAB, 0xB2, 0xFF);

        [Test]
        public void PassivesFirst_KeepsEquippedOrderInsideEachGroup()
        {
            var source = new[]
            {
                new Entry("A", false, 0),
                new Entry("P1", true, 1),
                new Entry("B", false, 2),
                new Entry("P2", true, 3),
            };

            List<Entry> ordered = MoveListOrdering.PassivesFirst(source, entry => entry.passive, out int passiveCount);

            Assert.That(passiveCount, Is.EqualTo(2));
            Assert.That(Ids(ordered), Is.EqualTo(new[] { "P1", "P2", "A", "B" }));
        }

        [Test]
        public void PassivesFirst_WithNoPassives_PreservesOrder()
        {
            var source = new[]
            {
                new Entry("A", false, 0),
                new Entry("B", false, 1),
            };

            List<Entry> ordered = MoveListOrdering.PassivesFirst(source, entry => entry.passive, out int passiveCount);

            Assert.That(passiveCount, Is.Zero);
            Assert.That(Ids(ordered), Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void PassivesFirst_PreservesOriginalMoveIndexes()
        {
            var source = new[]
            {
                new Entry("A", false, 7),
                new Entry("P", true, 3),
            };

            List<Entry> ordered = MoveListOrdering.PassivesFirst(source, entry => entry.passive);

            Assert.That(ordered[0].moveIndex, Is.EqualTo(3));
            Assert.That(ordered[1].moveIndex, Is.EqualTo(7));
        }

        [Test]
        public void Nav_DownFromRest_GoesToFirstActive()
        {
            Assert.That(MoveListNav.Step(cursor: 4, step: 1, passiveCount: 2, moveCount: 4), Is.EqualTo(2));
        }

        [Test]
        public void Nav_UpFromFirstActive_GoesToLastPassive()
        {
            Assert.That(MoveListNav.Step(cursor: 2, step: -1, passiveCount: 2, moveCount: 4), Is.EqualTo(1));
        }

        [Test]
        public void Nav_UpFromTopPassive_GoesToRest()
        {
            Assert.That(MoveListNav.Step(cursor: 0, step: -1, passiveCount: 2, moveCount: 4), Is.EqualTo(4));
        }

        [Test]
        public void Nav_WithNoPassives_MatchesPlainWrap()
        {
            const int moveCount = 4;
            int rowCount = moveCount + 1;
            for (int cursor = 0; cursor < rowCount; cursor++)
            {
                Assert.That(MoveListNav.Step(cursor, -1, 0, moveCount), Is.EqualTo(Wrap(cursor - 1, rowCount)));
                Assert.That(MoveListNav.Step(cursor, 1, 0, moveCount), Is.EqualTo(Wrap(cursor + 1, rowCount)));
            }
        }

        [Test]
        public void Nav_WithOnlyPassives_KeepsEveryRowReachableAndInRange()
        {
            const int moveCount = 3;
            var visited = new HashSet<int>();
            int cursor = moveCount;
            for (int i = 0; i < moveCount + 1; i++)
            {
                cursor = MoveListNav.Step(cursor, 1, moveCount, moveCount);
                Assert.That(cursor, Is.InRange(0, moveCount));
                visited.Add(cursor);
            }

            Assert.That(visited.Count, Is.EqualTo(moveCount + 1));
        }

        [Test]
        public void DefaultCursor_SkipsPassivesAndFallsBackToRest()
        {
            Assert.That(MoveListNav.DefaultCursor(new[] { false, false, true, false }, value => value), Is.EqualTo(2));
            Assert.That(MoveListNav.DefaultCursor(new[] { false, false, false }, value => value), Is.EqualTo(3));
        }

        [Test]
        public void RowLabelColors_RespectPassiveSelectedAndUsabilityPrecedence()
        {
            RowFixture fixture = CreateRowFixture();
            try
            {
                fixture.row.SetData("Passive", null, false, false, isPassive: true);
                AssertColor(fixture.label.color, DarkLabel);

                fixture.row.SetData("Selected", null, true, true);
                AssertColor(fixture.label.color, DarkLabel);

                fixture.row.SetData("Active", null, true, false);
                AssertColor(fixture.label.color, Color.white);

                fixture.row.SetData("Blocked", null, false, false);
                AssertColor(fixture.label.color, new Color(0.74f, 0.74f, 0.74f, 0.85f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public void PassiveRow_TintsFrameAndBackdrop_WithoutOverwritingAuthoredBackdrop()
        {
            RowFixture fixture = CreateRowFixture();
            Color authoredBackdrop = new Color(0.12f, 0.23f, 0.34f, 0.77f);
            fixture.backdrop.color = authoredBackdrop;
            try
            {
                fixture.row.SetData("Passive", null, false, false, isPassive: true);
                AssertColor(fixture.frame.color, PassiveNormal);
                AssertColor(fixture.backdrop.color, PassiveNormal);

                fixture.row.SetData("Passive", null, false, true, isPassive: true);
                AssertColor(fixture.frame.color, PassiveSelected);
                AssertColor(fixture.backdrop.color, PassiveSelected);

                fixture.row.SetData("Active", null, true, false);
                AssertColor(fixture.backdrop.color, authoredBackdrop);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public void RowPill_ShowsPassivePriorityOrNothing()
        {
            RowFixture fixture = CreateRowFixture();
            try
            {
                fixture.row.SetData("Passive", null, false, false, isPassive: true);
                RectTransform pill = fixture.root.transform.Find("PriorityPill") as RectTransform;
                Assert.That(pill, Is.Not.Null);
                Assert.That(pill.gameObject.activeSelf, Is.True);
                Assert.That(pill.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(MoveSelectDisplayUtility.PassivePillLabel));

                fixture.row.SetData("Priority", null, true, true, hasPriority: true);
                Assert.That(pill.gameObject.activeSelf, Is.True);
                TMP_Text pillText = pill.GetComponentInChildren<TMP_Text>(true);
                Assert.That(pillText.text, Is.EqualTo(MoveSelectDisplayUtility.PriorityPillLabel));
                AssertColor(pillText.color, DarkLabel);

                fixture.row.SetData("Plain", null, true, false);
                Assert.That(pill.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public void PassiveDetails_KeepReadableContentFullStrengthAndMuteApRoleAndStats()
        {
            var panelObject = new GameObject("MoveSelectPanel", typeof(RectTransform), typeof(CombatMoveSelectPanelUGUI));
            CombatMoveSelectPanelUGUI panel = panelObject.GetComponent<CombatMoveSelectPanelUGUI>();
            try
            {
                panel.SetTitle("Move Select");
                TMP_Text name = GetField<TMP_Text>(panel, "selectedNameText");
                TMP_Text role = GetField<TMP_Text>(panel, "selectedRoleText");
                TMP_Text ap = GetField<TMP_Text>(panel, "selectedApText");
                TMP_Text power = GetField<TMP_Text>(panel, "selectedPowerText");
                TMP_Text modifier = GetField<TMP_Text>(panel, "selectedModifierText");
                TMP_Text description = GetField<TMP_Text>(panel, "selectedDescriptionText");
                Image icon = GetField<Image>(panel, "selectedTypeIconImage");

                Color authoredName = new Color(0.91f, 0.22f, 0.13f, 1f);
                Color authoredRole = new Color(0.14f, 0.82f, 0.27f, 1f);
                Color authoredAp = new Color(0.18f, 0.31f, 0.94f, 1f);
                Color authoredModifier = new Color(0.77f, 0.18f, 0.78f, 1f);
                Color authoredDescription = new Color(0.18f, 0.77f, 0.82f, 1f);
                name.color = authoredName;
                role.color = authoredRole;
                ap.color = authoredAp;
                modifier.color = authoredModifier;
                description.color = authoredDescription;
                icon.color = Color.white;
                SetField(panel, "_authoringColorsCached", false);

                panel.SetSelectedDetails(new CombatMoveSelectPanelUGUI.MoveDetailData
                {
                    moveName = "Passive Probe",
                    apText = "Passive ACTIVE",
                    powerText = "Effect",
                    accuracyText = "Accuracy --",
                    targetingText = "Target Self",
                    typeRoleLabel = "Node | Passive",
                    modifierSummary = "Mods: None",
                    shortDescription = "Readable passive description.",
                    isUsable = false,
                    isPassive = true,
                });

                Color muted = new Color(0.74f, 0.74f, 0.74f, 0.85f);
                AssertColor(name.color, authoredName);
                AssertColor(description.color, authoredDescription);
                AssertColor(modifier.color, authoredModifier);
                AssertColor(role.color, muted);
                AssertColor(ap.color, muted);
                AssertColor(power.color, muted);
                Assert.That(icon.color.a, Is.EqualTo(1f).Within(0.002f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void LibraryCardPassive_MutesApAndRoleAndRestoresAuthoredColorsForActiveMoves()
        {
            var root = new GameObject("LibraryCard", typeof(RectTransform));
            TMP_Text ap = Child<TextMeshProUGUI>(root.transform, "APCost");
            TMP_Text role = Child<TextMeshProUGUI>(root.transform, "MoveRole");
            Color authoredAp = new Color(0.22f, 0.48f, 0.92f, 1f);
            Color authoredRole = new Color(0.18f, 0.86f, 0.42f, 1f);
            ap.color = authoredAp;
            role.color = authoredRole;
            CombatMoveLibraryCardView view = root.AddComponent<CombatMoveLibraryCardView>();
            try
            {
                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    apCost = 0,
                    currentAp = 4,
                    roleLabel = "Passive",
                    isPassive = true,
                });

                Color muted = new Color(0.74f, 0.74f, 0.74f, 0.85f);
                AssertColor(ap.color, muted);
                AssertColor(role.color, muted);

                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    apCost = 1,
                    currentAp = 4,
                    roleLabel = "Damage",
                    isPassive = false,
                });

                AssertColor(ap.color, authoredAp);
                AssertColor(role.color, authoredRole);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConfirmOnPassive_PlaysInvalidOnceAndStaysInMoveSelect()
        {
            var coordinatorObject = new GameObject("Coordinator", typeof(CombatSelectionCoordinatorUGUI));
            var panelObject = new GameObject("MoveSelectPanel", typeof(RectTransform), typeof(CombatMoveSelectPanelUGUI));
            var audioObject = new GameObject("FakeCombatUiSfx", typeof(FakePassiveMoveUiSfxPlayer));
            CombatSelectionCoordinatorUGUI coordinator = coordinatorObject.GetComponent<CombatSelectionCoordinatorUGUI>();
            CombatMoveSelectPanelUGUI panel = panelObject.GetComponent<CombatMoveSelectPanelUGUI>();
            FakePassiveMoveUiSfxPlayer audio = audioObject.GetComponent<FakePassiveMoveUiSfxPlayer>();
            int commitCount = 0;

            try
            {
                SetField(panel, "uiSfxBehaviour", audio);
                SetField(panel, "unavailablePanelNudgeDuration", 0f);
                SetField(coordinator, "moveSelectPanel", panel);
                SetField(coordinator, "_moveCursor", 0);
                SetField(coordinator, "_commitAction", new Action<CombatAction>(_ => commitCount++));

                FieldInfo modeField = Field(typeof(CombatSelectionCoordinatorUGUI), "_mode");
                object moveSelectMode = Enum.Parse(modeField.FieldType, "MoveSelect");
                modeField.SetValue(coordinator, moveSelectMode);

                Type entryType = typeof(CombatSelectionCoordinatorUGUI).GetNestedType("MoveEntry", BindingFlags.NonPublic);
                Assert.That(entryType, Is.Not.Null);
                object entry = Activator.CreateInstance(entryType);
                SetField(entry, entryType, "move", CombatFactory.CreateTestMove(
                    "Passive Probe", MoveCategory.Passive, ChainlingType.None, power: 0, apCost: 0, accuracy: 100));
                SetField(entry, entryType, "moveIndex", 0);
                SetField(entry, entryType, "usable", true);
                SetField(entry, entryType, "blockReason", "AllTargetsBlocked");

                var moves = (IList)Field(typeof(CombatSelectionCoordinatorUGUI), "_moves").GetValue(coordinator);
                moves.Add(entry);

                coordinator.HandleConfirm();

                Assert.That(audio.invalidCount, Is.EqualTo(1));
                Assert.That(audio.totalCount, Is.EqualTo(1));
                Assert.That(commitCount, Is.Zero);
                Assert.That(modeField.GetValue(coordinator).ToString(), Is.EqualTo("MoveSelect"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
                UnityEngine.Object.DestroyImmediate(panelObject);
                UnityEngine.Object.DestroyImmediate(audioObject);
            }
        }

        private static RowFixture CreateRowFixture()
        {
            var root = new GameObject(
                "MoveRow",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(CombatMoveRowItemUGUI));

            Image frame = Child<Image>(root.transform, "MoveRowBG");
            Image backdrop = Child<Image>(root.transform, "MoveRowSelectionBackdrop");
            TMP_Text label = Child<TextMeshProUGUI>(root.transform, "Label");
            return new RowFixture(root, root.GetComponent<CombatMoveRowItemUGUI>(), frame, backdrop, label);
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            child.transform.SetParent(parent, false);
            return child.GetComponent<T>();
        }

        private static string[] Ids(IReadOnlyList<Entry> entries)
        {
            var ids = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                ids[i] = entries[i].id;
            return ids;
        }

        private static int Wrap(int value, int count)
        {
            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.002f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.002f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.002f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.002f));
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {type.FullName}.{name}");
            return field;
        }

        private static void SetField(object target, string name, object value)
        {
            Field(target.GetType(), name).SetValue(target, value);
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            T value = Field(target.GetType(), name).GetValue(target) as T;
            Assert.That(value, Is.Not.Null, $"Expected {target.GetType().FullName}.{name} to hold {typeof(T).Name}");
            return value;
        }

        private static void SetField(object target, Type type, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {type.FullName}.{name}");
            field.SetValue(target, value);
        }

        private readonly struct Entry
        {
            public readonly string id;
            public readonly bool passive;
            public readonly int moveIndex;

            public Entry(string id, bool passive, int moveIndex)
            {
                this.id = id;
                this.passive = passive;
                this.moveIndex = moveIndex;
            }
        }

        private readonly struct RowFixture
        {
            public readonly GameObject root;
            public readonly CombatMoveRowItemUGUI row;
            public readonly Image frame;
            public readonly Image backdrop;
            public readonly TMP_Text label;

            public RowFixture(
                GameObject root,
                CombatMoveRowItemUGUI row,
                Image frame,
                Image backdrop,
                TMP_Text label)
            {
                this.root = root;
                this.row = row;
                this.frame = frame;
                this.backdrop = backdrop;
                this.label = label;
            }
        }
    }

    public sealed class FakePassiveMoveUiSfxPlayer : MonoBehaviour, ICombatUiSfxPlayer
    {
        public int invalidCount;
        public int totalCount;

        public void PlayCue(CombatUiSfxCue cue)
        {
            totalCount++;
            if (cue == CombatUiSfxCue.Invalid)
                invalidCount++;
        }
    }
}
