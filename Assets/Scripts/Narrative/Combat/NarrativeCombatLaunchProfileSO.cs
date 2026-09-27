using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Narrative-authored combat launch overrides copied into a transient runtime bridge
    /// just before battle scene load.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NarrativeCombatLaunchProfile",
        menuName = "Chainbound/Narrative/Combat/Combat Launch Profile")]
    public class NarrativeCombatLaunchProfileSO : ScriptableObject
    {
        [Header("Player Party")]
        [Tooltip("Optional scripted player party for narrative battles (e.g. intro ghost roster). Requires \"Disable Supabase party resolution\" on this profile.")]
        [SerializeField] private ManualPartyProviderSO playerPartyOverrideOrFallback;

        [Header("Resolution")]
        [Tooltip("When enabled, skip Supabase lineup resolution and use the scripted player party only.")]
        [SerializeField] private bool disableSupabasePartyResolution;

        [Tooltip("When enabled, skip on-chain move loadout resolution for this narrative encounter.")]
        [SerializeField] private bool disableOnChainLoadoutResolution;

        [Header("Scripted Flow")]
        [Tooltip("When enabled, arm IntroEncounterAutoEscaper for this battle.")]
        [SerializeField] private bool armIntroAutoEscaper;

        public ManualPartyProviderSO PlayerPartyOverrideOrFallback => playerPartyOverrideOrFallback;
        public bool DisableSupabasePartyResolution => disableSupabasePartyResolution;
        public bool DisableOnChainLoadoutResolution => disableOnChainLoadoutResolution;
        public bool ArmIntroAutoEscaper => armIntroAutoEscaper;
    }
}
