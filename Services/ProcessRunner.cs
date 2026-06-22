using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class ProcessRunner
{
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;
    public event Action<int>? ProcessExited;

    public async Task<int> RunAsync(string fileName, string arguments, string? workingDirectory = null,
        CancellationToken ct = default, int timeoutMs = 0)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (workingDirectory != null)
            psi.WorkingDirectory = workingDirectory;

        using var p = Process.Start(psi);
        if (p == null) return -1;

        p.OutputDataReceived += (s, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data); };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) ErrorReceived?.Invoke(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            if (timeoutMs > 0)
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                await p.WaitForExitAsync(linkedCts.Token);
            }
            else
            {
                await p.WaitForExitAsync(ct);
            }

            ProcessExited?.Invoke(p.ExitCode);
            return p.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            ProcessExited?.Invoke(-1);
            return -1;
        }
    }

    public Process? StartBackground(string fileName, string arguments, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (workingDirectory != null)
            psi.WorkingDirectory = workingDirectory;

        var p = Process.Start(psi);
        if (p != null)
        {
            p.OutputDataReceived += (s, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) ErrorReceived?.Invoke(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.EnableRaisingEvents = true;
            p.Exited += (s, e) => ProcessExited?.Invoke(p.ExitCode);
        }
        return p;
    }
}
