using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Overworld-only camera-follow atmosphere. Emits motes, wind streaks, and
/// billboard debris from the active scene's AtmosphericProfileSO.
/// Combat and interiors stay dark because they release the persistent camera.
/// </summary>
public sealed class AtmosphericCameraVolume : MonoBehaviour
{
    private const string ShaderName = "SignalJam/Sprites/AtmosphericSoftParticle";
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int DepthFadeId = Shader.PropertyToID("_DepthFadeDist");
    private static readonly int ParticleColorId = Shader.PropertyToID("_ParticleColor");
    private static readonly int BaseAlphaId = Shader.PropertyToID("_BaseAlpha");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly Vector3 WindGlobalIdFallback = new Vector3(0.85f, 0f, 0.2f);

    [Header("Data")]
    [SerializeField] private AtmosphericCatalogSO catalog;
    [SerializeField] private AtmosphericProfileSO debugOverrideProfile;

    [Header("Materials")]
    [SerializeField] private Material additiveMaterial;
    [SerializeField] private Material streakMaterial;
    [SerializeField] private Material alphaMaterial;

    [Header("Layers")]
    [SerializeField] private ParticleSystem motes;
    [SerializeField] private ParticleSystem streaks;
    [SerializeField] private AtmosphericWindTrailEmitter windTrailEmitter;
    [SerializeField] private ParticleSystem debris;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private Material additiveInstance;
    private Material alphaInstance;
    private Material moteInstance;
    private Material streakInstance;
    private Material debrisInstance;
    private AtmosphericProfileSO fromProfile;
    private AtmosphericProfileSO toProfile;
    private float blendT = 1f;
    private string currentSceneId = string.Empty;
    private float indoorWeight;
    private bool overworldLive;
    private bool timeSubscribed;
    private TimeOfDayManager subscribedManager;
    private float lastAppliedTime = -1f;
    private static readonly int WindDirGlobal = Shader.PropertyToID("_AtmosphereWindDirWS");
    private static readonly int WindSpeedGlobal = Shader.PropertyToID("_AtmosphereWindSpeed");

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        TimeOfDayManager.OnInstanceAvailable += HandleTimeManagerAvailable;
        EnsureParticleSystems();
        EnsureMaterials();
        currentSceneId = SceneManager.GetActiveScene().name;
        toProfile = ResolveProfile(currentSceneId);
        fromProfile = toProfile;
        blendT = 1f;
        indoorWeight = TimeOfDayVisualApplier.IsAnyIndoors ? 0f : 1f;
        SubscribeTime();
        ApplyState(force: true);
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(ref moteInstance);
        DestroyRuntimeMaterial(ref streakInstance);
        DestroyRuntimeMaterial(ref debrisInstance);
        DestroyRuntimeMaterial(ref additiveInstance);
        DestroyRuntimeMaterial(ref alphaInstance);
    }

    private static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(material);
        else
            Object.DestroyImmediate(material);
        material = null;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        TimeOfDayManager.OnInstanceAvailable -= HandleTimeManagerAvailable;
        UnsubscribeTime();
        StopAndClear();
    }

    private void LateUpdate()
    {
        bool live = SceneCameraResolver.HasLivePersistentCamera;
        if (live != overworldLive)
        {
            overworldLive = live;
            if (!live)
                StopAndClear();
            else
                ApplyState(force: true);
        }

        if (!overworldLive)
            return;

        Camera camera = SceneCameraResolver.PersistentGameplayCamera;
        AtmosphericProfileSO profile = ActiveProfile;
        if (camera == null || profile == null)
        {
            StopAndClear();
            return;
        }

        Vector3 volumeSize = CalculateVolumeSize(camera, profile.Follow);
        FollowTarget(camera, profile.Follow, volumeSize);
        UpdateVolumeCoverage(volumeSize);
        TickIndoor(profile.IndoorFadeSeconds);
        TickSceneBlend(profile);
        Vector3 windVelocity = UpdateWind(camera, profile.Wind);
        ApplyRates(TimeOfDayManager.Instance != null ? TimeOfDayManager.Instance.NormalizedTime : 0.5f);
        if (windTrailEmitter != null)
            windTrailEmitter.Tick(camera, volumeSize, windVelocity, Time.deltaTime);
    }

    private AtmosphericProfileSO ActiveProfile
    {
        get
        {
            if (debugOverrideProfile != null)
                return debugOverrideProfile;
            if (toProfile != null && blendT >= 0.999f)
                return toProfile;
            return toProfile != null ? toProfile : fromProfile;
        }
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        SetScene(next.name);
    }

    private void SetScene(string sceneId)
    {
        if (string.Equals(currentSceneId, sceneId, System.StringComparison.OrdinalIgnoreCase))
            return;

        currentSceneId = sceneId ?? string.Empty;
        AtmosphericProfileSO next = ResolveProfile(currentSceneId);
        fromProfile = ActiveProfile;
        toProfile = next;
        blendT = 0f;
        if (catalog == null || catalog.SceneBlendSeconds <= 0.001f || fromProfile == next)
            blendT = 1f;

        if (enableDebugLogs)
            Debug.Log($"[AtmosphericCameraVolume] Scene '{currentSceneId}' -> profile '{(next != null ? next.ProfileId : "none")}'");

        ApplyState(force: true);
    }

    private AtmosphericProfileSO ResolveProfile(string sceneId)
    {
        if (debugOverrideProfile != null)
            return debugOverrideProfile;
        return catalog != null ? catalog.Resolve(sceneId) : null;
    }

    private void TickSceneBlend(AtmosphericProfileSO fallback)
    {
        string activeName = SceneManager.GetActiveScene().name;
        if (!string.Equals(activeName, currentSceneId, System.StringComparison.OrdinalIgnoreCase))
            SetScene(activeName);

        if (blendT >= 1f)
            return;

        float duration = catalog != null ? Mathf.Max(0.01f, catalog.SceneBlendSeconds) : 1f;
        blendT = Mathf.MoveTowards(blendT, 1f, Time.deltaTime / duration);
        if (blendT >= 1f)
            fromProfile = toProfile != null ? toProfile : fallback;
    }

    private void TickIndoor(float fadeSeconds)
    {
        float target = TimeOfDayVisualApplier.IsAnyIndoors ? 0f : 1f;
        float duration = Mathf.Max(0.05f, fadeSeconds);
        indoorWeight = Mathf.MoveTowards(indoorWeight, target, Time.deltaTime / duration);
    }

    private void FollowTarget(Camera camera, AtmosphericFollowSettings follow, Vector3 volumeSize)
    {
        Transform cam = camera.transform;
        Vector3 anchor = ResolveUndampedPlayerAnchor(camera, out bool foundPlayer);
        Vector3 offset = follow != null ? follow.LocalOffset : new Vector3(0f, 1.5f, 2f);

        Vector3 viewForward = cam.forward;
        if (viewForward.sqrMagnitude < 0.0001f)
            viewForward = Vector3.forward;
        viewForward.Normalize();

        Vector3 viewRight = Vector3.ProjectOnPlane(cam.right, Vector3.up);
        if (viewRight.sqrMagnitude < 0.0001f)
            viewRight = Vector3.right;
        viewRight.Normalize();

        // The player marks the far/front portion of the volume. Increasing Z
        // therefore extends the box back toward the lens instead of pushing its
        // center farther into the scene.
        float halfDepth = Mathf.Max(0f, volumeSize.z * 0.5f);
        Vector3 worldOffset = viewRight * offset.x + Vector3.up * offset.y + viewForward * (offset.z - halfDepth);
        transform.position = foundPlayer ? anchor + worldOffset : cam.position + viewForward * halfDepth;
        Vector3 forward = cam.forward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private static Vector3 ResolveUndampedPlayerAnchor(Camera camera, out bool foundPlayer)
    {
        foundPlayer = false;
        TopDownCameraController3D controller = SceneCameraResolver.GetController(camera);
        Transform target = controller != null ? controller.Target : null;
        if (target == null)
            return camera.transform.position;

        foundPlayer = true;
        PlayerMovement3D movement = target.GetComponent<PlayerMovement3D>();
        if (movement == null)
            movement = target.GetComponentInChildren<PlayerMovement3D>(true);

        Collider playerCollider = movement != null ? movement.PlayerCollider : null;
        if (playerCollider == null)
            playerCollider = target.GetComponentInChildren<Collider>(true);

        return playerCollider != null ? playerCollider.bounds.center : target.position;
    }

    private static Vector3 CalculateVolumeSize(Camera camera, AtmosphericFollowSettings follow)
    {
        if (camera == null || follow == null)
            return new Vector3(18f, 12f, 42f);

        float zoomScale = camera.orthographic
            ? camera.orthographicSize / Mathf.Max(0.1f, follow.ReferenceOrthographicSize)
            : Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) /
              Mathf.Tan(Mathf.Max(1f, follow.ReferenceFieldOfView) * 0.5f * Mathf.Deg2Rad);
        zoomScale = Mathf.Clamp(zoomScale, 1f, Mathf.Max(1f, follow.MaximumZoomScale));

        // Width and depth need the full zoom compensation. Height gets a softer
        // expansion so particles cover the view without multiplying overdraw.
        Vector3 baseSize = follow.BoxSize;
        return new Vector3(
            baseSize.x * zoomScale,
            baseSize.y * Mathf.Lerp(1f, zoomScale, 0.5f),
            baseSize.z * zoomScale);
    }

    private void UpdateVolumeCoverage(Vector3 scaledSize)
    {
        SetShapeScale(motes, scaledSize);
        SetShapeScale(streaks, scaledSize);
        SetShapeScale(debris, scaledSize);
    }

    private static void SetShapeScale(ParticleSystem system, Vector3 scale)
    {
        if (system == null)
            return;
        var shape = system.shape;
        shape.scale = scale;
    }

    private Vector3 UpdateWind(Camera camera, AtmosphericWindSettings wind)
    {
        Vector3 velocity = ResolveWindVelocity(camera, wind);
        SetVelocity(motes, velocity);
        SetVelocity(debris, velocity);
        Shader.SetGlobalVector(WindDirGlobal, velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector3.right);
        Shader.SetGlobalFloat(WindSpeedGlobal, velocity.magnitude);
        return velocity;
    }

    private static Vector3 ResolveWindVelocity(Camera camera, AtmosphericWindSettings wind)
    {
        Vector3 velocity = wind != null ? wind.EvaluateWorldVelocity() : WindGlobalIdFallback;
        if (wind == null || !wind.AlignToCameraYaw || camera == null)
            return velocity;

        Vector3 direction = wind.Direction;
        Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Vector3 cameraRelative = right * direction.x + Vector3.up * (direction.y + wind.VerticalBias) + forward * direction.z;
        if (cameraRelative.sqrMagnitude < 0.0001f)
            cameraRelative = right;
        return cameraRelative.normalized * wind.Speed;
    }

    private static void SetVelocity(ParticleSystem system, Vector3 velocity)
    {
        if (system == null)
            return;
        var module = system.velocityOverLifetime;
        module.enabled = true;
        module.space = ParticleSystemSimulationSpace.World;
        module.x = velocity.x;
        module.y = velocity.y;
        module.z = velocity.z;
    }

    private void SubscribeTime()
    {
        if (timeSubscribed)
            return;
        TimeOfDayManager manager = TimeOfDayManager.Instance;
        if (manager == null)
            return;
        manager.OnTimeNormalizedChanged += HandleTimeChanged;
        subscribedManager = manager;
        timeSubscribed = true;
        HandleTimeChanged(manager.NormalizedTime);
    }

    private void UnsubscribeTime()
    {
        if (!timeSubscribed)
            return;
        if (subscribedManager != null)
            subscribedManager.OnTimeNormalizedChanged -= HandleTimeChanged;
        subscribedManager = null;
        timeSubscribed = false;
    }

    private void HandleTimeManagerAvailable(TimeOfDayManager manager)
    {
        if (manager == null)
            return;
        UnsubscribeTime();
        SubscribeTime();
    }

    private void HandleTimeChanged(float normalizedTime)
    {
        lastAppliedTime = normalizedTime;
        if (overworldLive)
            ApplyRates(normalizedTime);
    }

    private void ApplyState(bool force)
    {
        EnsureParticleSystems();
        EnsureMaterials();
        overworldLive = SceneCameraResolver.HasLivePersistentCamera;
        if (!overworldLive)
        {
            StopAndClear();
            return;
        }

        AtmosphericProfileSO profile = toProfile != null ? toProfile : fromProfile;
        if (profile == null)
        {
            StopAndClear();
            return;
        }

        ConfigureLayer(motes, profile.Motes, profile);
        ConfigureLayer(debris, profile.Debris, profile);
        EnsureWindTrailEmitter();
        if (windTrailEmitter != null)
            windTrailEmitter.Configure(profile.Streaks, profile.WindTrails, streakMaterial);
        ApplyRates(lastAppliedTime >= 0f ? lastAppliedTime : (TimeOfDayManager.Instance != null ? TimeOfDayManager.Instance.NormalizedTime : 0.5f));
        if (force && enableDebugLogs)
            Debug.Log("[AtmosphericCameraVolume] Applied profile " + profile.ProfileId);
    }

    private void ApplyRates(float normalizedTime)
    {
        AtmosphericProfileSO a = fromProfile != null ? fromProfile : toProfile;
        AtmosphericProfileSO b = toProfile != null ? toProfile : fromProfile;
        if (b == null)
        {
            StopAndClear();
            return;
        }

        float t = Mathf.Clamp01(blendT);
        float outdoor = indoorWeight * (overworldLive ? 1f : 0f);
        ApplyLayerRate(motes, a != null ? a.Motes : null, b.Motes, normalizedTime, t, outdoor);
        ApplyWindTrailRate(a != null ? a.Streaks : null, b.Streaks, normalizedTime, t, outdoor);
        ApplyLayerRate(debris, a != null ? a.Debris : null, b.Debris, normalizedTime, t, outdoor);
    }

    private void ApplyWindTrailRate(
        AtmosphericLayerSettings from,
        AtmosphericLayerSettings to,
        float normalizedTime,
        float blend,
        float outdoor)
    {
        if (windTrailEmitter == null || to == null)
            return;

        float fromRate = from != null ? from.EvaluateRate(normalizedTime) : 0f;
        float rate = Mathf.Lerp(fromRate, to.EvaluateRate(normalizedTime), blend) * outdoor;
        Color fromColor = from != null ? from.EvaluateColor(normalizedTime) : Color.clear;
        Color color = Color.Lerp(fromColor, to.EvaluateColor(normalizedTime), blend);
        color.a *= outdoor;
        windTrailEmitter.SetMaxConcurrent(to.MaxParticles);
        windTrailEmitter.SetPresentation(to.Enabled ? rate : 0f, color);

        var emission = streaks.emission;
        emission.enabled = false;
        if (streaks.isPlaying || streaks.particleCount > 0)
            streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ApplyLayerRate(
        ParticleSystem system,
        AtmosphericLayerSettings from,
        AtmosphericLayerSettings to,
        float normalizedTime,
        float blend,
        float outdoor)
    {
        if (system == null)
            return;

        float fromRate = from != null ? from.EvaluateRate(normalizedTime) : 0f;
        float toRate = to != null ? to.EvaluateRate(normalizedTime) : 0f;
        float rate = Mathf.Lerp(fromRate, toRate, blend) * outdoor;
        bool on = rate > 0.01f && to != null && to.Enabled && outdoor > 0.01f;

        var emission = system.emission;
        emission.enabled = on;
        emission.rateOverTime = rate;

        Color fromColor = from != null ? from.EvaluateColor(normalizedTime) : Color.clear;
        Color toColor = to != null ? to.EvaluateColor(normalizedTime) : Color.clear;
        Color color = Color.Lerp(fromColor, toColor, blend);
        color.a *= outdoor;

        var main = system.main;
        main.startColor = color;

        if (!on && outdoor <= 0.01f)
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        else if (on && !system.isPlaying)
            system.Play(true);
    }

    private void ConfigureLayer(ParticleSystem system, AtmosphericLayerSettings layer, AtmosphericProfileSO profile)
    {
        if (system == null || layer == null || profile == null)
            return;

        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = Mathf.Max(0, layer.MaxParticles);
        main.startLifetime = new ParticleSystem.MinMaxCurve(layer.Lifetime.x, layer.Lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(layer.StartSpeed.x, layer.StartSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(layer.StartSize.x, layer.StartSize.y);
        main.startColor = layer.EvaluateColor(0.5f);
        main.gravityModifier = layer.GravityModifier;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;

        var emission = system.emission;
        emission.rateOverDistance = 0f;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = profile.Follow.BoxSize;
        shape.randomDirectionAmount = layer.RenderMode == AtmosphericRenderMode.StretchedBillboard ? 0f : 0.2f;

        Vector3 wind = profile.Wind.EvaluateWorldVelocity();
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = wind.x;
        velocity.y = wind.y;
        velocity.z = wind.z;

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildLifetimeGradient(layer.FadeIn, layer.FadeOut);

        var noise = system.noise;
        bool useNoise = layer.NoiseStrength > 0.001f;
        noise.enabled = useNoise;
        if (useNoise)
        {
            noise.strength = layer.NoiseStrength;
            noise.frequency = profile.Wind.NoiseFrequency;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;
            noise.octaveCount = 2;
        }

        var rotation = system.rotationOverLifetime;
        bool tumble = Mathf.Abs(layer.AngularVelocity.x) > 0.01f || Mathf.Abs(layer.AngularVelocity.y) > 0.01f;
        rotation.enabled = tumble;
        if (tumble)
        {
            // Profile values are authored in familiar degrees/second; Unity's
            // ParticleSystem rotation module stores radians/second.
            rotation.z = new ParticleSystem.MinMaxCurve(
                layer.AngularVelocity.x * Mathf.Deg2Rad,
                layer.AngularVelocity.y * Mathf.Deg2Rad);
            rotation.separateAxes = false;
        }

        var force = system.forceOverLifetime;
        force.enabled = false;

        ConfigureCustomData(system, layer);

        ConfigureTextureSheet(system, layer);
        ConfigureRenderer(system, layer, profile);
    }

    private static void ConfigureCustomData(ParticleSystem system, AtmosphericLayerSettings layer)
    {
        var customData = system.customData;
        bool isWindStreak = layer.RenderMode == AtmosphericRenderMode.StretchedBillboard;
        customData.enabled = isWindStreak;
        if (!isWindStreak)
            return;

        customData.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        customData.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 1);
        customData.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(0f, 1f));
    }

    private static ParticleSystem.MinMaxGradient BuildLifetimeGradient(float fadeIn, float fadeOut)
    {
        float inT = Mathf.Clamp(fadeIn, 0f, 0.45f);
        float outT = Mathf.Clamp(1f - fadeOut, 0.55f, 1f);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, inT),
                new GradientAlphaKey(1f, outT),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private void ConfigureTextureSheet(ParticleSystem system, AtmosphericLayerSettings layer)
    {
        var sheet = system.textureSheetAnimation;
        int frames = layer.SheetColumns * layer.SheetRows;
        bool useSheet = layer.Sprite != null && frames > 1;
        sheet.enabled = useSheet;
        if (!useSheet)
            return;

        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = layer.SheetColumns;
        sheet.numTilesY = layer.SheetRows;
        sheet.animation = layer.AnimateSheet
            ? ParticleSystemAnimationType.WholeSheet
            : ParticleSystemAnimationType.SingleRow;
        sheet.startFrame = layer.RandomSheetFrame
            ? new ParticleSystem.MinMaxCurve(0f, Mathf.Max(0, frames - 1))
            : new ParticleSystem.MinMaxCurve(0f);
        sheet.cycleCount = Mathf.Max(1, Mathf.RoundToInt(layer.SheetCycles));
        sheet.fps = 4f;
    }

    private void ConfigureRenderer(ParticleSystem system, AtmosphericLayerSettings layer, AtmosphericProfileSO profile)
    {
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return;

        bool additive = layer.Blend == AtmosphericParticleBlend.Additive;
        Material source;
        if (system == debris)
        {
            if (debrisInstance == null && alphaInstance != null)
                debrisInstance = new Material(alphaInstance)
                {
                    name = "AtmosphericDebris_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
            source = debrisInstance != null ? debrisInstance : alphaInstance;
        }
        else if (system == streaks)
        {
            Material authoredStreak = streakMaterial != null ? streakMaterial : additiveMaterial;
            if (streakInstance == null && authoredStreak != null)
                streakInstance = new Material(authoredStreak)
                {
                    name = "AtmosphericStreak_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
            source = streakInstance != null ? streakInstance : additiveInstance;
        }
        else
        {
            if (moteInstance == null && additiveInstance != null)
                moteInstance = new Material(additiveInstance)
                {
                    name = "AtmosphericMote_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
            source = moteInstance != null ? moteInstance : additiveInstance;
        }

        if (!additive && source == moteInstance)
            source = alphaInstance;

        if (source != null)
        {
            renderer.sharedMaterial = source;
            source.SetFloat(DepthFadeId, profile.DepthFadeDistance);
            source.SetColor(ParticleColorId, Color.white);
            source.SetFloat(BaseAlphaId, 1f);
            Texture texture = layer.Sprite != null ? layer.Sprite.texture : null;
            if (texture != null)
                source.SetTexture(MainTexId, texture);
        }

        renderer.renderMode = layer.RenderMode == AtmosphericRenderMode.StretchedBillboard
            ? ParticleSystemRenderMode.Stretch
            : ParticleSystemRenderMode.Billboard;
        float aspectScale = 1f;
        if (layer.RenderMode == AtmosphericRenderMode.StretchedBillboard && layer.Sprite != null)
        {
            Rect spriteRect = layer.Sprite.rect;
            float spriteAspect = spriteRect.height > 0.001f ? spriteRect.width / spriteRect.height : 1f;
            aspectScale = Mathf.Max(0.01f, spriteAspect / layer.StretchReferenceAspect);
        }
        renderer.lengthScale = layer.StretchLengthScale * aspectScale;
        renderer.velocityScale = layer.StretchVelocityScale;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.allowRoll = layer.RenderMode != AtmosphericRenderMode.StretchedBillboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = -1f;

        if (system == streaks)
        {
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XYZW
            });
        }
    }

    private void EnsureParticleSystems()
    {
        motes = EnsureChildSystem(motes, "Motes");
        streaks = EnsureChildSystem(streaks, "Streaks");
        debris = EnsureChildSystem(debris, "Debris");
        EnsureWindTrailEmitter();
    }

    private void EnsureWindTrailEmitter()
    {
        if (windTrailEmitter != null || streaks == null)
            return;
        windTrailEmitter = streaks.GetComponent<AtmosphericWindTrailEmitter>();
        if (windTrailEmitter == null)
            windTrailEmitter = streaks.gameObject.AddComponent<AtmosphericWindTrailEmitter>();
    }

    private ParticleSystem EnsureChildSystem(ParticleSystem current, string childName)
    {
        if (current != null)
            return current;

        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            var found = existing.GetComponent<ParticleSystem>();
            if (found != null)
                return found;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        ParticleSystem created = go.AddComponent<ParticleSystem>();
        var main = created.main;
        main.playOnAwake = false;
        created.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return created;
    }

    private void EnsureMaterials()
    {
        if (additiveInstance == null)
            additiveInstance = CreateBlendMaterial(additiveMaterial, true, "AtmosphericMote_Runtime");
        if (alphaInstance == null)
            alphaInstance = CreateBlendMaterial(alphaMaterial, false, "AtmosphericAlpha_Runtime");
    }

    private static Material CreateBlendMaterial(Material authored, bool additive, string instanceName)
    {
        Shader shader = authored != null ? authored.shader : Shader.Find(ShaderName);
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return authored;

        Material material = authored != null ? new Material(authored) : new Material(shader);
        material.name = instanceName;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        material.SetFloat(DstBlendId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        return material;
    }

    private void StopAndClear()
    {
        StopSystem(motes);
        StopSystem(streaks);
        if (windTrailEmitter != null)
            windTrailEmitter.StopAndClear();
        StopSystem(debris);
    }

    private static void StopSystem(ParticleSystem system)
    {
        if (system == null)
            return;
        var emission = system.emission;
        emission.enabled = false;
        if (system.isPlaying || system.particleCount > 0)
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
