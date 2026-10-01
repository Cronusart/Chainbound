using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Authenticated, read-only client for the selected Ownership Spine DAS proxy.
    /// The Helius endpoint and key remain server-side; Unity supplies only its
    /// existing public anon API key and process-memory user access token.
    /// </summary>
    public sealed class OwnershipDasProxyClient
    {
        public const string FunctionName = "ownership_das_proxy";
        private const int MaximumResponseCharacters = 2 * 1024 * 1024;

        private readonly SupabaseConfigSO supabaseConfig;
        private readonly SolanaConfigSO solanaConfig;

        public OwnershipDasProxyClient(SupabaseConfigSO backend, SolanaConfigSO chain)
        {
            supabaseConfig = PlatformContext.ResolveSupabaseConfig(backend)
                ?? throw new ArgumentNullException(nameof(backend));
            solanaConfig = PlatformContext.ResolveSolanaConfig(chain)
                ?? throw new ArgumentNullException(nameof(chain));
        }

        public async Task<OwnershipDasAsset> GetAssetAsync(string assetId)
        {
            RequestContext context = RequireRequestContext();
            string normalizedId = RequirePublicKey(assetId, nameof(assetId));
            var request = new GetAssetRequest
            {
                method = "getAsset",
                @params = new GetAssetParams { id = normalizedId },
            };
            string json = await PostAsync(JsonUtility.ToJson(request), context.AccessToken);
            return OwnershipDasResponseParser.ParseAsset(
                json,
                normalizedId,
                context.MovesCollection,
                context.MovesAuthority);
        }

        public async Task<OwnershipDasCollectionPage> GetMovesAsync(int page = 1, int limit = 20)
        {
            RequestContext context = RequireRequestContext();
            int normalizedPage = Mathf.Clamp(page, 1, 1000);
            int normalizedLimit = Mathf.Clamp(limit, 1, 50);
            var request = new GetAssetsByGroupRequest
            {
                method = "getAssetsByGroup",
                @params = new GetAssetsByGroupParams
                {
                    groupKey = "collection",
                    groupValue = context.MovesCollection,
                    page = normalizedPage,
                    limit = normalizedLimit,
                },
            };
            string json = await PostAsync(JsonUtility.ToJson(request), context.AccessToken);
            return OwnershipDasResponseParser.ParseCollectionPage(
                json,
                context.MovesCollection,
                context.MovesAuthority,
                normalizedPage,
                normalizedLimit);
        }

        private async Task<string> PostAsync(string body, string accessToken)
        {
            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                supabaseConfig,
                FunctionName,
                body,
                accessToken);
            if (!response.IsSuccess)
            {
                string code = OwnershipDasResponseParser.TryReadError(response.body);
                throw new InvalidOperationException(
                    $"{FunctionName} returned HTTP {response.statusCode}: {code}");
            }

            if (string.IsNullOrWhiteSpace(response.body) || response.body.Length > MaximumResponseCharacters)
                throw new InvalidOperationException("Ownership DAS proxy returned an invalid response size.");
            return response.body;
        }

        private RequestContext RequireRequestContext()
        {
            string expectedCluster = OwnershipDeploymentProfileRules.ExpectedCluster(
                PlatformContext.ResolveOwnershipEnvironment());
            if (!string.Equals(
                    OwnershipDeploymentProfileRules.NormalizeCluster(solanaConfig.ownershipCluster),
                    expectedCluster,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("Ownership DAS cluster does not match the selected deployment profile.");

            if (!Uri.TryCreate(supabaseConfig.supabaseUrl, UriKind.Absolute, out Uri projectEndpoint)
                || !Uri.TryCreate(supabaseConfig.ResolvedEdgeFunctionBaseUrl, UriKind.Absolute, out Uri endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps
                || !string.Equals(endpoint.Host, projectEndpoint.Host, StringComparison.OrdinalIgnoreCase)
                || !endpoint.AbsolutePath.TrimEnd('/').Equals("/functions/v1", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ownership DAS proxy must use the selected HTTPS Supabase project endpoint.");

            if (string.IsNullOrWhiteSpace(supabaseConfig.supabaseAnonKey))
                throw new InvalidOperationException("The public Supabase anon key is unavailable.");

            string accessToken = SupabaseAuthSessionContext.AccessToken?.Trim();
            if (string.IsNullOrWhiteSpace(accessToken)
                || string.Equals(accessToken, supabaseConfig.supabaseAnonKey.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("An authenticated Supabase user session is required.");

            return new RequestContext
            {
                AccessToken = accessToken,
                MovesCollection = RequirePublicKey(solanaConfig.coreMovesCollectionId, "coreMovesCollectionId"),
                MovesAuthority = RequirePublicKey(solanaConfig.coreMovesUpdateAuthority, "coreMovesUpdateAuthority"),
            };
        }

        private static string RequirePublicKey(string value, string fieldName)
        {
            string normalized = value?.Trim() ?? string.Empty;
            try
            {
                _ = new PublicKey(normalized);
                return normalized;
            }
            catch (Exception exception)
            {
                throw new ArgumentException($"{fieldName} must be a valid Solana public key.", fieldName, exception);
            }
        }

        private sealed class RequestContext
        {
            public string AccessToken;
            public string MovesCollection;
            public string MovesAuthority;
        }

        [Serializable] private sealed class GetAssetRequest { public string method; public GetAssetParams @params; }
        [Serializable] private sealed class GetAssetParams { public string id; }
        [Serializable] private sealed class GetAssetsByGroupRequest { public string method; public GetAssetsByGroupParams @params; }
        [Serializable] private sealed class GetAssetsByGroupParams
        {
            public string groupKey;
            public string groupValue;
            public int page;
            public int limit;
        }
    }

    public sealed class OwnershipDasAsset
    {
        public string id;
        public string interfaceName;
        public string owner;
        public string collection;
        public string updateAuthority;
    }

    public sealed class OwnershipDasCollectionPage
    {
        public int reportedTotal;
        public int page;
        public int limit;
        public int droppedItems;
        public IReadOnlyList<OwnershipDasAsset> items;
    }

    /// <summary>Strict parser for untrusted proxy/DAS responses.</summary>
    public static class OwnershipDasResponseParser
    {
        public static OwnershipDasAsset ParseAsset(
            string json,
            string expectedId,
            string expectedCollection,
            string expectedAuthority)
        {
            ProxyEnvelopeDto envelope = ParseEnvelope(json);
            if (!envelope.success || envelope.result == null)
                throw new InvalidOperationException("Ownership DAS proxy did not return an asset.");

            if (!TryValidateAsset(
                    envelope.result,
                    expectedId,
                    expectedCollection,
                    expectedAuthority,
                    out OwnershipDasAsset asset))
                throw new InvalidOperationException("Ownership DAS proxy returned an untrusted asset.");
            return asset;
        }

        public static OwnershipDasCollectionPage ParseCollectionPage(
            string json,
            string expectedCollection,
            string expectedAuthority,
            int expectedPage,
            int expectedLimit)
        {
            ProxyEnvelopeDto envelope = ParseEnvelope(json);
            ResultDto result = envelope.result;
            if (!envelope.success || result == null || result.total < 0
                || result.page != expectedPage || result.limit != expectedLimit
                || result.limit < 1 || result.limit > 50)
                throw new InvalidOperationException("Ownership DAS proxy returned an invalid collection page.");

            var trusted = new List<OwnershipDasAsset>();
            int dropped = 0;
            AssetDto[] items = result.items ?? Array.Empty<AssetDto>();
            for (int i = 0; i < items.Length; i++)
            {
                if (TryValidateAsset(
                        items[i],
                        null,
                        expectedCollection,
                        expectedAuthority,
                        out OwnershipDasAsset asset))
                    trusted.Add(asset);
                else
                    dropped++;
            }

            return new OwnershipDasCollectionPage
            {
                reportedTotal = result.total,
                page = result.page,
                limit = result.limit,
                droppedItems = dropped,
                items = trusted,
            };
        }

        public static string TryReadError(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "empty_response";
            try
            {
                ProxyEnvelopeDto envelope = JsonUtility.FromJson<ProxyEnvelopeDto>(json);
                return string.IsNullOrWhiteSpace(envelope?.error) ? "request_failed" : envelope.error.Trim();
            }
            catch (ArgumentException)
            {
                return "invalid_error_response";
            }
        }

        private static ProxyEnvelopeDto ParseEnvelope(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Ownership DAS proxy returned an empty response.");
            try
            {
                return JsonUtility.FromJson<ProxyEnvelopeDto>(json)
                       ?? throw new InvalidOperationException("Ownership DAS proxy returned invalid JSON.");
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Ownership DAS proxy returned invalid JSON.", exception);
            }
        }

        private static bool TryValidateAsset(
            AssetDto dto,
            string expectedId,
            string expectedCollection,
            string expectedAuthority,
            out OwnershipDasAsset asset)
        {
            asset = null;
            if (dto == null || !IsPublicKey(dto.id)) return false;
            if (!string.IsNullOrWhiteSpace(expectedId)
                && !string.Equals(dto.id, expectedId, StringComparison.Ordinal)) return false;

            string interfaceName = dto.@interface?.Trim() ?? string.Empty;
            bool isCollection = string.Equals(dto.id, expectedCollection, StringComparison.Ordinal);
            if (isCollection)
            {
                if (!string.Equals(interfaceName, "MplCoreCollection", StringComparison.Ordinal)
                    || !ContainsAuthority(dto.authorities, expectedAuthority)) return false;
            }
            else
            {
                if (!string.Equals(interfaceName, "MplCoreAsset", StringComparison.Ordinal)
                    || !ContainsGroup(dto.grouping, expectedCollection)
                    || !ContainsAuthority(dto.authorities, expectedAuthority)) return false;
            }

            asset = new OwnershipDasAsset
            {
                id = dto.id,
                interfaceName = interfaceName,
                owner = dto.ownership?.owner?.Trim() ?? string.Empty,
                collection = isCollection ? expectedCollection : expectedCollection,
                updateAuthority = expectedAuthority,
            };
            return true;
        }

        private static bool ContainsAuthority(AuthorityDto[] authorities, string expected)
        {
            if (authorities == null) return false;
            for (int i = 0; i < authorities.Length; i++)
            {
                if (string.Equals(authorities[i]?.address?.Trim(), expected, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ContainsGroup(GroupingDto[] groups, string expectedCollection)
        {
            if (groups == null) return false;
            for (int i = 0; i < groups.Length; i++)
            {
                if (string.Equals(groups[i]?.group_key?.Trim(), "collection", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(groups[i]?.group_value?.Trim(), expectedCollection, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsPublicKey(string value)
        {
            try
            {
                _ = new PublicKey(value?.Trim() ?? string.Empty);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [Serializable] private sealed class ProxyEnvelopeDto
        {
            public bool success;
            public string error;
            public ResultDto result;
        }

        [Serializable] private sealed class ResultDto : AssetDto
        {
            public int total;
            public int page;
            public int limit;
            public AssetDto[] items;
        }

        [Serializable] private class AssetDto
        {
            public string id;
            public string @interface;
            public OwnershipDto ownership;
            public AuthorityDto[] authorities;
            public GroupingDto[] grouping;
        }

        [Serializable] private sealed class OwnershipDto { public string owner; }
        [Serializable] private sealed class AuthorityDto { public string address; }
        [Serializable] private sealed class GroupingDto { public string group_key; public string group_value; }
    }
}
