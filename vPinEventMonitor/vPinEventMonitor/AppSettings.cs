using System.Text.Json;

namespace vPinEventMonitor;

public class AppSettings
{
    public string GameName   { get; set; } = "ft_l5";
    public string PipeName   { get; set; } = @"\\.\pipe\PinballEvents";
    public bool   AutoStart  { get; set; } = false;
    public string B2sFilePath { get; set; } = "";

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(string path)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(this, options));
        }
        catch { }
    }
}
