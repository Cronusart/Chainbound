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

        [Test]
        public void BlockTarget_BackNavigation_ClearsInvalidQueuedBlock()
        {
            var go = new GameObject("CombatActionSelectorBridge_BlockConflictTest");
            try
            {
                var controller = go.AddComponent<CombatBattleController>();
                var bridge = go.AddComponent<CombatActionSelectorBridge>();
                SetPrivateField(bridge, "battleController", controller);

                ChainlingInstance first = Chainling("first");
                ChainlingInstance second = Chainling("second");
                first.moves.Add(BlockMove("ember_wall"));
                second.moves.Add(BlockMove("blockade"));
                var team = new TeamState(0, "Allies");
                team.chainlings.Add(first);
                team.chainlings.Add(second);
                team.activeIndices.Add(0);
                team.activeIndices.Add(1);
                var state = new CombatState { team0 = team, team1 = new TeamState(1, "Foes") };
                SetPrivateField(bridge, "_pendingState", state);

                var queued = GetPrivateField<Dictionary<int, CombatAction>>(bridge, "_queuedActionsBySlot");
                queued[1] = CombatAction.UseMove(0, 0, 0, 0);
                CombatAction preferred = CombatAction.UseMove(0, 0, 0, 0);
                queued[0] = preferred;

                InvokePrivate(bridge, "ReconcileQueuedBlockTargets", 0, preferred);

                Assert.IsTrue(queued.ContainsKey(0));
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

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
            method.Invoke(target, arguments);
        }

        private static ChainlingInstance Chainling(string id)
        {
            var value = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                isAlive = true,
                currentHP = 100,
                baseMaxHP = 100,
                currentAP = 5,
            };
            value.SyncDisplayVitals();
            return value;
        }

        private static MoveInstance BlockMove(string id)
        {
            var move = new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = MoveCategory.Support,
                targeting = MoveTargeting.SingleAlly,
                apCost = 2,
            };
            move.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.BlockWindow,
                magnitude = 0.5f,
                turnBudget = 2,
                hitBudget = 1,
                stackKey = "wall_" + id,
                contactFilter = UtilityContactFilter.Contact,
                chance = 1f,
            });
            return move;
        }
    }
}
