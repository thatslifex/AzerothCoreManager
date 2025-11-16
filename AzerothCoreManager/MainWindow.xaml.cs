using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace AzerothCoreManager
{
    public partial class MainWindow : Window
    {
        private readonly ServerController _controller = new();
        private readonly DispatcherTimer _sqlServiceTimer = new();
        private readonly DispatcherTimer _logTimer = new(); // Timer für Log-Dateien
        private const string SqlServiceSearchTerm = "mysql";
        private string? _cachedSqlServiceName;

        private long _authLastPos = 0;
        private long _worldLastPos = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Buttons initial deaktivieren
            AuthStartButton.IsEnabled = false;
            AuthStopButton.IsEnabled = false;
            WorldStartButton.IsEnabled = false;
            WorldStopButton.IsEnabled = false;

            // Rahmenfarben initial setzen
            bool authRunning = ServerIsRunning("authserver.exe");
            bool worldRunning = ServerIsRunning("worldserver.exe");
            OnAuthRunningChanged(authRunning);
            OnWorldRunningChanged(worldRunning);
            UpdateSqlServiceState();

            if (_controller.HasValidPath)
            {
                UpdateServerButtons();
            }
            else
            {
                MessageBox.Show(
                    "No valid server path found.\nPlease click 'Settings' to select the folder.",
                    "Path required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }

            // SQL Timer starten
            _sqlServiceTimer.Interval = TimeSpan.FromSeconds(3);
            _sqlServiceTimer.Tick += (s, e) => UpdateSqlServiceState();
            _sqlServiceTimer.Start();

            // LogTimer initial nur erstellt, aber noch nicht gestartet
            _logTimer.Interval = TimeSpan.FromSeconds(1);
            _logTimer.Tick += LogTimer_Tick;

            // Prüfen, ob Prozesse schon laufen beim Start -> LogTimer starten
            if (authRunning || worldRunning)
            {
                ResetLogPositions();
                _logTimer.Start();
            }
        }

        // --- Auth / World Buttons ---
        private void AuthStartButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.StartAuth();
            AuthStartButton.IsEnabled = false;
            AuthStopButton.IsEnabled = true;
            OnAuthRunningChanged(true);

            // LogTimer starten, falls noch nicht
            ResetLogPositions();
            if (!_logTimer.IsEnabled) _logTimer.Start();
        }

        private async void AuthStopButton_Click(object sender, RoutedEventArgs e)
        {
            await _controller.StopAuth();
            AuthStartButton.IsEnabled = true;
            AuthStopButton.IsEnabled = false;
            OnAuthRunningChanged(false);
        }

        private void WorldStartButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.StartWorld();
            WorldStartButton.IsEnabled = false;
            WorldStopButton.IsEnabled = true;
            OnWorldRunningChanged(true);

            // LogTimer starten, falls noch nicht
            ResetLogPositions();
            if (!_logTimer.IsEnabled) _logTimer.Start();
        }

        private async void WorldStopButton_Click(object sender, RoutedEventArgs e)
        {
            await _controller.StopWorld();
            WorldStartButton.IsEnabled = true;
            WorldStopButton.IsEnabled = false;
            OnWorldRunningChanged(false);
        }

        // --- SQL Buttons ---
        private void SQLStartButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.StartSQL();
            SQLStartButton.IsEnabled = false;
            SQLStopButton.IsEnabled = true;
            OnSqlRunningChanged(true);
        }

        private async void SQLStopButton_Click(object sender, RoutedEventArgs e)
        {
            await _controller.StopSQL();
            SQLStartButton.IsEnabled = true;
            SQLStopButton.IsEnabled = false;
            OnSqlRunningChanged(false);
        }

        private void SQLRestartButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.RestartSQL();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the folder containing authserver.exe and worldserver.exe"
            };

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string path = dlg.SelectedPath;
                _controller.SetBasePath(path);
                MessageBox.Show($"Path saved:\n{path}");
                UpdateServerButtons();
            }
        }

        // --- LogTimer ---
        private string AuthLogPath => Path.Combine(_controller.BasePath, "Auth.log");
        private string WorldLogPath => Path.Combine(_controller.BasePath, "Server.log");

        private void LogTimer_Tick(object? sender, EventArgs e)
        {
            // Auth.log auslesen
            if (File.Exists(AuthLogPath))
            {
                try
                {
                    using var fs = new FileStream(AuthLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(_authLastPos, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs);
                    string newText = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(newText))
                    {
                        AuthConsoleTextBox.AppendText(newText);
                        AuthConsoleTextBox.ScrollToEnd();
                    }
                    _authLastPos = fs.Position;
                }
                catch { }
            }

            // Server.log auslesen
            if (File.Exists(WorldLogPath))
            {
                try
                {
                    using var fs = new FileStream(WorldLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(_worldLastPos, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs);
                    string newText = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(newText))
                    {
                        WorldConsoleTextBox.AppendText(newText);
                        WorldConsoleTextBox.ScrollToEnd();
                    }
                    _worldLastPos = fs.Position;
                }
                catch { }
            }
        }

        private void ResetLogPositions()
        {
            _authLastPos = 0;
            _worldLastPos = 0;
        }

        // --- Border Updates ---
        private void OnAuthRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try { AuthBorder.BorderBrush = running ? (Brush)FindResource("OnlineBrush") : (Brush)FindResource("OfflineBrush"); }
                catch { AuthBorder.BorderBrush = running ? Brushes.Green : Brushes.Red; }
            });
        }

        private void OnWorldRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try { WorldBorder.BorderBrush = running ? (Brush)FindResource("OnlineBrush") : (Brush)FindResource("OfflineBrush"); }
                catch { WorldBorder.BorderBrush = running ? Brushes.Green : Brushes.Red; }
            });
        }

        private void OnSqlRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try { SQLBorder.BorderBrush = running ? (Brush)FindResource("OnlineBrush") : (Brush)FindResource("OfflineBrush"); }
                catch { SQLBorder.BorderBrush = running ? Brushes.Green : Brushes.Red; }
            });
        }

        // --- Helpers ---
        private void UpdateSqlServiceState()
        {
            try
            {
                if (string.IsNullOrEmpty(_cachedSqlServiceName))
                    _cachedSqlServiceName = ServerController.FindSqlServiceName(SqlServiceSearchTerm);

                bool running = false;

                if (!string.IsNullOrEmpty(_cachedSqlServiceName))
                {
                    try
                    {
                        using var svc = new ServiceController(_cachedSqlServiceName);
                        running = svc.Status == ServiceControllerStatus.Running;
                    }
                    catch { _cachedSqlServiceName = null; }
                }

                OnSqlRunningChanged(running);
                try
                {
                    SQLStartButton.IsEnabled = !running;
                    SQLStopButton.IsEnabled = running;
                }
                catch { }
            }
            catch { }
        }

        private void UpdateServerButtons()
        {
            bool authRunning = ServerIsRunning("authserver.exe");
            AuthStartButton.IsEnabled = !authRunning;
            AuthStopButton.IsEnabled = authRunning;

            bool worldRunning = ServerIsRunning("worldserver.exe");
            WorldStartButton.IsEnabled = !worldRunning;
            WorldStopButton.IsEnabled = worldRunning;
        }

        private bool ServerIsRunning(string exeName)
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(exeName);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.Equals(nameNoExt, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }
    }
}
