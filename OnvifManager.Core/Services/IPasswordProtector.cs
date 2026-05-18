namespace OnvifManager.Services;

public interface IPasswordProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class PlaintextPasswordProtector : IPasswordProtector
{
    public string Protect(string plaintext) => plaintext ?? string.Empty;
    public string Unprotect(string ciphertext) => ciphertext ?? string.Empty;
}
