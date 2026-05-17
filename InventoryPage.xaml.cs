using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public partial class InventoryPage : UserControl
    {
        public InventoryPage()
        {
            this.InitializeComponent();
        }

        public async Task RefreshStockAsync()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string currentToken = settings.Values["SavedStashToken"]?.ToString() ?? "";
            string currentClientId = settings.Values["SavedClientId"]?.ToString() ?? "";

            // FIXED: Intercept missing configuration credentials explicitly inside this module view layer
            if (string.IsNullOrEmpty(currentToken) || string.IsNullOrEmpty(currentClientId))
            {
                CatalogScrollViewer.Visibility = Visibility.Collapsed;
                InventoryInfoBar.Title = "Authentication Required";
                InventoryInfoBar.Message = "⚠️ Not logged in. Please configure your Client ID and execute loopback authentication via Dashboard or Portal first.";
                InventoryInfoBar.Severity = InfoBarSeverity.Error;
                InventoryInfoBar.IsOpen = true;
                InventoryCardsGrid.ItemsSource = null;
                return;
            }

            DiapStashClient.Instance.ConfigureAuthentication(currentToken, currentClientId);
            var stockItemsList = await DiapStashClient.Instance.FetchCurrentStockItemsAsync();

            // Intercept 429 Too Many Requests structural limits bounds
            if (DiapStashClient.Instance.IsRateLimited)
            {
                CatalogScrollViewer.Visibility = Visibility.Collapsed;
                InventoryInfoBar.Title = "Rate Limited (429)";
                InventoryInfoBar.Message = "⚠️ Too many requests! API limit reached. Core endpoint cooldown threshold active. Try again later.";
                InventoryInfoBar.Severity = InfoBarSeverity.Warning;
                InventoryInfoBar.IsOpen = true;
                InventoryCardsGrid.ItemsSource = null;
                return;
            }

            // FIXED: Verify if endpoint processing returned empty arrays due to token dropping / expired contexts (401 response)
            if (stockItemsList == null || stockItemsList.Count == 0)
            {
                CatalogScrollViewer.Visibility = Visibility.Collapsed;
                InventoryInfoBar.Title = "Session Unauthorized";
                InventoryInfoBar.Message = "❌ API authorization failed. Token context rejected or expired. Execute Portal Login to re-authenticate.";
                InventoryInfoBar.Severity = InfoBarSeverity.Error;
                InventoryInfoBar.IsOpen = true;
                InventoryCardsGrid.ItemsSource = null;
                return;
            }

            // Reset visibility states upon successful sync response processing loops
            InventoryInfoBar.IsOpen = false;
            CatalogScrollViewer.Visibility = Visibility.Visible;

            foreach (var item in stockItemsList)
            {
                if (string.IsNullOrWhiteSpace(item.ImageUrl))
                {
                    item.ImageUrl = "ms-appx:///Assets/StoreLogo.png";
                }
            }

            InventoryCardsGrid.ItemsSource = stockItemsList;
        }

        private void RefreshStock_Click(object sender, RoutedEventArgs e)
        {
            _ = RefreshStockAsync();
        }

        private async void InspectRawStock_Click(object sender, RoutedEventArgs e)
        {
            RawStockDiagnosticBox.Visibility = Visibility.Visible;
            RawStockDiagnosticBox.Text = "⌛ Requesting raw catalog payloads stream...";
            string rawJson = await DiapStashClient.Instance.GetRawEndpointDataAsync("api/v1/stock/disposables");
            RawStockDiagnosticBox.Text = rawJson;
        }
    }
}