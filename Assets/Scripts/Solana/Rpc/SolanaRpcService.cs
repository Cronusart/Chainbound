using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Solana;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Small project-owned Solana JSON-RPC client. Responses are treated as untrusted and
    /// validated before account bytes or signatures cross into gameplay services.
    /// </summary>
    public sealed class SolanaRpcService : IRpcService, IDisposable
    {
        private const int BlockhashCacheTtlSeconds = 45;
        // The newest finalized slot often has no block time yet. Nearby older slots still
        // represent the live cluster day and stay well inside a one-hour window.
        private static readonly int[] BlockTimeSlotOffsets = { 0, 30, 150, 300 };
        private readonly Uri _endpoint;
        private readonly HttpClient _httpClient;
        private string _cachedBlockhash;
        private DateTime _blockhashCachedAtUtc;
        private int _requestId;

        public SolanaRpcService(string rpcEndpoint)
        {
            if (!Uri.TryCreate(rpcEndpoint, UriKind.Absolute, out Uri endpoint))
                throw new ArgumentException("RPC endpoint must be an absolute URI.", nameof(rpcEndpoint));
            _endpoint = endpoint;
            if (_endpoint.Scheme != Uri.UriSchemeHttps && !(_endpoint.Scheme == Uri.UriSchemeHttp && _endpoint.IsLoopback))
                throw new ArgumentException("RPC endpoint must use HTTPS unless it targets localhost.", nameof(rpcEndpoint));

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        }

        public async Task<string> SendTransactionAsync(byte[] signedTransaction)
        {
            if (signedTransaction == null || signedTransaction.Length == 0)
                throw new ArgumentException("Signed transaction is required.", nameof(signedTransaction));

            JObject response = await PostAsync(
                "sendTransaction",
                new JArray(
                    Convert.ToBase64String(signedTransaction),
                    new JObject
                    {
                        ["encoding"] = "base64",
                        ["skipPreflight"] = false,
                        ["preflightCommitment"] = "confirmed",
                        ["maxRetries"] = 3,
                    }));

            string signature = response["result"]?.Value<string>();
            ValidateSignature(signature);
            return signature;
        }

        public async Task<RpcSimulationResult> SimulateTransactionAsync(byte[] unsignedTransaction)
        {
            if (unsignedTransaction == null || unsignedTransaction.Length == 0)
                return Failure("Transaction payload is empty.");

            JObject response;
            try
            {
                response = await PostAsync(
                    "simulateTransaction",
                    new JArray(
                        Convert.ToBase64String(unsignedTransaction),
                        new JObject
                        {
                            ["encoding"] = "base64",
                            ["commitment"] = "confirmed",
                            ["sigVerify"] = false,
                            ["replaceRecentBlockhash"] = false,
                        }));
            }
            catch (Exception ex)
            {
                return Failure(ex.Message);
            }

            JToken value = response["result"]?["value"];
            if (value == null || value.Type != JTokenType.Object)
                return Failure("RPC returned no simulation value.");

            string[] logs = value["logs"] is JArray logArray
                ? logArray.Values<string>().Where(log => log != null).Take(128).ToArray()
                : Array.Empty<string>();
            long units = value["unitsConsumed"]?.Value<long?>() ?? 0;
            JToken error = value["err"];
            bool success = error == null || error.Type == JTokenType.Null;

            return new RpcSimulationResult
            {
                success = success,
                errorMessage = success ? null : error.ToString(Formatting.None),
                logs = logs,
                unitsConsumed = Math.Max(0, units),
            };
        }

        public async Task<RpcConfirmationStatus> ConfirmTransactionAsync(string txSignature, int timeoutMs = 30000)
        {
            ValidateSignature(txSignature);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMs));

            while (DateTime.UtcNow < deadline)
            {
                JObject response = await PostAsync(
                    "getSignatureStatuses",
                    new JArray(
                        new JArray(txSignature),
                        new JObject { ["searchTransactionHistory"] = true }));

                JToken status = response["result"]?["value"]?.FirstOrDefault();
                if (status != null && status.Type != JTokenType.Null)
                {
                    JToken error = status["err"];
                    if (error != null && error.Type != JTokenType.Null)
                        throw new InvalidOperationException($"Transaction failed on-chain: {error.ToString(Formatting.None)}");

                    string confirmation = status["confirmationStatus"]?.Value<string>();
                    if (confirmation == "finalized") return RpcConfirmationStatus.Finalized;
                    if (confirmation == "confirmed") return RpcConfirmationStatus.Confirmed;
                }

                await Task.Delay(1500);
            }

            return RpcConfirmationStatus.TimedOut;
        }

        public async Task<RpcAccountInfo> GetAccountAsync(string publicKey)
        {
            ValidatePublicKey(publicKey);
            JObject response = await PostAsync(
                "getAccountInfo",
                new JArray(
                    publicKey,
                    new JObject { ["encoding"] = "base64", ["commitment"] = "confirmed" }));

            JToken value = response["result"]?["value"];
            if (value == null || value.Type == JTokenType.Null) return null;
            return DecodeAccount(publicKey, response, value);
        }

        public async Task<RpcAccountInfo[]> GetMultipleAccountsAsync(string[] publicKeys)
        {
            if (publicKeys == null) throw new ArgumentNullException(nameof(publicKeys));
            if (publicKeys.Length == 0) return Array.Empty<RpcAccountInfo>();
            if (publicKeys.Length > 100) throw new ArgumentException("getMultipleAccounts supports at most 100 keys per call.", nameof(publicKeys));
            foreach (string publicKey in publicKeys) ValidatePublicKey(publicKey);

            JObject response = await PostAsync(
                "getMultipleAccounts",
                new JArray(
                    new JArray(publicKeys),
                    new JObject { ["encoding"] = "base64", ["commitment"] = "confirmed" }));

            JArray values = response["result"]?["value"] as JArray;
            if (values == null || values.Count != publicKeys.Length)
                throw new InvalidOperationException("RPC returned an unexpected getMultipleAccounts result length.");

            var result = new RpcAccountInfo[values.Count];
            for (int i = 0; i < values.Count; i++)
                result[i] = values[i] == null || values[i].Type == JTokenType.Null
                    ? null
                    : DecodeAccount(publicKeys[i], response, values[i]);
            return result;
        }

        public async Task<string> GetLatestBlockhashAsync()
        {
            if (!string.IsNullOrEmpty(_cachedBlockhash) &&
                DateTime.UtcNow - _blockhashCachedAtUtc < TimeSpan.FromSeconds(BlockhashCacheTtlSeconds))
                return _cachedBlockhash;

            JObject response = await PostAsync(
                "getLatestBlockhash",
                new JArray(new JObject { ["commitment"] = "finalized" }));

            string blockhash = response["result"]?["value"]?["blockhash"]?.Value<string>();
            byte[] decoded = Base58.Decode(blockhash ?? string.Empty);
            if (decoded.Length != 32)
                throw new InvalidOperationException("RPC returned an invalid recent blockhash.");

            _cachedBlockhash = blockhash;
            _blockhashCachedAtUtc = DateTime.UtcNow;
            return blockhash;
        }

        public async Task<string> GetGenesisHashAsync()
        {
            JObject response = await PostAsync("getGenesisHash", new JArray());
            string genesisHash = response["result"]?.Value<string>();
            byte[] decoded = Base58.Decode(genesisHash ?? string.Empty);
            if (decoded.Length != 32)
                throw new InvalidOperationException("RPC returned an invalid genesis hash.");
            return genesisHash;
        }

        public async Task<long?> GetBlockTimeAsync()
        {
            try
            {
                JObject slotResponse = await PostAsync(
                    "getSlot",
                    new JArray(new JObject { ["commitment"] = "finalized" }));
                long? slot = slotResponse["result"]?.Value<long?>();
                if (!slot.HasValue || slot.Value <= 0) return null;

                for (int i = 0; i < BlockTimeSlotOffsets.Length; i++)
                {
                    long target = slot.Value - BlockTimeSlotOffsets[i];
                    if (target <= 0)
                        continue;

                    try
                    {
                        JObject timeResponse = await PostAsync("getBlockTime", new JArray(target));
                        long? blockTime = timeResponse["result"]?.Value<long?>();
                        if (blockTime.HasValue && blockTime.Value > 0)
                            return blockTime;
                    }
                    catch (Exception)
                    {
                        // The tip slot often has no timestamp yet. Try the next offset.
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SolanaRpcService] Cluster time query failed: {ex.Message}");
                return null;
            }
        }

        private async Task<JObject> PostAsync(string method, JArray parameters)
        {
            int id = Interlocked.Increment(ref _requestId);
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters ?? new JArray(),
            };

            using (var content = new StringContent(request.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage httpResponse = await _httpClient.PostAsync(_endpoint, content))
            {
                string json = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                    throw new HttpRequestException($"RPC HTTP {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}).");

                JObject response;
                try { response = JObject.Parse(json); }
                catch (JsonException ex) { throw new InvalidOperationException("RPC returned malformed JSON.", ex); }

                if (response["id"]?.Value<int?>() != id)
                    throw new InvalidOperationException("RPC response ID did not match the request.");
                if (response["error"] != null)
                {
                    string message = response["error"]?["message"]?.Value<string>() ?? "Unknown RPC error.";
                    int? code = response["error"]?["code"]?.Value<int?>();
                    throw new InvalidOperationException($"RPC {method} failed ({code?.ToString() ?? "no code"}): {message}");
                }
                return response;
            }
        }

        private static RpcAccountInfo DecodeAccount(string publicKey, JObject response, JToken account)
        {
            string encoded = account?["data"]?.FirstOrDefault()?.Value<string>();
            if (string.IsNullOrWhiteSpace(encoded))
                throw new InvalidOperationException("RPC account data was missing or not base64 encoded.");
            byte[] data;
            try { data = Convert.FromBase64String(encoded); }
            catch (FormatException ex) { throw new InvalidOperationException("RPC account data contained invalid base64.", ex); }

            string owner = account["owner"]?.Value<string>();
            ValidatePublicKey(owner);
            long slot = response["result"]?["context"]?["slot"]?.Value<long?>()
                ?? throw new InvalidOperationException("RPC account response was missing its context slot.");
            long lamports = account["lamports"]?.Value<long?>()
                ?? throw new InvalidOperationException("RPC account response was missing lamports.");
            if (slot < 0 || lamports < 0)
                throw new InvalidOperationException("RPC account response contained a negative slot or lamport value.");

            return new RpcAccountInfo
            {
                publicKey = publicKey,
                owner = owner,
                lamports = (ulong)lamports,
                executable = account["executable"]?.Value<bool?>()
                    ?? throw new InvalidOperationException("RPC account response was missing executable."),
                data = data,
                contextSlot = slot,
            };
        }

        private static void ValidatePublicKey(string value)
        {
            try { _ = new PublicKey(value); }
            catch (Exception ex) { throw new ArgumentException("Invalid Solana public key.", nameof(value), ex); }
        }

        private static void ValidateSignature(string value)
        {
            byte[] bytes;
            try { bytes = Base58.Decode(value ?? string.Empty); }
            catch (Exception ex) { throw new ArgumentException("Invalid transaction signature.", nameof(value), ex); }
            if (bytes.Length != 64) throw new ArgumentException("A transaction signature must decode to 64 bytes.", nameof(value));
        }

        private static RpcSimulationResult Failure(string message) => new RpcSimulationResult
        {
            success = false,
            errorMessage = string.IsNullOrWhiteSpace(message) ? "Simulation failed." : message,
            logs = Array.Empty<string>(),
            unitsConsumed = 0,
        };

        public void Dispose() => _httpClient.Dispose();
    }
}
