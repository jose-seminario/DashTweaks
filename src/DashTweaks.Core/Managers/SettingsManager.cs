using System.Text.Json;

namespace DashTweaks.Core.Managers;

public class DashTweaksSettings
{
    public List<string> GameModeWhitelist { get; set; } = new() { "discord.exe" };
}

public class SettingsManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DashTweaks"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    public DashTweaksSettings LoadSettings()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<DashTweaksSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // En caso de corrupción o lectura fallida, devolver configuración predeterminada
        }

        var defaultSettings = new DashTweaksSettings();
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    public bool SaveSettings(DashTweaksSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
