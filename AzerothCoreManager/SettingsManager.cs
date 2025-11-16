using System;
using System.IO;

namespace AzerothCoreManager
{
    public static class SettingsManager
    {
        private static readonly string SettingsFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AzerothCoreManager", "Settings.txt");

        public static string? LoadBasePath()
        {
            if (!File.Exists(SettingsFile))
                return null;

            var lines = File.ReadAllLines(SettingsFile);
            foreach (var line in lines)
            {
                if (line.StartsWith("BasePath="))
                {
                    var value = line.Substring("BasePath=".Length).Trim();
                    if (Directory.Exists(value))
                        return value; // gültig
                }
            }

            return null; // ungültig / nicht vorhanden
        }

        public static void SaveBasePath(string path)
        {
            var dir = Path.GetDirectoryName(SettingsFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SettingsFile, $"BasePath={path}");
        }
    }
}
