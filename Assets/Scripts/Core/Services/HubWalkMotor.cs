using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chainbound.Hub
{
    /// <summary>
    /// Small kinematic walker for the hub avatar. Remote copies leave this disabled
    /// so they do not run the gameplay movement controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubWalkMotor : MonoBehaviour
    {
        [SerializeField] private float speed = 3.5f;

        private Rigidbody body;

        public Vector2 LastMove { get; private set; }

        public bool Walking => LastMove.sqrMagnitude > 0.01f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            HoldKinematic();
        }

        private void Update()
        {
            HoldKinematic();
            Vector2 move = ReadMove();
            LastMove = move.sqrMagnitude > 1f ? move.normalized : move;
            if (LastMove.sqrMagnitude < 0.0001f)
                return;

            Vector3 delta = new Vector3(LastMove.x, 0f, LastMove.y);
            transform.position += delta * (speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(delta, Vector3.up);
        }

        private void HoldKinematic()
        {
            if (body == null)
                return;

            body.isKinematic = true;
            body.useGravity = false;
        }

        private static Vector2 ReadMove()
        {
            MobileInputManager mobile = FindFirstObjectByType<MobileInputManager>();
            if (mobile != null && mobile.MoveInput.sqrMagnitude > 0.0001f)
                return mobile.MoveInput;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            Vector2 keys = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                keys.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                keys.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                keys.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                keys.x -= 1f;
            return keys;
        }
    }
}
