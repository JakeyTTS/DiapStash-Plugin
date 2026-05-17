using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DiapStash_Plugin
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        private readonly UserControl _homePage = new HomePage();
        private readonly UserControl _portalPage = new PortalPage();
        private readonly InventoryPage _inventoryPage = new InventoryPage();
        private readonly ChangeTrackerPage _changeTrackerPage = new ChangeTrackerPage();
        private readonly UserControl _injectionsPage = new InjectionsPage();

        public MainWindow()
        {
            Instance = this;
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = false;

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.SetIcon("Assets/StoreLogo.scale-400.png"); 
            }

            JakeyTtsClient.Instance.LogReceived += OnLogReceived;
            this.Closed += MainWindow_Closed;

            NavView.SelectedItem = NavView.MenuItems[0];
            MainContentFrame.Content = _homePage;
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var item = args.InvokedItemContainer as NavigationViewItem;
            if (item?.Tag == null) return;

            switch (item.Tag.ToString())
            {
                case "Home":
                    MainContentFrame.Content = _homePage;
                    break;
                case "StashAuth":
                    MainContentFrame.Content = _portalPage;
                    break;
                case "Inventory":
                    MainContentFrame.Content = _inventoryPage;
                    _ = _inventoryPage.RefreshStockAsync();
                    break;
                case "ChangeTracker":
                    MainContentFrame.Content = _changeTrackerPage;
                    _ = _changeTrackerPage.RefreshChangeAsync();
                    break;
                case "Injections":
                    MainContentFrame.Content = _injectionsPage;
                    break;
            }
        }

        public void Log(string message)
        {
            if (_homePage is HomePage home)
            {
                home.AppendLog(message);
            }
        }

        private void OnLogReceived(string message) => Log(message);

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            JakeyTtsClient.Instance.LogReceived -= OnLogReceived;
            await JakeyTtsClient.Instance.StopAsync();

            (_portalPage as PortalPage)?.ShutdownServer();
        }
    }
}