namespace OnvifManager.Models;

public class VideoEncoderConfig
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Encoding { get; set; } = "H264";
    public int UseCount { get; set; }
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int FrameRateLimit { get; set; } = 30;
    public int EncodingInterval { get; set; } = 1;
    public int BitrateLimit { get; set; } = 4096;
    public string GovLength { get; set; } = "30";
    public string H264Profile { get; set; } = "High";
    public VideoQualityType Quality { get; set; } = VideoQualityType.ConstantBitrate;

    // Populated from ONVIF Media2 (ver20) so an H265 config round-trips correctly.
    public bool ConstantBitRate { get; set; }
    public double QualityLevel { get; set; } = 3;
}

public enum VideoQualityType
{
    ConstantBitrate,
    VariableBitrate,
    ConstantQuality
}
