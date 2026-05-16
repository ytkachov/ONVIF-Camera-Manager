using OnvifManager.Models;

namespace OnvifManager.Vendors;

public sealed class GenericVendorAdapter : VendorAdapterBase
{
    public static readonly GenericVendorAdapter Instance = new();

    public override string Vendor => "Generic";
    public override bool Supports(CameraDevice camera) => true;
}
