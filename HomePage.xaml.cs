using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.UI;

namespace DiapStash_Plugin
{
    public partial class HomePage : UserControl
    {
        private LogWindow? _floatingLogWindow;
        private string _cachedTtsUrl = "ws://localhost:8889/";

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

            // FIXED: Intercept 429 error payloads instantly to update screen status bounds
            if (DiapStashClient.Instance.IsRateLimited)
            {
                UpdateStatusUi("⚠️ Too many requests! API limit reached. Screen cannot be loaded right now.", showConfigure: false, showLogin: false, showTts: false, hideLoading: true);
                AppendLog("❌ Platform status: 429 Rate limited. Postponing thread sequence requests.");
                return;
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
            // Abort connections if client engine states denote global lock bounds
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

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedTtsUrl"] = _cachedTtsUrl;

            AppendLog("⚡ Manual pipeline injection trigger captured from Home interface view.");
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
            AppendLog("ℹ️ Telemetry terminal instance attached to independent window thread context.");
        }

        public void AppendLog(string message)
        {
            _floatingLogWindow?.AppendLog(message);
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void ConfigureApi_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.NavigateToPage("StashAuth");
        private void LoginPortal_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.NavigateToPage("StashAuth");
    }
}