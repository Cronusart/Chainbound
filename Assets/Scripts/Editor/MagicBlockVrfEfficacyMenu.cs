#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class MagicBlockVrfEfficacyMenu
{
    [MenuItem("Chainbound/Solana/Run MagicBlock VRF Devnet Efficacy")]
    private static async void Run()
    {
        try
        {
            SolanaServiceBridge bridge = UnityEngine.Object.FindFirstObjectByType<SolanaServiceBridge>(
                FindObjectsInactive.Include);
            if (bridge == null || bridge.VrfService == null)
                throw new InvalidOperationException("Open Bootstrap in Play Mode and initialize SolanaServiceBridge first.");
            if (bridge.VrfService is not VrfService)
                throw new InvalidOperationException("Disable useDeterministicVrfInEditor to run the live Devnet efficacy path.");

            byte[] context = Encoding.UTF8.GetBytes($"editor-efficacy:{Guid.NewGuid():N}");
            var timer = Stopwatch.StartNew();
            VrfRequestResult requested = await bridge.VrfService.RequestRandomnessAsync(context);
            Require(requested, VrfRequestStatus.Pending, "request");
            Debug.Log($"[MagicBlock VRF] request PDA={requested.requestId} tx={requested.txSignature}");

            VrfRequestResult fulfilled = await bridge.VrfService.PollRandomnessAsync(
                requested.requestId, pollIntervalMs: 2_000, timeoutMs: 60_000);
            Require(fulfilled, VrfRequestStatus.Fulfilled, "fulfill");
            string randomness = BitConverter.ToString(fulfilled.randomness).Replace("-", string.Empty).ToLowerInvariant();
            Debug.Log($"[MagicBlock VRF] fulfilled PDA={fulfilled.requestId} randomness={randomness} latencyMs={timer.ElapsedMilliseconds}");

            VrfRequestResult consumed = await bridge.VrfService.ConsumeRandomnessAsync(requested.requestId);
            Require(consumed, VrfRequestStatus.Consumed, "consume");
            Debug.Log($"[MagicBlock VRF] consumed PDA={consumed.requestId} tx={consumed.txSignature} totalLatencyMs={timer.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MagicBlock VRF] efficacy failed: {ex.Message}");
        }
    }

    private static void Require(VrfRequestResult result, VrfRequestStatus expected, string phase)
    {
        if (result == null || result.status != expected)
            throw new InvalidOperationException($"{phase} failed: {result?.errorMessage ?? "no result"}");
    }
}
#endif
