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

        // Custom development handler bypassing local TLS validation errors during authorization handshakes
        private readonly HttpClient _tokenHttpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });

        public PortalPage()
        {
            this.InitializeComponent();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            ClientIdBox.Text = settings.Values["SavedClientId"]?.ToString() ?? "";
            ClientSecretBox.Password = settings.Values["SavedClientSecret"]?.ToString() ?? "";
            StashTokenBox.Text = settings.Values["SavedStashToken"]?.ToString() ?? "";
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

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedClientId"] = clientId;
            settings.Values["SavedClientSecret"] = clientSecret;

            AuthBtn.IsEnabled = false;
            MainWindow.Instance?.Log("🌐 Initializing loopback server on http://localhost:8888/ ...");

            try
            {
                _oauthListener = new HttpListener();
                _oauthListener.Prefixes.Add("http://localhost:8888/");
                _oauthListener.Start();

                string redirectUri = Uri.EscapeDataString("http://localhost:8888/");
                // Explicitly requesting offline_access to retrieve a persistent refresh_token
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

                // Parse the code out of the loopback redirect query parameters array cleanly
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

                // FIXED: Log full outgoing envelope tracking parameters for context confirmation
                var sbDebug = new StringBuilder();
                sbDebug.AppendLine("============ 🛫 OUTGOING HTTP REQUEST DETAILS ============");
                sbDebug.AppendLine($"URL: {request.RequestUri}");
                sbDebug.AppendLine($"Method: {request.Method}");
                sbDebug.AppendLine($"Header - Authorization: Basic {base64Credentials.Substring(0, Math.Min(base64Credentials.Length, 8))}...");
                sbDebug.AppendLine($"Header - User-Agent: {request.Headers.UserAgent}");
                sbDebug.AppendLine($"Body Content: grant_type=authorization_code&code={code.Substring(0, Math.Min(code.Length, 6))}...&redirect_uri=http://localhost:8888/");
                sbDebug.AppendLine("=========================================================");
                MainWindow.Instance?.Log(sbDebug.ToString());

                using var response = await _tokenHttpClient.SendAsync(request);
                string rawResponseText = await response.Content.ReadAsStringAsync();

                // FIXED: Capture full response context when an anomaly or standard HTML structural envelope is intercepted
                if (rawResponseText.Trim().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                    rawResponseText.Trim().StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                    !response.IsSuccessStatusCode)
                {
                    var sbErrorDump = new StringBuilder();
                    sbErrorDump.AppendLine("============ 🛬 INCOMING VERBOSE ERROR DUMP ============");
                    sbErrorDump.AppendLine($"HTTP Status Code: {(int)response.StatusCode} ({response.StatusCode})");
                    sbErrorDump.AppendLine($"Reason Phrase: {response.ReasonPhrase}");
                    sbErrorDump.AppendLine("--- Response Headers ---");

                    foreach (var header in response.Headers)
                    {
                        sbErrorDump.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                    }
                    foreach (var header in response.Content.Headers)
                    {
                        sbErrorDump.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                    }

                    sbErrorDump.AppendLine("--- Raw HTML Document Source Payload Content ---");
                    sbErrorDump.AppendLine(rawResponseText);
                    sbErrorDump.AppendLine("========================================================");

                    // Drop the massive text payload cleanly down into the logging framework
                    MainWindow.Instance?.Log(sbErrorDump.ToString());
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(rawResponseText);
                    string accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
                    string refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rfProp) ? rfProp.GetString() ?? "" : "";

                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["SavedStashToken"] = accessToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        localSettings.Values["SavedRefreshToken"] = refreshToken;
                    }

                    DiapStashClient.Instance.ConfigureAuthentication(accessToken, clientId);

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

        // FIXED: Added dedicated Access Token Refresh implementation matching documentation rules
        public async Task<bool> RefreshAccessTokenAsync()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string clientId = settings.Values["SavedClientId"]?.ToString() ?? "";
            string clientSecret = settings.Values["SavedClientSecret"]?.ToString() ?? "";
            string refreshToken = settings.Values["SavedRefreshToken"]?.ToString() ?? "";

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

                    settings.Values["SavedStashToken"] = newAccessToken;
                    if (!string.IsNullOrEmpty(newRefreshToken))
                    {
                        settings.Values["SavedRefreshToken"] = newRefreshToken;
                    }

                    DiapStashClient.Instance.ConfigureAuthentication(newAccessToken, clientId);

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