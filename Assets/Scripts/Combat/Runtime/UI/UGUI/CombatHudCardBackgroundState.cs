using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatHudCardBackgroundState : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite targetSprite;
        [SerializeField] private Sprite activeTargetSprite;

        [Header("HP Frame Overlay (shadows/highlights, syncs with background state)")]
        [SerializeField] private Image hpFrameOverlayImage;
        [SerializeField] private Sprite hpFrameOverlayNormal;
        [SerializeField] private Sprite hpFrameOverlayActive;
        [SerializeField] private Sprite hpFrameOverlayTarget;
        [SerializeField] private Sprite hpFrameOverlayActiveTarget;

        private bool _isActive;
        private bool _isTarget;

        public void SetBackgroundImage(Image image)
        {
            backgroundImage = image;
            Refresh();
        }

        public void SetSprites(Sprite normal, Sprite active, Sprite target, Sprite activeTarget = null)
        {
            normalSprite = normal;
            activeSprite = active;
            targetSprite = target;
            activeTargetSprite = activeTarget;
            Refresh();
        }

        public void SetHpFrameOverlayImage(Image image)
        {
            hpFrameOverlayImage = image;
            Refresh();
        }

        public void SetHpFrameOverlaySprites(Sprite normal, Sprite active, Sprite target, Sprite activeTarget = null)
        {
            hpFrameOverlayNormal = normal;
            hpFrameOverlayActive = active;
            hpFrameOverlayTarget = target;
            hpFrameOverlayActiveTarget = activeTarget;
            Refresh();
        }

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
            Refresh();
        }

        public void SetTarget(bool isTarget)
        {
            _isTarget = isTarget;
            Refresh();
        }

        public void Refresh()
        {
            Sprite chosen = PickSpriteForState(
                normalSprite, activeSprite, targetSprite, activeTargetSprite);

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                if (normalSprite == null)
                    normalSprite = backgroundImage.sprite;
                if (chosen != null)
                    backgroundImage.sprite = chosen;
            }

            if (hpFrameOverlayImage != null)
            {
                Sprite overlayChosen = PickSpriteForState(
                    hpFrameOverlayNormal, hpFrameOverlayActive, hpFrameOverlayTarget, hpFrameOverlayActiveTarget);
                hpFrameOverlayImage.enabled = overlayChosen != null;
                if (overlayChosen != null)
                    hpFrameOverlayImage.sprite = overlayChosen;
            }
        }

        private Sprite PickSpriteForState(Sprite normal, Sprite active, Sprite target, Sprite activeTarget)
        {
            if (_isActive && _isTarget && activeTarget != null)
                return activeTarget;
            if (_isActive && active != null)
                return active;
            if (_isTarget && target != null)
                return target;
            return normal;
        }
    }
}
