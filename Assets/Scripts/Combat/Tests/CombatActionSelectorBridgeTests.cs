using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatActionSelectorBridgeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void BackRewindClearsQueuedActionForRewoundToSlot()
        {
            var go = new GameObject("CombatActionSelectorBridge_Test");
            try
            {
                var bridge = go.AddComponent<CombatActionSelectorBridge>();
                SetPrivateField(bridge, "_awaitingSelection", true);
                SetPrivateField(bridge, "_pendingSlotCursor", 1);

                var pendingSlots = GetPrivateField<List<int>>(bridge, "_pendingSlots");
                pendingSlots.Add(0);
                pendingSlots.Add(1);

                var queued = GetPrivateField<Dictionary<int, CombatAction>>(bridge, "_queuedActionsBySlot");
                queued[0] = CombatAction.Swap(0, 2);

                var reservedBench = GetPrivateField<HashSet<int>>(bridge, "_reservedBenchIndices");
                reservedBench.Add(2);

                bool rewound = InvokePrivate<bool>(bridge, "RewindPendingSlotCursorForBack");

                Assert.IsTrue(rewound);
                Assert.AreEqual(0, GetPrivateField<int>(bridge, "_pendingSlotCursor"));
                Assert.IsFalse(queued.ContainsKey(0));
                Assert.IsFalse(reservedBench.Contains(2));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BackRewindAtFirstSlotDoesNotQueuePass()
        {
            var go = new GameObject("CombatActionSelectorBridge_Test");
            try
            {
                var bridge = go.AddComponent<CombatActionSelectorBridge>();
                SetPrivateField(bridge, "_awaitingSelection", true);
                SetPrivateField(bridge, "_pendingSlotCursor", 0);

                var pendingSlots = GetPrivateField<List<int>>(bridge, "_pendingSlots");
                pendingSlots.Add(0);
                pendingSlots.Add(1);

                var queued = GetPrivateField<Dictionary<int, CombatAction>>(bridge, "_queuedActionsBySlot");

                bool rewound = InvokePrivate<bool>(bridge, "RewindPendingSlotCursorForBack");

                Assert.IsFalse(rewound);
                Assert.AreEqual(0, GetPrivateField<int>(bridge, "_pendingSlotCursor"));
                Assert.IsFalse(queued.ContainsKey(0));
                Assert.IsFalse(queued.ContainsKey(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
            return (T)method.Invoke(target, null);
        }
    }
}
