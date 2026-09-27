using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pooled world-space wind heads with fading TrailRenderer histories.</summary>
public sealed class AtmosphericWindTrailEmitter : MonoBehaviour
{
    private static readonly int TrailPhaseId = Shader.PropertyToID("_TrailPhase");

    private sealed class TrailState
    {
        public GameObject Host;
        public TrailRenderer Renderer;
        public MaterialPropertyBlock Properties;
        public Vector3 BasePosition;
        public Vector3 Direction;
        public Vector3 WaveAxis;
        public float Age;
        public float ActiveDuration;
        public float Phase;
        public float Frequency;
        public float Amplitude;
        public bool Active;
        public bool Draining;
    }

    [SerializeField] private List<TrailRenderer> authoredPool = new List<TrailRenderer>();

    private readonly List<TrailState> states = new List<TrailState>();
    private AtmosphericTrailSettings settings;
    private Material material;
    private float spawnAccumulator;
    private float desiredRate;
    private int maxConcurrent = 4;
    private Color desiredColor = Color.white;
    private Vector2 widthRange = new Vector2(0.03f, 0.08f);
    private Vector2 headLifetimeRange = new Vector2(2.5f, 4.5f);

    public void Configure(AtmosphericLayerSettings layer, AtmosphericTrailSettings trailSettings, Material sharedMaterial)
    {
        settings = trailSettings;
        material = sharedMaterial;
        widthRange = layer != null ? layer.StartSize : widthRange;
        headLifetimeRange = layer != null ? layer.Lifetime : headLifetimeRange;
        SetMaxConcurrent(layer != null ? layer.MaxParticles : 4);
        EnsurePool(maxConcurrent);
        for (int i = 0; i < states.Count; i++)
            ConfigureRenderer(states[i].Renderer);
    }

    public void SetPresentation(float rate, Color color)
    {
        bool emissionStopped = desiredRate > 0.001f && rate <= 0.001f;
        desiredRate = Mathf.Max(0f, rate);
        desiredColor = color;
        if (!emissionStopped)
            return;

        spawnAccumulator = 0f;
        for (int i = 0; i < states.Count; i++)
        {
            TrailState state = states[i];
            if (!state.Active || state.Draining)
                continue;
            state.Renderer.emitting = false;
            state.Draining = true;
            state.ActiveDuration = state.Age;
        }
    }

    public void SetMaxConcurrent(int value)
    {
        maxConcurrent = Mathf.Clamp(value, 1, 16);
        for (int i = maxConcurrent; i < states.Count; i++)
        {
            TrailState state = states[i];
            if (!state.Active || state.Draining)
                continue;
            state.Renderer.emitting = false;
            state.Draining = true;
            state.ActiveDuration = state.Age;
        }
    }

    public void Tick(Camera camera, Vector3 volumeSize, Vector3 windVelocity, float deltaTime)
    {
        if (settings == null || camera == null || deltaTime <= 0f)
            return;

        UpdateExisting(deltaTime, windVelocity, camera.transform.up);
        if (desiredRate <= 0.001f)
            return;

        spawnAccumulator += desiredRate * deltaTime;
        while (spawnAccumulator >= 1f)
        {
            spawnAccumulator -= 1f;
            Spawn(volumeSize, windVelocity, camera.transform.up);
        }
    }

    public void StopAndClear()
    {
        desiredRate = 0f;
        spawnAccumulator = 0f;
        for (int i = 0; i < states.Count; i++)
        {
            TrailState state = states[i];
            state.Renderer.emitting = false;
            state.Renderer.Clear();
            state.Host.SetActive(false);
            state.Active = false;
            state.Draining = false;
        }
    }

    public void EditorSetPool(List<TrailRenderer> pool)
    {
        authoredPool = pool ?? new List<TrailRenderer>();
        states.Clear();
    }

    private void EnsurePool(int requestedCount)
    {
        int count = Mathf.Clamp(requestedCount, 1, 16);
        for (int i = 0; i < authoredPool.Count && states.Count < count; i++)
        {
            TrailRenderer renderer = authoredPool[i];
            if (renderer != null)
                states.Add(CreateState(renderer));
        }

        while (states.Count < count)
        {
            GameObject child = new GameObject("WindTrail_" + states.Count.ToString("00"));
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            TrailRenderer renderer = child.AddComponent<TrailRenderer>();
            authoredPool.Add(renderer);
            states.Add(CreateState(renderer));
        }
    }

    private static TrailState CreateState(TrailRenderer renderer)
    {
        renderer.gameObject.SetActive(false);
        return new TrailState
        {
            Host = renderer.gameObject,
            Renderer = renderer,
            Properties = new MaterialPropertyBlock()
        };
    }

    private void ConfigureRenderer(TrailRenderer renderer)
    {
        renderer.sharedMaterial = material;
        renderer.time = settings.TrailTime;
        renderer.minVertexDistance = settings.MinVertexDistance;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.alignment = LineAlignment.View;
        renderer.numCornerVertices = 0;
        renderer.numCapVertices = 0;
        renderer.generateLightingData = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.autodestruct = false;
        renderer.emitting = false;
    }

