using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OnvifManager.Models;

namespace OnvifManager.Vendors.Config;

public sealed class VendorProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly List<VendorProfile> _profiles = new();

    public IReadOnlyList<VendorProfile> Profiles => _profiles;

    public VendorProfileStore(IEnumerable<string> directories)
    {
        // Later directories override earlier ones by vendor name, so callers should
        // pass bundled defaults first and user overrides last.
        foreach (var dir in directories)
            LoadDirectory(dir);
    }

    private void LoadDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            VendorProfile? profile;
            try
            {
                var json = File.ReadAllText(file);
                profile = JsonSerializer.Deserialize<VendorProfile>(json, Options);
            }
            catch
            {
                continue;
            }

            if (profile == null || string.IsNullOrWhiteSpace(profile.Vendor)) continue;

            var existing = _profiles.FindIndex(p =>
                p.Vendor.Equals(profile.Vendor, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) _profiles[existing] = profile;
            else _profiles.Add(profile);
        }
    }

    public VendorProfile? For(CameraDevice camera)
    {
        var m = camera?.Manufacturer;
        if (string.IsNullOrEmpty(m)) return null;

        return _profiles.FirstOrDefault(p =>
            p.Match.ManufacturerContains.Any(token =>
                m!.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }
}
