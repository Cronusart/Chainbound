using System.Collections;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Moves only the visual slot roots into the settled absolute-1v1 presentation.
    /// Logical slot indices, target references, and Chainling identity remain unchanged.
    /// </summary>
    public sealed class Combat2BattlefieldSingleCombatLayoutController : MonoBehaviour, ICombatBattlefieldSingleCombatLayoutController
    {
        [Header("Toggle")]
        [Tooltip("Controls battlefield and HUD repositioning only. Camera and wallet framing remain independent.")]
        [SerializeField] private bool enableSingleCombatLayout = true;

        [Header("Battlefield Slot Roots")]
        [SerializeField] private Combat2ChainlingView team0Slot0;
        [SerializeField] private Combat2ChainlingView team0Slot1;
        [SerializeField] private Combat2ChainlingView team1Slot0;
        [SerializeField] private Combat2ChainlingView team1Slot1;

        [Header("Cached Authored Positions")]
        [SerializeField, HideInInspector] private Vector3 team0Slot0AuthoredLocalPosition;
        [SerializeField, HideInInspector] private Vector3 team0Slot1AuthoredLocalPosition;
        [SerializeField, HideInInspector] private Vector3 team1Slot0AuthoredLocalPosition;
        [SerializeField, HideInInspector] private Vector3 team1Slot1AuthoredLocalPosition;
        [SerializeField, HideInInspector] private bool hasCachedAuthoredPositions;

        // The test harness currently overrides each slot's groundCircle with a
        // legacy TargetCircle under GroundCirclePivots, outside the slot hierarchy.
        // Keep those presentation-only anchors synchronized with their slot roots.
        private Transform _team0Slot0GroundCircle;
        private Transform _team0Slot1GroundCircle;
        private Transform _team1Slot0GroundCircle;
        private Transform _team1Slot1GroundCircle;
        private Vector3 _team0Slot0GroundCircleAuthoredWorldPosition;
        private Vector3 _team0Slot1GroundCircleAuthoredWorldPosition;
        private Vector3 _team1Slot0GroundCircleAuthoredWorldPosition;
        private Vector3 _team1Slot1GroundCircleAuthoredWorldPosition;
        private bool _hasCachedGroundCirclePositions;

        public bool IsEnabled => enableSingleCombatLayout;

        public bool EnableSingleCombatLayout
        {
            get => enableSingleCombatLayout;
            set
            {
                if (enableSingleCombatLayout == value)
                    return;

                enableSingleCombatLayout = value;
                if (!value)
                    RestoreAuthoredPositions();
            }
        }

        private void Awake()
        {
            ResolveSlotReferences();
            CacheAuthoredPositions();
        }

        private void OnEnable()
        {
            ResolveSlotReferences();
            CacheAuthoredPositions();
        }

        private void OnValidate()
        {
            ResolveSlotReferences();
            CacheAuthoredPositions();
        }

        private void OnDisable()
        {
            RestoreAuthoredPositions();
        }

        /// <summary>
        /// Allows the battlefield presenter to bind the same roots it already owns when
        /// this controller is added to a scene object without manual slot wiring.
        /// </summary>
        public void SetSlotRoots(
            Combat2ChainlingView slot0Team0,
            Combat2ChainlingView slot1Team0,
            Combat2ChainlingView slot0Team1,
            Combat2ChainlingView slot1Team1)
        {
            team0Slot0 = slot0Team0;
            team0Slot1 = slot1Team0;
            team1Slot0 = slot0Team1;
            team1Slot1 = slot1Team1;

            hasCachedAuthoredPositions = false;
            _hasCachedGroundCirclePositions = false;
            CacheAuthoredPositions();
        }

        public IEnumerator BlendSingleCombatLayout(CombatState state, bool active, float blendSeconds)
        {
            ResolveSlotReferences();
            CacheAuthoredPositions();
            if (!hasCachedAuthoredPositions)
                yield break;

            Vector3[] starts =
            {
                team0Slot0.transform.localPosition,
                team0Slot1.transform.localPosition,
                team1Slot0.transform.localPosition,
                team1Slot1.transform.localPosition
            };
            Vector3[] targets = GetTargetPositions(state, active);
            float duration = Mathf.Max(0f, blendSeconds);
            SyncGroundCirclePositions();

            if (duration <= 0f)
            {
                ApplyPositions(targets);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                ApplyInterpolatedPositions(starts, targets, eased);
                yield return null;
            }

            ApplyPositions(targets);
        }

        private void ResolveSlotReferences()
        {
            if (team0Slot0 != null && team0Slot1 != null && team1Slot0 != null && team1Slot1 != null)
                return;

            var slots = GetComponentsInChildren<Combat2ChainlingView>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                switch (slot.name)
                {
                    case "Team0_Slot0": team0Slot0 = slot; break;
                    case "Team0_Slot1": team0Slot1 = slot; break;
                    case "Team1_Slot0": team1Slot0 = slot; break;
                    case "Team1_Slot1": team1Slot1 = slot; break;
                }
            }
        }

        private void CacheAuthoredPositions()
        {
            if (team0Slot0 == null || team0Slot1 == null || team1Slot0 == null || team1Slot1 == null)
                return;

            if (!hasCachedAuthoredPositions)
            {
                team0Slot0AuthoredLocalPosition = team0Slot0.transform.localPosition;
                team0Slot1AuthoredLocalPosition = team0Slot1.transform.localPosition;
                team1Slot0AuthoredLocalPosition = team1Slot0.transform.localPosition;
                team1Slot1AuthoredLocalPosition = team1Slot1.transform.localPosition;
                hasCachedAuthoredPositions = true;
            }

            CacheGroundCirclePositions();
        }

        private void CacheGroundCirclePositions()
        {
            if (_hasCachedGroundCirclePositions)
                return;

            _team0Slot0GroundCircle = ResolveGroundCircleTransform(team0Slot0);
            _team0Slot1GroundCircle = ResolveGroundCircleTransform(team0Slot1);
            _team1Slot0GroundCircle = ResolveGroundCircleTransform(team1Slot0);
            _team1Slot1GroundCircle = ResolveGroundCircleTransform(team1Slot1);

            // A Combat2ChainlingView creates a fallback circle in Awake when its
            // reference is missing. Wait until all four views have resolved before
            // caching, so the controller never treats an uninitialized position as
            // the authored anchor.
            if (_team0Slot0GroundCircle == null || _team0Slot1GroundCircle == null ||
                _team1Slot0GroundCircle == null || _team1Slot1GroundCircle == null)
                return;

            _team0Slot0GroundCircleAuthoredWorldPosition = _team0Slot0GroundCircle.position;
            _team0Slot1GroundCircleAuthoredWorldPosition = _team0Slot1GroundCircle.position;
            _team1Slot0GroundCircleAuthoredWorldPosition = _team1Slot0GroundCircle.position;
            _team1Slot1GroundCircleAuthoredWorldPosition = _team1Slot1GroundCircle.position;
            _hasCachedGroundCirclePositions = true;
        }

        private static Transform ResolveGroundCircleTransform(Combat2ChainlingView slot)
        {
            return slot != null && slot.GroundCircle != null
                ? slot.GroundCircle.transform
                : null;
        }

        private Vector3[] GetTargetPositions(CombatState state, bool active)
        {
            var targets =
                new Vector3[4]
                {
                    team0Slot0AuthoredLocalPosition,
                    team0Slot1AuthoredLocalPosition,
                    team1Slot0AuthoredLocalPosition,
                    team1Slot1AuthoredLocalPosition
                };

            if (!active || state == null || state.team0 == null || state.team1 == null)
                return targets;

            // The presenter supplies the settled absolute-1v1 predicate, but keep the
            // guard here so this controller cannot move slots for 2v1 or swap-in states
            // if it is called directly by another presentation path.
            if (state.team0.CountAliveActiveSlots() != 1 || state.team1.CountAliveActiveSlots() != 1)
                return targets;

            if (state.team0.GetActiveInSlot(0) == null && state.team0.GetActiveInSlot(1) != null)
                targets[1] = team0Slot0AuthoredLocalPosition;

            if (state.team1.GetActiveInSlot(0) == null && state.team1.GetActiveInSlot(1) != null)
                targets[3] = team1Slot0AuthoredLocalPosition;

            return targets;
        }

        public void RestoreAuthoredLayout()
        {
            if (!hasCachedAuthoredPositions)
                return;

            ApplyPositions(new Vector3[4]
            {
                team0Slot0AuthoredLocalPosition,
                team0Slot1AuthoredLocalPosition,
                team1Slot0AuthoredLocalPosition,
                team1Slot1AuthoredLocalPosition
            });
        }

        private void RestoreAuthoredPositions()
        {
            RestoreAuthoredLayout();
        }

        private void ApplyPositions(Vector3[] positions)
        {
            if (positions == null || positions.Length < 4)
                return;

            if (team0Slot0 == null || team0Slot1 == null || team1Slot0 == null || team1Slot1 == null)
                return;

            team0Slot0.transform.localPosition = positions[0];
            team0Slot1.transform.localPosition = positions[1];
            team1Slot0.transform.localPosition = positions[2];
            team1Slot1.transform.localPosition = positions[3];
            SyncGroundCirclePositions();
        }

        private void ApplyInterpolatedPositions(Vector3[] starts, Vector3[] targets, float t)
        {
            if (starts == null || targets == null || starts.Length < 4 || targets.Length < 4)
                return;
            if (team0Slot0 == null || team0Slot1 == null || team1Slot0 == null || team1Slot1 == null)
                return;

            team0Slot0.transform.localPosition = Vector3.LerpUnclamped(starts[0], targets[0], t);
            team0Slot1.transform.localPosition = Vector3.LerpUnclamped(starts[1], targets[1], t);
            team1Slot0.transform.localPosition = Vector3.LerpUnclamped(starts[2], targets[2], t);
            team1Slot1.transform.localPosition = Vector3.LerpUnclamped(starts[3], targets[3], t);
            SyncGroundCirclePositions();
        }

        private void SyncGroundCirclePositions()
        {
            if (!_hasCachedGroundCirclePositions)
                return;

            SyncGroundCirclePosition(
                team0Slot0,
                team0Slot0AuthoredLocalPosition,
                _team0Slot0GroundCircle,
                _team0Slot0GroundCircleAuthoredWorldPosition);
            SyncGroundCirclePosition(
                team0Slot1,
                team0Slot1AuthoredLocalPosition,
                _team0Slot1GroundCircle,
                _team0Slot1GroundCircleAuthoredWorldPosition);
            SyncGroundCirclePosition(
                team1Slot0,
                team1Slot0AuthoredLocalPosition,
                _team1Slot0GroundCircle,
                _team1Slot0GroundCircleAuthoredWorldPosition);
            SyncGroundCirclePosition(
                team1Slot1,
                team1Slot1AuthoredLocalPosition,
                _team1Slot1GroundCircle,
                _team1Slot1GroundCircleAuthoredWorldPosition);
        }

        private static void SyncGroundCirclePosition(
            Combat2ChainlingView slot,
            Vector3 authoredSlotLocalPosition,
            Transform groundCircle,
            Vector3 authoredGroundCircleWorldPosition)
        {
            if (slot == null || groundCircle == null)
                return;

            Transform slotTransform = slot.transform;
            Transform slotParent = slotTransform.parent;
            Vector3 authoredSlotWorldPosition = slotParent != null
                ? slotParent.TransformPoint(authoredSlotLocalPosition)
                : authoredSlotLocalPosition;
            Vector3 slotDeltaWorld = slotTransform.position - authoredSlotWorldPosition;
            groundCircle.position = authoredGroundCircleWorldPosition + slotDeltaWorld;
        }
    }
}
