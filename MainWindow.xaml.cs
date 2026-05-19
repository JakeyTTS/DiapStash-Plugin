using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
            NavView.ItemInvoked += NavView_ItemInvokedHandler;

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "appicon.ico");
                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
            }

            JakeyTtsClient.Instance.LogReceived += OnLogReceived;
            this.Closed += MainWindow_Closed;

            NavView.SelectedItem = NavView.MenuItems[0];
            MainContentFrame.Content = _homePage;

            // Always activate the window normally to ensure it displays standard layouts natively
            this.Activate();
        }

        public UserControl GetPortalPageInstance() => _portalPage;

        private void NavView_ItemInvokedHandler(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var item = args.InvokedItemContainer as NavigationViewItem;
            if (item?.Tag != null) HandleNavigation(item.Tag.ToString());
        }

        public void NavigateToPage(string pageTag)
        {
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                foreach (var menuItem in NavView.MenuItems)
                {
                    if (menuItem is NavigationViewItem item && item.Tag?.ToString() == pageTag)
                    {
                        NavView.SelectedItem = item;
                        HandleNavigation(pageTag);
                        break;
                    }
                }
            });
        }

        private void HandleNavigation(string pageTag)
        {
            switch (pageTag)
            {
                case "Home": MainContentFrame.Content = _homePage; break;
                case "StashAuth": MainContentFrame.Content = _portalPage; break;
                case "Inventory": MainContentFrame.Content = _inventoryPage; _ = _inventoryPage.RefreshStockAsync(); break;
                case "ChangeTracker": MainContentFrame.Content = _changeTrackerPage; _ = _changeTrackerPage.RefreshChangeAsync(); break;
                case "Injections": MainContentFrame.Content = _injectionsPage; break;
            }
        }

        public void Log(string message) { if (_homePage is HomePage home) home.AppendLog(message); }
        private void OnLogReceived(string message) => Log(message);

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            NavView.ItemInvoked -= NavView_ItemInvokedHandler;
            JakeyTtsClient.Instance.LogReceived -= OnLogReceived;
            Task.Run(async () => { try { await JakeyTtsClient.Instance.StopAsync(); } catch { } });
            (_portalPage as PortalPage)?.ShutdownServer();
        }
    }
}