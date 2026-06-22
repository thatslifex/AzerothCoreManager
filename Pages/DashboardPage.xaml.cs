using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class DashboardPage : Page
{
    private readonly ServerService _server = new();
    private readonly UpdateService _update = new();
    private DispatcherTimer? _statusTimer;

    public DashboardPage()
    {
        InitializeComponent();
        WireServerEvents();
        StartStatusPolling();
    }

    private void WireServerEvents()
    {
        _server.StatusChanged += (type, online) =>
        {
            Dispatcher.Invoke(() => UpdateStatusCard(type, online));
        };

        _server.OutputReceived += (line, type) =>
        {
            Dispatcher.Invoke(() => AppendConsole($"[{type}] {line}"));
        };

        _server.LogMessage += msg =>
        {
            Dispatcher.Invoke(() => AppendConsole(msg));
        };
    }

    private void StartStatusPolling()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += async (s, e) =>
        {
            var settings = App.Settings.Load();

            var db = new DatabaseService();
            var mysqlOk = await db.TestConnectionAsync(
                settings.MySqlHost, settings.MySqlPort, "acore", "acore");
            UpdateMySqlStatus(mysqlOk);

            UpdateStatusCard(ServerProcessType.Authserver, _server.IsAuthRunning);
            UpdateStatusCard(ServerProcessType.Worldserver, _server.IsWorldRunning);
        };
        _statusTimer.Start();

        Dispatcher.BeginInvoke(async () =>
        {
            var settings = App.Settings.Load();
            var db = new DatabaseService();
            var mysqlOk = await db.TestConnectionAsync(
                settings.MySqlHost, settings.MySqlPort, "acore", "acore");
            UpdateMySqlStatus(mysqlOk);
        });
    }

    private void UpdateStatusCard(ServerProcessType type, bool online)
    {
        var statusBlock = type == ServerProcessType.Authserver ? AuthStatus : WorldStatus;
        if (online)
        {
            statusBlock.Text = "Online";
            statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x00));
        }
        else
        {
            statusBlock.Text = "Offline";
            statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
    }

    private void UpdateMySqlStatus(bool online)
    {
        if (online)
        {
            MySqlStatus.Text = "Online";
            MySqlStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x00));
        }
        else
        {
            MySqlStatus.Text = "Offline";
            MySqlStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }
    }

    private void AppendConsole(string text)
    {
        ConsoleOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        ConsoleOutput.ScrollToEnd();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("dashboard");
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var cmd = CommandInput.Text.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        AppendConsole($"> {cmd}");
        CommandInput.Clear();

        var settings = App.Settings.Load();
        if (_server.IsWorldRunning && !string.IsNullOrEmpty(settings.SoapUsername))
        {
            var config = new ConfigService();
            var confPath = Path.Combine(settings.ServerPath, "configs", "worldserver.conf");
            if (File.Exists(confPath))
            {
                var (enabled, ip, port) = config.ReadSoapConfig(confPath);
                if (enabled)
                {
                    var soap = new SoapService();
                    var (success, response) = await soap.SendCommandAsync(
                        ip, port, settings.SoapUsername, settings.SoapPassword, cmd);
                    AppendConsole(success ? response : $"ERROR: {response}");
                    return;
                }
            }
        }

        AppendConsole("[Worldserver not running or SOAP not configured — command queued]");
    }

    private async void StartAllButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings.Load();
        AppendConsole("=== Starting all servers ===");
        _server.StartAuthserver(settings.ServerPath);
        await Task.Delay(2000);
        _server.StartWorldserver(settings.ServerPath);
    }

    private async void StopAllButton_Click(object sender, RoutedEventArgs e)
    {
        AppendConsole("=== Stopping all servers ===");
        await _server.StopAllAsync();
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new Windows.BackupManagerWindow { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        AppendConsole("[Checking for updates...]");
        var result = await _update.CheckForUpdateAsync("thatslifex", "AzerothCoreManager", "0.2.2");
        if (result.HasValue)
        {
            AppendConsole($"Update available: v{result.Value.Version} ({result.Value.Size / 1024 / 1024} MB)");
            AppendConsole($"Download: {result.Value.DownloadUrl}");
        }
        else
        {
            AppendConsole("No updates available.");
        }
    }
}
