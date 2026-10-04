/// <summary>
/// Stackable movement modifier sampled live by PlayerMovement3D.
/// Terrain volumes and PlayerMovementSpeedModifierStack both register here.
/// Their multipliers are multiplied together. The serialized moveSpeed baseline is never written.
/// </summary>
public interface IPlayerMovementModifier
{
    /// <summary>Multiplier on walk and sprint speed. 1 = unchanged, lower = bogged down.</summary>
    float MovementSpeedMultiplier { get; }

    /// <summary>Multiplier on jump impulse. 1 = unchanged, lower = weaker hop.</summary>
    float JumpImpulseMultiplier { get; }
}
