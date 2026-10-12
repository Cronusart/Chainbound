using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Overhead glyph shown during the encounter lead-in. It matches the interact
/// prompt's offset and quad, but it is not an interactable and ignores input locks.
/// </summary>
[DisallowMultipleComponent]
public sealed class EncounterLeadInCue : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float scale = 5f;
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
    [SerializeField] private Vector2 quadSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private MeshQuadImage bubbleMesh;
    [SerializeField] private Sprite sprite;

    private Camera mainCam;
    private MeshRenderer meshRenderer;
    private float alpha;
    private float targetAlpha;
    private bool meshPrepared;

    public bool IsShown { get; private set; }

    public static EncounterLeadInCue FindOnPlayer(GameObject playerRoot)
    {
        if (playerRoot == null)
            return null;

        return playerRoot.GetComponentInChildren<EncounterLeadInCue>(true);
    }

    private void Awake()
    {
        EnsureBubble();
        alpha = 0f;
        targetAlpha = 0f;
        IsShown = false;
        ApplyAlpha(0f);
        SetRendererEnabled(false);
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        EnsureBubble();
        IsShown = true;
        targetAlpha = 1f;
        SetRendererEnabled(true);
        Place();
    }

    public void HideImmediate()
    {
        IsShown = false;
        targetAlpha = 0f;
        alpha = 0f;
        ApplyAlpha(0f);
        SetRendererEnabled(false);
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!IsShown && alpha <= 0.001f)
            return;

        Place();

        float difference = targetAlpha - alpha;
        if (Mathf.Abs(difference) > 0.01f)
        {
            float step = (1f / Mathf.Max(0.01f, fadeDuration)) * Time.deltaTime;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, step);
            ApplyAlpha(alpha);
        }
    }

    private void EnsureBubble()
    {
        if (bubbleMesh == null)
            bubbleMesh = GetComponent<MeshQuadImage>();
        if (bubbleMesh == null)
        {
            if (GetComponent<MeshFilter>() == null)
                gameObject.AddComponent<MeshFilter>();
            if (GetComponent<MeshRenderer>() == null)
                gameObject.AddComponent<MeshRenderer>();
            bubbleMesh = gameObject.AddComponent<MeshQuadImage>();
        }

        meshRenderer = bubbleMesh.GetComponent<MeshRenderer>();
        if (!meshPrepared)
        {
            if (Application.isPlaying)
                bubbleMesh.SetSize(quadSize);
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }

            meshPrepared = true;
        }

        if (sprite != null)
            bubbleMesh.SetSprite(sprite);
    }

    private void Place()
    {
        if (!SceneCameraResolver.IsUsableFor(mainCam, gameObject))
            mainCam = SceneCameraResolver.ResolveGameplayCamera();
        if (mainCam == null || transform.parent == null)
            return;

        transform.position = transform.parent.TransformPoint(offset);
        transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
        transform.localScale = Vector3.Scale(Vector3.one * scale, scaleMultiplier);
    }

    private void ApplyAlpha(float value)
    {
        if (bubbleMesh != null)
            bubbleMesh.SetAlpha(value);
    }

    private void SetRendererEnabled(bool enabled)
    {
        if (meshRenderer == null && bubbleMesh != null)
            meshRenderer = bubbleMesh.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = enabled;
    }
}
