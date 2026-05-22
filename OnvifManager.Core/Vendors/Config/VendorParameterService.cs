using System.Xml.Linq;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Vendors.Config;

public sealed class VendorParameterService
{
    private readonly VendorProfileStore _store;
    private readonly IReadOnlyDictionary<string, IVendorProtocol> _protocols;

    public VendorParameterService(VendorProfileStore store, IEnumerable<IVendorProtocol> protocols)
    {
        _store = store;
        _protocols = protocols.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasProfile(CameraDevice camera) => _store.For(camera) != null;

    public async Task<IReadOnlyList<VendorParameterValue>> ReadAllAsync(
        OnvifClient client, CancellationToken ct = default)
    {
        var profile = _store.For(client.Camera);
        if (profile == null) return Array.Empty<VendorParameterValue>();

        var result = new List<VendorParameterValue>();

        foreach (var group in profile.Parameters.GroupBy(p => (p.Resource.Protocol, p.Resource.Path)))
        {
            if (!_protocols.TryGetValue(group.Key.Protocol, out var proto))
            {
                foreach (var d in group) result.Add(new VendorParameterValue(d));
                continue;
            }

            XDocument? doc = null;
            try
            {
                doc = await proto.ReadAsync(client, group.Key.Path, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            foreach (var desc in group)
            {
                var value = new VendorParameterValue(desc);
                var node = doc?.Root != null ? FindNode(doc.Root, desc.ValuePath) : null;
                if (node != null)
                {
                    value.RawValue = node.Value;
                    value.Available = true;
                }
                value.Snapshot();
                result.Add(value);
            }
        }

        return result;
    }

    public async Task<int> WriteAsync(
        OnvifClient client, IEnumerable<VendorParameterValue> values, CancellationToken ct = default)
    {
        var dirty = values.Where(v => v.IsDirty).ToList();
        if (dirty.Count == 0) return 0;

        var written = 0;
        foreach (var group in dirty.GroupBy(v => (v.Descriptor.Resource.Protocol, v.Descriptor.Resource.Path)))
        {
            if (!_protocols.TryGetValue(group.Key.Protocol, out var proto)) continue;

            var doc = await proto.ReadAsync(client, group.Key.Path, ct);
            if (doc?.Root == null) continue;

            var patched = false;
            foreach (var v in group)
            {
                var node = FindNode(doc.Root, v.Descriptor.ValuePath);
                if (node == null) continue;
                node.Value = v.RawValue ?? "";
                patched = true;
            }
            if (!patched) continue;

            await proto.WriteAsync(client, group.Key.Path, doc, ct);
            foreach (var v in group) { v.MarkClean(); written++; }
        }

        return written;
    }

    private static XElement? FindNode(XElement root, string valuePath)
    {
        var parts = valuePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        XElement? cur = root;
        foreach (var part in parts)
        {
            cur = cur.Elements().FirstOrDefault(e =>
                e.Name.LocalName.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (cur == null) return null;
        }
        return cur;
    }
}
