using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiapStash_Plugin
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Load saved configuration keys cleanly from LocalSettings container cache bounds
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            TtsServerBox.Text = settings.Values["SavedTtsUrl"]?.ToString() ?? "ws://localhost:8889/";
            StashTokenBox.Password = settings.Values["SavedStashToken"]?.ToString() ?? "";

            // Pass authentication data to endpoint access instances right away if present
            if (!string.IsNullOrEmpty(StashTokenBox.Password))
            {
                DiapStashClient.Instance.ConfigureAuthentication(StashTokenBox.Password);
            }

            JakeyTtsClient.Instance.LogReceived += OnLogReceived;
            this.Closed += MainWindow_Closed;

            NavView.SelectedItem = NavView.MenuItems[0];
        }

        #region Master Navigation View Control Page Router
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // Reset all panels visibility layouts to Collapsed bounds safely
            PageHome.Visibility = Visibility.Collapsed;
            PageStashAuth.Visibility = Visibility.Collapsed;
            PageInventory.Visibility = Visibility.Collapsed;
            PageChangeTracker.Visibility = Visibility.Collapsed;
            PageInjections.Visibility = Visibility.Collapsed;

            var item = args.InvokedItemContainer as NavigationViewItem;
            if (item?.Tag == null) return;

            switch (item.Tag.ToString())
            {
                case "Home": PageHome.Visibility = Visibility.Visible; break;
                case "StashAuth": PageStashAuth.Visibility = Visibility.Visible; break;
                case "Inventory": PageInventory.Visibility = Visibility.Visible; checkedRefreshStock(); break;
                case "ChangeTracker": PageChangeTracker.Visibility = Visibility.Visible; checkedRefreshChange(); break;
                case "Injections": PageInjections.Visibility = Visibility.Visible; break;
            }
        }

        private async void checkedRefreshStock()
        {
            string stockData = await DiapStashClient.Instance.FetchCurrentStockSummaryAsync();
            InventoryDumpBlock.Text = stockData;
        }

        private async void checkedRefreshChange()
        {
            string logData = await DiapStashClient.Instance.FetchLatestChangeStateAsync();
            ChangeTrackerOutputBlock.Text = logData;
        }
        #endregion

        #region Structural REST Endpoint Queries Action Mappings
        private async void OpenPortal_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://docs.diapstash.com/api/authentication"));
        }

        private void StashTokenBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string token = StashTokenBox.Password.Trim();
            DiapStashClient.Instance.ConfigureAuthentication(token);

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedStashToken"] = token;
        }

        private async void RefreshStock_Click(object sender, RoutedEventArgs e)
        {
            InventoryDumpBlock.Text = "⌛ Syncing live stock metrics matrices...";
            string dataSummary = await DiapStashClient.Instance.FetchCurrentStockSummaryAsync();
            InventoryDumpBlock.Text = dataSummary;
        }

        private async void RefreshChange_Click(object sender, RoutedEventArgs e)
        {
            ChangeTrackerOutputBlock.Text = "⌛ Polling active history metadata tables...";
            string stateChangeLog = await DiapStashClient.Instance.FetchLatestChangeStateAsync();
            ChangeTrackerOutputBlock.Text = stateChangeLog;
        }
        #endregion

        #region WebSocket Loop Handlers & Framework Injection Routines
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            string token = StashTokenBox.Password.Trim();
            string ttsUrl = TtsServerBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(ttsUrl))
            {
                OnLogReceived("⚠️ Settings mismatch. Specify a valid local server websocket URL.");
                return;
            }

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedTtsUrl"] = ttsUrl;

            ConnectBtn.IsEnabled = false;
            TtsServerBox.IsEnabled = false;
            DisconnectBtn.IsEnabled = true;
            InjectUiBtn.IsEnabled = true;

            await JakeyTtsClient.Instance.StartAsync(ttsUrl);
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            ConnectBtn.IsEnabled = true;
            TtsServerBox.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;
            InjectUiBtn.IsEnabled = false;

            await JakeyTtsClient.Instance.StopAsync();
        }

        private async void InjectUi_Click(object sender, RoutedEventArgs e)
        {
            // Pushes its own integrated configuration view parameter back into the JakeyTTS workspace dynamically
            // Note: In an open production environment, replace with an embedded hosted server endpoint dashboard link if required.
            string customLocalPanelUrl = "https://api.diapstash.com/api/docs/";

            await JakeyTtsClient.Instance.InjectRemoteDashboardUiFrameAsync(customLocalPanelUrl);
            OnLogReceived("📡 Sent layout injection frame packet via connection stack.");
        }

        private void OnLogReceived(string message)
        {
            if (this.DispatcherQueue == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (LogBlock != null)
                    {
                        LogBlock.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                        LogScroll?.ChangeView(0, LogScroll.ScrollableHeight, 1);
                    }
                }
                catch { }
            });
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            JakeyTtsClient.Instance.LogReceived -= OnLogReceived;
            await JakeyTtsClient.Instance.StopAsync();
        }
        #endregion
    }
}