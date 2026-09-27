using UnityEngine;

/// <summary>
/// Shared data asset for sporadic ambience one-shots and their playback cadence.
/// </summary>
[CreateAssetMenu(fileName = "SporadicAmbienceProfile", menuName = "Chainbound/Audio/Ambience/Sporadic Ambience Profile")]
public class SporadicAmbienceProfileSO : ScriptableObject
{
    [Header("One-Shot Pool")]
    [SerializeField] private SoundEventSO[] oneShotPool = new SoundEventSO[0];

    [Header("Playback Cadence")]
    [SerializeField] private float minCooldownSeconds = 8f;
    [SerializeField] private float maxCooldownSeconds = 20f;
    [SerializeField] private bool avoidImmediateRepeat = true;

    public SoundEventSO[] OneShotPool => oneShotPool;
    public float MinCooldownSeconds => minCooldownSeconds;
    public float MaxCooldownSeconds => maxCooldownSeconds;
    public bool AvoidImmediateRepeat => avoidImmediateRepeat;

    private void OnValidate()
    {
        minCooldownSeconds = Mathf.Max(0f, minCooldownSeconds);
        maxCooldownSeconds = Mathf.Max(minCooldownSeconds, maxCooldownSeconds);
    }
}
