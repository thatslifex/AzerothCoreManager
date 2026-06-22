using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AzerothCoreManager.Controls;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class ServerControlPage : Page
{
    private readonly ServerService _server = new();
    private readonly ConfigService _config = new();
    private DispatcherTimer? _uptimeTimer;
    private DateTime _authStartTime;
    private DateTime _worldStartTime;

    public ServerControlPage()
    {
        InitializeComponent();
        WireEvents();
        _ = RefreshMySqlStatusAsync();
    }

    private void WireEvents()
    {
        _server.StatusChanged += OnStatusChanged;
        _server.OutputReceived += OnOutputReceived;
        _server.LogMessage += msg => Dispatcher.Invoke(() => AppendConsole(msg));

        AuthCard.StartRequested += () => _ = StartAuthAsync();
        AuthCard.StopRequested += () => _ = StopAuthAsync();
        AuthCard.RestartRequested += () => _ = RestartAuthAsync();

        WorldCard.StartRequested += () => _ = StartWorldAsync();
        WorldCard.StopRequested += () => _ = StopWorldAsync();
        WorldCard.RestartRequested += () => _ = RestartWorldAsync();

        AuthCard.SetServerInfo("Authserver");
        WorldCard.SetServerInfo("Worldserver");
    }

    private void OnStatusChanged(ServerProcessType type, bool online)
    {
        Dispatcher.Invoke(() =>
        {
            if (type == ServerProcessType.Authserver)
            {
                AuthCard.SetStatus(online);
                if (online) _authStartTime = DateTime.Now;
            }
            else
            {
                WorldCard.SetStatus(online);
                if (online) _worldStartTime = DateTime.Now;
            }

            UpdateUptimeTimer();
        });
    }

    private void OnOutputReceived(string line, ServerProcessType type)
    {
        Dispatcher.Invoke(() =>
        {
            AppendConsole($"[{type}] {line}");
            if (type == ServerProcessType.Authserver)
                AuthCard.AppendOutput(line);
            else
                WorldCard.AppendOutput(line);
        });
    }

    private void UpdateUptimeTimer()
    {
        if (_server.IsAuthRunning || _server.IsWorldRunning)
        {
            if (_uptimeTimer == null)
            {
                _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _uptimeTimer.Tick += (s, e) =>
                {
                    if (_server.IsAuthRunning)
                        AuthCard.SetUptime(DateTime.Now - _authStartTime);
                    if (_server.IsWorldRunning)
                        WorldCard.SetUptime(DateTime.Now - _worldStartTime);
                };
                _uptimeTimer.Start();
            }
        }
        else
        {
            _uptimeTimer?.Stop();
            _uptimeTimer = null;
            AuthCard.SetUptime(null);
            WorldCard.SetUptime(null);
        }
    }

    private async Task StartAuthAsync()
    {
        var settings = App.Settings.Load();
        AppendConsole("Starting authserver...");
        _server.StartAuthserver(settings.ServerPath);
    }

    private async Task StopAuthAsync()
    {
        AppendConsole("Stopping authserver...");
        await _server.StopProcessAsync(ServerProcessType.Authserver);
    }

    private async Task RestartAuthAsync()
    {
        var settings = App.Settings.Load();
        AppendConsole("Restarting authserver...");
        await _server.RestartProcessAsync(ServerProcessType.Authserver, settings.ServerPath);
    }

    private async Task StartWorldAsync()
    {
        var settings = App.Settings.Load();
        AppendConsole("Starting worldserver...");
        _server.StartWorldserver(settings.ServerPath);
    }

    private async Task StopWorldAsync()
    {
        AppendConsole("Stopping worldserver...");
        await _server.StopProcessAsync(ServerProcessType.Worldserver);
    }

    private async Task RestartWorldAsync()
    {
        var settings = App.Settings.Load();
        AppendConsole("Restarting worldserver...");
        await _server.RestartProcessAsync(ServerProcessType.Worldserver, settings.ServerPath);
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

    private async void RestartAllButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings.Load();
        AppendConsole("=== Restarting all servers ===");
        await _server.StopAllAsync();
        await Task.Delay(1000);
        _server.StartAuthserver(settings.ServerPath);
        await Task.Delay(2000);
        _server.StartWorldserver(settings.ServerPath);
    }

    private async void RefreshMySqlButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshMySqlStatusAsync();
    }

    private async Task RefreshMySqlStatusAsync()
    {
        var settings = App.Settings.Load();
        var online = await _server.IsMySqlRunningAsync(
            settings.MySqlHost, settings.MySqlPort, "acore", "acore");

        Dispatcher.Invoke(() =>
        {
            if (online)
            {
                MySqlDot.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));
                MySqlStatus.Text = "Online";
                MySqlStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x00));
            }
            else
            {
                MySqlDot.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                MySqlStatus.Text = "Offline";
                MySqlStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            }
        });
    }

    private void ClearConsoleButton_Click(object sender, RoutedEventArgs e)
    {
        ConsoleOutput.Clear();
    }

    private void AppendConsole(string text)
    {
        ConsoleOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        ConsoleOutput.ScrollToEnd();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("server_control");
    }
}
