using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Loads the authenticated profile's canonical party lineup via <c>profile_get_party</c>.
    /// Fails closed: no fallback roster; callers should abort combat bootstrap when <see cref="IsReady"/> is false or party is empty.
    /// Profile-equipped loadouts are returned with the party. On-chain resolution may still enrich Stamped ownership when wired.
    /// </summary>
    public class SupabasePlayerLineupPartyProvider : MonoBehaviour, IPlayerPartyProvider, IPlayerLoadoutRefresh
    {
        [Header("Supabase")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        [Header("Presentation")]
        [SerializeField] private string teamName = "Player";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private PartySnapshot _cached;

        public string WalletAddress { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public PartySnapshot CachedParty => _cached;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<PartySnapshot> FetchAndCacheAsync(string walletAddress)
        {
            WalletAddress = walletAddress;
            IsReady = false;
            LastError = null;
            _cached = null;

            string playerProfileId = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "";
            if (string.IsNullOrWhiteSpace(playerProfileId))
            {
                return FailStrict("Authenticated player profile id was empty.");
            }

            if (supabaseConfig == null)
            {
                return FailStrict("SupabaseConfigSO was not assigned.");
            }

            try
            {
                string requestJson = JsonUtility.ToJson(new GetPlayerPartyRequest
                {
                    player_profile_id = playerProfileId,
                });

                const string functionName = "profile_get_party";

                var response = await SupabaseClient.PostJsonAsync(supabaseConfig, functionName, requestJson);
                if (!response.IsSuccess)
                {
                    return FailStrict($"{functionName} returned {response.statusCode}: {response.body}");
                }

                var payload = JsonUtility.FromJson<GetPlayerPartyResponse>(response.body);
                if (payload == null || payload.party == null || payload.party.Count == 0)
                {
                    return FailStrict($"{functionName} returned an empty party.");
                }

                var snapshot = new PartySnapshot { teamName = teamName };
                for (int i = 0; i < payload.party.Count; i++)
                {
                    var entry = payload.party[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.species_id))
                        continue;

                    int stars = ChainlingProgressionUtility.NormalizeStars(entry.stars);
                    int rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(entry.rebirth_count);
                    int[] ivs = ChainlingSnapshotStatUtility.CopyIvs(entry.ivs);
                    int[] picks = ChainlingSnapshotStatUtility.CopyRebirthIvBonuses(entry.rebirth_iv_bonuses);
                    ChainlingSnapshotStatUtility.WarnIfPickSumMismatch(
                        $"lineup '{entry.species_id}'",
                        rebirthCount,
                        picks);

                    snapshot.chainlings.Add(new ChainlingSnapshot
                    {
                        backendChainlingId = entry.backend_chainling_id,
                        speciesId = entry.species_id,
                        displayName = entry.display_name,
                        level = 0,
                        stars = stars,
                        masteryXp = Math.Max(0, entry.mastery_xp),
                        rebirthCount = rebirthCount,
                        evolutionStage = entry.evolution_stage > 0 ? entry.evolution_stage : 1,
                        visualIdentity = ResolveVisualIdentity(entry),
                        moveIds = CopyStringSlots(entry.move_ids),
                        modifier1SectionIds = CopyStringSlots(entry.modifier1_section_ids),
                        modifier2SectionIds = CopyStringSlots(entry.modifier2_section_ids),
                        ivs = ivs,
                        rebirthIvBonuses = picks,
                        currentHp = entry.current_hp,
                        nftMintAddress = FirstString(entry.nft_mint_address, entry.archive_asset_id),
                        archivedAtUnix = Math.Max(0, entry.archived_at_unix),
                        isEntangled = entry.entangled_flag,
                        entangledType = entry.entangled_flag
                            ? ChainlingTypeParseUtility.ParseOrNone(entry.entangled_type)
                            : ChainlingType.None,
                        isMutated = !string.IsNullOrWhiteSpace(entry.mutation_id) &&
                                    !string.Equals(entry.mutation_id.Trim(), ChainlingVisualIdentity.NoneId, System.StringComparison.OrdinalIgnoreCase),
                        mutationId = ChainlingVisualIdentity.NormalizeId(entry.mutation_id, ChainlingVisualIdentity.NoneId),
                    });
                }

                _cached = snapshot;
                IsReady = _cached.Count > 0;

                if (!IsReady)
                {
                    return FailStrict($"{functionName} returned no usable party entries.");
                }

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[SupabasePlayerLineupPartyProvider] Loaded {_cached.Count} party chainling(s) for profile={playerProfileId} wallet={walletAddress}.");
                }

                return _cached;
            }
            catch (Exception ex)
            {
                return FailStrict($"FetchAndCacheAsync exception: {ex.Message}");
            }
        }

        public void SetResolvedParty(PartySnapshot party)
        {
            _cached = party;
            IsReady = _cached != null && _cached.Count > 0;
        }

        public PartySnapshot GetCurrentParty()
        {
            return _cached;
        }

        public async Task<PartySnapshot> RefreshPartyAsync()
        {
            PartySnapshot previous = _cached;
            bool wasReady = IsReady;
            string wallet = !string.IsNullOrWhiteSpace(WalletAddress)
                ? WalletAddress
                : (GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "");
            PartySnapshot refreshed = await FetchAndCacheAsync(wallet);
            if (refreshed != null)
                return refreshed;

            _cached = previous;
            IsReady = wasReady && previous != null && previous.Count > 0;
            return null;
        }

        private PartySnapshot FailStrict(string reason)
        {
            LastError = reason;
            _cached = null;
            IsReady = false;

            if (enableDebugLogs)
                Debug.LogWarning($"[SupabasePlayerLineupPartyProvider] {reason}");

            return null;
        }

        private static ChainlingVisualIdentity ResolveVisualIdentity(GetPlayerPartyEntry entry)
        {
            var identity = ChainlingVisualIdentity.CreateDefault();
            if (entry == null)
                return identity;

            identity.visualVariantId = entry.visual_variant_id;

            if (entry.visual_identity != null)
            {
                identity.auraId = entry.visual_identity.aura_id;
                identity.hudCardVariantId = FirstString(entry.visual_identity.hud_card_variant_id, entry.visual_identity.nameplate_frame_id);
                identity.partyColumnVariantId = entry.visual_identity.party_column_variant_id;
                identity.swapInFxId = FirstString(entry.visual_identity.swap_in_fx_id, entry.visual_identity.spawn_fx_id);
                identity.swapOutFxId = FirstString(entry.visual_identity.swap_out_fx_id, entry.visual_identity.ko_swap_fx_id);
                identity.koFxId = FirstString(entry.visual_identity.ko_fx_id, entry.visual_identity.defender_ko_fx_id);
                identity.victoryPoseId = entry.visual_identity.victory_pose_id;
                identity.idleStyleId = entry.visual_identity.idle_style_id;
                identity.idleFlourishId = entry.visual_identity.idle_flourish_id;
                identity.meleeAttackStyleId = entry.visual_identity.melee_attack_style_id;
                identity.rangedAttackStyleId = entry.visual_identity.ranged_attack_style_id;
                identity.hitReactionStyleId = entry.visual_identity.hit_reaction_style_id;
                identity.attackerKoStyleId = FirstString(entry.visual_identity.attacker_ko_style_id, entry.visual_identity.enemy_ko_style_id);
            }

            if (!string.IsNullOrWhiteSpace(entry.mutation_id) &&
                !string.Equals(entry.mutation_id.Trim(), ChainlingVisualIdentity.NoneId, System.StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(identity.auraId))
            {
                identity.auraId = entry.mutation_id.Trim();
            }

            identity.Normalize();
            return identity;
        }

        private static string FirstString(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
        }

        private static List<string> CopyStringSlots(List<string> source)
        {
            var result = new List<string>(source != null ? source.Count : 0);
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
                result.Add(source[i] != null ? source[i].Trim() : "");

            return result;
        }

        [Serializable]
        private class GetPlayerPartyRequest
        {
            public string player_profile_id;
        }

        [Serializable]
        private class GetPlayerPartyResponse
        {
            public List<GetPlayerPartyEntry> party = new List<GetPlayerPartyEntry>();
        }

        [Serializable]
        private class GetPlayerPartyEntry
        {
            public string backend_chainling_id;
            public string species_id;
            public int stars;
            public int mastery_xp;
            public int rebirth_count;
            public int evolution_stage;
            public string display_name;
            public int[] ivs;
            public int[] rebirth_iv_bonuses;
            public bool entangled_flag;
            public string entangled_type;
            public string mutation_id;
            public string visual_variant_id;
            public VisualIdentityPayload visual_identity;
            public List<string> move_ids = new List<string>();
            public List<string> modifier1_section_ids = new List<string>();
            public List<string> modifier2_section_ids = new List<string>();
            public int current_hp = ChainlingVitalsConstants.UnsyncedCurrentHp;
            public string nft_mint_address;
            public string archive_asset_id;
            public long archived_at_unix;
        }

        [Serializable]
        private class VisualIdentityPayload
        {
            public string weapon_id;
            public string aura_id;
            public string nameplate_frame_id;
            public string hud_card_variant_id;
            public string party_column_variant_id;
            public string swap_in_fx_id;
            public string swap_out_fx_id;
            public string ko_fx_id;
            public string spawn_fx_id;
            public string ko_swap_fx_id;
            public string defender_ko_fx_id;
            public string victory_pose_id;
            public string idle_style_id;
            public string idle_flourish_id;
            public string melee_attack_style_id;
            public string ranged_attack_style_id;
            public string hit_reaction_style_id;
            public string attacker_ko_style_id;
            public string enemy_ko_style_id;
        }
    }
}
