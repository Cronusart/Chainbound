#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Editor/standalone wallet bridge that opens a browser page for real wallet
    /// connection (Phantom, Solflare, etc.). Uses HttpListener (same pattern as
    /// a local devnet connector with HTTP polling — no WebSocket required.
    /// </summary>
    public sealed class DesktopWalletBridge : IDisposable
    {
        private static DesktopWalletBridge _instance;
        public static DesktopWalletBridge Instance => _instance ??= new DesktopWalletBridge();

        private const int DefaultPort = 18821;
        private const int PortRange = 10;
        internal const string UseInGameSentinel = "__USE_INGAME__";

        private HttpListener _listener;
        private int _port;
        private volatile bool _running;

        private string _walletPublicKey;
        private string _walletName;

        private TaskCompletionSource<string> _connectTcs;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
        private readonly ConcurrentQueue<string> _pendingRequests = new();

        public bool IsConnected => _walletPublicKey != null;
        public string PublicKey => _walletPublicKey;
        public string WalletName => _walletName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance?.Dispose();
            _instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public async Task<string> ConnectAsync(int timeoutMs = 300_000)
        {
            if (IsConnected) return _walletPublicKey;
            if (!_running) StartServer();
            if (!_running) return null;

            _connectTcs = new TaskCompletionSource<string>();

            string url = $"http://localhost:{_port}/";
            Debug.Log($"[DesktopWalletBridge] Opening wallet bridge at {url}");
            Application.OpenURL(url);

            var completed = await Task.WhenAny(
                _connectTcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (completed != _connectTcs.Task)
            {
                _connectTcs = null;
                throw new TimeoutException("Wallet connection timed out.");
            }

            return await _connectTcs.Task.ConfigureAwait(false);
        }

        public async Task<byte[]> SignTransactionAsync(byte[] txBytes)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No wallet connected via bridge.");

            string id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<string>();
            _pending[id] = tcs;

            string payload = Convert.ToBase64String(txBytes);
            _pendingRequests.Enqueue(
                $"{{\"type\":\"sign_transaction\",\"id\":\"{id}\",\"payload\":\"{payload}\"}}");

            var completed = await Task.WhenAny(
                tcs.Task, Task.Delay(120_000)).ConfigureAwait(false);
            _pending.TryRemove(id, out _);

            if (completed != tcs.Task)
                throw new TimeoutException("Transaction signing timed out.");

            return Convert.FromBase64String(await tcs.Task.ConfigureAwait(false));
        }

        public async Task<byte[]> SignMessageAsync(byte[] messageBytes)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No wallet connected via bridge.");

            string id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<string>();
            _pending[id] = tcs;

            string payload = Convert.ToBase64String(messageBytes);
            _pendingRequests.Enqueue(
                $"{{\"type\":\"sign_message\",\"id\":\"{id}\",\"payload\":\"{payload}\"}}");

            var completed = await Task.WhenAny(
                tcs.Task, Task.Delay(120_000)).ConfigureAwait(false);
            _pending.TryRemove(id, out _);

            if (completed != tcs.Task)
                throw new TimeoutException("Message signing timed out.");

            return Convert.FromBase64String(await tcs.Task.ConfigureAwait(false));
        }

        public Task DisconnectAsync()
        {
            _walletPublicKey = null;
            _walletName = null;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            StopServer();
            _connectTcs?.TrySetCanceled();
            foreach (var kv in _pending) kv.Value.TrySetCanceled();
            _pending.Clear();
            while (_pendingRequests.TryDequeue(out _)) { }
            if (_instance == this) _instance = null;
        }

        // ── Server Lifecycle ─────────────────────────────────────────────────

        private void StartServer()
        {
            if (_running) return;

            for (int i = 0; i < PortRange; i++)
            {
                try
                {
                    _port = DefaultPort + i;
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{_port}/");
                    _listener.Start();
                    _listener.BeginGetContext(OnHttpRequest, null);
                    _running = true;
                    Debug.Log($"[DesktopWalletBridge] HTTP server started on port {_port}");
                    return;
                }
                catch (Exception)
                {
                    try { _listener?.Close(); } catch { /* ignore */ }
                    _listener = null;
                }
            }

            Debug.LogError("[DesktopWalletBridge] Failed to start HTTP server on any port.");
        }

        private void StopServer()
        {
            _running = false;
            try { _listener?.Stop(); } catch { /* ignore */ }
            try { _listener?.Close(); } catch { /* ignore */ }
            _listener = null;
            _walletPublicKey = null;
            _walletName = null;
        }

        // ── HTTP Request Dispatch (APM pattern) ──────────────────────────────

        private void OnHttpRequest(IAsyncResult result)
        {
            if (!_running || _listener == null) return;

            HttpListenerContext context;
            try { context = _listener.EndGetContext(result); }
            catch (ObjectDisposedException) { return; }
            catch (HttpListenerException) { return; }

            // Immediately start listening for the next request
            try { _listener.BeginGetContext(OnHttpRequest, null); }
            catch { /* server stopping */ }

            try { HandleRequest(context); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DesktopWalletBridge] Request error: {ex.Message}");
                try { context.Response.StatusCode = 500; context.Response.Close(); }
                catch { /* ignore */ }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            resp.AddHeader("Access-Control-Allow-Origin", "*");
            resp.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                SendText(resp, 200, "");
                return;
            }

            string path = req.Url.LocalPath;

            if (path == "/" && req.HttpMethod == "GET")
            {
                ServeHtml(resp);
            }
            else if (path == "/api/connect" && req.HttpMethod == "POST")
            {
                ApiConnect(req, resp);
            }
            else if (path == "/api/use-test-wallet" && req.HttpMethod == "POST")
            {
                ApiUseTestWallet(resp);
            }
            else if (path == "/api/disconnect" && req.HttpMethod == "POST")
            {
                ApiDisconnect(resp);
            }
            else if (path == "/api/pending" && req.HttpMethod == "GET")
            {
                ApiPending(resp);
            }
            else if (path == "/api/sign-result" && req.HttpMethod == "POST")
            {
                ApiSignResult(req, resp);
            }
            else
            {
                SendJson(resp, 404, "{\"error\":\"not found\"}");
            }
        }

        // ── API Handlers ──────────────────────────────────────────────────────

        private void ServeHtml(HttpListenerResponse resp)
        {
            string html = LoadBridgeHtml();
            byte[] body = Encoding.UTF8.GetBytes(html);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = body.Length;
            resp.OutputStream.Write(body, 0, body.Length);
            resp.Close();
        }

        private void ApiConnect(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            _walletPublicKey = ExtractJsonString(body, "publicKey");
            _walletName = ExtractJsonString(body, "walletName") ?? "Browser Wallet";

            Debug.Log($"[DesktopWalletBridge] Wallet connected: {_walletName} ({_walletPublicKey})");
            _connectTcs?.TrySetResult(_walletPublicKey);

            SendJson(resp, 200, "{\"ok\":true}");
        }

        private void ApiUseTestWallet(HttpListenerResponse resp)
        {
            Debug.Log("[DesktopWalletBridge] User chose test keypair.");
            _connectTcs?.TrySetResult(UseInGameSentinel);
            SendJson(resp, 200, "{\"ok\":true}");
        }

        private void ApiDisconnect(HttpListenerResponse resp)
        {
            Debug.Log("[DesktopWalletBridge] Wallet disconnected from browser.");
            _walletPublicKey = null;
            _walletName = null;
            SendJson(resp, 200, "{\"ok\":true}");
        }

        private void ApiPending(HttpListenerResponse resp)
        {
            if (_pendingRequests.TryDequeue(out string pending))
            {
                SendJson(resp, 200, pending);
            }
            else
            {
                SendJson(resp, 200, "{\"type\":\"none\"}");
            }
        }

        private void ApiSignResult(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            string id = ExtractJsonString(body, "id");
            string type = ExtractJsonString(body, "type");

            if (id != null && _pending.TryRemove(id, out var tcs))
            {
                if (type == "error")
                {
                    string msg = ExtractJsonString(body, "message") ?? "Signing rejected";
                    tcs.TrySetException(new Exception(msg));
                }
                else
                {
                    string data = ExtractJsonString(body, "signedPayload")
                               ?? ExtractJsonString(body, "signature");
                    tcs.TrySetResult(data);
                }
            }

            SendJson(resp, 200, "{\"ok\":true}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string LoadBridgeHtml()
        {
            string path = Path.Combine(Application.streamingAssetsPath,
                "SolanaWalletBridge", "bridge.html");
            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);

            Debug.LogWarning($"[DesktopWalletBridge] bridge.html not found at: {path}");
            return "<html><body style='background:#111;color:#eee;font-family:sans-serif;" +
                   "display:flex;align-items:center;justify-content:center;height:100vh'>" +
                   "<h2>bridge.html not found</h2></body></html>";
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            return reader.ReadToEnd();
        }

        private static void SendJson(HttpListenerResponse resp, int status, string json)
        {
            resp.StatusCode = status;
            resp.ContentType = "application/json; charset=utf-8";
            byte[] body = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = body.Length;
            resp.OutputStream.Write(body, 0, body.Length);
            resp.Close();
        }

        private static void SendText(HttpListenerResponse resp, int status, string text)
        {
            resp.StatusCode = status;
            byte[] body = Encoding.UTF8.GetBytes(text);
            resp.ContentLength64 = body.Length;
            resp.OutputStream.Write(body, 0, body.Length);
            resp.Close();
        }

        private static string ExtractJsonString(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*?)\"");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
#endif
