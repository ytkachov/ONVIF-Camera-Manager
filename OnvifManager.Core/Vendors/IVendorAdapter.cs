using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Vendors;

public interface IVendorAdapter
{
    string Vendor { get; }
    bool Supports(CameraDevice camera);

    Task<string?> GetFriendlyNameAsync(OnvifClient client, CancellationToken ct = default);
    Task<bool> SetFriendlyNameAsync(OnvifClient client, string name, CancellationToken ct = default);
}
