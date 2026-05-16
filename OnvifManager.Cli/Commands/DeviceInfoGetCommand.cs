using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class DeviceInfoGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("device-info", "Read manufacturer, model, firmware, serial, hardware id");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var camera = CommandSupport.CreateCamera(conn);
                var client = provider.Get(camera);
                var device = new DeviceService(client);
                await device.GetDeviceInformationAsync(ct);

                OutputFormatter.Write(new
                {
                    camera.Manufacturer,
                    camera.Model,
                    camera.FirmwareVersion,
                    camera.SerialNumber,
                    camera.HardwareId,
                    camera.IpAddress,
                    camera.Port
                }, json);
                return 0;
            });
        });

        return cmd;
    }
}
