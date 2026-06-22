using System.Threading.Tasks;
using System.Windows;
using AzerothCoreManager.Dialogs;
using AzerothCoreManager.Services;

namespace AzerothCoreManager;

public partial class App : Application
{
    public static SettingsService Settings { get; } = new();
    public static ThemeService Theme { get; } = new();
    public static LocalizationService Loc { get; } = new();
    public static readonly string Version = "0.9.0";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var settings = Settings.Load();
        Loc.SetLanguage(settings.Language);
        Theme.ApplyTheme(settings.Theme);

        if (settings.AutoCheckUpdates)
            _ = CheckForUpdatesAsync();
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = new UpdateService();
            var result = await update.CheckForUpdateAsync("thatslifex", "AzerothCoreManager", Version);

            if (result.HasValue)
            {
                var (newVersion, downloadUrl, size) = result.Value;
                Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new UpdateDialog(Version, newVersion, downloadUrl, size);
                    if (Current.MainWindow != null)
                        dialog.Owner = Current.MainWindow;
                    dialog.ShowDialog();
                });
            }
        }
        catch
        {
            // Silent fail — don't bother the user on startup
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Exception}");
        System.Windows.MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}", "AzerothCore Manager - Error",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }
}
