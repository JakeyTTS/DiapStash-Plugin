using Microsoft.UI.Xaml;
using System;

namespace DiapStash_Plugin
{
    public sealed partial class LogWindow : Window
    {
        public LogWindow()
        {
            this.InitializeComponent();

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow?.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "appicon.ico"));
        }

        public void AppendLog(string message)
        {
            if (this.DispatcherQueue == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    LogBlock.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                    LogScroll?.ChangeView(0, LogScroll.ScrollableHeight, 1);
                }
                catch { }
            });
        }
    }
}