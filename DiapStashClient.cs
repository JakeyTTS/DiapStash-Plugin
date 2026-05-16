using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public class DiapStashClient
    {
        private static DiapStashClient? _instance;
        public static DiapStashClient Instance => _instance ??= new DiapStashClient();

        private readonly HttpClient _httpClient = new HttpClient();
        private string _accessToken = string.Empty;

        private DiapStashClient()
        {
            _httpClient.BaseAddress = new Uri("https://api.diapstash.com/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void ConfigureAuthentication(string token)
        {
            _accessToken = token?.Trim() ?? string.Empty;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        /// <summary>
        /// Queries the remote server catalog for a human-readable inventory summary.
        /// </summary>
        public async Task<string> FetchCurrentStockSummaryAsync()
        {
            if (string.IsNullOrEmpty(_accessToken)) return "⚠️ DiapStash plugin error: Access Token missing or not authorized.";

            try
            {
                // Endpoint target matching public catalog specifications
                var response = await _httpClient.GetAsync("api/v1/stock");
                if (!response.IsSuccessStatusCode) return $"⚠️ DiapStash API error: HTTP {(int)response.StatusCode}";

                string rawJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(rawJson);
                var data = doc.RootElement;

                // Loop through items in inventory data array array
                if (data.ValueKind == JsonValueKind.Array)
                {
                    var summary = new System.Text.StringBuilder("📦 Current DiapStash Stock: ");
                    int uniqueTypes = 0;

                    foreach (var item in data.EnumerateArray())
                    {
                        string diaperName = item.GetProperty("name").GetString() ?? "Unknown";
                        int count = item.GetProperty("quantity").GetInt32();
                        string size = item.TryGetProperty("size", out var s) ? s.GetString() ?? "" : "";

                        summary.Append($"[{diaperName} {(string.IsNullOrEmpty(size) ? "" : $"({size})")}: {count}] ");
                        uniqueTypes++;
                    }

                    return uniqueTypes == 0 ? "📦 DiapStash Inventory is currently empty." : summary.ToString().Trim();
                }

                return "⚠️ Unexpected payload scheme returned from stock API.";
            }
            catch (Exception ex)
            {
                return $"❌ Network tracking exception: {ex.Message}";
            }
        }

        /// <summary>
        /// Retrieves telemetry logging metadata on the latest active or current process state change.
        /// </summary>
        public async Task<string> FetchLatestChangeStateAsync()
        {
            if (string.IsNullOrEmpty(_accessToken)) return "⚠️ DiapStash plugin unauthorized.";

            try
            {
                var response = await _httpClient.GetAsync("api/v1/changes/current");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return "🍼 No active process or changes running currently.";
                if (!response.IsSuccessStatusCode) return $"⚠️ History API returned HTTP {(int)response.StatusCode}";

                string rawJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string timeString = root.TryGetProperty("started_at", out var t) ? t.GetString() ?? "" : "Unknown Time";
                string note = root.TryGetProperty("notes", out var n) ? n.GetString() ?? "None" : "None";
                string product = root.TryGetProperty("diaper_name", out var d) ? d.GetString() ?? "Standard Product" : "Standard Product";

                return $"🚼 Active Change State: Wearing {product}. Started at: {timeString}. Memo Notes: {note}.";
            }
            catch (Exception ex)
            {
                return $"❌ Failed to retrieve runtime change state: {ex.Message}";
            }
        }
    }
}