using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Vendors;

public sealed class HikvisionVendorAdapter : VendorAdapterBase
{
    private static readonly string[] SupportedManufacturers =
    {
        "HIKVISION",
        // HiWatch is a Hikvision sub-brand sharing the same firmware and ISAPI surface.
        "HiWatch"
    };

    public override string Vendor => "HIKVISION";

    public override bool Supports(CameraDevice camera)
    {
        var m = camera?.Manufacturer;
        if (string.IsNullOrEmpty(m)) return false;
        foreach (var s in SupportedManufacturers)
            if (m!.Contains(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public override async Task<string?> GetFriendlyNameAsync(OnvifClient client, CancellationToken ct = default)
    {
        try
        {
            var isapi = new HikvisionIsapiService(client);
            var name = await isapi.GetDeviceNameAsync(ct);
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    public override async Task<bool> SetFriendlyNameAsync(OnvifClient client, string name, CancellationToken ct = default)
    {
        var isapi = new HikvisionIsapiService(client);
        await isapi.SetDeviceNameAsync(name, ct);
        return true;
    }
}
