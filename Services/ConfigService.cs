using System.IO;
using System.Collections.Generic;

namespace AzerothCoreManager.Services;

public class ConfigService
{
    /// <summary>
    /// Reads a .conf file and returns a dictionary of key-value pairs.
    /// Values are returned WITHOUT surrounding quotes (trimmed).
    /// Format: KeyName = "value"
    /// </summary>
    public Dictionary<string, string> ReadConfig(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath)) return result;

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = trimmed[..eqIdx].Trim();
            var value = trimmed[(eqIdx + 1)..].Trim().Trim('"');
            result[key] = value;
        }
        return result;
    }

    /// <summary>
    /// Reads SOAP config from worldserver.conf.
    /// Returns (enabled, ip, port) — all values unquoted.
    /// </summary>
    public (bool Enabled, string Ip, int Port) ReadSoapConfig(string worldserverConfPath)
    {
        var config = ReadConfig(worldserverConfPath);
        var enabled = config.TryGetValue("SOAP.Enabled", out var e) && e == "1";
        var ip = config.GetValueOrDefault("SOAP.IP", "127.0.0.1");
        var port = config.TryGetValue("SOAP.Port", out var p) && int.TryParse(p, out var portNum) ? portNum : 7878;
        return (enabled, ip, port);
    }

    /// <summary>
    /// Reads database connection info from a .conf file.
    /// Format: "host;port;user;password;database"
    /// </summary>
    public (string Host, int Port, string User, string Password, string Database)? ReadDatabaseInfo(string confPath, string key)
    {
        var config = ReadConfig(confPath);
        if (!config.TryGetValue(key, out var connStr)) return null;

        var parts = connStr.Split(';');
        if (parts.Length < 5) return null;

        return (
            parts[0],
            int.TryParse(parts[1], out var p) ? p : 3306,
            parts[2],
            parts[3],
            parts[4]
        );
    }

    /// <summary>
    /// Writes a config dictionary back to a .conf file, preserving comments and structure.
    /// Reads the original file, updates matching keys, writes back.
    /// </summary>
    public void WriteConfig(string filePath, Dictionary<string, string> updates)
    {
        if (!File.Exists(filePath)) return;

        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = trimmed[..eqIdx].Trim();
            if (updates.TryGetValue(key, out var newValue))
            {
                var originalValue = trimmed[(eqIdx + 1)..].Trim();
                var needsQuotes = originalValue.StartsWith('"');
                var newValStr = needsQuotes ? $"\"{newValue}\"" : newValue;
                lines[i] = $"{key} = {newValStr}";
            }
        }

        var tmp = filePath + ".tmp";
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, filePath, overwrite: true);
    }

    /// <summary>
    /// Returns the raw lines of a .conf file for display in an editor.
    /// </summary>
    public string[] ReadRawLines(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllLines(filePath) : Array.Empty<string>();
    }
}
