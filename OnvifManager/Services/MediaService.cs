using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class MediaService
{
    private readonly OnvifClient _client;

    public MediaService(OnvifClient client) => _client = client;

    public async Task<List<CameraProfile>> GetProfilesAsync()
    {
        var profiles = new List<CameraProfile>();
        var bodyXml = "<trt:GetProfiles/>";

        var xml = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetProfiles", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var response = body.Element(OnvifXml.Ttrt + "GetProfilesResponse")
            ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetProfilesResponse");
        if (response == null) return profiles;

        foreach (var profileEl in response.Elements())
        {
            if (profileEl.Name.LocalName != "Profiles") continue;

            var profile = new CameraProfile
            {
                Token = profileEl.Attribute("token")?.Value ?? "",
                Name = profileEl.Element(OnvifXml.Tt + "Name")?.Value ?? "",
                Fixed = ParseBool(profileEl.Attribute("fixed")?.Value, false)
            };

            var vsConfig = profileEl.Element(OnvifXml.Tt + "VideoSourceConfiguration");
            if (vsConfig != null)
            {
                var vsToken = vsConfig.Element(OnvifXml.Tt + "SourceToken")?.Value;
                if (!string.IsNullOrEmpty(vsToken))
                    profile.VideoSourceToken = vsToken;
            }

            var veConfig = profileEl.Element(OnvifXml.Tt + "VideoEncoderConfiguration");
            if (veConfig != null)
                profile.VideoEncoderToken = veConfig.Attribute("token")?.Value ?? "";

            profiles.Add(profile);
        }

        _client.Camera.Profiles = profiles;
        return profiles;
    }

    public async Task<VideoEncoderConfig> GetVideoEncoderConfigurationAsync(string configToken)
    {
        var config = new VideoEncoderConfig();
        var bodyXml = $"<trt:GetVideoEncoderConfiguration><trt:ConfigurationToken>{configToken}</trt:ConfigurationToken></trt:GetVideoEncoderConfiguration>";

        var xml = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetVideoEncoderConfiguration", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var response = body.Element(OnvifXml.Ttrt + "GetVideoEncoderConfigurationResponse")
            ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetVideoEncoderConfigurationResponse");
        if (response == null) return config;

        var veConfig = response.Element(OnvifXml.Ttrt + "Configuration")
            ?? response.Element(OnvifXml.Tt + "Configuration");
        if (veConfig == null) return config;

        config.Token = veConfig.Attribute("token")?.Value ?? configToken;
        config.Name = veConfig.Element(OnvifXml.Tt + "Name")?.Value ?? "";
        config.Encoding = veConfig.Element(OnvifXml.Tt + "Encoding")?.Value ?? "H264";
        config.UseCount = ParseInt(veConfig.Attribute("UseCount")?.Value, 0);

        var resolution = veConfig.Element(OnvifXml.Tt + "Resolution");
        if (resolution != null)
        {
            config.Width = ParseInt(resolution.Element(OnvifXml.Tt + "Width")?.Value, 1920);
            config.Height = ParseInt(resolution.Element(OnvifXml.Tt + "Height")?.Value, 1080);
        }

        var rateControl = veConfig.Element(OnvifXml.Tt + "RateControl");
        if (rateControl != null)
        {
            config.FrameRateLimit = ParseInt(rateControl.Element(OnvifXml.Tt + "FrameRateLimit")?.Value, 30);
            config.EncodingInterval = ParseInt(rateControl.Element(OnvifXml.Tt + "EncodingInterval")?.Value, 1);
            config.BitrateLimit = ParseInt(rateControl.Element(OnvifXml.Tt + "BitrateLimit")?.Value, 4096);
        }

        var h264 = veConfig.Element(OnvifXml.Tt + "H264");
        if (h264 != null)
        {
            config.GovLength = h264.Element(OnvifXml.Tt + "GovLength")?.Value ?? "30";
            config.H264Profile = h264.Element(OnvifXml.Tt + "H264Profile")?.Value ?? "High";
        }

        return config;
    }

    public async Task<List<VideoEncoderConfig>> GetAllVideoEncoderConfigurationsAsync()
    {
        var configs = new List<VideoEncoderConfig>();
        var bodyXml = "<trt:GetVideoEncoderConfigurations/>";

        var xml = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetVideoEncoderConfigurations", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var response = body.Element(OnvifXml.Ttrt + "GetVideoEncoderConfigurationsResponse")
            ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetVideoEncoderConfigurationsResponse");
        if (response == null) return configs;

        foreach (var configEl in response.Elements())
        {
            if (configEl.Name.LocalName != "Configurations") continue;

            var config = new VideoEncoderConfig
            {
                Token = configEl.Attribute("token")?.Value ?? "",
                Name = configEl.Element(OnvifXml.Tt + "Name")?.Value ?? "",
                Encoding = configEl.Element(OnvifXml.Tt + "Encoding")?.Value ?? "H264",
                UseCount = ParseInt(configEl.Attribute("UseCount")?.Value, 0)
            };

            var resolution = configEl.Element(OnvifXml.Tt + "Resolution");
            if (resolution != null)
            {
                config.Width = ParseInt(resolution.Element(OnvifXml.Tt + "Width")?.Value, 1920);
                config.Height = ParseInt(resolution.Element(OnvifXml.Tt + "Height")?.Value, 1080);
            }

            var rateControl = configEl.Element(OnvifXml.Tt + "RateControl");
            if (rateControl != null)
            {
                config.FrameRateLimit = ParseInt(rateControl.Element(OnvifXml.Tt + "FrameRateLimit")?.Value, 30);
                config.EncodingInterval = ParseInt(rateControl.Element(OnvifXml.Tt + "EncodingInterval")?.Value, 1);
                config.BitrateLimit = ParseInt(rateControl.Element(OnvifXml.Tt + "BitrateLimit")?.Value, 4096);
            }

            var h264 = configEl.Element(OnvifXml.Tt + "H264");
            if (h264 != null)
            {
                config.GovLength = h264.Element(OnvifXml.Tt + "GovLength")?.Value ?? "30";
                config.H264Profile = h264.Element(OnvifXml.Tt + "H264Profile")?.Value ?? "High";
            }

            configs.Add(config);
        }

        return configs;
    }

    public async Task SetVideoEncoderConfigurationAsync(VideoEncoderConfig config)
    {
        var quality = config.Quality switch
        {
            VideoQualityType.ConstantBitrate => "CBR",
            VideoQualityType.VariableBitrate => "VBR",
            VideoQualityType.ConstantQuality => "CQ",
            _ => "CBR"
        };

        var bodyXml = $@"
<trt:SetVideoEncoderConfiguration>
  <trt:Configuration token=""{config.Token}"">
    <tt:Name>{config.Name}</tt:Name>
    <tt:UseCount>{config.UseCount}</tt:UseCount>
    <tt:Encoding>{config.Encoding}</tt:Encoding>
    <tt:Resolution>
      <tt:Width>{config.Width}</tt:Width>
      <tt:Height>{config.Height}</tt:Height>
    </tt:Resolution>
    <tt:Quality>{quality}</tt:Quality>
    <tt:RateControl>
      <tt:FrameRateLimit>{config.FrameRateLimit}</tt:FrameRateLimit>
      <tt:EncodingInterval>{config.EncodingInterval}</tt:EncodingInterval>
      <tt:BitrateLimit>{config.BitrateLimit}</tt:BitrateLimit>
    </tt:RateControl>
    <tt:H264>
      <tt:GovLength>{config.GovLength}</tt:GovLength>
      <tt:H264Profile>{config.H264Profile}</tt:H264Profile>
    </tt:H264>
    <tt:Multicast>
      <tt:Address>
        <tt:Type>IPv4</tt:Type>
        <tt:IPv4Address>0.0.0.0</tt:IPv4Address>
      </tt:Address>
      <tt:Port>0</tt:Port>
      <tt:TTL>0</tt:TTL>
      <tt:AutoStart>false</tt:AutoStart>
    </tt:Multicast>
    <tt:SessionTimeout>PT0S</tt:SessionTimeout>
  </trt:Configuration>
</trt:SetVideoEncoderConfiguration>";

        await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/SetVideoEncoderConfiguration", bodyXml);
    }

    public async Task<string> GetStreamUriAsync(string profileToken, string protocol = "RTSP")
    {
        var bodyXml = $@"
<trt:GetStreamUri>
  <trt:StreamSetup>
    <tt:Stream>RTP-Unicast</tt:Stream>
    <tt:Transport>
      <tt:Protocol>{protocol}</tt:Protocol>
    </tt:Transport>
  </trt:StreamSetup>
  <trt:ProfileToken>{profileToken}</trt:ProfileToken>
</trt:GetStreamUri>";

        var xml = await _client.SendSoapAsync(OnvifXml.MediaServicePath,
            "http://www.onvif.org/ver10/media/wsdl/GetStreamUri", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var response = body.Element(OnvifXml.Ttrt + "GetStreamUriResponse")
            ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetStreamUriResponse");
        if (response == null) return "";

        return response.Element(OnvifXml.Ttrt + "MediaUri")?.Element(OnvifXml.Tt + "Uri")?.Value
            ?? response.Elements().FirstOrDefault(e => e.Name.LocalName == "MediaUri")
                ?.Element(OnvifXml.Tt + "Uri")?.Value ?? "";
    }

    private static int ParseInt(string? val, int def) =>
        int.TryParse(val, out var r) ? r : def;

    private static bool ParseBool(string? val, bool def) =>
        bool.TryParse(val, out var r) ? r : def;
}
