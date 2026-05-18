using System.IO;
using System.Text.Json;
using WinLayout.Models;

namespace WinLayout.Services;

public class ConfigService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLayout");

    private static readonly string ConfigFilePath = Path.Combine(AppDataPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public UserConfig LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
            return new UserConfig();

        var json = File.ReadAllText(ConfigFilePath);
        return JsonSerializer.Deserialize<UserConfig>(json, JsonOptions) ?? new UserConfig();
    }

    public void SaveConfig(UserConfig config)
    {
        Directory.CreateDirectory(AppDataPath);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempPath = ConfigFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigFilePath, overwrite: true);
    }

    public void SaveLayout(LayoutDefinition layout)
    {
        var layoutDir = Path.Combine(AppDataPath, "layouts");
        Directory.CreateDirectory(layoutDir);

        var filePath = Path.Combine(layoutDir, $"{layout.LayoutId}.json");
        var json = JsonSerializer.Serialize(layout, JsonOptions);
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }

    public List<LayoutDefinition> LoadAllLayouts()
    {
        var layoutDir = Path.Combine(AppDataPath, "layouts");
        if (!Directory.Exists(layoutDir))
            return new List<LayoutDefinition>();

        var layouts = new List<LayoutDefinition>();
        foreach (var file in Directory.GetFiles(layoutDir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var layout = JsonSerializer.Deserialize<LayoutDefinition>(json, JsonOptions);
            if (layout != null)
                layouts.Add(layout);
        }
        return layouts;
    }

    public void DeleteLayout(string layoutId)
    {
        var filePath = Path.Combine(AppDataPath, "layouts", $"{layoutId}.json");
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public string GetLayoutsDirectory()
    {
        var path = Path.Combine(AppDataPath, "layouts");
        Directory.CreateDirectory(path);
        return path;
    }
}
