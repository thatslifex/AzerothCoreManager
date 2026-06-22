using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using AzerothCoreManager.Services;
using Microsoft.Win32;

namespace AzerothCoreManager.Windows;

public partial class BackupManagerWindow : FluentWindow
{
    private readonly BackupService _backup = new();
    private CancellationTokenSource? _cts;

    public BackupManagerWindow()
    {
        InitializeComponent();
        _backup.LogMessage += AppendLog;
        LoadSettings();
        RefreshList();
    }

    private void LoadSettings()
    {
        var s = App.Settings.Load();
        var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AzerothCoreBackups");
        BackupDirBox.Text = Directory.Exists(s.ServerPath) ? Path.Combine(s.ServerPath, "backups") : defaultDir;
    }

    private void RefreshList()
    {
        var dir = BackupDirBox.Text.Trim();
        if (!Directory.Exists(dir))
        {
            BackupGrid.ItemsSource = null;
            return;
        }
        var backups = _backup.ListBackups(dir);
        BackupGrid.ItemsSource = backups;
        StatusText.Text = $"{backups.Count} backup(s) found";
    }

    private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings.Load();
        var dir = BackupDirBox.Text.Trim();
        Directory.CreateDirectory(dir);

        StatusText.Text = "Creating backup...";
        CreateBackupButton.IsEnabled = false;
        _cts = new CancellationTokenSource();

        try
        {
            var result = await _backup.CreateBackupAsync(
                settings.MySqlHost, settings.MySqlPort, "acore", "acore", dir, _cts.Token);

            if (result != null)
            {
                StatusText.Text = "Backup complete.";
                RefreshList();
            }
            else
            {
                StatusText.Text = "Backup failed — see log.";
            }
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
            StatusText.Text = "Error";
        }
        finally
        {
            CreateBackupButton.IsEnabled = true;
            _cts = null;
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is not BackupService.BackupInfo backup) return;

        var result = System.Windows.MessageBox.Show(
            $"Restore database from '{backup.FileName}'?\n\nThis will OVERWRITE all current data!",
            "Confirm Restore", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        var settings = App.Settings.Load();
        StatusText.Text = "Restoring...";
        RestoreButton.IsEnabled = false;
        _cts = new CancellationTokenSource();

        try
        {
            var ok = await _backup.RestoreBackupAsync(
                settings.MySqlHost, settings.MySqlPort, "acore", "acore", backup.Path, _cts.Token);
            StatusText.Text = ok ? "Restore complete." : "Restore failed — see log.";
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
            StatusText.Text = "Error";
        }
        finally
        {
            RestoreButton.IsEnabled = true;
            _cts = null;
        }
    }

    private void RefreshListButton_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select Backup Directory", Multiselect = false };
        if (dialog.ShowDialog() == true)
        {
            BackupDirBox.Text = dialog.FolderName;
            RefreshList();
        }
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogOutput.ScrollToEnd();
        });
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("backup_manager");
    }
}
