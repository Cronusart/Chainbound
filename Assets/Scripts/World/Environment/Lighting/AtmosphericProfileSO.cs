using UnityEngine;

public enum AtmosphericParticleBlend
{
    Alpha,
    Additive
}

public enum AtmosphericRenderMode
{
    Billboard,
    StretchedBillboard
}

[System.Serializable]
public sealed class AtmosphericTrailSettings
{
    [SerializeField, Min(0.05f)] private float trailTime = 1.35f;
    [SerializeField, Min(0.01f)] private float minVertexDistance = 0.16f;
    [SerializeField] private Vector2 waveAmplitude = new Vector2(0.18f, 0.48f);
    [SerializeField] private Vector2 waveFrequency = new Vector2(1.1f, 2.1f);
    [SerializeField, Min(0.01f)] private float minimumSpeed = 2.2f;
    [SerializeField, Min(0.01f)] private float speedMultiplier = 5.5f;

    public float TrailTime => trailTime;
    public float MinVertexDistance => minVertexDistance;
    public Vector2 WaveAmplitude => waveAmplitude;
    public Vector2 WaveFrequency => waveFrequency;
    public float MinimumSpeed => minimumSpeed;
    public float SpeedMultiplier => speedMultiplier;
}

[System.Serializable]
public sealed class AtmosphericFollowSettings
{
    [Tooltip("Placement relative to the undamped player anchor. X follows camera-right, Y is world-up, and Z is how far the volume extends beyond the player into the view.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.5f, 2f);

    [Tooltip("Emitter box size at the reference zoom (width, height, depth).")]
    [SerializeField] private Vector3 boxSize = new Vector3(18f, 12f, 42f);

    [Tooltip("Perspective FOV at which Box Size is used without scaling.")]
    [SerializeField, Min(1f)] private float referenceFieldOfView = 60f;

    [Tooltip("Orthographic size at which Box Size is used without scaling.")]
    [SerializeField, Min(0.1f)] private float referenceOrthographicSize = 7f;

    [Tooltip("Maximum box expansion allowed while the camera zooms out.")]
    [SerializeField, Min(1f)] private float maximumZoomScale = 1.8f;

    public Vector3 LocalOffset => localOffset;
    public Vector3 BoxSize => boxSize;
    public float ReferenceFieldOfView => referenceFieldOfView;
    public float ReferenceOrthographicSize => referenceOrthographicSize;
    public float MaximumZoomScale => maximumZoomScale;

    public void CopyFrom(AtmosphericFollowSettings other)
    {
        if (other == null)
            return;
        localOffset = other.localOffset;
        boxSize = other.boxSize;
        referenceFieldOfView = other.referenceFieldOfView;
        referenceOrthographicSize = other.referenceOrthographicSize;
        maximumZoomScale = other.maximumZoomScale;
    }

    public void ApplyForestPreset()
    {
        localOffset = new Vector3(0f, 1.5f, 2f);
        boxSize = new Vector3(18f, 12f, 42f);
        referenceFieldOfView = 60f;
        referenceOrthographicSize = 7f;
        maximumZoomScale = 1.8f;
    }
}

[System.Serializable]
public sealed class AtmosphericWindSettings
{
    [Tooltip("Wind direction. When Align To Camera Yaw is enabled, X is screen-right, Y is world-up, and Z is into the view.")]
    [SerializeField] private Vector3 direction = new Vector3(0.85f, 0.08f, 0.2f);

    [SerializeField, Min(0f)] private float speed = 0.55f;
    [Tooltip("Rotate horizontal wind with the gameplay camera yaw so streaks keep a consistent screen-space direction through camera zones.")]
    [SerializeField] private bool alignToCameraYaw = true;
    [SerializeField, Range(0f, 2f)] private float noiseStrength = 0.18f;
    [SerializeField, Min(0.01f)] private float noiseFrequency = 0.28f;
    [SerializeField, Range(0f, 1f)] private float verticalBias = 0.08f;

    public Vector3 Direction => direction;
    public float Speed => speed;
    public bool AlignToCameraYaw => alignToCameraYaw;
    public float NoiseStrength => noiseStrength;
    public float NoiseFrequency => noiseFrequency;
    public float VerticalBias => verticalBias;

    public Vector3 EvaluateWorldVelocity()
    {
        Vector3 dir = direction;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;
        dir.Normalize();
        dir.y += verticalBias;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;
        return dir.normalized * speed;
    }

    public void CopyFrom(AtmosphericWindSettings other)
    {
        if (other == null)
            return;
        direction = other.direction;
        speed = other.speed;
        alignToCameraYaw = other.alignToCameraYaw;
        noiseStrength = other.noiseStrength;
        noiseFrequency = other.noiseFrequency;
        verticalBias = other.verticalBias;
    }

    public void ApplyForestPreset()
    {
        direction = new Vector3(0.85f, 0.08f, 0.2f);
        speed = 0.55f;
        alignToCameraYaw = true;
        noiseStrength = 0.18f;
        noiseFrequency = 0.28f;
        verticalBias = 0.08f;
    }
}

[System.Serializable]
public sealed class AtmosphericLayerSettings
{
    [SerializeField] private bool enabled = true;
    [SerializeField, Min(0)] private int maxParticles = 24;
    [Tooltip("Emission rate versus normalized time of day (0 midnight, 0.5 noon).")]
    [SerializeField] private AnimationCurve rateOverDay = AnimationCurve.Linear(0f, 8f, 1f, 8f);
    [SerializeField] private Vector2 lifetime = new Vector2(4f, 8f);
    [SerializeField] private Vector2 startSize = new Vector2(0.03f, 0.07f);
    [SerializeField] private Vector2 startSpeed = new Vector2(0.04f, 0.12f);
    [SerializeField] private Gradient colorOverDay = new Gradient();
    [SerializeField, Range(0f, 0.5f)] private float fadeIn = 0.18f;
    [SerializeField, Range(0f, 0.5f)] private float fadeOut = 0.22f;
    [SerializeField] private AtmosphericParticleBlend blend = AtmosphericParticleBlend.Additive;
    [SerializeField] private AtmosphericRenderMode renderMode = AtmosphericRenderMode.Billboard;
    [SerializeField, Min(0f)] private float stretchLengthScale = 2.4f;
    [SerializeField, Min(0f)] private float stretchVelocityScale = 0.35f;
    [Tooltip("Sprite aspect ratio that corresponds to the authored Stretch Length Scale. The original streak art was 8:1.")]
    [SerializeField, Min(0.01f)] private float stretchReferenceAspect = 8f;
    [SerializeField, Range(0f, 2f)] private float noiseStrength = 0.12f;
    [SerializeField] private float gravityModifier;
    [SerializeField] private Vector2 angularVelocity = Vector2.zero;
    [SerializeField] private Sprite sprite;
    [SerializeField, Min(1)] private int sheetColumns = 1;
    [SerializeField, Min(1)] private int sheetRows = 1;
    [SerializeField] private bool randomSheetFrame = true;
    [SerializeField] private bool animateSheet;
    [SerializeField, Min(0.1f)] private float sheetCycles = 1f;

    public bool Enabled => enabled;
    public int MaxParticles => maxParticles;
    public AnimationCurve RateOverDay => rateOverDay;
    public Vector2 Lifetime => lifetime;
    public Vector2 StartSize => startSize;
    public Vector2 StartSpeed => startSpeed;
    public Gradient ColorOverDay => colorOverDay;
    public float FadeIn => fadeIn;
    public float FadeOut => fadeOut;
    public AtmosphericParticleBlend Blend => blend;
    public AtmosphericRenderMode RenderMode => renderMode;
    public float StretchLengthScale => stretchLengthScale;
    public float StretchVelocityScale => stretchVelocityScale;
    public float StretchReferenceAspect => stretchReferenceAspect > 0.01f ? stretchReferenceAspect : 8f;
    public float NoiseStrength => noiseStrength;
    public float GravityModifier => gravityModifier;
    public Vector2 AngularVelocity => angularVelocity;
    public Sprite Sprite => sprite;
    public int SheetColumns => Mathf.Max(1, sheetColumns);
    public int SheetRows => Mathf.Max(1, sheetRows);
    public bool RandomSheetFrame => randomSheetFrame;
    public bool AnimateSheet => animateSheet;
    public float SheetCycles => sheetCycles;

    public float EvaluateRate(float normalizedTime)
    {
        if (!enabled || rateOverDay == null)
            return 0f;
        return Mathf.Max(0f, rateOverDay.Evaluate(Mathf.Repeat(normalizedTime, 1f)));
    }

    public Color EvaluateColor(float normalizedTime)
    {
        if (colorOverDay == null)
            return Color.white;
        return colorOverDay.Evaluate(Mathf.Repeat(normalizedTime, 1f));
    }

    public void SetSprite(Sprite value)
    {
        sprite = value;
    }

    public void CopyFrom(AtmosphericLayerSettings other)
    {
        if (other == null)
            return;
        enabled = other.enabled;
        maxParticles = other.maxParticles;
        rateOverDay = CloneCurve(other.rateOverDay);
        lifetime = other.lifetime;
        startSize = other.startSize;
        startSpeed = other.startSpeed;
        colorOverDay = CloneGradient(other.colorOverDay);
        fadeIn = other.fadeIn;
        fadeOut = other.fadeOut;
        blend = other.blend;
        renderMode = other.renderMode;
        stretchLengthScale = other.stretchLengthScale;
        stretchVelocityScale = other.stretchVelocityScale;
        stretchReferenceAspect = other.stretchReferenceAspect;
        noiseStrength = other.noiseStrength;
        gravityModifier = other.gravityModifier;
        angularVelocity = other.angularVelocity;
        sprite = other.sprite;
        sheetColumns = other.sheetColumns;
        sheetRows = other.sheetRows;
        randomSheetFrame = other.randomSheetFrame;
        animateSheet = other.animateSheet;
        sheetCycles = other.sheetCycles;
    }

    public void ConfigureMotesForest()
    {
        enabled = true;
        maxParticles = 36;
        rateOverDay = NightHeavyRate(5f, 14f);
        lifetime = new Vector2(5f, 10f);
        startSize = new Vector2(0.025f, 0.065f);
        startSpeed = new Vector2(0.02f, 0.08f);
        colorOverDay = NightWarmMoteGradient();
        fadeIn = 0.2f;
        fadeOut = 0.25f;
        blend = AtmosphericParticleBlend.Additive;
        renderMode = AtmosphericRenderMode.Billboard;
        stretchLengthScale = 1f;
        stretchVelocityScale = 0f;
        stretchReferenceAspect = 8f;
        noiseStrength = 0.16f;
        gravityModifier = 0f;
        angularVelocity = Vector2.zero;
        sheetColumns = 1;
        sheetRows = 1;
        randomSheetFrame = false;
        animateSheet = false;
    }

    public void ConfigureStreaksForest()
    {
        enabled = true;
        maxParticles = 10;
        rateOverDay = NightHeavyRate(0.8f, 2.2f);
        lifetime = new Vector2(3.5f, 6f);
        startSize = new Vector2(0.012f, 0.028f);
        startSpeed = new Vector2(0.12f, 0.3f);
        colorOverDay = StreakGradient();
        fadeIn = 0.12f;
        fadeOut = 0.35f;
        blend = AtmosphericParticleBlend.Additive;
        renderMode = AtmosphericRenderMode.StretchedBillboard;
        stretchLengthScale = 3.2f;
        stretchVelocityScale = 0.45f;
        stretchReferenceAspect = 8f;
        noiseStrength = 0.08f;
        gravityModifier = 0f;
        angularVelocity = Vector2.zero;
        sheetColumns = 1;
        sheetRows = 1;
        randomSheetFrame = false;
        animateSheet = false;
    }

    public void ConfigureSparseWindTrailCadence()
    {
        enabled = true;
        maxParticles = 4;
        rateOverDay = new AnimationCurve(
            new Keyframe(0f, 0.48f),
            new Keyframe(0.22f, 0.4f),
            new Keyframe(0.5f, 0.32f),
            new Keyframe(0.78f, 0.4f),
            new Keyframe(1f, 0.48f));
        lifetime = new Vector2(5.5f, 8.5f);
    }

    public void ConfigureDebrisLeaves(bool jungle)
    {
        enabled = true;
        maxParticles = jungle ? 8 : 5;
        rateOverDay = DayHeavyRate(jungle ? 0.9f : 0.35f, jungle ? 0.25f : 0.08f);
        lifetime = new Vector2(6f, 12f);
        startSize = jungle ? new Vector2(0.14f, 0.22f) : new Vector2(0.11f, 0.18f);
        startSpeed = new Vector2(0.12f, 0.28f);
        colorOverDay = LeafTintGradient();
        fadeIn = 0.15f;
        fadeOut = 0.28f;
        blend = AtmosphericParticleBlend.Alpha;
        renderMode = AtmosphericRenderMode.Billboard;
        stretchLengthScale = 1f;
        stretchVelocityScale = 0f;
        stretchReferenceAspect = 8f;
        noiseStrength = 0.22f;
        gravityModifier = 0.018f;
        angularVelocity = new Vector2(-80f, 80f);
        sheetColumns = 2;
        sheetRows = 1;
        randomSheetFrame = true;
        animateSheet = true;
        sheetCycles = 1.2f;
    }

    public void ConfigureGentleLeafMotion()
    {
        lifetime = new Vector2(7f, 13f);
        startSpeed = new Vector2(0.08f, 0.2f);
        noiseStrength = 0.3f;
        gravityModifier = 0.01f;
        angularVelocity = new Vector2(-12f, 12f);
    }

    public void DisableLayer()
    {
        enabled = false;
        maxParticles = 0;
        rateOverDay = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    }

    private static AnimationCurve CloneCurve(AnimationCurve source)
    {
        return source != null ? new AnimationCurve(source.keys) : AnimationCurve.Linear(0f, 0f, 1f, 0f);
    }

    private static Gradient CloneGradient(Gradient source)
    {
        var clone = new Gradient();
        if (source != null)
            clone.SetKeys(source.colorKeys, source.alphaKeys);
        return clone;
    }

    private static AnimationCurve NightHeavyRate(float dayRate, float nightRate)
    {
        return new AnimationCurve(
            new Keyframe(0f, nightRate),
            new Keyframe(0.22f, nightRate * 0.7f),
            new Keyframe(0.5f, dayRate),
            new Keyframe(0.78f, nightRate * 0.75f),
            new Keyframe(1f, nightRate));
    }

    private static AnimationCurve DayHeavyRate(float dayRate, float nightRate)
    {
        return new AnimationCurve(
            new Keyframe(0f, nightRate),
            new Keyframe(0.25f, nightRate * 1.4f),
            new Keyframe(0.5f, dayRate),
            new Keyframe(0.75f, nightRate * 1.5f),
            new Keyframe(1f, nightRate));
    }

    private static Gradient NightWarmMoteGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.35f, 1.05f, 0.55f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.95f, 0.75f, 1f), 0.5f),
                new GradientColorKey(new Color(1.2f, 0.95f, 0.5f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0.85f, 1f)
            });
        return gradient;
    }

    private static Gradient StreakGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.1f, 1.15f, 1.2f, 1f), 0f),
                new GradientColorKey(new Color(1f, 1f, 1f, 1f), 0.5f),
                new GradientColorKey(new Color(1.05f, 1.1f, 1.2f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.55f, 0f),
                new GradientAlphaKey(0.22f, 0.5f),
                new GradientAlphaKey(0.5f, 1f)
            });
        return gradient;
    }

    private static Gradient LeafTintGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.62f, 0.28f, 1f), 0f),
                new GradientColorKey(new Color(0.62f, 0.78f, 0.32f, 1f), 0.5f),
                new GradientColorKey(new Color(0.5f, 0.58f, 0.26f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0.9f, 1f)
            });
        return gradient;
    }
}