    private void Spawn(Vector3 volumeSize, Vector3 windVelocity, Vector3 cameraUp)
    {
        TrailState state = FindAvailableState();
        if (state == null)
            return;

        Vector3 local = new Vector3(
            Random.Range(-0.5f, 0.5f) * volumeSize.x,
            Random.Range(-0.5f, 0.5f) * volumeSize.y,
            Random.Range(-0.5f, 0.5f) * volumeSize.z);
        Vector3 start = transform.TransformPoint(local);
        Vector3 direction = windVelocity.sqrMagnitude > 0.0001f ? windVelocity.normalized : transform.right;
        Vector3 waveAxis = Vector3.ProjectOnPlane(cameraUp, direction).normalized;
        if (waveAxis.sqrMagnitude < 0.0001f)
            waveAxis = Vector3.up;

        // Place while inactive and non-emitting. Enabling at the pooled object's
        // previous position can make TrailRenderer connect that stale point to
        // the new spawn with one screen-spanning straight segment.
        state.Renderer.emitting = false;
        state.Host.transform.position = start;
        state.Host.SetActive(true);
        state.Renderer.Clear();
        state.BasePosition = start;
        state.Direction = direction;
        state.WaveAxis = waveAxis;
        state.Age = 0f;
        state.ActiveDuration = Random.Range(
            Mathf.Min(headLifetimeRange.x, headLifetimeRange.y),
            Mathf.Max(headLifetimeRange.x, headLifetimeRange.y));
        state.Phase = Random.value * Mathf.PI * 2f;
        state.Frequency = Random.Range(settings.WaveFrequency.x, settings.WaveFrequency.y);
        state.Amplitude = Random.Range(settings.WaveAmplitude.x, settings.WaveAmplitude.y);
        state.Active = true;
        state.Draining = false;

        state.Renderer.widthMultiplier = Random.Range(
            Mathf.Min(widthRange.x, widthRange.y),
            Mathf.Max(widthRange.x, widthRange.y));
        state.Renderer.colorGradient = BuildFadeGradient(desiredColor);
        state.Properties.SetFloat(TrailPhaseId, state.Phase);
        state.Renderer.SetPropertyBlock(state.Properties);
        state.Renderer.emitting = true;
    }

    private void UpdateExisting(float deltaTime, Vector3 windVelocity, Vector3 cameraUp)
    {
        float speed = Mathf.Max(settings.MinimumSpeed, windVelocity.magnitude * settings.SpeedMultiplier);
        for (int i = 0; i < states.Count; i++)
        {
            TrailState state = states[i];
            if (!state.Active)
                continue;

            state.Age += deltaTime;
            if (!state.Draining && state.Age >= state.ActiveDuration)
            {
                state.Renderer.emitting = false;
                state.Draining = true;
            }

            if (state.Draining)
            {
                if (state.Age >= state.ActiveDuration + settings.TrailTime)
                {
                    state.Renderer.Clear();
                    state.Host.SetActive(false);
                    state.Active = false;
                    state.Draining = false;
                }
                continue;
            }

            Vector3 direction = windVelocity.sqrMagnitude > 0.0001f ? windVelocity.normalized : state.Direction;
            Vector3 waveAxis = Vector3.ProjectOnPlane(cameraUp, direction).normalized;
            if (waveAxis.sqrMagnitude < 0.0001f)
                waveAxis = state.WaveAxis;
            state.Direction = Vector3.Slerp(state.Direction, direction, Mathf.Clamp01(deltaTime * 5f)).normalized;
            state.WaveAxis = Vector3.Slerp(state.WaveAxis, waveAxis, Mathf.Clamp01(deltaTime * 5f)).normalized;

            // Integrate forward motion from the current point. Rebuilding the
            // position from the original spawn and a changing direction causes
            // a large endpoint jump whenever a camera zone rotates the wind.
            state.BasePosition += state.Direction * (speed * deltaTime);
            float distance = state.Age * speed;
            float wave = Mathf.Sin(distance * state.Frequency + state.Phase) * state.Amplitude;
            float flutter = Mathf.Sin(distance * state.Frequency * 2.17f + state.Phase * 0.73f) * state.Amplitude * 0.22f;
            state.Host.transform.position = state.BasePosition + state.WaveAxis * (wave + flutter);
        }
    }

    private TrailState FindAvailableState()
    {
        TrailState oldest = null;
        int usableCount = Mathf.Min(maxConcurrent, states.Count);
        for (int i = 0; i < usableCount; i++)
        {
            if (!states[i].Active)
                return states[i];
            if (oldest == null || states[i].Age > oldest.Age)
                oldest = states[i];
        }

        if (oldest != null)
        {
            oldest.Renderer.emitting = false;
            oldest.Renderer.Clear();
        }
        return oldest;
    }

    private static Gradient BuildFadeGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a * 0.35f, 0.35f),
                new GradientAlphaKey(color.a, 1f)
            });
        return gradient;
    }
}
