using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseRebirthService : MonoBehaviour, IChainlingRebirthService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<ChainlingRebirthResult> RebirthAsync(
            string playerProfileId,
            string walletAddress,
            string chainlingId,
            CoreStat chosenStat)
        {
            if (supabaseConfig == null)
                return Failed("SupabaseConfigSO was not assigned.");

            if (string.IsNullOrWhiteSpace(chainlingId))
                return Failed("chainling_id_required");

            var session = GameSessionContext.Instance;
            string profileId = !string.IsNullOrWhiteSpace(playerProfileId)
                ? playerProfileId.Trim()
                : session != null ? session.PlayerProfileId : "";
            string wallet = !string.IsNullOrWhiteSpace(walletAddress)
                ? walletAddress.Trim()
                : session != null ? session.WalletAddress : "";
            string authUserId = session != null ? session.AuthUserId : "";
            string statId = ToStatId(chosenStat);
            string operationScope = string.Join("|", profileId, wallet, chainlingId.Trim(), statId);
            string idempotencyKey;
            try
            {
                idempotencyKey = RebirthOperationJournal.GetOrCreate(operationScope);
            }
            catch (Exception ex)
            {
                return Failed($"rebirth_journal_unavailable: {ex.Message}");
            }

            string json = JsonUtility.ToJson(new RebirthRequest
            {
                player_profile_id = profileId,
                wallet_address = wallet,
                auth_user_id = authUserId,
                chainling_id = chainlingId.Trim(),
                stat = statId,
                idempotency_key = idempotencyKey,
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, "profile_rebirth_chainling", json);
            if (!response.IsSuccess)
                return Failed($"profile_rebirth_chainling_http_{response.statusCode}: {response.body}");

            var envelope = JsonUtility.FromJson<RebirthEnvelope>(response.body);
            if (envelope == null || !envelope.success)
            {
                string error = envelope != null && !string.IsNullOrEmpty(envelope.error)
                    ? envelope.error
                    : "profile_rebirth_chainling_invalid_payload";
                return Failed(error);
            }

            RebirthOperationJournal.Complete(operationScope, idempotencyKey);

            return new ChainlingRebirthResult
            {
                success = true,
                error = "",
                ramBalance = envelope.ram_balance,
                updatedChainling = FromPayload(envelope.chainling),
            };
        }

        public static string ToStatId(CoreStat stat)
        {
            switch (stat)
            {
                case CoreStat.MaxHP: return "max_hp";
                case CoreStat.MeleeAtk: return "melee_atk";
                case CoreStat.MeleeDef: return "melee_def";
                case CoreStat.RangedAtk: return "ranged_atk";
                case CoreStat.RangedDef: return "ranged_def";
                case CoreStat.Speed: return "speed";
                default: return "max_hp";
            }
        }

        private static ChainlingSnapshot FromPayload(ChainlingPayload payload)
        {
            if (payload == null)
                return null;

            int rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(payload.rebirth_count);
            int[] ivs = ChainlingSnapshotStatUtility.CopyIvs(payload.ivs);
            int[] picks = ChainlingSnapshotStatUtility.CopyRebirthIvBonuses(payload.rebirth_iv_bonuses);
            ChainlingSnapshotStatUtility.WarnIfPickSumMismatch(
                $"rebirth '{payload.species_id}'",
                rebirthCount,
                picks);

            var snapshot = new ChainlingSnapshot
            {
                backendChainlingId = payload.backend_chainling_id,
                speciesId = payload.species_id,
                displayName = payload.display_name,
                level = 0,
                stars = ChainlingProgressionUtility.NormalizeStars(payload.stars),
                masteryXp = 0,
                rebirthCount = rebirthCount,
                evolutionStage = payload.evolution_stage > 0 ? payload.evolution_stage : 1,
                ivs = ivs,
                rebirthIvBonuses = picks,
                moveIds = new List<string>(),
                modifier1SectionIds = new List<string>(),
                modifier2SectionIds = new List<string>(),
                visualIdentity = ChainlingVisualIdentity.CreateDefault(),
                currentHp = ChainlingVitalsConstants.UnsyncedCurrentHp,
                nftMintAddress = FirstString(payload.nft_mint_address, payload.archive_asset_id),
                archivedAtUnix = Math.Max(0, payload.archived_at_unix),
            };

            snapshot.visualIdentity.visualVariantId = payload.visual_variant_id;
            if (payload.visual_identity != null)
            {
                snapshot.visualIdentity.auraId = payload.visual_identity.aura_id;
                snapshot.visualIdentity.hudCardVariantId = FirstString(payload.visual_identity.hud_card_variant_id, payload.visual_identity.nameplate_frame_id);
                snapshot.visualIdentity.partyColumnVariantId = payload.visual_identity.party_column_variant_id;
                snapshot.visualIdentity.swapInFxId = FirstString(payload.visual_identity.swap_in_fx_id, payload.visual_identity.spawn_fx_id);
                snapshot.visualIdentity.swapOutFxId = FirstString(payload.visual_identity.swap_out_fx_id, payload.visual_identity.ko_swap_fx_id);
                snapshot.visualIdentity.koFxId = FirstString(payload.visual_identity.ko_fx_id, payload.visual_identity.defender_ko_fx_id);
                snapshot.visualIdentity.victoryPoseId = payload.visual_identity.victory_pose_id;
                snapshot.visualIdentity.idleStyleId = payload.visual_identity.idle_style_id;
                snapshot.visualIdentity.idleFlourishId = payload.visual_identity.idle_flourish_id;
                snapshot.visualIdentity.meleeAttackStyleId = payload.visual_identity.melee_attack_style_id;
                snapshot.visualIdentity.rangedAttackStyleId = payload.visual_identity.ranged_attack_style_id;
                snapshot.visualIdentity.hitReactionStyleId = payload.visual_identity.hit_reaction_style_id;
                snapshot.visualIdentity.attackerKoStyleId = FirstString(payload.visual_identity.attacker_ko_style_id, payload.visual_identity.enemy_ko_style_id);
            }

            snapshot.isEntangled = payload.entangled_flag;
            snapshot.entangledType = payload.entangled_flag
                ? ChainlingTypeParseUtility.ParseOrNone(payload.entangled_type)
                : ChainlingType.None;
            snapshot.mutationId = ChainlingVisualIdentity.NormalizeId(payload.mutation_id, ChainlingVisualIdentity.NoneId);
            snapshot.isMutated = !string.Equals(
                snapshot.mutationId,
                ChainlingVisualIdentity.NoneId,
                StringComparison.Ordinal);
            if (snapshot.isMutated &&
                string.IsNullOrWhiteSpace(snapshot.visualIdentity.auraId))
            {
                snapshot.visualIdentity.auraId = snapshot.mutationId;
            }

            snapshot.visualIdentity.Normalize();
            return snapshot;
        }

        private static string FirstString(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
        }

        private static ChainlingRebirthResult Failed(string error)
        {
            return new ChainlingRebirthResult
            {
                success = false,
                error = error ?? "",
            };
        }

        [Serializable]
        private class RebirthRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string chainling_id;
            public string stat;
            public string idempotency_key;
        }

        private static class RebirthOperationJournal
        {
            private const int CurrentVersion = 1;
            private const string FileName = "chainbound_rebirth_operations_v1.json";
            private static readonly object Gate = new object();

            [Serializable]
            private sealed class Entry
            {
                public string scope;
                public string idempotencyKey;
                public long createdTicks;
            }

            [Serializable]
            private sealed class Envelope
            {
                public int version = CurrentVersion;
                public List<Entry> entries = new List<Entry>();
            }

            public static string GetOrCreate(string scope)
            {
                lock (Gate)
                {
                    Envelope journal = Load();
                    Entry existing = journal.entries.FirstOrDefault(item => item.scope == scope);
                    if (existing != null && !string.IsNullOrWhiteSpace(existing.idempotencyKey))
                        return existing.idempotencyKey;

                    var created = new Entry
                    {
                        scope = scope,
                        idempotencyKey = Guid.NewGuid().ToString("N"),
                        createdTicks = DateTime.UtcNow.Ticks,
                    };
                    journal.entries.Add(created);
                    Save(journal);
                    return created.idempotencyKey;
                }
            }

            public static void Complete(string scope, string idempotencyKey)
            {
                lock (Gate)
                {
                    Envelope journal = Load();
                    journal.entries.RemoveAll(item => item.scope == scope && item.idempotencyKey == idempotencyKey);
                    Save(journal);
                }
            }

            private static Envelope Load()
            {
                string path = Path.Combine(Application.persistentDataPath, FileName);
                if (!File.Exists(path)) return new Envelope();
                Envelope value = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if (value == null || value.version != CurrentVersion || value.entries == null)
                    throw new InvalidDataException("unsupported rebirth journal");
                return value;
            }

            private static void Save(Envelope journal)
            {
                string path = Path.Combine(Application.persistentDataPath, FileName);
                string temporaryPath = path + ".tmp";
                string backupPath = path + ".bak";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(journal), Encoding.UTF8);
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, backupPath, true);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
        }

        [Serializable]
        private class RebirthEnvelope
        {
            public bool success;
            public string error;
            public long ram_balance;
            public ChainlingPayload chainling;
        }

        [Serializable]
        private class ChainlingPayload
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
