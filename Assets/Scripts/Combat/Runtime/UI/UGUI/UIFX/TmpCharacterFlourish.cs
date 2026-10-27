using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Typewriter reveal with a per-character pop, a short idle wave, and a staggered exit.
/// Mesh offsets are reapplied after each TMP rebuild so the glyphs stay on the authored layout.
/// </summary>
[DisallowMultipleComponent]
public sealed class TmpCharacterFlourish : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField, Min(1f)] private float charactersPerSecond = 22f;
    [SerializeField, Min(0.05f)] private float popSeconds = 0.2f;
    [SerializeField, Min(1f)] private float popOvershoot = 1.15f;
    [SerializeField, Min(0f)] private float waveAmplitude = 1.5f;
    [SerializeField, Min(0f)] private float waveFrequency = 2.2f;
    [SerializeField, Min(0.01f)] private float exitSeconds = 0.22f;
    [SerializeField, Min(0f)] private float exitStagger = 0.018f;
    [SerializeField, Min(0f)] private float exitRise = 8f;

    private enum Phase
    {
        Idle,
        Revealing,
        Holding,
        Exiting,
    }

    private Phase phase = Phase.Idle;
    private float[] bornAt = System.Array.Empty<float>();
    private int revealed;
    private float exitStartedAt;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
    }

    private void OnDisable()
    {
        phase = Phase.Idle;
    }

    public void PrepareHidden()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
        if (text == null)
            return;

        phase = Phase.Idle;
        revealed = 0;
        text.maxVisibleCharacters = 0;
        enabled = false;
    }

    public IEnumerator PlayReveal()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
        if (text == null)
            yield break;

        enabled = true;
        phase = Phase.Revealing;
        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();
        int total = text.textInfo != null ? text.textInfo.characterCount : 0;
        bornAt = new float[total];
        for (int i = 0; i < total; i++)
            bornAt[i] = -1f;
        revealed = 0;

        float nextAt = Time.unscaledTime;
        float step = 1f / Mathf.Max(1f, charactersPerSecond);
        while (phase == Phase.Revealing && revealed < total)
        {
            if (Time.unscaledTime >= nextAt)
            {
                bornAt[revealed] = Time.unscaledTime;
                revealed++;
                text.maxVisibleCharacters = revealed;
                char revealedChar = text.textInfo.characterInfo[revealed - 1].character;
                float pause = revealedChar == '!' || revealedChar == '?' || revealedChar == '.'
                    ? 0.07f
                    : 0f;
                nextAt = Time.unscaledTime + step + pause;
            }

            yield return null;
        }

        float settleUntil = Time.unscaledTime + popSeconds;
        while (phase == Phase.Revealing && Time.unscaledTime < settleUntil)
            yield return null;

        if (phase == Phase.Revealing)
            phase = Phase.Holding;
    }

    public IEnumerator PlayExit()
    {
        if (text == null)
            yield break;

        enabled = true;
        if (bornAt == null || bornAt.Length == 0)
        {
            text.ForceMeshUpdate();
            int total = text.textInfo != null ? text.textInfo.characterCount : 0;
            bornAt = new float[total];
            float now = Time.unscaledTime;
            for (int i = 0; i < total; i++)
                bornAt[i] = now - popSeconds;
            revealed = total;
            text.maxVisibleCharacters = int.MaxValue;
        }

        phase = Phase.Exiting;
        exitStartedAt = Time.unscaledTime;
        float duration = exitSeconds + Mathf.Max(0, revealed - 1) * exitStagger;
        while (Time.unscaledTime < exitStartedAt + duration)
            yield return null;

        phase = Phase.Idle;
        enabled = false;
    }

    public void StopMotion(bool restoreMesh)
    {
        phase = Phase.Idle;
        enabled = false;
        if (text == null || !restoreMesh)
            return;

        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();
    }

    private void LateUpdate()
    {
        if (phase == Phase.Idle || text == null)
            return;

        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        if (info == null)
            return;

        float now = Time.unscaledTime;
        int count = Mathf.Min(revealed, info.characterCount);
        for (int i = 0; i < count; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible)
                continue;

            float scale = 1f;
            float rise = 0f;
            float born = i < bornAt.Length ? bornAt[i] : -1f;
            if (phase == Phase.Exiting)
            {
                float exitAge = now - exitStartedAt - (i * exitStagger);
                float t = Mathf.Clamp01(exitAge / Mathf.Max(0.01f, exitSeconds));
                float eased = t * t;
                scale = 1f - eased;
                rise = eased * exitRise;
            }
            else if (born < 0f)
            {
                scale = 0f;
            }
            else
            {
                float age = now - born;
                float t = Mathf.Clamp01(age / Mathf.Max(0.01f, popSeconds));
                scale = EaseOutBack(t, popOvershoot);
                if (t >= 1f)
                    rise = Mathf.Sin((now * waveFrequency) + (i * 0.45f)) * waveAmplitude;
            }

            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            Vector3[] vertices = info.meshInfo[materialIndex].vertices;
            Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
            for (int v = 0; v < 4; v++)
            {
                Vector3 source = vertices[vertexIndex + v];
                vertices[vertexIndex + v] = center + ((source - center) * scale) + new Vector3(0f, rise, 0f);
            }
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot - 1f);
        float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + (c3 * p * p * p) + (c1 * p * p);
    }
}
