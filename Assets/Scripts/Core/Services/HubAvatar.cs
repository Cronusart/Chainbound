using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace Chainbound.Hub
{
    /// <summary>
    /// Owner-authoritative hub body. The network carries a UGS player id and a walk bit.
    /// The visible name and cosmetic ids come from the presence card.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(HubWalkMotor))]
    public sealed class HubAvatar : NetworkBehaviour
    {
        [SerializeField] private HubWalkMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private TMP_Text nameLabel;

        private readonly NetworkVariable<byte> motion = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<FixedString128Bytes> ugsPlayerId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public string AppliedDisplayName { get; private set; } = "Trainer";

        public string AppliedCosmeticIdsJson { get; private set; } = "[]";

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<HubWalkMotor>();
            EnsureLabel();
        }

        public override void OnNetworkSpawn()
        {
            ugsPlayerId.OnValueChanged += HandleIdChanged;
            if (IsOwner)
            {
                SnapToSpawn();
                PublishOwnerId();
            }
            else
                motor.enabled = false;

            ApplyCard(ugsPlayerId.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            ugsPlayerId.OnValueChanged -= HandleIdChanged;
        }

        private void Update()
        {
            bool localDriver = !IsSpawned || IsOwner;
            motor.enabled = localDriver;
            if (IsSpawned && IsOwner)
                motion.Value = motor.Walking ? (byte)1 : (byte)0;

            if (animator != null)
            {
                bool walking = localDriver ? motor.Walking : motion.Value == 1;
                animator.SetBool("Walking", walking);
            }
        }

        private void LateUpdate()
        {
            if (nameLabel == null || Camera.main == null)
                return;

            nameLabel.transform.rotation = Quaternion.LookRotation(
                nameLabel.transform.position - Camera.main.transform.position,
                Vector3.up);
        }

        private void SnapToSpawn()
        {
            GameObject spawn = GameObject.Find("HubSpawn");
            if (spawn == null)
                return;

            transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        }

        private void PublishOwnerId()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                return;

            string id = AuthenticationService.Instance.PlayerId;
            if (string.IsNullOrEmpty(id))
                return;

            ugsPlayerId.Value = new FixedString128Bytes(id);
        }

        private void HandleIdChanged(FixedString128Bytes previous, FixedString128Bytes next)
        {
            ApplyCard(next.ToString());
        }

        private void ApplyCard(string id)
        {
            _ = ApplyCardAsync(id);
        }

        private async Task ApplyCardAsync(string id)
        {
            HubPresenceCard card = await HubPresenceClient.FetchAsync(id);
            if (this == null || card == null)
                return;

            AppliedDisplayName = string.IsNullOrWhiteSpace(card.DisplayName) ? "Trainer" : card.DisplayName;
            AppliedCosmeticIdsJson = string.IsNullOrWhiteSpace(card.CosmeticIdsJson) ? "[]" : card.CosmeticIdsJson;
            if (nameLabel != null)
                nameLabel.text = AppliedDisplayName;
        }

        private void EnsureLabel()
        {
            if (nameLabel != null)
                return;

            var labelObject = new GameObject("Name");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            nameLabel = labelObject.AddComponent<TextMeshPro>();
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.fontSize = 3f;
            nameLabel.text = "Trainer";
        }
    }
}
