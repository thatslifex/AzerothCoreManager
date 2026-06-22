using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class ModuleManagerPage : Page
{
    private readonly ModuleService _moduleService = new();
    private readonly BuildService _buildService = new();
    private CancellationTokenSource? _cts;
    private List<ModuleService.ModuleInfo> _catalog = new();
    private List<ModuleService.InstalledModule> _installed = new();

    public ModuleManagerPage()
    {
        InitializeComponent();
        _moduleService.LogMessage += AppendLog;
        ShowCatalog();
    }

    private void ShowCatalog()
    {
        CatalogTab.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x5A));
        CatalogTab.FontWeight = FontWeights.SemiBold;
        InstalledTab.ClearValue(Control.BackgroundProperty);
        InstalledTab.FontWeight = FontWeights.Normal;

        CatalogScroller.Visibility = Visibility.Visible;
        InstalledScroller.Visibility = Visibility.Collapsed;
        EmptyText.Visibility = _catalog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = "Search GitHub to find modules.";
    }

    private void ShowInstalled()
    {
        InstalledTab.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x5A));
        InstalledTab.FontWeight = FontWeights.SemiBold;
        CatalogTab.ClearValue(Control.BackgroundProperty);
        CatalogTab.FontWeight = FontWeights.Normal;

        CatalogScroller.Visibility = Visibility.Collapsed;
        InstalledScroller.Visibility = Visibility.Visible;
        EmptyText.Visibility = _installed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = "No modules installed. Browse the GitHub catalog to install some.";
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e) => await DoSearchAsync();

    private async Task DoSearchAsync()
    {
        var query = SearchBox.Text.Trim();
        AppendLog($"Searching GitHub for AzerothCore modules{(string.IsNullOrEmpty(query) ? "" : $" matching '{query}'")}...");

        _cts = new CancellationTokenSource();
        try
        {
            _catalog = await _moduleService.SearchModulesAsync(query, _cts.Token);
            CatalogList.ItemsSource = _catalog;
            AppendLog($"Found {_catalog.Count} modules.");
            ShowCatalog();
        }
        catch (Exception ex)
        {
            AppendLog($"Search failed: {ex.Message}");
        }
        finally { _cts = null; }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = DoSearchAsync();
    }

    private void RefreshLocalButton_Click(object sender, RoutedEventArgs e) => DoRefreshLocal();

    private void DoRefreshLocal()
    {
        var settings = App.Settings.Load();
        if (string.IsNullOrEmpty(settings.SourcePath))
        {
            AppendLog("Source path not configured. Run Setup first.");
            return;
        }

        _installed = _moduleService.GetInstalledModules(settings.SourcePath);
        InstalledList.ItemsSource = _installed;
        AppendLog($"Found {_installed.Count} installed modules.");
        ShowInstalled();
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModuleService.ModuleInfo module)
        {
            var settings = App.Settings.Load();
            if (string.IsNullOrEmpty(settings.SourcePath))
            {
                AppendLog("Source path not configured. Run Setup first.");
                return;
            }

            AppendLog($"Installing module '{module.Name}'...");
            _cts = new CancellationTokenSource();
            try
            {
                var ok = await _moduleService.InstallModuleAsync(
                    module.CloneUrl, settings.SourcePath, module.DefaultBranch, _cts.Token);

                if (ok)
                {
                    AppendLog($"Module '{module.Name}' installed. Run Rebuild CMake to integrate it.");
                    DoRefreshLocal();
                }
            }
            catch (Exception ex) { AppendLog($"Install failed: {ex.Message}"); }
            finally { _cts = null; }
        }
    }

    private void OpenGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModuleService.ModuleInfo module)
        {
            Process.Start(new ProcessStartInfo(module.HtmlUrl) { UseShellExecute = true });
        }
    }

    private async void UpdateLocalButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModuleService.InstalledModule mod)
        {
            AppendLog($"Updating module '{mod.Name}'...");
            _cts = new CancellationTokenSource();
            try
            {
                var ok = await _moduleService.PullModuleAsync(mod.Path, "master", _cts.Token);
                AppendLog(ok ? $"Module '{mod.Name}' updated." : $"Update failed for '{mod.Name}'.");
            }
            catch (Exception ex) { AppendLog($"Update failed: {ex.Message}"); }
            finally { _cts = null; }
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModuleService.InstalledModule mod)
        {
            var result = System.Windows.MessageBox.Show(
                $"Remove module '{mod.Name}'?\n\nThis will delete the folder:\n{mod.Path}",
                "Remove Module", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var ok = _moduleService.RemoveModule(mod.Path);
                if (ok) DoRefreshLocal();
            }
        }
    }

    private async void RebuildButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings.Load();
        if (string.IsNullOrEmpty(settings.SourcePath) || string.IsNullOrEmpty(settings.BuildPath))
        {
            AppendLog("Source or build path not configured. Run Setup first.");
            return;
        }

        AppendLog("Reconfiguring CMake to integrate modules...");
        _cts = new CancellationTokenSource();
        try
        {
            var ok = await _buildService.ConfigureAsync(settings.SourcePath, settings.BuildPath, _cts.Token);
            if (ok)
            {
                AppendLog("CMake reconfigured. Building...");
                await _buildService.BuildAsync(settings.BuildPath, "RelWithDebInfo", _cts.Token);
            }
        }
        catch (Exception ex) { AppendLog($"Rebuild failed: {ex.Message}"); }
        finally { _cts = null; }
    }

    private void CatalogTab_Click(object sender, RoutedEventArgs e) => ShowCatalog();
    private void InstalledTab_Click(object sender, RoutedEventArgs e) => ShowInstalled();

    private void ClearLogButton_Click(object sender, RoutedEventArgs e) => LogOutput.Clear();

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
        WikiLinkService.OpenHelp("module_manager");
    }
}
