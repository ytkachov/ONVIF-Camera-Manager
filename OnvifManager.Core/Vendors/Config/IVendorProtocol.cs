using System.Xml.Linq;
using OnvifManager.Services;

namespace OnvifManager.Vendors.Config;

// Reads/writes a vendor resource document (e.g. one ISAPI endpoint). Value extraction
// and node patching are done generically by VendorParameterService, so a protocol only
// needs to move whole XML documents to and from the camera.
public interface IVendorProtocol
{
    string Name { get; }
    Task<XDocument?> ReadAsync(OnvifClient client, string path, CancellationToken ct = default);
    Task WriteAsync(OnvifClient client, string path, XDocument doc, CancellationToken ct = default);
}
