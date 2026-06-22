using System.Net.Http;
using System.IO;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzerothCoreManager.Services;

public class UpdateService
{
    private readonly HttpClient _http = new();

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AzerothCoreManager/1.0");
    }

    public async Task<(string Version, string DownloadUrl, long Size)?> CheckForUpdateAsync(
        string owner, string repo, string currentVersion, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            var json = await _http.GetStringAsync(url, ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null) return null;

            var remoteVersion = release.TagName.TrimStart('v');
            if (!IsNewer(remoteVersion, currentVersion)) return null;

            // Find the installer asset
            var assetName = $"AzerothCoreManager-Setup-{remoteVersion}.exe";
            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));

            if (asset == null) return null;

            return (remoteVersion, asset.BrowserDownloadUrl, asset.Size);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> DownloadInstallerAsync(string url, IProgress<long>? progress, CancellationToken ct = default)
    {
        var tempDir = Path.GetTempPath();
        var dest = Path.Combine(tempDir, $"AzerothCoreManager-Setup-Update.exe");

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(dest);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            totalRead += read;
            progress?.Report(totalRead);
        }

        return dest;
    }

    public void RunInstaller(string installerPath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        System.Diagnostics.Process.Start(psi);
    }

    private static bool IsNewer(string remote, string current)
    {
        try
        {
            var r = new Version(remote);
            var c = new Version(current);
            return r > c;
        }
        catch { return false; }
    }

    private record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);

    private record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
