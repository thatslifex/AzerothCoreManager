using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace AzerothCoreManager.Services;

public class PrerequisitesInstaller
{
    public event Action<string>? ProgressChanged;
    public event Action<string>? LogMessage;

    public async Task InstallMissing(List<PrerequisitesService.PrerequisiteCheck> checks, CancellationToken ct = default)
    {
        foreach (var check in checks.Where(c => !c.Found && c.Required))
        {
            LogMessage?.Invoke($"Installing {check.Name}...");
            ProgressChanged?.Invoke($"Installing {check.Name}...");

            switch (check.Name)
            {
                case "Git for Windows":
                    await InstallGit(ct);
                    break;
                case "CMake":
                    await InstallCMake(ct);
                    break;
                case "Visual Studio 2022":
                    await InstallVS(ct);
                    break;
                case "MySQL":
                    await InstallMySQL(ct);
                    break;
                case "OpenSSL":
                    await InstallOpenSSL(ct);
                    break;
                case "Boost":
                    await InstallBoost(ct);
                    break;
                case ".NET SDK":
                    await InstallDotNet(ct);
                    break;
                case "VC Redist":
                    await InstallVCRedist(ct);
                    break;
            }
        }
    }

    private async Task InstallGit(CancellationToken ct)
    {
        var url = "https://github.com/git-for-windows/git/releases/download/v2.50.1.windows.1/Git-2.50.1-64-bit.exe";
        var installer = await DownloadAsync(url, "git-installer.exe", ct);
        await RunInstallerAsync(installer, "/VERYSILENT /NORESTART", ct);
    }

    private async Task InstallCMake(CancellationToken ct)
    {
        var url = "https://github.com/Kitware/CMake/releases/download/v4.2.1/cmake-4.2.1-windows-x86_64.msi";
        var installer = await DownloadAsync(url, "cmake-installer.msi", ct);
        await RunInstallerAsync("msiexec", $"/i \"{installer}\" /quiet /norestart", ct);
    }

    private async Task InstallVS(CancellationToken ct)
    {
        LogMessage?.Invoke("Visual Studio 2022 Community — this may take 30-90 minutes...");
        var url = "https://aka.ms/vs/17/release/vs_community.exe";
        var installer = await DownloadAsync(url, "vs-installer.exe", ct);
        await RunInstallerAsync(installer,
            "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.NativeDesktop --includeRecommended",
            ct, timeout: 5400000); // 90 minutes
    }

    private async Task InstallMySQL(CancellationToken ct)
    {
        var url = "https://dev.mysql.com/get/Downloads/MySQLInstaller/mysql-installer-community-8.4.0.msi";
        var installer = await DownloadAsync(url, "mysql-installer.msi", ct);
        await RunInstallerAsync("msiexec", $"/i \"{installer}\" /quiet /norestart", ct);
    }

    private async Task InstallOpenSSL(CancellationToken ct)
    {
        var url = "https://slproweb.com/download/Win64OpenSSL-3_5_0.exe";
        var installer = await DownloadAsync(url, "openssl-installer.exe", ct);
        await RunInstallerAsync(installer, "/VERYSILENT /NORESTART", ct);
    }

    private async Task InstallBoost(CancellationToken ct)
    {
        var url = "https://github.com/boostorg/boost/releases/download/boost-1.87.0/boost-1.87.0-msvc-14.3-64.exe";
        var installer = await DownloadAsync(url, "boost-installer.exe", ct);
        await RunInstallerAsync(installer, "/VERYSILENT /NORESTART /DIR=C:\\local\\boost_1_87_0", ct);

        // Set BOOST_ROOT
        Environment.SetEnvironmentVariable("BOOST_ROOT", @"C:\local\boost_1_87_0", EnvironmentVariableTarget.User);
    }

    private async Task InstallDotNet(CancellationToken ct)
    {
        // .NET 10 SDK is already installed on this machine — skip
        LogMessage?.Invoke(".NET SDK already installed — skipping.");
    }

    private async Task InstallVCRedist(CancellationToken ct)
    {
        var url = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
        var installer = await DownloadAsync(url, "vc-redist-installer.exe", ct);
        await RunInstallerAsync(installer, "/quiet /norestart", ct);
    }

    private async Task<string> DownloadAsync(string url, string fileName, CancellationToken ct)
    {
        var tempDir = Path.GetTempPath();
        var dest = Path.Combine(tempDir, fileName);

        LogMessage?.Invoke($"Downloading {fileName}...");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(dest);
        await stream.CopyToAsync(file, ct);

        LogMessage?.Invoke($"Downloaded {fileName} ({new FileInfo(dest).Length / 1024 / 1024} MB)");
        return dest;
    }

    private async Task RunInstallerAsync(string path, string args, CancellationToken ct, int timeout = 900000)
    {
        LogMessage?.Invoke($"Running: {Path.GetFileName(path)} {args}");

        var psi = new ProcessStartInfo(path, args)
        {
            UseShellExecute = true,
            Verb = "runas", // Admin
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var p = Process.Start(psi);
        if (p == null)
        {
            LogMessage?.Invoke($"Failed to start installer: {path}");
            return;
        }

        try
        {
            await p.WaitForExitAsync(ct);
            LogMessage?.Invoke($"{Path.GetFileName(path)} completed (exit code {p.ExitCode})");
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(); } catch { }
            LogMessage?.Invoke($"{Path.GetFileName(path)} timed out");
        }
    }
}
