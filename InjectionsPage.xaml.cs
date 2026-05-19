using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Text.Json;

namespace DiapStash_Plugin
{
    public partial class InjectionsPage : UserControl
    {
        public InjectionsPage()
        {
            this.InitializeComponent();

            // FIXED: Eliminamos por completo ApplicationData.Current. 
            // Ahora leemos la URL de JakeyTTS desde el archivo local credentials.json en el disco.
            string ttsUrl = "ws://localhost:8889/";
            try
            {
                string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                if (File.Exists(credentialsPath))
                {
                    string rawJson = File.ReadAllText(credentialsPath);
                    using var doc = JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;
                    ttsUrl = root.TryGetProperty("TtsUrl", out var urlProp) ? urlProp.GetString() ?? ttsUrl : ttsUrl;
                }
            }
            catch { }

            TtsServerBox.Text = ttsUrl;
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            string ttsUrl = TtsServerBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(ttsUrl)) return;

            // FIXED: Modificamos y guardamos la URL de red de forma segura dentro del JSON local en disco
            try
            {
                string credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                string clientId = "";
                string token = "";
                string clientSecret = "";
                string refreshToken = "";
                string customTemplate = "";

                if (File.Exists(credentialsPath))
                {
                    string rawJson = File.ReadAllText(credentialsPath);
                    using var doc = JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;
                    clientId = root.TryGetProperty("ClientId", out var ci) ? ci.GetString() ?? "" : "";
                    clientSecret = root.TryGetProperty("ClientSecret", out var cs) ? cs.GetString() ?? "" : "";
                    token = root.TryGetProperty("AccessToken", out var at) ? at.GetString() ?? "" : "";
                    refreshToken = root.TryGetProperty("RefreshToken", out var rt) ? rt.GetString() ?? "" : "";
                    customTemplate = root.TryGetProperty("CustomTtsTemplate", out var ct) ? ct.GetString() ?? "" : "";
                }

                var updatedBackup = new
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    AccessToken = token,
                    RefreshToken = refreshToken,
                    TtsUrl = ttsUrl,
                    CustomTtsTemplate = customTemplate
                };

                File.WriteAllText(credentialsPath, JsonSerializer.Serialize(updatedBackup, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

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