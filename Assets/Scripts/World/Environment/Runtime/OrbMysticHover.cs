using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OrbMysticHover : MonoBehaviour
{
    [Header("Anchor / Target")]
    [Tooltip("Anchor pivot. If null, uses this transform.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Object to move/rotate. If null, uses this transform.")]
    [SerializeField] private Transform target;

    [Header("Hover (Bob)")]
    [Tooltip("Vertical bob amplitude in meters.")]
    [SerializeField] private float bobAmplitude = 0.06f;

    [Tooltip("Cycles per second. 0.2 = one up/down cycle every 5 seconds.")]
    [SerializeField] private float bobFrequency = 0.25f;

    [Tooltip("Random phase per instance so multiple Orbs don't sync.")]
    [SerializeField] private bool randomizePhase = true;

    [Header("Rotation")]
    [Tooltip("Yaw rotation speed in degrees/sec.")]
    [SerializeField] private float yawDegreesPerSecond = 6f;

    [Tooltip("Rotate around anchor's up axis (recommended). If false, uses world up.")]
    [SerializeField] private bool rotateAroundAnchorUp = true;

    [Header("Timing")]
    [Tooltip("Use unscaled time (keeps animating during Time.timeScale=0).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Pixel Particle Emission (Optional)")]
    [Tooltip("Prefab spawned under the Orb. Use your existing pixel-perfect dust/debris style prefab.")]
    [SerializeField] private GameObject pixelParticlePrefab;

    [Tooltip("Spawn point (for example an empty child under the Orb). If null, uses anchor.")]
    [SerializeField] private Transform particleSpawnPoint;

    [Tooltip("Particles per second. Keep low for mobile (0.5 - 3).")]
    [SerializeField] private float particleRate = 1.2f;

    [Tooltip("Random horizontal spawn radius around spawn point.")]
    [SerializeField] private float particleSpawnRadius = 0.06f;

    [Tooltip("Random vertical offset added to spawn point.")]
    [SerializeField] private float particleSpawnYOffset = 0.0f;

    [Tooltip("Optional: rotate spawned particles to face camera if they are quads.")]
    [SerializeField] private bool faceCameraOnSpawn = false;

    [Header("Sparkle Burst (Optional)")]
    [Tooltip("Prefab that contains a billboard quad + sprite frame animator (4-5 frames) and self-destroys.")]
    [SerializeField] private GameObject sparkleBurstPrefab;

    [Tooltip("Seconds between sparkle triggers (random range).")]
    [SerializeField] private Vector2 sparkleIntervalRange = new Vector2(8f, 10f);

    [Tooltip("Radius around anchor where sparkle can appear.")]
    [SerializeField] private float sparkleRadius = 0.10f;

    [Tooltip("Sparkle vertical offset relative to anchor (for example slightly above base).")]
    [SerializeField] private float sparkleYOffset = 0.03f;

    [Tooltip("Optional: restrict sparkle to spawn only within a ring (min,max). Set min=0 for full disk.")]
    [SerializeField] private Vector2 sparkleRadiusRing = new Vector2(0f, 0.10f);

    [Header("Enable Flags")]
    [SerializeField] private bool enableHover = true;
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private bool enableParticles = true;
    [SerializeField] private bool enableSparkles = true;

    private Vector3 _baseLocalPos;
    private float _phase;
    private float _particleAccumulator;
    private Coroutine _sparkleRoutine;

    private void Awake()
    {
        if (anchor == null) anchor = transform;
        if (target == null) target = transform;

        // Record base offset in anchor-local space so the bob stays relative to placement.
        _baseLocalPos = anchor.InverseTransformPoint(target.position);

        if (randomizePhase)
            _phase = Random.value * Mathf.PI * 2f;

        if (particleSpawnPoint == null)
            particleSpawnPoint = anchor;
    }

    private void OnEnable()
    {
        if (enableSparkles && sparkleBurstPrefab != null)
            _sparkleRoutine = StartCoroutine(SparkleLoop());
    }

    private void OnDisable()
    {
        if (_sparkleRoutine != null)
        {
            StopCoroutine(_sparkleRoutine);
            _sparkleRoutine = null;
        }
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;

        if (enableHover) ApplyHover();
        if (enableRotation) ApplyRotation(dt);
        if (enableParticles) TickParticles(dt);
    }

    private void ApplyHover()
    {
        // Smooth bob using SmoothStep to soften the peaks and dips.
        float t = (useUnscaledTime ? Time.unscaledTime : Time.time) * bobFrequency * (Mathf.PI * 2f) + _phase;
        float s = Mathf.Sin(t);
        float normalized = (s + 1f) * 0.5f;
        float eased = Mathf.SmoothStep(0f, 1f, normalized);
        float offset = (eased - 0.5f) * 2f * bobAmplitude;

        Vector3 local = _baseLocalPos + new Vector3(0f, offset, 0f);
        Vector3 world = anchor.TransformPoint(local);
        target.position = world;
    }

    private void ApplyRotation(float dt)
    {
        Vector3 axis = rotateAroundAnchorUp ? anchor.up : Vector3.up;
        target.RotateAround(anchor.position, axis, yawDegreesPerSecond * dt);
    }

    private void TickParticles(float dt)
    {
        if (pixelParticlePrefab == null || particleRate <= 0f) return;

        _particleAccumulator += dt * particleRate;
        int spawnCount = Mathf.FloorToInt(_particleAccumulator);
        if (spawnCount <= 0) return;

        _particleAccumulator -= spawnCount;

        for (int i = 0; i < spawnCount; i++)
            SpawnPixelParticle();
    }

    private void SpawnPixelParticle()
    {
        Vector3 center = particleSpawnPoint.position + new Vector3(0f, particleSpawnYOffset, 0f);
        Vector2 r = Random.insideUnitCircle * particleSpawnRadius;
        Vector3 pos = center + new Vector3(r.x, 0f, r.y);

        GameObject go = Instantiate(pixelParticlePrefab, pos, Quaternion.identity);

        if (faceCameraOnSpawn)
        {
            Camera cam = SceneCameraResolver.ResolveGameplayCamera();
            if (cam != null)
            {
                Vector3 forward = cam.transform.forward;
                go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
    }

    private IEnumerator SparkleLoop()
    {
        while (true)
        {
            float wait = Random.Range(sparkleIntervalRange.x, sparkleIntervalRange.y);
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(wait);
            else yield return new WaitForSeconds(wait);

            if (!enableSparkles || sparkleBurstPrefab == null) continue;

            SpawnSparkleBurst();
        }
    }

    private void SpawnSparkleBurst()
    {
        float minR = Mathf.Max(0f, sparkleRadiusRing.x);
        float maxR = Mathf.Max(minR, sparkleRadiusRing.y);

        float radius = Mathf.Sqrt(Random.Range(minR * minR, maxR * maxR));
        float angle = Random.Range(0f, Mathf.PI * 2f);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        Vector3 pos = anchor.position + offset + new Vector3(0f, sparkleYOffset, 0f);

        GameObject go = Instantiate(sparkleBurstPrefab, pos, Quaternion.identity);

        Camera cam = SceneCameraResolver.ResolveGameplayCamera();
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
