using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SoulScript.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += (sender, error) =>
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled WinUI Exception: {error.Exception.Message}");
                System.Diagnostics.Debug.WriteLine(error.Exception.StackTrace);
                // Optionally log to file or show dialog if UI is still alive
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
