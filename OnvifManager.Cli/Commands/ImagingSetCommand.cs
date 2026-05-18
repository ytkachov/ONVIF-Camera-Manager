using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class ImagingSetCommand
{
    public static Command Build()
    {
        var cmd = new Command("imaging", "Write imaging settings (merge specified fields into current)");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.VideoSourceToken);
        cmd.AddOption(CliOptions.ImgBrightness);
        cmd.AddOption(CliOptions.ImgContrast);
        cmd.AddOption(CliOptions.ImgSaturation);
        cmd.AddOption(CliOptions.ImgSharpness);
        cmd.AddOption(CliOptions.ImgIrCut);
        cmd.AddOption(CliOptions.ImgBacklight);
        cmd.AddOption(CliOptions.ImgExposure);
        cmd.AddOption(CliOptions.ImgWhiteBalance);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var token = ctx.ParseResult.GetValueForOption(CliOptions.VideoSourceToken)!;
            var brightness = ctx.ParseResult.GetValueForOption(CliOptions.ImgBrightness);
            var contrast = ctx.ParseResult.GetValueForOption(CliOptions.ImgContrast);
            var saturation = ctx.ParseResult.GetValueForOption(CliOptions.ImgSaturation);
            var sharpness = ctx.ParseResult.GetValueForOption(CliOptions.ImgSharpness);
            var irCut = ctx.ParseResult.GetValueForOption(CliOptions.ImgIrCut);
            var backlight = ctx.ParseResult.GetValueForOption(CliOptions.ImgBacklight);
            var exposure = ctx.ParseResult.GetValueForOption(CliOptions.ImgExposure);
            var whiteBalance = ctx.ParseResult.GetValueForOption(CliOptions.ImgWhiteBalance);

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var imaging = new ImagingService(provider.Get(CommandSupport.CreateCamera(conn)));
                var settings = await imaging.GetImagingSettingsAsync(token, ct);

                if (brightness.HasValue) settings.Brightness = brightness.Value;
                if (contrast.HasValue) settings.Contrast = contrast.Value;
                if (saturation.HasValue) settings.ColorSaturation = saturation.Value;
                if (sharpness.HasValue) settings.Sharpness = sharpness.Value;
                if (irCut.HasValue) settings.IrCutFilter = irCut.Value;
                if (!string.IsNullOrEmpty(backlight)) settings.BacklightCompensationMode = backlight;
                if (!string.IsNullOrEmpty(exposure)) settings.ExposureMode = exposure;
                if (!string.IsNullOrEmpty(whiteBalance)) settings.WhiteBalanceMode = whiteBalance;

                await imaging.SetImagingSettingsAsync(settings, ct);
                if (json) OutputFormatter.Write(new { ok = true, videoSource = token, applied = settings }, json: true);
                else OutputFormatter.Write($"ok: imaging settings updated for {token}", json: false);
                return 0;
            });
        });

        return cmd;
    }
}
