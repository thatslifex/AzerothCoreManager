using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using AzerothCoreManager.Models;
using AzerothCoreManager.Services;
using Microsoft.Win32;

namespace AzerothCoreManager.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {App.Version}";
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = App.Settings.Load();

        SourcePathBox.Text = s.SourcePath;
        BuildPathBox.Text = s.BuildPath;
        ServerPathBox.Text = s.ServerPath;
        MySqlHostBox.Text = s.MySqlHost;
        MySqlPortBox.Text = s.MySqlPort.ToString();
        MySqlRootPassBox.Text = s.MySqlRootPassword;
        SoapUserBox.Text = s.SoapUsername;
        SoapPassBox.Text = s.SoapPassword;

        // Theme
        foreach (ComboBoxItem item in ThemeCombo.Items)
        {
            if (item.Content.ToString() == s.Theme)
            {
                ThemeCombo.SelectedItem = item;
                break;
            }
        }

        // Language
        foreach (ComboBoxItem item in LanguageCombo.Items)
        {
            if (item.Tag?.ToString() == s.Language)
            {
                LanguageCombo.SelectedItem = item;
                break;
            }
        }

        // Background
        foreach (ComboBoxItem item in BackgroundCombo.Items)
        {
            if (item.Tag?.ToString() == s.BackgroundImage)
            {
                BackgroundCombo.SelectedItem = item;
                break;
            }
        }

        AutoUpdateCheck.IsChecked = s.AutoCheckUpdates;
        AutoRestartCheck.IsChecked = s.AutoRestartOnCrash;

        foreach (ComboBoxItem item in UpdateChannelCombo.Items)
        {
            if (item.Tag?.ToString() == s.UpdateChannel)
            {
                UpdateChannelCombo.SelectedItem = item;
                break;
            }
        }
    }

    private AppSettings CollectSettings()
    {
        return new AppSettings
        {
            FirstRunComplete = App.Settings.Load().FirstRunComplete,
            SourcePath = SourcePathBox.Text.Trim(),
            BuildPath = BuildPathBox.Text.Trim(),
            ServerPath = ServerPathBox.Text.Trim(),
            MySqlHost = MySqlHostBox.Text.Trim(),
            MySqlPort = int.TryParse(MySqlPortBox.Text.Trim(), out var p) ? p : 3306,
            MySqlRootPassword = MySqlRootPassBox.Text.Trim(),
            SoapUsername = SoapUserBox.Text.Trim(),
            SoapPassword = SoapPassBox.Text.Trim(),
            Language = ((ComboBoxItem)LanguageCombo.SelectedItem).Tag?.ToString() ?? "en",
            Theme = ((ComboBoxItem)ThemeCombo.SelectedItem).Content.ToString() ?? "Dark",
            BackgroundImage = ((ComboBoxItem)BackgroundCombo.SelectedItem).Tag?.ToString() ?? "background_wow.jpg",
            AutoCheckUpdates = AutoUpdateCheck.IsChecked ?? true,
            AutoRestartOnCrash = AutoRestartCheck.IsChecked ?? false,
            UpdateChannel = ((ComboBoxItem)UpdateChannelCombo.SelectedItem).Tag?.ToString() ?? "stable"
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = CollectSettings();
        App.Settings.Save(settings);
        App.Loc.SetLanguage(settings.Language);
        App.Theme.ApplyTheme(settings.Theme);

        System.Windows.MessageBox.Show("Settings saved successfully.", "Settings", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item)
        {
            var theme = item.Content.ToString() ?? "Dark";
            App.Theme.ApplyTheme(theme);
        }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            App.Loc.SetLanguage(lang);
        }
    }

    private void BrowseSourcePath_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder("Select AzerothCore Source Directory");
        if (path != null) SourcePathBox.Text = path;
    }

    private void BrowseBuildPath_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder("Select Build Output Directory");
        if (path != null) BuildPathBox.Text = path;
    }

    private void BrowseServerPath_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder("Select Server Directory");
        if (path != null) ServerPathBox.Text = path;
    }

    private static string? BrowseFolder(string description)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
            return dialog.FolderName;
        return null;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = CollectSettings();
        var dialog = new SaveFileDialog
        {
            Title = "Export Settings",
            Filter = "JSON Files (*.json)|*.json",
            FileName = "AzerothCoreManager-settings.json"
        };
        if (dialog.ShowDialog() == true)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            System.Windows.MessageBox.Show($"Settings exported to:\n{dialog.FileName}", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Settings",
            Filter = "JSON Files (*.json)|*.json"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    App.Settings.Save(settings);
                    LoadSettings();
                    App.Loc.SetLanguage(settings.Language);
                    App.Theme.ApplyTheme(settings.Theme);
                    System.Windows.MessageBox.Show("Settings imported successfully.", "Import", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to import settings:\n{ex.Message}", "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("settings");
    }
}
