using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiapStash_Plugin
{
    public partial class PortalPage : UserControl
    {
        private HttpListener? _oauthListener;

        private readonly HttpClient _tokenHttpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });

        public PortalPage()
        {
            this.InitializeComponent();

            // FIXED: Read values from the local JSON config file instead of Windows ApplicationData containers
            string clientId = "";
            string token = "";
            try
            {
                string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                if (File.Exists(credentialsPath))
                {
                    string rawJson = File.ReadAllText(credentialsPath);
                    using var doc = JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;
                    clientId = root.TryGetProperty("ClientId", out var idProp) ? idProp.GetString() ?? "" : "";
                    token = root.TryGetProperty("AccessToken", out var tokenProp) ? tokenProp.GetString() ?? "" : "";

                    // Attempt to fetch ClientSecret from disk if it was saved previously
                    if (root.TryGetProperty("ClientSecret", out var secretProp))
                    {
                        ClientSecretBox.Password = secretProp.GetString() ?? "";
                    }
                }
            }
            catch { }

            ClientIdBox.Text = clientId;
            StashTokenBox.Text = token;
        }

        private async void OpenPortal_Click(object sender, RoutedEventArgs e)
        {
            string clientId = ClientIdBox.Text.Trim();
            string clientSecret = ClientSecretBox.Password.Trim();

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                MainWindow.Instance?.Log("⚠️ Settings validation mismatch. Specify Client ID and Secret properties.");
                return;
            }

            AuthBtn.IsEnabled = false;
            MainWindow.Instance?.Log("🌐 Initializing loopback server on http://localhost:8888/ ...");

            try
            {
                _oauthListener = new HttpListener();
                _oauthListener.Prefixes.Add("http://localhost:8888/");
                _oauthListener.Start();

                string redirectUri = Uri.EscapeDataString("http://localhost:8888/");
                string scope = Uri.EscapeDataString("cloud-sync.stock cloud-sync.history cloud-sync.types offline_access");
                string loginUrl = $"https://account.diapstash.com/oidc/auth?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope={scope}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(loginUrl) { UseShellExecute = true });

                HttpListenerContext context;
                try
                {
                    context = await _oauthListener.GetContextAsync();
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is HttpListenerException)
                {
                    return;
                }

                if (context.Request.Url?.AbsolutePath.Contains("favicon.ico") == true)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                string error = context.Request.QueryString["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    MainWindow.Instance?.Log($"❌ DiapStash Server rejected authorization request: {error}");
                    byte[] htmlError = Encoding.UTF8.GetBytes($"<html><body style='font-family:sans-serif;text-align:center;padding-top:50px;color:red;'><h2>❌ Authentication Failed</h2><p>Reason: {error}</p></body></html>");
                    context.Response.OutputStream.Write(htmlError, 0, htmlError.Length);
                    context.Response.Close();
                    return;
                }

                string code = context.Request.QueryString["code"] ?? "";

                byte[] htmlFeedback = Encoding.UTF8.GetBytes("<html><body style='font-family:sans-serif;text-align:center;padding-top:50px;'><h2>✓ Link Successful!</h2><p>You can close this tab safely and return to the Application panel layout.</p></body></html>");
                context.Response.OutputStream.Write(htmlFeedback, 0, htmlFeedback.Length);

                await context.Response.OutputStream.FlushAsync();
                context.Response.Close();

                if (_oauthListener != null && _oauthListener.IsListening)
                {
                    _oauthListener.Stop();
                }

                if (!string.IsNullOrEmpty(code))
                {
                    MainWindow.Instance?.Log("📡 Code retrieved. Redeeming security payload for Access Tokens...");
                    await ExchangeCodeForTokenAsync(code, clientId, clientSecret);
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"❌ Auth Handshake Exception: {ex.Message}");
            }
            finally
            {
                ShutdownServer();
                AuthBtn.IsEnabled = true;
            }
        }

        private async Task ExchangeCodeForTokenAsync(string code, string clientId, string clientSecret)
        {
            try
            {
                var bodyParams = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", "http://localhost:8888/" }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://account.diapstash.com/oidc/token");
                request.Content = new FormUrlEncodedContent(bodyParams);

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.TryAddWithoutValidation("User-Agent", "JakeyTTS-DiapStash-Plugin/1.0.0 (WinUI3; .NET)");

                string rawCredentials = $"{clientId}:{clientSecret}";
                string base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

                using var response = await _tokenHttpClient.SendAsync(request);
                string rawResponseText = await response.Content.ReadAsStringAsync();

                if (rawResponseText.Trim().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                    rawResponseText.Trim().StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                    !response.IsSuccessStatusCode)
                {
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(rawResponseText);
                    string accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
                    string refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rfProp) ? rfProp.GetString() ?? "" : "";

                    DiapStashClient.Instance.ConfigureAuthentication(accessToken, clientId);

                    // FIXED: Persist credential sets securely directly onto local disk
                    try
                    {
                        string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                        string existingTtsUrl = "ws://localhost:8889/";
                        string existingTemplate = "";

                        if (File.Exists(credentialsPath))
                        {
                            try
                            {
                                string existingRaw = File.ReadAllText(credentialsPath);
                                using var existingDoc = JsonDocument.Parse(existingRaw);
                                var r = existingDoc.RootElement;
                                existingTtsUrl = r.TryGetProperty("TtsUrl", out var urlProp) ? urlProp.GetString() ?? existingTtsUrl : existingTtsUrl;
                                existingTemplate = r.TryGetProperty("CustomTtsTemplate", out var tmpProp) ? tmpProp.GetString() ?? existingTemplate : existingTemplate;
                            }
                            catch { }
                        }

                        var credentialBackup = new
                        {
                            ClientId = clientId,
                            ClientSecret = clientSecret,
                            AccessToken = accessToken,
                            RefreshToken = refreshToken,
                            TtsUrl = existingTtsUrl,
                            CustomTtsTemplate = existingTemplate
                        };
                        File.WriteAllText(credentialsPath, JsonSerializer.Serialize(credentialBackup, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { }

                    if (this.DispatcherQueue != null)
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            StashTokenBox.Text = accessToken;
                        });
                    }

                    MainWindow.Instance?.Log("✨ Handshake finalized. DiapStash active token persistent across future runtime sessions.");
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"❌ HTTP token request execution exception critically thrown: {ex.Message}");
            }
        }

        public async Task<bool> RefreshAccessTokenAsync()
        {
            string clientId = "";
            string clientSecret = "";
            string refreshToken = "";
            string ttsUrl = "ws://localhost:8889/";
            string template = "";

            try
            {
                string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                if (File.Exists(credentialsPath))
                {
                    string rawJson = File.ReadAllText(credentialsPath);
                    using var doc = JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;
                    clientId = root.TryGetProperty("ClientId", out var idProp) ? idProp.GetString() ?? "" : "";
                    clientSecret = root.TryGetProperty("ClientSecret", out var secProp) ? secProp.GetString() ?? "" : "";
                    refreshToken = root.TryGetProperty("RefreshToken", out var refProp) ? refProp.GetString() ?? "" : "";
                    ttsUrl = root.TryGetProperty("TtsUrl", out var urlProp) ? urlProp.GetString() ?? ttsUrl : ttsUrl;
                    template = root.TryGetProperty("CustomTtsTemplate", out var tmpProp) ? tmpProp.GetString() ?? template : template;
                }
            }
            catch { return false; }

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            try
            {
                var bodyParams = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://account.diapstash.com/oidc/token");
                request.Content = new FormUrlEncodedContent(bodyParams);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string rawCredentials = $"{clientId}:{clientSecret}";
                string base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

                using var response = await _tokenHttpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string rawJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(rawJson);

                    string newAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
                    string newRefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rfProp) ? rfProp.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(newRefreshToken))
                    {
                        refreshToken = newRefreshToken;
                    }

                    DiapStashClient.Instance.ConfigureAuthentication(newAccessToken, clientId);

                    try
                    {
                        string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                        var updatedBackup = new
                        {
                            ClientId = clientId,
                            ClientSecret = clientSecret,
                            AccessToken = newAccessToken,
                            RefreshToken = refreshToken,
                            TtsUrl = ttsUrl,
                            CustomTtsTemplate = template
                        };
                        File.WriteAllText(credentialsPath, JsonSerializer.Serialize(updatedBackup, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { }

                    if (this.DispatcherQueue != null)
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            StashTokenBox.Text = newAccessToken;
                        });
                    }

                    MainWindow.Instance?.Log("🔄 Access token refreshed successfully using background persistent refresh tokens.");
                    return true;
                }
            }
            catch { }
            return false;
        }

        public void ShutdownServer()
        {
            try
            {
                if (_oauthListener != null)
                {
                    if (_oauthListener.IsListening)
                    {
                        _oauthListener.Stop();
                    }
                    _oauthListener.Close();
                    _oauthListener = null;
                }
            }
            catch { }
        }
    }
}