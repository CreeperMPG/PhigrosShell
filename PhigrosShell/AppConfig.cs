using System.Collections;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PhigrosShell;

internal class AppConfig : IEnumerable<KeyValuePair<string, string>>
{
    private readonly string _configFilePath;
    private Dictionary<string, string> _settings = new();

    public string? this[string key]
    {
        get => _settings.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value == null) _settings.Remove(key);
            else _settings[key] = value;
        }
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _settings.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _settings.GetEnumerator();

    public AppConfig()
    {
        _configFilePath = GetConfigFilePath();
        Load();
    }

    public void Load()
    {
        if (File.Exists(_configFilePath))
        {
            try
            {
                string json = File.ReadAllText(_configFilePath);
                _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
                return;
            }
            catch { }
        }
        _settings = new Dictionary<string, string>();
    }

    public void Save()
    {
        string? dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string contents = JsonSerializer.Serialize(_settings,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configFilePath, contents);
    }

    public static string GetConfigFilePath()
    {
        string basePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhiShell")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "PhiShell")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "PhiShell");

        return Path.Combine(basePath, "config.json");
    }
}
