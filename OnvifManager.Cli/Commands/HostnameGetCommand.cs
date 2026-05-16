using System.CommandLine;
using System.CommandLine.Invocation;
using OnvifManager.Cli.Output;
using OnvifManager.Services;

namespace OnvifManager.Cli.Commands;

internal static class HostnameGetCommand
{
    public static Command Build()
    {
        var cmd = new Command("hostname", "Read ONVIF Hostname value");
        CliOptions.AddConnectionOptions(cmd);

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(CliOptions.Json);
            ctx.ExitCode = await CommandSupport.RunAsync(ctx, requireCredentials: true, async (conn, ct) =>
            {
                using var provider = CommandSupport.CreateProvider(conn);
                var client = provider.Get(CommandSupport.CreateCamera(conn));
                var device = new DeviceService(client);
                var hostname = await device.GetHostnameAsync(ct);

                if (json)
                    OutputFormatter.Write(new { hostname }, json: true);
                else
                    OutputFormatter.Write(hostname, json: false);
                return 0;
            });
        });

        return cmd;
    }
}
