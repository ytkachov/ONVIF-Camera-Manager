namespace OnvifManager.Vendors.Config;

public enum VendorParameterType
{
    Bool,
    Enum,
    Int,
    String
}

public sealed class VendorProfile
{
    public string Vendor { get; set; } = "";
    public VendorMatch Match { get; set; } = new();
    public List<VendorParameterDescriptor> Parameters { get; set; } = new();
}

public sealed class VendorMatch
{
    public List<string> ManufacturerContains { get; set; } = new();
}

public sealed class VendorResource
{
    public string Protocol { get; set; } = "isapi";
    public string Path { get; set; } = "";
}

public sealed class VendorEnumOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class VendorParameterDescriptor
{
    public string Id { get; set; } = "";

    // Maps to ParamTab (Info/Video/Network/Ptz/Events) — where the section is shown.
    public string Tab { get; set; } = "Video";
    public string Section { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Description { get; set; }
    public VendorParameterType Type { get; set; } = VendorParameterType.String;

    public VendorResource Resource { get; set; } = new();

    // Slash-separated chain of element local-names relative to the resource document root.
    public string ValuePath { get; set; } = "";

    public List<VendorEnumOption>? Options { get; set; }
    public int Min { get; set; }
    public int Max { get; set; } = 100;
    public int Step { get; set; } = 1;
    public string TrueValue { get; set; } = "true";
    public string FalseValue { get; set; } = "false";
}
