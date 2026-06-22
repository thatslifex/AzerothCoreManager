using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class AccountsPage : Page
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _cts;

    public AccountsPage()
    {
        InitializeComponent();
        ShowActivity();
        _ = RefreshAsync();
    }

    private void ShowActivity()
    {
        SetActiveTab(ActivityTab);
        ActivityScroller.Visibility = Visibility.Visible;
        MassCreateView.Visibility = Visibility.Collapsed;
        AuditScroller.Visibility = Visibility.Collapsed;
    }

    private void ShowMassCreate()
    {
        SetActiveTab(MassCreateTab);
        ActivityScroller.Visibility = Visibility.Collapsed;
        MassCreateView.Visibility = Visibility.Visible;
        AuditScroller.Visibility = Visibility.Collapsed;
    }

    private void ShowAudit()
    {
        SetActiveTab(AuditTab);
        ActivityScroller.Visibility = Visibility.Collapsed;
        MassCreateView.Visibility = Visibility.Collapsed;
        AuditScroller.Visibility = Visibility.Visible;
    }

    private void SetActiveTab(Button active)
    {
        var activeColor = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x5A));
        foreach (var btn in new[] { ActivityTab, MassCreateTab, AuditTab })
        {
            if (btn == active)
            {
                btn.Background = activeColor;
                btn.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                btn.ClearValue(Control.BackgroundProperty);
                btn.FontWeight = FontWeights.Normal;
            }
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        var settings = App.Settings.Load();
        _cts = new CancellationTokenSource();

        try
        {
            if (ActivityScroller.Visibility == Visibility.Visible)
            {
                var activity = await _db.GetPlayerActivityAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore", _cts.Token);
                ActivityGrid.ItemsSource = activity;
            }
            else if (AuditScroller.Visibility == Visibility.Visible)
            {
                var audit = await _db.GetGmAuditLogAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore", _cts.Token);
                AuditGrid.ItemsSource = audit;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cts = null; }
    }

    private async void CreateMassButton_Click(object sender, RoutedEventArgs e)
    {
        var baseName = BaseUsernameBox.Text.Trim();
        var password = MassPasswordBox.Text.Trim();
        if (!int.TryParse(CountBox.Text.Trim(), out var count) || count < 1 || count > 100)
        {
            System.Windows.MessageBox.Show("Count must be between 1 and 100.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(password))
        {
            System.Windows.MessageBox.Show("Base username and password are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = App.Settings.Load();
        MassStatus.Text = $"Creating {count} accounts...";
        CreateMassButton.IsEnabled = false;
        MassLog.Clear();

        _cts = new CancellationTokenSource();
        try
        {
            for (int i = 1; i <= count; i++)
            {
                var username = $"{baseName}{i}";
                await _db.CreateAccountAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore",
                    username, password, gmLevel: 0, _cts.Token);
                AppendMassLog($"✅ Created: {username}");
                MassStatus.Text = $"Created {i}/{count}...";
            }
            MassStatus.Text = $"✅ {count} accounts created successfully.";
        }
        catch (OperationCanceledException)
        {
            MassStatus.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            MassStatus.Text = "Error — see log.";
            AppendMassLog($"ERROR: {ex.Message}");
        }
        finally
        {
            CreateMassButton.IsEnabled = true;
            _cts = null;
        }
    }

    private void AppendMassLog(string text)
    {
        Dispatcher.Invoke(() =>
        {
            MassLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            MassLog.ScrollToEnd();
        });
    }

    private void ActivityTab_Click(object sender, RoutedEventArgs e) { ShowActivity(); _ = RefreshAsync(); }
    private void MassCreateTab_Click(object sender, RoutedEventArgs e) => ShowMassCreate();
    private void AuditTab_Click(object sender, RoutedEventArgs e) { ShowAudit(); _ = RefreshAsync(); }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("accounts");
    }
}
