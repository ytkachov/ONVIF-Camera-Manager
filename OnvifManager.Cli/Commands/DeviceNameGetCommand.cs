using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class DeviceNameGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("device-name", "Read device name (from ONVIF name scope)");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                var name = await device.GetDeviceNameAsync(ct);
                if (json) OutputFormatter.Write(new { deviceName = name }, json: true);
                else OutputFormatter.Write(name, json: false);
                return 0;
            });
        });

        return cmd;
    }
}
