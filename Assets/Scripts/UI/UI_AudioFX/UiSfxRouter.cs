using SignalJam.UI.MenuNavigation;
using UnityEngine;

/// <summary>
/// Central SFX router for menu navigation and confirm sounds.
/// Place once per menu root. Deduplicates navigate SFX so it only
/// fires when the selected GameObject actually changes.
/// </summary>
public class UiSfxRouter : MonoBehaviour, IMenuUiSfx, IPlayerMenuSfxPlayer
{
    [Header("Player Menu Profile")]
    [SerializeField] private PlayerMenuSfxProfileSO playerMenuProfile;

    [Header("Sound Events")]
    [SerializeField] private SoundEventSO navigateEvent;
    [SerializeField] private SoundEventSO confirmEvent;
    [SerializeField] private SoundEventSO cancelEvent;
    [SerializeField] private SoundEventSO menuOpenEvent;
    [SerializeField] private SoundEventSO menuCloseEvent;
    [SerializeField] private SoundEventSO panelSwitchEvent;
    [SerializeField] private SoundEventSO invalidEvent;
    [SerializeField] private SoundEventSO carouselMoveEvent;
    [SerializeField] private SoundEventSO chainlingSelectFallbackEvent;

    [Header("Validation")]
    [SerializeField] private bool warnIfMenuCueNotUiBus = true;
    [SerializeField, Min(0f)] private float duplicateCueWindowSeconds = 0.045f;

    private GameObject _lastSelected;
    private static PlayerMenuSfxCue s_lastCue;
    private static float s_lastCueTime = -100f;

    /// <summary>
    /// Plays the navigate SFX only if <paramref name="source"/> differs
    /// from the previously selected GameObject.
    /// </summary>
    public void PlayNavigate(GameObject source)
    {
        if (ShouldSkipCue(PlayerMenuSfxCue.ButtonSwitch))
            return;

        if (source == _lastSelected) return;
        _lastSelected = source;

        PlayResolved(ResolveCue(PlayerMenuSfxCue.ButtonSwitch));

        UiHapticsService.Instance?.TriggerNavigate();
    }

    /// <summary>
    /// Plays the confirm / submit SFX unconditionally.
    /// </summary>
    public void PlayConfirm()
    {
        if (ShouldSkipCue(PlayerMenuSfxCue.Confirm))
            return;

        PlayerMenuSfxResolver.SuppressSubmitTransitionCues(0.12f);
        PlayResolved(ResolveCue(PlayerMenuSfxCue.Confirm));

        UiHapticsService.Instance?.TriggerConfirm();
    }

    /// <summary>
    /// Plays the cancel / back SFX unconditionally.
    /// </summary>
    public void PlayCancel()
    {
        if (ShouldSkipCue(PlayerMenuSfxCue.Cancel))
            return;

        PlayResolved(ResolveCue(PlayerMenuSfxCue.Cancel));

        UiHapticsService.Instance?.TriggerCancel();
    }

    public void PlayCue(PlayerMenuSfxCue cue, Object overrideSound = null, float volumeScale = 1f)
    {
        if (ShouldSkipCue(cue))
            return;

        if (cue == PlayerMenuSfxCue.MenuOpen)
            PlayerMenuSfxResolver.SuppressNavigationCues(0.35f);
        else if (cue == PlayerMenuSfxCue.MenuClose)
            PlayerMenuSfxResolver.SuppressWholeMenuCloseCues(0.35f);
        else if (cue == PlayerMenuSfxCue.PanelSwitch)
            PlayerMenuSfxResolver.SuppressCue(PlayerMenuSfxCue.ButtonSwitch, 0.14f);
        else if (cue == PlayerMenuSfxCue.Confirm)
            PlayerMenuSfxResolver.SuppressSubmitTransitionCues(0.12f);
        else if (cue == PlayerMenuSfxCue.CarouselMove)
            PlayerMenuSfxResolver.SuppressCue(PlayerMenuSfxCue.ButtonSwitch, 0.12f);
        else if (cue == PlayerMenuSfxCue.Invalid)
            PlayerMenuSfxResolver.SuppressCue(PlayerMenuSfxCue.Confirm, 0.12f);

        SoundEventSO soundEvent = ResolveOverride(overrideSound) ?? ResolveCue(cue);
        PlayResolved(soundEvent, cue == PlayerMenuSfxCue.ChainlingSelect && overrideSound != null, volumeScale);

        switch (cue)
        {
            case PlayerMenuSfxCue.ButtonSwitch:
            case PlayerMenuSfxCue.PanelSwitch:
            case PlayerMenuSfxCue.CarouselMove:
                UiHapticsService.Instance?.TriggerNavigate();
                break;
            case PlayerMenuSfxCue.Confirm:
            case PlayerMenuSfxCue.ChainlingSelect:
            case PlayerMenuSfxCue.HealComplete:
            case PlayerMenuSfxCue.PartyHealComplete:
            case PlayerMenuSfxCue.ReviveComplete:
            case PlayerMenuSfxCue.FavorActivate:
                UiHapticsService.Instance?.TriggerConfirm();
                break;
            case PlayerMenuSfxCue.HealTick:
                UiHapticsService.Instance?.TriggerNavigate();
                break;
            case PlayerMenuSfxCue.Cancel:
            case PlayerMenuSfxCue.MenuClose:
                UiHapticsService.Instance?.TriggerCancel();
                break;
        }
    }

