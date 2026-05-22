namespace OnvifManager.Vendors.Config;

// Runtime state of one vendor parameter: its descriptor plus the value currently read
// from (or about to be written to) the camera. Dirty tracking drives selective writes.
public sealed class VendorParameterValue
{
    public VendorParameterDescriptor Descriptor { get; }

    // True when the resource was readable and the value node was present — parameters
    // the firmware reports as notSupport stay Available=false and are hidden in the UI.
    public bool Available { get; set; }

    public string? RawValue { get; set; }

    private string? _original;

    public VendorParameterValue(VendorParameterDescriptor descriptor) => Descriptor = descriptor;

    public void Snapshot() => _original = RawValue;
    public void MarkClean() => _original = RawValue;

    public bool IsDirty => Available && !string.Equals(RawValue, _original, StringComparison.Ordinal);
}
