using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AzerothCoreManager.Controls;

public partial class ServerStatusCard : UserControl
{
    public event Action? StartRequested;
    public event Action? StopRequested;
    public event Action? RestartRequested;

    public ServerStatusCard()
    {
        InitializeComponent();
        UpdateButtonStates(false);
    }

    public void SetServerInfo(string name, string iconSymbol = "Server24")
    {
        ServerName.Text = name;
    }

    public void SetStatus(bool online, string? lastOutput = null)
    {
        if (online)
        {
            StatusDot.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));
            StatusText.Text = "Online";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x00));
        }
        else
        {
            StatusDot.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            StatusText.Text = "Offline";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        if (lastOutput != null)
            LastOutput.Text = lastOutput;

        UpdateButtonStates(online);
    }

    public void SetUptime(TimeSpan? uptime)
    {
        UptimeText.Text = uptime.HasValue
            ? $"Uptime: {uptime.Value:hh\\:mm\\:ss}"
            : "Uptime: --";
    }

    public void AppendOutput(string line)
    {
        Dispatcher.Invoke(() => LastOutput.Text = line);
    }

    private void UpdateButtonStates(bool online)
    {
        StartButton.IsEnabled = !online;
        StopButton.IsEnabled = online;
        RestartButton.IsEnabled = online;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e) => StartRequested?.Invoke();
    private void StopButton_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
    private void RestartButton_Click(object sender, RoutedEventArgs e) => RestartRequested?.Invoke();
}
