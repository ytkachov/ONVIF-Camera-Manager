using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class MediaService
{
    private readonly OnvifClient _client;

    public MediaService(OnvifClient client) => _client = client;

    public async Task<List<CameraProfile>> GetProfilesAsync(CancellationToken ct = default)
    {
        var profiles = new List<CameraProfile>();
        var body = new XElement(OnvifXml.Ttrt + "GetProfiles");

        var doc = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetProfiles", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetProfilesResponse");
        if (response == null) return profiles;

        foreach (var p in response.Elements().Where(e => e.Name.LocalName == "Profiles"))
        {
            var profile = new CameraProfile
            {
                Token = p.Attribute("token")?.Value ?? "",
                Name = LocalValue(p, "Name"),
                Fixed = ParseBool(p.Attribute("fixed")?.Value, false)
            };

            var vsConfig = p.Elements().FirstOrDefault(e => e.Name.LocalName == "VideoSourceConfiguration");
            if (vsConfig != null)
                profile.VideoSourceToken = LocalValue(vsConfig, "SourceToken");

            var veConfig = p.Elements().FirstOrDefault(e => e.Name.LocalName == "VideoEncoderConfiguration");
            if (veConfig != null)
                profile.VideoEncoderToken = veConfig.Attribute("token")?.Value ?? "";

            profiles.Add(profile);
        }

        _client.Camera.Profiles = profiles;
        return profiles;
    }

    public async Task<List<VideoEncoderConfig>> GetAllVideoEncoderConfigurationsAsync(CancellationToken ct = default)
    {
        var configs = new List<VideoEncoderConfig>();
        var body = new XElement(OnvifXml.Ttrt + "GetVideoEncoderConfigurations");

        var doc = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetVideoEncoderConfigurations", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetVideoEncoderConfigurationsResponse");
        if (response == null) return configs;

        foreach (var cfg in response.Elements().Where(e => e.Name.LocalName == "Configurations"))
            configs.Add(ReadEncoderConfig(cfg));

        return configs;
    }

    // ONVIF Media2 (ver20) — needed because Media1's Encoding enum has no H265: ver10
    // reports H264 for an H265 stream, and its Set rejects H265. Media2 carries the real
    // codec (and GovLength/Profile/RateControl as on the wire), so when it's available we
    // read and write encoder configs through it instead.
    public async Task<List<VideoEncoderConfig>> GetVideoEncoderConfigurations2Async(CancellationToken ct = default)
    {
        var configs = new List<VideoEncoderConfig>();
        var body = new XElement(OnvifXml.Ttr2 + "GetVideoEncoderConfigurations");

        var doc = await _client.SendSoapAsync(OnvifXml.Media2ServicePath,
            "http://www.onvif.org/ver20/media/wsdl/GetVideoEncoderConfigurations", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetVideoEncoderConfigurationsResponse");
        if (response == null) return configs;

        foreach (var cfg in response.Elements().Where(e => e.Name.LocalName == "Configurations"))
            configs.Add(ReadEncoderConfig2(cfg));

        return configs;
    }

    public async Task SetVideoEncoderConfiguration2Async(VideoEncoderConfig config, CancellationToken ct = default)
    {
        var configuration = new XElement(OnvifXml.Ttr2 + "Configuration",
            new XAttribute("token", config.Token),
            new XElement(OnvifXml.Tt + "Name", config.Name),
            new XElement(OnvifXml.Tt + "UseCount", config.UseCount),
            new XElement(OnvifXml.Tt + "Encoding", config.Encoding),
            new XElement(OnvifXml.Tt + "Resolution",
                new XElement(OnvifXml.Tt + "Width", config.Width),
                new XElement(OnvifXml.Tt + "Height", config.Height)),
            new XElement(OnvifXml.Tt + "RateControl",
                new XAttribute("ConstantBitRate", config.ConstantBitRate ? "true" : "false"),
                new XElement(OnvifXml.Tt + "FrameRateLimit", config.FrameRateLimit),
                new XElement(OnvifXml.Tt + "BitrateLimit", config.BitrateLimit)),
            new XElement(OnvifXml.Tt + "Quality", config.QualityLevel));

        if (int.TryParse(config.GovLength, out var gov))
            configuration.SetAttributeValue("GovLength", gov);
        if (!string.IsNullOrEmpty(config.H264Profile))
            configuration.SetAttributeValue("Profile", config.H264Profile);

        var bodyEl = new XElement(OnvifXml.Ttr2 + "SetVideoEncoderConfiguration", configuration);

        await _client.SendSoapAsync(OnvifXml.Media2ServicePath,
            "http://www.onvif.org/ver20/media/wsdl/SetVideoEncoderConfiguration", bodyEl, ct);
    }

    private static VideoEncoderConfig ReadEncoderConfig2(XElement cfg)
    {
        var config = new VideoEncoderConfig
        {
            Token = cfg.Attribute("token")?.Value ?? "",
            Name = LocalValue(cfg, "Name"),
            Encoding = string.IsNullOrEmpty(LocalValue(cfg, "Encoding")) ? "H264" : LocalValue(cfg, "Encoding"),
            UseCount = ParseInt(LocalValue(cfg, "UseCount"), 0),
            GovLength = string.IsNullOrEmpty(cfg.Attribute("GovLength")?.Value) ? "30" : cfg.Attribute("GovLength")!.Value,
            H264Profile = cfg.Attribute("Profile")?.Value ?? ""
        };

        var resolution = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "Resolution");
        if (resolution != null)
        {
            config.Width = ParseInt(LocalValue(resolution, "Width"), 1920);
            config.Height = ParseInt(LocalValue(resolution, "Height"), 1080);
        }

        var rateControl = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "RateControl");
        if (rateControl != null)
        {
            config.ConstantBitRate = bool.TryParse(rateControl.Attribute("ConstantBitRate")?.Value, out var cbr) && cbr;
            config.FrameRateLimit = (int)Math.Round(ParseDouble(LocalValue(rateControl, "FrameRateLimit"), 30));
            config.BitrateLimit = ParseInt(LocalValue(rateControl, "BitrateLimit"), 4096);
        }

        config.QualityLevel = ParseDouble(LocalValue(cfg, "Quality"), 3);
        return config;
    }

    public async Task SetVideoEncoderConfigurationAsync(VideoEncoderConfig config, CancellationToken ct = default)
    {
        var quality = config.Quality switch
        {
            VideoQualityType.ConstantBitrate => "CBR",
            VideoQualityType.VariableBitrate => "VBR",
            VideoQualityType.ConstantQuality => "CQ",
            _ => "CBR"
        };

        var body = new XElement(OnvifXml.Ttrt + "SetVideoEncoderConfiguration",
            new XElement(OnvifXml.Ttrt + "Configuration",
                new XAttribute("token", config.Token),
                new XElement(OnvifXml.Tt + "Name", config.Name),
                new XElement(OnvifXml.Tt + "UseCount", config.UseCount),
                new XElement(OnvifXml.Tt + "Encoding", config.Encoding),
                new XElement(OnvifXml.Tt + "Resolution",
                    new XElement(OnvifXml.Tt + "Width", config.Width),
                    new XElement(OnvifXml.Tt + "Height", config.Height)),
                new XElement(OnvifXml.Tt + "Quality", quality),
                new XElement(OnvifXml.Tt + "RateControl",
                    new XElement(OnvifXml.Tt + "FrameRateLimit", config.FrameRateLimit),
                    new XElement(OnvifXml.Tt + "EncodingInterval", config.EncodingInterval),
                    new XElement(OnvifXml.Tt + "BitrateLimit", config.BitrateLimit)),
                new XElement(OnvifXml.Tt + "H264",
                    new XElement(OnvifXml.Tt + "GovLength", config.GovLength),
                    new XElement(OnvifXml.Tt + "H264Profile", config.H264Profile)),
                new XElement(OnvifXml.Tt + "Multicast",
                    new XElement(OnvifXml.Tt + "Address",
                        new XElement(OnvifXml.Tt + "Type", "IPv4"),
                        new XElement(OnvifXml.Tt + "IPv4Address", "0.0.0.0")),
                    new XElement(OnvifXml.Tt + "Port", "0"),
                    new XElement(OnvifXml.Tt + "TTL", "0"),
                    new XElement(OnvifXml.Tt + "AutoStart", "false")),
                new XElement(OnvifXml.Tt + "SessionTimeout", "PT0S")),
            new XElement(OnvifXml.Ttrt + "ForcePersistence", "true"));

        await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/SetVideoEncoderConfiguration", body, ct);
    }

    public async Task<string> GetStreamUriAsync(string profileToken, string protocol = "RTSP",
        CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttrt + "GetStreamUri",
            new XElement(OnvifXml.Ttrt + "StreamSetup",
                new XElement(OnvifXml.Tt + "Stream", "RTP-Unicast"),
                new XElement(OnvifXml.Tt + "Transport",
                    new XElement(OnvifXml.Tt + "Protocol", protocol))),
            new XElement(OnvifXml.Ttrt + "ProfileToken", profileToken));

        var doc = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetStreamUri", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetStreamUriResponse");
        if (response == null) return "";

        var mediaUri = response.Elements().FirstOrDefault(e => e.Name.LocalName == "MediaUri");
        return mediaUri?.Elements().FirstOrDefault(e => e.Name.LocalName == "Uri")?.Value ?? "";
    }

    public async Task<string> GetSnapshotUriAsync(string profileToken, CancellationToken ct = default)
    {
        var body = new XElement(OnvifXml.Ttrt + "GetSnapshotUri",
            new XElement(OnvifXml.Ttrt + "ProfileToken", profileToken));

        var doc = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetSnapshotUri", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetSnapshotUriResponse");
        if (response == null) return "";

        var mediaUri = response.Elements().FirstOrDefault(e => e.Name.LocalName == "MediaUri");
        return mediaUri?.Elements().FirstOrDefault(e => e.Name.LocalName == "Uri")?.Value ?? "";
    }

    private static VideoEncoderConfig ReadEncoderConfig(XElement cfg)
    {
        var config = new VideoEncoderConfig
        {
            Token = cfg.Attribute("token")?.Value ?? "",
            Name = LocalValue(cfg, "Name"),
            Encoding = string.IsNullOrEmpty(LocalValue(cfg, "Encoding")) ? "H264" : LocalValue(cfg, "Encoding"),
            UseCount = ParseInt(LocalValue(cfg, "UseCount"), 0)
        };

        var resolution = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "Resolution");
        if (resolution != null)
        {
            config.Width = ParseInt(LocalValue(resolution, "Width"), 1920);
            config.Height = ParseInt(LocalValue(resolution, "Height"), 1080);
        }

        var rateControl = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "RateControl");
        if (rateControl != null)
        {
            config.FrameRateLimit = ParseInt(LocalValue(rateControl, "FrameRateLimit"), 30);
            config.EncodingInterval = ParseInt(LocalValue(rateControl, "EncodingInterval"), 1);
            config.BitrateLimit = ParseInt(LocalValue(rateControl, "BitrateLimit"), 4096);
        }

        var h264 = cfg.Elements().FirstOrDefault(e => e.Name.LocalName == "H264");
        if (h264 != null)
        {
            config.GovLength = string.IsNullOrEmpty(LocalValue(h264, "GovLength")) ? "30" : LocalValue(h264, "GovLength");
            config.H264Profile = string.IsNullOrEmpty(LocalValue(h264, "H264Profile")) ? "High" : LocalValue(h264, "H264Profile");
        }

        return config;
    }

    private static string LocalValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value ?? "";

    private static int ParseInt(string? val, int def) =>
        int.TryParse(val, out var r) ? r : def;

    private static double ParseDouble(string? val, double def) =>
        double.TryParse(val, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : def;

    private static bool ParseBool(string? val, bool def) =>
        bool.TryParse(val, out var r) ? r : def;
}
