using OnvifManager.Models;

namespace OnvifManager.Vendors;

public sealed class VendorRegistry
{
    public static readonly VendorRegistry Empty = new(Array.Empty<IVendorAdapter>());

    private readonly IReadOnlyList<IVendorAdapter> _adapters;

    public VendorRegistry(IEnumerable<IVendorAdapter> adapters) =>
        _adapters = adapters.Where(a => a is not GenericVendorAdapter).ToList();

    public IVendorAdapter For(CameraDevice camera) =>
        _adapters.FirstOrDefault(a => a.Supports(camera)) ?? GenericVendorAdapter.Instance;
}
