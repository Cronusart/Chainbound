using Chainbound.Combat;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Service-backed combat UI SFX helper. Author assigned SoundEventSO assets on AudioBus.UI.
    /// Battle outcome SFX use CombatBattleSfxCatalogSO; ambience stays on AmbienceZone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatUiSfxPlayer : MonoBehaviour, ICombatUiSfxPlayer
    {
        [SerializeField] private global::SoundEventSO navigate;
        [SerializeField] private global::SoundEventSO confirm;
        [SerializeField] private global::SoundEventSO cancel;
        [SerializeField] private global::SoundEventSO invalid;
        [SerializeField] private global::SoundEventSO targetChanged;
        [SerializeField] private bool warnIfNotUiBus = true;

        public void PlayCue(CombatUiSfxCue cue)
        {
            Play(Resolve(cue));
        }

        private global::SoundEventSO Resolve(CombatUiSfxCue cue)
        {
            switch (cue)
            {
                case CombatUiSfxCue.Confirm:
                    return confirm;
                case CombatUiSfxCue.Cancel:
                    return cancel;
                case CombatUiSfxCue.Invalid:
                    return invalid;
                case CombatUiSfxCue.TargetChanged:
                    return targetChanged != null ? targetChanged : navigate;
                default:
                    return navigate;
            }
        }

        private void Play(global::SoundEventSO soundEvent)
        {
            if (soundEvent == null)
                return;

            if (warnIfNotUiBus && soundEvent.OutputBus != global::AudioBus.UI)
            {
                Debug.LogWarning(
                    $"[CombatUiSfxPlayer] '{soundEvent.name}' is assigned to {soundEvent.OutputBus}; combat UI SFX should use AudioBus.UI.",
                    this);
            }

            global::AudioService service = global::AudioService.Instance;
            if (service != null)
                service.Play(soundEvent);
        }
    }
}
