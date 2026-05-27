namespace OnvifManager.Web.Contracts;

public sealed record CameraProfileDto(
    string Token,
    string Name,
    bool Fixed,
    string VideoSourceToken,
    string VideoEncoderToken);
