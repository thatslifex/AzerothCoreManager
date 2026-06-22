using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class DeployService
{
    public event Action<string>? LogMessage;
    public event Action<int>? ProgressChanged;

    public async Task<bool> DeployAsync(string buildOutputPath, string serverPath, CancellationToken ct = default)
    {
        LogMessage?.Invoke("Deploying build output to server directory...");
        ProgressChanged?.Invoke(92);

        await Task.Run(() =>
        {
            // Create server directory
            Directory.CreateDirectory(serverPath);

            // Copy all files from build output
            CopyDirectory(buildOutputPath, serverPath);

            // Handle configs directory specially
            var buildConfigs = Path.Combine(buildOutputPath, "configs");
            var serverConfigs = Path.Combine(serverPath, "configs");

            if (Directory.Exists(buildConfigs))
            {
                Directory.CreateDirectory(serverConfigs);

                // Copy .conf.dist files (always overwrite)
                foreach (var distFile in Directory.GetFiles(buildConfigs, "*.conf.dist"))
                {
                    var destFile = Path.Combine(serverConfigs, Path.GetFileName(distFile));
                    File.Copy(distFile, destFile, overwrite: true);
                    LogMessage?.Invoke($"Copied: {Path.GetFileName(distFile)}");
                }

                // Create .conf from .dist only if .conf doesn't exist
                foreach (var distFile in Directory.GetFiles(serverConfigs, "*.conf.dist"))
                {
                    var confFile = distFile.Replace(".conf.dist", ".conf");
                    if (!File.Exists(confFile))
                    {
                        File.Copy(distFile, confFile, overwrite: false);
                        LogMessage?.Invoke($"Created: {Path.GetFileName(confFile)}");
                    }
                    else
                    {
                        LogMessage?.Invoke($"Preserved existing: {Path.GetFileName(confFile)}");
                    }
                }
            }
        }, ct);

        ProgressChanged?.Invoke(95);
        LogMessage?.Invoke("Deploy completed.");
        return true;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            // Skip configs — handled separately
            if (dirName.Equals("configs", StringComparison.OrdinalIgnoreCase)) continue;
            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }
}
