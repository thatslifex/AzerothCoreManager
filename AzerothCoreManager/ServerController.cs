using System;
using System.IO;
using System.Threading.Tasks;

namespace AzerothCoreManager
{
    /// <summary>
    /// Controls the lifecycle of the server processes used by the application.
    /// Provides start/stop operations for the authentication server and the world server,
    /// and exposes the underlying <see cref="ProcessManager"/> instances so callers can
    /// inspect state or subscribe to additional events.
    /// </summary>
    public class ServerController
    {
        /// <summary>
        /// Process manager responsible for starting, monitoring and stopping the authentication server process.
        /// Subscribe to <c>AuthServer.OutputReceived</c> to receive text output from the authserver process.
        /// </summary>
        public ProcessManager AuthServer { get; } = new();

        /// <summary>
        /// Process manager responsible for starting, monitoring and stopping the world server process.
        /// Subscribe to <c>WorldServer.OutputReceived</c> to receive text output from the worldserver process.
        /// </summary>
        public ProcessManager WorldServer { get; } = new();

        /// <summary>
        /// Base folder where the server executables are expected to be located.
        /// This value is combined with executable file names using <see cref="GetPath(string)"/>.
        /// </summary>
        private readonly string _basePath = "C:\\Northrend Legacy\\";

        /// <summary>
        /// Combine the configured base path with an executable file name and return the full path.
        /// </summary>
        /// <param name="exeName">The executable file name (for example: "authserver.exe").</param>
        /// <returns>The full filesystem path to the executable.</returns>
        private string GetPath(string exeName)
        {
            // Use Path.Combine to produce a platform-correct path string from base path and file name.
            return Path.Combine(_basePath, exeName);
        }

        /// <summary>
        /// Start the authentication server process if the executable is present.
        /// The method will:
        ///  - determine the full path to "authserver.exe",
        ///  - verify the file exists,
        ///  - subscribe the provided logger to the process output events,
        ///  - start the process via the <see cref="ProcessManager"/>, and
        ///  - log a confirmation message.
        /// If the executable is not found, a descriptive message is logged and no process is started.
        /// </summary>
        /// <param name="log">Action to receive textual log messages (e.g. to append to a UI log control).</param>
        public void StartAuth(Action<string> log)
        {
            // Resolve full path to the authserver executable and its directory.
            var exe = GetPath("authserver.exe");
            var dir = Path.GetDirectoryName(exe)!;

            // If the exe does not exist, inform the caller via the provided log action and abort startup.
            if (!File.Exists(exe)) { log($"Authserver nicht gefunden: {exe}"); return; }

            // Attach the caller-provided logger to the process output event so output lines are forwarded.
            AuthServer.OutputReceived += log;

            // Start the process using the ProcessManager with the executable path and its working directory.
            AuthServer.Start(exe, dir);

            // Notify that the authserver has been started.
            log("-- Authserver gestartet --");
        }

        /// <summary>
        /// Start the world server process if the executable is present.
        /// The method will:
        ///  - determine the full path to "worldserver.exe",
        ///  - verify the file exists,
        ///  - subscribe the provided logger to the process output events,
        ///  - start the process via the <see cref="ProcessManager"/>, and
        ///  - log a confirmation message.
        /// If the executable is not found, a descriptive message is logged and no process is started.
        /// </summary>
        /// <param name="log">Action to receive textual log messages (e.g. to append to a UI log control).</param>
        public void StartWorld(Action<string> log)
        {
            // Resolve full path to the worldserver executable and its directory.
            var exe = GetPath("worldserver.exe");
            var dir = Path.GetDirectoryName(exe)!;

            // If the exe does not exist, inform the caller via the provided log action and abort startup.
            if (!File.Exists(exe)) { log($"Worldserver nicht gefunden: {exe}"); return; }

            // Attach the caller-provided logger to the process output event so output lines are forwarded.
            WorldServer.OutputReceived += log;

            // Start the process using the ProcessManager with the executable path and its working directory.
            WorldServer.Start(exe, dir);

            // Notify that the worldserver has been started.
            log("-- Worldserver gestartet --");
        }

        /// <summary>
        /// Stop the authentication server process asynchronously.
        /// This method awaits the <see cref="ProcessManager.StopAsync"/> call to ensure the process has exited
        /// before returning and then logs a confirmation message via the provided <paramref name="log"/> action.
        /// </summary>
        /// <param name="log">Action to receive textual log messages (e.g. to append to a UI log control).</param>
        /// <returns>A task that completes once the authserver has stopped and the confirmation message has been logged.</returns>
        public async Task StopAuth(Action<string> log)
        {
            // Ask the ProcessManager to stop the process and wait for completion.
            await AuthServer.StopAsync();

            // Inform the caller that the authserver was stopped.
            log("-- Authserver gestoppt --");
        }

        /// <summary>
        /// Stop the world server process asynchronously.
        /// This method awaits the <see cref="ProcessManager.StopAsync"/> call to ensure the process has exited
        /// before returning and then logs a confirmation message via the provided <paramref name="log"/> action.
        /// </summary>
        /// <param name="log">Action to receive textual log messages (e.g. to append to a UI log control).</param>
        /// <returns>A task that completes once the worldserver has stopped and the confirmation message has been logged.</returns>
        public async Task StopWorld(Action<string> log)
        {
            // Ask the ProcessManager to stop the process and wait for completion.
            await WorldServer.StopAsync();

            // Inform the caller that the worldserver was stopped.
            log("-- Worldserver gestoppt --");
        }
    }
}
