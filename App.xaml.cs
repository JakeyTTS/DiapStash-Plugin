using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DiapStash_Plugin
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            StringBuilder errorReport = new StringBuilder();
            errorReport.AppendLine("==========================================================================");
            errorReport.AppendLine($"🚨 WINUI CRASH INTERCEPTED: {DateTime.Now}");
            errorReport.AppendLine($"Message: {e.Message}");
            errorReport.AppendLine($"Exception Type: {e.Exception?.GetType().FullName}");
            errorReport.AppendLine("==========================================================================");
            errorReport.AppendLine("📝 FULL RUNTIME EXCEPTION STACK TRACE:");
            errorReport.AppendLine(e.Exception?.ToString() ?? "No diagnostic exception sub-properties available.");
            errorReport.AppendLine("==========================================================================");

            string fullErrorText = errorReport.ToString();
            System.Diagnostics.Debug.WriteLine(fullErrorText);

            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(fullErrorText);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                System.Diagnostics.Debug.WriteLine("📋 Full stack trace automatically copied to clipboard!");
            }
            catch (Exception clipEx)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Clipboard copy skipped due to cross-thread boundaries: {clipEx.Message}");
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            JakeyTtsClient.Instance.LoadRulesFromSettings();

            OverlayServer.Instance.Start();

            _window = new MainWindow();
            _window.Activate();
        }
        }
}