    public void PlayLayered(Object soundEvent, float volumeScale = 1f)
    {
        PlayResolved(ResolveOverride(soundEvent), allowNonUiBus: true, volumeScale);
    }

    /// <summary>
    /// Resets tracked selection so the next PlayNavigate always fires.
    /// Call after group swaps or screen transitions.
    /// </summary>
    public void ResetTracking()
    {
        _lastSelected = null;
    }

    private bool ShouldSkipCue(PlayerMenuSfxCue cue)
    {
        if (PlayerMenuSfxResolver.IsCueSuppressed(cue))
            return true;

        if (cue == PlayerMenuSfxCue.ButtonSwitch &&
            !PlayerMenuSfxResolver.HasRecentPlayerNavigationIntent())
        {
            return true;
        }

        float now = Time.unscaledTime;
        if (duplicateCueWindowSeconds > 0f &&
            s_lastCue == cue &&
            now - s_lastCueTime <= duplicateCueWindowSeconds)
        {
            return true;
        }

        s_lastCue = cue;
        s_lastCueTime = now;
        return false;
    }

    private SoundEventSO ResolveCue(PlayerMenuSfxCue cue)
    {
        switch (cue)
        {
            case PlayerMenuSfxCue.MenuOpen:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.MenuOpen : null, menuOpenEvent, confirmEvent);
            case PlayerMenuSfxCue.MenuClose:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.MenuClose : null, menuCloseEvent, cancelEvent);
            case PlayerMenuSfxCue.ButtonSwitch:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.ButtonSwitch : null, navigateEvent);
            case PlayerMenuSfxCue.PanelSwitch:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.PanelSwitch : null, panelSwitchEvent, navigateEvent);
            case PlayerMenuSfxCue.Confirm:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.Confirm : null, confirmEvent);
            case PlayerMenuSfxCue.Cancel:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.Cancel : null, cancelEvent);
            case PlayerMenuSfxCue.Invalid:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.Invalid : null, invalidEvent, cancelEvent);
            case PlayerMenuSfxCue.CarouselMove:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.CarouselMove : null, carouselMoveEvent, navigateEvent);
            case PlayerMenuSfxCue.ChainlingSelect:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.ChainlingSelectFallback : null, chainlingSelectFallbackEvent, confirmEvent);
            case PlayerMenuSfxCue.HealTick:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.HealTick : null, navigateEvent);
            case PlayerMenuSfxCue.HealComplete:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.HealComplete : null, confirmEvent);
            case PlayerMenuSfxCue.FavorActivate:
                return Coalesce(playerMenuProfile != null ? playerMenuProfile.FavorActivate : null, confirmEvent);
            case PlayerMenuSfxCue.PartyHealComplete:
                return Coalesce(
                    playerMenuProfile != null ? playerMenuProfile.PartyHealComplete : null,
                    playerMenuProfile != null ? playerMenuProfile.HealComplete : null,
                    confirmEvent);
            case PlayerMenuSfxCue.ReviveComplete:
                return Coalesce(
                    playerMenuProfile != null ? playerMenuProfile.ReviveComplete : null,
                    playerMenuProfile != null ? playerMenuProfile.HealComplete : null,
                    confirmEvent);
            default:
                return null;
        }
    }

    private static SoundEventSO ResolveOverride(Object overrideSound)
    {
        return overrideSound as SoundEventSO;
    }

    private static SoundEventSO Coalesce(params SoundEventSO[] candidates)
    {
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null)
                return candidates[i];
        }

        return null;
    }

    private void PlayResolved(SoundEventSO soundEvent, bool allowNonUiBus = false, float volumeScale = 1f)
    {
        if (soundEvent == null)
            return;

        if (!allowNonUiBus && warnIfMenuCueNotUiBus && soundEvent.OutputBus != AudioBus.UI)
        {
            Debug.LogWarning(
                $"[UiSfxRouter] '{soundEvent.name}' is assigned to {soundEvent.OutputBus}; generic player menu SFX should use AudioBus.UI.",
                this);
        }

        AudioService service = AudioService.Instance;
        if (service != null)
            service.Play(soundEvent, volumeScale: Mathf.Clamp01(volumeScale));
    }
}
