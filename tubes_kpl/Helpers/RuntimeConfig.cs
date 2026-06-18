// Teknik: Runtime Configuration - Laksamana Dwi Daffa
// Konfigurasi sistem yang dapat diubah saat runtime tanpa recompile
// FR-007: Publikasi Bertahap (jadwal publikasi dikonfigurasi runtime)

using System;
using System.Collections.Generic;
using System.Text;

using System.Text.Json;

namespace Tubes_KPL.Helpers;

public class RuntimeConfig
{
    private static RuntimeConfig? _instance;
    private static readonly object _lock = new();
    private Dictionary<string, object> _config = new();
    private readonly string _configPath;
    private FileSystemWatcher? _watcher;

    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public static RuntimeConfig Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new RuntimeConfig();
                return _instance;
            }
        }
    }

    private RuntimeConfig()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDir = Path.Combine(appData, "ManajemenNilai");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");

        LoadConfig();
        SetupFileWatcher();
    }

    // ---- Typed accessors (Runtime Configuration pattern) ----

    public string DatabasePath => Get("database.path",
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "ManajemenNilai", "nilai.db"));

    public string ApiBaseUrl => Get("api.baseUrl", "http://localhost:5000/api");

    public int MaxImportRows => Get("import.maxRows", 100);

    public int ImportTimeoutSeconds => Get("import.timeoutSeconds", 10);

    public bool AutoPublishEnabled => Get("publish.autoEnabled", false);

    public string AutoPublishSchedule => Get("publish.schedule", "weekly"); // daily, weekly, manual

    public int MaxFailedLoginAttempts => Get("auth.maxFailedAttempts", 5);

    public int LockDurationMinutes => Get("auth.lockDurationMinutes", 15);

    public bool NotificationsEnabled => Get("notifications.enabled", true);

    public string AppTheme => Get("ui.theme", "Default");

    public string InstitutionName => Get("institution.name", "Telkom University");

    public string CurrentSemester => Get("institution.currentSemester", "Ganjil 2024/2025");

    public double NilaiMinimumLulus => Get("grading.minimumPass", 55.0);

    // ---- Runtime update (dapat diubah saat aplikasi berjalan) ----

    public void Set(string key, object value)
    {
        _config[key] = value;
        SaveConfig();
        ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(key, value));
    }

    public T Get<T>(string key, T defaultValue)
    {
        if (_config.TryGetValue(key, out var val))
        {
            try
            {
                if (val is JsonElement je)
                {
                    if (typeof(T) == typeof(string)) return (T)(object)je.GetString()!;
                    if (typeof(T) == typeof(int)) return (T)(object)je.GetInt32();
                    if (typeof(T) == typeof(double)) return (T)(object)je.GetDouble();
                    if (typeof(T) == typeof(bool)) return (T)(object)je.GetBoolean();
                }
                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch { }
        }
        return defaultValue;
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            CreateDefaultConfig();
            return;
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            _config = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                      ?? new Dictionary<string, object>();
        }
        catch
        {
            CreateDefaultConfig();
        }
    }

    private void CreateDefaultConfig()
    {
        _config = new Dictionary<string, object>
        {
            ["database.path"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ManajemenNilai", "nilai.db"),
            ["api.baseUrl"] = "http://localhost:5000/api",
            ["import.maxRows"] = 100,
            ["import.timeoutSeconds"] = 10,
            ["publish.autoEnabled"] = false,
            ["publish.schedule"] = "weekly",
            ["auth.maxFailedAttempts"] = 5,
            ["auth.lockDurationMinutes"] = 15,
            ["notifications.enabled"] = true,
            ["ui.theme"] = "Default",
            ["institution.name"] = "Telkom University",
            ["institution.currentSemester"] = "Ganjil 2024/2025",
            ["grading.minimumPass"] = 55.0,
        };
        SaveConfig();
    }

    private void SaveConfig()
    {
        try
        {
            string json = JsonSerializer.Serialize(_config,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { /* non-critical */ }
    }

    private void SetupFileWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_configPath)!)
            {
                Filter = Path.GetFileName(_configPath),
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, _) =>
            {
                Thread.Sleep(100); // debounce
                LoadConfig();
                ConfigChanged?.Invoke(this, new ConfigChangedEventArgs("*", null!));
            };
        }
        catch { /* watcher is non-critical */ }
    }
}

public class ConfigChangedEventArgs : EventArgs
{
    public string Key { get; }
    public object Value { get; }
    public ConfigChangedEventArgs(string key, object value) { Key = key; Value = value; }
}
