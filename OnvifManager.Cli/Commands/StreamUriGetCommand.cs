using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class StreamUriGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("stream-uri", "Get RTSP/HTTP stream URI for a media profile");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.ProfileToken);
        cmd.AddOption(CliOptions.StreamProtocol);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var profile = ctx.ParseResult.GetValueForOption(CliOptions.ProfileToken)!;
            var protocol = ctx.ParseResult.GetValueForOption(CliOptions.StreamProtocol)!;

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var media = new MediaService(provider.Get(CommandSupport.CreateCamera(conn)));
                var uri = await media.GetStreamUriAsync(profile, protocol, ct);
                if (json) OutputFormatter.Write(new { uri }, json: true);
                else OutputFormatter.Write(uri, json: false);
                return 0;
            });
        });

        return cmd;
    }
}
