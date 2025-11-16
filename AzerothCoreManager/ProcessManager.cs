using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AzerothCoreManager
{
    /// <summary>
    /// Manages a single system process: starting it, observing its exit, and stopping/disposing it when requested.
    /// Output wird jetzt nicht mehr über Events erfasst, sondern direkt aus Logfiles gelesen.
    /// </summary>
    public class ProcessManager
    {
        /// <summary>
        /// Event invoked when the running state changes. Parameter ist true wenn Prozess startet, false beim Stop.
        /// </summary>
        public event Action<bool>? RunningChanged;

        private Process? _process;

        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>
        /// Startet einen neuen Prozess, wenn noch keiner läuft.
        /// </summary>
        public void Start(string exePath, string workingDir)
        {
            if (IsRunning)
                return;

            var psi = new ProcessStartInfo(exePath)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = true,  // keine Umleitung nötig
                CreateNoWindow = true,
                
            };

            var proc = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            proc.Exited += (s, e) => RunningChanged?.Invoke(false);

            proc.Start();
            _process = proc;
            RunningChanged?.Invoke(true);
        }

        /// <summary>
        /// Stoppt den Prozess asynchron.
        /// </summary>
        public async Task StopAsync()
        {
            if (_process == null)
                return;

            var proc = _process;
            _process = null;

            try
            {
                if (!proc.HasExited)
                {
                    try { proc.CloseMainWindow(); } catch { }
                    if (!proc.WaitForExit(3000))
                    {
                        try { proc.Kill(); } catch { }
                        proc.WaitForExit(5000);
                    }
                }
            }
            catch { }
            finally
            {
                try { proc.Dispose(); } catch { }
                RunningChanged?.Invoke(false);
            }
        }
    }
}
