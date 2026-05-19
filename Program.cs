using System;
using System.Linq;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DiapStash_Plugin
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // Always let WinUI initialize its core platform mechanics normally
            Application.Start((p) =>
            {
                var dispatcherQueueContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(dispatcherQueueContext);
                new App();
            });
        }
    }
}