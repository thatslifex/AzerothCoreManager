using System.Text.Json;

namespace AzerothCoreManager.Models;

public class AppSettings
{
    public bool FirstRunComplete { get; set; } = false;
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Dark";
    public string SourcePath { get; set; } = @"C:\Azerothcore";
    public string BuildPath { get; set; } = @"C:\Build";
    public string ServerPath { get; set; } = @"C:\AzerothcoreServer";
    public string MySqlHost { get; set; } = "127.0.0.1";
    public int MySqlPort { get; set; } = 3306;
    public string MySqlRootPassword { get; set; } = "";
    public string SoapUsername { get; set; } = "";
    public string SoapPassword { get; set; } = "";
    public bool AutoCheckUpdates { get; set; } = true;
    public string UpdateChannel { get; set; } = "stable";
    public bool AutoRestartOnCrash { get; set; } = false;
    public string BackgroundImage { get; set; } = "background_wow.jpg";
}
