using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DiapStash_Plugin
{
    public partial class InjectionsPage : UserControl
    {
        public InjectionsPage()
        {
            this.InitializeComponent();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            TtsServerBox.Text = settings.Values["SavedTtsUrl"]?.ToString() ?? "ws://localhost:8889/";
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            string ttsUrl = TtsServerBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(ttsUrl)) return;

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedTtsUrl"] = ttsUrl;

            ConnectBtn.IsEnabled = false;
            TtsServerBox.IsEnabled = false;
            DisconnectBtn.IsEnabled = true;

            await JakeyTtsClient.Instance.StartAsync(ttsUrl);
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            ConnectBtn.IsEnabled = true;
            TtsServerBox.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;

            await JakeyTtsClient.Instance.StopAsync();
            MainWindow.Instance?.Log("🛑 Core connection pipeline closed manually.");
        }
    }
}