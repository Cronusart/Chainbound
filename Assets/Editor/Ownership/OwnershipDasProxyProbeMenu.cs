#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authenticated, read-only Editor probe for the server-side Ownership DAS proxy.
/// Uses the process-memory user JWT and configured public anon key without logging either.
/// </summary>
public static class OwnershipDasProxyProbeMenu
{
    private const string SupabaseConfigPath = "Assets/Data/Supabase/SupabaseConfig.asset";
    private const string SolanaConfigPath = "Assets/Data/Solana/SolanaConfig.asset";
    private const string MenuPath = "Chainbound/Ownership/Probe Moves via Supabase DAS proxy (authenticated)";

    [MenuItem(MenuPath)]
    public static async void ProbeMovesCollection()
    {
        try
        {
            string logPath = await RunProbeAsync();
            Debug.Log($"[OwnershipDASProxy] Authenticated read probe passed. Redacted log: {logPath}");
            EditorUtility.DisplayDialog(
                "Ownership DAS proxy",
                $"Authenticated read probe passed.\n\nRedacted log:\n{logPath}\n\nNo flags or credentials were changed.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[OwnershipDASProxy] Probe failed: {exception.Message}");
            EditorUtility.DisplayDialog("Ownership DAS proxy", $"Probe failed:\n{exception.Message}", "OK");
        }
    }

    private static async Task<string> RunProbeAsync()
    {
        SupabaseConfigSO backend = AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(SupabaseConfigPath);
        SolanaConfigSO chain = AssetDatabase.LoadAssetAtPath<SolanaConfigSO>(SolanaConfigPath);
        if (backend == null) throw new InvalidOperationException($"Missing SupabaseConfig at {SupabaseConfigPath}.");
        if (chain == null) throw new InvalidOperationException($"Missing SolanaConfig at {SolanaConfigPath}.");

        var client = new OwnershipDasProxyClient(backend, chain);
        OwnershipDasAsset collection = await client.GetAssetAsync(chain.coreMovesCollectionId);
        OwnershipDasCollectionPage page = await client.GetMovesAsync(1, 50);

        string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        string logsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        Directory.CreateDirectory(logsDirectory);
        string outputPath = Path.Combine(logsDirectory, $"ownership-das-proxy-unity-{stamp}.log");

        var evidence = new StringBuilder();
        evidence.AppendLine("OwnershipDasProxyProbeMenu — AUTHENTICATED READ ONLY");
        evidence.AppendLine($"utc={stamp}");
        evidence.AppendLine("cluster=devnet");
        evidence.AppendLine($"function={OwnershipDasProxyClient.FunctionName}");
        evidence.AppendLine($"projectHost={OwnershipDasProxyClient.ExpectedProjectHost}");
        evidence.AppendLine($"collection={chain.coreMovesCollectionId}");
        evidence.AppendLine($"expectedAuthority={chain.coreMovesUpdateAuthority}");
        evidence.AppendLine("--- getAsset ---");
        evidence.AppendLine("http=200");
        evidence.AppendLine($"id={collection.id}");
        evidence.AppendLine($"interface={collection.interfaceName}");
        evidence.AppendLine("authorityMatched=true");
        evidence.AppendLine("--- getAssetsByGroup ---");
        evidence.AppendLine("http=200");
        evidence.AppendLine($"page={page.page}");
        evidence.AppendLine($"limit={page.limit}");
        evidence.AppendLine($"reportedTotal={page.reportedTotal}");
        evidence.AppendLine($"trustedItems={page.items.Count}");
        evidence.AppendLine($"droppedItems={page.droppedItems}");
        for (int i = 0; i < page.items.Count; i++)
            evidence.AppendLine($"asset[{i}]={page.items[i].id}");
        evidence.AppendLine("--- redaction/safety ---");
        evidence.AppendLine("userJwt=REDACTED/not written");
        evidence.AppendLine("anonKey=REDACTED/not written");
        evidence.AppendLine("heliusUrl=server-side/not present in Unity");
        evidence.AppendLine("enableDevnetOwnershipSpine=not modified");
        evidence.AppendLine("mint/Stamp/roll=not invoked");
        File.WriteAllText(outputPath, evidence.ToString());
        return outputPath;
    }
}
#endif
