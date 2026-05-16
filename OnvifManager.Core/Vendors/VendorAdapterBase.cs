using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Vendors;

public abstract class VendorAdapterBase : IVendorAdapter
{
    public abstract string Vendor { get; }
    public abstract bool Supports(CameraDevice camera);

    public virtual Task<string?> GetFriendlyNameAsync(OnvifClient client, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public virtual Task<bool> SetFriendlyNameAsync(OnvifClient client, string name, CancellationToken ct = default)
        => Task.FromResult(false);
}
