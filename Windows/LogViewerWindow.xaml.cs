using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Windows;

public partial class LogViewerWindow : FluentWindow
{
    private readonly LogService _log = new();
    private DispatcherTimer? _autoTimer;
    private string? _currentFile;

    public LogViewerWindow()
    {
        InitializeComponent();
        DiscoverLogs();
    }

    private void DiscoverLogs()
    {
        var settings = App.Settings.Load();
        var serverPath = settings.ServerPath;

        LogFileCombo.Items.Clear();
        if (!string.IsNullOrEmpty(serverPath) && Directory.Exists(serverPath))
        {
            var logs = _log.DiscoverLogs(serverPath);
            foreach (var log in logs)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{log.Name} ({log.Size / 1024.0:F1} KB)",
                    Tag = log.Path
                };
                LogFileCombo.Items.Add(item);
            }
        }

        if (LogFileCombo.Items.Count > 0)
            LogFileCombo.SelectedIndex = 0;
        else
            LogContent.Text = "No log files found. Configure server path in Settings and run Setup.";
    }

    private void LoadLog(string? filePath)
    {
        if (filePath == null || !File.Exists(filePath))
        {
            LogContent.Text = $"File not found: {filePath}";
            return;
        }

        _currentFile = filePath;
        var lines = _log.Tail(filePath, 500);
        LogContent.Text = string.Join(Environment.NewLine, lines);
        StatusText.Text = $"Showing last {lines.Length} lines of {Path.GetFileName(filePath)}";
    }

    private void LogFileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogFileCombo.SelectedItem is ComboBoxItem item && item.Tag is string path)
            LoadLog(path);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile != null)
            LoadLog(_currentFile);
        else
            DiscoverLogs();
    }

    private void FilterBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DoFilter();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e) => DoFilter();

    private void DoFilter()
    {
        var term = FilterBox.Text.Trim();
        if (string.IsNullOrEmpty(term) || _currentFile == null) return;

        var results = _log.Filter(_currentFile, term, 500);
        LogContent.Text = string.Join(Environment.NewLine, results);
        StatusText.Text = $"{results.Length} lines matching '{term}'";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        LogContent.Clear();
        FilterBox.Clear();
        StatusText.Text = "";
    }

    private void AutoRefreshCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshCheck.IsChecked == true)
        {
            _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _autoTimer.Tick += (s, args) =>
            {
                if (_currentFile != null) LoadLog(_currentFile);
            };
            _autoTimer.Start();
        }
        else
        {
            _autoTimer?.Stop();
            _autoTimer = null;
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("log_viewer");
    }
}
