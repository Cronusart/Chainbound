using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Persists a Chainling's player-selected visual identity through profile_set_visual_identity.
    /// The full body look is controlled by visual_variant_id.
    /// </summary>
    public static class SupabaseVisualIdentityMutation
    {
        [Serializable]
        private class ProfileSetVisualIdentityRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string chainling_id;
            public string visual_variant_id;
            public VisualIdentityPayload visual_identity;
            public VisualIdentityAssetIdsPayload visual_identity_asset_ids;
        }

        [Serializable]
        private class ProfileSetVisualIdentityEnvelope
        {
            public bool success;
            public string error;
            public string player_profile_id;
            public string chainling_id;
            public string visual_variant_id;
            public VisualIdentityPayload visual_identity;
            public VisualIdentityAssetIdsPayload visual_identity_asset_ids;
        }

        [Serializable]
        private class VisualIdentityPayload
        {
            public string aura_id;
            public string hud_card_variant_id;
            public string party_column_variant_id;
            public string swap_in_fx_id;
            public string swap_out_fx_id;
            public string ko_fx_id;
            public string victory_pose_id;
            public string idle_style_id;
            public string idle_flourish_id;
            public string melee_attack_style_id;
            public string ranged_attack_style_id;
            public string hit_reaction_style_id;
            public string attacker_ko_style_id;

            public string weapon_id;
            public string nameplate_frame_id;
            public string spawn_fx_id;
            public string ko_swap_fx_id;
            public string defender_ko_fx_id;
            public string enemy_ko_style_id;
        }

        [Serializable]
        private class VisualIdentityAssetIdsPayload
        {
            public string visual_variant_id;
            public string aura_id;
            public string hud_card_variant_id;
            public string party_column_variant_id;
            public string swap_in_fx_id;
            public string swap_out_fx_id;
            public string ko_fx_id;
            public string victory_pose_id;
            public string idle_style_id;
            public string idle_flourish_id;
            public string melee_attack_style_id;
            public string ranged_attack_style_id;
            public string hit_reaction_style_id;
            public string attacker_ko_style_id;
        }

        public static async Task<(bool Ok, string ErrorMessage, ChainlingVisualIdentity VisualIdentity)> ProfileSetVisualIdentityAsync(
            SupabaseConfigSO config,
            string playerProfileId,
            string walletAddress,
            string authUserIdOrEmpty,
            string chainlingBackendId,
            ChainlingVisualIdentity visualIdentity,
            IReadOnlyCollection<string> ownedCosmeticIds = null,
            PlayerRosterSnapshot rosterCacheToPatch = null,
            ChainlingVisualIdentityDatabaseSO identityDatabase = null,
            string speciesId = null,
            IReadOnlyDictionary<string, string> cosmeticAssetIdsByCosmeticId = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(chainlingBackendId))
                return (false, "chainling_id_required", null);

            ChainlingVisualIdentity normalized = ChainlingVisualIdentity.CloneForPlayerSave(visualIdentity);
            string ownershipError = ValidateOwnedCosmetics(normalized, ownedCosmeticIds);
            if (!string.IsNullOrEmpty(ownershipError))
                return (false, ownershipError, null);
            string variantError = ValidateVariantCompatibility(normalized, identityDatabase, speciesId);
            if (!string.IsNullOrEmpty(variantError))
                return (false, variantError, null);

            string json = JsonUtility.ToJson(new ProfileSetVisualIdentityRequest
            {
                player_profile_id = playerProfileId != null ? playerProfileId.Trim() : "",
                wallet_address = walletAddress != null ? walletAddress.Trim() : "",
                auth_user_id = authUserIdOrEmpty != null ? authUserIdOrEmpty.Trim() : "",
                chainling_id = chainlingBackendId.Trim(),
                visual_variant_id = normalized.visualVariantId,
                visual_identity = ToPayload(normalized),
                visual_identity_asset_ids = ToAssetIdsPayload(normalized, cosmeticAssetIdsByCosmeticId),
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "profile_set_visual_identity", json);
            if (!response.IsSuccess)
                return (false, $"profile_set_visual_identity_http_{response.statusCode}: {response.body}", null);

            var envelope = JsonUtility.FromJson<ProfileSetVisualIdentityEnvelope>(response.body);
            if (envelope == null || !envelope.success)
            {
                string err = envelope != null && !string.IsNullOrEmpty(envelope.error)
                    ? envelope.error
                    : "profile_set_visual_identity_invalid_payload";
                return (false, err, null);
            }

            ChainlingVisualIdentity updatedIdentity = FromPayload(envelope.visual_variant_id, envelope.visual_identity);
            PatchRosterCache(rosterCacheToPatch, chainlingBackendId, updatedIdentity);
            return (true, null, updatedIdentity);
        }

        public static bool PatchRosterCache(PlayerRosterSnapshot rosterCache, string chainlingBackendId, ChainlingVisualIdentity visualIdentity)
        {
            if (rosterCache == null || rosterCache.chainlings == null || string.IsNullOrWhiteSpace(chainlingBackendId))
                return false;

            string normalizedChainlingId = chainlingBackendId.Trim();
            for (int i = 0; i < rosterCache.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = rosterCache.chainlings[i];
                ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
                if (snapshot == null || !string.Equals(snapshot.backendChainlingId, normalizedChainlingId, StringComparison.Ordinal))
                    continue;

                snapshot.visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(visualIdentity);
                return true;
            }

            return false;
        }

        private static VisualIdentityPayload ToPayload(ChainlingVisualIdentity identity)
        {
            return new VisualIdentityPayload
            {
                aura_id = identity.auraId,
                hud_card_variant_id = identity.hudCardVariantId,
                party_column_variant_id = identity.partyColumnVariantId,
                swap_in_fx_id = identity.swapInFxId,
                swap_out_fx_id = identity.swapOutFxId,
                ko_fx_id = identity.koFxId,
                victory_pose_id = identity.victoryPoseId,
                idle_style_id = identity.idleStyleId,
                idle_flourish_id = identity.idleFlourishId,
                melee_attack_style_id = identity.meleeAttackStyleId,
                ranged_attack_style_id = identity.rangedAttackStyleId,
                hit_reaction_style_id = identity.hitReactionStyleId,
                attacker_ko_style_id = identity.attackerKoStyleId,
            };
        }

        private static VisualIdentityAssetIdsPayload ToAssetIdsPayload(
            ChainlingVisualIdentity identity,
            IReadOnlyDictionary<string, string> assetIdsByCosmeticId)
        {
            if (identity == null || assetIdsByCosmeticId == null || assetIdsByCosmeticId.Count == 0)
                return new VisualIdentityAssetIdsPayload();

            return new VisualIdentityAssetIdsPayload
            {
                visual_variant_id = AssetFor(identity.visualVariantId, assetIdsByCosmeticId),
                aura_id = AssetFor(identity.auraId, assetIdsByCosmeticId),
                hud_card_variant_id = AssetFor(identity.hudCardVariantId, assetIdsByCosmeticId),
                party_column_variant_id = AssetFor(identity.partyColumnVariantId, assetIdsByCosmeticId),
                swap_in_fx_id = AssetFor(identity.swapInFxId, assetIdsByCosmeticId),
                swap_out_fx_id = AssetFor(identity.swapOutFxId, assetIdsByCosmeticId),
                ko_fx_id = AssetFor(identity.koFxId, assetIdsByCosmeticId),
                victory_pose_id = AssetFor(identity.victoryPoseId, assetIdsByCosmeticId),
                idle_style_id = AssetFor(identity.idleStyleId, assetIdsByCosmeticId),
                idle_flourish_id = AssetFor(identity.idleFlourishId, assetIdsByCosmeticId),
                melee_attack_style_id = AssetFor(identity.meleeAttackStyleId, assetIdsByCosmeticId),
                ranged_attack_style_id = AssetFor(identity.rangedAttackStyleId, assetIdsByCosmeticId),
                hit_reaction_style_id = AssetFor(identity.hitReactionStyleId, assetIdsByCosmeticId),
                attacker_ko_style_id = AssetFor(identity.attackerKoStyleId, assetIdsByCosmeticId),
            };
        }

        private static string AssetFor(string cosmeticId, IReadOnlyDictionary<string, string> assetIdsByCosmeticId)
        {
            string normalized = NormalizeOwnershipId(cosmeticId);
            if (string.IsNullOrEmpty(normalized) || IsFreeId(normalized))
                return "";

            return assetIdsByCosmeticId.TryGetValue(normalized, out string assetId) && !string.IsNullOrWhiteSpace(assetId)
                ? assetId.Trim()
                : "";
        }

        private static ChainlingVisualIdentity FromPayload(string visualVariantId, VisualIdentityPayload payload)
        {
            var identity = ChainlingVisualIdentity.CreateDefault();
            identity.visualVariantId = visualVariantId;

            if (payload != null)
            {
                identity.auraId = payload.aura_id;
                identity.hudCardVariantId = FirstString(payload.hud_card_variant_id, payload.nameplate_frame_id);
                identity.partyColumnVariantId = payload.party_column_variant_id;
                identity.swapInFxId = FirstString(payload.swap_in_fx_id, payload.spawn_fx_id);
                identity.swapOutFxId = FirstString(payload.swap_out_fx_id, payload.ko_swap_fx_id);
                identity.koFxId = FirstString(payload.ko_fx_id, payload.defender_ko_fx_id);
                identity.victoryPoseId = payload.victory_pose_id;
                identity.idleStyleId = payload.idle_style_id;
                identity.idleFlourishId = payload.idle_flourish_id;
                identity.meleeAttackStyleId = payload.melee_attack_style_id;
                identity.rangedAttackStyleId = payload.ranged_attack_style_id;
                identity.hitReactionStyleId = payload.hit_reaction_style_id;
                identity.attackerKoStyleId = FirstString(payload.attacker_ko_style_id, payload.enemy_ko_style_id);
            }

            identity.Normalize();
            return identity;
        }

        private static string ValidateOwnedCosmetics(ChainlingVisualIdentity identity, IReadOnlyCollection<string> ownedCosmeticIds)
        {
            if (ownedCosmeticIds == null)
                return null;

            var owned = new HashSet<string>(ownedCosmeticIds, StringComparer.Ordinal);
            return FirstMissingOwnedId(owned,
                identity.visualVariantId,
                identity.auraId,
                identity.hudCardVariantId,
                identity.partyColumnVariantId,
                identity.swapInFxId,
                identity.swapOutFxId,
                identity.koFxId,
                identity.victoryPoseId,
                identity.idleStyleId,
                identity.idleFlourishId,
                identity.meleeAttackStyleId,
                identity.rangedAttackStyleId,
                identity.hitReactionStyleId,
                identity.attackerKoStyleId);
        }

        private static string ValidateVariantCompatibility(
            ChainlingVisualIdentity identity,
            ChainlingVisualIdentityDatabaseSO database,
            string speciesId)
        {
            if (identity == null || database == null || string.IsNullOrWhiteSpace(speciesId))
                return null;

            string variantId = identity.visualVariantId;
            return FirstIncompatibleDefinition(speciesId, variantId,
                database.GetSwapInFx(identity.swapInFxId),
                database.GetSwapOutFx(identity.swapOutFxId),
                database.GetKoFx(identity.koFxId),
                database.GetVictoryPose(identity.victoryPoseId),
                database.GetIdleStyle(identity.idleStyleId),
                database.GetIdleFlourish(identity.idleFlourishId),
                database.GetMeleeAttackStyle(identity.meleeAttackStyleId),
                database.GetRangedAttackStyle(identity.rangedAttackStyleId),
                database.GetHitReactionStyle(identity.hitReactionStyleId),
                database.GetAttackerKoStyle(identity.attackerKoStyleId));
        }

        private static string FirstString(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
        }

        private static string FirstIncompatibleDefinition(
            string speciesId,
            string visualVariantId,
            params ChainlingVisualDefinitionSO[] definitions)
        {
            if (definitions == null)
                return null;

            for (int i = 0; i < definitions.Length; i++)
            {
                ChainlingVisualDefinitionSO definition = definitions[i];
                if (definition == null || definition.IsCompatibleWith(speciesId, visualVariantId))
                    continue;

                return $"cosmetic_variant_mismatch:{definition.Id}";
            }

            return null;
        }

        private static string FirstMissingOwnedId(HashSet<string> owned, params string[] ids)
        {
            if (ids == null)
                return null;

            for (int i = 0; i < ids.Length; i++)
            {
                string id = NormalizeOwnershipId(ids[i]);
                if (string.IsNullOrEmpty(id) || IsFreeId(id))
                    continue;

                if (!owned.Contains(id))
                    return $"cosmetic_not_owned:{id}";
            }

            return null;
        }

        private static string NormalizeOwnershipId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        private static bool IsFreeId(string id)
        {
            return string.Equals(id, ChainlingVisualIdentity.NoneId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.DefaultId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal) ||
                   string.Equals(id, ChainlingVisualIdentity.EntangledVariantId, StringComparison.Ordinal);
        }
    }
}
