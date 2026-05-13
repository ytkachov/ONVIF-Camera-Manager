namespace OnvifManager.Services;

public sealed class OnvifClientOptions
{
    public bool AllowSelfSignedCertificates { get; init; } = true;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
