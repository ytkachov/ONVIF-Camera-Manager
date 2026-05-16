namespace OnvifManager.Models;

public class CameraProfile
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Fixed { get; set; }
    public string VideoSourceToken { get; set; } = string.Empty;
    public string VideoEncoderToken { get; set; } = string.Empty;
}
