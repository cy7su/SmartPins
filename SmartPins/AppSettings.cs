using Newtonsoft.Json;
using System.IO;

namespace SmartPins
{
    public class AppSettings
    {
        public string Hotkey { get; set; } = "Ctrl+Alt+P";
        public bool HighlightOnlyPinned { get; set; } = false;
        public bool ShowPinIcon { get; set; } = false;
        public List<string> BlacklistPatterns { get; set; } = new();

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartPins", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }
    }
}
