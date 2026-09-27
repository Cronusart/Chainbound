using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalJam.UI.VisualFx
{
    /// <summary>
    /// Lightweight runtime diagnostics for "visible in Scene view, missing in Game view" particle issues.
    /// Attach to a particle root or any parent, then use the context menu or log-on-start options.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiParticleRenderDiagnostics : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private Camera[] cameras;
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private bool includeInactive = true;

        [Header("Logging")]
        [SerializeField] private bool logOnStart = true;
        [SerializeField] private bool logOnEnable;
        [SerializeField] private bool logContinuously;
        [SerializeField, Min(0.1f)] private float continuousLogInterval = 1f;

        [Header("Gizmos")]
        [SerializeField] private bool drawRendererBounds = true;
        [SerializeField] private Color boundsColor = new Color(1f, 0.85f, 0.1f, 0.7f);

        private float _nextLogTime;

        private void Reset()
        {
            CollectLocalParticleSystems();
        }

        private void OnEnable()
        {
            if (logOnEnable)
                LogDiagnostics();
        }

        private void Start()
        {
            if (logOnStart)
                LogDiagnostics();
        }

        private void Update()
        {
            if (!logContinuously || Time.unscaledTime < _nextLogTime)
                return;

            _nextLogTime = Time.unscaledTime + continuousLogInterval;
            LogDiagnostics();
        }

        [ContextMenu("Collect Local Particle Systems")]
        public void CollectLocalParticleSystems()
        {
            particleSystems = includeChildren
                ? GetComponentsInChildren<ParticleSystem>(includeInactive)
                : GetComponents<ParticleSystem>();
        }

        [ContextMenu("Log Particle Render Diagnostics")]
        public void LogDiagnostics()
        {
            if (particleSystems == null || particleSystems.Length == 0)
                CollectLocalParticleSystems();

            Camera[] resolvedCameras = ResolveCameras();
            var sb = new StringBuilder(4096);
            sb.AppendLine($"[UiParticleRenderDiagnostics] {name}");
            sb.AppendLine($"Active render pipeline: {ResolveRenderPipelineName()}");
            sb.AppendLine($"Particle systems: {CountValidParticles()} | Cameras checked: {resolvedCameras.Length}");

            if (particleSystems == null || particleSystems.Length == 0)
                sb.AppendLine("No ParticleSystem targets found. Assign targets or enable includeChildren on a parent object.");

            if (resolvedCameras.Length == 0)
                sb.AppendLine("No cameras found. Game view needs an enabled camera to render ParticleSystemRenderer objects.");

            for (int i = 0; particleSystems != null && i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;

                AppendParticleDiagnostics(sb, ps, resolvedCameras);
            }

            Debug.Log(sb.ToString(), this);
        }

        private int CountValidParticles()
        {
            int count = 0;
            if (particleSystems == null)
                return count;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                    count++;
            }

            return count;
        }

        private Camera[] ResolveCameras()
        {
            if (cameras != null && cameras.Length > 0)
            {
                int valid = 0;
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null)
                        valid++;
                }

                if (valid > 0)
                {
                    var filtered = new Camera[valid];
                    int write = 0;
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        if (cameras[i] != null)
                            filtered[write++] = cameras[i];
                    }

                    return filtered;
                }
            }

            return Camera.allCameras;
        }

        private static string ResolveRenderPipelineName()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null ? pipeline.name : "Built-in Render Pipeline";
        }

        private static void AppendParticleDiagnostics(StringBuilder sb, ParticleSystem ps, Camera[] cameras)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Transform tr = ps.transform;
            string layerName = LayerMask.LayerToName(ps.gameObject.layer);
            if (string.IsNullOrEmpty(layerName))
                layerName = ps.gameObject.layer.ToString();

            sb.AppendLine();
            sb.AppendLine($"ParticleSystem: {ps.name}");
            sb.AppendLine($"  ActiveSelf/Hierarchy: {ps.gameObject.activeSelf}/{ps.gameObject.activeInHierarchy}");
            sb.AppendLine($"  Layer: {ps.gameObject.layer} ({layerName})");
            sb.AppendLine($"  Position: {tr.position} | LocalPosition: {tr.localPosition} | LossyScale: {tr.lossyScale}");
            sb.AppendLine($"  Playing/Emitting/Paused/ParticleCount: {ps.isPlaying}/{ps.isEmitting}/{ps.isPaused}/{ps.particleCount}");

            ParticleSystem.MainModule main = ps.main;
            sb.AppendLine($"  Main: duration={main.duration:0.###}, loop={main.loop}, playOnAwake={main.playOnAwake}, simulationSpace={main.simulationSpace}, scalingMode={main.scalingMode}");

            Canvas canvas = ps.GetComponentInParent<Canvas>();
            if (canvas != null)
                sb.AppendLine($"  Parent Canvas: {canvas.name}, renderMode={canvas.renderMode}, sortingLayer={canvas.sortingLayerName}, sortingOrder={canvas.sortingOrder}");
            else
                sb.AppendLine("  Parent Canvas: <none>");

            if (renderer == null)
            {
                sb.AppendLine("  Renderer: <missing ParticleSystemRenderer>");
                return;
            }

            sb.AppendLine($"  Renderer enabled/isVisible: {renderer.enabled}/{renderer.isVisible}");
            sb.AppendLine($"  Bounds center/size: {renderer.bounds.center} / {renderer.bounds.size}");
            sb.AppendLine($"  Renderer sortingLayer/order: {renderer.sortingLayerName}/{renderer.sortingOrder}");
            AppendMaterialDiagnostics(sb, renderer);

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                    continue;

                AppendCameraDiagnostics(sb, camera, ps.gameObject.layer, renderer.bounds);
            }
        }

        private static void AppendMaterialDiagnostics(StringBuilder sb, ParticleSystemRenderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                sb.AppendLine("  Materials: <none>");
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    sb.AppendLine($"  Material[{i}]: <null> (will not render reliably)");
                    continue;
                }

                Shader shader = material.shader;
                string shaderName = shader != null ? shader.name : "<null shader>";
                string support = shader != null && shader.isSupported ? "supported" : "NOT supported";
                sb.AppendLine($"  Material[{i}]: {material.name}, shader={shaderName}, {support}, renderQueue={material.renderQueue}");
            }
        }

        private static void AppendCameraDiagnostics(StringBuilder sb, Camera camera, int particleLayer, Bounds bounds)
        {
            int layerMask = 1 << particleLayer;
            bool layerVisible = (camera.cullingMask & layerMask) != 0;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            bool inFrustum = GeometryUtility.TestPlanesAABB(planes, bounds);
            Vector3 viewport = camera.WorldToViewportPoint(bounds.center);
            bool centerInViewport = viewport.z >= camera.nearClipPlane
                && viewport.z <= camera.farClipPlane
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;

            sb.AppendLine($"  Camera: {camera.name}");
            sb.AppendLine($"    Enabled/Active: {camera.enabled}/{camera.gameObject.activeInHierarchy}, type={camera.cameraType}, targetDisplay={camera.targetDisplay}, depth={camera.depth}");
            sb.AppendLine($"    CullingMask includes particle layer: {layerVisible} (mask={camera.cullingMask}, layerMask={layerMask})");
            sb.AppendLine($"    Bounds in frustum: {inFrustum}, bounds center viewport={viewport}, centerInViewport={centerInViewport}");
            sb.AppendLine($"    Near/Far/FOV/Orthographic: {camera.nearClipPlane:0.###}/{camera.farClipPlane:0.###}/{camera.fieldOfView:0.###}/{camera.orthographic}");
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawRendererBounds || particleSystems == null)
                return;

            Color previous = Gizmos.color;
            Gizmos.color = boundsColor;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
            }

            Gizmos.color = previous;
        }
    }
}
