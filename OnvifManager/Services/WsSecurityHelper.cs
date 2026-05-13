using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace OnvifManager.Services;

public static class WsSecurityHelper
{
    public static string GenerateNonce()
    {
        var bytes = new byte[24];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string GenerateCreated() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    public static string ComputePasswordDigest(string nonce, string created, string password)
    {
        var nonceBytes = Convert.FromBase64String(nonce);
        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        var combined = new byte[nonceBytes.Length + createdBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(nonceBytes, 0, combined, 0, nonceBytes.Length);
        Buffer.BlockCopy(createdBytes, 0, combined, nonceBytes.Length, createdBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, combined, nonceBytes.Length + createdBytes.Length, passwordBytes.Length);

        var hash = SHA1.HashData(combined);
        return Convert.ToBase64String(hash);
    }

    public static XElement BuildSecurityElement(string username, string password)
    {
        var nonce = GenerateNonce();
        var created = GenerateCreated();
        var digest = ComputePasswordDigest(nonce, created, password);

        return new XElement(OnvifXml.WsseNs + "Security",
            new XAttribute(OnvifXml.S + "mustUnderstand", "true"),
            new XElement(OnvifXml.WsseNs + "UsernameToken",
                new XElement(OnvifXml.WsseNs + "Username", username),
                new XElement(OnvifXml.WsseNs + "Password",
                    new XAttribute("Type",
                        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"),
                    digest),
                new XElement(OnvifXml.WsseNs + "Nonce",
                    new XAttribute("EncodingType",
                        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"),
                    nonce),
                new XElement(OnvifXml.WsuNs + "Created", created)));
    }
}
