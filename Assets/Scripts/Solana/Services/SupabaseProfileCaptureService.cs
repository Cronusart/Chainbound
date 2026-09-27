using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Profile-first wild capture: Supabase dossier + Known Known. No wallet / PDA / NFT.
/// </summary>
public class SupabaseProfileCaptureService : MonoBehaviour, ICaptureService
{
    private const string DefaultKnownMoveId = "baseline_jab";

    [Header("Supabase")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private bool _inProgress;

    public bool IsAttemptInProgress => _inProgress;

    public void Configure(SupabaseConfigSO config)
    {
        if (config != null)
            supabaseConfig = config;
    }

    public async Task<CaptureResult> AttemptCaptureAsync(CaptureParams captureParams)
    {
        if (_inProgress)
        {
            return CaptureResult.FromError("Capture attempt already in progress.");
        }

        if (captureParams == null)
            return CaptureResult.FromError("Capture params were null.");

        string playerProfileId = GameSessionContext.Instance != null
            ? GameSessionContext.Instance.PlayerProfileId
            : "";

        if (string.IsNullOrWhiteSpace(playerProfileId))
            return CaptureResult.FromError("Player profile id is required for capture.");

        if (supabaseConfig == null)
            return CaptureResult.FromError("SupabaseConfigSO was not assigned.");

        string speciesId = captureParams.speciesId;
        if (string.IsNullOrWhiteSpace(speciesId))
            return CaptureResult.FromError("species_id is required.");

        string idempotencyKey = FirstNonEmpty(
            captureParams.idempotencyKey,
            $"wild_capture:{playerProfileId}:{speciesId}:{Guid.NewGuid():N}");

        string knownMoveId = FirstNonEmpty(captureParams.knownMoveId, DefaultKnownMoveId);
        int favorScore = captureParams.favorScore >= 0
            ? captureParams.favorScore
            : FavorRuntime.CurrentScore;

        int[] ivs = captureParams.ivs;
        int stars = captureParams.stars;
        int level = Math.Max(1, captureParams.level);
        bool entangled = captureParams.entangledFlag;
        bool mutated = captureParams.mutatedTypeFlag;
        string visualVariantId = ChainlingVisualIdentity.NormalizeId(
            captureParams.visualVariantId,
            ChainlingVisualIdentity.BaseVariantId);
        string mutationId = mutated
            ? ChainlingVisualIdentity.NormalizeId(captureParams.mutationId, ChainlingVisualIdentity.NoneId)
            : ChainlingVisualIdentity.NoneId;
        string entangledType = entangled
            ? ChainlingTypeParseUtility.ToStorageId(captureParams.entangledType)
            : "none";

        _inProgress = true;
        try
        {
            string requestJson = JsonUtility.ToJson(new CaptureWildRequest
            {
                player_profile_id = playerProfileId,
                species_id = speciesId.Trim(),
                idempotency_key = idempotencyKey,
                ordinal_move_id = knownMoveId,
                current_hp = Math.Max(0, captureParams.currentHP),
                max_hp = Math.Max(1, captureParams.maxHP),
                base_capture_rate = captureParams.baseCaptureRate > 0 ? captureParams.baseCaptureRate : 45,
                status_modifier = captureParams.statusModifier > 0f ? captureParams.statusModifier : 1f,
                device_modifier = captureParams.deviceModifier > 0f ? captureParams.deviceModifier : 1f,
                favor_score = favorScore,
                stars = Math.Max(0, stars),
                level = level,
                ivs = NormalizeIvs(ivs),
                entangled = entangled,
                mutated = mutated,
                visual_variant_id = visualVariantId,
                entangled_type = entangledType,
                mutation_id = mutationId,
            });

            if (enableDebugLogs)
                Debug.Log($"[SupabaseProfileCaptureService] Capturing {speciesId}...");

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                supabaseConfig,
                "profile_capture_wild",
                requestJson);

            if (!response.IsSuccess)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"[SupabaseProfileCaptureService] profile_capture_wild returned {response.statusCode}: {response.body}");
                }

                return CaptureResult.FromError(
                    $"profile_capture_wild returned {response.statusCode}: {response.body}");
            }

            CaptureWildResponse payload = JsonUtility.FromJson<CaptureWildResponse>(response.body);
            if (payload == null)
                return CaptureResult.FromError("profile_capture_wild returned an empty body.");

            if (!string.IsNullOrWhiteSpace(payload.error) && !payload.success)
                return CaptureResult.FromError(payload.error);

            var result = new CaptureResult
            {
                success = payload.success,
                probability = payload.probability,
                roll = payload.roll,
                errorMessage = payload.success
                    ? ""
                    : (string.IsNullOrWhiteSpace(payload.reason) ? "capture_failed" : payload.reason),
                backendChainlingId = payload.chainling_id ?? "",
                grantedKnownMoveId = payload.move_id ?? knownMoveId,
                grantedMoveItemId = payload.move_item_id ?? "",
                isPartyMember = payload.is_party_member,
                partySlot = payload.party_slot,
                alreadyApplied = payload.already_applied,
            };

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[SupabaseProfileCaptureService] result success={result.success} " +
                    $"chainling={result.backendChainlingId} move={result.grantedKnownMoveId} " +
                    $"P={result.probability:F3} roll={result.roll:F3}");
            }

            return result;
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabaseProfileCaptureService] Exception: {ex.Message}");

            return CaptureResult.FromError(ex.Message);
        }
        finally
        {
            _inProgress = false;
        }
    }

    public static SupabaseProfileCaptureService ResolveOrCreate(SupabaseConfigSO config = null)
    {
        SupabaseProfileCaptureService service =
            FindFirstObjectByType<SupabaseProfileCaptureService>(FindObjectsInactive.Include);

        if (service == null)
        {
            var go = new GameObject("[SupabaseProfileCaptureService]");
            DontDestroyOnLoad(go);
            service = go.AddComponent<SupabaseProfileCaptureService>();
        }

        if (config != null)
            service.Configure(config);

        return service;
    }

    private static int[] NormalizeIvs(int[] ivs)
    {
        var result = new int[6];
        if (ivs == null)
            return result;

        for (int i = 0; i < 6 && i < ivs.Length; i++)
            result[i] = ivs[i];
        return result;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }

    [Serializable]
    private class CaptureWildRequest
    {
        public string player_profile_id;
        public string species_id;
        public string idempotency_key;
        public string ordinal_move_id;
        public int current_hp;
        public int max_hp;
        public int base_capture_rate;
        public float status_modifier;
        public float device_modifier;
        public int favor_score;
        public int stars;
        public int level;
        public int[] ivs;
        public bool entangled;
        public bool mutated;
        public string visual_variant_id;
        public string entangled_type;
        public string mutation_id;
    }

    [Serializable]
    private class CaptureWildResponse
    {
        public bool success;
        public bool already_applied;
        public string reason;
        public float probability;
        public float roll;
        public string species_id;
        public string chainling_id;
        public bool is_party_member;
        public int party_slot;
        public string move_item_id;
        public string move_id;
        public string error;
    }
}
