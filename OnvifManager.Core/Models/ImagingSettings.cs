namespace OnvifManager.Models;

public class ImagingSettings
{
    public string VideoSourceToken { get; set; } = string.Empty;
    public float Brightness { get; set; } = 50f;
    public float ColorSaturation { get; set; } = 50f;
    public float Contrast { get; set; } = 50f;
    public float Sharpness { get; set; } = 50f;
    public bool IrCutFilter { get; set; } = true;
    public string BacklightCompensationMode { get; set; } = "OFF";
    public string ExposureMode { get; set; } = "AUTO";
    public string WhiteBalanceMode { get; set; } = "AUTO";
}
