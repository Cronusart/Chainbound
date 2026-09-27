/// <summary>
/// Stackable movement modifier applied while the player occupies a volume.
/// Values are sampled live each physics tick so inspector tweaks apply immediately.
/// </summary>
public interface IPlayerMovementModifier
{
    /// <summary>Multiplier on walk and sprint speed. 1 = unchanged, lower = bogged down.</summary>
    float MovementSpeedMultiplier { get; }

    /// <summary>Multiplier on jump impulse. 1 = unchanged, lower = weaker hop.</summary>
    float JumpImpulseMultiplier { get; }
}
