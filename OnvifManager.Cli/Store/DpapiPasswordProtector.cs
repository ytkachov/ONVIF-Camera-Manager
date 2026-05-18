using System.Security.Cryptography;
using System.Text;
using OnvifManager.Services;

namespace OnvifManager.Cli.Store;

internal sealed class DpapiPasswordProtector : IPasswordProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OnvifManager.CameraStore.v1");

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        var cipher = Convert.FromBase64String(ciphertext);
        var bytes = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
