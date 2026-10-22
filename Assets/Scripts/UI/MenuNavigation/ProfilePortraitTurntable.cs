using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Turns the profile dog through up, right, down, and left idle frames.
    /// Hat and outfit images stay inactive until directional cosmetic art exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfilePortraitTurntable : MonoBehaviour
    {
        public const float HoldSeconds = 0.6f;
        public const float IdleFramesPerSecond = 8f;

        public enum Facing
        {
            Up = 0,
            Right = 1,
            Down = 2,
            Left = 3
        }

        private static readonly Facing[] TurnOrder =
        {
            Facing.Up,
            Facing.Right,
            Facing.Down,
            Facing.Left
        };

        [SerializeField] private Image bodyImage;
        [SerializeField] private Image hatImage;
        [SerializeField] private Image outfitImage;
        [SerializeField] private Sprite[] upSprites = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] rightSprites = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] downSprites = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] leftSprites = System.Array.Empty<Sprite>();
        [SerializeField] private float holdSeconds = HoldSeconds;
        [SerializeField] private float idleFramesPerSecond = IdleFramesPerSecond;

        private int facingIndex;
        private int frameIndex;
        private float holdElapsed;
        private float frameElapsed;
        private bool frozen;

        public Facing CurrentFacing => TurnOrder[facingIndex];

        public bool IsFrozen => frozen;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void SetBody(Image body, Image hat, Image outfit)
        {
            bodyImage = body;
            hatImage = hat;
            outfitImage = outfit;
        }

        public void SetSequences(Sprite[] up, Sprite[] right, Sprite[] down, Sprite[] left)
        {
            upSprites = up ?? System.Array.Empty<Sprite>();
            rightSprites = right ?? System.Array.Empty<Sprite>();
            downSprites = down ?? System.Array.Empty<Sprite>();
            leftSprites = left ?? System.Array.Empty<Sprite>();
            facingIndex = 0;
            frameIndex = 0;
            holdElapsed = 0f;
            frameElapsed = 0f;
            frozen = false;
            Apply();
        }

        public void Tick(float unscaledDelta)
        {
            if (frozen || unscaledDelta <= 0f)
                return;

            float frameDuration = 1f / Mathf.Max(1f, idleFramesPerSecond);
            frameElapsed += unscaledDelta;
            Sprite[] current = SpritesFor(CurrentFacing);
            int frameCount = current != null ? current.Length : 0;
            if (frameCount > 1)
            {
                while (frameElapsed >= frameDuration)
                {
                    frameElapsed -= frameDuration;
                    frameIndex = (frameIndex + 1) % frameCount;
                }
            }

            float hold = Mathf.Max(0.05f, holdSeconds);
            holdElapsed += unscaledDelta;
            while (holdElapsed >= hold)
            {
                holdElapsed -= hold;
                facingIndex = (facingIndex + 1) % TurnOrder.Length;
                frameIndex = 0;
                frameElapsed = 0f;
            }

            Apply();
        }

        public void FreezeOnDown()
        {
            frozen = true;
            facingIndex = IndexOf(Facing.Down);
            frameIndex = 0;
            frameElapsed = 0f;
            holdElapsed = 0f;
            Apply();
        }

        public void Resume()
        {
            frozen = false;
        }

        private void Apply()
        {
            Sprite body = Frame(SpritesFor(CurrentFacing));
            if (bodyImage != null && body != null)
                bodyImage.sprite = body;
        }

        private Sprite[] SpritesFor(Facing facing)
        {
            switch (facing)
            {
                case Facing.Right: return rightSprites;
                case Facing.Down: return downSprites;
                case Facing.Left: return leftSprites;
                default: return upSprites;
            }
        }

        private Sprite Frame(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0)
                return null;

            int index = frameIndex % sprites.Length;
            if (index < 0)
                index = 0;
            return sprites[index];
        }

        private static int IndexOf(Facing facing)
        {
            for (int i = 0; i < TurnOrder.Length; i++)
            {
                if (TurnOrder[i] == facing)
                    return i;
            }

            return 0;
        }
    }
}
