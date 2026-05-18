using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class DeviceNameSetCommand
{
    public static Command Build()
    {
        var cmd = new Command("device-name", "Write device name (rewrites ONVIF name scope)");
        CliOptions.AddConnectionOptions(cmd);
        cmd.AddOption(CliOptions.StringValue);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            var value = ctx.ParseResult.GetValueForOption(CliOptions.StringValue)!;

            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                await device.SetDeviceNameAsync(value, ct);
                if (json) OutputFormatter.Write(new { ok = true, deviceName = value }, json: true);
                else OutputFormatter.Write($"ok: device-name set to {value}", json: false);
                return 0;
            });
        });

        return cmd;
    }
}
