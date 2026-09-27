using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Loads the full owned Chainling roster and equipped move slots from Supabase via profile_get_chainlings.
    /// </summary>
    public class SupabasePlayerRosterProvider : MonoBehaviour, IPlayerRosterProvider
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public PlayerRosterSnapshot CachedRoster { get; private set; }
        public string LastError { get; private set; }

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<PlayerRosterSnapshot> FetchRosterAsync(string playerProfileId, string walletAddress)
        {
            LastError = "";
            CachedRoster = null;

            if (supabaseConfig == null)
                return Fail("SupabaseConfigSO was not assigned.");

            string profileId = !string.IsNullOrWhiteSpace(playerProfileId)
                ? playerProfileId.Trim()
                : (GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "");
            string wallet = !string.IsNullOrWhiteSpace(walletAddress)
                ? walletAddress.Trim()
                : (GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "");

            if (string.IsNullOrWhiteSpace(profileId) && string.IsNullOrWhiteSpace(wallet))
                return Fail("Player profile id or wallet address was empty.");

            try
            {
                string json = JsonUtility.ToJson(new RosterRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, "profile_get_chainlings", json);
                if (!response.IsSuccess)
                    return Fail($"profile_get_chainlings returned {response.statusCode}: {response.body}");

                RosterResponse payload = JsonUtility.FromJson<RosterResponse>(response.body);
                if (payload == null)
                    return Fail("profile_get_chainlings returned an invalid payload.");

                var snapshot = new PlayerRosterSnapshot
                {
                    playerProfileId = payload.player_profile_id,
                };

                List<RosterEntryPayload> entries = payload.chainlings ?? new List<RosterEntryPayload>();
                for (int i = 0; i < entries.Count; i++)
                {
                    RosterEntryPayload entry = entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.species_id))
                        continue;

                    int stars = ChainlingProgressionUtility.NormalizeStars(entry.stars);
                    int rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(entry.rebirth_count);
                    ChainlingPveServiceRecord pveRecord = ResolvePveServiceRecord(entry);
                    int[] ivs = ChainlingSnapshotStatUtility.CopyIvs(entry.ivs);
                    int[] picks = ChainlingSnapshotStatUtility.CopyRebirthIvBonuses(entry.rebirth_iv_bonuses);
                    ChainlingSnapshotStatUtility.WarnIfPickSumMismatch(
                        $"roster '{entry.species_id}'",
                        rebirthCount,
                        picks);

                    snapshot.chainlings.Add(new PlayerRosterEntry
                    {
                        isPartyMember = entry.is_party_member,
                        partySlot = entry.party_slot,
                        masteryXp = entry.mastery_xp,
                        pveServiceRecord = pveRecord,
                        battleStats = new ChainlingBattleStats
                        {
                            battlesWon = pveRecord.totalVictories > 0 ? pveRecord.totalVictories : entry.battles_won,
                            battlesLost = entry.battles_lost,
                            kosScored = pveRecord.enemyKos > 0 ? pveRecord.enemyKos : entry.kos_scored,
                            timesKod = entry.times_kod,
                            timesCaptured = entry.times_captured,
                        },
                        snapshot = new ChainlingSnapshot
                        {
                            backendChainlingId = entry.backend_chainling_id,
                            speciesId = entry.species_id,
                            displayName = entry.display_name,
                            level = 0,
                            stars = stars,
                            masteryXp = entry.mastery_xp,
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
                                        !string.Equals(entry.mutation_id.Trim(), ChainlingVisualIdentity.NoneId, StringComparison.OrdinalIgnoreCase),
                            mutationId = ChainlingVisualIdentity.NormalizeId(entry.mutation_id, ChainlingVisualIdentity.NoneId),
                        },
                    });
                }

                CachedRoster = snapshot;
                if (enableDebugLogs)
                    Debug.Log($"[SupabasePlayerRosterProvider] Loaded {snapshot.Count} chainling(s) for profile={snapshot.playerProfileId}.");

                return CachedRoster;
            }
            catch (Exception ex)
            {
                return Fail($"FetchRosterAsync exception: {ex.Message}");
            }
        }

        private PlayerRosterSnapshot Fail(string reason)
        {
            LastError = reason;
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabasePlayerRosterProvider] {reason}", this);
            return null;
        }

        private static ChainlingVisualIdentity ResolveVisualIdentity(RosterEntryPayload entry)
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
                !string.Equals(entry.mutation_id.Trim(), ChainlingVisualIdentity.NoneId, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(identity.auraId))
            {
                identity.auraId = entry.mutation_id.Trim();
            }

            identity.Normalize();
            return identity;
        }

        private static ChainlingPveServiceRecord ResolvePveServiceRecord(RosterEntryPayload entry)
        {
            if (entry?.pve_service_record == null)
                return new ChainlingPveServiceRecord();

            PveServiceRecordPayload payload = entry.pve_service_record;
            return new ChainlingPveServiceRecord
            {
                serviceRecordVersion = payload.service_record_version > 0
                    ? payload.service_record_version
                    : ChainlingPveServiceRecord.CurrentVersion,
                totalVictories = Math.Max(0, payload.total_victories),
                xpEarnedLifetime = Math.Max(0, payload.xp_earned_lifetime),
                progressionMovesGranted = Math.Max(0, payload.progression_moves_granted),
                damageDealt = Math.Max(0, payload.damage_dealt),
                damageTaken = Math.Max(0, payload.damage_taken),
                damageMitigated = Math.Max(0, payload.damage_mitigated),
                healingTotal = Math.Max(0, payload.healing_total),
                enemyKos = Math.Max(0, payload.enemy_kos),
            };
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
        private class RosterRequest
        {
            public string player_profile_id;
            public string wallet_address;
        }

        [Serializable]
        private class RosterResponse
        {
            public string player_profile_id;
            public List<RosterEntryPayload> chainlings = new List<RosterEntryPayload>();
        }

        [Serializable]
        private class RosterEntryPayload
        {
            public string backend_chainling_id;
            public string species_id;
            public int stars;
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
            public bool is_party_member;
            public int party_slot;
            public int mastery_xp;
            public int battles_won;
            public int battles_lost;
            public int kos_scored;
            public int times_kod;
            public int times_captured;
            public PveServiceRecordPayload pve_service_record;
            public List<string> move_ids = new List<string>();
            public List<string> modifier1_section_ids = new List<string>();
            public List<string> modifier2_section_ids = new List<string>();
            public int current_hp = ChainlingVitalsConstants.UnsyncedCurrentHp;
            public string nft_mint_address;
            public string archive_asset_id;
            public long archived_at_unix;
        }

        [Serializable]
        private class PveServiceRecordPayload
        {
            public int service_record_version;
            public int total_victories;
            public long xp_earned_lifetime;
            public int progression_moves_granted;
            public long damage_dealt;
            public long damage_taken;
            public long damage_mitigated;
            public long healing_total;
            public int enemy_kos;
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
