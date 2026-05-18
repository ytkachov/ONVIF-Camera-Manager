using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class DnsGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("dns", "Read DNS configuration");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var device = new DeviceService(provider.Get(CommandSupport.CreateCamera(conn)));
                var dns = await device.GetDnsAsync(ct);
                OutputFormatter.Write(dns, json);
                return 0;
            });
        });

        return cmd;
    }
}
