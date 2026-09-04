using EVADAV.ImGuiUi;
using EVADAV.Theme;
using Class;
using System.Windows;

namespace EVADAV
{
    public partial class App : Application
    {
        private AppController? _controller;
        private EvadavOverlay? _overlay;

        protected override async void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (_, args) =>
            {
                try { MessageBox.Show(args.Exception.Message, "EVADAV"); } catch { }
                args.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
            };

            InitializeTheme();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _controller = new AppController();
            var host = _controller.CreateHiddenHost();
            MainWindow = host;
            host.Show();

            await _controller.InitializeAsync();

            _overlay = new EvadavOverlay(_controller);
            _ = RunOverlayAsync();
        }

        private async Task RunOverlayAsync()
        {
            try
            {
                await _overlay!.Run();
            }
            finally
            {
                _controller?.Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _overlay?.Close(); } catch { }
            base.OnExit(e);
        }

        private void InitializeTheme()
        {
            try
            {
                var colorState = new Dictionary<string, dynamic>
                {
                    { "Theme Color", "#FF00D26A" }
                };

                SaveDictionary.LoadJSON(colorState, "bin\\colors.cfg");

                if (colorState.TryGetValue("Theme Color", out var themeColor) && themeColor is string colorString)
                {
                    if (colorString.Equals("#FF722ED1", StringComparison.OrdinalIgnoreCase) ||
                        colorString.Equals("#722ED1", StringComparison.OrdinalIgnoreCase) ||
                        colorString.Equals("#FF00C8B4", StringComparison.OrdinalIgnoreCase) ||
                        colorString.Equals("#00C8B4", StringComparison.OrdinalIgnoreCase))
                    {
                        colorString = "#FF00D26A";
                    }
                    ThemeManager.SetThemeColor(colorString);
                    ImGuiUi.OstinStyle.SetAccent(ThemeManager.ThemeColor);
                }
                else
                {
                    ThemeManager.SetThemeColor("#FF00D26A");
                    ImGuiUi.OstinStyle.SetAccent(ThemeManager.ThemeColor);
                }
            }
            catch
            {
                ThemeManager.SetThemeColor("#FF00D26A");
                ImGuiUi.OstinStyle.SetAccent(ThemeManager.ThemeColor);
            }
        }
    }
}
