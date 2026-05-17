using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        // Colección jerárquica de bloques de tarjetas complejas
        public ObservableCollection<TtsComplexRuleCard> ComplexRuleCards { get; } = new ObservableCollection<TtsComplexRuleCard>();

        private JakeyTtsClient() { }

        public void LoadRulesFromSettings()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("SavedComplexRulesMatrixJSON", out var jsonRaw))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<TtsComplexRuleCard>>(jsonRaw.ToString());
                    ComplexRuleCards.Clear();
                    if (list != null)
                    {
                        foreach (var card in list) ComplexRuleCards.Add(card);
                    }
                }
                catch { }
            }
        }

        public void SaveRulesToSettings()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedComplexRulesMatrixJSON"] = JsonSerializer.Serialize(ComplexRuleCards);
        }

        // RESTAURADO: Método de inicio de ciclo de conexión WebSocket
        public async Task StartAsync(string bridgeUrl)
        {
            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            try
            {
                LogReceived?.Invoke($"🔌 Connecting to JakeyTTS Core Engine at {bridgeUrl}...");
                await _webSocket.ConnectAsync(new Uri(bridgeUrl), _cts.Token);
                LogReceived?.Invoke("✅ Connected! Provisioning system variable injection pipelines...");

                await SendHandshakeAsync();

                _ = Task.Run(async () => await SynchronizeJakeyGlobalVariablesAsync());
                _ = Task.Run(ReceiveLoopAsync);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"❌ Connection exception thrown: {ex.Message}");
            }
        }

        // RESTAURADO: Método de parada y cierre seguro de sockets asíncronos
        public async Task StopAsync()
        {
            if (_cts != null) _cts.Cancel();
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing structural plugin context", CancellationToken.None);
                }
                catch { }
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
                    subscriptions = new[] { "redeems", "commands" }
                }
            };
            await SendJsonAsync(handshake);
        }

        public async Task SynchronizeJakeyGlobalVariablesAsync()
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            var state = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();
            if (state == null) return;

            string elapsedString = "0 minutes";
            if (state.StartTime != DateTime.MinValue)
            {
                DateTime localStart = state.StartTime.ToLocalTime();
                TimeSpan diff = state.IsActiveSession ? (DateTime.Now - localStart) : (state.EndTime.Value.ToLocalTime() - localStart);
                elapsedString = $"{(int)diff.TotalHours} hours and {diff.Minutes} minutes";
            }

            // Compilación e individualización de cada bloque de tarjeta como una macro {} independiente
            var dynamicCardTokens = new Dictionary<string, string>();

            foreach (var card in ComplexRuleCards)
            {
                if (card.Clauses.Count == 0) continue;

                bool blockEvaluatedAndPassed = false;
                bool currentChainIsMatch = false;
                StringBuilder cardOutputBuilder = new StringBuilder();

                foreach (var clause in card.Clauses)
                {
                    bool clauseIsTrue = false;
                    string actualValue = clause.TargetVariable switch
                    {
                        "Leak" => state.HasLeak ? "YES" : "NO",
                        "Blowout" => (state.HasLeak && state.MessyLevel >= 2) ? "YES" : "NO",
                        "Status" => state.IsActiveSession ? "Active" : "Completed",
                        "Wetness" => state.Wetness.ToString(),
                        "Messy" => state.MessyLevel.ToString(),
                        _ => ""
                    };

                    if (int.TryParse(actualValue, out int currentNum) && int.TryParse(clause.TargetValue, out int targetNum))
                    {
                        clauseIsTrue = clause.ConditionType switch
                        {
                            "Equals" => currentNum == targetNum,
                            "GreaterThan" => currentNum > targetNum,
                            "LessThan" => currentNum < targetNum,
                            _ => false
                        };
                    }
                    else
                    {
                        clauseIsTrue = string.Equals(actualValue, clause.TargetValue, StringComparison.OrdinalIgnoreCase);
                    }

                    if (clause.LogicalOperator == "IF")
                    {
                        currentChainIsMatch = clauseIsTrue;
                        if (currentChainIsMatch)
                        {
                            blockEvaluatedAndPassed = true;
                            if (!string.IsNullOrWhiteSpace(clause.OutputMessage)) cardOutputBuilder.Append(clause.OutputMessage);
                        }
                    }
                    else if (clause.LogicalOperator == "AND")
                    {
                        currentChainIsMatch = currentChainIsMatch && clauseIsTrue;
                        if (!currentChainIsMatch) blockEvaluatedAndPassed = false;
                        if (currentChainIsMatch && !string.IsNullOrWhiteSpace(clause.OutputMessage))
                        {
                            if (cardOutputBuilder.Length > 0) cardOutputBuilder.Append(" ");
                            cardOutputBuilder.Append(clause.OutputMessage);
                        }
                    }
                    else if (clause.LogicalOperator == "ELSE IF")
                    {
                        if (blockEvaluatedAndPassed) break;

                        currentChainIsMatch = clauseIsTrue;
                        if (currentChainIsMatch)
                        {
                            blockEvaluatedAndPassed = true;
                            if (!string.IsNullOrWhiteSpace(clause.OutputMessage)) cardOutputBuilder.Append(clause.OutputMessage);
                            break;
                        }
                    }
                }

                // Generar una clave de variable limpia basada en el nombre asignado a la tarjeta
                string cleanCardKey = "diapstash_rule_" + card.CardName.Trim().ToLower().Replace(" ", "_");
                string finalValue = blockEvaluatedAndPassed ? cardOutputBuilder.ToString().Trim() : string.Empty;

                dynamicCardTokens.Add(cleanCardKey, finalValue);
            }

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string defaultTemplate = "Current diaper status is {diapstash_status}. Product in use: {diapstash_product}, size {diapstash_size}. Wetness level: {diapstash_wetness}, mess level: {diapstash_messy}. Elapsed runtime: {diapstash_elapsed}.";
            string userCustomTemplate = settings.Values["SavedTtsTemplate"]?.ToString() ?? defaultTemplate;

            userCustomTemplate = userCustomTemplate.Replace("{diapstash_product}", state.ProductName);
            userCustomTemplate = userCustomTemplate.Replace("{diapstash_size}", state.Size);
            userCustomTemplate = userCustomTemplate.Replace("{diapstash_wetness}", state.WetnessDisplay);
            userCustomTemplate = userCustomTemplate.Replace("{diapstash_messy}", state.MessyDisplay);
            userCustomTemplate = userCustomTemplate.Replace("{diapstash_elapsed}", elapsedString);
            userCustomTemplate = userCustomTemplate.Replace("{diapstash_leak}", state.HasLeak ? "YES" : "NO");
            userCustomTemplate = userCustomTemplate.Replace("{diapstash_status}", state.IsActiveSession ? "Active" : "Completed");

            // Reemplazar macros dinámicas personalizadas dentro del propio framework si existen
            foreach (var token in dynamicCardTokens)
            {
                userCustomTemplate = userCustomTemplate.Replace("{" + token.Key + "}", token.Value);
            }

            await UpdateGlobalVariableAsync("diapstash_change", userCustomTemplate);

            // Registrar cada bloque de condiciones de forma independiente en la pila global de JakeyTTS
            foreach (var token in dynamicCardTokens)
            {
                await UpdateGlobalVariableAsync(token.Key, token.Value);
            }

            await UpdateGlobalVariableAsync("diapstash_product", state.ProductName);
            await UpdateGlobalVariableAsync("diapstash_size", state.Size);
            await UpdateGlobalVariableAsync("diapstash_wetness", state.WetnessDisplay);
            await UpdateGlobalVariableAsync("diapstash_wetness_percent", $"{state.WetnessPercentage}%");
            await UpdateGlobalVariableAsync("diapstash_messy", state.MessyDisplay);
            await UpdateGlobalVariableAsync("diapstash_messy_percent", $"{state.MessyPercentage}%");
            await UpdateGlobalVariableAsync("diapstash_elapsed", elapsedString);
            await UpdateGlobalVariableAsync("diapstash_leak", state.HasLeak ? "YES" : "NO");
            await UpdateGlobalVariableAsync("diapstash_status", state.IsActiveSession ? "Active" : "Completed");

            string stockSummary = await DiapStashClient.Instance.FetchCurrentStockSummaryAsync();
            await UpdateGlobalVariableAsync("diapstash_stock", stockSummary);

            LogReceived?.Invoke("✨ Nested conditional matrix tokens individualization pipeline synchronized successfully.");
        }

        private async Task UpdateGlobalVariableAsync(string key, string value)
        {
            var packet = new
            {
                type = "set_global_variable",
                payload = new
                {
                    variable_name = key,
                    variable_value = value
                }
            };
            await SendJsonAsync(packet);
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

                    if (type == "event_broadcast")
                    {
                        string scope = root.TryGetProperty("scope", out var sProp) ? sProp.GetString() ?? "" : "";
                        if (scope == "commands" || scope == "redeems")
                        {
                            _ = Task.Run(async () => await SynchronizeJakeyGlobalVariablesAsync());
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