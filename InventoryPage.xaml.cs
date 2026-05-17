using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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

            // Evaluate configuration credentials state parameters synchronously instantly
            if (string.IsNullOrEmpty(currentToken) || string.IsNullOrEmpty(currentClientId))
            {
                InventoryInfoBar.Title = "Authentication Required";
                InventoryInfoBar.Message = "⚠️ Not logged in. Please configure your Client ID and execute loopback authentication via Dashboard or Portal first.";
                InventoryInfoBar.Severity = InfoBarSeverity.Error;
                InventoryInfoBar.IsOpen = true;
                InventoryCardsGrid.ItemsSource = null;
                return;
            }

            DiapStashClient.Instance.ConfigureAuthentication(currentToken, currentClientId);

            try
            {
                // Fetch stock items from the background network worker stack safely
                var stockItemsList = await DiapStashClient.Instance.FetchCurrentStockItemsAsync();

                if (this.DispatcherQueue == null) return;

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    // Catch thread execution anomalies if controls are missing
                    if (InventoryInfoBar == null || InventoryCardsGrid == null) return;

                    if (DiapStashClient.Instance.IsRateLimited)
                    {
                        InventoryInfoBar.Title = "Rate Limited (429)";
                        InventoryInfoBar.Message = "⚠️ Too many requests! API limit reached. Core endpoint cooldown threshold active. Try again later.";
                        InventoryInfoBar.Severity = InfoBarSeverity.Warning;
                        InventoryInfoBar.IsOpen = true;
                        InventoryCardsGrid.ItemsSource = null;
                        return;
                    }

                    if (stockItemsList == null || stockItemsList.Count == 0)
                    {
                        InventoryInfoBar.Title = "Session Unauthorized";
                        InventoryInfoBar.Message = "❌ API authorization failed. Token context rejected, empty or expired. Execute Portal Login to re-authenticate.";
                        InventoryInfoBar.Severity = InfoBarSeverity.Error;
                        InventoryInfoBar.IsOpen = true;
                        InventoryCardsGrid.ItemsSource = null;
                        return;
                    }

                    // Clear error notifications upon clean data validation response checks
                    InventoryInfoBar.IsOpen = false;

                    // Fallback injection to replace empty image paths with a standard local asset link path directly
                    foreach (var item in stockItemsList)
                    {
                        if (string.IsNullOrWhiteSpace(item.ImageUrl) || !item.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            // Injects local icon asset link directly if CDN thumbnail field returns empty string values
                            item.ImageUrl = "ms-appx:///Assets/StoreLogo.png";
                        }
                    }

                    // Feed collection directly straight to the UI item grid source binding list context
                    InventoryCardsGrid.ItemsSource = stockItemsList;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Catalog data dispatch processing loop aborted: {ex.Message}");
            }
        }

        private void RefreshStock_Click(object sender, RoutedEventArgs e)
        {
            _ = RefreshStockAsync();
        }

        private async void InspectRawStock_Click(object sender, RoutedEventArgs e)
        {
            if (RawStockDiagnosticBox == null) return;

            RawStockDiagnosticBox.Visibility = Visibility.Visible;
            RawStockDiagnosticBox.Text = "⌛ Requesting raw catalog payloads stream...";
            string rawJson = await DiapStashClient.Instance.GetRawEndpointDataAsync("api/v1/stock/disposables");
            RawStockDiagnosticBox.Text = rawJson;
        }
    }
}