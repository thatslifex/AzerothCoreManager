using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

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
        /// Process manager responsible for starting, monitoring and stopping the SQL server process.
        /// Subscribe to <c>SQLServer.OutputReceived</c> to receive text output from the authserver process.
        /// </summary>
        public ProcessManager SQLServer { get; } = new();

        /// <summary>
        /// Base folder where the server executables are expected to be located.
        /// This value is combined with executable file names using <see cref="GetPath(string)"/>.
        /// </summary>
        private string _basePath;

        /// <summary>
        /// Base folder where the MySQL server executables are expected to be located.
        /// This value is combined with executable file names using <see cref="GetPath(string)"/>.
        /// </summary>
        private readonly string _baseSQLPath = "C:\\Program Files\\MySQL\\MySQL Server 8.4\\bin\\";

        /// <summary>
        /// Approximate search term used to find the MySQL Windows service (case-insensitive substring match).
        /// </summary>
        private const string _sqlServiceSearchTerm = "mysql";

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

        public void SetBasePath(string path)
        {
            _basePath = path;
            SettingsManager.SaveBasePath(path);
        }


        public ServerController()
        {
            _basePath = SettingsManager.LoadBasePath();
        }

        public bool HasValidPath => !string.IsNullOrEmpty(_basePath);

        public string BasePath => _basePath;

        /// <summary>
        /// Combines the base SQL directory path with the specified executable name to generate a platform-correct file
        /// path.
        /// </summary>
        /// <param name="exeName">The name of the executable file to append to the base SQL path. Cannot be null or contain invalid path
        /// characters.</param>
        /// <returns>A string representing the full file path to the specified executable within the base SQL directory.</returns>
        private string GetSQLPath(string exeName)
        {
            // Use Path.Combine to produce a platform-correct path string from base path and file name.
            return Path.Combine(_baseSQLPath, exeName);
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
        /// Start the MySQL server by controlling the Windows service instead of launching mysqld.exe directly.
        /// </summary>
        public void StartSQL()
        {
            // Try to find a service whose ServiceName or DisplayName contains "mysql".
            var svcName = FindSqlServiceName(_sqlServiceSearchTerm);
            if (string.IsNullOrEmpty(svcName))
            {
                MessageBox.Show("MySQL-Service nicht gefunden. (Suche nach \"mysql\" im ServiceName/DisplayName)");
                return;
            }

            try
            {
                using var svc = new ServiceController(svcName);
                if (svc.Status == ServiceControllerStatus.Running)
                {
                    MessageBox.Show($"SQL-Service '{svcName}' läuft bereits.");
                    return;
                }

                try
                {
                    svc.Start();
                    // Block briefly while service starts; do not block indefinitely.
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    MessageBox.Show($"-- SQL-Service '{svcName}' gestartet --");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Konnte SQL-Service '{svcName}' nicht starten: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zugriff auf den Service '{svcName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the authentication server process asynchronously.
        /// If this application started the process via the <see cref="ProcessManager"/>, that manager will be used.
        /// If the process was started externally, the code will attempt to stop/kill matching processes by name.
        /// </summary>
        /// <param name="log">Action to receive textual log messages.</param>
        public async Task StopAuth(Action<string> log)
        {
            // If we started the process, ask the ProcessManager to stop it.
            if (AuthServer.IsRunning)
            {
                await AuthServer.StopAsync();
                log("-- Authserver gestoppt --");
                return;
            }

            // Otherwise, attempt to stop external processes named "authserver"
            var procName = Path.GetFileNameWithoutExtension("authserver.exe");
            var procs = Process.GetProcessesByName(procName);
            if (procs == null || procs.Length == 0)
            {
                log("Authserver nicht gefunden.");
                return;
            }

            foreach (var p in procs)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        try { p.CloseMainWindow(); } catch { }
                        if (!p.WaitForExit(3000))
                        {
                            try { p.Kill(); }
                            catch (Exception ex)
                            {
                                log($"Konnte Authserver process {p.Id} nicht killen: {ex.Message}");
                                continue;
                            }
                            p.WaitForExit(5000);
                        }
                    }
                    log($"Authserver process {p.Id} beendet.");
                }
                catch (Exception ex)
                {
                    log($"Fehler beim Beenden des Authserver process {p.Id}: {ex.Message}");
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// Stop the world server process asynchronously.
        /// If this application started the process via the <see cref="ProcessManager"/>, that manager will be used.
        /// If the process was started externally, the code will attempt to stop/kill matching processes by name.
        /// </summary>
        /// <param name="log">Action to receive textual log messages.</param>
        public async Task StopWorld(Action<string> log)
        {
            // If we started the process, ask the ProcessManager to stop it.
            if (WorldServer.IsRunning)
            {
                await WorldServer.StopAsync();
                log("-- Worldserver gestoppt --");
                return;
            }

            // Otherwise, attempt to stop external processes named "worldserver"
            var procName = Path.GetFileNameWithoutExtension("worldserver.exe");
            var procs = Process.GetProcessesByName(procName);
            if (procs == null || procs.Length == 0)
            {
                log("Worldserver nicht gefunden.");
                return;
            }

            foreach (var p in procs)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        try { p.CloseMainWindow(); } catch { }
                        if (!p.WaitForExit(3000))
                        {
                            try { p.Kill(); }
                            catch (Exception ex)
                            {
                                log($"Konnte Worldserver process {p.Id} nicht killen: {ex.Message}");
                                continue;
                            }
                            p.WaitForExit(5000);
                        }
                    }
                    log($"Worldserver process {p.Id} beendet.");
                }
                catch (Exception ex)
                {
                    log($"Fehler beim Beenden des Worldserver process {p.Id}: {ex.Message}");
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// Stop the MySQL server by stopping the Windows service instead of killing a mysqld.exe process.
        /// </summary>
        /// <returns>A task that completes when the stop attempt finished (or if service not available).</returns>
        public async Task StopSQL()
        {
            // Try to find a service whose ServiceName or DisplayName contains "mysql".
            var svcName = FindSqlServiceName(_sqlServiceSearchTerm);
            if (string.IsNullOrEmpty(svcName))
            {
                MessageBox.Show("MySQL-Service nicht gefunden. (Suche nach \"mysql\" im ServiceName/DisplayName)");
                return;
            }

            try
            {
                using var svc = new ServiceController(svcName);
                if (svc.Status == ServiceControllerStatus.Stopped)
                {
                    MessageBox.Show($"SQL-Service '{svcName}' ist bereits gestoppt.");
                    return;
                }

                try
                {
                    svc.Stop();
                    // Wait for stop asynchronously to avoid blocking UI thread.
                    await Task.Run(() =>
                    {
                        try
                        {
                            svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                        }
                        catch
                        {
                            // ignore wait exceptions; we attempted stop
                        }
                    });

                    MessageBox.Show($"-- SQL-Service '{svcName}' gestoppt --");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Konnte SQL-Service '{svcName}' nicht stoppen: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zugriff auf den Service '{svcName}': {ex.Message}");
            }
        }

        public async Task RestartSQL()
        {
            // Try to find a service whose ServiceName or DisplayName contains "mysql".
            var svcName = FindSqlServiceName(_sqlServiceSearchTerm);
            if (string.IsNullOrEmpty(svcName))
            {
                MessageBox.Show("MySQL-Service nicht gefunden. (Suche nach \"mysql\" im ServiceName/DisplayName)");
                return;
            }

            try
            {
                using var svc = new ServiceController(svcName);

                try
                {
                    svc.Stop();
                    // Wait for stop asynchronously to avoid blocking UI thread.
                    await Task.Run(() =>
                    {
                        try
                        {
                            svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                        }
                        catch
                        {
                            // ignore wait exceptions; we attempted stop
                        }
                    });
                    svc.Start();
                    // Block briefly while service starts; do not block indefinitely.
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    MessageBox.Show($"-- SQL-Service '{svcName}' neu gestartet --");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Konnte SQL-Service '{svcName}' nicht neu gestartet werden: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zugriff auf den Service '{svcName}': {ex.Message}");
            }
        }


        /// <summary>
        /// Searches for a Windows service whose ServiceName or DisplayName contains the configured search term.
        /// Returns the ServiceName if found, otherwise null.
        /// </summary>
        public static string? FindSqlServiceName(string searchTerm)
        {
            try
            {
                var services = ServiceController.GetServices();
                foreach (var svc in services)
                {
                    if ((svc.ServiceName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                        || (svc.DisplayName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    {
                        return svc.ServiceName;
                    }
                }
            }
            catch
            {
                // ignore issues enumerating services (insufficient permissions, etc.)
            }

            return null;
        }
    }
}