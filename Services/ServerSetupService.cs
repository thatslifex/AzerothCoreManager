using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.IO.Compression;
using MySqlConnector;

namespace AzerothCoreManager.Services;

public class ServerSetupService
{
    public event Action<string>? LogMessage;
    public event Action<int>? ProgressChanged;

    private readonly DatabaseService _db = new();

    public async Task<bool> InitializeDatabaseAsync(string host, int port, string rootPassword, CancellationToken ct = default)
    {
        LogMessage?.Invoke("Initializing MySQL databases...");
        ProgressChanged?.Invoke(96);

        var connString = $"Server={host};Port={port};User ID=root;Password={rootPassword};AllowUserVariables=true";

        try
        {
            await using var conn = new MySqlConnection(connString);
            await conn.OpenAsync(ct);

            var sql = @"
                DROP USER IF EXISTS 'acore'@'localhost';
                CREATE USER 'acore'@'localhost' IDENTIFIED BY 'acore'
                    WITH MAX_QUERIES_PER_HOUR 0 MAX_CONNECTIONS_PER_HOUR 0 MAX_UPDATES_PER_HOUR 0;
                CREATE DATABASE IF NOT EXISTS `acore_world` DEFAULT CHARACTER SET UTF8MB4 COLLATE utf8mb4_unicode_ci;
                CREATE DATABASE IF NOT EXISTS `acore_characters` DEFAULT CHARACTER SET UTF8MB4 COLLATE utf8mb4_unicode_ci;
                CREATE DATABASE IF NOT EXISTS `acore_auth` DEFAULT CHARACTER SET UTF8MB4 COLLATE utf8mb4_unicode_ci;
                GRANT ALL PRIVILEGES ON `acore_world`.* TO 'acore'@'localhost' WITH GRANT OPTION;
                GRANT ALL PRIVILEGES ON `acore_characters`.* TO 'acore'@'localhost' WITH GRANT OPTION;
                GRANT ALL PRIVILEGES ON `acore_auth`.* TO 'acore'@'localhost' WITH GRANT OPTION;
                GRANT PROCESS ON *.* TO 'acore'@'localhost';
                FLUSH PRIVILEGES;
            ";

            await using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);

            LogMessage?.Invoke("Databases created: acore_world, acore_characters, acore_auth");
            LogMessage?.Invoke("User 'acore' created with full privileges.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Database initialization failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CreateSoapAdminAccountAsync(string host, int port, string rootPassword, string soapUser, string soapPass, CancellationToken ct = default)
    {
        LogMessage?.Invoke("Creating SOAP admin account...");

        var connString = $"Server={host};Port={port};User ID=root;Password={rootPassword};Database=acore_auth;AllowUserVariables=true";

        try
        {
            await using var conn = new MySqlConnection(connString);
            await conn.OpenAsync(ct);

            // Compute SRP6
            var salt = Srp6Helper.GenerateSalt();
            var verifier = Srp6Helper.ComputeVerifier(soapUser, soapPass, salt);

            var sql = @"
                INSERT INTO account (username, salt, verifier, email, reg_mail, joindate, expansion)
                VALUES (@user, @salt, @verifier, '', '', NOW(), 2)
                ON DUPLICATE KEY UPDATE salt = @salt, verifier = @verifier;

                INSERT INTO account_access (id, gmlevel, RealmID)
                SELECT id, 3, -1 FROM account WHERE username = @user
                ON DUPLICATE KEY UPDATE gmlevel = 3;
            ";

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@user", soapUser.ToUpper());
            cmd.Parameters.AddWithValue("@salt", salt);
            cmd.Parameters.AddWithValue("@verifier", verifier);
            await cmd.ExecuteNonQueryAsync(ct);

            LogMessage?.Invoke($"SOAP admin account '{soapUser}' created with gmlevel=3.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"SOAP admin creation failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DownloadClientDataAsync(string serverPath, CancellationToken ct = default)
    {
        LogMessage?.Invoke("Downloading client data (~1.1 GB)...");
        ProgressChanged?.Invoke(97);

        var dataDir = Path.Combine(serverPath, "Data");
        Directory.CreateDirectory(dataDir);

        // Check if already exists
        if (Directory.Exists(Path.Combine(dataDir, "dbc")) && Directory.Exists(Path.Combine(dataDir, "maps")))
        {
            LogMessage?.Invoke("Client data already exists — skipping download.");
            ProgressChanged?.Invoke(100);
            return true;
        }

        try
        {
            // Get latest release from wowgaming/client-data
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
            var apiUrl = "https://api.github.com/repos/wowgaming/client-data/releases/latest";
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AzerothCoreManager/1.0");

            var json = await client.GetStringAsync(apiUrl, ct);
            var release = System.Text.Json.JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null || release.Assets.Count == 0)
            {
                LogMessage?.Invoke("No client data release found.");
                return false;
            }

            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip"));
            if (asset == null)
            {
                LogMessage?.Invoke("No ZIP asset found in release.");
                return false;
            }

            LogMessage?.Invoke($"Downloading {asset.Name} ({asset.Size / 1024 / 1024} MB)...");

            var tempZip = Path.GetTempFileName() + ".zip";
            await using (var stream = await client.GetStreamAsync(asset.BrowserDownloadUrl, ct))
            await using (var file = File.Create(tempZip))
            {
                await stream.CopyToAsync(file, ct);
            }

            LogMessage?.Invoke("Extracting client data...");
            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(tempZip);
                foreach (var entry in zip.Entries)
                {
                    // Strip "Data/" prefix if present
                    var relativePath = entry.FullName;
                    if (relativePath.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
                        relativePath = relativePath[5..];

                    if (string.IsNullOrEmpty(relativePath)) continue;

                    var dest = Path.Combine(dataDir, relativePath);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    if (!entry.FullName.EndsWith('/'))
                        entry.ExtractToFile(dest, overwrite: true);
                }
            }, ct);

            File.Delete(tempZip);
            LogMessage?.Invoke("Client data extracted successfully.");
            ProgressChanged?.Invoke(100);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Client data download failed: {ex.Message}");
            return false;
        }
    }

    private record GitHubRelease(
        [property: System.Text.Json.Serialization.JsonPropertyName("tag_name")] string TagName,
        [property: System.Text.Json.Serialization.JsonPropertyName("assets")] List<GitHubAsset> Assets);

    private record GitHubAsset(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("size")] long Size,
        [property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
