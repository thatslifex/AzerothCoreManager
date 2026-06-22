using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class BackupService
{
    public event Action<string>? LogMessage;

    /// <summary>
    /// Creates a mysqldump backup of all three AzerothCore databases.
    /// </summary>
    public async Task<string?> CreateBackupAsync(string host, int port, string user, string password, string outputDir, CancellationToken ct = default)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filePath = Path.Combine(outputDir, $"acore_backup_{timestamp}.sql");

        Directory.CreateDirectory(outputDir);

        var args = $"-h {host} -P {port} -u {user} -p\"{password}\" " +
                   "--databases acore_auth acore_characters acore_world " +
                   $"--result-file=\"{filePath}\" " +
                   "--single-transaction --routines --triggers --events";

        LogMessage?.Invoke($"Running mysqldump to {filePath}...");

        var psi = new ProcessStartInfo("mysqldump", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p == null)
        {
            LogMessage?.Invoke("mysqldump not found. Is MySQL installed?");
            return null;
        }

        p.ErrorDataReceived += (s, e) => { if (e.Data != null) LogMessage?.Invoke(e.Data); };
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
            if (p.ExitCode == 0)
            {
                var size = new FileInfo(filePath).Length;
                LogMessage?.Invoke($"Backup complete: {filePath} ({size / 1024 / 1024:F1} MB)");
                return filePath;
            }
            else
            {
                LogMessage?.Invoke($"mysqldump failed with exit code {p.ExitCode}");
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(); } catch { }
            LogMessage?.Invoke("Backup timed out.");
            return null;
        }
    }

    /// <summary>
    /// Restores a database from a .sql backup file.
    /// </summary>
    public async Task<bool> RestoreBackupAsync(string host, int port, string user, string password, string backupFile, CancellationToken ct = default)
    {
        if (!File.Exists(backupFile))
        {
            LogMessage?.Invoke($"Backup file not found: {backupFile}");
            return false;
        }

        LogMessage?.Invoke($"Restoring from {backupFile}...");

        var args = $"-h {host} -P {port} -u {user} -p\"{password}\"";
        var psi = new ProcessStartInfo("mysql", args)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p == null)
        {
            LogMessage?.Invoke("mysql client not found.");
            return false;
        }

        p.ErrorDataReceived += (s, e) => { if (e.Data != null) LogMessage?.Invoke(e.Data); };
        p.BeginErrorReadLine();

        // Feed the SQL file to stdin
        var sql = await File.ReadAllTextAsync(backupFile, ct);
        await p.StandardInput.WriteAsync(sql);
        p.StandardInput.Close();

        try
        {
            await p.WaitForExitAsync(ct);
            var ok = p.ExitCode == 0;
            LogMessage?.Invoke(ok ? "Restore complete." : $"Restore failed with exit code {p.ExitCode}.");
            return ok;
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(); } catch { }
            LogMessage?.Invoke("Restore timed out.");
            return false;
        }
    }

    /// <summary>
    /// Lists backup files in the output directory.
    /// </summary>
    public List<BackupInfo> ListBackups(string outputDir)
    {
        var backups = new List<BackupInfo>();
        if (!Directory.Exists(outputDir)) return backups;

        foreach (var file in Directory.GetFiles(outputDir, "acore_backup_*.sql"))
        {
            var info = new FileInfo(file);
            backups.Add(new BackupInfo
            {
                FileName = info.Name,
                Path = info.FullName,
                Size = info.Length,
                Created = info.CreationTime
            });
        }

        backups.Sort((a, b) => b.Created.CompareTo(a.Created));
        return backups;
    }

    public record BackupInfo
    {
        public string FileName { get; init; } = "";
        public string Path { get; init; } = "";
        public long Size { get; init; }
        public DateTime Created { get; init; }
    }
}
