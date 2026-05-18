using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class EncoderSetCommand
{
    public static Command Build()
    {
        var cmd = new Command("encoder", "Write video encoder configuration (merge specified fields)");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.EncoderToken);
        cmd.AddOption(CliOptions.EncoderWidth);
        cmd.AddOption(CliOptions.EncoderHeight);
        cmd.AddOption(CliOptions.EncoderBitrate);
        cmd.AddOption(CliOptions.EncoderFps);
        cmd.AddOption(CliOptions.EncoderGov);
        cmd.AddOption(CliOptions.EncoderH264Profile);
        cmd.AddOption(CliOptions.EncoderQuality);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var token = ctx.ParseResult.GetValueForOption(CliOptions.EncoderToken)!;
            var width = ctx.ParseResult.GetValueForOption(CliOptions.EncoderWidth);
            var height = ctx.ParseResult.GetValueForOption(CliOptions.EncoderHeight);
            var bitrate = ctx.ParseResult.GetValueForOption(CliOptions.EncoderBitrate);
            var fps = ctx.ParseResult.GetValueForOption(CliOptions.EncoderFps);
            var gov = ctx.ParseResult.GetValueForOption(CliOptions.EncoderGov);
            var h264Profile = ctx.ParseResult.GetValueForOption(CliOptions.EncoderH264Profile);
            var quality = ctx.ParseResult.GetValueForOption(CliOptions.EncoderQuality);

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var media = new MediaService(provider.Get(CommandSupport.CreateCamera(conn)));
                var configs = await media.GetAllVideoEncoderConfigurationsAsync(ct);
                var cfg = configs.FirstOrDefault(c => c.Token == token);
                if (cfg == null)
                {
                    OutputFormatter.WriteError($"encoder configuration '{token}' not found (available: {string.Join(", ", configs.Select(c => c.Token))})", 1, json);
                    return 1;
                }

                if (width.HasValue) cfg.Width = width.Value;
                if (height.HasValue) cfg.Height = height.Value;
                if (bitrate.HasValue) cfg.BitrateLimit = bitrate.Value;
                if (fps.HasValue) cfg.FrameRateLimit = fps.Value;
                if (!string.IsNullOrEmpty(gov)) cfg.GovLength = gov;
                if (!string.IsNullOrEmpty(h264Profile)) cfg.H264Profile = h264Profile;
                if (!string.IsNullOrEmpty(quality))
                {
                    cfg.Quality = quality.ToUpperInvariant() switch
                    {
                        "CBR" => VideoQualityType.ConstantBitrate,
                        "VBR" => VideoQualityType.VariableBitrate,
                        "CQ" => VideoQualityType.ConstantQuality,
                        _ => throw new ArgumentException($"unknown quality mode '{quality}' (expected CBR, VBR, or CQ)")
                    };
                }

                await media.SetVideoEncoderConfigurationAsync(cfg, ct);
                if (json) OutputFormatter.Write(new { ok = true, token = cfg.Token, applied = cfg }, json: true);
                else OutputFormatter.Write($"ok: encoder {cfg.Token} updated", json: false);
                return 0;
            });
        });

        return cmd;
    }
}
