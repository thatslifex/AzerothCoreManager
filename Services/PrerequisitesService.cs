using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace AzerothCoreManager.Services;

public class PrerequisitesService
{
    public record PrerequisiteCheck(string Name, bool Found, string? Version, string? Path, bool Required = true);

    public List<PrerequisiteCheck> CheckAll()
    {
        var results = new List<PrerequisiteCheck>();

        results.Add(CheckGit());
        results.Add(CheckCMake());
        results.Add(CheckVisualStudio());
        results.Add(CheckMySQL());
        results.Add(CheckOpenSSL());
        results.Add(CheckBoost());
        results.Add(CheckDotNet());
        results.Add(CheckVCRedist());
        results.Add(CheckGitHubDesktop());

        return results;
    }

    private PrerequisiteCheck CheckGit()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(3000);

            if (p?.ExitCode == 0 && output.Contains("git version"))
            {
                var version = output.Replace("git version", "").Trim();
                return new PrerequisiteCheck("Git for Windows", true, version, FindInPath("git.exe"));
            }
        }
        catch { }
        return new PrerequisiteCheck("Git for Windows", false, null, null);
    }

    private PrerequisiteCheck CheckCMake()
    {
        try
        {
            var psi = new ProcessStartInfo("cmake", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadLine() ?? "";
            p?.WaitForExit(3000);

            if (p?.ExitCode == 0 && output.Contains("cmake version"))
            {
                var version = output.Replace("cmake version", "").Trim();
                return new PrerequisiteCheck("CMake", true, version, FindInPath("cmake.exe"));
            }
        }
        catch { }
        return new PrerequisiteCheck("CMake", false, null, null);
    }

    private PrerequisiteCheck CheckVisualStudio()
    {
        // Check for VS 2022 via vswhere
        try
        {
            var vswhere = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";
            if (!File.Exists(vswhere))
                vswhere = FindInPath("vswhere.exe") ?? "";

            if (!string.IsNullOrEmpty(vswhere) && File.Exists(vswhere))
            {
                var psi = new ProcessStartInfo(vswhere, "-latest -property installationPath")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var path = p?.StandardOutput.ReadToEnd().Trim();
                p?.WaitForExit(5000);

                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return new PrerequisiteCheck("Visual Studio 2022", true, "2022", path);
            }
        }
        catch { }

        // Fallback: check registry
        try
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\VisualStudio\17.0\Setup\VS");
            if (key != null)
            {
                var path = key.GetValue("ProductDir") as string;
                if (!string.IsNullOrEmpty(path))
                    return new PrerequisiteCheck("Visual Studio 2022", true, "2022", path);
            }
        }
        catch { }

        return new PrerequisiteCheck("Visual Studio 2022", false, null, null);
    }

    private PrerequisiteCheck CheckMySQL()
    {
        // Check for MySQL via mysqld
        try
        {
            var psi = new ProcessStartInfo("mysqld", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(3000);

            if (output.Contains("mysqld"))
            {
                var version = ExtractVersion(output);
                return new PrerequisiteCheck("MySQL", true, version, FindInPath("mysqld.exe"));
            }
        }
        catch { }

        // Check common install paths
        var paths = new[] {
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqld.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld.exe",
            @"C:\Program Files\MySQL\MySQL Server 9.0\bin\mysqld.exe",
        };
        foreach (var p in paths)
        {
            if (File.Exists(p))
                return new PrerequisiteCheck("MySQL", true, "8.x", p);
        }

        return new PrerequisiteCheck("MySQL", false, null, null);
    }

    private PrerequisiteCheck CheckOpenSSL()
    {
        try
        {
            var psi = new ProcessStartInfo("openssl", "version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(3000);

            if (output.Contains("OpenSSL"))
            {
                var version = output.Replace("OpenSSL", "").Split(' ')[0].Trim();
                return new PrerequisiteCheck("OpenSSL", true, version, FindInPath("openssl.exe"));
            }
        }
        catch { }

        // Check common paths
        var paths = new[] {
            @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
            @"C:\OpenSSL-Win64\bin\openssl.exe",
        };
        foreach (var p in paths)
        {
            if (File.Exists(p))
                return new PrerequisiteCheck("OpenSSL", true, "3.x", p);
        }

        return new PrerequisiteCheck("OpenSSL", false, null, null);
    }

    private PrerequisiteCheck CheckBoost()
    {
        // Check BOOST_ROOT env var
        var boostRoot = Environment.GetEnvironmentVariable("BOOST_ROOT");
        if (!string.IsNullOrEmpty(boostRoot) && Directory.Exists(boostRoot))
            return new PrerequisiteCheck("Boost", true, "1.78+", boostRoot);

        // Check common paths
        var paths = new[] {
            @"C:\local\boost_1_87_0",
            @"C:\local\boost_1_85_0",
            @"C:\local\boost_1_81_0",
            @"C:\Boost",
        };
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
                return new PrerequisiteCheck("Boost", true, "1.x", p);
        }

        return new PrerequisiteCheck("Boost", false, null, null);
    }

    private PrerequisiteCheck CheckDotNet()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var version = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit(3000);

            if (!string.IsNullOrEmpty(version) && p?.ExitCode == 0)
                return new PrerequisiteCheck(".NET SDK", true, version, FindInPath("dotnet.exe"));
        }
        catch { }
        return new PrerequisiteCheck(".NET SDK", false, null, null);
    }

    private PrerequisiteCheck CheckVCRedist()
    {
        // Check via registry for VC++ 2015-2022 Redist
        try
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            if (key != null)
            {
                var installed = key.GetValue("Installed") as int?;
                if (installed == 1)
                    return new PrerequisiteCheck("VC Redist", true, "14.x", null);
            }
        }
        catch { }
        return new PrerequisiteCheck("VC Redist", false, null, null);
    }

    private PrerequisiteCheck CheckGitHubDesktop()
    {
        var path = @"C:\Users\" + Environment.UserName + @"\AppData\Local\GitHubDesktop\GitHubDesktop.exe";
        if (File.Exists(path))
            return new PrerequisiteCheck("GitHub Desktop", true, null, path, Required: false);

        path = FindInPath("GitHubDesktop.exe");
        if (path != null)
            return new PrerequisiteCheck("GitHub Desktop", true, null, path, Required: false);

        return new PrerequisiteCheck("GitHub Desktop", false, null, null, Required: false);
    }

    private static string? FindInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';'))
        {
            var full = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static string ExtractVersion(string output)
    {
        // Simple version extraction: find X.Y.Z pattern
        var match = System.Text.RegularExpressions.Regex.Match(output, @"\d+\.\d+\.\d+");
        return match.Success ? match.Value : output.Trim();
    }
}
