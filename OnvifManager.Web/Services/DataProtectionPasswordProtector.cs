using System.Text;
using Microsoft.AspNetCore.DataProtection;
using OnvifManager.Services;

namespace OnvifManager.Web.Services;

// Replaces the WPF/CLI DpapiPasswordProtector. DPAPI is Windows-only; in the
// web build credentials are sealed via ASP.NET Core Data Protection so the
// store remains portable to Linux/Docker.
public sealed class DataProtectionPasswordProtector : IPasswordProtector
{
    public const string Purpose = "OnvifManager.CameraStore.v1";

    private readonly IDataProtector _protector;

    public DataProtectionPasswordProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return _protector.Protect(Convert.ToBase64String(bytes));
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        var b64 = _protector.Unprotect(ciphertext);
        return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
    }
}
