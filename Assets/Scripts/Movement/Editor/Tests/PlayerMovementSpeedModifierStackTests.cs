using System.Reflection;
using System.Text.RegularExpressions;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerMovementSpeedModifierStackTests
{
    [SetUp]
    public void ResetHosts()
    {
        PlayerMovementSpeedModifierRuntime.ClearHosts();
    }

    [TearDown]
    public void ClearHosts()
    {
        PlayerMovementSpeedModifierRuntime.ClearHosts();
    }

    [Test]
    public void Consumable_ScalesPlayerMovementAndExpiryRestoresPreBoostSpeed()
    {
        var go = new GameObject("PlayerMovementSpeedStack");
        try
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[PlayerMovement3D\] InputActionAsset not found"));
            PlayerMovement3D movement = go.AddComponent<PlayerMovement3D>();
            PlayerMovementSpeedModifierStack stack = movement.GetComponent<PlayerMovementSpeedModifierStack>();
            Assert.IsNotNull(stack);
            SetMoveSpeed(movement, 5f);

            ConstantMovementModifier terrain = go.AddComponent<ConstantMovementModifier>();
            terrain.speedMultiplier = 0.5f;
            movement.RegisterMovementModifier(terrain);

            float baseline = movement.GetCurrentSpeed();
            Assert.AreEqual(5f * 0.5f, baseline, 0.000001f);

            Assert.IsTrue(stack.SetSource(
                "wearable.example_boots",
                PlayerMovementSpeedModifierKind.Wearable,
                0.10f,
                0f,
                0f));
            float withWearable = movement.GetCurrentSpeed();
            Assert.AreEqual(baseline * (1f + 0.10f), withWearable, 0.000001f);

            Assert.IsTrue(stack.ApplyConsumable(0f));
            float boosted = movement.GetCurrentSpeed();
            Assert.AreEqual(baseline * (1f + 0.10f + PlayerMovementSpeedConsumable.BonusFraction), boosted, 0.000001f);
            Assert.AreEqual(5f, ReadMoveSpeed(movement), 0f);

            stack.Tick(PlayerMovementSpeedConsumable.DurationSeconds);
            Assert.AreEqual(withWearable, movement.GetCurrentSpeed(), 0f);

            Assert.IsTrue(PlayerMovementSpeedModifierRuntime.TrySetSource(
                "party.passive.example",
                PlayerMovementSpeedModifierKind.PartyPassive,
                0.05f,
                0f,
                out string partyError), partyError);
            float withParty = movement.GetCurrentSpeed();
            Assert.AreEqual(baseline * (1f + 0.10f + 0.05f), withParty, 0.000001f);

            Assert.IsTrue(stack.ApplyConsumable(0f));
            float once = movement.GetCurrentSpeed();
            Assert.AreEqual(
                baseline * (1f + 0.10f + 0.05f + PlayerMovementSpeedConsumable.BonusFraction),
                once,
                0.000001f);
            const float refreshAt = 10f;
            Assert.IsTrue(stack.ApplyConsumable(refreshAt));
            Assert.AreEqual(once, movement.GetCurrentSpeed(), 0f);
            stack.Tick(refreshAt + PlayerMovementSpeedConsumable.DurationSeconds - 1f);
            Assert.AreEqual(once, movement.GetCurrentSpeed(), 0f);
            stack.Tick(refreshAt + PlayerMovementSpeedConsumable.DurationSeconds);
            Assert.AreEqual(withParty, movement.GetCurrentSpeed(), 0f);

            Assert.IsTrue(stack.ApplyConsumable(0f));
            Assert.Greater(movement.GetCurrentSpeed(), withParty);
            movement.enabled = false;
            Assert.AreEqual(withParty, movement.GetCurrentSpeed(), 0f);
            movement.enabled = true;
            Assert.AreEqual(withParty, movement.GetCurrentSpeed(), 0f);

            Assert.IsTrue(stack.ApplyConsumable(0f));
            go.SetActive(false);
            go.SetActive(true);
            movement = go.GetComponent<PlayerMovement3D>();
            stack = movement.GetComponent<PlayerMovementSpeedModifierStack>();
            Assert.AreEqual(withParty, movement.GetCurrentSpeed(), 0f);
            Assert.IsFalse(stack.TryGetSource(PlayerMovementSpeedConsumable.SourceId, out _));
            Assert.IsTrue(stack.TryGetSource("wearable.example_boots", out _));
            Assert.IsTrue(stack.TryGetSource("party.passive.example", out _));
            Assert.AreEqual(5f, ReadMoveSpeed(movement), 0f);
        }
        finally
        {
            PlayerMovementSpeedModifierRuntime.ClearHosts();
            Object.DestroyImmediate(go);
        }
    }

    private static void SetMoveSpeed(PlayerMovement3D movement, float speed)
    {
        MoveSpeedField().SetValue(movement, speed);
    }

    private static float ReadMoveSpeed(PlayerMovement3D movement)
    {
        return (float)MoveSpeedField().GetValue(movement);
    }

    private static FieldInfo MoveSpeedField()
    {
        return typeof(PlayerMovement3D).GetField("moveSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private sealed class ConstantMovementModifier : MonoBehaviour, IPlayerMovementModifier
    {
        public float speedMultiplier = 1f;
        public float MovementSpeedMultiplier => speedMultiplier;
        public float JumpImpulseMultiplier => 1f;
    }
}
