using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class BuildService
{
    public event Action<string>? LogMessage;
    public event Action<int>? ProgressChanged; // 0-100

    public async Task<bool> ConfigureAsync(string sourcePath, string buildPath, CancellationToken ct = default)
    {
        LogMessage?.Invoke("Configuring CMake...");
        ProgressChanged?.Invoke(10);

        // Ensure build directory exists
        Directory.CreateDirectory(buildPath);

        var args = $"-S \"{sourcePath}\" -B \"{buildPath}\" -G \"Visual Studio 17 2022\" -A x64 " +
                   "-DTOOLS_BUILD=all -DWITH_WARNINGS=0 -DSCRIPTS=dynamic -DMODULES=dynamic";

        var psi = new ProcessStartInfo("cmake", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await RunProcessAsync(psi, ct);
        if (result) ProgressChanged?.Invoke(30);
        return result;
    }

    public async Task<bool> BuildAsync(string buildPath, string config = "RelWithDebInfo", CancellationToken ct = default)
    {
        LogMessage?.Invoke($"Building with MSBuild ({config})...");
        ProgressChanged?.Invoke(35);

        var args = $"--build \"{buildPath}\" --config {config} --target ALL_BUILD -j 8";

        var psi = new ProcessStartInfo("cmake", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var result = await RunProcessAsync(psi, ct);
        if (result) ProgressChanged?.Invoke(90);
        return result;
    }

    public string GetBuildOutputPath(string buildPath, string config = "RelWithDebInfo")
    {
        return Path.Combine(buildPath, "bin", config);
    }

    private async Task<bool> RunProcessAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        using var p = Process.Start(psi);
        if (p == null) return false;

        p.OutputDataReceived += (s, e) => { if (e.Data != null) LogMessage?.Invoke(e.Data); };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) LogMessage?.Invoke(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
            var ok = p.ExitCode == 0;
            LogMessage?.Invoke(ok ? "Build completed successfully." : $"Build failed with exit code {p.ExitCode}.");
            return ok;
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(); } catch { }
            LogMessage?.Invoke("Build timed out.");
            return false;
        }
    }
}
