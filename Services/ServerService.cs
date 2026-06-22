using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class ServerService
{
    public event Action<string>? LogMessage;
    public event Action<string, ServerProcessType>? OutputReceived;
    public event Action<ServerProcessType, bool>? StatusChanged;

    private Process? _authProcess;
    private Process? _worldProcess;
    private readonly ProcessRunner _runner = new();

    public bool IsAuthRunning => _authProcess is { HasExited: false };
    public bool IsWorldRunning => _worldProcess is { HasExited: false };

    /// <summary>
    /// Starts the authserver process.
    /// </summary>
    public bool StartAuthserver(string serverPath)
    {
        if (IsAuthRunning) return true;

        var exePath = Path.Combine(serverPath, "authserver.exe");
        if (!File.Exists(exePath))
        {
            LogMessage?.Invoke($"authserver.exe not found at {exePath}");
            return false;
        }

        try
        {
            _authProcess = _runner.StartBackground(exePath, "", serverPath);
            if (_authProcess == null) return false;

            _authProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data, ServerProcessType.Authserver);
            };
            _authProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data, ServerProcessType.Authserver);
            };
            _authProcess.Exited += (s, e) =>
            {
                StatusChanged?.Invoke(ServerProcessType.Authserver, false);
                LogMessage?.Invoke("Authserver stopped.");
            };

            StatusChanged?.Invoke(ServerProcessType.Authserver, true);
            LogMessage?.Invoke("Authserver started.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Failed to start authserver: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts the worldserver process.
    /// </summary>
    public bool StartWorldserver(string serverPath)
    {
        if (IsWorldRunning) return true;

        var exePath = Path.Combine(serverPath, "worldserver.exe");
        if (!File.Exists(exePath))
        {
            LogMessage?.Invoke($"worldserver.exe not found at {exePath}");
            return false;
        }

        try
        {
            _worldProcess = _runner.StartBackground(exePath, "", serverPath);
            if (_worldProcess == null) return false;

            _worldProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data, ServerProcessType.Worldserver);
            };
            _worldProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data, ServerProcessType.Worldserver);
            };
            _worldProcess.Exited += (s, e) =>
            {
                StatusChanged?.Invoke(ServerProcessType.Worldserver, false);
                LogMessage?.Invoke("Worldserver stopped.");
            };

            StatusChanged?.Invoke(ServerProcessType.Worldserver, true);
            LogMessage?.Invoke("Worldserver started.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Failed to start worldserver: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stops a server process gracefully (CloseMainWindow), then force-kills after timeout.
    /// </summary>
    public async Task<bool> StopProcessAsync(ServerProcessType type, int gracefulTimeoutMs = 10000)
    {
        var process = type switch
        {
            ServerProcessType.Authserver => _authProcess,
            ServerProcessType.Worldserver => _worldProcess,
            _ => null
        };

        if (process == null || process.HasExited) return true;

        try
        {
            process.CloseMainWindow();
            var waited = process.WaitForExit(gracefulTimeoutMs);
            if (!waited)
            {
                process.Kill(entireProcessTree: true);
                await Task.Run(() => process.WaitForExit(5000));
            }

            StatusChanged?.Invoke(type, false);
            LogMessage?.Invoke($"{type} stopped.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Failed to stop {type}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restarts a server process.
    /// </summary>
    public async Task<bool> RestartProcessAsync(ServerProcessType type, string serverPath)
    {
        await StopProcessAsync(type);

        return type switch
        {
            ServerProcessType.Authserver => StartAuthserver(serverPath),
            ServerProcessType.Worldserver => StartWorldserver(serverPath),
            _ => false
        };
    }

    /// <summary>
    /// Stops all running server processes.
    /// </summary>
    public async Task StopAllAsync()
    {
        if (IsAuthRunning) await StopProcessAsync(ServerProcessType.Authserver);
        if (IsWorldRunning) await StopProcessAsync(ServerProcessType.Worldserver);
    }

    /// <summary>
    /// Checks if MySQL is running by testing a connection.
    /// </summary>
    public async Task<bool> IsMySqlRunningAsync(string host, int port, string user, string password)
    {
        var db = new DatabaseService();
        return await db.TestConnectionAsync(host, port, user, password);
    }
}

public enum ServerProcessType
{
    Authserver,
    Worldserver
}
