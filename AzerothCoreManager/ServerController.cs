using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace AzerothCoreManager
{
    public class ServerController
    {
        public ProcessManager AuthServer { get; } = new();
        public ProcessManager WorldServer { get; } = new();
        public ProcessManager SQLServer { get; } = new();

        private string _basePath;
        private const string _sqlServiceSearchTerm = "mysql";

        private string GetPath(string exeName) => Path.Combine(_basePath, exeName);
        private string GetLogPath(string logFileName) => Path.Combine(_basePath, logFileName);

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

        // --- Auth ---
        public void StartAuth()
        {
            var exe = GetPath("authserver.exe");
            var dir = Path.GetDirectoryName(exe)!;

            if (!File.Exists(exe))
            {
                MessageBox.Show($"Authserver not found: {exe}");
                return;
            }

            // Auth.log löschen
            try
            {
                var logFile = GetLogPath("Auth.log");
                if (File.Exists(logFile))
                    File.Delete(logFile);
            }
            catch { }

            AuthServer.Start(exe, dir);
        }

        // --- World ---
        public void StartWorld()
        {
            var exe = GetPath("worldserver.exe");
            var dir = Path.GetDirectoryName(exe)!;

            if (!File.Exists(exe))
            {
                MessageBox.Show($"Worldserver not found: {exe}");
                return;
            }

            // Server.log löschen
            try
            {
                var logFile = GetLogPath("Server.log");
                if (File.Exists(logFile))
                    File.Delete(logFile);
            }
            catch { }

            WorldServer.Start(exe, dir);
        }

        // --- SQL ---
        public void StartSQL()
        {
            var svcName = FindSqlServiceName(_sqlServiceSearchTerm);
            if (string.IsNullOrEmpty(svcName))
            {
                MessageBox.Show("MySQL service not found. (Searching for \"mysql\" in ServiceName/DisplayName)");
                return;
            }

            try
            {
                using var svc = new ServiceController(svcName);
                if (svc.Status == ServiceControllerStatus.Running)
                {
                    MessageBox.Show($"SQL service '{svcName}' is already running.");
                    return;
                }

                try
                {
                    svc.Start();
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    MessageBox.Show($"-- SQL service '{svcName}' started --");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not start SQL service '{svcName}': {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing service '{svcName}': {ex.Message}");
            }
        }

        // --- Stop Methoden ---
        public async Task StopAuth()
        {
            if (AuthServer.IsRunning)
            {
                await AuthServer.StopAsync();
                return;
            }

            var procName = Path.GetFileNameWithoutExtension("authserver.exe");
            var procs = Process.GetProcessesByName(procName);
            foreach (var p in procs)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        try { p.CloseMainWindow(); } catch { }
                        if (!p.WaitForExit(3000))
                        {
                            try { p.Kill(); } catch { }
                            p.WaitForExit(5000);
                        }
                    }
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }

        public async Task StopWorld()
        {
            if (WorldServer.IsRunning)
            {
                await WorldServer.StopAsync();
                return;
            }

            var procName = Path.GetFileNameWithoutExtension("worldserver.exe");
            var procs = Process.GetProcessesByName(procName);
            foreach (var p in procs)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        try { p.CloseMainWindow(); } catch { }
                        if (!p.WaitForExit(3000))
                        {
                            try { p.Kill(); } catch { }
                            p.WaitForExit(5000);
                        }
                    }
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }

        public async Task StopSQL()
        {
            var svcName = FindSqlServiceName(_sqlServiceSearchTerm);
            if (string.IsNullOrEmpty(svcName))
            {
                MessageBox.Show("MySQL service not found. (Searching for \"mysql\" in ServiceName/DisplayName)");
                return;
            }

            try
            {
                using var svc = new ServiceController(svcName);
                if (svc.Status == ServiceControllerStatus.Stopped)
                {
                    MessageBox.Show($"SQL service '{svcName}' is already stopped.");
                    return;
                }

                try
                {
                    svc.Stop();
                    await Task.Run(() =>
                    {
                        try { svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); }
                        catch { }
                    });

                    MessageBox.Show($"-- SQL service '{svcName}' stopped --");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not stop SQL service '{svcName}': {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing service '{svcName}': {ex.Message}");
            }
        }

        public async Task RestartSQL()
        {
            var svcName = FindSqlServiceName(_sqlServiceSearchTerm);
            if (string.IsNullOrEmpty(svcName))
            {
                MessageBox.Show("MySQL service not found. (Searching for \"mysql\" in ServiceName/DisplayName)");
                return;
            }

            try
            {
                using var svc = new ServiceController(svcName);

                try
                {
                    svc.Stop();
                    await Task.Run(() =>
                    {
                        try { svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); }
                        catch { }
                    });
                    svc.Start();
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    MessageBox.Show($"-- SQL service '{svcName}' restarted --");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not restart SQL service '{svcName}': {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing service '{svcName}': {ex.Message}");
            }
        }

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
                // ignore enumeration errors
            }

            return null;
        }
    }
}
