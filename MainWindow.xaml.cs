using System;
using System.IO;
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

            // Registro explícito del evento en el código subyacente
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
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Icon not found at target physical path: {iconPath}");
                }
            }

            JakeyTtsClient.Instance.LogReceived += OnLogReceived;
            this.Closed += MainWindow_Closed;

            NavView.SelectedItem = NavView.MenuItems[0];
            MainContentFrame.Content = _homePage;
        }

        // 💡 SOLUCIÓN AL ERROR: Método público expuesto para que HomePage pueda acceder a la instancia de PortalPage y refrescar tokens
        public UserControl GetPortalPageInstance()
        {
            return _portalPage;
        }

        private void NavView_ItemInvokedHandler(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var item = args.InvokedItemContainer as NavigationViewItem;
            if (item?.Tag == null) return;

            HandleNavigation(item.Tag.ToString());
        }

        public void NavigateToPage(string pageTag)
        {
            if (this.DispatcherQueue == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
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

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // Desuscripción segura de eventos en el desmontaje
            NavView.ItemInvoked -= NavView_ItemInvokedHandler;
            JakeyTtsClient.Instance.LogReceived -= OnLogReceived;

            // Ejecución sin bloqueo para evitar advertencias de async/void en eventos de ciclo de vida de WinUI 3
            _ = Task.Run(async () =>
            {
                try
                {
                    await JakeyTtsClient.Instance.StopAsync();
                }
                catch { }
            });

            (_portalPage as PortalPage)?.ShutdownServer();
        }
    }
}