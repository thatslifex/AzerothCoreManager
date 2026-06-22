using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class DatabasePage : Page
{
    private readonly DatabaseService _db = new();
    private CancellationTokenSource? _cts;

    public DatabasePage()
    {
        InitializeComponent();
        ShowAccounts();
        _ = RefreshAsync();
    }

    private void ShowAccounts()
    {
        SetActiveTab(AccountsTab);
        AccountsScroller.Visibility = Visibility.Visible;
        CharactersScroller.Visibility = Visibility.Collapsed;
        BansScroller.Visibility = Visibility.Collapsed;
        SqlView.Visibility = Visibility.Collapsed;
        CreateAccountButton.Visibility = Visibility.Visible;
        BanAccountButton.Visibility = Visibility.Collapsed;
    }

    private void ShowCharacters()
    {
        SetActiveTab(CharactersTab);
        AccountsScroller.Visibility = Visibility.Collapsed;
        CharactersScroller.Visibility = Visibility.Visible;
        BansScroller.Visibility = Visibility.Collapsed;
        SqlView.Visibility = Visibility.Collapsed;
        CreateAccountButton.Visibility = Visibility.Collapsed;
        BanAccountButton.Visibility = Visibility.Collapsed;
    }

    private void ShowBans()
    {
        SetActiveTab(BansTab);
        AccountsScroller.Visibility = Visibility.Collapsed;
        CharactersScroller.Visibility = Visibility.Collapsed;
        BansScroller.Visibility = Visibility.Visible;
        SqlView.Visibility = Visibility.Collapsed;
        CreateAccountButton.Visibility = Visibility.Collapsed;
        BanAccountButton.Visibility = Visibility.Visible;
    }

    private void ShowSql()
    {
        SetActiveTab(SqlTab);
        AccountsScroller.Visibility = Visibility.Collapsed;
        CharactersScroller.Visibility = Visibility.Collapsed;
        BansScroller.Visibility = Visibility.Collapsed;
        SqlView.Visibility = Visibility.Visible;
        CreateAccountButton.Visibility = Visibility.Collapsed;
        BanAccountButton.Visibility = Visibility.Collapsed;
    }

    private void SetActiveTab(Button active)
    {
        var activeColor = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x5A));
        foreach (var btn in new[] { AccountsTab, CharactersTab, BansTab, SqlTab })
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
            if (AccountsScroller.Visibility == Visibility.Visible)
            {
                var accounts = await _db.GetAccountsAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore", _cts.Token);
                AccountsGrid.ItemsSource = accounts;
            }
            else if (CharactersScroller.Visibility == Visibility.Visible)
            {
                var chars = await _db.GetCharactersAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore", _cts.Token);
                CharactersGrid.ItemsSource = chars;
            }
            else if (BansScroller.Visibility == Visibility.Visible)
            {
                var bans = await _db.GetAccountBansAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore", _cts.Token);
                BansGrid.ItemsSource = bans;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _cts = null; }
    }

    private async void CreateAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CreateAccountDialog();
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() != true) return;

        var settings = App.Settings.Load();
        try
        {
            await _db.CreateAccountAsync(
                settings.MySqlHost, settings.MySqlPort, "acore", "acore",
                dialog.Username, dialog.Password, dialog.GmLevel);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to create account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BanAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (BansGrid.SelectedItem is not DatabaseService.BanInfo ban) return;

        var result = System.Windows.MessageBox.Show(
            $"Unban account '{ban.Username}' (ID: {ban.AccountId})?",
            "Unban Account", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var settings = App.Settings.Load();
            try
            {
                await _db.UnbanAccountAsync(
                    settings.MySqlHost, settings.MySqlPort, "acore", "acore", ban.AccountId);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to unban: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void RunSqlButton_Click(object sender, RoutedEventArgs e)
    {
        var sql = SqlInput.Text.Trim();
        if (string.IsNullOrEmpty(sql)) return;

        var database = ((ComboBoxItem)DatabaseCombo.SelectedItem).Content.ToString()!;
        var settings = App.Settings.Load();

        SqlStatus.Text = "Running...";
        _cts = new CancellationTokenSource();

        try
        {
            var (success, columns, rows, error) = await _db.ExecuteQueryAsync(
                settings.MySqlHost, settings.MySqlPort, "acore", "acore", database, sql, _cts.Token);

            if (success)
            {
                var dt = new DataTable();
                foreach (var col in columns) dt.Columns.Add(col);
                foreach (var row in rows) dt.Rows.Add(row.ToArray());
                SqlResultGrid.ItemsSource = dt.DefaultView;
                SqlStatus.Text = $"{rows.Count} row(s) returned.";
            }
            else
            {
                SqlStatus.Text = $"Error: {error}";
                SqlResultGrid.ItemsSource = null;
            }
        }
        catch (Exception ex)
        {
            SqlStatus.Text = $"Error: {ex.Message}";
        }
        finally { _cts = null; }
    }

    private void ClearSqlButton_Click(object sender, RoutedEventArgs e)
    {
        SqlInput.Clear();
        SqlResultGrid.ItemsSource = null;
        SqlStatus.Text = "";
    }

    private void AccountsTab_Click(object sender, RoutedEventArgs e) { ShowAccounts(); _ = RefreshAsync(); }
    private void CharactersTab_Click(object sender, RoutedEventArgs e) { ShowCharacters(); _ = RefreshAsync(); }
    private void BansTab_Click(object sender, RoutedEventArgs e) { ShowBans(); _ = RefreshAsync(); }
    private void SqlTab_Click(object sender, RoutedEventArgs e) => ShowSql();

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("database");
    }
}

/// <summary>
/// Simple dialog for creating a new account.
/// </summary>
public class CreateAccountDialog : Window
{
    public string Username { get; private set; } = "";
    public string Password { get; private set; } = "";
    public int GmLevel { get; private set; } = 0;

    public CreateAccountDialog()
    {
        Title = "Create Account";
        Width = 350;
        Height = 250;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
        Foreground = Brushes.White;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var userLabel = new TextBlock { Text = "Username:", Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(userLabel, 0);
        var userBox = new TextBox { Name = "UserBox", Margin = new Thickness(0, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x1A)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x55)) };
        Grid.SetRow(userBox, 1);

        var passLabel = new TextBlock { Text = "Password:", Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(passLabel, 2);
        var passBox = new PasswordBox { Name = "PassBox", Margin = new Thickness(0, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x1A)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x55)) };
        Grid.SetRow(passBox, 3);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var createBtn = new Button { Content = "Create", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x5A, 0x2D)),
            Foreground = Brushes.White };
        var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 28, IsCancel = true };
        btnPanel.Children.Add(createBtn);
        btnPanel.Children.Add(cancelBtn);
        Grid.SetRow(btnPanel, 4);

        grid.Children.Add(userLabel);
        grid.Children.Add(userBox);
        grid.Children.Add(passLabel);
        grid.Children.Add(passBox);
        grid.Children.Add(btnPanel);
        Content = grid;

        createBtn.Click += (s, e) =>
        {
            Username = userBox.Text.Trim();
            Password = passBox.Password;
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                System.Windows.MessageBox.Show("Username and password are required.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        };
    }
}
