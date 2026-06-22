using System.Diagnostics;
using System.IO;

namespace AzerothCoreManager.Services;

public static class WikiLinkService
{
    private const string BaseUrl = "https://www.azerothcore.org/wiki/";

    private static readonly Dictionary<string, string> PageLinks = new()
    {
        ["dashboard"] = "final-server-steps",
        ["setup"] = "core-installation",
        ["server_control"] = "server-setup",
        ["gm_console"] = "gm-commands",
        ["module_manager"] = "installing-a-module",
        ["database"] = "database-installation",
        ["accounts"] = "account",
        ["settings"] = "how-to-work-with-conf-files",
        ["config_editor"] = "how-to-work-with-conf-files",
        ["backup_manager"] = "database-installation",
        ["log_viewer"] = "server-setup",
    };

    public static void OpenHelp(string pageKey)
    {
        if (PageLinks.TryGetValue(pageKey, out var page))
        {
            Process.Start(new ProcessStartInfo(BaseUrl + page) { UseShellExecute = true });
        }
    }
}
