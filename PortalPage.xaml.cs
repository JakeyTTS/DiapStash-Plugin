using System;
using System.IO;
using System.Net;
using System.Net.Http;
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
        private readonly HttpClient _tokenHttpClient = new HttpClient();

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
                    // Catch internal native queue drops gracefully if execution aborts early
                    return;
                }

                // Filter browser side-channel noise requests safely
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

                // Flush underlying buffers cleanly before touching listener pipeline switches
                await context.Response.OutputStream.FlushAsync();
                context.Response.Close();

                // Stop listening to NEW connection inbound requests, but leave the handle structure intact
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
                // Clean up references safely and bring back button controls
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
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "code", code },
                    { "redirect_uri", "http://localhost:8888/" }
                };

                var response = await _tokenHttpClient.PostAsync("https://account.diapstash.com/oidc/token", new FormUrlEncodedContent(bodyParams));
                if (response.IsSuccessStatusCode)
                {
                    string rawJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(rawJson);
                    string token = doc.RootElement.GetProperty("access_token").GetString() ?? "";

                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["SavedStashToken"] = token;

                    DiapStashClient.Instance.ConfigureAuthentication(token, clientId);

                    // FIXED: Re-route back to the Main UI thread using DispatcherQueue to set WinUI 3 control fields
                    if (this.DispatcherQueue != null)
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            StashTokenBox.Text = token;
                        });
                    }

                    MainWindow.Instance?.Log("✨ Handshake finalized. DiapStash active token persistent across future runtime sessions.");
                }
                else
                {
                    MainWindow.Instance?.Log($"❌ Token request failed. Server response code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"❌ HTTP token request execution failed: {ex.Message}");
            }
        }

        public void ShutdownServer()
        {
            // FIXED: Isolated teardown block preventing abrupt HttpRequestQueue structural exceptions
            try
            {
                if (_oauthListener != null)
                {
                    if (_oauthListener.IsListening)
                    {
                        _oauthListener.Stop();
                    }
                    _oauthListener.Close(); // Safely teardown handles now that execution is complete
                    _oauthListener = null;
                }
            }
            catch { /* Suppress race conditions occurring on system channel teardowns */ }
        }
    }
}