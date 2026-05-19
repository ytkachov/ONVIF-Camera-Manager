using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnvifManager.Services;

public sealed class AppSettings
{
    public string RecordingsPath { get; set; } = DefaultRecordingsPath();
    public bool AutoPlayOnSelect { get; set; }

    public static string DefaultRecordingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "OnvifManager");
}

public sealed partial class AppSettingsService : ObservableObject
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    [ObservableProperty] private string _recordingsPath = AppSettings.DefaultRecordingsPath();
    [ObservableProperty] private bool _autoPlayOnSelect;

    public AppSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _path = Path.Combine(appData, "SeaGull", "settings.json");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (data == null) return;
            RecordingsPath = string.IsNullOrWhiteSpace(data.RecordingsPath)
                ? AppSettings.DefaultRecordingsPath()
                : data.RecordingsPath;
            AutoPlayOnSelect = data.AutoPlayOnSelect;
        }
        catch { }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var data = new AppSettings
            {
                RecordingsPath = RecordingsPath,
                AutoPlayOnSelect = AutoPlayOnSelect
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data, Options));
        }
        catch { }
    }
}
