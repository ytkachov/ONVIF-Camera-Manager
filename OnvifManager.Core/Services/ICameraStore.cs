using OnvifManager.Models;

namespace OnvifManager.Services;

public interface ICameraStore
{
    IReadOnlyList<CameraDevice> Load();
    Task SaveAsync(IEnumerable<CameraDevice> cameras, CancellationToken ct = default);
    string StorePath { get; }
}
