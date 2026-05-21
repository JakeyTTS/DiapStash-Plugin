using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiapStash_Plugin
{
    public partial class HomePage : UserControl
    {
        private LogWindow? _floatingLogWindow;
        private string _cachedTtsUrl = "ws://localhost:8889/";

        public static readonly List<string> LogCacheBacklog = new List<string>();

        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => _ = CheckPlatformStateAsync();
        }

        private async Task CheckPlatformStateAsync()
        {
            // FIXED: Stripped all ApplicationData container settings.
            // Pulled configuration tokens strictly out of our local disk credentials file map structure.
            string clientId = "";
            string clientSecret = "";
            string token = "";
            _cachedTtsUrl = "ws://localhost:8889/";

            try
            {
                string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                if (File.Exists(credentialsPath))
                {
                    string rawCreds = File.ReadAllText(credentialsPath);
                    using var doc = JsonDocument.Parse(rawCreds);
                    var root = doc.RootElement;
                    clientId = root.TryGetProperty("ClientId", out var idProp) ? idProp.GetString() ?? "" : "";
                    clientSecret = root.TryGetProperty("ClientSecret", out var secProp) ? secProp.GetString() ?? "" : "";
                    token = root.TryGetProperty("AccessToken", out var tokenProp) ? tokenProp.GetString() ?? "" : "";
                    _cachedTtsUrl = root.TryGetProperty("TtsUrl", out var urlProp) ? urlProp.GetString() ?? _cachedTtsUrl : _cachedTtsUrl;
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                UpdateStatusUi("API credentials missing. Please configure Client ID and Secret in Settings.", showConfigure: true, showLogin: false, dotColor: Microsoft.UI.Colors.Red);
                AppendLog("⚠️ Platform status: Unconfigured. Awaiting Client ID credentials.");
                
                var settingsInstance = MainWindow.Instance?.GetSettingsPageInstance();
                if (settingsInstance != null)
                {
                    MainWindow.Instance?.NavigateToPage("Settings");
                    _ = settingsInstance.LaunchSetupWizardAsync();
                }
                
                return;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                UpdateStatusUi("Authentication token missing. Portal authorization required.", showConfigure: false, showLogin: true, dotColor: Microsoft.UI.Colors.Yellow);
                AppendLog("⚠️ Platform status: Missing security token context.");
                return;
            }

            DiapStashClient.Instance.ConfigureAuthentication(token, clientId);

            if (JakeyTtsClient.Instance.IsConnected)
            {
                UpdateStatusUi("DiapStash Ready. Connected to Platform.", showConfigure: false, showLogin: false, dotColor: Microsoft.UI.Colors.Green, hideLoading: true);
                AppendLog("✨ Connected to engine passively via background thread orchestration channels.");
                return;
            }

            AppendLog("📡 Verifying token lifetime persistence against secure API infrastructure...");
            var payloadCheck = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();

            if (DiapStashClient.Instance.IsRateLimited)
            {
                UpdateStatusUi("⚠️ Too many requests! API limit reached. Screen cannot be loaded right now.", showConfigure: false, showLogin: false, dotColor: Microsoft.UI.Colors.Red, hideLoading: true);
                AppendLog("❌ Platform status: 429 Rate limited. Postponing thread sequence requests.");
                return;
            }

            if (payloadCheck == null)
            {
                AppendLog("⏳ Access token expired or rejected. Attempting automated silent refresh cycle sequence...");

                var settingsInstance = MainWindow.Instance?.GetSettingsPageInstance();
                if (settingsInstance != null)
                {
                    bool refreshSuccess = await settingsInstance.RefreshAccessTokenAsync();
                    if (refreshSuccess)
                    {
                        AppendLog("✨ Token renewed successfully! Retrying state data synchronization sequence...");
                        payloadCheck = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();
                    }
                }
            }

            if (payloadCheck == null)
            {
                UpdateStatusUi("Session expired (401 Unauthorized). Please Login.", showConfigure: false, showLogin: true, dotColor: Microsoft.UI.Colors.Red);
                AppendLog("❌ Platform status: Unauthorized API connection context. Awaiting manual login.");
                return;
            }

            UpdateStatusUi("DiapStash Ready. Connected to Platform.", showConfigure: false, showLogin: false, dotColor: Microsoft.UI.Colors.Green, hideLoading: true);
            AppendLog("✨ Integration bridge authorized. Synchronizing real-time telemetry datasets...");

            await ExecuteTtsConnectionAsync();
        }

        private async Task ExecuteTtsConnectionAsync()
        {
            if (DiapStashClient.Instance.IsRateLimited) return;

            try
            {
                AppendLog($"🔌 Initializing injection loop towards JakeyTTS Core Engine at {_cachedTtsUrl}...");
                await JakeyTtsClient.Instance.StartAsync(_cachedTtsUrl);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Background connection initialization pipeline aborted: {ex.Message}");
            }
        }

        private void UpdateStatusUi(string message, bool showConfigure, bool showLogin, Windows.UI.Color dotColor, bool hideLoading = false)
        {
            if (this.DispatcherQueue == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessageLabel.Text = message;
                StatusDot.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(dotColor);
                ConfigureApiBtn.Visibility = showConfigure ? Visibility.Visible : Visibility.Collapsed;
                LoginPortalBtn.Visibility = showLogin ? Visibility.Visible : Visibility.Collapsed;

                if (hideLoading) StatusLoadingRing.IsActive = false;
            });
        }

        private void OpenFloatingLog_Click(object sender, RoutedEventArgs e)
        {
            if (_floatingLogWindow == null)
            {
                _floatingLogWindow = new LogWindow();
                _floatingLogWindow.Closed += (s, args) => _floatingLogWindow = null;
            }
            _floatingLogWindow.Activate();
        }

        public void AppendLog(string message)
        {
            string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}";

            lock (LogCacheBacklog)
            {
                LogCacheBacklog.Add(logLine);
                if (LogCacheBacklog.Count > 500) LogCacheBacklog.RemoveAt(0);
            }

            _floatingLogWindow?.AppendLog(message);
            System.Diagnostics.Debug.WriteLine(logLine);
        }

        private void ConfigureApi_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.NavigateToPage("Settings");

        private void LoginPortal_Click(object sender, RoutedEventArgs e)
        {
            var settingsInstance = MainWindow.Instance?.GetSettingsPageInstance();
            if (settingsInstance != null)
            {
                MainWindow.Instance?.NavigateToPage("Settings");
                settingsInstance.TriggerLogin();
            }
        }
    }
}