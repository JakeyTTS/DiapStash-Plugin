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

            if (string.IsNullOrEmpty(currentToken) || string.IsNullOrEmpty(currentClientId)) return;

            DiapStashClient.Instance.ConfigureAuthentication(currentToken, currentClientId);
            var stockItemsList = await DiapStashClient.Instance.FetchCurrentStockItemsAsync();

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