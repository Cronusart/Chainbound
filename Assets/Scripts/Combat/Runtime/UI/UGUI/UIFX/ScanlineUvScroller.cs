using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScanlineUvScroller : MonoBehaviour
{
    [SerializeField] private RawImage rawImage;
    [SerializeField] private float speedY = 0.08f; // UV units per second
    [SerializeField] private bool ignoreTimeScale = true;

    private void Reset()
    {
        rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        if (!rawImage || rawImage.texture == null) return;

        float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
        Rect uv = rawImage.uvRect;
        uv.y = Mathf.Repeat(uv.y + speedY * dt, 1f);
        rawImage.uvRect = uv;
    }
}