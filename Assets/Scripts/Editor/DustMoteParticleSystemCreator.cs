using UnityEngine;

namespace UnityEditor
{
    /// <summary>
    /// Creates a dust mote particle system configured for WindowBeamController.
    /// Menu: GameObject > Chainbound > Create Dust Mote Particle System
    /// </summary>
    public static class DustMoteParticleSystemCreator
    {
        [MenuItem("GameObject/Chainbound/Create Dust Mote Particle System", false, 10)]
        public static void Create()
        {
            var go = new GameObject("DustMotes");
            var ps = go.AddComponent<ParticleSystem>();
            var applier = go.AddComponent<DustMoteMaskApplier>();

            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 8f;
            emission.enabled = true;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2f, 1.8f, 4f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.8f, 0.2f),
                    new GradientAlphaKey(0.8f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = grad;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.05f);
            noise.frequency = 0.3f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var mat = FindMaterialByName("M_DustMote_Masked");
            if (mat != null)
                applier.dustMaterial = mat;
            else
                Debug.LogWarning("[DustMoteParticleSystemCreator] Material M_DustMote_Masked not found. Create it from Shader SignalJam/Sprites/DustMote_Masked and assign it to DustMoteMaskApplier.");

            Undo.RegisterCreatedObjectUndo(go, "Create Dust Mote Particle System");
            Selection.activeGameObject = go;
        }

        static Material FindMaterialByName(string name)
        {
            var guids = AssetDatabase.FindAssets($"t:Material {name}");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.name == name) return mat;
            }
            return null;
        }
    }
}
