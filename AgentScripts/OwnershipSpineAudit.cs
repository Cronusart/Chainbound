using System;
using System.Collections.Generic;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEngine;

public static class OwnershipSpineAudit
{
    public static string Main()
    {
        var rows = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:SolanaConfigSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<SolanaConfigSO>(path);
            rows.Add(
                $"SOLANA|{path}|program={config.gameProgramId}|rpc={config.rpcEndpoint}" +
                $"|cluster={config.ownershipCluster}|moves={config.coreMovesCollectionId}" +
                $"|movesAuthority={config.coreMovesUpdateAuthority}|schema={config.ownershipSchemaVersion}" +
                $"|dasSet={!string.IsNullOrWhiteSpace(config.dasRpcEndpoint)}" +
                $"|cosmetics={config.coreCosmeticsCollectionId}");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:SupabaseConfigSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(path);
            rows.Add(
                $"SUPABASE|{path}|url={config.supabaseUrl}" +
                $"|functions={config.ResolvedEdgeFunctionBaseUrl}" +
                $"|anonKeySet={!string.IsNullOrWhiteSpace(config.supabaseAnonKey)}");
        }

        foreach (CandyMachineScreen controller in
                 Resources.FindObjectsOfTypeAll<CandyMachineScreen>())
        {
            var serialized = new SerializedObject(controller);
            SerializedProperty gateway = serialized.FindProperty("ownershipGateway");
            rows.Add(
                $"CANDY|scene={controller.gameObject.scene.path}|name={controller.name}" +
                $"|flag={serialized.FindProperty("enableDevnetOwnershipSpine").boolValue}" +
                $"|gateway={(gateway.objectReferenceValue != null ? gateway.objectReferenceValue.GetType().FullName : "null")}" +
                $"|solana={AssetDatabase.GetAssetPath(serialized.FindProperty("solanaConfig").objectReferenceValue)}");
        }

        foreach (SupabaseOwnershipSpineGateway gateway in
                 Resources.FindObjectsOfTypeAll<SupabaseOwnershipSpineGateway>())
        {
            var serialized = new SerializedObject(gateway);
            rows.Add(
                $"GATEWAY|scene={gateway.gameObject.scene.path}|name={gateway.name}" +
                $"|flag={serialized.FindProperty("enableDevnetOwnershipSpine").boolValue}" +
                $"|supabase={AssetDatabase.GetAssetPath(serialized.FindProperty("supabaseConfig").objectReferenceValue)}" +
                $"|solana={AssetDatabase.GetAssetPath(serialized.FindProperty("solanaConfig").objectReferenceValue)}");
        }

        return string.Join("\n", rows);
    }
}
