using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// One <see cref="IPlayerMovementModifier"/> for every timed and lasting speed source
/// on the player. Consumables, wearables, and party passives register here.
/// <see cref="PlayerMovement3D"/> multiplies the result into walk and sprint speed
/// and is the only component that applies planar speed.
///
/// Fractions inside this stack add: multiplier = max(0, 1 + sum(bonusFraction)).
/// The same source id refreshes. Other <see cref="IPlayerMovementModifier"/> instances
/// (terrain volumes) multiply outside this stack.
///
/// Timed sources are removed when they expire, when <see cref="CancelExpiringSources"/>
/// runs, and when this component is disabled, so an interrupted timer cannot stick.
/// Permanent sources (duration &lt;= 0) stay across that cancel and are re-applied
/// on enable. Their owners remove them with <see cref="RemoveSource"/>.
///
/// Future wearable:
/// stack.SetSource("wearable.example_boots", PlayerMovementSpeedModifierKind.Wearable, 0.10f, 0f);
/// Future party passive:
/// stack.SetSource("party.passive.example", PlayerMovementSpeedModifierKind.PartyPassive, 0.05f, 0f);
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerMovement3D))]
public sealed class PlayerMovementSpeedModifierStack : MonoBehaviour, IPlayerMovementModifier, IPlayerMovementSpeedModifierHost
{
    private readonly PlayerMovementSpeedModifierPipeline pipeline = new PlayerMovementSpeedModifierPipeline();
    private PlayerMovement3D movement;
    private float? timeOverride;

    public float MovementSpeedMultiplier
    {
        get
        {
            pipeline.Expire(SampleTime());
            return pipeline.SpeedMultiplier;
        }
    }

    public float JumpImpulseMultiplier => 1f;

    public float SpeedMultiplier => MovementSpeedMultiplier;

    public int SourceCount => pipeline.SourceCount;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
    }

    private void OnEnable()
    {
        PlayerMovementSpeedModifierRuntime.Register(this);
        RegisterWithMovement();
    }

    private void OnDisable()
    {
        pipeline.RemoveExpiringSources();
        timeOverride = null;
        UnregisterFromMovement();
        PlayerMovementSpeedModifierRuntime.Unregister(this);
    }

    private void Update()
    {
        if (movement != null && !movement.isActiveAndEnabled)
        {
            pipeline.RemoveExpiringSources();
            return;
        }

        pipeline.Expire(SampleTime());
    }

    /// <summary>
    /// Registers the Stride Tonic source (+15% for <see cref="PlayerMovementSpeedConsumable.DurationSeconds"/>).
    /// A second use refreshes that same source instead of stacking another +15%.
    /// </summary>
    public bool ApplyConsumable(float? now = null)
    {
        if (now.HasValue)
            timeOverride = now.Value;

        return SetSource(
            PlayerMovementSpeedConsumable.SourceId,
            PlayerMovementSpeedModifierKind.Consumable,
            PlayerMovementSpeedConsumable.BonusFraction,
            PlayerMovementSpeedConsumable.DurationSeconds,
            now ?? SampleTime());
    }

    public void SetSource(
        string sourceId,
        PlayerMovementSpeedModifierKind kind,
        float bonusFraction,
        float durationSeconds)
    {
        SetSource(sourceId, kind, bonusFraction, durationSeconds, SampleTime());
    }

    public bool SetSource(
        string sourceId,
        PlayerMovementSpeedModifierKind kind,
        float bonusFraction,
        float durationSeconds,
        float now)
    {
        if (!isActiveAndEnabled)
            return false;

        RegisterWithMovement();
        return pipeline.SetSource(sourceId, kind, bonusFraction, durationSeconds, now);
    }

    public bool RemoveSource(string sourceId)
    {
        return pipeline.RemoveSource(sourceId);
    }

    public void Clear()
    {
        pipeline.Clear();
    }

    /// <summary>
    /// Drops timed sources immediately. Movement disable and object disable call this
    /// so a cancelled or interrupted tonic cannot leave speed raised.
    /// </summary>
    public void CancelExpiringSources()
    {
        pipeline.RemoveExpiringSources();
    }

    /// <summary>Advances expiry without waiting on <see cref="Time.time"/>. Used by tests and debug.</summary>
    public void Tick(float now)
    {
        timeOverride = now;
        pipeline.Expire(now);
    }

    public bool TryGetSource(string sourceId, out PlayerMovementSpeedModifierState state)
    {
        return pipeline.TryGetSource(sourceId, out state);
    }

    private float SampleTime()
    {
        return timeOverride ?? Time.time;
    }

    private void RegisterWithMovement()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement3D>();
        if (movement == null)
            return;
        movement.RegisterMovementModifier(this);
    }

    private void UnregisterFromMovement()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement3D>();
        if (movement == null)
            return;
        movement.UnregisterMovementModifier(this);
    }
}
