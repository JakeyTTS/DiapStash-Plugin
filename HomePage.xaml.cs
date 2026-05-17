using System;
using Microsoft.UI.Xaml.Controls;

namespace DiapStash_Plugin
{
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            this.InitializeComponent();
        }

        public void AppendLog(string message)
        {
            if (this.DispatcherQueue == null) return;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (LogBlock != null)
                    {
                        LogBlock.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                        LogScroll?.ChangeView(0, LogScroll.ScrollableHeight, 1);
                    }
                }
                catch { }
            });
        }
    }
}