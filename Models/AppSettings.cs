using System;
using System.IO;
using System.Text.Json;

namespace CFanControl.Models
{
    public class AppSettings
    {
        public bool AutoStart { get; set; } = false;
        public bool MinimizeOnExit { get; set; } = false;

        private static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FanControl", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                return new AppSettings();
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception)
            {
            }
        }
    }
}