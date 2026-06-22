using System.Windows;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class SetupPage : Page
{
    private readonly PrerequisitesService _prereqService = new();
    private readonly PrerequisitesInstaller _installer = new();
    private readonly SourceService _sourceService = new();
    private readonly BuildService _buildService = new();
    private readonly DeployService _deployService = new();
    private readonly ServerSetupService _setupService = new();
    private CancellationTokenSource? _cts;

    public SetupPage()
    {
        InitializeComponent();
        WireLogEvents();
    }

    private void WireLogEvents()
    {
        _installer.LogMessage += AppendLog;
        _installer.ProgressChanged += msg => Dispatcher.Invoke(() => StatusText.Text = msg);
        _sourceService.LogMessage += AppendLog;
        _buildService.LogMessage += AppendLog;
        _buildService.ProgressChanged += pct => Dispatcher.Invoke(() => OverallProgress.Value = pct);
        _deployService.LogMessage += AppendLog;
        _deployService.ProgressChanged += pct => Dispatcher.Invoke(() => OverallProgress.Value = pct);
        _setupService.LogMessage += AppendLog;
        _setupService.ProgressChanged += pct => Dispatcher.Invoke(() => OverallProgress.Value = pct);
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogOutput.ScrollToEnd();
        });
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e) => WikiLinkService.OpenHelp("setup");

    private async void RunAllButton_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        OverallProgress.Value = 0;
        StatusText.Text = "Starting full setup...";

        try
        {
            var settings = App.Settings.Load();
            var ct = _cts.Token;

            // Step 1: System Check
            AppendLog("=== Step 1: System Check ===");
            StatusText.Text = "Checking prerequisites...";
            var checks = _prereqService.CheckAll();
            foreach (var c in checks)
                AppendLog($"  {(c.Found ? "✅" : "❌")} {c.Name} {(c.Version != null ? $"(v{c.Version})" : "")}");
            OverallProgress.Value = 10;

            // Step 2: Install Missing
            AppendLog("=== Step 2: Installing Missing Prerequisites ===");
            StatusText.Text = "Installing missing tools...";
            await _installer.InstallMissing(checks, ct);
            OverallProgress.Value = 25;

            // Step 3: Clone Source
            AppendLog("=== Step 3: Source Code ===");
            StatusText.Text = "Cloning AzerothCore...";
            var cloneOk = await _sourceService.CloneAsync(
                "https://github.com/azerothcore/azerothcore-wotlk.git",
                settings.SourcePath, "master", ct);
            if (cloneOk) await _sourceService.InitSubmodulesAsync(settings.SourcePath, ct);
            OverallProgress.Value = 40;

            // Step 4: Build
            AppendLog("=== Step 4: Build ===");
            StatusText.Text = "Building AzerothCore...";
            var configOk = await _buildService.ConfigureAsync(settings.SourcePath, settings.BuildPath, ct);
            if (configOk)
                await _buildService.BuildAsync(settings.BuildPath, "RelWithDebInfo", ct);
            OverallProgress.Value = 80;

            // Step 5: Deploy + DB Init
            AppendLog("=== Step 5: Deploy & Database ===");
            StatusText.Text = "Deploying and initializing database...";
            var buildOutput = _buildService.GetBuildOutputPath(settings.BuildPath);
            await _deployService.DeployAsync(buildOutput, settings.ServerPath, ct);

            if (!string.IsNullOrEmpty(settings.MySqlRootPassword))
            {
                await _setupService.InitializeDatabaseAsync(
                    settings.MySqlHost, settings.MySqlPort, settings.MySqlRootPassword, ct);
            }
            OverallProgress.Value = 95;

            // Step 6: Client Data
            AppendLog("=== Step 6: Client Data ===");
            StatusText.Text = "Downloading client data...";
            await _setupService.DownloadClientDataAsync(settings.ServerPath, ct);
            OverallProgress.Value = 100;

            StatusText.Text = "✅ Setup complete!";
            AppendLog("=== Setup Complete! ===");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Setup cancelled by user.");
            StatusText.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
            StatusText.Text = "Error — see log";
        }
        finally
        {
            SetButtonsEnabled(true);
            _cts = null;
        }
    }

    private async void Step1Button_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Checking prerequisites...";
        AppendLog("=== System Check ===");
        var checks = _prereqService.CheckAll();
        foreach (var c in checks)
            AppendLog($"  {(c.Found ? "✅" : "❌")} {c.Name} {(c.Version != null ? $"(v{c.Version})" : "")}");
        StatusText.Text = "Check complete.";
    }

    private async void Step2Button_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        StatusText.Text = "Installing missing prerequisites...";
        AppendLog("=== Installing Missing Prerequisites ===");

        try
        {
            var checks = _prereqService.CheckAll();
            await _installer.InstallMissing(checks, _cts.Token);
            StatusText.Text = "Installation complete.";
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
            _cts = null;
        }
    }

    private async void Step3Button_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        StatusText.Text = "Cloning source...";
        AppendLog("=== Cloning AzerothCore ===");

        try
        {
            var settings = App.Settings.Load();
            var ok = await _sourceService.CloneAsync(
                "https://github.com/azerothcore/azerothcore-wotlk.git",
                settings.SourcePath, "master", _cts.Token);
            if (ok) await _sourceService.InitSubmodulesAsync(settings.SourcePath, _cts.Token);
            StatusText.Text = ok ? "Clone complete." : "Clone failed.";
        }
        catch (Exception ex) { AppendLog($"ERROR: {ex.Message}"); }
        finally { SetButtonsEnabled(true); _cts = null; }
    }

    private async void Step4Button_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        StatusText.Text = "Building...";
        AppendLog("=== Building AzerothCore ===");

        try
        {
            var settings = App.Settings.Load();
            var ok = await _buildService.ConfigureAsync(settings.SourcePath, settings.BuildPath, _cts.Token);
            if (ok) await _buildService.BuildAsync(settings.BuildPath, "RelWithDebInfo", _cts.Token);
            StatusText.Text = ok ? "Build complete." : "Build failed.";
        }
        catch (Exception ex) { AppendLog($"ERROR: {ex.Message}"); }
        finally { SetButtonsEnabled(true); _cts = null; }
    }

    private async void Step5Button_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        StatusText.Text = "Deploying...";
        AppendLog("=== Deploy & Database ===");

        try
        {
            var settings = App.Settings.Load();
            var buildOutput = _buildService.GetBuildOutputPath(settings.BuildPath);
            await _deployService.DeployAsync(buildOutput, settings.ServerPath, _cts.Token);

            if (!string.IsNullOrEmpty(settings.MySqlRootPassword))
            {
                await _setupService.InitializeDatabaseAsync(
                    settings.MySqlHost, settings.MySqlPort, settings.MySqlRootPassword, _cts.Token);
            }
            StatusText.Text = "Deploy complete.";
        }
        catch (Exception ex) { AppendLog($"ERROR: {ex.Message}"); }
        finally { SetButtonsEnabled(true); _cts = null; }
    }

    private async void Step6Button_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        StatusText.Text = "Downloading client data...";
        AppendLog("=== Client Data ===");

        try
        {
            var settings = App.Settings.Load();
            await _setupService.DownloadClientDataAsync(settings.ServerPath, _cts.Token);
            StatusText.Text = "Client data ready.";
        }
        catch (Exception ex) { AppendLog($"ERROR: {ex.Message}"); }
        finally { SetButtonsEnabled(true); _cts = null; }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        RunAllButton.IsEnabled = enabled;
        Step1Button.IsEnabled = enabled;
        Step2Button.IsEnabled = enabled;
        Step3Button.IsEnabled = enabled;
        Step4Button.IsEnabled = enabled;
        Step5Button.IsEnabled = enabled;
        Step6Button.IsEnabled = enabled;
    }
}
