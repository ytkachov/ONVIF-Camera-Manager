using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class TimeGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("time", "Read system date/time, timezone, and sync source");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                var info = await device.GetSystemDateAndTimeAsync(ct);
                OutputFormatter.Write(info, json);
                return 0;
            });
        });

        return cmd;
    }
}
