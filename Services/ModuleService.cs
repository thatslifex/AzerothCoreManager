using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace AzerothCoreManager.Services;

public class ModuleService
{
    private readonly HttpClient _http = new();
    public event Action<string>? LogMessage;

    public ModuleService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AzerothCoreManager/1.0");
    }

    /// <summary>
    /// Searches GitHub for AzerothCore modules (topic:azerothcore-module).
    /// Returns list of module info.
    /// </summary>
    public async Task<List<ModuleInfo>> SearchModulesAsync(string query = "", CancellationToken ct = default)
    {
        var results = new List<ModuleInfo>();
        try
        {
            var q = string.IsNullOrEmpty(query)
                ? "topic:azerothcore-module"
                : $"topic:azerothcore-module+{Uri.EscapeDataString(query)}";

            var url = $"https://api.github.com/search/repositories?q={q}&sort=stars&per_page=30";
            var json = await _http.GetStringAsync(url, ct);
            var search = JsonSerializer.Deserialize<GitHubSearchResult>(json);

            if (search?.Items == null) return results;

            foreach (var repo in search.Items)
            {
                results.Add(new ModuleInfo
                {
                    Name = repo.Name,
                    FullName = repo.FullName,
                    Description = repo.Description ?? "",
                    Stars = repo.StargazersCount,
                    UpdatedAt = repo.UpdatedAt,
                    CloneUrl = repo.CloneUrl,
                    HtmlUrl = repo.HtmlUrl,
                    DefaultBranch = repo.DefaultBranch
                });
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Module search failed: {ex.Message}");
        }
        return results;
    }

    /// <summary>
    /// Lists locally installed modules in the AzerothCore modules/ directory.
    /// </summary>
    public List<InstalledModule> GetInstalledModules(string sourcePath)
    {
        var modules = new List<InstalledModule>();
        var modulesDir = Path.Combine(sourcePath, "modules");
        if (!Directory.Exists(modulesDir)) return modules;

        foreach (var dir in Directory.GetDirectories(modulesDir))
        {
            var name = Path.GetFileName(dir);
            var hasConf = File.Exists(Path.Combine(dir, "conf", $"{name}.conf.dist"));
            var hasCmake = File.Exists(Path.Combine(dir, "CMakeLists.txt"));

            modules.Add(new InstalledModule
            {
                Name = name,
                Path = dir,
                HasConfig = hasConf,
                HasCmake = hasCmake,
                IsActive = hasCmake
            });
        }
        return modules;
    }

    /// <summary>
    /// Clones a module into the AzerothCore modules/ directory.
    /// </summary>
    public async Task<bool> InstallModuleAsync(string cloneUrl, string sourcePath, string branch = "master", CancellationToken ct = default)
    {
        var modulesDir = Path.Combine(sourcePath, "modules");
        Directory.CreateDirectory(modulesDir);

        var name = cloneUrl.Split('/').Last().Replace(".git", "");
        var targetPath = Path.Combine(modulesDir, name);

        if (Directory.Exists(targetPath))
        {
            LogMessage?.Invoke($"Module '{name}' already exists. Pulling latest instead.");
            return await PullModuleAsync(targetPath, branch, ct);
        }

        LogMessage?.Invoke($"Cloning module '{name}'...");

        var psi = new ProcessStartInfo("git", $"clone --branch {branch} --single-branch {cloneUrl} \"{targetPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
            if (ok) LogMessage?.Invoke($"Module '{name}' installed successfully.");
            else LogMessage?.Invoke($"Module clone failed with exit code {p.ExitCode}.");
            return ok;
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(); } catch { }
            LogMessage?.Invoke("Module install timed out.");
            return false;
        }
    }

    /// <summary>
    /// Pulls latest changes for an installed module.
    /// </summary>
    public async Task<bool> PullModuleAsync(string modulePath, string branch = "master", CancellationToken ct = default)
    {
        var name = Path.GetFileName(modulePath);
        LogMessage?.Invoke($"Pulling latest for '{name}'...");

        var psi = new ProcessStartInfo("git", $"pull origin {branch}")
        {
            WorkingDirectory = modulePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p == null) return false;

        p.OutputDataReceived += (s, e) => { if (e.Data != null) LogMessage?.Invoke(e.Data); };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) LogMessage?.Invoke(e.Data); };
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
            return false;
        }
    }

    /// <summary>
    /// Removes a module from the modules/ directory.
    /// Handles read-only .git objects.
    /// </summary>
    public bool RemoveModule(string modulePath)
    {
        var name = Path.GetFileName(modulePath);
        if (!Directory.Exists(modulePath))
        {
            LogMessage?.Invoke($"Module '{name}' not found.");
            return false;
        }

        try
        {
            foreach (var file in Directory.GetFiles(modulePath, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(modulePath, true);
            LogMessage?.Invoke($"Module '{name}' removed.");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Failed to remove module '{name}': {ex.Message}");
            LogMessage?.Invoke($"You may need to delete it manually: {modulePath}");
            return false;
        }
    }

    public class ModuleInfo
    {
        public string Name { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Description { get; init; } = "";
        public int Stars { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string CloneUrl { get; init; } = "";
        public string HtmlUrl { get; init; } = "";
        public string DefaultBranch { get; init; } = "master";
    }

    public class InstalledModule
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool HasConfig { get; init; }
        public bool HasCmake { get; init; }
        public bool IsActive { get; init; }
    }

    private record GitHubSearchResult(
        [property: JsonPropertyName("items")] List<GitHubRepo> Items);

    private record GitHubRepo(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("full_name")] string FullName,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("stargazers_count")] int StargazersCount,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
        [property: JsonPropertyName("clone_url")] string CloneUrl,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("default_branch")] string DefaultBranch);
}
