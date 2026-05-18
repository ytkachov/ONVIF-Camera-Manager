using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class ImagingGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("imaging", "Read imaging settings (brightness, contrast, exposure, ...) for a video source");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.VideoSourceToken);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var token = ctx.ParseResult.GetValueForOption(CliOptions.VideoSourceToken)!;

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var imaging = new ImagingService(provider.Get(CommandSupport.CreateCamera(conn)));
                var settings = await imaging.GetImagingSettingsAsync(token, ct);
                OutputFormatter.Write(settings, json);
                return 0;
            });
        });

        return cmd;
    }
}
