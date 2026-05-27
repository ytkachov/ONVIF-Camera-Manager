namespace OnvifManager.Web.Contracts;

public sealed record VideoEncoderConfigDto(
    string Token,
    string Name,
    string Encoding,
    int UseCount,
    int Width,
    int Height,
    int FrameRateLimit,
    int EncodingInterval,
    int BitrateLimit,
    string GovLength,
    string H264Profile,
    bool ConstantBitRate,
    double QualityLevel);

public sealed record VideoEncoderConfigsResponse(
    string MediaVersion,
    IReadOnlyList<VideoEncoderConfigDto> Configurations);
