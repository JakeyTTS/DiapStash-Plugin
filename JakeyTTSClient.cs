using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public class JakeyTtsClient
    {
        private static JakeyTtsClient? _instance;
        public static JakeyTtsClient Instance => _instance ??= new JakeyTtsClient();

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;

        public event Action<string>? LogReceived;

        private JakeyTtsClient() { }

        public async Task StartAsync(string bridgeUrl)
        {
            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            try
            {
                LogReceived?.Invoke($"🔌 Connecting to JakeyTTS Core Engine at {bridgeUrl}...");
                await _webSocket.ConnectAsync(new Uri(bridgeUrl), _cts.Token);
                LogReceived?.Invoke("✅ Connected! Provisioning operational macro capabilities...");

                // 1. Send Handshake
                await SendHandshakeAsync();

                // 2. Inject Native Commands to make them accessible via chat automatically
                await InjectAutomationMacrosAsync();

                // 3. Start Receiving Stream Packets
                _ = Task.Run(ReceiveLoopAsync);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"❌ Connection exception thrown: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (_cts != null) _cts.Cancel();
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing structural plugin context", CancellationToken.None);
            }
            LogReceived?.Invoke("🛑 Core connection interface terminated.");
        }

        private async Task SendHandshakeAsync()
        {
            var handshake = new
            {
                type = "register",
                payload = new
                {
                    id = "diapstash-automation-plugin",
                    name = "DiapStash Integration Bridge",
                    version = "1.0.0",
                    protocol_version = "1.0",
                    subscriptions = new[] { "redeems" } // Explicitly subscribe to Twitch point events
                }
            };
            await SendJsonAsync(handshake);
        }

        private async Task InjectAutomationMacrosAsync()
        {
            // Inject standard response commands into the main app automatically
            await SendJsonAsync(new { type = "inject_command", payload = new { trigger = "!stash", response = "Check stock using !stock or status via !change" } });

            // Inject specialized semantic interception tag variables
            await SendJsonAsync(new { type = "inject_tag", payload = new { tag_name = "diapstash_stock" } });
            await SendJsonAsync(new { type = "inject_tag", payload = new { tag_name = "diapstash_change" } });

            // Re-inject updated command properties wrapping custom syntax token targets
            await SendJsonAsync(new { type = "inject_command", payload = new { trigger = "!stock", response = "[diapstash_stock]" } });
            await SendJsonAsync(new { type = "inject_command", payload = new { trigger = "!change", response = "[diapstash_change]" } });

            LogReceived?.Invoke("📡 Automation macros mapping successfully injected into the local server.");
        }

        /// <summary>
        /// Packages and serializes a runtime injection frame command targeting the core JakeyTTS NavigationView host workspace.
        /// </summary>
        public async Task InjectRemoteDashboardUiFrameAsync(string embedUrl)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                LogReceived?.Invoke("⚠️ Cannot inject UI frame: WebSocket connection is not open.");
                return;
            }

            var uiPayloadPacket = new
            {
                type = "inject_ui_page",
                payload = new
                {
                    embed_url = embedUrl
                }
            };

            LogReceived?.Invoke($"📡 Packaging UI injection payload targeting endpoint: {embedUrl}");
            await SendJsonAsync(uiPayloadPacket);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024 * 16];

            while (_webSocket?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                try
                {
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    string message = Encoding.UTF8.GetString(ms.ToArray());
                    if (string.IsNullOrWhiteSpace(message)) continue;

                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;
                    string type = root.GetProperty("type").GetString() ?? "";

                    // Acknowledge dynamic system tag triggers dispatched from JakeyTTS Core
                    if (type == "tag_triggered")
                    {
                        string tag = root.GetProperty("tag").GetString()!;
                        LogReceived?.Invoke($"🎯 Syntax Match: Local token context activated -> [{tag}]");

                        _ = Task.Run(async () => {
                            if (tag == "diapstash_stock")
                            {
                                string outMessage = await DiapStashClient.Instance.FetchCurrentStockSummaryAsync();
                                await SendJsonAsync(new { type = "speak_request", payload = new { text = outMessage } });
                            }
                            else if (tag == "diapstash_change")
                            {
                                string outMessage = await DiapStashClient.Instance.FetchLatestChangeStateAsync();
                                await SendJsonAsync(new { type = "speak_request", payload = new { text = outMessage } });
                            }
                        });
                    }
                    // Handle dynamic Channel Point Reward Redemptions (Redeems)
                    else if (type == "event_broadcast" && root.GetProperty("scope").GetString() == "redeems")
                    {
                        var payload = root.GetProperty("payload");
                        string text = payload.GetProperty("text").GetString()!;

                        // Match an event redeem pattern string like "[diapstash_stock]" or text triggers
                        if (text.Contains("!stock") || text.Contains("[diapstash_stock]"))
                        {
                            string outMessage = await DiapStashClient.Instance.FetchCurrentStockSummaryAsync();
                            await SendJsonAsync(new { type = "speak_request", payload = new { text = outMessage } });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke($"❌ Pipeline stream tracking exception: {ex.Message}");
                }
            }
        }

        private async Task SendJsonAsync(object data)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
            string json = JsonSerializer.Serialize(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}