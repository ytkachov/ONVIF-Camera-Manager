using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Vendors;

public sealed class DahuaVendorAdapter : VendorAdapterBase
{
    private static readonly string[] SupportedManufacturers =
    {
        "Dahua",
        // Consumer/OEM brands on Dahua firmware sharing the same CGI surface.
        "Lechange",
        "Imou",
        "Amcrest"
    };

    public override string Vendor => "Dahua";

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
            var cgi = new DahuaCgiService(client);
            var name = await cgi.GetMachineNameAsync(ct);
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    public override async Task<bool> SetFriendlyNameAsync(OnvifClient client, string name, CancellationToken ct = default)
    {
        var cgi = new DahuaCgiService(client);
        await cgi.SetMachineNameAsync(name, ct);
        return true;
    }
}
