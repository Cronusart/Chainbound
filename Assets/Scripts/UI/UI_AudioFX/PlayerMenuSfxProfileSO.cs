using UnityEngine;

/// <summary>
/// Semantic sound profile for the player menu. Assign SoundEventSO assets here
/// instead of wiring one-off clips to every button or panel.
/// </summary>
[CreateAssetMenu(fileName = "PlayerMenuSfxProfile", menuName = "Chainbound/Audio/UI/Player Menu SFX Profile")]
public sealed class PlayerMenuSfxProfileSO : ScriptableObject
{
    [SerializeField] private SoundEventSO menuOpen;
    [SerializeField] private SoundEventSO menuClose;
    [SerializeField] private SoundEventSO buttonSwitch;
    [SerializeField] private SoundEventSO panelSwitch;
    [SerializeField] private SoundEventSO confirm;
    [SerializeField] private SoundEventSO cancel;
    [SerializeField] private SoundEventSO invalid;
    [SerializeField] private SoundEventSO carouselMove;
    [SerializeField] private SoundEventSO chainlingSelectFallback;
    [SerializeField] private SoundEventSO healTick;
    [SerializeField] private SoundEventSO healComplete;
    [SerializeField] private SoundEventSO favorActivate;
    [SerializeField] private SoundEventSO partyHealComplete;
    [SerializeField] private SoundEventSO reviveComplete;

    public SoundEventSO MenuOpen => menuOpen;
    public SoundEventSO MenuClose => menuClose;
    public SoundEventSO ButtonSwitch => buttonSwitch;
    public SoundEventSO PanelSwitch => panelSwitch;
    public SoundEventSO Confirm => confirm;
    public SoundEventSO Cancel => cancel;
    public SoundEventSO Invalid => invalid;
    public SoundEventSO CarouselMove => carouselMove;
    public SoundEventSO ChainlingSelectFallback => chainlingSelectFallback;
    public SoundEventSO HealTick => healTick;
    public SoundEventSO HealComplete => healComplete;
    public SoundEventSO FavorActivate => favorActivate;
    public SoundEventSO PartyHealComplete => partyHealComplete;
    public SoundEventSO ReviveComplete => reviveComplete;
}
