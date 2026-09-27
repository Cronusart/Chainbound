using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a player-local wake to child water renderers through MaterialPropertyBlocks.
/// The component never writes global shader state, so non-interactive water stays isolated.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class WaterStreamInteractor : MonoBehaviour
{
    private const string WakeKeyword = "_ENABLE_PLAYER_WAKE";

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private GameObject playerObject;

    [Header("Wake Renderers")]
    [Tooltip("When assigned, only child renderers using this exact shared material are affected.")]
    [SerializeField] private Material wakeMaterial;
    [SerializeField] private bool collectRenderersOnAwake = true;

    [Header("Wake Shape")]
    [Min(0.05f)]
    [SerializeField] private float interactRadius = 0.9f;
    [Range(0f, 0.1f)]
    [SerializeField] private float bowlStrength = 0.045f;
    [Range(0f, 0.1f)]
    [SerializeField] private float ringStrength = 0.032f;
    [Min(0.01f)]
    [SerializeField] private float ringWidth = 0.22f;
    [Range(0f, 0.1f)]
    [SerializeField] private float maxDown = 0.055f;
    [Min(1f)]
    [SerializeField] private float wakeStretch = 1.8f;
    [Range(0f, 1f)]
    [SerializeField] private float foamStrength = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float downstreamBias = 0.22f;

    [Header("Timing")]
    [Range(1f, 60f)]
    [SerializeField] private float updateRateHz = 20f;
    [Min(0.01f)]
    [SerializeField] private float decayTime = 0.4f;

    [Header("Foot Foam")]
    [SerializeField] private ParticleSystem footFoamPrefab;
    [Min(0f)]
    [SerializeField] private float foamSurfaceOffset = 0.045f;
    [Min(0f)]
    [SerializeField] private float foamMoveThreshold = 0.1f;
    [Min(0f)]
    [SerializeField] private float foamMaxSpeed = 4f;
    [Min(0f)]
    [SerializeField] private float foamMaxEmissionRate = 7f;
    [Range(0, 8)]
    [SerializeField] private int enterFoamCount = 2;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;
    [SerializeField] private bool drawDebugGizmos;

    private readonly List<Renderer> wakeRenderers = new List<Renderer>();
    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private MaterialPropertyBlock propertyBlock;
    private Transform playerTransform;
    private ParticleSystem footFoamInstance;
    private Vector3 lastPlayerPosition;
    private Vector3 currentPosition;
    private Vector3 currentVelocity;
    private float currentStrengthScale;
    private float lastSampleTime;
    private float nextUpdateTime;
    private float decayStartedAt;
    private Vector3 decayVelocity;
    private float decayStrength;
    private bool isDecaying;

    private static readonly int InteractorPosWSId = Shader.PropertyToID("_InteractorPosWS");
    private static readonly int InteractorVelWSId = Shader.PropertyToID("_InteractorVelWS");
    private static readonly int InteractRadiusId = Shader.PropertyToID("_InteractRadius");
    private static readonly int BowlStrengthId = Shader.PropertyToID("_WakeBowlStrength");
    private static readonly int RingStrengthId = Shader.PropertyToID("_WakeRingStrength");
    private static readonly int RingWidthId = Shader.PropertyToID("_WakeRingWidth");
    private static readonly int MaxDownId = Shader.PropertyToID("_WakeMaxDown");
    private static readonly int StretchId = Shader.PropertyToID("_WakeStretch");
    private static readonly int FoamStrengthId = Shader.PropertyToID("_WakeFoamStrength");
    private static readonly int DownstreamBiasId = Shader.PropertyToID("_WakeDownstreamBias");

    public int ManagedRendererCount => wakeRenderers.Count;
    public bool IsPlayerInside => playerColliders.Count > 0;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        GetComponent<BoxCollider>().isTrigger = true;

        if (collectRenderersOnAwake)
            CollectWakeRenderers();

        ResolveConfiguredPlayer();
        EnsureFootFoam();
        ResetWake();
    }

    private void OnValidate()
    {
        interactRadius = Mathf.Max(0.05f, interactRadius);
        ringWidth = Mathf.Max(0.01f, ringWidth);
        wakeStretch = Mathf.Max(1f, wakeStretch);
        updateRateHz = Mathf.Clamp(updateRateHz, 1f, 60f);
        decayTime = Mathf.Max(0.01f, decayTime);
        foamMaxSpeed = Mathf.Max(foamMoveThreshold + 0.01f, foamMaxSpeed);

        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Update()
    {
        if (IsPlayerInside && playerTransform != null)
        {
            if (Time.time >= nextUpdateTime)
                UpdateInteraction();
            return;
        }

        if (isDecaying)
            UpdateDecay();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolvePlayer(other, out Transform resolvedPlayer))
            return;

        bool wasOutside = playerColliders.Count == 0;
        playerColliders.Add(other);
        if (!wasOutside)
            return;

        playerTransform = resolvedPlayer;
        BeginInteraction();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!TryResolvePlayer(other, out Transform resolvedPlayer))
            return;

        bool wasOutside = playerColliders.Count == 0;
        playerColliders.Add(other);
        playerTransform = resolvedPlayer;
        if (wasOutside)
            BeginInteraction();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count > 0)
            return;

        BeginDecay();
    }

    public void CollectWakeRenderers()
    {
        wakeRenderers.Clear();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                bool matches = wakeMaterial != null
                    ? material == wakeMaterial
                    : material.IsKeywordEnabled(WakeKeyword);
                if (!matches)
                    continue;

                wakeRenderers.Add(renderer);
                break;
            }
        }

        if (enableDebugLogs)
            Debug.Log($"[WaterStreamInteractor] Collected {wakeRenderers.Count} wake renderers on {name}.", this);
    }

    private bool TryResolvePlayer(Collider other, out Transform resolvedPlayer)
    {
        resolvedPlayer = null;
        if (other == null)
            return false;

        if (playerObject != null &&
            (other.gameObject == playerObject || other.transform.IsChildOf(playerObject.transform)))
        {
            resolvedPlayer = playerObject.transform;
            return true;
        }

        PlayerMovement3D movement = other.GetComponentInParent<PlayerMovement3D>();
        if (movement != null)
        {
            playerObject = movement.gameObject;
            resolvedPlayer = movement.transform;
            return true;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            Transform candidate = other.transform;
            while (candidate != null)
            {
                if (candidate.CompareTag(playerTag))
                {
                    playerObject = candidate.gameObject;
                    resolvedPlayer = candidate;
                    return true;
                }
                candidate = candidate.parent;
            }
        }

        return false;
    }

    private void ResolveConfiguredPlayer()
    {
        if (playerObject == null && !string.IsNullOrEmpty(playerTag))
        {
            try { playerObject = GameObject.FindGameObjectWithTag(playerTag); }
            catch (UnityException) { playerObject = null; }
        }

        if (playerObject == null)
        {
            PlayerMovement3D movement = FindFirstObjectByType<PlayerMovement3D>();
            if (movement != null)
                playerObject = movement.gameObject;
        }

        playerTransform = playerObject != null ? playerObject.transform : null;
    }

    private void BeginInteraction()
    {
        isDecaying = false;
        currentPosition = playerTransform.position;
        currentVelocity = Vector3.zero;
        currentStrengthScale = 1f;
        lastPlayerPosition = currentPosition;
        lastSampleTime = Time.time;
        nextUpdateTime = Time.time;
        ApplyWake();

        EnsureFootFoam();
        PositionFootFoam();
        if (footFoamInstance != null)
        {
            SetFoamEmission(0f);
            footFoamInstance.Play(true);
            if (enterFoamCount > 0)
                footFoamInstance.Emit(enterFoamCount);
        }

        if (enableDebugLogs)
            Debug.Log($"[WaterStreamInteractor] Player entered {name}.", this);
    }

    private void UpdateInteraction()
    {
        float now = Time.time;
        float deltaTime = Mathf.Max(now - lastSampleTime, 0.001f);
        currentPosition = playerTransform.position;
        currentVelocity = (currentPosition - lastPlayerPosition) / deltaTime;
        currentVelocity.y = 0f;
        currentStrengthScale = 1f;

        lastPlayerPosition = currentPosition;
        lastSampleTime = now;
        nextUpdateTime = now + 1f / updateRateHz;

        ApplyWake();
        UpdateFootFoam();
    }

    private void BeginDecay()
    {
        isDecaying = true;
        decayStartedAt = Time.time;
        decayVelocity = currentVelocity;
        decayStrength = currentStrengthScale;
        StopFootFoamEmission();

        if (enableDebugLogs)
            Debug.Log($"[WaterStreamInteractor] Player exited {name}; wake decay started.", this);
    }

    private void UpdateDecay()
    {
        float linearT = Mathf.Clamp01((Time.time - decayStartedAt) / decayTime);
        float easedT = 1f - (1f - linearT) * (1f - linearT);
        currentVelocity = Vector3.Lerp(decayVelocity, Vector3.zero, easedT);
        currentStrengthScale = Mathf.Lerp(decayStrength, 0f, easedT);
        ApplyWake();

        if (linearT < 1f)
            return;

        isDecaying = false;
        ResetWake();
    }

    private void ApplyWake()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        for (int i = wakeRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = wakeRenderers[i];
            if (renderer == null)
            {
                wakeRenderers.RemoveAt(i);
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(InteractorPosWSId, currentPosition);
            propertyBlock.SetVector(InteractorVelWSId, currentVelocity);
            propertyBlock.SetFloat(InteractRadiusId, interactRadius);
            propertyBlock.SetFloat(BowlStrengthId, bowlStrength * currentStrengthScale);
            propertyBlock.SetFloat(RingStrengthId, ringStrength * currentStrengthScale);
            propertyBlock.SetFloat(RingWidthId, ringWidth);
            propertyBlock.SetFloat(MaxDownId, maxDown);
            propertyBlock.SetFloat(StretchId, wakeStretch);
            propertyBlock.SetFloat(FoamStrengthId, foamStrength * currentStrengthScale);
            propertyBlock.SetFloat(DownstreamBiasId, downstreamBias);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ResetWake()
    {
        currentPosition = Vector3.zero;
        currentVelocity = Vector3.zero;
        currentStrengthScale = 0f;
        ApplyWake();
    }

    private void EnsureFootFoam()
    {
        if (footFoamInstance != null || footFoamPrefab == null)
            return;

        footFoamInstance = Instantiate(footFoamPrefab, transform);
        footFoamInstance.name = footFoamPrefab.name;
        SetFoamEmission(0f);
        footFoamInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateFootFoam()
    {
        if (footFoamInstance == null)
            return;

        PositionFootFoam();
        float planarSpeed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;
        if (planarSpeed < foamMoveThreshold)
        {
            SetFoamEmission(0f);
            return;
        }

        float speedT = Mathf.InverseLerp(foamMoveThreshold, foamMaxSpeed, planarSpeed);
        SetFoamEmission(Mathf.Lerp(0.6f, foamMaxEmissionRate, speedT));
        if (!footFoamInstance.isPlaying)
            footFoamInstance.Play(true);
    }

    private void PositionFootFoam()
    {
        if (footFoamInstance == null || playerTransform == null)
            return;

        Vector3 position = playerTransform.position;
        position.y = ResolveWaterSurfaceY() + foamSurfaceOffset;
        footFoamInstance.transform.position = position;
        footFoamInstance.transform.rotation = Quaternion.identity;
    }

    private float ResolveWaterSurfaceY()
    {
        for (int i = 0; i < wakeRenderers.Count; i++)
        {
            Renderer renderer = wakeRenderers[i];
            if (renderer == null)
                continue;

            float surfaceY = renderer.bounds.center.y;
            Material material = renderer.sharedMaterial;
            if (material == null || playerTransform == null || !material.IsKeywordEnabled("_ENABLE_WAVES"))
                return surfaceY;

            Vector3 samplePosition = playerTransform.position;
            samplePosition.y = surfaceY;
            samplePosition += EvaluateGerstnerDisplacement(
                samplePosition,
                material.GetVector("_WaveDirection"),
                material.GetFloat("_WaveAmplitude"),
                material.GetFloat("_WaveFrequency"),
                material.GetFloat("_WaveSpeed"),
                Time.time);

            if (material.IsKeywordEnabled("_ENABLE_SECONDARY_WAVE"))
            {
                samplePosition += EvaluateGerstnerDisplacement(
                    samplePosition,
                    material.GetVector("_Wave2Direction"),
                    material.GetFloat("_Wave2Amplitude"),
                    material.GetFloat("_Wave2Frequency"),
                    material.GetFloat("_Wave2Speed"),
                    Time.time);
            }

            return samplePosition.y;
        }
        return transform.position.y;
    }

    private static Vector3 EvaluateGerstnerDisplacement(
        Vector3 worldPosition,
        Vector4 directionProperty,
        float amplitude,
        float frequency,
        float speed,
        float time)
    {
        Vector2 direction = new Vector2(directionProperty.x, directionProperty.z);
        if (direction.sqrMagnitude < 0.0001f || amplitude <= 0f)
            return Vector3.zero;

        direction.Normalize();
        float phase = Vector2.Dot(direction, new Vector2(worldPosition.x, worldPosition.z)) * frequency + time * speed;
        float cosine = Mathf.Cos(phase);
        return new Vector3(
            direction.x * amplitude * cosine,
            amplitude * Mathf.Sin(phase),
            direction.y * amplitude * cosine);
    }

    private void SetFoamEmission(float rate)
    {
        if (footFoamInstance == null)
            return;

        ParticleSystem.EmissionModule emission = footFoamInstance.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, rate);
    }

    private void StopFootFoamEmission()
    {
        SetFoamEmission(0f);
        if (footFoamInstance != null)
            footFoamInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        isDecaying = false;
        StopFootFoamEmission();
        ResetWake();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.8f);
        Vector3 center = Application.isPlaying && playerTransform != null
            ? playerTransform.position
            : transform.position;
        Gizmos.DrawWireSphere(center, interactRadius);
    }
}
