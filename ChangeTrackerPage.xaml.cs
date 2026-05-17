using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public partial class ChangeTrackerPage : UserControl
    {
        public ChangeTrackerPage()
        {
            this.InitializeComponent();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;

            // FIXED: Modificado para indicar explícitamente que es el mensaje base por defecto de DiapStash
            string defaultTemplate = "[DiapStash Default Notification] Status: {diapstash_status}. Item: {diapstash_product} (Size {diapstash_size}). Wetness: {diapstash_wetness}, Mess: {diapstash_messy}. Active Runtime: {diapstash_elapsed}.";
            CustomTtsTemplateBox.Text = settings.Values["SavedTtsTemplate"]?.ToString() ?? defaultTemplate;

            JakeyTtsClient.Instance.LoadRulesFromSettings();
            RulesListView.ItemsSource = JakeyTtsClient.Instance.ComplexRuleCards;
        }

        public async Task RefreshChangeAsync()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string currentToken = settings.Values["SavedStashToken"]?.ToString() ?? "";
            string currentClientId = settings.Values["SavedClientId"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(currentToken) || string.IsNullOrEmpty(currentClientId))
            {
                NoActiveSessionInfoBar.Message = "⚠️ Authentication credentials missing. Link your client portal context.";
                return;
            }

            DiapStashClient.Instance.ConfigureAuthentication(currentToken, currentClientId);
            var statePayload = await DiapStashClient.Instance.FetchLatestChangeStateObjectAsync();

            if (statePayload == null)
            {
                StatusCardFrame.Visibility = Visibility.Collapsed;
                NoActiveSessionInfoBar.IsOpen = true;
                return;
            }

            NoActiveSessionInfoBar.IsOpen = false;
            StatusCardFrame.Visibility = Visibility.Visible;

            CardProductName.Text = statePayload.ProductName;
            CardProductSize.Text = $"Size: {statePayload.Size}";
            CardNotesDisplay.Text = string.IsNullOrEmpty(statePayload.Note) ? "No session context notes attached." : statePayload.Note;

            if (statePayload.StartTime != DateTime.MinValue)
            {
                DateTime localStartTime = statePayload.StartTime.ToLocalTime();
                CardStartTimeDisplay.Text = localStartTime.ToString("dd/MM/yyyy HH:mm");

                if (statePayload.IsActiveSession)
                {
                    TimeSpan elapsed = DateTime.Now - localStartTime;
                    CardElapsedTimeDisplay.Text = $"Elapsed: {(int)elapsed.TotalHours}h {elapsed.Minutes}m";
                    CardStatusBadgeText.Text = "ACTIVE";
                    CardStatusBadge.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 147, 112, 219));
                }
                else
                {
                    DateTime localEndTime = statePayload.EndTime.Value.ToLocalTime();
                    TimeSpan duration = localEndTime - localStartTime;
                    CardElapsedTimeDisplay.Text = $"Total Duration: {(int)duration.TotalHours}h {duration.Minutes}m";
                    CardStatusBadgeText.Text = "COMPLETED";
                    CardStatusBadge.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
                }
            }

            if (!string.IsNullOrEmpty(statePayload.VariantId) && statePayload.VariantId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                CardProductImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(statePayload.VariantId));
            }
            else
            {
                CardProductImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/StoreLogo.png"));
            }

            BadgeLeak.Visibility = statePayload.HasLeak ? Visibility.Visible : Visibility.Collapsed;

            if (statePayload.Wetness > 0)
            {
                BadgeWet.Visibility = Visibility.Visible;
                BadgeWetText.Text = $"💧 WET ({statePayload.WetnessDisplay} - {statePayload.WetnessPercentage}%)";
            }
            else { BadgeWet.Visibility = Visibility.Collapsed; }

            if (statePayload.MessyLevel > 0)
            {
                BadgeMessy.Visibility = Visibility.Visible;
                BadgeMessyText.Text = $"💩 MESSY ({statePayload.MessyDisplay} - {statePayload.MessyPercentage}%)";
            }
            else { BadgeMessy.Visibility = Visibility.Collapsed; }

            if (JakeyTtsClient.Instance != null)
            {
                _ = Task.Run(async () => await JakeyTtsClient.Instance.SynchronizeJakeyGlobalVariablesAsync());
            }
        }

        private void RefreshChange_Click(object sender, RoutedEventArgs e) => _ = RefreshChangeAsync();

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            JakeyTtsClient.Instance.ComplexRuleCards.Add(new TtsComplexRuleCard());
        }

        private void DeleteCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TtsComplexRuleCard cardToRemove)
            {
                JakeyTtsClient.Instance.ComplexRuleCards.Remove(cardToRemove);
            }
        }

        private void AddClause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TtsComplexRuleCard targetCard)
            {
                string logicalOp = targetCard.Clauses.Count > 0 ? "AND" : "IF";
                targetCard.Clauses.Add(new TtsClause { LogicalOperator = logicalOp });
            }
        }

        private void DeleteClause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TtsClause clauseToRemove)
            {
                foreach (var card in JakeyTtsClient.Instance.ComplexRuleCards)
                {
                    if (card.Clauses.Contains(clauseToRemove))
                    {
                        card.Clauses.Remove(clauseToRemove);
                        break;
                    }
                }
            }
        }

        private void SaveRules_Click(object sender, RoutedEventArgs e)
        {
            JakeyTtsClient.Instance.SaveRulesToSettings();
            MainWindow.Instance?.Log("💾 Persisted multi-level conditional tree matrix hierarchy safely into Windows Container Storage.");
            _ = JakeyTtsClient.Instance.SynchronizeJakeyGlobalVariablesAsync();
        }

        private void SaveTtsTemplate_Click(object sender, RoutedEventArgs e)
        {
            string customText = CustomTtsTemplateBox.Text.Trim();
            if (string.IsNullOrEmpty(customText)) return;

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SavedTtsTemplate"] = customText;

            MainWindow.Instance?.Log("💾 Saved custom blueprint template framework text into persistent state properties.");
            _ = JakeyTtsClient.Instance.SynchronizeJakeyGlobalVariablesAsync();
        }

        private async void InspectRawChange_Click(object sender, RoutedEventArgs e)
        {
            RawChangeDiagnosticBox.Visibility = Visibility.Visible;
            RawChangeDiagnosticBox.Text = "⌛ Requesting raw timeline frame payload stream...";
            string rawJson = await DiapStashClient.Instance.GetRawEndpointDataAsync("api/v1/history/changes");
            RawChangeDiagnosticBox.Text = rawJson;
        }
    }
}