[CreateAssetMenu(fileName = "AtmosphericProfile", menuName = "Chainbound/World/Environment/Atmospheric Profile")]
public sealed class AtmosphericProfileSO : ScriptableObject
{
    [SerializeField] private string profileId = "forest";
    [SerializeField, Min(0.05f)] private float depthFadeDistance = 0.45f;
    [SerializeField, Min(0f)] private float indoorFadeSeconds = 0.6f;
    [SerializeField] private AtmosphericFollowSettings follow = new AtmosphericFollowSettings();
    [SerializeField] private AtmosphericWindSettings wind = new AtmosphericWindSettings();
    [SerializeField] private AtmosphericLayerSettings motes = new AtmosphericLayerSettings();
    [SerializeField] private AtmosphericLayerSettings streaks = new AtmosphericLayerSettings();
    [SerializeField] private AtmosphericTrailSettings windTrails = new AtmosphericTrailSettings();
    [SerializeField] private AtmosphericLayerSettings debris = new AtmosphericLayerSettings();

    public string ProfileId => profileId;
    public float DepthFadeDistance => Mathf.Max(0.05f, depthFadeDistance);
    public float IndoorFadeSeconds => indoorFadeSeconds;
    public AtmosphericFollowSettings Follow => follow;
    public AtmosphericWindSettings Wind => wind;
    public AtmosphericLayerSettings Motes => motes;
    public AtmosphericLayerSettings Streaks => streaks;
    public AtmosphericTrailSettings WindTrails => windTrails;
    public AtmosphericLayerSettings Debris => debris;

    public void ApplyForestPreset()
    {
        profileId = "forest";
        depthFadeDistance = 0.45f;
        indoorFadeSeconds = 0.6f;
        follow.ApplyForestPreset();
        wind.ApplyForestPreset();
        motes.ConfigureMotesForest();
        streaks.ConfigureStreaksForest();
        debris.ConfigureDebrisLeaves(jungle: false);
    }

    public void AssignSprites(Sprite moteSprite, Sprite streakSprite, Sprite debrisSprite)
    {
        motes.SetSprite(moteSprite);
        streaks.SetSprite(streakSprite);
        debris.SetSprite(debrisSprite);
    }

    public void ApplyJunglePreset()
    {
        ApplyForestPreset();
        profileId = "jungle";
        debris.ConfigureDebrisLeaves(jungle: true);
    }

    public void ApplyCurrentMotionTuning()
    {
        streaks.ConfigureSparseWindTrailCadence();
        debris.ConfigureGentleLeafMotion();
    }

#if UNITY_EDITOR
    [ContextMenu("Apply Forest Preset")]
    private void EditorApplyForestPreset()
    {
        ApplyForestPreset();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Apply Jungle Preset")]
    private void EditorApplyJunglePreset()
    {
        ApplyJunglePreset();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
