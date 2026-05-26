namespace OnvifManager.Web.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string DataDirectory { get; set; } = "/data";
    public string KeysDirectory { get; set; } = "/data/keys";
}
