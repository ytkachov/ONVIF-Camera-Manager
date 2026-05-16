namespace OnvifManager.Models;

public class DnsInfo
{
    public bool FromDhcp { get; set; }
    public List<string> Manual { get; set; } = new();
    public List<string> FromDhcpServers { get; set; } = new();

    public IEnumerable<string> Effective => FromDhcp ? FromDhcpServers : Manual;
}

public class NtpInfo
{
    public bool FromDhcp { get; set; }
    public List<string> Manual { get; set; } = new();
    public List<string> FromDhcpServers { get; set; } = new();

    public IEnumerable<string> Effective => FromDhcp ? FromDhcpServers : Manual;
}

public class SystemDateTimeInfo
{
    public string SyncSource { get; set; } = string.Empty;
    public bool DaylightSavings { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public DateTime? Utc { get; set; }
    public DateTime? Local { get; set; }
}
