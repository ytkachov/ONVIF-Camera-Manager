namespace OnvifManager.Models;

public class NetworkInterfaceInfo
{
    public string Token { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public bool IPv4Enabled { get; set; } = true;
    public bool IPv4Dhcp { get; set; } = true;
    public string IPv4Address { get; set; } = string.Empty;
    public int IPv4PrefixLength { get; set; } = 24;
    public string IPv4Gateway { get; set; } = string.Empty;

    public string IPv6Address { get; set; } = string.Empty;
    public int IPv6PrefixLength { get; set; } = 64;

    public int Mtu { get; set; } = 1500;
    public string HwAddress { get; set; } = string.Empty;

    public List<string> DnsServers { get; set; } = new();

    public string Display
    {
        get
        {
            var label = string.IsNullOrEmpty(Token) ? "(unnamed)" : Token;
            if (!string.IsNullOrEmpty(IPv4Address))
                label += $"  —  {IPv4Address}/{IPv4PrefixLength}";
            if (!string.IsNullOrEmpty(HwAddress))
                label += $"  ({HwAddress})";
            return label;
        }
    }
}
