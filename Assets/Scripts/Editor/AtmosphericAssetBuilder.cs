using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AtmosphericAssetBuilder
{
    private const string PrefabPath = "Assets/Prefabs/World/Lighting/AtmosphericCameraVolume.prefab";
    private const string ServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string AtmosphereLayerName = "PartialOccluders";

    [MenuItem("Chainbound/World/Rebuild Overworld Atmosphere Assets")]
    public static void RebuildFromMenu()
    {
        Debug.Log("[AtmosphericAssetBuilder] " + Run());
    }

    public static string Run()
    {
        Texture2D moteTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/World/atmosphere_mote.png");
        Texture2D streakTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/World/atmosphere_streak.png");
        Texture2D leafTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/World/atmosphere_leaf.png");
        Sprite moteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/atmosphere_mote.png");
        Sprite streakSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/atmosphere_streak.png");
        Sprite leafSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/atmosphere_leaf.png");

        Material additive = ConfigureMat("Assets/Sprites/Materials/AtmosphericMote_Additive.mat", moteTex, true);
        Material streak = ConfigureMat(
            "Assets/Sprites/Materials/AtmosphericStreak_Additive.mat",
            streakTex,
            true,
            "SignalJam/Sprites/AtmosphericWindStreak");
        Material alpha = ConfigureMat("Assets/Sprites/Materials/AtmosphericDebris_Alpha.mat", leafTex, false);

        AtmosphericProfileSO forest = AssetDatabase.LoadAssetAtPath<AtmosphericProfileSO>("Assets/Data/World/AtmosphericProfile_Forest.asset");
        AtmosphericProfileSO jungle = AssetDatabase.LoadAssetAtPath<AtmosphericProfileSO>("Assets/Data/World/AtmosphericProfile_Jungle.asset");
        if (forest != null && string.IsNullOrWhiteSpace(forest.ProfileId))
        {
            forest.ApplyForestPreset();
            forest.AssignSprites(moteSprite, streakSprite, leafSprite);
            EditorUtility.SetDirty(forest);
        }
        if (jungle != null && string.IsNullOrWhiteSpace(jungle.ProfileId))
        {
            jungle.ApplyJunglePreset();
            jungle.AssignSprites(moteSprite, streakSprite, leafSprite);
            EditorUtility.SetDirty(jungle);
        }
        // Persist newly introduced serialized settings while preserving every
        // existing authored value on initialized profiles.
        if (forest != null)
            EditorUtility.SetDirty(forest);
        if (jungle != null)
            EditorUtility.SetDirty(jungle);

        AtmosphericCatalogSO catalog = AssetDatabase.LoadAssetAtPath<AtmosphericCatalogSO>("Assets/Data/World/AtmosphericCatalog_Overworld.asset");
        catalog.EditorSetDefaultProfile(forest);
        catalog.EditorSetSceneBlendSeconds(1.2f);
        List<AtmosphericSceneBinding> bindings = new List<AtmosphericSceneBinding>();
        AtmosphericSceneBinding loom = new AtmosphericSceneBinding();
        loom.EditorSet("Loomville", forest);
        AtmosphericSceneBinding link = new AtmosphericSceneBinding();
        link.EditorSet("Link1", forest);
        AtmosphericSceneBinding jungBind = new AtmosphericSceneBinding();
        jungBind.EditorSet("Jungle", jungle);
        bindings.Add(loom);
        bindings.Add(link);
        bindings.Add(jungBind);
        catalog.EditorSetBindings(bindings);
        EditorUtility.SetDirty(catalog);

        GameObject temp = new GameObject("AtmosphericCameraVolume");
        int atmosphereLayer = LayerMask.NameToLayer(AtmosphereLayerName);
        if (atmosphereLayer < 0)
            atmosphereLayer = 0;
        temp.layer = atmosphereLayer;
        AtmosphericCameraVolume volume = temp.AddComponent<AtmosphericCameraVolume>();
        ParticleSystem motes = CreateParticleChild(temp.transform, "Motes", atmosphereLayer);
        ParticleSystem streaks = CreateParticleChild(temp.transform, "Streaks", atmosphereLayer);
        AtmosphericWindTrailEmitter windTrailEmitter = streaks.gameObject.AddComponent<AtmosphericWindTrailEmitter>();
        List<TrailRenderer> trailPool = CreateTrailPool(streaks.transform, atmosphereLayer, 10);
        windTrailEmitter.EditorSetPool(trailPool);
        ParticleSystem debris = CreateParticleChild(temp.transform, "Debris", atmosphereLayer);
        SerializedObject so = new SerializedObject(volume);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.FindProperty("additiveMaterial").objectReferenceValue = additive;
        so.FindProperty("streakMaterial").objectReferenceValue = streak;
        so.FindProperty("alphaMaterial").objectReferenceValue = alpha;
        so.FindProperty("motes").objectReferenceValue = motes;
        so.FindProperty("streaks").objectReferenceValue = streaks;
        so.FindProperty("windTrailEmitter").objectReferenceValue = windTrailEmitter;
        so.FindProperty("debris").objectReferenceValue = debris;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);

        GameObject servicesRoot = PrefabUtility.LoadPrefabContents(ServicesPath);
        Transform existing = servicesRoot.transform.Find("AtmosphericCameraVolume");
        if (existing == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            PrefabUtility.InstantiatePrefab(prefab, servicesRoot.transform);
        }
        else
        {
            AtmosphericCameraVolume existingVolume = existing.GetComponent<AtmosphericCameraVolume>();
            if (existingVolume != null)
            {
                SerializedObject existingSo = new SerializedObject(existingVolume);
                existingSo.FindProperty("catalog").objectReferenceValue = catalog;
                existingSo.FindProperty("additiveMaterial").objectReferenceValue = additive;
                existingSo.FindProperty("streakMaterial").objectReferenceValue = streak;
                existingSo.FindProperty("alphaMaterial").objectReferenceValue = alpha;
                existingSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        PrefabUtility.SaveAsPrefabAsset(servicesRoot, ServicesPath);
        PrefabUtility.UnloadPrefabContents(servicesRoot);
        AssetDatabase.SaveAssets();

        string jungleId = catalog.Resolve("Jungle") != null ? catalog.Resolve("Jungle").ProfileId : "null";
        return "wired GameServices; jungleProfile=" + jungleId + " leaf=" + (leafSprite != null);
    }

    public static string ApplyCurrentMotionTuning()
    {
        string[] paths =
        {
            "Assets/Data/World/AtmosphericProfile_Forest.asset",
            "Assets/Data/World/AtmosphericProfile_Jungle.asset"
        };
        int updated = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            AtmosphericProfileSO profile = AssetDatabase.LoadAssetAtPath<AtmosphericProfileSO>(paths[i]);
            if (profile == null)
                continue;
            profile.ApplyCurrentMotionTuning();
            EditorUtility.SetDirty(profile);
            updated++;
        }
        AssetDatabase.SaveAssets();
        return "updated atmosphere motion profiles=" + updated;
    }

    private static ParticleSystem CreateParticleChild(Transform parent, string name, int layer)
    {
        GameObject child = new GameObject(name);
        child.layer = layer;
        child.transform.SetParent(parent, false);
        ParticleSystem system = child.AddComponent<ParticleSystem>();
        var main = system.main;
        main.playOnAwake = false;
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static List<TrailRenderer> CreateTrailPool(Transform parent, int layer, int count)
    {
        var pool = new List<TrailRenderer>(count);
        for (int i = 0; i < count; i++)
        {
            GameObject child = new GameObject("WindTrail_" + i.ToString("00"));
            child.layer = layer;
            child.transform.SetParent(parent, false);
            TrailRenderer renderer = child.AddComponent<TrailRenderer>();
            renderer.emitting = false;
            child.SetActive(false);
            pool.Add(renderer);
        }
        return pool;
    }

    private static Material ConfigureMat(string path, Texture2D tex, bool additive, string shaderName = null)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!string.IsNullOrEmpty(shaderName))
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
                mat.shader = shader;
        }
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_ParticleColor", Color.white);
        mat.SetFloat("_BaseAlpha", 1f);
        mat.SetFloat("_DepthFadeDist", 0.45f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_WaveAmplitude")) mat.SetFloat("_WaveAmplitude", 0.055f);
        if (mat.HasProperty("_WaveFrequency")) mat.SetFloat("_WaveFrequency", 1.35f);
        if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", 2.2f);
        if (mat.HasProperty("_FlowFrequency")) mat.SetFloat("_FlowFrequency", 2f);
        if (mat.HasProperty("_FlowSpeed")) mat.SetFloat("_FlowSpeed", 2.8f);
        if (mat.HasProperty("_FlowStrength")) mat.SetFloat("_FlowStrength", 0.35f);
        if (mat.HasProperty("_PulseMinimum")) mat.SetFloat("_PulseMinimum", 0.55f);
        if (mat.HasProperty("_PulseSpeed")) mat.SetFloat("_PulseSpeed", 1.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
