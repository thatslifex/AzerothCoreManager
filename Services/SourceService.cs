using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class SourceService
{
    public event Action<string>? LogMessage;

    public async Task<bool> CloneAsync(string repoUrl, string targetPath, string branch = "master", CancellationToken ct = default)
    {
        LogMessage?.Invoke($"Cloning {repoUrl} into {targetPath}...");

        if (Directory.Exists(targetPath))
        {
            LogMessage?.Invoke("Target directory already exists. Pulling latest changes instead.");
            return await PullAsync(targetPath, branch, ct);
        }

        var psi = new ProcessStartInfo("git", $"clone --branch {branch} --single-branch {repoUrl} \"{targetPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return await RunProcessAsync(psi, ct);
    }

    public async Task<bool> PullAsync(string repoPath, string branch = "master", CancellationToken ct = default)
    {
        LogMessage?.Invoke($"Pulling latest changes for {branch}...");

        var psi = new ProcessStartInfo("git", $"pull origin {branch}")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return await RunProcessAsync(psi, ct);
    }

    public async Task<bool> InitSubmodulesAsync(string repoPath, CancellationToken ct = default)
    {
        LogMessage?.Invoke("Initializing submodules...");

        var psi = new ProcessStartInfo("git", "submodule update --init --recursive")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return await RunProcessAsync(psi, ct);
    }

    public string? GetCurrentBranch(string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
            {
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var branch = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit(3000);
            return branch;
        }
        catch { return null; }
    }

    public string? GetLatestCommit(string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "log -1 --format=%H")
            {
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var hash = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit(3000);
            return hash;
        }
        catch { return null; }
    }

    private async Task<bool> RunProcessAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        using var p = Process.Start(psi);
        if (p == null) return false;

        var output = new StringWriter();
        p.OutputDataReceived += (s, e) => { if (e.Data != null) { output.WriteLine(e.Data); LogMessage?.Invoke(e.Data); } };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) { output.WriteLine(e.Data); LogMessage?.Invoke(e.Data); } };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(); } catch { }
            LogMessage?.Invoke("Operation timed out.");
            return false;
        }
    }
}
