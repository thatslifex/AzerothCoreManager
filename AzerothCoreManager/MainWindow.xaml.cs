using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

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

        /// <summary>
        /// Initialize the WPF window, wire up the Closing handler used to ensure servers are stopped
        /// when the UI closes.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Subscribe to the window closing event so we can stop running processes cleanly.
            Closing += MainWindow_Closing;

            // Subscribe to running-state changes from the process managers so UI borders update automatically.
            _controller.AuthServer.RunningChanged += OnAuthRunningChanged;
            _controller.WorldServer.RunningChanged += OnWorldRunningChanged;
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
    }
}
