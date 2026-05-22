using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnvifManager.Services;

public enum AppViewMode
{
    Onvif,
    Full
}

public sealed class AppSettings
{
    public string RecordingsPath { get; set; } = DefaultRecordingsPath();
    public bool AutoPlayOnSelect { get; set; }
    public AppViewMode ViewMode { get; set; } = AppViewMode.Onvif;

    public static string DefaultRecordingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "OnvifManager");
}

public sealed partial class AppSettingsService : ObservableObject
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    private readonly string _path;

    [ObservableProperty] private string _recordingsPath = AppSettings.DefaultRecordingsPath();
    [ObservableProperty] private bool _autoPlayOnSelect;
    [ObservableProperty] private AppViewMode _viewMode = AppViewMode.Onvif;

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
            ViewMode = data.ViewMode;
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
                AutoPlayOnSelect = AutoPlayOnSelect,
                ViewMode = ViewMode
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data, Options));
        }
        catch { }
    }
}
