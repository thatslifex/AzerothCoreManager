using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Dialogs;

public partial class UpdateDialog : FluentWindow
{
    private readonly UpdateService _update = new();
    private CancellationTokenSource? _cts;
    private string? _downloadedInstaller;

    public UpdateDialog(string currentVersion, string newVersion, string downloadUrl, long size)
    {
        Title = "Update Available";
        Width = 450;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

        var titleBlock = new System.Windows.Controls.TextBlock
        {
            Text = "AzerothCore Manager Update",
            FontSize = 18, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 12)
        };
        System.Windows.Controls.Grid.SetRow(titleBlock, 0);

        var infoBlock = new System.Windows.Controls.TextBlock
        {
            Text = $"Current version: v{currentVersion}\nNew version: v{newVersion} ({size / 1024 / 1024} MB)",
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            FontSize = 13, Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = System.Windows.TextWrapping.Wrap
        };
        System.Windows.Controls.Grid.SetRow(infoBlock, 1);

        var progressBar = new System.Windows.Controls.ProgressBar
        {
            Height = 8, Minimum = 0, Maximum = 100, Value = 0,
            Margin = new Thickness(0, 0, 0, 8)
        };
        System.Windows.Controls.Grid.SetRow(progressBar, 2);

        var statusBlock = new System.Windows.Controls.TextBlock
        {
            Text = "Ready to download.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 12)
        };
        System.Windows.Controls.Grid.SetRow(statusBlock, 3);

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var downloadBtn = new System.Windows.Controls.Button
        {
            Content = "Download & Install",
            Width = 150, Height = 32, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x5A, 0x2D)),
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold
        };

        var skipBtn = new System.Windows.Controls.Button
        {
            Content = "Skip",
            Width = 80, Height = 32, IsCancel = true
        };

        btnPanel.Children.Add(downloadBtn);
        btnPanel.Children.Add(skipBtn);
        System.Windows.Controls.Grid.SetRow(btnPanel, 4);

        var hintBlock = new System.Windows.Controls.TextBlock
        {
            Text = "The installer will close this app automatically.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            FontSize = 11, Margin = new Thickness(0, 8, 0, 0)
        };
        System.Windows.Controls.Grid.SetRow(hintBlock, 5);

        grid.Children.Add(titleBlock);
        grid.Children.Add(infoBlock);
        grid.Children.Add(progressBar);
        grid.Children.Add(statusBlock);
        grid.Children.Add(btnPanel);
        grid.Children.Add(hintBlock);
        Content = grid;

        downloadBtn.Click += async (s, e) =>
        {
            downloadBtn.IsEnabled = false;
            skipBtn.IsEnabled = false;
            statusBlock.Text = "Downloading...";
            _cts = new CancellationTokenSource();

            var progress = new Progress<long>(bytes =>
            {
                var pct = size > 0 ? (double)bytes / size * 100 : 0;
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = Math.Min(pct, 100);
                    statusBlock.Text = $"Downloading... {bytes / 1024 / 1024:F1} / {size / 1024 / 1024} MB";
                });
            });

            try
            {
                _downloadedInstaller = await _update.DownloadInstallerAsync(downloadUrl, progress, _cts.Token);
                statusBlock.Text = "Download complete. Launching installer...";
                progressBar.Value = 100;

                await Task.Delay(500);

                _update.RunInstaller(_downloadedInstaller);
                Application.Current.Shutdown();
            }
            catch (OperationCanceledException)
            {
                statusBlock.Text = "Download cancelled.";
                downloadBtn.IsEnabled = true;
                skipBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                statusBlock.Text = $"Download failed: {ex.Message}";
                downloadBtn.IsEnabled = true;
                skipBtn.IsEnabled = true;
            }
            finally { _cts = null; }
        };

        skipBtn.Click += (s, e) => Close();
    }
}
