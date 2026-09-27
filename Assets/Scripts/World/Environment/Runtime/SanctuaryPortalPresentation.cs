using System.Collections;
using Chainbound.Combat;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Prefab-owned sanctuary visual: drop in from above, hover in a matte hold,
/// then rise while the structure fades into emission and the catalog wake
/// line plays. Latch volumes keep this object hidden until
/// SanctuarySpawnDirector presents a respawn.
/// </summary>
[DisallowMultipleComponent]
public sealed class SanctuaryPortalPresentation : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private MeshRenderer structureRenderer;
    [SerializeField] private MeshRenderer portalRenderer;
    [SerializeField] private SignalGlitchEffect structureGlitch;
    [SerializeField] private SignalGlitchEffect portalGlitch;
    [SerializeField] private Light radiantLight;

    [Header("Hover")]
    [SerializeField] private float hoverAmplitude = 0.22f;
    [SerializeField] private float hoverFrequency = 0.18f;
    [SerializeField] private float swayDegrees = 1.8f;
    [SerializeField] private float arrivalHoldDuration = 1.5f;

    [Header("Departure")]
    [SerializeField] private float riseHeight = 3.2f;
    [SerializeField] private float riseDuration = 1.15f;
    [SerializeField] private float dissolveDuration = 0.85f;
    [SerializeField] private float emissionFadeDuration = 1.25f;

    [Header("Audio")]
    [SerializeField] private SoundEventSO arrivalSound;
    [SerializeField] private SoundEventSO departSound;
    [SerializeField] private float radiantIntensity = 2.15f;

    [Header("Presentation Placement")]
    [Tooltip("World meters behind the player along camera yaw (deeper in the shot).")]
    [SerializeField] private float presentationBehindDistance = 1.75f;

    private Vector3 visualBaseLocalPos;
    private Quaternion visualBaseLocalRot;
    private bool hovering;
    private bool departing;
    private Tween riseTween;
    private Tween lightTween;
    private Tween emissionTween;
    private EmissionPulse structurePulse;
    private float emissionAmount;

    public MeshRenderer PortalRenderer => portalRenderer;
    public SignalGlitchEffect PortalGlitch => portalGlitch;
    public float ArrivalHoldDuration => arrivalHoldDuration;
    public float PresentationBehindDistance => presentationBehindDistance;

    /// <summary>
    /// Places the portal deeper in the gameplay shot than the player, along
    /// camera yaw. Restores nothing — callers should snapshot local pose.
    /// </summary>
    public void PlaceBehindPlayer(Transform player)
    {
        AlignTransformBehindPlayer(
            transform,
            player,
            presentationBehindDistance,
            portalRenderer,
            structureRenderer);
    }

    public static void AlignTransformBehindPlayer(Transform portalRoot, Transform player, float distance)
    {
        AlignTransformBehindPlayer(portalRoot, player, distance, null, null);
    }

    public static void AlignTransformBehindPlayer(
        Transform portalRoot,
        Transform player,
        float distance,
        MeshRenderer portalDisc,
        MeshRenderer structure)
    {
        if (portalRoot == null || player == null)
            return;

        Vector3 deeper = ResolveDeeperInShot(player);
        float gap = Mathf.Max(0f, distance);
        portalRoot.position = player.position;

        if (!TryGetPresentationBounds(portalDisc, structure, out Bounds bounds))
        {
            portalRoot.position = player.position + deeper * gap;
            return;
        }

        // Camera-facing face of the whole visual sits `gap` meters deeper than
        // the player: dog in the foreground, portal behind them in the shot.
        Vector3 absDeeper = new Vector3(Mathf.Abs(deeper.x), Mathf.Abs(deeper.y), Mathf.Abs(deeper.z));
        float nearAlong = Vector3.Dot(bounds.center, deeper) - Vector3.Dot(bounds.extents, absDeeper);
        float targetNear = Vector3.Dot(player.position, deeper) + gap;
        portalRoot.position += deeper * (targetNear - nearAlong);
    }

    private static bool TryGetPresentationBounds(
        MeshRenderer portalDisc,
        MeshRenderer structure,
        out Bounds bounds)
    {
        bounds = default;
        if (structure != null && portalDisc != null)
        {
            bounds = structure.bounds;
            bounds.Encapsulate(portalDisc.bounds);
            return true;
        }

        MeshRenderer single = structure != null ? structure : portalDisc;
        if (single == null)
            return false;

        bounds = single.bounds;
        return true;
    }

    private static Vector3 ResolveDeeperInShot(Transform player)
    {
        Camera camera = SceneCameraResolver.ResolveGameplayCamera();
        if (camera != null)
        {
            Vector3 awayFromCamera = player.position - camera.transform.position;
            awayFromCamera.y = 0f;
            if (awayFromCamera.sqrMagnitude > 0.0001f)
                return awayFromCamera.normalized;
        }

        TopDownCameraController3D controller = SceneCameraResolver.ResolveGameplayCameraController();
        if (controller != null)
        {
            Vector3 yawForward = controller.GetCameraForwardXZ();
            if (yawForward.sqrMagnitude > 0.0001f)
                return yawForward;
        }

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        return playerForward.sqrMagnitude > 0.0001f ? playerForward.normalized : Vector3.forward;
    }

    private void Awake()
    {
        CaptureBasePose();
        ApplyGameplayRenderLayers();
    }

    private void OnEnable()
    {
        CaptureBasePose();
        ResetVisualState();
    }

    private void OnDisable()
    {
        KillMotion();
        hovering = false;
        departing = false;
    }

    private void Update()
    {
        if (!hovering || visualRoot == null || departing)
            return;

        float t = Time.unscaledTime * hoverFrequency * Mathf.PI * 2f;
        float bob = Mathf.Sin(t);
        float eased = Mathf.SmoothStep(0f, 1f, (bob + 1f) * 0.5f);
        float y = (eased - 0.5f) * 2f * hoverAmplitude;
        float sway = Mathf.Sin(t * 0.65f) * swayDegrees;

        visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, y, 0f);
        visualRoot.localRotation = visualBaseLocalRot * Quaternion.Euler(0f, sway, sway * 0.35f);
    }

    public IEnumerator PlayArrival()
    {
        CaptureBasePose();
        ResetVisualState();
        hovering = false;
        departing = false;

        Vector3 rest = visualBaseLocalPos;
        float height = Mathf.Max(0.01f, riseHeight);
        if (visualRoot != null)
            visualRoot.localPosition = rest + Vector3.up * height;

        if (radiantLight != null)
        {
            radiantLight.enabled = true;
            radiantLight.intensity = radiantIntensity;
        }

        if (structureRenderer != null)
            structureRenderer.enabled = true;
        if (portalRenderer != null)
            portalRenderer.enabled = portalGlitch == null;

        ApplyMatteStructure();

        if (portalGlitch != null)
        {
            portalGlitch.Configure(portalRenderer, portalGlitch.GetComponent<MeshRenderer>() != null
                ? portalGlitch.GetComponent<MeshRenderer>().sharedMaterial
                : null);
            if (portalRenderer != null)
                portalRenderer.enabled = false;
            portalGlitch.PlayGlitchLoop();
        }

        PlaySound(arrivalSound);

        if (visualRoot != null)
        {
            float overshoot = Mathf.Max(0.05f, hoverAmplitude);
            float dropSeconds = Mathf.Max(0.05f, riseDuration) * 0.82f;
            float settleSeconds = Mathf.Max(0.05f, riseDuration) * 0.18f;
            riseTween = DOTween.Sequence()
                .Append(
                    visualRoot
                        .DOLocalMove(rest - Vector3.up * overshoot, dropSeconds)
                        .SetEase(Ease.InCubic))
                .Append(
                    visualRoot
                        .DOLocalMove(rest, settleSeconds)
                        .SetEase(Ease.OutQuad))
                .SetUpdate(true);
            yield return WaitForTween(riseTween, dropSeconds + settleSeconds + 2f);
            if (visualRoot != null)
                visualRoot.localPosition = rest;
            riseTween = null;
        }

        hovering = true;
    }

    public IEnumerator PlayDeparture()
    {
        if (departing)
            yield break;

        departing = true;
        hovering = false;
        PlaySound(departSound);
        BeginEmissionFadeIn();

        if (visualRoot != null)
        {
            Vector3 start = visualRoot.position;
            riseTween = visualRoot
                .DOMove(start + Vector3.up * riseHeight, riseDuration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true);
        }

        if (radiantLight != null)
        {
            lightTween = DOTween.To(
                    () => radiantLight.intensity,
                    x => radiantLight.intensity = x,
                    0f,
                    riseDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        // Keep the portal disc riding the structure during the rise. GlitchOut
        // used to shrink it away at hover height before the structure left.
        if (portalGlitch != null && !portalGlitch.IsPlaying)
            portalGlitch.PlayGlitchLoop();

        // Keep the structure mesh visible so the matte → emissive fade can read,
        // then dissolve after the hold-to-depart glow.
        float glowSeconds = Mathf.Max(0.05f, emissionFadeDuration);
        yield return new WaitForSecondsRealtime(glowSeconds);

        bool portalDone = portalGlitch == null;
        if (portalGlitch != null)
            portalGlitch.PlayDissolveOut(dissolveDuration, () => portalDone = true);

        bool structureDone = structureGlitch == null;
        if (structureGlitch != null)
            structureGlitch.PlayDissolveOut(dissolveDuration, () => structureDone = true);

        float dissolveWait = 0f;
        float dissolveTimeout = Mathf.Max(0.05f, dissolveDuration) + 2f;
        while ((!structureDone || !portalDone) && dissolveWait < dissolveTimeout)
        {
            dissolveWait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (riseTween != null && riseTween.IsActive() && !riseTween.IsComplete())
            yield return WaitForTween(riseTween, Mathf.Max(0.05f, riseDuration) + 2f);

        if (structureRenderer != null)
            structureRenderer.enabled = false;
        if (portalRenderer != null)
            portalRenderer.enabled = false;
        if (radiantLight != null)
            radiantLight.enabled = false;

        KillMotion();
        departing = false;
    }

    public void ResetVisualState()
    {
        KillMotion();
        hovering = false;
        departing = false;

        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPos;
            visualRoot.localRotation = visualBaseLocalRot;
        }

        structureGlitch?.StopGlitch();
        portalGlitch?.StopGlitch();
        ApplyMatteStructure();

        if (structureRenderer != null)
            structureRenderer.enabled = true;
        if (portalRenderer != null)
            portalRenderer.enabled = true;
        if (radiantLight != null)
        {
            radiantLight.enabled = true;
            radiantLight.intensity = radiantIntensity;
        }
    }

    private static IEnumerator WaitForTween(Tween tween, float timeoutSeconds)
    {
        float elapsed = 0f;
        timeoutSeconds = Mathf.Max(0.05f, timeoutSeconds);
        while (tween != null && tween.IsActive() && !tween.IsComplete() && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CaptureBasePose()
    {
        if (visualRoot == null)
            visualRoot = transform;
        visualBaseLocalPos = visualRoot.localPosition;
        visualBaseLocalRot = visualRoot.localRotation;
    }

    /// <summary>
    /// Overworld cameras omit Default. Structure renders with buildings;
    /// the portal disc/glitch overlays use Emissive (also in the gameplay mask).
    /// </summary>
    private void ApplyGameplayRenderLayers()
    {
        int buildings = LayerMask.NameToLayer("Buildings");
        int emissive = LayerMask.NameToLayer("Emissive");
        if (buildings < 0 && emissive < 0)
            return;

        if (buildings >= 0)
        {
            SetLayerIfDefault(gameObject, buildings);
            if (visualRoot != null)
                SetLayerIfDefault(visualRoot.gameObject, buildings);
            if (structureRenderer != null)
                SetLayerRecursively(structureRenderer.gameObject, buildings);
            if (radiantLight != null)
                SetLayerIfDefault(radiantLight.gameObject, buildings);
        }

        if (emissive >= 0)
        {
            if (portalRenderer != null)
                SetLayerRecursively(portalRenderer.gameObject, emissive);
            if (structureGlitch != null)
                SetLayerRecursively(structureGlitch.gameObject, emissive);
            if (portalGlitch != null)
                SetLayerRecursively(portalGlitch.gameObject, emissive);
        }
    }

    private static void SetLayerIfDefault(GameObject go, int layer)
    {
        if (go != null && go.layer == 0)
            go.layer = layer;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null || layer < 0)
            return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    private void KillMotion()
    {
        if (riseTween != null && riseTween.IsActive())
            riseTween.Kill();
        riseTween = null;

        if (lightTween != null && lightTween.IsActive())
            lightTween.Kill();
        lightTween = null;

        if (emissionTween != null && emissionTween.IsActive())
            emissionTween.Kill();
        emissionTween = null;
    }

    private EmissionPulse ResolveStructurePulse()
    {
        if (structurePulse != null)
            return structurePulse;

        if (structureRenderer != null)
            structurePulse = structureRenderer.GetComponent<EmissionPulse>();
        if (structurePulse == null)
            structurePulse = GetComponentInChildren<EmissionPulse>(true);
        return structurePulse;
    }

    private void ApplyMatteStructure()
    {
        emissionAmount = 0f;
        EmissionPulse pulse = ResolveStructurePulse();
        pulse?.SetPulseActive(false);
        pulse?.SetEmissionAmount(0f);
    }

    private void BeginEmissionFadeIn()
    {
        EmissionPulse pulse = ResolveStructurePulse();
        if (pulse == null)
            return;

        pulse.SetPulseActive(false);
        emissionAmount = 0f;
        pulse.SetEmissionAmount(0f);

        float duration = Mathf.Max(0.05f, emissionFadeDuration);
        emissionTween = DOTween.To(
                () => emissionAmount,
                x =>
                {
                    emissionAmount = x;
                    pulse.SetEmissionAmount(x);
                },
                1f,
                duration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true);
    }

    private void PlaySound(SoundEventSO sound)
    {
        if (sound == null)
            return;

        AudioService audio = AudioService.Instance;
        if (audio != null)
            audio.Play(sound, transform.position);
    }
}
