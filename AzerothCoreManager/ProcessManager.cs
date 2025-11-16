using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AzerothCoreManager
{
    /// <summary>
    /// Manages a single system process: starting it, routing its textual output to subscribers,
    /// observing its exit, and stopping/disposing it when requested.
    /// This class intentionally keeps a very small surface area to encapsulate Process lifecycle concerns.
    /// </summary>
    public class ProcessManager
    {
        /// <summary>
        /// Event raised for each line of text produced by the process (standard output and standard error).
        /// Subscribers receive the raw text line; error lines are prefixed by the manager when raised.
        /// </summary>
        public event Action<string>? OutputReceived;

        /// <summary>
        /// Event invoked when the running state changes. Parameter is true when process starts, false when it stops/exits.
        /// </summary>
        public event Action<bool>? RunningChanged;

        /// <summary>
        /// Backing field that holds the currently managed <see cref="Process"/> instance.
        /// Null when no process is active or after it has been stopped/disposed.
        /// </summary>
        private Process? _process;

        /// <summary>
        /// Indicates whether a managed process is currently running and has not exited.
        /// </summary>
        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>
        /// Start a new process if none is currently running.
        /// The method configures the process to redirect stdout/stderr, attaches handlers to forward lines
        /// to <see cref="OutputReceived"/>, and begins asynchronous line reading.
        /// If a process is already running the call is ignored.
        /// </summary>
        /// <param name="exePath">Full path to the executable to start.</param>
        /// <param name="workingDir">Working directory to use for the process.</param>
        public void Start(string exePath, string workingDir)
        {
            // If a process is already running, do nothing to avoid starting multiple instances.
            if (IsRunning)
                return;

            // Configure ProcessStartInfo to run the executable without a shell and with redirected output.
            var psi = new ProcessStartInfo(exePath)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false,        // required for redirecting output
                RedirectStandardOutput = true,  // capture stdout lines
                RedirectStandardError = true,   // capture stderr lines
                CreateNoWindow = true,          // do not create a console window
            };

            // Create the Process instance and enable event raising for Exited.
            var proc = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            // Forward standard output lines to subscribers.
            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OutputReceived?.Invoke(e.Data);
            };

            // Forward standard error lines to subscribers, adding a simple marker so callers can distinguish them.
            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OutputReceived?.Invoke("[ERR] " + e.Data);
            };

            // Notify subscribers when the process exits and include the exit code for diagnostics.
            proc.Exited += (s, e) =>
            {
                try
                {
                    OutputReceived?.Invoke($"-- Process exited (Code: {proc.ExitCode}) --");
                }
                catch { }
                // Inform observers that the running state changed to false.
                RunningChanged?.Invoke(false);
            };

            // Start the process and begin asynchronous, line-oriented reading for both stdout and stderr.
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Store the running process so other methods can interact with it (e.g. StopAsync).
            _process = proc;

            // Notify observers that the process is running now.
            RunningChanged?.Invoke(true);
        }

        /// <summary>
        /// Asynchronously stops and disposes the managed process if one is active.
        /// The method attempts to cancel reading, kill the process if still running, waits briefly
        /// for exit, and then disposes the <see cref="Process"/> instance.
        /// The method swallows exceptions during shutdown to favor best-effort cleanup.
        /// </summary>
        /// <returns>A task that completes once the stop/dispose sequence has finished.</returns>
        public async Task StopAsync()
        {
            // If there's no process, nothing to do.
            if (_process == null)
                return;

            // Capture the process reference and clear the field to mark this manager as no longer owning a running process.
            var proc = _process;
            _process = null;

            try
            {
                // Attempt to detach event handlers.
                // NOTE: The original implementation uses '-= null' which is effectively a no-op.
                // Because anonymous delegates were attached in Start, removing them requires keeping references.
                // This call is kept to reflect the original behavior; actual handler removal would need stored delegates.
                proc.OutputDataReceived -= null;
                proc.ErrorDataReceived -= null;

                // Try to cancel the asynchronous read loops; ignore failures.
                try { proc.CancelOutputRead(); } catch { }
                try { proc.CancelErrorRead(); } catch { }

                // If the process hasn't exited, try to kill it. Ignore exceptions (process may already be terminating).
                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }

                // Wait up to a short timeout for the process to exit. Run in a background task to avoid blocking caller threads.
                await Task.Run(() =>
                {
                    try { proc.WaitForExit(5000); } catch { }
                });
            }
            finally
            {
                // Ensure the Process object is disposed to free native handles. Swallow disposal exceptions.
                try { proc.Dispose(); } catch { }

                // Inform observers that the process is no longer running.
                RunningChanged?.Invoke(false);
            }
        }

    }
}
