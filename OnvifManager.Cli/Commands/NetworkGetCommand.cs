using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class NetworkGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("network", "Read network interface configuration (IPv4, DHCP, MTU, MAC)");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                var interfaces = await device.GetNetworkInterfacesAsync(ct);
                OutputFormatter.Write(interfaces, json);
                return 0;
            });
        });

        return cmd;
    }
}
