using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OwnershipSpineWire
{
    private const string SolanaConfigPath = "Assets/Data/Solana/SolanaConfig.asset";
    private const string SupabaseConfigPath = "Assets/Resources/SupabaseConfig.asset";
    private const string CoreAuthority = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";

    public static string Main()
    {
        SolanaConfigSO solanaConfig =
            AssetDatabase.LoadAssetAtPath<SolanaConfigSO>(SolanaConfigPath);
        SupabaseConfigSO supabaseConfig =
            AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(SupabaseConfigPath);
        if (solanaConfig == null || supabaseConfig == null)
            return "Missing Solana or Supabase config asset.";

        solanaConfig.ownershipCluster = "devnet";
        solanaConfig.coreMovesUpdateAuthority = CoreAuthority;
        solanaConfig.ownershipSchemaVersion = 1;
        EditorUtility.SetDirty(solanaConfig);

        GameObject serviceObject = GameObject.Find("/GameServices/SolanaServices");
        CandyMachineScreen controller =
            Object.FindFirstObjectByType<CandyMachineScreen>(FindObjectsInactive.Include);
        if (serviceObject == null || controller == null)
            return "Bootstrap SolanaServices or CandyMachineScreen was not found.";

        SupabaseOwnershipSpineGateway gateway =
            serviceObject.GetComponent<SupabaseOwnershipSpineGateway>();
        if (gateway == null)
            gateway = Undo.AddComponent<SupabaseOwnershipSpineGateway>(serviceObject);

        var gatewaySerialized = new SerializedObject(gateway);
        gatewaySerialized.FindProperty("supabaseConfig").objectReferenceValue = supabaseConfig;
        gatewaySerialized.FindProperty("solanaConfig").objectReferenceValue = solanaConfig;
        gatewaySerialized.FindProperty("enableDevnetOwnershipSpine").boolValue = false;
        gatewaySerialized.ApplyModifiedPropertiesWithoutUndo();

        var controllerSerialized = new SerializedObject(controller);
        controllerSerialized.FindProperty("ownershipGateway").objectReferenceValue = gateway;
        controllerSerialized.FindProperty("solanaConfig").objectReferenceValue = solanaConfig;
        controllerSerialized.FindProperty("enableDevnetOwnershipSpine").boolValue = false;
        controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(gateway);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        return "Ownership Spine references wired; controller and gateway flags remain OFF.";
    }

    public static string ApplyCollection(string movesCollectionAddress)
    {
        if (string.IsNullOrWhiteSpace(movesCollectionAddress))
            return "Moves collection address is required.";

        SolanaConfigSO solanaConfig =
            AssetDatabase.LoadAssetAtPath<SolanaConfigSO>(SolanaConfigPath);
        if (solanaConfig == null)
            return "Solana config asset was not found.";

        solanaConfig.coreMovesCollectionId = movesCollectionAddress.Trim();
        solanaConfig.coreMovesUpdateAuthority = CoreAuthority;
        EditorUtility.SetDirty(solanaConfig);
        AssetDatabase.SaveAssets();
        return $"Trusted Moves collection set to {solanaConfig.coreMovesCollectionId}; flags unchanged.";
    }
}
