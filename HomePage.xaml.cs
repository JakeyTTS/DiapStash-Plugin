using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public partial class HomePage : UserControl
    {
        private LogWindow? _floatingLogWindow;
        private string _cachedTtsUrl = "ws://localhost:8889/";

        // Static historical log cache container to store backend worker context logs securely across window lifecycles
        public static readonly List<string> LogCacheBacklog = new List<string>();

        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => _ = CheckPlatformStateAsync();
        }

        private async Task CheckPlatformStateAsync()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string clientId = settings.Values["SavedClientId"]?.ToString() ?? "";
            string clientSecret = settings.Values["SavedClientSecret"]?.ToString() ?? "";
            string token = settings.Values["SavedStashToken"]?.ToString() ?? "";
            _cachedTtsUrl = settings.Values["SavedTtsUrl"]?.ToString() ?? "ws://localhost:8889/";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                UpdateStatusUi("API credentials missing. Please set up Client parameters.", showConfigure: true, showLogin: false, showTts: false);
                AppendLog("⚠️ Platform status: Unconfigured. Awaiting Client ID credentials.");
                return;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                UpdateStatusUi("Authentication token missing. Portal authorization required.", showConfigure: false, showLogin: true, showTts: false);
                AppendLog("⚠️ Platform status: Missing security token context.");
                return;
            }

            AppendLog("📡 Verifying token lifetime persistence against secure API infrastructure...");
            DiapStashClient.Instance.ConfigureAuthentication(token, clientId);

            var payloadCheck = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();

            if (DiapStashClient.Instance.IsRateLimited)
            {
                UpdateStatusUi("⚠️ Too many requests! API limit reached. Screen cannot be loaded right now.", showConfigure: false, showLogin: false, showTts: false, hideLoading: true);
                AppendLog("❌ Platform status: 429 Rate limited. Postponing thread sequence requests.");
                return;
            }

            if (payloadCheck == null)
            {
                AppendLog("⏳ Access token expired or rejected. Attempting automated silent refresh cycle sequence...");

                var portalInstance = MainWindow.Instance?.GetPortalPageInstance() as PortalPage;
                if (portalInstance != null)
                {
                    bool refreshSuccess = await portalInstance.RefreshAccessTokenAsync();
                    if (refreshSuccess)
                    {
                        AppendLog("✨ Token renewed successfully! Retrying state data synchronization sequence...");
                        payloadCheck = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();
                    }
                }
            }

            if (payloadCheck == null)
            {
                UpdateStatusUi("Session expired (401 Unauthorized). Re-authenticate via portal.", showConfigure: false, showLogin: true, showTts: false);
                AppendLog("❌ Platform status: Unauthorized API connection context. Token dropped.");
                return;
            }

            UpdateStatusUi($"DiapStash Ready. Connected Client: {clientId}", showConfigure: false, showLogin: false, showTts: true, hideLoading: true);
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

        private void UpdateStatusUi(string message, bool showConfigure, bool showLogin, bool showTts, bool hideLoading = false)
        {
            if (this.DispatcherQueue == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessageLabel.Text = message;
                ConfigureApiBtn.Visibility = showConfigure ? Visibility.Visible : Visibility.Collapsed;
                LoginPortalBtn.Visibility = showLogin ? Visibility.Visible : Visibility.Collapsed;
                ConnectTtsBtn.Visibility = showTts ? Visibility.Visible : Visibility.Collapsed;

                if (hideLoading) StatusLoadingRing.IsActive = false;
            });
        }

        private async void ConnectTts_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_cachedTtsUrl)) return;

            ConnectTtsBtn.IsEnabled = false;
            AppendLog("Hierarchy manual injection forced by streamer context.");
            await ExecuteTtsConnectionAsync();
            ConnectTtsBtn.IsEnabled = true;
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

        private void ConfigureApi_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.NavigateToPage("StashAuth");
        private void LoginPortal_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.NavigateToPage("StashAuth");
    }
}