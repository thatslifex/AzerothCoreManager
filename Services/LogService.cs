using System.IO;
using System.Threading;

namespace AzerothCoreManager.Services;

public class LogService
{
    /// <summary>
    /// Reads the last N lines from a log file.
    /// </summary>
    public string[] Tail(string filePath, int lines = 200)
    {
        if (!File.Exists(filePath)) return Array.Empty<string>();

        var allLines = File.ReadAllLines(filePath);
        var start = Math.Max(0, allLines.Length - lines);
        return allLines[start..];
    }

    /// <summary>
    /// Reads all lines from a log file.
    /// </summary>
    public string[] ReadAll(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllLines(filePath) : Array.Empty<string>();
    }

    /// <summary>
    /// Filters log lines by a search term (case-insensitive).
    /// </summary>
    public string[] Filter(string filePath, string searchTerm, int maxResults = 500)
    {
        if (!File.Exists(filePath) || string.IsNullOrEmpty(searchTerm))
            return Array.Empty<string>();

        var results = new List<string>();
        foreach (var line in File.ReadLines(filePath))
        {
            if (line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(line);
                if (results.Count >= maxResults) break;
            }
        }
        return results.ToArray();
    }

    /// <summary>
    /// Discovers available log files in the server directory.
    /// </summary>
    public List<LogFileInfo> DiscoverLogs(string serverPath)
    {
        var logs = new List<LogFileInfo>();
        if (!Directory.Exists(serverPath)) return logs;

        // Known log files
        var knownLogs = new[] { "Server.log", "DBErrors.log", "auth.log", "world.log" };
        foreach (var name in knownLogs)
        {
            var path = Path.Combine(serverPath, name);
            if (File.Exists(path))
            {
                logs.Add(new LogFileInfo
                {
                    Name = name,
                    Path = path,
                    Size = new FileInfo(path).Length,
                    LastModified = File.GetLastWriteTime(path)
                });
            }
        }

        // Also check Logs/ subdirectory
        var logsDir = Path.Combine(serverPath, "Logs");
        if (Directory.Exists(logsDir))
        {
            foreach (var file in Directory.GetFiles(logsDir, "*.log"))
            {
                var name = Path.GetFileName(file);
                if (!logs.Any(l => l.Name == name))
                {
                    logs.Add(new LogFileInfo
                    {
                        Name = $"Logs/{name}",
                        Path = file,
                        Size = new FileInfo(file).Length,
                        LastModified = File.GetLastWriteTime(file)
                    });
                }
            }
        }

        return logs;
    }

    public record LogFileInfo
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public long Size { get; init; }
        public DateTime LastModified { get; init; }
    }
}
