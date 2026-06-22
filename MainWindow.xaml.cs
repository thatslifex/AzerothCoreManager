using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using AzerothCoreManager.Windows;

namespace AzerothCoreManager;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Check if first run
        if (!App.Settings.Load().FirstRunComplete)
        {
            var wizard = new Dialogs.FirstRunWizard();
            wizard.Owner = this;
            wizard.ShowDialog();

            var settings = App.Settings.Load();
            settings.FirstRunComplete = true;
            App.Settings.Save(settings);
        }

        // Apply theme
        App.Theme.ApplyTheme(App.Settings.Load().Theme);

        // Localize navigation
        LocalizeNavigation();

        // Localize status bar
        LocalizeStatusBar();

        // Dynamic version
        VersionText.Text = $"v{App.Version}";

        // Dynamic background
        ApplyBackground();
    }

    private void LocalizeNavigation()
    {
        var loc = App.Loc;
        NavDashboard.Content = loc.T("nav.dashboard");
        NavSetup.Content = loc.T("nav.setup");
        NavServerControl.Content = loc.T("nav.server_control");
        NavGmConsole.Content = loc.T("nav.gm_console");
        NavModuleManager.Content = loc.T("nav.module_manager");
        NavDatabase.Content = loc.T("nav.database");
        NavAccounts.Content = loc.T("nav.accounts");
        NavSettings.Content = loc.T("nav.settings");
        NavConfigEditor.Content = loc.T("tool.config_editor");
        NavBackupManager.Content = loc.T("tool.backup_manager");
        NavLogViewer.Content = loc.T("tool.log_viewer");
    }

    private void LocalizeStatusBar()
    {
        var loc = App.Loc;
        AuthStatusText.Text = loc.T("status.auth_offline");
        WorldStatusText.Text = loc.T("status.world_offline");
        MySqlStatusText.Text = loc.T("status.mysql_offline");
        PlayerCountText.Text = loc.T("status.players", 0);
    }

    private void ApplyBackground()
    {
        var bg = App.Settings.Load().BackgroundImage;
        if (!string.IsNullOrEmpty(bg))
        {
            var uri = new Uri($"pack://application:,,,/Resources/Assets/Backgrounds/{bg}");
            BackgroundImage.Source = new BitmapImage(uri);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeButton();
    }

    private void UpdateMaximizeButton()
    {
        if (WindowState == WindowState.Maximized)
        {
            var template = MaximizeButton.Template;
            if (template?.FindName("MaxImage", MaximizeButton) is System.Windows.Controls.Image img)
            {
                img.Source = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/Assets/Chrome/btn_win_restore_normal.png"));
            }
            MaximizeButton.ToolTip = "Restore";
        }
        else
        {
            var template = MaximizeButton.Template;
            if (template?.FindName("MaxImage", MaximizeButton) is System.Windows.Controls.Image img)
            {
                img.Source = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/Assets/Chrome/btn_win_maximize_normal.png"));
            }
            MaximizeButton.ToolTip = "Maximize";
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            MaximizeRestoreToggle();
        else
            DragMove();
    }

    private void MaximizeRestoreToggle()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => MaximizeRestoreToggle();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CogButton_Click(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(Pages.SettingsPage));
    }

    private void OpenConfigEditor_Click(object sender, RoutedEventArgs e)
    {
        new ConfigEditorWindow { Owner = this }.Show();
    }

    private void OpenBackupManager_Click(object sender, RoutedEventArgs e)
    {
        new BackupManagerWindow { Owner = this }.Show();
    }

    private void OpenLogViewer_Click(object sender, RoutedEventArgs e)
    {
        new LogViewerWindow { Owner = this }.Show();
    }
}
