using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Scene-authored capture orb. Holds rest pose and an optional beam particle cone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureAttemptOrbAnchor : MonoBehaviour
    {
        [SerializeField] private Transform motionRoot;
        [SerializeField] private ParticleSystem beamParticles;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _restScale;
        private bool _cached;

        public Transform MotionRoot => motionRoot != null ? motionRoot : transform;
        public ParticleSystem BeamParticles => beamParticles;

        private void Awake()
        {
            if (motionRoot == null)
                motionRoot = transform;
            CacheRestPose();
        }

        public void CacheRestPose()
        {
            Transform root = MotionRoot;
            _restPosition = root.position;
            _restRotation = root.rotation;
            _restScale = root.localScale;
            _cached = true;
        }

        public void RestoreRestPose()
        {
            if (!_cached)
                CacheRestPose();
            Transform root = MotionRoot;
            root.position = _restPosition;
            root.rotation = _restRotation;
            root.localScale = _restScale;
        }

        public ParticleSystem EnsureBeam(Color color, float range, float angle, Material material)
        {
            if (beamParticles == null)
            {
                Transform root = MotionRoot;
                var go = new GameObject("CaptureBeam");
                go.transform.SetParent(root, false);
                go.transform.localPosition = Vector3.zero;
                beamParticles = go.AddComponent<ParticleSystem>();
            }

            // Unity cannot change a playing system's duration. A beam can be
            // replayed before its previous particles expire, so reset it before
            // applying the next presentation configuration.
            if (beamParticles.isPlaying)
                beamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = beamParticles.main;
            main.loop = true;
            main.duration = 0.6f;
            main.startLifetime = 0.45f;
            main.startSpeed = Mathf.Max(4f, range);
            main.startSize = 0.18f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = beamParticles.emission;
            emission.rateOverTime = 70f;

            ParticleSystem.ShapeModule shape = beamParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.08f;
            shape.length = Mathf.Max(1f, range * 0.25f);

            ParticleSystem.ColorOverLifetimeModule colorOver = beamParticles.colorOverLifetime;
            colorOver.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            colorOver.color = gradient;

            ParticleSystemRenderer renderer = beamParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && material != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
            }

            return beamParticles;
        }

        public void PlayBeamToward(Vector3 worldTarget, Color color, float range, float angle, Material material)
        {
            ParticleSystem beam = EnsureBeam(color, range, angle, material);
            Vector3 origin = MotionRoot.position;
            Vector3 delta = worldTarget - origin;
            if (delta.sqrMagnitude > 0.001f)
                beam.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            beam.Play(true);
        }

        public void StopBeam()
        {
            if (beamParticles == null)
                return;
            beamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
