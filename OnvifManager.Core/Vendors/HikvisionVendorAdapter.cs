using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Vendors;

public sealed class HikvisionVendorAdapter : VendorAdapterBase
{
    public override string Vendor => "HIKVISION";

    public override bool Supports(CameraDevice camera) =>
        !string.IsNullOrEmpty(camera?.Manufacturer)
        && camera!.Manufacturer.Contains("HIKVISION", StringComparison.OrdinalIgnoreCase);

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
