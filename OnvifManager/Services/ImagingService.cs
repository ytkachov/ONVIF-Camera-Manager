using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class ImagingService
{
    private readonly OnvifClient _client;

    public ImagingService(OnvifClient client) => _client = client;

    public async Task<ImagingSettings> GetImagingSettingsAsync(string videoSourceToken)
    {
        var settings = new ImagingSettings { VideoSourceToken = videoSourceToken };
        var bodyXml = $"<timg:GetImagingSettings><timg:VideoSourceToken>{videoSourceToken}</timg:VideoSourceToken></timg:GetImagingSettings>";

        var xml = await _client.SendSoapAsync(OnvifXml.ImagingServicePath,
            "http://www.onvif.org/ver10/imaging/wsdl/GetImagingSettings", bodyXml);

        var body = SoapMessageParser.ParseBody(xml);
        var response = body.Element(OnvifXml.Timg + "GetImagingSettingsResponse")
            ?? body.Elements().FirstOrDefault(e => e.Name.LocalName == "GetImagingSettingsResponse");
        if (response == null) return settings;

        var isettings = response.Element(OnvifXml.Timg + "ImagingSettings")
            ?? response.Element(OnvifXml.Tt + "ImagingSettings");
        if (isettings == null) return settings;

        settings.Brightness = ParseFloat(isettings.Element(OnvifXml.Tt + "Brightness")?.Value, 50f);
        settings.ColorSaturation = ParseFloat(isettings.Element(OnvifXml.Tt + "ColorSaturation")?.Value, 50f);
        settings.Contrast = ParseFloat(isettings.Element(OnvifXml.Tt + "Contrast")?.Value, 50f);
        settings.Sharpness = ParseFloat(isettings.Element(OnvifXml.Tt + "Sharpness")?.Value, 50f);

        var irCut = isettings.Element(OnvifXml.Tt + "IrCutFilter");
        if (irCut != null)
            settings.IrCutFilter = irCut.Value.Trim().Equals("ON", StringComparison.OrdinalIgnoreCase);

        var backlight = isettings.Element(OnvifXml.Tt + "BacklightCompensation");
        if (backlight != null)
            settings.BacklightCompensationMode = backlight.Element(OnvifXml.Tt + "Mode")?.Value ?? "OFF";

        var exposure = isettings.Element(OnvifXml.Tt + "Exposure");
        if (exposure != null)
            settings.ExposureMode = exposure.Element(OnvifXml.Tt + "Mode")?.Value ?? "AUTO";

        var wb = isettings.Element(OnvifXml.Tt + "WhiteBalance");
        if (wb != null)
            settings.WhiteBalanceMode = wb.Element(OnvifXml.Tt + "Mode")?.Value ?? "AUTO";

        return settings;
    }

    public async Task SetImagingSettingsAsync(ImagingSettings settings)
    {
        var irCut = settings.IrCutFilter ? "ON" : "OFF";
        // Use InvariantCulture to ensure dot decimal separator regardless of system locale
        var bodyXml = $@"
<timg:SetImagingSettings>
  <timg:VideoSourceToken>{settings.VideoSourceToken}</timg:VideoSourceToken>
  <timg:ImagingSettings>
    <tt:Brightness>{settings.Brightness.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}</tt:Brightness>
    <tt:ColorSaturation>{settings.ColorSaturation.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}</tt:ColorSaturation>
    <tt:Contrast>{settings.Contrast.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}</tt:Contrast>
    <tt:Sharpness>{settings.Sharpness.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}</tt:Sharpness>
    <tt:IrCutFilter>{irCut}</tt:IrCutFilter>
    <tt:BacklightCompensation>
      <tt:Mode>{settings.BacklightCompensationMode}</tt:Mode>
    </tt:BacklightCompensation>
    <tt:Exposure>
      <tt:Mode>{settings.ExposureMode}</tt:Mode>
    </tt:Exposure>
    <tt:WhiteBalance>
      <tt:Mode>{settings.WhiteBalanceMode}</tt:Mode>
    </tt:WhiteBalance>
  </timg:ImagingSettings>
</timg:SetImagingSettings>";

        await _client.SendSoapAsync(OnvifXml.ImagingServicePath,
            "http://www.onvif.org/ver10/imaging/wsdl/SetImagingSettings", bodyXml);
    }

    private static float ParseFloat(string? val, float def) =>
        float.TryParse(val, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : def;
}
