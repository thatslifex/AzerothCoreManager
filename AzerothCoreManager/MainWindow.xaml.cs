using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace AzerothCoreManager
{
    /// <summary>
    /// Main window for the application. Hosts controls to start/stop auth and world server processes
    /// and displays their textual output in two separate console text boxes.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Controller that manages starting, stopping and monitoring the auth and world server processes.
        /// Stored as a readonly field so the same controller instance (and its ProcessManager instances)
        /// is used for the lifetime of the window.
        /// </summary>
        private readonly ServerController _controller = new();

        // DispatcherTimer used to poll for the SQL Windows Service state (searching by approximate name)
        private readonly DispatcherTimer _sqlServiceTimer = new();
        // Approximate search term used to find the SQL service (case-insensitive substring match)
        private const string SqlServiceSearchTerm = "mysql";
        // Cache the discovered service name (ServiceName) to avoid searching every tick when stable
        private string? _cachedSqlServiceName;

        /// <summary>
        /// Initialize the WPF window, wire up the Closing handler used to ensure servers are stopped
        /// when the UI closes.
        /// Also checks currently running processes and updates UI (borders / buttons) accordingly.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Auth/World Buttons immer initial deaktivieren
            AuthStartButton.IsEnabled = false;
            AuthStopButton.IsEnabled = false;
            WorldStartButton.IsEnabled = false;
            WorldStopButton.IsEnabled = false;

            // SQL Timer starten (unabhängig vom Pfad)
            _sqlServiceTimer.Interval = TimeSpan.FromSeconds(3);
            _sqlServiceTimer.Tick += (s, e) => UpdateSqlServiceState();
            _sqlServiceTimer.Start();

            if (_controller.HasValidPath)
            {
                // Pfad ist gesetzt: Buttons entsprechend laufender Prozesse aktivieren
                UpdateServerButtons();

                // Subscribe zu RunningChanged-Events
                _controller.AuthServer.RunningChanged += OnAuthRunningChanged;
                _controller.WorldServer.RunningChanged += OnWorldRunningChanged;
            }
            else
            {
                // Kein Pfad gesetzt: Buttons bleiben deaktiviert
                MessageBox.Show(
                    "Es wurde kein gültiger Serverpfad gefunden.\n" +
                    "Bitte klicken Sie auf 'Settings', um den Ordner auszuwählen.",
                    "Pfad erforderlich",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
        // AUTH

        /// <summary>
        /// Called when the user clicks the "Start Auth" button.
        /// Resolves and starts the authserver process via the ServerController and
        /// updates the UI buttons to reflect the running state.
        /// </summary>
        private void AuthStartButton_Click(object sender, RoutedEventArgs e)
        {
            // Start auth server and provide a logging callback that appends lines to the auth console.
            _controller.StartAuth(AppendAuth);

            // Disable the start button and enable the stop button to prevent duplicate starts.
            AuthStartButton.IsEnabled = false;
            AuthStopButton.IsEnabled = true;

            // Update visual state optimistically.
            OnAuthRunningChanged(true);
        }

        /// <summary>
        /// Called when the user clicks the "Stop Auth" button.
        /// Asynchronously stops the authserver process and updates button enabled states afterwards.
        /// </summary>
        private async void AuthStopButton_Click(object sender, RoutedEventArgs e)
        {
            // Await the controller's stop to ensure the process is requested to stop before toggling UI.
            await _controller.StopAuth(AppendAuth);

            // Restore UI state so the user can start the auth server again.
            AuthStartButton.IsEnabled = true;
            AuthStopButton.IsEnabled = false;

            // Update visual state.
            OnAuthRunningChanged(false);
        }

        // WORLD

        /// <summary>
        /// Called when the user clicks the "Start World" button.
        /// Starts the worldserver process via the ServerController and updates UI button states.
        /// </summary>
        private void WorldStartButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.StartWorld(AppendWorld);
            WorldStartButton.IsEnabled = false;
            WorldStopButton.IsEnabled = true;

            // Update visual state optimistically.
            OnWorldRunningChanged(true);
        }

        /// <summary>
        /// Called when the user clicks the "Stop World" button.
        /// Asynchronously stops the worldserver process and restores button states afterwards.
        /// </summary>
        private async void WorldStopButton_Click(object sender, RoutedEventArgs e)
        {
            await _controller.StopWorld(AppendWorld);
            WorldStartButton.IsEnabled = true;
            WorldStopButton.IsEnabled = false;

            // Update visual state.
            OnWorldRunningChanged(false);
        }

        /// <summary>
        /// Called when the user clicks the "Start World" button.
        /// Starts the SQLserver process via the ServerController and updates UI button states.
        /// </summary>
        private void SQLStartButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.StartSQL();
            SQLStartButton.IsEnabled = false;
            SQLStopButton.IsEnabled = true;

            // Update visual state optimistically.
            OnSqlRunningChanged(true);
        }

        /// <summary>
        /// Handles the Click event for the SQL Stop button, stopping the SQL process and updating the UI to reflect the
        /// stopped state.
        /// </summary>
        /// <remarks>After stopping the SQL process, this method enables the World Start button and
        /// disables the World Stop button to reflect the current application state.</remarks>
        /// <param name="sender">The source of the event, typically the SQL Stop button control.</param>
        /// <param name="e">An object that contains the event data associated with the Click event.</param>
        private async void SQLStopButton_Click(object sender, RoutedEventArgs e)
        {
            await _controller.StopSQL();
            SQLStartButton.IsEnabled = true;
            SQLStopButton.IsEnabled = false;

            // Update visual state.
            OnSqlRunningChanged(false);
        }

        private void SQLRestartButton_Click(object sender, RoutedEventArgs e) => _controller.RestartSQL();

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Ordner auswählen, der authserver.exe und worldserver.exe enthält"
            };

            var result = dlg.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string path = dlg.SelectedPath;
                _controller.SetBasePath(path);

                MessageBox.Show($"Pfad gespeichert:\n{path}");

                // Jetzt Buttons entsprechend dem Zustand der Server aktivieren
                UpdateServerButtons();

                // RunningChanged-Events registrieren, falls noch nicht geschehen
                _controller.AuthServer.RunningChanged += OnAuthRunningChanged;
                _controller.WorldServer.RunningChanged += OnWorldRunningChanged;
            }
        }

        /// <summary>
        /// Append a single line of text to the auth console text box.
        /// This method marshals the update to the UI thread using the window's Dispatcher.
        /// </summary>
        /// <param name="msg">The text line to append; a newline is added automatically.</param>
        private void AppendAuth(string msg)
        {
            // BeginInvoke is used to ensure UI updates happen on the UI thread even when the call
            // originates from background process output events.
            Dispatcher.BeginInvoke(() =>
            {
                AuthConsoleTextBox.AppendText(msg + Environment.NewLine);
                AuthConsoleTextBox.ScrollToEnd();
            });
        }

        /// <summary>
        /// Append a single line of text to the world console text box.
        /// Marshals to the UI thread to avoid cross-thread exceptions.
        /// </summary>
        /// <param name="msg">The text line to append; a newline is added automatically.</param>
        private void AppendWorld(string msg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                WorldConsoleTextBox.AppendText(msg + Environment.NewLine);
                WorldConsoleTextBox.ScrollToEnd();
            });
        }

        /// <summary>
        /// Update Auth border color when running state changes.
        /// </summary>
        private void OnAuthRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    AuthBorder.BorderBrush = running
                        ? (Brush)FindResource("OnlineBrush")
                        : (Brush)FindResource("OfflineBrush");
                }
                catch
                {
                    // If resources are missing, fall back to simple colors.
                    AuthBorder.BorderBrush = running ? Brushes.Green : Brushes.Red;
                }
            });
        }

        /// <summary>
        /// Update World border color when running state changes.
        /// </summary>
        private void OnWorldRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    WorldBorder.BorderBrush = running
                        ? (Brush)FindResource("OnlineBrush")
                        : (Brush)FindResource("OfflineBrush");
                }
                catch
                {
                    WorldBorder.BorderBrush = running ? Brushes.Green : Brushes.Red;
                }
            });
        }

        /// <summary>
        /// Update SQL border color when running state changes.
        /// </summary>
        private void OnSqlRunningChanged(bool running)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    SQLBorder.BorderBrush = running
                        ? (Brush)FindResource("OnlineBrush")
                        : (Brush)FindResource("OfflineBrush");
                }
                catch
                {
                    SQLBorder.BorderBrush = running ? Brushes.Green : Brushes.Red;
                }
            });
        }

        /// <summary>
        /// Window closing handler that ensures both auth and world servers are stopped before the application exits.
        /// The handler awaits the asynchronous stop calls so the shutdown sequence attempts an orderly cleanup.
        /// </summary>
        /// <param name="sender">Event source (the window).</param>
        /// <param name="e">CancelEventArgs provided by WPF; not used to cancel closure here.</param>
        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Stop auth and world servers if they are running. Await ensures we give them time to stop.
            await _controller.StopAuth(AppendAuth);
            await _controller.StopWorld(AppendWorld);
        }

        // --- Helper methods for initial process detection ---

        /// <summary>
        /// Polls the Windows Service state for the SQL server (searching by approximate name) and updates the UI.
        /// Called periodically by the DispatcherTimer and once during initialization.
        /// </summary>
        private void UpdateSqlServiceState()
        {
            try
            {
                // If we don't yet have a cached service name, try to find one.
                if (string.IsNullOrEmpty(_cachedSqlServiceName))
                {
                    _cachedSqlServiceName = ServerController.FindSqlServiceName(SqlServiceSearchTerm);
                }

                bool running = false;

                if (!string.IsNullOrEmpty(_cachedSqlServiceName))
                {
                    try
                    {
                        using var svc = new ServiceController(_cachedSqlServiceName);
                        running = svc.Status == ServiceControllerStatus.Running;
                    }
                    catch
                    {
                        // If the cached service name stopped existing or cannot be queried, clear cache and try a fresh lookup next tick.
                        _cachedSqlServiceName = null;
                    }
                }

                OnSqlRunningChanged(running);
                try
                {
                    SQLStartButton.IsEnabled = !running;
                    SQLStopButton.IsEnabled = running;
                }
                catch { }
            }
            catch
            {
                // Swallow any unexpected exceptions to keep timer running.
            }
        }
        private void UpdateServerButtons()
        {
            // Authserver prüfen
            bool authRunning = ServerIsRunning("authserver.exe");
            AuthStartButton.IsEnabled = !authRunning;
            AuthStopButton.IsEnabled = authRunning;

            // Worldserver prüfen
            bool worldRunning = ServerIsRunning("worldserver.exe");
            WorldStartButton.IsEnabled = !worldRunning;
            WorldStopButton.IsEnabled = worldRunning;
        }

        // Prüft, ob ein Prozess mit dem angegebenen Exe-Namen läuft
        private bool ServerIsRunning(string exeName)
        {
            var nameNoExt = System.IO.Path.GetFileNameWithoutExtension(exeName);
            foreach (var p in System.Diagnostics.Process.GetProcesses())
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
