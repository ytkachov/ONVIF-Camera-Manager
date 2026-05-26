namespace OnvifManager.Web.Configuration;

public sealed class MediaMtxOptions
{
    public const string SectionName = "MediaMtx";

    public string WhepBaseUrl { get; set; } = "http://localhost:8889";
    public string RtspBaseUrl { get; set; } = "rtsp://localhost:8554";
    public string ControlApiUrl { get; set; } = "http://localhost:9997";
}
