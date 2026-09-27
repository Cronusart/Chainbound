#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Read-only Devnet DAS probe for the trusted Moves collection.
/// Does not enable ownership flags, mint, Stamp, or roll paths.
/// </summary>
public static class MovesCollectionDasProbeMenu
{
    private const string SolanaConfigPath = "Assets/Data/Solana/SolanaConfig.asset";
    private const string MenuPath = "Chainbound/Ownership/Probe Moves Collection (DAS read-only)";

    [MenuItem(MenuPath)]
    public static void ProbeMovesCollection()
    {
        var config = AssetDatabase.LoadAssetAtPath<SolanaConfigSO>(SolanaConfigPath);
        if (config == null)
        {
            EditorUtility.DisplayDialog(
                "Moves DAS probe",
                $"Missing SolanaConfig at {SolanaConfigPath}.",
                "OK");
            return;
        }

        if (!string.Equals(config.ownershipCluster, "devnet", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Moves DAS probe",
                $"Refusing probe: ownershipCluster is '{config.ownershipCluster}' (expected devnet).",
                "OK");
            return;
        }

        var collectionId = (config.coreMovesCollectionId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(collectionId))
        {
            EditorUtility.DisplayDialog("Moves DAS probe", "coreMovesCollectionId is empty.", "OK");
            return;
        }

        var dasUrl = string.IsNullOrWhiteSpace(config.dasRpcEndpoint)
            ? config.rpcEndpoint
            : config.dasRpcEndpoint;
        dasUrl = (dasUrl ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(dasUrl))
        {
            EditorUtility.DisplayDialog("Moves DAS probe", "No DAS/RPC endpoint configured.", "OK");
            return;
        }

        try
        {
            var assetJson = PostJson(dasUrl, BuildGetAsset(collectionId));
            var groupJson = PostJson(dasUrl, BuildGetAssetsByGroup(collectionId));

            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDir);
            var outPath = Path.Combine(logsDir, $"moves-das-editor-probe-{stamp}.log");

            var sb = new StringBuilder();
            sb.AppendLine("MovesCollectionDasProbeMenu — READ ONLY");
            sb.AppendLine($"utc={stamp}");
            sb.AppendLine($"cluster={config.ownershipCluster}");
            sb.AppendLine($"das={dasUrl}");
            sb.AppendLine($"collection={collectionId}");
            sb.AppendLine($"expectedAuthority={config.coreMovesUpdateAuthority}");
            sb.AppendLine("--- getAsset ---");
            sb.AppendLine(assetJson);
            sb.AppendLine("--- getAssetsByGroup ---");
            sb.AppendLine(groupJson);
            sb.AppendLine("--- safety ---");
            sb.AppendLine("enableDevnetOwnershipSpine: not modified");
            sb.AppendLine("mint/Stamp/roll: not invoked");
            File.WriteAllText(outPath, sb.ToString());

            Debug.Log($"[MovesDAS] Probe complete. Log: {outPath}\ngetAsset snippet: {Trim(assetJson, 400)}\ngetAssetsByGroup snippet: {Trim(groupJson, 400)}");
            EditorUtility.DisplayDialog(
                "Moves DAS probe",
                $"Read-only probe finished.\n\nCollection: {collectionId}\nLog:\n{outPath}\n\nFlags were not changed.",
                "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MovesDAS] Probe failed: {ex.Message}");
            EditorUtility.DisplayDialog("Moves DAS probe", $"Probe failed:\n{ex.Message}", "OK");
        }
    }

    private static string BuildGetAsset(string id)
    {
        return "{\"jsonrpc\":\"2.0\",\"id\":\"moves-getAsset\",\"method\":\"getAsset\",\"params\":{\"id\":\"" +
               Escape(id) + "\"}}";
    }

    private static string BuildGetAssetsByGroup(string collectionId)
    {
        return "{\"jsonrpc\":\"2.0\",\"id\":\"moves-getAssetsByGroup\",\"method\":\"getAssetsByGroup\",\"params\":{" +
               "\"groupKey\":\"collection\",\"groupValue\":\"" + Escape(collectionId) + "\",\"page\":1,\"limit\":20}}";
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string PostJson(string url, string body)
    {
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            var raw = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 30;
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                // Editor menu: brief blocking wait for a single diagnostic call.
            }

#if UNITY_2020_1_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                throw new InvalidOperationException($"HTTP {req.responseCode}: {req.error}");
            }

            return req.downloadHandler.text ?? string.Empty;
        }
    }

    private static string Trim(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        value = value.Replace('\n', ' ');
        return value.Length <= max ? value : value.Substring(0, max) + "…";
    }
}
#endif
