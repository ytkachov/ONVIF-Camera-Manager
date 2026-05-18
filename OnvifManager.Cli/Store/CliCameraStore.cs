using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Cli.Store;

internal static class CliCameraStore
{
    private static readonly Lazy<ICameraStore> Instance = new(CreateStore);

    public static string StorePath => Instance.Value.StorePath;

    public static IReadOnlyList<CameraDevice> LoadAll() => Instance.Value.Load();

    public static CameraDevice? TryResolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var cameras = LoadAll();
        return cameras.FirstOrDefault(c =>
            string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase))
            ?? cameras.FirstOrDefault(c =>
            string.Equals(c.IpAddress, key, StringComparison.OrdinalIgnoreCase));
    }

    private static ICameraStore CreateStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "SeaGull", "cameras.json");
        return new JsonCameraStore(path, new DpapiPasswordProtector());
    }
}
