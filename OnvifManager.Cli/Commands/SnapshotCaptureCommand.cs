using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class SnapshotCaptureCommand
{
    public static Command Build()
    {
        var cmd = new Command("snapshot", "Capture a JPEG snapshot from a media profile and save to disk");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.ProfileToken);
        cmd.AddOption(CliOptions.SnapshotOutDir);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var profile = ctx.ParseResult.GetValueForOption(CliOptions.ProfileToken)!;
            var outDir = ctx.ParseResult.GetValueForOption(CliOptions.SnapshotOutDir);

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var snapshot = new SnapshotService(provider);
                var camera = CommandSupport.CreateCamera(conn);
                var dir = string.IsNullOrEmpty(outDir) ? Environment.CurrentDirectory : outDir!;
                var result = await snapshot.CaptureAsync(camera, profile, dir, ct);
                OutputFormatter.Write(result, json);
                return 0;
            });
        });

        return cmd;
    }
}
