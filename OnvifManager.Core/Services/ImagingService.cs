using System.Globalization;
using System.Xml.Linq;
using OnvifManager.Models;

namespace OnvifManager.Services;

public class ImagingService
{
    private readonly OnvifClient _client;

    public ImagingService(OnvifClient client) => _client = client;

    public async Task<ImagingSettings> GetImagingSettingsAsync(string videoSourceToken,
        CancellationToken ct = default)
    {
        var settings = new ImagingSettings { VideoSourceToken = videoSourceToken };
        var body = new XElement(OnvifXml.Timg + "GetImagingSettings",
            new XElement(OnvifXml.Timg + "VideoSourceToken", videoSourceToken));

        var doc = await _client.SendSoapAsync(OnvifXml.ImagingServicePath,
            "http://www.onvif.org/ver10/imaging/wsdl/GetImagingSettings", body, ct);

        var response = SoapMessageParser.ParseBody(doc)
            .Elements().FirstOrDefault(e => e.Name.LocalName == "GetImagingSettingsResponse");
        if (response == null) return settings;

        var iset = response.Elements().FirstOrDefault(e => e.Name.LocalName == "ImagingSettings");
        if (iset == null) return settings;

        settings.Brightness = ParseFloat(LocalValue(iset, "Brightness"), 50f);
        settings.ColorSaturation = ParseFloat(LocalValue(iset, "ColorSaturation"), 50f);
        settings.Contrast = ParseFloat(LocalValue(iset, "Contrast"), 50f);
        settings.Sharpness = ParseFloat(LocalValue(iset, "Sharpness"), 50f);

        var irCut = LocalValue(iset, "IrCutFilter");
        if (!string.IsNullOrEmpty(irCut))
            settings.IrCutFilter = irCut.Trim().Equals("ON", StringComparison.OrdinalIgnoreCase);

        var backlight = iset.Elements().FirstOrDefault(e => e.Name.LocalName == "BacklightCompensation");
        if (backlight != null) settings.BacklightCompensationMode = LocalValue(backlight, "Mode");

        var exposure = iset.Elements().FirstOrDefault(e => e.Name.LocalName == "Exposure");
        if (exposure != null) settings.ExposureMode = LocalValue(exposure, "Mode");

        var wb = iset.Elements().FirstOrDefault(e => e.Name.LocalName == "WhiteBalance");
        if (wb != null) settings.WhiteBalanceMode = LocalValue(wb, "Mode");

        return settings;
    }

    public async Task SetImagingSettingsAsync(ImagingSettings settings, CancellationToken ct = default)
    {
        string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

        var body = new XElement(OnvifXml.Timg + "SetImagingSettings",
            new XElement(OnvifXml.Timg + "VideoSourceToken", settings.VideoSourceToken),
            new XElement(OnvifXml.Timg + "ImagingSettings",
                new XElement(OnvifXml.Tt + "Brightness", F(settings.Brightness)),
                new XElement(OnvifXml.Tt + "ColorSaturation", F(settings.ColorSaturation)),
                new XElement(OnvifXml.Tt + "Contrast", F(settings.Contrast)),
                new XElement(OnvifXml.Tt + "Sharpness", F(settings.Sharpness)),
                new XElement(OnvifXml.Tt + "IrCutFilter", settings.IrCutFilter ? "ON" : "OFF"),
                new XElement(OnvifXml.Tt + "BacklightCompensation",
                    new XElement(OnvifXml.Tt + "Mode", settings.BacklightCompensationMode)),
                new XElement(OnvifXml.Tt + "Exposure",
                    new XElement(OnvifXml.Tt + "Mode", settings.ExposureMode)),
                new XElement(OnvifXml.Tt + "WhiteBalance",
                    new XElement(OnvifXml.Tt + "Mode", settings.WhiteBalanceMode))));

        await _client.SendSoapAsync(OnvifXml.ImagingServicePath,
            "http://www.onvif.org/ver10/imaging/wsdl/SetImagingSettings", body, ct);
    }

    private static string LocalValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value ?? "";

    private static float ParseFloat(string? val, float def) =>
        float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : def;
}
