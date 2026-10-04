using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

public static class AuthorWaterFootFoam
{
    private const string TexturePath = "Assets/Sprites/World/water_foot_foam.png";
    private const string AtlasPath = "Assets/Sprites/World/water_foam_particle_atlas.png";
    private const string MaterialPath = "Assets/Scripts/World/Environment/Prefabs/WaterFootFoam.mat";
    private const string PrefabPath = "Assets/Scripts/World/Environment/Prefabs/WaterFootFoam.prefab";
    private const string JungleScenePath = "Assets/Scenes/Jungle.unity";

    public static string Run()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(TexturePath);
        ConfigureTexture(AtlasPath);
        ConfigureMaterial();
        ConfigureParticlePrefab();
        ConfigureJungleScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Water foot foam texture, material, particle prefab, and Jungle interaction authored.";
    }

    public static string Validate()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        ParticleSystem particles = prefab.GetComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = prefab.GetComponent<ParticleSystemRenderer>();
        Texture texture = material.GetTexture("_BaseMap");
        return $"texture={texture?.name}:{texture?.width}x{texture?.height}; " +
               $"lifetime={particles.main.startLifetime.constantMin:0.00}-{particles.main.startLifetime.constantMax:0.00}; " +
               $"size={particles.main.startSize.constantMin:0.00}-{particles.main.startSize.constantMax:0.00}; " +
               $"distanceRate={particles.emission.rateOverDistance.constant:0.0}; " +
               $"noise={particles.noise.enabled}; render={renderer.renderMode}; queue={material.renderQueue}";
    }

    public static string ReportWaterfalls()
    {
        Scene existing = SceneManager.GetSceneByPath(JungleScenePath);
        bool wasLoaded = existing.IsValid() && existing.isLoaded;
        Scene scene = wasLoaded ? existing : EditorSceneManager.OpenScene(JungleScenePath, OpenSceneMode.Additive);
        var report = new StringBuilder();
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Waterfall", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                Bounds bounds = default;
                bool hasBounds = false;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                report.Append(root.name).Append(" pos=").Append(root.transform.position)
                    .Append(" boundsCenter=").Append(bounds.center).Append(" boundsSize=").Append(bounds.size)
                    .Append(" children=").Append(root.transform.childCount).AppendLine();
            }
            return report.ToString();
        }
        finally
        {
            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ConfigureTexture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ConfigureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.renderQueue = 3020;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureParticlePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ParticleSystem particles = root.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();

            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.95f, 1.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.92f, 1f, 0.34f),
                new Color(0.9f, 1f, 1f, 0.55f));
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 7f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 0.65f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 1.12f)));

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.72f, 0.96f, 1f), 0f),
                    new GradientColorKey(Color.white, 0.35f),
                    new GradientColorKey(new Color(0.72f, 0.96f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.32f, 0.12f),
                    new GradientAlphaKey(0.42f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = gradient;

            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = 0f;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            var noise = particles.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = 0.08f;
            noise.strengthY = 0.015f;
            noise.strengthZ = 0.08f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.18f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.maxParticleSize = 0.2f;
            renderer.sortingOrder = 6;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureJungleScene()
    {
        Scene existing = SceneManager.GetSceneByPath(JungleScenePath);
        bool wasLoaded = existing.IsValid() && existing.isLoaded;
        Scene scene = wasLoaded ? existing : EditorSceneManager.OpenScene(JungleScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (WaterStreamInteractor interactor in root.GetComponentsInChildren<WaterStreamInteractor>(true))
                {
                    var serialized = new SerializedObject(interactor);
                    serialized.FindProperty("ringStrength").floatValue = 0.032f;
                    serialized.FindProperty("ringWidth").floatValue = 0.22f;
                    serialized.FindProperty("foamStrength").floatValue = 0.7f;
                    serialized.FindProperty("foamSurfaceOffset").floatValue = 0.045f;
                    serialized.FindProperty("foamMaxEmissionRate").floatValue = 7f;
                    serialized.FindProperty("enterFoamCount").intValue = 2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(interactor);
                }

                if (root.name == "Waterfall")
                    AuthorWaterfallBaseFoam(root);
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AuthorWaterfallBaseFoam(GameObject waterfall)
    {
        Transform existing = waterfall.transform.Find("WaterfallBaseFoam");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer sourceRenderer in waterfall.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds) { bounds = sourceRenderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(sourceRenderer.bounds);
        }
        if (!hasBounds)
            return;

        GameObject host = new GameObject("WaterfallBaseFoam");
        host.transform.SetParent(waterfall.transform, true);
        host.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.25f, bounds.center.z);
        host.layer = waterfall.layer;

        ParticleSystem chunks = host.AddComponent<ParticleSystem>();
        ParticleSystemRenderer chunksRenderer = host.GetComponent<ParticleSystemRenderer>();
        ConfigureWaterfallChunks(chunks, chunksRenderer, bounds.size.x);

        GameObject mistObject = new GameObject("FineFoamMist");
        mistObject.transform.SetParent(host.transform, false);
        mistObject.layer = waterfall.layer;
        ParticleSystem mist = mistObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer mistRenderer = mistObject.GetComponent<ParticleSystemRenderer>();
        ConfigureWaterfallMist(mist, mistRenderer, bounds.size.x);

        var subEmitters = chunks.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(mist, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

        chunks.Play(true);
        mist.Play(true);
        EditorUtility.SetDirty(waterfall);
    }

    private static void ConfigureWaterfallChunks(ParticleSystem particles, ParticleSystemRenderer renderer, float width)
    {
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.45f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.65f, 0.92f, 1f, 0.38f), new Color(1f, 1f, 1f, 0.68f));
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = Mathf.Clamp(width * 0.7f, 12f, 28f);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 0.92f, 0.4f, 2.4f);

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.22f, 1f), new Keyframe(1f, 0.35f)));
        ApplyWaterfallFade(particles, 0.5f);
        ApplyAtlas(particles);
        ApplyWaterfallNoise(particles, 0.38f, 0.7f);
        ConfigureRenderer(renderer, ParticleSystemRenderMode.Billboard, 8);
    }

    private static void ConfigureWaterfallMist(ParticleSystem particles, ParticleSystemRenderer renderer, float width)
    {
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.52f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.94f, 1f, 0.18f), new Color(1f, 1f, 1f, 0.38f));
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = Mathf.Clamp(width * 1.1f, 22f, 46f);
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 6) });

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.25f, 2.8f);

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-3.6f, 3.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        ApplyWaterfallFade(particles, 0.3f);
        ApplyAtlas(particles);
        ApplyWaterfallNoise(particles, 0.55f, 0.48f);
        ConfigureRenderer(renderer, ParticleSystemRenderMode.Billboard, 7);
    }

    private static void ApplyAtlas(ParticleSystem particles)
    {
        var sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 2;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = 0f;
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
    }

    private static void ApplyWaterfallFade(ParticleSystem particles, float peakAlpha)
    {
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.72f, 0.95f, 1f), 0f), new GradientColorKey(Color.white, 0.3f), new GradientColorKey(new Color(0.72f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.16f), new GradientAlphaKey(peakAlpha * 0.55f, 0.65f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    private static void ApplyWaterfallNoise(ParticleSystem particles, float strength, float frequency)
    {
        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.28f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    private static void ConfigureRenderer(ParticleSystemRenderer renderer, ParticleSystemRenderMode mode, int sortingOrder)
    {
        renderer.renderMode = mode;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        renderer.sortingOrder = sortingOrder;
        renderer.maxParticleSize = 0.22f;
    }
}
